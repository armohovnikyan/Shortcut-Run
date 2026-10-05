using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Puts a single BoardStack on a road by TrackPlacement (section, distance, side) instead of by hand.
// The placement is the source of truth: the Transform is recalculated from it whenever the road
// rebakes (knot moved, width changed, spline added/removed) or the placement changes.
// On a TrackPlatform instead (no spline there): the stack is the platform's child and its Transform
// is free — moved with the normal tools, carried along when the platform moves.
// Editor-time only — at runtime the stack just stays where it was saved.
[ExecuteAlways]
[RequireComponent(typeof(BoardStack))]
public class TrackPlacedStack : MonoBehaviour, ITrackPlaced
{
    [SerializeField] private TrackPlacement placement = TrackPlacement.Default;
    [Tooltip("Set = this stack sits on that platform, not on the road; the placement above is ignored.")]
    [SerializeField] private TrackPlatform platform;

    public TrackPlacement Placement => placement;
    public TrackPlatform Platform => platform;

#if UNITY_EDITOR
    private bool applyQueued;

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        SplineRoad.Baked += OnRoadBaked;
        Undo.undoRedoPerformed += QueueApply;
    }

    private void OnDisable()
    {
        SplineRoad.Baked -= OnRoadBaked;
        Undo.undoRedoPerformed -= QueueApply;
    }

    // Runs when the component is added: find the road and snap onto it from wherever the object is now.
    private void Reset()
    {
        // Added to an object already sitting on a platform: belong to that platform, not the road.
        platform = GetComponentInParent<TrackPlatform>();
        if (platform != null) return;

        placement.road = TrackMath.FindRoad(gameObject);
        if (placement.road != null)
        {
            placement.road.EnsureSectionIds();
            TrackMath.TryProject(placement.road, transform.position, ref placement);
        }
        QueueApply();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueueApply();
    }

    private void OnRoadBaked(SplineRoad road)
    {
        if (road == placement.road) Apply();
    }

    // Moving the Transform inside OnValidate/Reset makes Unity complain, so do it on the next editor tick.
    private void QueueApply()
    {
        if (applyQueued) return;
        applyQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            applyQueued = false;
            Apply();
        };
    }

    // Used by the Inspector buttons and Scene-view handles: one undo step covers the placement and the move.
    public void SetPlacement(TrackPlacement newPlacement, string undoName)
    {
        Undo.RecordObjects(new Object[] { this, transform }, undoName);
        placement = newPlacement;
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        Apply();
    }

    /// <summary>Puts the stack on a platform at this pose: becomes its child, leaves the road placement.</summary>
    public void PlaceOnPlatform(TrackPlatform target, Vector3 position, Quaternion rotation, string undoName)
    {
        Undo.RecordObjects(new Object[] { this, transform }, undoName);
        platform = target;
        placement.road = null; // nothing to re-snap to; the board counter lists it under platforms
        Undo.SetTransformParent(transform, target.transform, undoName);
        transform.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
    }

    /// <summary>Takes the stack off its platform and puts it on the nearest point of the road, centred.</summary>
    public void BackToRoad(string undoName)
    {
        if (platform == null) return;
        Undo.RecordObjects(new Object[] { this, transform }, undoName);
        Transform outside = platform.transform.parent;
        platform = null;
        Undo.SetTransformParent(transform, outside, undoName);

        placement.road = TrackMath.FindRoad(gameObject);
        if (placement.road != null)
        {
            placement.road.EnsureSectionIds();
            if (TrackMath.TryProject(placement.road, transform.position, ref placement)) placement.side = 0f;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        Apply();
    }

    public void Apply()
    {
        // Prefab assets in the Project window also get OnValidate — never write through those.
        if (Application.isPlaying || EditorUtility.IsPersistent(this)) return;
        if (platform != null) return; // on a platform the Transform is free — nothing to recalculate
        if (!TrackMath.TryEvaluate(placement, out Vector3 position, out Quaternion rotation)) return;

        bool moved = (transform.position - position).sqrMagnitude > 1e-8f
                     || Quaternion.Angle(transform.rotation, rotation) > 0.01f;
        if (!moved) return; // don't dirty the level when nothing changed (e.g. on open)

        transform.SetPositionAndRotation(position, rotation);
        EditorUtility.SetDirty(transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
    }
#endif
}
