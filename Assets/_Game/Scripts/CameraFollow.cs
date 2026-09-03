using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 cameraOffset = new Vector3(0f, 6f, -7f);

    public float cameraSmoothSpeed = 5f;
    public float rotationSmoothSpeed = 5f;

    Transform playerTransform;
    private void Start()
    {
        playerTransform = GameManager.Instance.GetPlayerTransform();
        InitialPos();

    }
    void LateUpdate()
    {
        if (GameManager.Instance.GameFlow == GameFlow.Died || GameManager.Instance.GameFlow == GameFlow.End) return;

        Vector3 targetPosition = playerTransform.position + (playerTransform.rotation * cameraOffset);

        transform.position = Vector3.Lerp(transform.position, targetPosition, cameraSmoothSpeed * Time.deltaTime);

        Vector3 lookAtTarget = playerTransform.position + Vector3.up * 1.5f;

        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }
    void InitialPos()
    { 
        transform.position = playerTransform.position + (playerTransform.rotation * cameraOffset);

        Vector3 lookAtTarget = playerTransform.position + Vector3.up * 1.5f;

        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position);

        transform.rotation = targetRotation;
    }
}