using System;
using UnityEngine;

public enum MotionState { OnRoad, Bridging, Jumping, Climbing, Falling }

/// <summary>
/// Owns the runner's height: follow the road, bridge over gaps, jump, climb, fall.
/// The runner decides where it wants to go horizontally, passes that in as "planned",
/// and gets back the final position. Reports what happened through events — no animations here.
/// </summary>
[Serializable]
public class RunnerMotion
{
    [Space]
    [Tooltip("Seconds without ground before it counts as leaving the road (ignores seams between road pieces).")]
    [SerializeField] private float offRoadDebounce = 0.05f;
    [Header("Jump when out of boards")]
    [SerializeField] private float jumpHeight = 1f;
    [SerializeField] private float jumpDuration = 1f;
    [Header("Climb / fall")]
    [SerializeField] private float climbDuration = 0.25f;
    [SerializeField] private float gravity = -20f;

    private GroundProbe probe;
    private BridgeBuilder bridge;

    private float groundY;         // current surface height; also the bridge height
    private float offRoadTimer;
    private float stateTime;
    private float jumpStartY, currentJumpHeight, currentJumpDuration;
    private Vector3 climbFrom, climbTo;
    private float fallVelocity;

    public MotionState State { get; private set; }
    public bool IsOnPlacedBoard { get; private set; }

    public event Action Jumped;
    public event Action ClimbStarted;
    public event Action Landed;
    public event Action Fell;
    public event Action BoardPlaced;

    public void Init(GroundProbe probe, BridgeBuilder bridge, Vector3 position)
    {
        this.probe = probe;
        this.bridge = bridge;
        Resume(position);
    }

    /// <summary>Start over from "standing on the road here" — e.g. an NPC taking over from its NavMeshAgent.</summary>
    public void Resume(Vector3 position)
    {
        State = MotionState.OnRoad;
        groundY = position.y;
        offRoadTimer = 0f;
    }

    public Vector3 Tick(Vector3 planned, Vector3 forward, float deltaTime)
    {
        forward.y = 0f;
        forward.Normalize();

        bool grounded = probe.TryGetGround(planned, out RaycastHit hit);
        IsOnPlacedBoard = grounded && hit.collider.TryGetComponent(out PlaceableBoard _);

        switch (State)
        {
            case MotionState.OnRoad:   return TickOnRoad(planned, grounded, hit, deltaTime);
            case MotionState.Bridging: return TickBridging(planned, forward, grounded, hit);
            case MotionState.Jumping:  return TickJumping(planned, forward, grounded, hit, deltaTime);
            case MotionState.Climbing: return TickClimbing(deltaTime);
            default:                   return TickFalling(planned, deltaTime);
        }
    }

    // ---------- States ----------

    private Vector3 TickOnRoad(Vector3 planned, bool grounded, RaycastHit hit, float deltaTime)
    {
        if (grounded)
        {
            offRoadTimer = 0f;
            groundY = hit.point.y;

            if (hit.collider.TryGetComponent(out JumpPad pad))
                StartJump(pad.Height, pad.Duration);

            return WithY(planned, groundY);
        }

        offRoadTimer += deltaTime;
        if (offRoadTimer >= offRoadDebounce)
            StartBridging();

        return WithY(planned, groundY);
    }

    private Vector3 TickBridging(Vector3 planned, Vector3 forward, bool grounded, RaycastHit hit)
    {
        // Reached real road again (not our own fresh boards).
        if (grounded && !IsOnPlacedBoard)
        {
            Land(hit.point.y);
            return WithY(planned, groundY);
        }

        if (bridge.NeedsBoard(planned))
        {
            if (bridge.TryPlace(planned, forward, groundY))
                BoardPlaced?.Invoke();
            else
                StartJump(jumpHeight, jumpDuration);
        }

        return WithY(planned, groundY);
    }

    private Vector3 TickJumping(Vector3 planned, Vector3 forward, bool grounded, RaycastHit hit, float deltaTime)
    {
        stateTime += deltaTime;
        float t = Mathf.Clamp01(stateTime / currentJumpDuration);
        float y = jumpStartY + 4f * currentJumpHeight * t * (1f - t);

        // Coming down onto something higher than the arc — land early instead of sinking into it.
        if (t > 0.5f && grounded && hit.point.y >= y)
        {
            Land(hit.point.y);
            return WithY(planned, groundY);
        }

        if (t < 1f) return WithY(planned, y);

        // Jump over: land, climb, keep bridging, or fall — in that order.
        groundY = jumpStartY;
        Vector3 end = WithY(planned, groundY);

        if (grounded)
            Land(hit.point.y);
        else if (probe.TryFindRoadAhead(end, forward, out Vector3 grabPoint))
            StartClimb(end, grabPoint);
        else if (bridge.HasBoards)
            StartBridging();
        else
            StartFalling();

        return WithY(planned, groundY);
    }

    private Vector3 TickClimbing(float deltaTime)
    {
        stateTime += deltaTime;
        float t = Mathf.Clamp01(stateTime / climbDuration);
        if (t >= 1f) Land(climbTo.y);

        return Vector3.Lerp(climbFrom, climbTo, t);
    }

    private Vector3 TickFalling(Vector3 planned, float deltaTime)
    {
        fallVelocity += gravity * deltaTime;
        groundY += fallVelocity * deltaTime;
        return WithY(planned, groundY);
    }

    // ---------- Transitions ----------

    private void StartBridging()
    {
        State = MotionState.Bridging;
        bridge.Begin();
    }

    private void StartJump(float height, float duration)
    {
        State = MotionState.Jumping;
        stateTime = 0f;
        jumpStartY = groundY;
        currentJumpHeight = height;
        currentJumpDuration = duration;
        Jumped?.Invoke();
    }

    private void StartClimb(Vector3 from, Vector3 to)
    {
        State = MotionState.Climbing;
        stateTime = 0f;
        climbFrom = from;
        climbTo = to;
        ClimbStarted?.Invoke();
    }

    private void StartFalling()
    {
        State = MotionState.Falling;
        fallVelocity = 0f;
        Fell?.Invoke();
    }

    private void Land(float surfaceY)
    {
        State = MotionState.OnRoad;
        groundY = surfaceY;
        offRoadTimer = 0f;
        Landed?.Invoke();
    }

    private static Vector3 WithY(Vector3 position, float y) => new Vector3(position.x, y, position.z);
}
