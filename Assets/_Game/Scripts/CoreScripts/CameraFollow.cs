using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 cameraOffset = new Vector3(0f, 6f, -7f);

    public float cameraSmoothSpeed = 5f;
    public float rotationSmoothSpeed = 5f; 

    [Tooltip("Follows the player this run manager spawns.")]
    [SerializeField] RunManager runManager;

    bool RaceFinished;
    Transform _playerTransform;

    // Minimal hook so runs can be tested. The full camera pass (pull back with boards, finish pos) comes later.
    void OnEnable()  { if (runManager != null) runManager.PlayerSpawned += OnPlayerSpawned; }
    void OnDisable() { if (runManager != null) runManager.PlayerSpawned -= OnPlayerSpawned; }
    void OnPlayerSpawned(Player player) => SetTarget(player.transform);

    public void SetTarget(Transform target) => _playerTransform = target;

    void LateUpdate()
    {
        if (_playerTransform == null) return;

        Vector3 targetPosition = _playerTransform.position + (_playerTransform.rotation * cameraOffset);

        transform.position = Vector3.Lerp(transform.position, targetPosition, cameraSmoothSpeed * Time.deltaTime);

        Vector3 lookAtTarget = _playerTransform.position + Vector3.up * 1.5f;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    public void RaceEnded()
    {
        RaceFinished = true;
    }
}