using UnityEngine;

// Spawned into the runner's hands when boards are collected; later placed as a bridge piece.
// The prefab should be on the Road layer, so runners can stand on it once placed.
public class PlaceableBoard : BaseBoard
{
    private Collider boardCollider;
    private MeshFilter meshFilter;

    /// <summary>World size of the board mesh (local axes, scale applied).</summary>
    public Vector3 Size => Vector3.Scale(meshFilter.sharedMesh.bounds.size, meshFilter.transform.lossyScale);

    private void Awake()
    {
        boardCollider = GetComponent<Collider>();
        meshFilter = GetComponentInChildren<MeshFilter>();
    }

    // While carried it's only a visual — its collider must not hit the road or other runners.
    public void OnCarried()
    {
        boardCollider.enabled = false;
    }

    public void OnPlaced()
    {
        boardCollider.enabled = true;
        // add effect on placed
    }
}
