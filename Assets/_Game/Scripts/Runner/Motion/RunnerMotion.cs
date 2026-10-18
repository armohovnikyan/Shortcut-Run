using System;
using UnityEngine;
using UnityEngine.AI;

public enum MotionState { OnRoad, Bridging, Jumping, Climbing, Falling }

/// <summary>
/// Owns the runner's height: follow the road, bridge over gaps, jump, climb, fall.
/// The runner decides where it wants to go horizontally, passes that in as "planned",
/// and gets back the final position. Reports what happened through events — no animations here.
/// </summary>
[Serializable]
public class RunnerMotion
{
    [Tooltip("Seconds without ground before it counts as leaving the road (ignores seams between road pieces).")]
    [SerializeField] private float offRoadDebounce = 0.05f;
    [Header("Jump when out of boards")]
    [SerializeField] private float jumpHeight = 1f;
    [SerializeField] private float jumpDuration = 1f;
    [Header("Climb / fall")]
    [SerializeField] private float climbDuration = 0.25f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("A jump that ends over nothing keeps dropping, still able to land or climb, until it is this far " +
             "from the NavMesh surface where the jump started (negative = below). Only then it is a fall.")]
    [SerializeField] private float fallMargin = -2f;

    private const float NavMeshSampleRadius = 3f;

    private GroundProbe probe;
    private BridgeBuilder bridge;

    private float groundY;         // current surface height; also the bridge height
    private float offRoadTimer;
    private float stateTime;
    private float jumpStartY, currentJumpHeight, currentJumpDuration;
    private Vector3 climbFrom, climbTo;
    private float fallVelocity;
    private float fallBorderY;     // below this, a jump that found nothing becomes a fall
    private Vector3 feet;          // planned position of the current Tick

    public MotionState State { get; private set; }
    public bool IsOnPlacedBoard { get; private set; }
    /// <summary>What was under the feet at the last Tick (road, placed board, jump pad, bonus platform). Null = nothing.</summary>
    public Collider GroundCollider { get; private set; }

    /// <summary>A jump began. True = off a jump pad, false = out of boards over a gap.</summary>
    public event Action<bool> Jumped;
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
        feet = planned;

        bool grounded = probe.TryGetGround(planned, out RaycastHit hit);
        IsOnPlacedBoard = grounded && hit.collider.TryGetComponent(out PlaceableBoard _);
        GroundCollider = grounded ? hit.collider : null;

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
                StartJump(pad.Height, pad.Duration, true);

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

        // A placed board under the feet (own or another runner's bridge) is something to run on — boards
        // are laid only over nothing. On our own fresh bridge this doesn't stop it: each new step lands
        // past the end of the last board, where the ray finds nothing again.
        // groundY isn't taken from the hit: placed boards sit slightly below the surface they were laid for.
        if (grounded) return WithY(planned, groundY);

        if (bridge.NeedsBoard(planned))
        {
            if (bridge.TryPlace(planned, forward, groundY))
                BoardPlaced?.Invoke();
            else
                StartJump(jumpHeight, jumpDuration, false);
        }

        return WithY(planned, groundY);
    }

    private Vector3 TickJumping(Vector3 planned, Vector3 forward, bool grounded, RaycastHit hit, float deltaTime)
    {
        stateTime += deltaTime;
        // Not clamped: past t = 1 the same arc keeps going down below the start height.
        float t = stateTime / currentJumpDuration;
        float y = jumpStartY + 4f * currentJumpHeight * t * (1f - t);

        // Coming down onto something higher than the arc — land early instead of sinking into it.
        if (t > 0.5f && grounded && hit.point.y >= y)
        {
            Land(hit.point.y);
            return WithY(planned, groundY);
        }

        if (t < 1f) return WithY(planned, y);

        // Back at the start height with boards in hand (a jump pad over a gap): bridge on from here.
        if (bridge.HasBoards)
        {
            groundY = jumpStartY;
            StartBridging();
            return WithY(planned, groundY);
        }

        // Still dropping below the start height: climb onto road ahead while it's in reach.
        Vector3 current = WithY(planned, y);
        if (probe.TryFindRoadAhead(current, forward, out Vector3 grabPoint))
        {
            groundY = y;
            StartClimb(current, grabPoint);
            return current;
        }

        // Passed the fall border with nothing to land on or climb: fall, keeping the arc's downward speed.
        if (y <= fallBorderY)
        {
            groundY = y;
            StartFalling(4f * currentJumpHeight * (1f - 2f * t) / currentJumpDuration);
        }

        return current;
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

    private void StartJump(float height, float duration, bool fromPad)
    {
        State = MotionState.Jumping;
        stateTime = 0f;
        jumpStartY = groundY;
        currentJumpHeight = height;
        currentJumpDuration = duration;
        fallBorderY = SurfaceY(feet, groundY) + fallMargin;
        Jumped?.Invoke(fromPad);
    }

    private void StartClimb(Vector3 from, Vector3 to)
    {
        State = MotionState.Climbing;
        stateTime = 0f;
        climbFrom = from;
        climbTo = to;
        ClimbStarted?.Invoke();
    }

    private void StartFalling(float startVelocity)
    {
        State = MotionState.Falling;
        fallVelocity = startVelocity;
        Fell?.Invoke();
    }

    // NavMesh surface height near this point — what the fall border is measured from. No NavMesh near = the road height.
    private static float SurfaceY(Vector3 point, float fallback) =>
        NavMesh.SamplePosition(point, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas) ? hit.position.y : fallback;

    private void Land(float surfaceY)
    {
        State = MotionState.OnRoad;
        groundY = surfaceY;
        offRoadTimer = 0f;
        Landed?.Invoke();
    }

    private static Vector3 WithY(Vector3 position, float y) => new Vector3(position.x, y, position.z);
}
