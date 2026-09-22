using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 cameraOffset = new Vector3(0f, 6f, -7f);

    public float cameraSmoothSpeed = 5f;
    public float rotationSmoothSpeed = 5f; 

    bool RaceFinished;
    Transform _playerTransform;
    
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