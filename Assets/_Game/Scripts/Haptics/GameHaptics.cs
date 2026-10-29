using UnityEngine;

/// <summary>
/// Decides WHEN the phone taps: listens to the player and asks Haptics for a short tap — the same job
/// GameAudio does for sounds. Connected by GameBootstrap (Bind); nothing in the game knows vibration exists.
/// Respects the Vibration switch in Settings.
/// </summary>
public class GameHaptics : MonoBehaviour
{
    [Header("Placing a board")]
    [SerializeField, Range(1, 100)] private int placeMilliseconds = 12;
    [SerializeField, Range(0.01f, 1f)] private float placeStrength = 0.25f;
    [Tooltip("Boards go down many times a second: at most one tap per this many seconds, or it turns into a buzz.")]
    [SerializeField, Min(0f)] private float placeInterval = 0.12f;

    [Header("Picking up boards")]
    [SerializeField, Range(1, 100)] private int pickupMilliseconds = 20;
    [SerializeField, Range(0.01f, 1f)] private float pickupStrength = 0.45f;

    private RunManager run;
    private GameSettings settings;
    private Player player;
    private float nextPlaceTime;

    /// <summary>Called once by GameBootstrap.</summary>
    public void Bind(GameManager game)
    {
        settings = game.Settings;
        run = game.Run;
        run.PlayerSpawned += OnPlayerSpawned;

        // GameManager may have spawned the player before Bind ran (Start order isn't fixed).
        if (run.Player != null) OnPlayerSpawned(run.Player);
    }

    private void OnDestroy()
    {
        if (run != null) run.PlayerSpawned -= OnPlayerSpawned;
        ReleasePlayer();
    }

    private void OnPlayerSpawned(Player spawned)
    {
        ReleasePlayer();
        player = spawned;
        player.BoardPlaced += OnBoardPlaced;
        player.BoardsPickedUp += OnBoardsPickedUp;
        player.BoardsStolen += OnBoardsPickedUp; // boards from a knocked-out runner feel the same
    }

    private void ReleasePlayer()
    {
        if (player == null) return;
        player.BoardPlaced -= OnBoardPlaced;
        player.BoardsPickedUp -= OnBoardsPickedUp;
        player.BoardsStolen -= OnBoardsPickedUp;
        player = null;
    }

    private void OnBoardPlaced()
    {
        if (Time.time < nextPlaceTime) return;
        nextPlaceTime = Time.time + placeInterval;
        Tap(placeMilliseconds, placeStrength);
    }

    private void OnBoardsPickedUp(int added, int stackCount) => Tap(pickupMilliseconds, pickupStrength);

    private void Tap(int milliseconds, float strength)
    {
        if (settings != null && !settings.VibrationOn) return;
        Haptics.Tap(milliseconds, strength);
    }
}
