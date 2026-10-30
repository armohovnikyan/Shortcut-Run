using System;
using UnityEngine;

/// <summary>
/// Top-level game flow — the one place the UI talks to.
/// Start: load the level and prepare the run = the menu (level, runners idle, camera on the player).
/// Play → countdown → race. After the run: claim the reward, then Replay (same level) or NextLevel.
/// Run details (countdown, places, multipliers) come from RunManager's events; UI listens to those directly.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private RunManager runManager;

    [Header("Economy")]
    [SerializeField] private RewardTableSO rewardTable;
    [Tooltip("Coins a new player starts with. Only used when there is no save yet.")]
    [SerializeField, Min(0)] private int startingCoins;

    [Header("Upgrades")]
    [SerializeField] private UpgradeSO boardUpgrade;
    [SerializeField] private UpgradeSO offlineEarnUpgrade;

    [Header("Buffs")]
    [SerializeField] private RunBoostSO runBoost;
    [Tooltip("Which magnet the magnet button uses. One Shot = one sweep; Timed = stays on for its duration. Set before Play.")]
    [SerializeField] private MagnetKind magnet = MagnetKind.OneShot;
    [SerializeField] private BoardMagnetSO boardMagnet;
    [SerializeField] private TimedMagnetSO timedMagnet;

    [Tooltip("Print rewards and the wallet to the Console (no UI needed to test).")]
    [SerializeField] private bool logEvents = true;

    public RunManager Run => runManager;
    /// <summary>The save: money, upgrades, settings, name, levels passed. Systems are handed it, never load it themselves.</summary>
    public GameSave Save { get; private set; }
    public Wallet Wallet { get; private set; }
    public PlayerProfile Profile { get; private set; }
    public GameSettings Settings { get; private set; }
    public Upgrade BoardUpgrade { get; private set; }
    public Upgrade OfflineEarnUpgrade { get; private set; }
    public Buff RunBoost { get; private set; }
    /// <summary>The magnet in use — the one-shot or the timed one, picked by the Magnet switch.</summary>
    public Buff BoardMagnet { get; private set; }
    /// <summary>Level the player is on, 1 = first. Levels passed = LevelNumber - 1.</summary>
    public int LevelNumber => Save.Data.levelIndex + 1;
    /// <summary>Coins earned by the last run, waiting to be claimed. 0 = nothing to claim.</summary>
    public int PendingReward { get; private set; }

    /// <summary>The menu is up: level loaded, runners waiting for Play.</summary>
    public event Action MenuShown;
    /// <summary>The player finished: open the reward panel. Result + coins to claim.</summary>
    public event Action<RunResult, int> RewardReady;
    /// <summary>The player fell during the race: open the game-over panel.</summary>
    public event Action GameOver;

    private void Awake()
    {
        Save = GameSave.Load(startingCoins);
        Wallet = new Wallet(Save);
        Profile = new PlayerProfile(Save);
        Settings = new GameSettings(Save);

        if (boardUpgrade == null || offlineEarnUpgrade == null)
            Debug.LogError($"{name}: Game Manager is missing an Upgrade asset — that upgrade can't be bought.", this);
        BoardUpgrade = new Upgrade(boardUpgrade, Save);
        OfflineEarnUpgrade = new Upgrade(offlineEarnUpgrade, Save);

        BoardMagnetSO magnetAsset = magnet == MagnetKind.Timed ? timedMagnet : boardMagnet;
        if (runBoost == null || magnetAsset == null)
            Debug.LogError($"{name}: Game Manager is missing a Buff asset — that buff can't be bought or used.", this);
        RunBoost = new Buff(runBoost, Save);
        BoardMagnet = new Buff(magnetAsset, Save);
        Log($"magnet in use: {magnet} ({(magnetAsset != null ? magnetAsset.name : "none")})");
    }

    // Buff effects and cooldowns only run while the run does (scaled time: a paused run freezes them too).
    private void Update()
    {
        if (runManager.State != RunState.Racing && runManager.State != RunState.Bonus) return;

        RunBoost.Tick(Time.deltaTime);
        BoardMagnet.Tick(Time.deltaTime);
    }

    private void OnDestroy() => Resume(); // never leave the editor / next scene frozen

    private void OnEnable()  => runManager.RunEnded += OnRunEnded;
    private void OnDisable() => runManager.RunEnded -= OnRunEnded;

    private void Start() => ShowMenu(LoadCurrentLevel());

    // ---------- UI entry points ----------

    /// <summary>PLAY button.</summary>
    public void Play() => runManager.StartRun();

    /// <summary>Reward panel "Claim" button.</summary>
    public void ClaimReward() => ClaimReward(1);

    /// <summary>Claim with an extra factor (e.g. ×2 for watching an ad).</summary>
    public void ClaimReward(int factor)
    {
        if (PendingReward <= 0) return;

        Wallet.Add(PendingReward * Mathf.Max(1, factor));
        PendingReward = 0;
        Log($"claimed — wallet: {Wallet.Coins}");
    }

    /// <summary>Upgrade button: pays from the wallet and levels the upgrade up. False = can't afford / maxed.</summary>
    public bool BuyUpgrade(Upgrade upgrade) => upgrade.TryBuy(Wallet);

    /// <summary>Menu buff button: buys one piece. False = can't afford.</summary>
    public bool BuyBuff(Buff buff) => buff.TryBuy(Wallet);

    /// <summary>The run lets the player use a buff right now: racing and still in it.</summary>
    public bool CanUseBuffs => runManager.State == RunState.Racing
                               && runManager.Player != null && !runManager.Player.HasFallen;

    /// <summary>In-run buff button: spends one piece on the player. False = not racing, none left, or cooling down.</summary>
    public bool UseBuff(Buff buff)
    {
        if (!CanUseBuffs || !buff.TryUse(runManager.Player)) return false;

        Log($"buff used — {buff.Count} left");
        return true;
    }

    /// <summary>Freezes the run (the "Back to menu?" question).</summary>
    public void Pause() => Time.timeScale = 0f;

    public void Resume() => Time.timeScale = 1f;

    /// <summary>Same level again (game-over panel, or leaving a run to the menu).</summary>
    public void Replay()
    {
        Resume();
        ClaimReward(); // never lose coins that were earned but not claimed
        runManager.Cleanup();
        ShowMenu(LoadCurrentLevel());
    }

    /// <summary>Next level (reward panel "Continue").</summary>
    public void NextLevel()
    {
        Resume();
        ClaimReward();
        runManager.Cleanup();
        // The level counts as passed only when the player moves on (Continue), not when the reward shows.
        Save.Data.levelIndex++;
        Save.Save();
        ShowMenu(LoadCurrentLevel());
    }

    // ---------- Flow ----------

    private Level LoadCurrentLevel() => levelLoader.Load(Save.Data.levelIndex);

    private void ShowMenu(Level level)
    {
        if (level == null) return;
        RunBoost.ResetRun();
        BoardMagnet.ResetRun();
        runManager.Prepare(level);
        MenuShown?.Invoke();
    }

    private void OnRunEnded(RunResult result)
    {
        if (!result.Finished)
        {
            Log("game over — no reward");
            GameOver?.Invoke();
            return;
        }

        if (rewardTable == null)
            Debug.LogError($"{name}: Game Manager has no Reward Table — every run pays 0 coins.", this);

        PendingReward = RewardCalculator.Calculate(result, rewardTable);
        Log($"reward: {PendingReward} coins (place {result.Place}, x{result.Multiplier}, {result.Boards} boards)");
        RewardReady?.Invoke(result, PendingReward);
    }

    private void Log(string message)
    {
        if (logEvents) Debug.Log($"[Game] {message}", this);
    }
}
