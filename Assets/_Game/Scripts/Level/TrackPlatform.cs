using UnityEngine;

/// <summary>
/// A piece of road that isn't part of a spline — an island, a pillar, a stepping stone.
/// For RUNNERS it is road because its collider is solid and on the Road layer (this component isn't needed for that).
/// For the TRACK PLACEMENT window this component is what makes it a target: stacks and stamps can be
/// clicked onto it. They become children of THIS object, so they move with it.
///
/// Keep this object at scale 1 and put the scaled mesh + collider in a child: children inherit scale,
/// so stacks placed on a scaled object would be scaled too (bigger boards, stretched patterns).
/// </summary>
public class TrackPlatform : MonoBehaviour
{
    public const string RoadLayerName = "Road";

    [Tooltip("The collider runners stand on and stacks are placed on — this object's or a child's. " +
             "Empty = found automatically (first solid collider that isn't part of a board stack).")]
    [SerializeField] private Collider surface;

    public Collider Surface => surface != null ? surface : surface = FindSurface();

    // Board stacks placed on the platform are children too — their board colliders must not count.
    private Collider FindSurface()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger && c.GetComponentInParent<BoardStack>() == null) return c;
        return null;
    }

#if UNITY_EDITOR
    // Catches the setup mistakes that would make the platform look like road but not act like it.
    private void OnValidate()
    {
        if (surface == null) surface = FindSurface();
        if (surface == null)
        {
            Debug.LogWarning($"{name}: TrackPlatform has no solid collider on itself or its children.", this);
            return;
        }

        if (Vector3.Distance(transform.localScale, Vector3.one) > 0.001f)
            Debug.LogWarning($"{name}: TrackPlatform object is scaled {transform.localScale} — stacks placed on it " +
                             "would be scaled too. Keep it at scale 1 and scale a child that holds the mesh and collider.", this);

        int road = LayerMask.NameToLayer(RoadLayerName);
        if (road >= 0 && surface.gameObject.layer != road)
            Debug.LogWarning($"{surface.name}: the platform's collider must be on the '{RoadLayerName}' layer — " +
                             "otherwise runners fall through it and the NavMesh skips it.", surface);

        if (surface is CapsuleCollider)
            Debug.LogWarning($"{surface.name}: Capsule Collider has a round top — runners would stand on a dome. " +
                             "Replace it with a Mesh Collider (or Box Collider).", surface);
    }
#endif
}
