using UnityEngine;

/// <summary>
/// Trial version of CameraFollow, used only in the CameraTest scene. Connected to the run by GameBootstrap
/// (Bind) — it doesn't look for the run manager itself.
///  - Follow: behind the player's heading (smoothed on its own, so sharp turns and jump/climb tilts don't
///    swing it), fixed tilt. Every boardsPerStep boards in hand: one step up, back and a bit more tilt.
///  - Fall: freezes where it is at the fall moment — no move, no turn.
///  - Finish: leaves the player and moves to a view in front of the platform the run ends on — the finish
///    platform's view point (always centred, whichever place the player got) or the reached bonus platform.
///    One offset, worked out from that point, so randomly placed bonus platforms need no camera setup of their own.
/// </summary>
public class StableCameraFollow : MonoBehaviour
{
    private enum Mode { Follow, Fallen, Finish }

    public Vector3 cameraOffset = new Vector3(0f, 4f, -2f);

    public float cameraSmoothSpeed = 10f;
    public float rotationSmoothSpeed = 10f;

    [Header("Stabilisation")]
    [Tooltip("How fast the camera turns after the player's heading. Lower = calmer on sharp turns.")]
    [SerializeField] float yawSmoothSpeed = 8f;
    [Tooltip("Downward tilt in degrees with no boards. The camera does not look at the player, so the player stays low on screen.")]
    [SerializeField] float pitch = 28f;

    [Header("Board pull-back")]
    [Tooltip("Every this many boards in hand the camera steps back and up once.")]
    [SerializeField] int boardsPerStep = 20;
    [SerializeField] float backPerStep = 2f;
    [SerializeField] float upPerStep = 1.6f;   // same ratio as the offset (8:10), so the player keeps its spot on screen
    [Tooltip("Extra downward tilt per step, in degrees, so the road ahead and the stack stay in view.")]
    [SerializeField] float pitchPerStep = 1.5f;
    [Tooltip("Cap: 8 steps × 20 boards = the camera stops growing at 160 boards.")]
    [SerializeField] int maxSteps = 8;
    [SerializeField] float offsetSmoothSpeed = 2f;

    [Header("Fall and finish")]
    [Tooltip("Height above the platform's view point the camera aims at in the finish view.")]
    [SerializeField] float lookHeight = 2f;
    [Tooltip("Camera position for the finish, in the view point's own axes: Z+ = in front (its blue arrow), " +
             "Y = up. The same offset is used on the finish and on every bonus platform.")]
    [SerializeField] Vector3 finishOffset = new Vector3(0f, 6f, 7f);
    [SerializeField] float finishMoveSpeed = 3f;

    RunManager _run;
    Runner _target;
    Mode _mode;
    Transform _finishPoint;
    float _yaw;
    float _currentPitch;
    Vector3 _currentOffset;

    /// <summary>Called once by GameBootstrap. Follows every player this run manager spawns.</summary>
    public void Bind(RunManager run)
    {
        Unbind();
        _run = run;
        if (_run == null) return;

        _run.PlayerSpawned += OnPlayerSpawned;
        _run.PlayerFinishStarted += OnFinishStarted;
        // GameManager may have spawned the player before Bind ran (Start order isn't fixed).
        if (_run.Player != null) SetTarget(_run.Player);
    }

    void OnDestroy() => Unbind();

    void Unbind()
    {
        if (_run != null)
        {
            _run.PlayerSpawned -= OnPlayerSpawned;
            _run.PlayerFinishStarted -= OnFinishStarted;
            _run = null;
        }
        SetTarget(null);
    }

    void OnPlayerSpawned(Player player) => SetTarget(player);

    // New player (start, Replay, Next level): back to following, placed behind it at once, no glide.
    void SetTarget(Runner target)
    {
        if (_target != null) _target.Fell -= OnTargetFell;
        _target = target;
        _mode = Mode.Follow;
        _finishPoint = null;
        if (_target == null) return;

        _target.Fell += OnTargetFell;
        _yaw = _target.transform.eulerAngles.y;
        int steps = BoardSteps();
        _currentOffset = OffsetFor(steps);
        _currentPitch = PitchFor(steps);
        transform.SetPositionAndRotation(FollowPosition(), FollowRotation());
    }

    // Race fall → stays here (game over). Bonus fall → stays here until RunManager starts the drag-back finish.
    void OnTargetFell(Runner runner)
    {
        if (_mode == Mode.Follow) _mode = Mode.Fallen;
    }

    // Starts with the walk to the stand point, so the camera never swings round with the runner turning to face it.
    void OnFinishStarted(Transform viewPoint)
    {
        _finishPoint = viewPoint;
        _mode = Mode.Finish;
    }

    void LateUpdate()
    {
        if (_target == null) return;

        float dt = Time.deltaTime;
        switch (_mode)
        {
            case Mode.Follow: Follow(dt); break;
            case Mode.Fallen: break; // frozen: no move, no turn
            case Mode.Finish: MoveToFinish(dt); break;
        }
    }

    void Follow(float dt)
    {
        // Only the heading, smoothed on its own: sharp turns and jump/climb tilts don't swing the camera.
        _yaw = Mathf.LerpAngle(_yaw, _target.transform.eulerAngles.y, Damp(yawSmoothSpeed, dt));

        int steps = BoardSteps();
        _currentOffset = Vector3.Lerp(_currentOffset, OffsetFor(steps), Damp(offsetSmoothSpeed, dt));
        _currentPitch = Mathf.Lerp(_currentPitch, PitchFor(steps), Damp(offsetSmoothSpeed, dt));

        transform.position = Vector3.Lerp(transform.position, FollowPosition(), Damp(cameraSmoothSpeed, dt));
        transform.rotation = Quaternion.Slerp(transform.rotation, FollowRotation(), Damp(rotationSmoothSpeed, dt));
    }

    void MoveToFinish(float dt)
    {
        if (_finishPoint == null) return; // level unloaded; the next PlayerSpawned resets the camera

        // Yaw only, like the runner standing on it (Runner.FinishRoutine).
        Quaternion facing = Quaternion.Euler(0f, _finishPoint.eulerAngles.y, 0f);
        Vector3 targetPosition = _finishPoint.position + facing * finishOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Damp(finishMoveSpeed, dt));
        LookAt(_finishPoint.position, finishMoveSpeed, dt);
    }

    void LookAt(Vector3 feet, float speed, float dt)
    {
        Vector3 direction = feet + Vector3.up * lookHeight - transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Damp(speed, dt));
    }

    Vector3 FollowPosition() => _target.transform.position + Quaternion.Euler(0f, _yaw, 0f) * _currentOffset;
    Quaternion FollowRotation() => Quaternion.Euler(_currentPitch, _yaw, 0f);

    // Every boardsPerStep boards in hand: one step. Fewer boards → steps back down.
    int BoardSteps() => Mathf.Min(_target.BoardCount / Mathf.Max(1, boardsPerStep), maxSteps);
    Vector3 OffsetFor(int steps) => cameraOffset + new Vector3(0f, upPerStep, -backPerStep) * steps;
    float PitchFor(int steps) => pitch + pitchPerStep * steps;

    // Frame-rate independent smoothing factor.
    static float Damp(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);
}
