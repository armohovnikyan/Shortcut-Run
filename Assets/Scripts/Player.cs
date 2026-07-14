using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки бега")]
    public float forwardSpeed = 5f;      
    public float laneChangeSpeed = 10f;  

    [Header("Ограничения дороги")]
    public float maxLaneOffset = 3f;

    private CharacterController _characterController;
    private bool _isGameStarted = false;
    private float _targetHorizontalPosition = 0f; 

    void Start()
    {
        _characterController = GetComponent<CharacterController>();

        StartCoroutine(StartCountdownRoutine());
    }

    void Update()
    {
        if (!_isGameStarted) return;
        HandleInput();
        MovePlayer();
    }

    void HandleInput()
    {
        float swipedDirection = 0f;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta.ReadValue();

                if (Mathf.Abs(delta.x) > 5f)
                {
                    swipedDirection = delta.x;
                }
            }
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                swipedDirection = -10f;
            }
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                swipedDirection = 10f;
            }
        }

        if (Mathf.Abs(swipedDirection) > 0.1f)
        {
            _targetHorizontalPosition += swipedDirection * Time.deltaTime * 0.5f;

            _targetHorizontalPosition = Mathf.Clamp(_targetHorizontalPosition, -maxLaneOffset, maxLaneOffset);
        }
    }

    void MovePlayer()
    {
        Vector3 moveDirection = transform.forward * forwardSpeed;

        float currentX = Mathf.Lerp(transform.position.x, _targetHorizontalPosition, laneChangeSpeed * Time.deltaTime);

        float xDiff = currentX - transform.position.x;

        Vector3 finalMove = new Vector3(xDiff / Time.deltaTime, 0f, moveDirection.z);

        if (!_characterController.isGrounded)
        {
            finalMove.y = Physics.gravity.y;
        }

        _characterController.Move(finalMove * Time.deltaTime);
    }

    private IEnumerator StartCountdownRoutine()
    {
        Debug.Log("3...");
        yield return new WaitForSeconds(1f);
        Debug.Log("2...");
        yield return new WaitForSeconds(1f);
        Debug.Log("1...");
        yield return new WaitForSeconds(1f);
        Debug.Log("СТАРТ!");

        _isGameStarted = true;
    }
}