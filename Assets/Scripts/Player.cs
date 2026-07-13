using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f; // Для мыши чувствительность выше
    public float touchSensitivity = 0.1f; // Для пальца ниже
    public Transform playerCamera;
    public TextMeshProUGUI countdownText;
    public Vector3 cameraOffset = new Vector3(0f, 2f, -5f);

    private float _verticalRotation = 0f;
    private CharacterController _characterController;
    private bool _isGameStarted = false;
    private float _horizontalRotation = 0f;


    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = Camera.main.transform;

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        StartCoroutine(StartCountdownRoutine());
    }

    void Update()
    {
        playerCamera.LookAt(transform.position + Vector3.up);
        HandleCameraRotation();
    }
    void HandleCameraRotation()
    {
        if (_isGameStarted)
        {
            CheckCameraRotation();
            Quaternion rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0);
            Vector3 positionOffset = rotation * cameraOffset;
            playerCamera.position = transform.position + positionOffset;

            MoveForwardAutomatically();
        }
        else
        {
            Quaternion defaultRotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0);
            playerCamera.position = transform.position + (defaultRotation * cameraOffset);
        }
    }

    void CheckCameraRotation()
    {
         float mouseX = 0f;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta.ReadValue();
                mouseX = delta.x * touchSensitivity;
            }
        }
        else if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        }
        _horizontalRotation += mouseX;
    }

    void MoveForwardAutomatically()
    {
        Vector3 move = playerCamera.forward;
        move.y = 0;
        move.Normalize();
        _characterController.Move(move * moveSpeed * Time.deltaTime);
    }

    IEnumerator StartCountdownRoutine()
    {
        countdownText.text = "3";
        yield return new WaitForSeconds(1f);
        countdownText.text = "2";
        yield return new WaitForSeconds(1f);
        countdownText.text = "1";
        yield return new WaitForSeconds(1f);
        countdownText.text = "Run!";
        _isGameStarted = true;
        yield return new WaitForSeconds(0.5f);
        countdownText.text = "";
    }
}