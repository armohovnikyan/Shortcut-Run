using UnityEngine;

// Goes on a big flat plane in the scene (not in a level: the water is the same for every level).
// Follows the game camera sideways so its edge is never in view — the plane must be wider than twice the
// camera's far clip distance. The water shader takes its UVs from world space, so the pattern doesn't
// move with the plane. Height is the plane's own Y. GameBootstrap tells it which camera to follow (Follow).
public class InfiniteWater : MonoBehaviour
{
    private Transform followTarget;

    /// <summary>World height of the water surface.</summary>
    public float SurfaceY => transform.position.y;

    /// <summary>Called once by GameBootstrap with the game camera.</summary>
    public void Follow(Transform target) => followTarget = target;

    private void LateUpdate()
    {
        if (followTarget == null) return;

        Vector3 target = followTarget.position;
        transform.position = new Vector3(target.x, transform.position.y, target.z);
    }
}
