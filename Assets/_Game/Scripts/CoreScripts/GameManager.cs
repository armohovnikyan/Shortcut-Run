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
    [Tooltip("Coins a new player starts with. Only used the first time the game runs on a device.")]
    [SerializeField, Min(0)] private int startingCoins;

    [Header("Upgrades")]
    [SerializeField] private UpgradeSO boardUpgrade;
    [SerializeField] private UpgradeSO offlineEarnUpgrade;

    [Tooltip("Print rewards and the wallet to the Console (no UI needed to test).")]
    [SerializeField] private bool logEvents = true;

    public RunManager Run => runManager;
    public Wallet Wallet { get; private set; }
    public PlayerProfile Profile { get; private set; }
    public GameSettings Settings { get; private set; }
    public Upgrade BoardUpgrade { get; private set; }
    public Upgrade OfflineEarnUpgrade { get; private set; }
    public int LevelNumber => levelLoader.LevelIndex + 1;
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
        Wallet = new Wallet(startingCoins);
        Profile = new PlayerProfile();
        Settings = new GameSettings();

        if (boardUpgrade == null || offlineEarnUpgrade == null)
            Debug.LogError($"{name}: Game Manager is missing an Upgrade asset — that upgrade can't be bought.", this);
        BoardUpgrade = new Upgrade(boardUpgrade);
        OfflineEarnUpgrade = new Upgrade(offlineEarnUpgrade);
    }

    private void OnDestroy() => Resume(); // never leave the editor / next scene frozen

    private void OnEnable()  => runManager.RunEnded += OnRunEnded;
    private void OnDisable() => runManager.RunEnded -= OnRunEnded;

    private void Start() => ShowMenu(levelLoader.LoadCurrent());

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

    /// <summary>Freezes the run (the "Back to menu?" question).</summary>
    public void Pause() => Time.timeScale = 0f;

    public void Resume() => Time.timeScale = 1f;

    /// <summary>Same level again (game-over panel, or leaving a run to the menu).</summary>
    public void Replay()
    {
        Resume();
        ClaimReward(); // never lose coins that were earned but not claimed
        runManager.Cleanup();
        ShowMenu(levelLoader.LoadCurrent());
    }

    /// <summary>Next level (reward panel "Continue").</summary>
    public void NextLevel()
    {
        Resume();
        ClaimReward();
        runManager.Cleanup();
        ShowMenu(levelLoader.LoadNext());
    }

    // ---------- Flow ----------

    private void ShowMenu(Level level)
    {
        if (level == null) return;
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
