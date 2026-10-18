using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides WHEN the world effects play: listens to the run and spawns the matching effect — the same job
/// GameAudio does for sounds. Connected by GameBootstrap (Bind); nothing in the game knows effects exist.
/// </summary>
public class GameEffects : MonoBehaviour
{
    [Header("Water splash (race fall = game over)")]
    [SerializeField] private ParticleSystem waterSplashPrefab;
    [Tooltip("Only when GameBootstrap has no Water: the splash plays this far below the point where the player left the road.")]
    [SerializeField, Min(0f)] private float waterDepth = 2.5f;
    [Tooltip("Seconds before a spawned splash is removed. Must be longer than its longest particle.")]
    [SerializeField, Min(0.1f)] private float splashLifetime = 3f;

    [Header("Board pickup")]
    [Tooltip("Plays on the stack of any runner that picks up boards. Should destroy itself when done (Stop Action).")]
    [SerializeField] private ParticleSystem pickupEffectPrefab;
    [Tooltip("The \"+N\" over the player's stack. Created once when bound.")]
    [SerializeField] private PickupCounter pickupCounterPrefab;

    [Header("Fast run")]
    [Tooltip("Rides on the player and streams out behind it while the board boost is on. Its emission is switched " +
             "on and off from here, so the prefab should start with emission off.")]
    [SerializeField] private ParticleSystem fastRunTrailPrefab;

    private InfiniteWater water; // the splash plays where the falling player crosses its surface
    private Transform view;      // the game camera: the +N faces it
    private RunManager run;
    private Player player;
    private PickupCounter pickupCounter;
    private ParticleSystem fastRunTrail; // child of the current player: destroyed with it
    private bool inBonus;
    private Coroutine splashRoutine;
    private readonly List<(Runner runner, Action<int, int> picked, Action<int, int> stolen)> boardHandlers = new();

    /// <summary>Called once by GameBootstrap. water and view may be null (then Water Depth is used / the +N doesn't turn).</summary>
    public void Bind(GameManager game, InfiniteWater water, Transform view)
    {
        this.water = water;
        this.view = view;
        if (waterSplashPrefab == null)
            Debug.LogWarning($"{name}: Game Effects has no Water Splash prefab — falls won't splash.", this);
        if (pickupCounterPrefab != null) pickupCounter = Instantiate(pickupCounterPrefab, transform);

        run = game.Run;
        run.PlayerSpawned += OnPlayerSpawned;
        run.BonusStarted += OnBonusStarted;

        // GameManager may have spawned the player before Bind ran (Start order isn't fixed).
        if (run.Player != null) OnPlayerSpawned(run.Player);
    }

    private void OnDestroy()
    {
        if (run != null)
        {
            run.PlayerSpawned -= OnPlayerSpawned;
            run.BonusStarted -= OnBonusStarted;
        }
        ReleasePlayer();
    }

    // A new run (start, Replay, Next level): new player and new opponents.
    private void OnPlayerSpawned(Player spawned)
    {
        ReleasePlayer();
        inBonus = false;
        player = spawned;
        player.Fell += OnPlayerFell;

        foreach (Runner runner in run.Runners)
        {
            Runner picker = runner;
            Action<int, int> picked = (added, _) => OnBoardsAdded(picker, added, true);
            Action<int, int> stolen = (added, _) => OnBoardsAdded(picker, added, false);
            picker.BoardsPickedUp += picked;
            picker.BoardsStolen += stolen;
            boardHandlers.Add((picker, picked, stolen));
        }
        if (pickupCounter != null) pickupCounter.Follow(player, view);

        if (fastRunTrailPrefab != null)
        {
            fastRunTrail = Instantiate(fastRunTrailPrefab, player.transform); // keeps the prefab's offset from the pivot
            SetFastRun(false);
            player.BoostChanged += SetFastRun;
        }
    }

    private void ReleasePlayer()
    {
        if (player != null)
        {
            player.Fell -= OnPlayerFell;
            player.BoostChanged -= SetFastRun;
        }
        player = null;
        fastRunTrail = null;
        if (splashRoutine != null) StopCoroutine(splashRoutine);
        splashRoutine = null;

        foreach (var (runner, picked, stolen) in boardHandlers)
        {
            if (runner == null) continue;
            runner.BoardsPickedUp -= picked;
            runner.BoardsStolen -= stolen;
        }
        boardHandlers.Clear();
    }

    // Boost on (placing boards, running on a bridge, until back on normal road): stream; off: let the rest fade.
    private void SetFastRun(bool on)
    {
        if (fastRunTrail == null) return;
        ParticleSystem.EmissionModule emission = fastRunTrail.emission;
        emission.enabled = on;
    }

    // Every runner gets the sparks on its stack; only the player gets the "+N", and only for boards picked up
    // from the track — not for the ones taken from a runner it knocked out.
    private void OnBoardsAdded(Runner runner, int added, bool pickedUp)
    {
        // Under the runner, not the stack point: the stack point is scaled (narrow boards), which would squash the sparks.
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, runner.BoardStackTop, runner.transform.rotation, runner.transform);

        if (pickedUp && runner == player && pickupCounter != null) pickupCounter.Add(added);
    }

    private void OnBonusStarted() => inBonus = true;

    // The race ends over water, the bonus over the void — only the race fall splashes.
    private void OnPlayerFell(Runner runner)
    {
        if (inBonus || waterSplashPrefab == null) return;
        float waterY = water != null ? water.SurfaceY : runner.transform.position.y - waterDepth;
        splashRoutine = StartCoroutine(SplashAtWater(runner.transform, waterY));
    }

    private IEnumerator SplashAtWater(Transform faller, float waterY)
    {
        while (faller != null && faller.position.y > waterY) yield return null;
        splashRoutine = null;
        if (faller == null) yield break;

        Vector3 at = faller.position;
        at.y = waterY;
        ParticleSystem splash = Instantiate(waterSplashPrefab, at, Quaternion.identity);
        Destroy(splash.gameObject, splashLifetime);
    }
}
