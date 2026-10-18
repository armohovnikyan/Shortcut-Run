using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One bonus platform (x2 … x15). It is ROAD, like a helper platform: a solid collider on the Road layer, so runners
/// stand on it and the NavMeshSurface bakes it. The run finds it through the runner's own ground check, no trigger needed.
/// Placed in the level at edit time (BonusPlatformLayout → Generate Layout). At runtime it only hides below its
/// place and rises back into it.
/// </summary>
public class MultiplierPlatform : MonoBehaviour
{
    [SerializeField, Min(1)] private int multiplier = 2;
    [Tooltip("Where the player stands and dances if the run ends here. Empty = the platform's centre. " +
             "Its blue (Z) arrow = the way the player faces.")]
    [SerializeField] private Transform standPoint;
    [Tooltip("Where the finish camera sits if the run ends here, position AND rotation (CameraPos). " +
             "Empty = the camera just stops where it stopped following.")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Text on the platform's world canvas showing the multiplier (5x). Empty = the first text found in the children.")]
    [SerializeField] private TMP_Text label;

    private Vector3 placedPosition;
    private bool placedPositionKnown;
    private Collider[] colliders;
    private Coroutine rise;

    public int Multiplier => multiplier;
    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public Transform ViewPoint => viewPoint;

    private void Awake()
    {
        RememberPlacedPosition();
        UpdateLabel(); // set at runtime too: an editor-only text change on a prefab instance isn't always saved
    }

    /// <summary>Editor: called by the layout when it creates or renumbers the platform.</summary>
    public void SetMultiplier(int value)
    {
        multiplier = value;
        UpdateLabel();
    }

    /// <summary>Out of sight below its place, with no collision — nobody can stand on it while hidden.</summary>
    public void Hide(float depth)
    {
        RememberPlacedPosition();
        if (rise != null) StopCoroutine(rise);
        rise = null;
        transform.localPosition = placedPosition + Vector3.down * depth;
        SetCollidersEnabled(false);
    }

    /// <summary>Rises from wherever it is back to its placed position, starting after `delay` seconds.</summary>
    public void Rise(float duration, float delay)
    {
        RememberPlacedPosition();
        if (rise != null) StopCoroutine(rise);
        rise = StartCoroutine(RiseRoutine(duration, delay));
    }

    private IEnumerator RiseRoutine(float duration, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Solid from the moment it moves: a fast player may land on it while it is still coming up.
        SetCollidersEnabled(true);
        Vector3 start = transform.localPosition;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = time / duration;
            transform.localPosition = Vector3.Lerp(start, placedPosition, 1f - (1f - t) * (1f - t)); // ease out
            yield return null;
        }
        transform.localPosition = placedPosition;
        rise = null;
    }

    // The position it has in the level prefab. Taken once, before anything moves it.
    private void RememberPlacedPosition()
    {
        if (placedPositionKnown) return;
        placedPosition = transform.localPosition;
        placedPositionKnown = true;
    }

    private void SetCollidersEnabled(bool on)
    {
        colliders ??= GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders) c.enabled = on;
    }

    private void UpdateLabel()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = $"{multiplier}x";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateLabel();

        int road = LayerMask.NameToLayer(TrackPlatform.RoadLayerName);
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger && road >= 0 && c.gameObject.layer != road)
                Debug.LogWarning($"{name}/{c.name}: a bonus platform collider must be on the '{TrackPlatform.RoadLayerName}' " +
                                 "layer — otherwise runners fall through it and the NavMesh skips it.", c);
    }
#endif
}
