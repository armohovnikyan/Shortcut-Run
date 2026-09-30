using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the bonus multiplier platforms after the finish, when the player comes first.
/// Put it on an empty object at the far edge of the finish platform: its position is the start,
/// its blue (Z) arrow the direction the platforms go.
/// Platforms are numbered in order (x2 … x15), each at a random distance and sideways offset,
/// and rise from below one after another.
/// First version — placement and rise are to be tuned once the rest of the run is tested.
/// </summary>
public class BonusPlatformSpawner : MonoBehaviour
{
    [SerializeField] private MultiplierPlatform platformPrefab;
    [SerializeField, Min(2)] private int firstMultiplier = 2;
    [SerializeField, Min(2)] private int lastMultiplier = 15;

    [Header("Placement — random per platform, between X (min) and Y (max)")]
    [Tooltip("Metres forward from the previous platform (centre to centre).")]
    [SerializeField] private Vector2 forwardGap = new Vector2(6f, 10f);
    [Tooltip("Metres sideways from the centre line. Negative = left.")]
    [SerializeField] private Vector2 sideOffset = new Vector2(-3f, 3f);

    [Header("Rise from below")]
    [SerializeField, Min(0f)] private float riseDepth = 6f;
    [SerializeField, Min(0.01f)] private float riseDuration = 0.6f;
    [Tooltip("Seconds between one platform starting to rise and the next.")]
    [SerializeField, Min(0f)] private float riseInterval = 0.15f;

    private readonly List<MultiplierPlatform> spawned = new List<MultiplierPlatform>();

    /// <summary>Reaching this one ends the bonus.</summary>
    public int LastMultiplier => lastMultiplier;
    public IReadOnlyList<MultiplierPlatform> Platforms => spawned;

    public void Spawn()
    {
        if (spawned.Count > 0) return; // once per level instance
        if (platformPrefab == null)
        {
            Debug.LogError($"{name}: Bonus Platform Spawner has no Platform Prefab.", this);
            return;
        }

        Vector3 centre = transform.position;
        for (int multiplier = firstMultiplier; multiplier <= lastMultiplier; multiplier++)
        {
            centre += transform.forward * Random.Range(forwardGap.x, forwardGap.y);
            Vector3 position = centre + transform.right * Random.Range(sideOffset.x, sideOffset.y);

            MultiplierPlatform platform = Instantiate(platformPrefab, position, transform.rotation, transform);
            platform.Setup(multiplier);
            platform.RiseFrom(riseDepth, riseDuration, (multiplier - firstMultiplier) * riseInterval);
            spawned.Add(platform);
        }
    }
}
