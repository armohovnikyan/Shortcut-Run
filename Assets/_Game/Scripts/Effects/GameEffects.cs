using System.Collections;
using UnityEngine;

/// <summary>
/// Decides WHEN the world effects play: listens to the run and spawns the matching effect — the same job
/// GameAudio does for sounds. Connected by GameBootstrap (Bind); nothing in the game knows effects exist.
/// </summary>
public class GameEffects : MonoBehaviour
{
    [Header("Water splash (race fall = game over)")]
    [SerializeField] private ParticleSystem waterSplashPrefab;
    [Tooltip("The splash plays where the falling player crosses this water's surface.")]
    [SerializeField] private InfiniteWater water;
    [Tooltip("Only without Water: the splash plays this far below the point where the player left the road.")]
    [SerializeField, Min(0f)] private float waterDepth = 2.5f;
    [Tooltip("Seconds before a spawned splash is removed. Must be longer than its longest particle.")]
    [SerializeField, Min(0.1f)] private float splashLifetime = 3f;

    private RunManager run;
    private Player player;
    private bool inBonus;
    private Coroutine splashRoutine;

    /// <summary>Called once by GameBootstrap.</summary>
    public void Bind(GameManager game)
    {
        if (waterSplashPrefab == null)
            Debug.LogWarning($"{name}: Game Effects has no Water Splash prefab — falls won't splash.", this);

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

    // A new run (start, Replay, Next level): new player.
    private void OnPlayerSpawned(Player spawned)
    {
        ReleasePlayer();
        inBonus = false;
        player = spawned;
        player.Fell += OnPlayerFell;
    }

    private void ReleasePlayer()
    {
        if (player != null) player.Fell -= OnPlayerFell;
        player = null;
        if (splashRoutine != null) StopCoroutine(splashRoutine);
        splashRoutine = null;
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
