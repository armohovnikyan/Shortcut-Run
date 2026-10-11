using UnityEngine;

// Goes on a big flat plane in the scene (not in a level: the water is the same for every level).
// Follows the camera sideways so its edge is never in view — the plane must be wider than twice the
// camera's far clip distance. The water shader takes its UVs from world space, so the pattern doesn't
// move with the plane. Height is the plane's own Y.
public class InfiniteWater : MonoBehaviour
{
    [Tooltip("Empty = Camera.main.")]
    [SerializeField] private Camera followCamera;

    /// <summary>World height of the water surface.</summary>
    public float SurfaceY => transform.position.y;

    private void LateUpdate()
    {
        if (followCamera == null) followCamera = Camera.main;
        if (followCamera == null) return;

        Vector3 camera = followCamera.transform.position;
        transform.position = new Vector3(camera.x, transform.position.y, camera.z);
    }
}
