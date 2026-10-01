using UnityEngine;

/// <summary>
/// Trial version of CameraFollow, used only in the CameraTest scene.
/// Follows only the player's heading (smoothed on its own) so sharp turns and jump/climb tilts
/// don't swing the camera, and steps back and up for every boardsPerStep boards in hand.
/// </summary>
public class StableCameraFollow : MonoBehaviour
{
    public Vector3 cameraOffset = new Vector3(0f, 8f, -10f);

    public float cameraSmoothSpeed = 10f;
    public float rotationSmoothSpeed = 10f;

    [Header("Stabilisation")]
    [Tooltip("How fast the camera turns after the player's heading. Lower = calmer on sharp turns.")]
    [SerializeField] float yawSmoothSpeed = 8f;
    [Tooltip("Fixed downward tilt in degrees. The camera does not look at the player, so the player stays low on screen.")]
    [SerializeField] float pitch = 28f;

    [Header("Board pull-back")]
    [Tooltip("Every this many boards in hand the camera steps back and up once.")]
    [SerializeField] int boardsPerStep = 20;
    [SerializeField] float backPerStep = 2f;
    [SerializeField] float upPerStep = 1.6f;   // same ratio as the offset (8:10), so the player keeps its spot on screen
    [Tooltip("Cap, so a huge stack doesn't send the camera into the sky.")]
    [SerializeField] int maxSteps = 5;
    [SerializeField] float offsetSmoothSpeed = 2f;

    [Tooltip("Follows the player this run manager spawns.")]
    [SerializeField] RunManager runManager;

    bool RaceFinished;
    Runner _target;
    float _yaw;
    Vector3 _currentOffset;

    void OnEnable()  { if (runManager != null) runManager.PlayerSpawned += OnPlayerSpawned; }
    void OnDisable() { if (runManager != null) runManager.PlayerSpawned -= OnPlayerSpawned; }
    void OnPlayerSpawned(Player player) => SetTarget(player);

    public void SetTarget(Runner target)
    {
        _target = target;
        if (target == null) return;
        _yaw = target.transform.eulerAngles.y;
        _currentOffset = cameraOffset;
    }

    void LateUpdate()
    {
        if (_target == null) return;

        float dt = Time.deltaTime;
        Vector3 playerPos = _target.transform.position;

        // Only the heading, smoothed on its own: sharp turns and jump/climb tilts don't swing the camera.
        _yaw = Mathf.LerpAngle(_yaw, _target.transform.eulerAngles.y, Damp(yawSmoothSpeed, dt));
        _currentOffset = Vector3.Lerp(_currentOffset, BoardOffset(), Damp(offsetSmoothSpeed, dt));

        Vector3 targetPosition = playerPos + Quaternion.Euler(0f, _yaw, 0f) * _currentOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Damp(cameraSmoothSpeed, dt));

        // Fixed tilt behind the player's heading, like the reference video.
        Quaternion targetRotation = Quaternion.Euler(pitch, _yaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Damp(rotationSmoothSpeed, dt));
    }

    // Every boardsPerStep boards in hand: one step back and up. Fewer boards → steps back down.
    Vector3 BoardOffset()
    {
        int steps = Mathf.Min(_target.BoardCount / Mathf.Max(1, boardsPerStep), maxSteps);
        return cameraOffset + new Vector3(0f, upPerStep, -backPerStep) * steps;
    }

    // Frame-rate independent smoothing factor.
    static float Damp(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);

    public void RaceEnded()
    {
        RaceFinished = true;
    }
}
