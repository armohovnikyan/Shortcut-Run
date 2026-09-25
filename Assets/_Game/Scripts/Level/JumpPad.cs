using UnityEngine;

// Put on the jump pad's collider, on the Road layer — the runner's GroundProbe finds it underfoot.
// Height and airtime are the same for everyone; the distance comes from the runner's own
// forward speed during the airtime, so faster runners land farther.
[RequireComponent(typeof(Collider))]
public class JumpPad : MonoBehaviour
{
    [SerializeField, Min(0f)] private float height = 2f;
    [SerializeField, Min(0.01f)] private float duration = 2f;

    public float Height => height;
    public float Duration => duration;
}
