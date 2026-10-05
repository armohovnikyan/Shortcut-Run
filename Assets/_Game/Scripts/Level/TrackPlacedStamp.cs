using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Puts a whole BoardStampSO pattern on a road. The placement is the pattern's anchor; every entry is
// placed from the track at (anchor + its offset), so the pattern bends with curves. Facing/Extra Yaw
// turn the whole pattern: offsets are rotated, and each stack's own Facing is added on top.
// The child BoardStacks are owned by the stamp (one per entry, in entry order) — edit the pattern
// asset, not the children. Editor-time only: at runtime everything stays where it was saved.
// On a TrackPlatform instead (no track to bend along): the stamp is the platform's child, its own
// Transform is free (Move / Rotate tools), and the entries are laid out flat in its frame —
// forward = its blue arrow, side = its red arrow, each stack turned by its entry's Facing.
[ExecuteAlways]
public class TrackPlacedStamp : MonoBehaviour, ITrackPlaced
{
    [SerializeField] private BoardStampSO stamp;
    [SerializeField] private CollectableBoard boardPrefab;
    [SerializeField] private TrackPlacement placement = TrackPlacement.Default;
    [Tooltip("Set = this stamp sits on that platform, not on the road; the placement above is ignored.")]
    [SerializeField] private TrackPlatform platform;

    public TrackPlacement Placement => placement;
    public TrackPlatform Platform => platform;
    public BoardStampSO Stamp => stamp;

    // Placement of one entry: anchor + offset rotated by the stamp's turn, measured along the track.
    public bool TryGetEntryPlacement(BoardStampSO.Entry entry, out TrackPlacement entryPlacement)
    {
        entryPlacement = placement;
        SplineRoad road = placement.road;
        if (road == null) return false;
        int index = road.FindSectionIndex(placement.sectionId);
        if (index < 0) return false;

        // Rotate the (side, forward) offset clockwise seen from above — same direction as Facing.
        float angle = ((float)placement.facing + placement.yaw) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        float side = entry.side * cos + entry.forward * sin;
        float forward = -entry.side * sin + entry.forward * cos;

        float halfWidth = road.GetWidth(index) * 0.5f;
        entryPlacement.distance = placement.distance + forward;
        entryPlacement.side = placement.side + side / halfWidth; // not clamped: the editor warns instead
        entryPlacement.facing = (TrackFacing)(((int)placement.facing + (int)entry.facing) % 360);
        return true;
    }

    private List<BoardStack> GetStackChildren()
    {
        var stacks = new List<BoardStack>();
        foreach (Transform child in transform)
            if (child.TryGetComponent(out BoardStack stack)) stacks.Add(stack);
        return stacks;
    }

#if UNITY_EDITOR
    private bool syncQueued;

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        SplineRoad.Baked += OnRoadBaked;
        BoardStampSO.Changed += OnStampAssetChanged;
        Undo.undoRedoPerformed += QueueSync;
    }

    private void OnDisable()
    {
        SplineRoad.Baked -= OnRoadBaked;
        BoardStampSO.Changed -= OnStampAssetChanged;
        Undo.undoRedoPerformed -= QueueSync;
    }

    // Runs when the component is added: find the road and snap onto it from wherever the object is now.
    private void Reset()
    {
        // Added to an object already sitting on a platform: belong to that platform, not the road.
        platform = GetComponentInParent<TrackPlatform>();
        if (platform != null)
        {
            QueueSync();
            return;
        }

        placement.road = TrackMath.FindRoad(gameObject);
        if (placement.road != null)
        {
            placement.road.EnsureSectionIds();
            TrackMath.TryProject(placement.road, transform.position, ref placement);
        }
        QueueSync();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueueSync();
    }

    private void OnRoadBaked(SplineRoad road)
    {
        if (road == placement.road) Sync();
    }

    private void OnStampAssetChanged(BoardStampSO changed)
    {
        if (changed == stamp) QueueSync();
    }

    // Creating/destroying/moving objects inside OnValidate/Reset isn't allowed — next editor tick instead.
    private void QueueSync()
    {
        if (syncQueued) return;
        syncQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            syncQueued = false;
            Sync();
        };
    }

    public void SetPlacement(TrackPlacement newPlacement, string undoName)
    {
        Undo.RecordObjects(new Object[] { this, transform }, undoName);
        placement = newPlacement;
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        Sync();
    }

    public void PlaceOnPlatform(TrackPlatform target, Vector3 position, Quaternion rotation, string undoName)
    {
        Undo.RecordObjects(new Object[] { this, transform }, undoName);
        platform = target;
        placement.road = null; // nothing to re-snap to; the board counter lists it under platforms
        Undo.SetTransformParent(transform, target.transform, undoName);
        transform.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        Sync();
    }

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
        Sync();
    }

    // Brings the children in line with the pattern and the road: one stack per entry, each configured
    // and moved. Every part only writes when something differs, so opening a level changes nothing.
    // Not recorded in undo — it's derived from the placement and the asset, and reruns after an undo.
    public void Sync()
    {
        // Prefab assets in the Project window also get OnValidate — never write through those.
        if (Application.isPlaying || EditorUtility.IsPersistent(this)) return;

        IReadOnlyList<BoardStampSO.Entry> entries = stamp != null ? stamp.Entries : new BoardStampSO.Entry[0];
        List<BoardStack> stacks = GetStackChildren();

        if (!MatchStackCount(stacks, entries.Count)) return;

        // The stamp's own object sits at the anchor, so the track handles appear there.
        // On a platform the anchor is wherever the stamp was put — nothing to evaluate.
        if (platform == null && TrackMath.TryEvaluate(placement, out Vector3 anchorPosition, out Quaternion anchorRotation))
            MoveIfNeeded(transform, anchorPosition, anchorRotation);

        for (int i = 0; i < entries.Count; i++)
        {
            stacks[i].Configure(entries[i].shape, boardPrefab);

            string stackName = $"Stack {i}" + (entries[i].shape != null ? $" ({entries[i].shape.name})" : "");
            if (stacks[i].name != stackName)
            {
                stacks[i].name = stackName;
                EditorUtility.SetDirty(stacks[i].gameObject);
            }

            if (platform != null)
            {
                // Flat pattern in the stamp's own frame: x = side (right), z = forward.
                MoveLocalIfNeeded(stacks[i].transform,
                    new Vector3(entries[i].side, 0f, entries[i].forward),
                    Quaternion.Euler(0f, (float)entries[i].facing, 0f));
            }
            else if (TryGetEntryPlacement(entries[i], out TrackPlacement entryPlacement)
                     && TrackMath.TryEvaluate(entryPlacement, out Vector3 position, out Quaternion rotation))
                MoveIfNeeded(stacks[i].transform, position, rotation);
        }
    }

    // Adds or removes child stacks at the end so there's exactly one per entry.
    private bool MatchStackCount(List<BoardStack> stacks, int count)
    {
        if (stacks.Count == count) return true;

        for (int i = count; i < stacks.Count; i++)
        {
            GameObject extra = stacks[i].gameObject;
            GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(extra);
            if (outermost != null && !PrefabUtility.IsAddedGameObjectOverride(extra))
            {
                string owner = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(extra);
                Debug.LogWarning($"{name}: can't change this stamp's stacks here — they're saved inside the prefab " +
                                 $"'{owner}'. Open that prefab (double-click it) and change the stamp there.", this);
                return false;
            }
        }

        for (int i = stacks.Count - 1; i >= count; i--)
        {
            DestroyImmediate(stacks[i].gameObject);
            stacks.RemoveAt(i);
        }

        while (stacks.Count < count)
        {
            var child = new GameObject($"Stack {stacks.Count}");
            child.transform.SetParent(transform, false);
            stacks.Add(child.AddComponent<BoardStack>());
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        return true;
    }

    private static void MoveLocalIfNeeded(Transform target, Vector3 localPosition, Quaternion localRotation)
    {
        bool moved = (target.localPosition - localPosition).sqrMagnitude > 1e-8f
                     || Quaternion.Angle(target.localRotation, localRotation) > 0.01f;
        if (!moved) return;

        target.SetLocalPositionAndRotation(localPosition, localRotation);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void MoveIfNeeded(Transform target, Vector3 position, Quaternion rotation)
    {
        bool moved = (target.position - position).sqrMagnitude > 1e-8f
                     || Quaternion.Angle(target.rotation, rotation) > 0.01f;
        if (!moved) return;

        target.SetPositionAndRotation(position, rotation);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
#endif
}
