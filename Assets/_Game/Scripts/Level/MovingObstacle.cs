using UnityEngine;

/// <summary>
/// Moves an obstacle in place: Rotate spins it nonstop, Swing rocks it back and forth like a pendulum.
/// Turns around this object's own pivot, so for a swing put this on an empty at the hinge point
/// and the visible mesh as a child hanging below it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MovingObstacle : MonoBehaviour
{
    private enum Motion { Rotate, Swing }

    [SerializeField] private Motion motion = Motion.Rotate;
    [Tooltip("Local axis it turns around. Up spins it flat like a turnstile; Forward swings it across the road.")]
    [SerializeField] private Vector3 axis = Vector3.up;

    [Header("Rotate")]
    [Tooltip("Degrees per second. Negative spins the other way.")]
    [SerializeField] private float rotateSpeed = 90f;

    [Header("Swing")]
    [Tooltip("Furthest it swings to each side of its start rotation, in degrees.")]
    [SerializeField, Range(0f, 180f)] private float swingAngle = 60f;
    [Tooltip("Seconds for one full swing there and back.")]
    [SerializeField, Min(0.1f)] private float swingPeriod = 2f;

    [Tooltip("Where in its cycle it starts, 0–1. Offsets obstacles of the same kind so they don't move in lockstep.")]
    [SerializeField, Range(0f, 1f)] private float phase;

    private Rigidbody body;
    private Quaternion startRotation;

    private void Awake()
    {
        // Kinematic body moved with MoveRotation: physics sees it moving, so runners get hit
        // instead of the collider teleporting through them.
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        startRotation = transform.localRotation;
    }

    private void FixedUpdate()
    {
        // Angle comes from the clock, not added up each step, so it never drifts.
        float time = Time.time;
        float angle = motion == Motion.Rotate
            ? rotateSpeed * time + phase * 360f
            : swingAngle * Mathf.Sin((time / swingPeriod + phase) * 2f * Mathf.PI);

        Quaternion local = startRotation * Quaternion.AngleAxis(angle, axis.normalized);
        Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        body.MoveRotation(parent * local);
    }
}
