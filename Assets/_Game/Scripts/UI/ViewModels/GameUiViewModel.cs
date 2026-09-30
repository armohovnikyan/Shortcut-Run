using System;
using UnityEngine;

// The one full-screen state of the UI. Settings and the "Back to menu?" question are overlays on top of it.
public enum UiScreen { Menu, Race, Reward, GameOver }

/// <summary>
/// The UI's view of the game (the "VM" of MVVM). It listens to GameManager / RunManager and turns their
/// events into plain values the views show; button presses come back in as the methods at the bottom.
/// Views never touch GameManager — they only know this class and its small child view models.
/// No Unity UI types here, so the whole UI flow can be read (and tested) without opening the canvas.
/// </summary>
public class GameUiViewModel : IDisposable
{
    private readonly GameManager game;
    private readonly RunManager run;

    public readonly Observable<UiScreen> Screen = new Observable<UiScreen>(UiScreen.Menu);
    public readonly Observable<bool> SettingsOpen = new Observable<bool>();
    public readonly Observable<bool> ExitConfirmOpen = new Observable<bool>();
    public readonly Observable<int> Coins = new Observable<int>();
    public readonly Observable<string> PlayerName = new Observable<string>();
    /// <summary>The spawned player, for UI that follows it in the world (the name title). Null between levels.</summary>
    public readonly Observable<Transform> PlayerTarget = new Observable<Transform>();

    public HudViewModel Hud { get; } = new HudViewModel();
    public RewardViewModel Reward { get; } = new RewardViewModel();
    public LevelProgressViewModel LevelProgress { get; } = new LevelProgressViewModel();
    public SettingsViewModel Settings { get; }
    public UpgradeViewModel BoardUpgrade { get; }
    public UpgradeViewModel OfflineEarnUpgrade { get; }

    public GameUiViewModel(GameManager game)
    {
        this.game = game;
        run = game.Run;

        Settings = new SettingsViewModel(game.Settings);
        BoardUpgrade = new UpgradeViewModel(game, game.BoardUpgrade);
        OfflineEarnUpgrade = new UpgradeViewModel(game, game.OfflineEarnUpgrade);

        // Current state first: the game may already be showing the menu when the UI is created.
        Coins.Value = game.Wallet.Coins;
        PlayerName.Value = game.Profile.Name;
        PlayerTarget.Value = run.Player != null ? run.Player.transform : null;
        LevelProgress.SetLevel(game.LevelNumber);

        game.MenuShown += OnMenuShown;
        game.RewardReady += OnRewardReady;
        game.GameOver += OnGameOver;
        game.Wallet.CoinsChanged += OnCoinsChanged;
        game.Profile.NameChanged += OnNameChanged;
        run.PlayerSpawned += OnPlayerSpawned;
        run.CountdownTick += OnCountdownTick;
        run.RaceStarted += OnRaceStarted;
        run.PlayerPlaceChanged += OnPlaceChanged;
    }

    public void Dispose()
    {
        game.MenuShown -= OnMenuShown;
        game.RewardReady -= OnRewardReady;
        game.GameOver -= OnGameOver;
        game.Wallet.CoinsChanged -= OnCoinsChanged;
        game.Profile.NameChanged -= OnNameChanged;
        run.PlayerSpawned -= OnPlayerSpawned;
        run.CountdownTick -= OnCountdownTick;
        run.RaceStarted -= OnRaceStarted;
        run.PlayerPlaceChanged -= OnPlaceChanged;

        BoardUpgrade.Dispose();
        OfflineEarnUpgrade.Dispose();
    }

    // ---------- Game → UI ----------

    private void OnMenuShown()
    {
        Hud.Reset();
        LevelProgress.SetLevel(game.LevelNumber);
        SettingsOpen.Value = false;
        ExitConfirmOpen.Value = false;
        Screen.Value = UiScreen.Menu;
    }

    private void OnRewardReady(RunResult result, int coins)
    {
        Reward.Place.Value = result.Place;
        Reward.Coins.Value = coins;
        Screen.Value = UiScreen.Reward;
    }

    private void OnGameOver()
    {
        ExitConfirmOpen.Value = false;
        Screen.Value = UiScreen.GameOver;
    }

    private void OnCoinsChanged(int coins) => Coins.Value = coins;
    private void OnNameChanged(string name) => PlayerName.Value = name;
    private void OnPlayerSpawned(Player player) => PlayerTarget.Value = player.transform;
    private void OnCountdownTick(int seconds) => Hud.Countdown.Value = seconds;
    private void OnPlaceChanged(int place) => Hud.Place.Value = place;

    private void OnRaceStarted()
    {
        Hud.Countdown.Value = HudViewModel.CountdownGo;
        Hud.PlaceVisible.Value = true;
    }

    // ---------- UI → Game (buttons) ----------

    /// <summary>PLAY button.</summary>
    public void Play()
    {
        if (run.State != RunState.Ready) return;

        Screen.Value = UiScreen.Race;
        game.Play();
    }

    /// <summary>Menu name field: the player finished typing.</summary>
    public void SetPlayerName(string name) => game.Profile.SetName(name);

    /// <summary>Reward panel GET button: takes the coins and moves on to the next level's menu.</summary>
    public void ClaimReward() => game.NextLevel();

    /// <summary>Game-over panel RETRY button.</summary>
    public void Retry() => game.Replay();

    public void OpenSettings() => SettingsOpen.Value = true;
    public void CloseSettings() => SettingsOpen.Value = false;

    /// <summary>Home button during a run: freezes the run and asks "Back to menu?".</summary>
    public void AskExit()
    {
        if (Screen.Value != UiScreen.Race) return;

        game.Pause();
        ExitConfirmOpen.Value = true;
    }

    public void CancelExit()
    {
        game.Resume();
        ExitConfirmOpen.Value = false;
    }

    /// <summary>The run is thrown away; back to the menu on the same level.</summary>
    public void ConfirmExit()
    {
        ExitConfirmOpen.Value = false;
        game.Replay();
    }
}
