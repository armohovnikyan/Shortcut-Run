using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The bonus multiplier platforms after the finish (x2 … x15). They are part of the level — placed at EDIT time
/// with the Generate Layout button, saved in the level prefab and baked into the NavMeshSurface like any other road.
///
/// Put it on an empty object at the far edge of the finish platform: its position is the start,
/// its blue (Z) arrow the direction the platforms go. Edit the level in Prefab Mode and press Generate Layout.
///
/// At runtime it only hides them below their places (run prepared) and raises them in a fast overlapping wave
/// (player came 1st).
/// </summary>
public class BonusPlatformLayout : MonoBehaviour
{
    [Header("Platforms")]
    [Tooltip("x2 … second to last. Same size, same spacing.")]
    [SerializeField] private MultiplierPlatform platformPrefab;
    [Tooltip("The last one (x15): bigger and further away. Reaching it always ends the bonus.")]
    [SerializeField] private MultiplierPlatform lastPlatformPrefab;
    [SerializeField, Min(2)] private int firstMultiplier = 2;
    [SerializeField, Min(2)] private int lastMultiplier = 15;

    [Header("Placement (Generate Layout)")]
    [Tooltip("Metres forward from this object to the centre of the first platform.")]
    [SerializeField, Min(0f)] private float firstGap = 1f;
    [Tooltip("Metres forward between platform centres, x2 … second to last.")]
    [SerializeField, Min(0f)] private float gap = 1.1f;
    [Tooltip("Metres forward from the second to last platform's centre to the last platform's centre.")]
    [SerializeField, Min(0f)] private float lastGap = 3f;
    [Tooltip("Sideways offset from the centre line, between X (min) and Y (max). Negative = left.")]
    [SerializeField] private Vector2 sideRange = new Vector2(-3f, 3f);
    [Tooltip("How much the sideways offset changes from one platform to the next. " +
             "Low = a gentle wave, high = close to fully random.")]
    [SerializeField, Range(0.05f, 1f)] private float sideVariation = 0.35f;
    [Tooltip("Same seed = same layout. Randomize picks a new one.")]
    [SerializeField] private int seed = 1;

    [Header("Rise from below")]
    [SerializeField, Min(0f)] private float riseDepth = 15f;
    [SerializeField, Min(0.01f)] private float riseDuration = 0.6f;
    [Tooltip("Seconds between one platform starting to rise and the next. Shorter than Rise Duration = " +
             "several move at once, like a shock wave.")]
    [SerializeField, Min(0f)] private float riseInterval = 0.15f;

    [SerializeField, HideInInspector] private List<MultiplierPlatform> platforms = new List<MultiplierPlatform>();

    /// <summary>Reaching this one ends the bonus.</summary>
    public int LastMultiplier => platforms.Count > 0 && platforms[platforms.Count - 1] != null
        ? platforms[platforms.Count - 1].Multiplier : lastMultiplier;
    public IReadOnlyList<MultiplierPlatform> Platforms => platforms;

    /// <summary>Run prepared: all platforms out of sight below their places.</summary>
    public void HideAll()
    {
        foreach (MultiplierPlatform platform in platforms)
            if (platform != null) platform.Hide(riseDepth);
    }

    /// <summary>Player came 1st: platforms rise in order, each starting shortly after the previous one.</summary>
    public void RiseAll()
    {
        for (int i = 0; i < platforms.Count; i++)
            if (platforms[i] != null) platforms[i].Rise(riseDuration, i * riseInterval);
    }

#if UNITY_EDITOR
    /// <summary>Editor: picks a new seed and generates.</summary>
    public void Randomize()
    {
        Undo.RecordObject(this, "Randomize Bonus Layout");
        seed = Random.Range(1, 100000);
        Generate();
    }

    /// <summary>Editor: replaces the platforms with a fresh set from the current settings (undoable).</summary>
    public void Generate()
    {
        if (platformPrefab == null || lastPlatformPrefab == null)
        {
            Debug.LogError($"{name}: set both Platform Prefab and Last Platform Prefab first.", this);
            return;
        }

        Undo.RecordObject(this, "Generate Bonus Layout");
        foreach (MultiplierPlatform old in platforms)
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        platforms.Clear();

        // Perlin noise along the row: neighbours get related offsets, so the row drifts instead of jittering.
        var offset = new Vector2((seed % 997) * 7.31f, (seed % 991) * 3.17f);
        float z = 0f;
        for (int multiplier = firstMultiplier; multiplier <= lastMultiplier; multiplier++)
        {
            int index = multiplier - firstMultiplier;
            bool last = multiplier == lastMultiplier;
            z += index == 0 ? firstGap : last ? lastGap : gap;

            float noise = Mathf.InverseLerp(0.2f, 0.8f, Mathf.PerlinNoise(offset.x + index * sideVariation, offset.y));
            float x = Mathf.Lerp(sideRange.x, sideRange.y, noise);

            var platform = (MultiplierPlatform)PrefabUtility.InstantiatePrefab(last ? lastPlatformPrefab : platformPrefab, transform);
            Undo.RegisterCreatedObjectUndo(platform.gameObject, "Generate Bonus Layout");
            platform.transform.localPosition = new Vector3(x, 0f, z);
            platform.transform.localRotation = Quaternion.identity;
            platform.name = $"Bonus x{multiplier}";
            platform.SetMultiplier(multiplier);
            platforms.Add(platform);
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"{name}: generated x{firstMultiplier} … x{lastMultiplier} (seed {seed}). Re-bake the NavMeshSurface.", this);
    }

    private void OnValidate()
    {
        if (lastMultiplier < firstMultiplier) lastMultiplier = firstMultiplier;
        if (sideRange.y < sideRange.x) sideRange.y = sideRange.x;
    }
#endif
}
