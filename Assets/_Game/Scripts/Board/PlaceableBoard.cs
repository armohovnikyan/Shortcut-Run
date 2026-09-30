using System.Collections;
using UnityEngine;

// Spawned into the runner's hands when boards are collected; later placed as a bridge piece.
// The prefab should be on the Road layer, so runners can stand on it once placed.
public class PlaceableBoard : BaseBoard
{
    [Header("Bridge")]
    [Tooltip("Empty space between neighbour boards in a bridge, as a fraction of the board's length. " +
             "0 = touching. The gap is only visual — the collider is stretched over it, so runners never fall through.")]
    [SerializeField, Min(0f)] private float gapRatio = 0.4f;

    [Header("Place pop")]
    [Tooltip("How big the board appears when placed, before shrinking to normal. 1 = no pop.")]
    [SerializeField, Min(1f)] private float popScale = 1.3f;
    [Tooltip("Seconds to shrink back to normal size.")]
    [SerializeField, Min(0f)] private float popDuration = 0.2f;

    private Collider boardCollider;
    private MeshFilter meshFilter;
    private Vector3 normalScale;

    /// <summary>World size of the board mesh (local axes, scale applied). Works on the prefab asset too.</summary>
    public Vector3 Size
    {
        get
        {
            // Lazy: a prefab asset never runs Awake, but NPCs ask it for the size to judge shortcuts.
            if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();
            return Vector3.Scale(meshFilter.sharedMesh.bounds.size, meshFilter.transform.lossyScale);
        }
    }

    /// <summary>Metres of bridge one board covers: its own length plus the gap after it.</summary>
    public float BridgeStep => Size.z * (1f + gapRatio);

    private void Awake()
    {
        boardCollider = GetComponent<Collider>();
        meshFilter = GetComponentInChildren<MeshFilter>();
        normalScale = transform.localScale; // the prefab's own scale = the size a placed board settles at
    }

    // While carried it's only a visual — its collider must not hit the road or other runners.
    public void OnCarried()
    {
        boardCollider.enabled = false;
    }

    /// <summary>Falls out of the runner's hands with a little random toss, then disappears.</summary>
    public void Drop(Transform parent, float lifetime = 2f)
    {
        transform.SetParent(parent, true);
        boardCollider.enabled = true;

        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.AddForce(new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1f, 2.5f), Random.Range(-1.5f, 1.5f)),
                      ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);

        Destroy(gameObject, lifetime);
    }

    public void OnPlaced()
    {
        boardCollider.enabled = true;

        // Visual gap, solid floor: stretch the collider along the bridge (local Z) so it reaches halfway
        // into the gap on both sides. Otherwise a runner walking over the bridge would find water between
        // boards and start bridging on its own. Proportional, so it doesn't depend on the board's scale.
        if (boardCollider is BoxCollider box)
        {
            Vector3 size = box.size;
            size.z *= 1f + gapRatio;
            box.size = size;
        }

        if (popDuration > 0f && popScale > 1f) StartCoroutine(Pop());
        else transform.localScale = normalScale;
    }

    // Appears a bit big, then settles: fast at first, slowing down at the end (ease-out).
    private IEnumerator Pop()
    {
        for (float time = 0f; time < popDuration; time += Time.deltaTime)
        {
            float t = time / popDuration;
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = normalScale * Mathf.Lerp(popScale, 1f, eased);
            yield return null;
        }
        transform.localScale = normalScale;
    }
}
