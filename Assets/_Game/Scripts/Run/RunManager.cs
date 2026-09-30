using System;
using System.Collections;
using UnityEngine;

// Idle = nothing prepared. Ready = the menu: level + runners in place, waiting for Play.
public enum RunState { Idle, Ready, Countdown, Racing, Bonus, Ended }

/// <summary>What the run produced. RewardCalculator turns it into coins.</summary>
public struct RunResult
{
    public bool Finished;   // false = the player fell during the race: game over, no reward
    public int Place;       // 1 = first
    public int Multiplier;  // 1 = finish platform; raised by the bonus round (1st place only)
    public int Boards;      // boards in hand when the finish flow started (0 after a bonus fall)
}

/// <summary>
/// Runs ONE race on a level it's given (by GameManager): Prepare = runners spawned and idle (the menu),
/// StartRun = PLAY pressed → countdown → race → (bonus) → ended.
/// Knows nothing about UI, camera, coins or level loading — it only raises events; those systems listen.
/// </summary>
public class RunManager : MonoBehaviour
{
    [SerializeField] private Player playerPrefab;
    [Tooltip("Models dealt to the NPCs, a different one each until they run out. Empty = NPCs keep their prefab model.")]
    [SerializeField] private GameObject[] npcSkins;
    [SerializeField, Min(0)] private int countdownSeconds = 3;
    [Tooltip("How often race places are recalculated, in seconds.")]
    [SerializeField, Min(0.02f)] private float standingsInterval = 0.2f;
    [Tooltip("Bonus only: seconds the player keeps falling before being dragged back to the last platform.")]
    [SerializeField, Min(0f)] private float bonusFallDelay = 0.6f;

    [Tooltip("Print countdown, places, finishes and the result to the Console (no UI needed to test).")]
    [SerializeField] private bool logEvents = true;

    private Level level;
    private RunnerSpawner spawner;
    private RaceStandings standings;
    private Countdown countdown;
    private BonusRound bonus;
    private float standingsTimer;
    private int lastPlayerPlace;

    public RunState State { get; private set; }
    public Player Player => spawner?.Player;
    /// <summary>Current countdown, for UI that wants smooth progress (Remaining / Progress01).</summary>
    public Countdown Countdown => countdown;

    public event Action<Player> PlayerSpawned;
    public event Action<int> CountdownTick;           // 3, 2, 1
    public event Action RaceStarted;                  // "GO!"
    public event Action<int> PlayerPlaceChanged;      // live place during the race
    public event Action<Runner, int> RunnerFinished;  // runner, finishing place
    public event Action BonusStarted;                 // player came 1st: platforms are rising
    public event Action<int> MultiplierReached;       // 2 … 15
    public event Action<RunResult> RunEnded;          // player reached its stand point, or fell in the race

    private void OnDestroy() => Unsubscribe();

    // ---------- Public API (called by GameManager) ----------

    /// <summary>The menu: spawns the runners on the level, idle, and waits for StartRun().</summary>
    public void Prepare(Level newLevel)
    {
        Cleanup();
        level = newLevel;

        spawner = new RunnerSpawner(level, playerPrefab, npcSkins);
        Player player = spawner.SpawnAll();
        if (player == null) return;

        standings = new RaceStandings(level, spawner.Runners);
        if (level.FinishLine != null) level.FinishLine.RunnerCrossed += OnRunnerCrossed;
        else Debug.LogError($"{level.name}: Level has no Finish Line — nobody can finish.", level);
        foreach (Runner runner in spawner.Runners) runner.Fell += OnRunnerFell;

        State = RunState.Ready;
        lastPlayerPlace = 0;
        PlayerSpawned?.Invoke(player);
        Log($"prepared {level.name}: {spawner.Runners.Count} runners");
    }

    /// <summary>PLAY pressed: countdown, then the race.</summary>
    public void StartRun()
    {
        if (State != RunState.Ready || Player == null) return;

        State = RunState.Countdown;
        countdown = new Countdown(countdownSeconds);
        countdown.Ticked += seconds => { Log(seconds.ToString()); CountdownTick?.Invoke(seconds); };
        countdown.Finished += BeginRace;
        countdown.Start();
    }

    /// <summary>Removes this run's runners. The level itself belongs to LevelLoader.</summary>
    public void Cleanup()
    {
        StopAllCoroutines(); // a pending bonus drag-back must not act on destroyed runners
        Unsubscribe();
        spawner?.DestroyAll();
        spawner = null;
        standings = null;
        countdown = null;
        bonus = null;
        level = null;
        State = RunState.Idle;
    }

    // ---------- Flow ----------

    private void Update()
    {
        if (State == RunState.Countdown) countdown.Tick(Time.deltaTime);
        else if (State == RunState.Racing) UpdateStandings(false);
        else if (State == RunState.Bonus && bonus.Tick(Player))
            FinishPlayer(1, bonus.Multiplier, Player.BoardCount, bonus.StandPoint, false);
    }

    private void BeginRace()
    {
        State = RunState.Racing;
        foreach (Runner runner in spawner.Runners) runner.BeginRace();
        Log("GO!");
        RaceStarted?.Invoke();
        UpdateStandings(true);
    }

    private void UpdateStandings(bool now)
    {
        standingsTimer -= Time.deltaTime;
        if (!now && standingsTimer > 0f) return;
        standingsTimer = standingsInterval;

        standings.Refresh();
        int place = standings.GetPlace(Player);
        if (place == lastPlayerPlace) return;
        lastPlayerPlace = place;
        Log($"player place: {place}");
        PlayerPlaceChanged?.Invoke(place);
    }

    private void OnRunnerCrossed(Runner runner)
    {
        // NPCs still finish while the player is in the bonus; nothing counts once the run is over.
        if (State != RunState.Racing && State != RunState.Bonus) return;

        int place = standings.MarkFinished(runner);
        Log($"{runner.name} finished #{place}");
        RunnerFinished?.Invoke(runner, place);

        if (runner == Player) OnPlayerFinished(place);
        else runner.FinishAt(StandPointFor(place), place);
    }

    private void OnPlayerFinished(int place)
    {
        // 1st place keeps running into the bonus round (finish = x1). Everyone else finishes here.
        if (place == 1 && level.BonusPlatforms != null)
            StartBonus();
        else
            FinishPlayer(place, 1, Player.BoardCount, StandPointFor(place), false);
    }

    private void StartBonus()
    {
        State = RunState.Bonus;
        level.BonusPlatforms.Spawn();

        bonus = new BonusRound(StandPointFor(1), level.BonusPlatforms.LastMultiplier);
        bonus.MultiplierReached += multiplier =>
        {
            Log($"reached x{multiplier}");
            MultiplierReached?.Invoke(multiplier);
        };

        Log("1st place — bonus round");
        BonusStarted?.Invoke();
    }

    // The finish flow. RunEnded (reward UI) fires once the player stands on its point and the finish animation starts.
    private void FinishPlayer(int place, int multiplier, int boards, Transform standPoint, bool dragged)
    {
        State = RunState.Ended;
        var result = new RunResult { Finished = true, Place = place, Multiplier = multiplier, Boards = boards };

        if (dragged) Player.DragTo(standPoint, place, () => EndRun(result));
        else Player.FinishAt(standPoint, place, () => EndRun(result));
    }

    private void OnRunnerFell(Runner runner)
    {
        standings.MarkOut(runner);
        if (runner != Player) return;

        if (State == RunState.Racing)
        {
            // Race fall: game over, no reward.
            State = RunState.Ended;
            EndRun(new RunResult { Finished = false, Place = standings.GetPlace(runner) });
        }
        else if (State == RunState.Bonus)
        {
            // Bonus fall: back to the last reached platform, no board bonus.
            State = RunState.Ended;
            StartCoroutine(DragBackAfterFall());
        }
    }

    private IEnumerator DragBackAfterFall()
    {
        Log($"fell in the bonus — back to x{bonus.Multiplier}");
        yield return new WaitForSeconds(bonusFallDelay);
        FinishPlayer(1, bonus.Multiplier, 0, bonus.StandPoint, true);
    }

    private void EndRun(RunResult result)
    {
        Log(result.Finished
            ? $"RUN ENDED: place {result.Place}, x{result.Multiplier}, boards {result.Boards}"
            : "RUN ENDED: player fell - game over");
        RunEnded?.Invoke(result);
    }

    private void Log(string message)
    {
        if (logEvents) Debug.Log($"[Run] {message}", this);
    }

    // More finishers than stand points: the extras share the last one.
    private Transform StandPointFor(int place)
    {
        Transform[] points = level.StandPoints;
        if (points == null || points.Length == 0) return level.FinishLine.transform;
        return points[Mathf.Clamp(place - 1, 0, points.Length - 1)];
    }

    private void Unsubscribe()
    {
        if (level != null && level.FinishLine != null) level.FinishLine.RunnerCrossed -= OnRunnerCrossed;
        if (spawner == null) return;
        foreach (Runner runner in spawner.Runners)
            if (runner != null) runner.Fell -= OnRunnerFell;
    }
}
