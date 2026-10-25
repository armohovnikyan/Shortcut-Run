using UnityEngine;

/// <summary>
/// One jump pad with the hand-placed spots it can stand on. Every run (RunManager.Prepare → Level.PlaceJumpPads)
/// the pad moves to one of the spots at random — position AND rotation. Put this on an empty holder object;
/// the pad and the spots are its children. The spots must NOT be children of the pad (they would move with it).
/// </summary>
public class JumpPadSpots : MonoBehaviour
{
    [Tooltip("The whole pad object to move — the prefab instance's root, not the child that holds the JumpPad.")]
    [SerializeField] private Transform pad;
    [Tooltip("Where the pad may stand. Position and rotation are copied; scale is not.")]
    [SerializeField] private Transform[] spots = new Transform[3];

    /// <summary>Moves the pad to a random spot. Empty spots are skipped; none set = the pad stays where it is.</summary>
    public void PlaceRandom()
    {
        if (pad == null)
        {
            Debug.LogError($"{name}: Jump Pad Spots has no Pad.", this);
            return;
        }

        Transform spot = PickSpot();
        if (spot == null) return;
        pad.SetPositionAndRotation(spot.position, spot.rotation);
        Physics.SyncTransforms(); // the GroundProbe must find the pad at its new place on the very first frame
    }

    private Transform PickSpot()
    {
        int count = 0;
        foreach (Transform spot in spots) if (spot != null) count++;
        if (count == 0) return null;

        int pick = Random.Range(0, count);
        foreach (Transform spot in spots)
        {
            if (spot == null) continue;
            if (pick-- == 0) return spot;
        }
        return null;
    }

    // Right-click the component → creates the 3 spot children at the pad's current pose, ready to drag around.
    [ContextMenu("Create Spots From Pad")]
    private void CreateSpots()
    {
        if (pad == null) pad = FindPadRoot();
        if (pad == null)
        {
            Debug.LogError($"{name}: put a JumpPad under this object first.", this);
            return;
        }

        spots = new Transform[3];
        for (int i = 0; i < spots.Length; i++)
        {
            var spot = new GameObject($"Spot_{(char)('A' + i)}").transform;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(spot.gameObject, "Create Jump Pad Spots");
#endif
            spot.SetParent(transform, false);
            spot.SetPositionAndRotation(pad.position, pad.rotation);
            spots[i] = spot;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // The direct child of this holder that has the JumpPad somewhere under it.
    private Transform FindPadRoot()
    {
        JumpPad jumpPad = GetComponentInChildren<JumpPad>();
        if (jumpPad == null) return null;
        Transform root = jumpPad.transform;
        while (root.parent != null && root.parent != transform) root = root.parent;
        return root;
    }

    // Each spot: a wire box the size of the pad, plus an arrow showing its forward (the pad's facing).
    private void OnDrawGizmos()
    {
        if (spots == null) return;
        // The pad's collider, in the pad root's space (it may sit on a scaled/offset child).
        Vector3 size = Vector3.one;
        Vector3 center = Vector3.zero;
        JumpPad jumpPad = pad != null ? pad.GetComponentInChildren<JumpPad>() : null;
        if (jumpPad != null && jumpPad.TryGetComponent(out BoxCollider box))
        {
            Transform t = box.transform;
            size = Vector3.Scale(box.size, t.lossyScale);
            center = Quaternion.Inverse(pad.rotation) * (t.TransformPoint(box.center) - pad.position);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f);
        foreach (Transform spot in spots)
        {
            if (spot == null) continue;
            Gizmos.matrix = Matrix4x4.TRS(spot.position, spot.rotation, Vector3.one);
            Gizmos.DrawWireCube(center, size);
            Gizmos.DrawLine(center, center + Vector3.forward * (size.z * 0.5f + 1f));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
