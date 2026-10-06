using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Decides WHEN each sound plays: listens to the run, the wallet and the UI buttons, and asks AudioPlayer
/// to play the matching sound from the library. Connected by GameBootstrap (Bind / BindUi) — nothing in the
/// game or the UI knows sound exists. The player's own sounds are in PlayerSounds.
/// </summary>
[RequireComponent(typeof(AudioPlayer))]
public class GameAudio : MonoBehaviour
{
    [SerializeField] private SoundLibrarySO library;

    [Header("Player")]
    [Tooltip("Seconds between board pickups that still count as a row (the pitch keeps climbing).")]
    [SerializeField, Min(0f)] private float pickupComboTime = 0.6f;

    [Header("Coins")]
    [SerializeField, Min(1)] private int coinTicks = 6;
    [SerializeField, Min(0.01f)] private float coinTickInterval = 0.07f;

    private AudioPlayer audioPlayer;
    private RunManager run;
    private Wallet wallet;
    private PlayerSounds playerSounds;
    private Player player;
    private int countdownBeat;
    private bool inBonus;
    private int lastCoins;

    private void Awake() => audioPlayer = GetComponent<AudioPlayer>();

    /// <summary>Called once by GameBootstrap.</summary>
    public void Bind(GameManager game)
    {
        if (library == null)
        {
            Debug.LogError($"{name}: Game Audio has no Sound Library — the game will be silent.", this);
            return;
        }

        run = game.Run;
        run.PlayerSpawned += OnPlayerSpawned;
        run.CountdownTick += OnCountdownTick;
        run.RaceStarted += OnRaceStarted;
        run.BonusStarted += OnBonusStarted;
        run.MultiplierReached += OnMultiplierReached;
        run.RunEnded += OnRunEnded;

        wallet = game.Wallet;
        lastCoins = wallet.Coins;
        wallet.CoinsChanged += OnCoinsChanged;

        // GameManager may have spawned the player before Bind ran (Start order isn't fixed).
        if (run.Player != null) OnPlayerSpawned(run.Player);
    }

    /// <summary>Called once by GameBootstrap: a click on every button and switch under the UI root.</summary>
    public void BindUi(Component uiRoot)
    {
        if (library == null || uiRoot == null) return;

        // true = inactive panels too (reward, settings … start hidden).
        foreach (Button button in uiRoot.GetComponentsInChildren<Button>(true))
            button.onClick.AddListener(PlayClick);
        foreach (ToggleSwitch toggle in uiRoot.GetComponentsInChildren<ToggleSwitch>(true))
            toggle.ValueChanged += _ => PlayClick();
    }

    private void OnDestroy()
    {
        if (run != null)
        {
            run.PlayerSpawned -= OnPlayerSpawned;
            run.CountdownTick -= OnCountdownTick;
            run.RaceStarted -= OnRaceStarted;
            run.BonusStarted -= OnBonusStarted;
            run.MultiplierReached -= OnMultiplierReached;
            run.RunEnded -= OnRunEnded;
        }
        if (wallet != null) wallet.CoinsChanged -= OnCoinsChanged;
        ReleasePlayer();
    }

    private void PlayClick() => audioPlayer.Play(library.Click);

    // ---------- Run ----------

    // A new run (start, Replay, Next level): new player, the opponents appear.
    private void OnPlayerSpawned(Player spawned)
    {
        ReleasePlayer();
        countdownBeat = 0;
        inBonus = false;

        player = spawned;
        player.Fell += OnPlayerFell;
        playerSounds = new PlayerSounds(player, library, audioPlayer, pickupComboTime);

        foreach (Runner runner in run.Runners)
            if (runner is NPC npc) npc.KnockedOut += OnOpponentKnockedOut;

        audioPlayer.Play(library.OpponentsSpawn);
    }

    private void ReleasePlayer()
    {
        if (player != null) player.Fell -= OnPlayerFell;
        player = null;
        playerSounds?.Dispose();
        playerSounds = null;
        // The NPCs' KnockedOut needs no unsubscribing: they are destroyed with the run.
    }

    // The clip's beats are faster than our countdown, so it is played one beat per number:
    // 3 = beat 1, 2 = beat 2, 1 = beat 3, GO = the last beat (to the clip's end).
    private void OnCountdownTick(int seconds)
    {
        int lastNumberBeat = library.CountdownBeats - 2; // a longer countdown repeats it
        int beat = Mathf.Min(countdownBeat++, Mathf.Max(0, lastNumberBeat));
        audioPlayer.PlaySegment(library.Countdown, beat * library.CountdownBeatLength, library.CountdownBeatLength);
    }

    private void OnRaceStarted()
    {
        int goBeat = library.CountdownBeats - 1;
        audioPlayer.PlaySegment(library.Countdown, goBeat * library.CountdownBeatLength, 0f);
    }

    private void OnBonusStarted() => inBonus = true;

    private void OnMultiplierReached(int multiplier) => audioPlayer.Play(library.MultiplierFor(multiplier));

    // The race ends over water, the bonus over the void.
    private void OnPlayerFell(Runner runner) => audioPlayer.Play(inBonus ? library.FallVoid : library.FallWater);

    private void OnOpponentKnockedOut(NPC npc) => audioPlayer.Play(library.OpponentKnockedOut);

    private void OnRunEnded(RunResult result)
    {
        if (result.Finished && result.Place == 1) audioPlayer.Play(library.Victory);
    }

    // ---------- Coins ----------

    // Only income rings (the reward); spending on upgrades already has the button click.
    private void OnCoinsChanged(int coins)
    {
        bool gained = coins > lastCoins;
        lastCoins = coins;
        if (gained) StartCoroutine(CoinTicks());
    }

    private IEnumerator CoinTicks()
    {
        var wait = new WaitForSeconds(coinTickInterval);
        for (int i = 0; i < coinTicks; i++)
        {
            audioPlayer.Play(library.Coin);
            yield return wait;
        }
        audioPlayer.Play(library.CoinDone);
    }
}
