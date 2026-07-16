using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float forwardSpeed = 7f;   
    public float turnSpeed = 90f;    

    private CharacterController _characterController;
    private bool _isGameStarted = false;
    private float _currentTurnInput = 0f; 

    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        StartCoroutine(StartCountdownRoutine());
    }

    void Update()
    {
        if (!_isGameStarted) return;

        HandleInput();

        MoveAndRotatePlayer();
    }

    void HandleInput()
    {
        _currentTurnInput = 0f;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta.ReadValue();
                _currentTurnInput = delta.x * 0.2f;
            }
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                _currentTurnInput = -1f; 
            }
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                _currentTurnInput = 1f;  
            }
        }
    }

    void MoveAndRotatePlayer()
    {
        if (Mathf.Abs(_currentTurnInput) > 0.01f)
        {
            float rotationAmount = _currentTurnInput * turnSpeed * Time.deltaTime;
            transform.Rotate(0, rotationAmount, 0);
        }

        Vector3 moveDirection = transform.forward * forwardSpeed;

        if (!_characterController.isGrounded)
        {
            moveDirection.y = Physics.gravity.y;
        }

        _characterController.Move(moveDirection * Time.deltaTime);
    }

    private IEnumerator StartCountdownRoutine()
    {
        yield return new WaitForSeconds(3f);
        _isGameStarted = true;
    }
}