using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour, ICharacter
{
    [Header("Настройки движения")]
    public float forwardSpeed = 7f;
    public float turnSpeed = 90f;

    private CharacterController _characterController;
    private float _currentTurnInput = 0f;
    public int Place;
    public AnimationsControl Animation;
    //[SerializeField] TMP_Text PlaceText;

    PlankCollector PlanksInfo;

    public void Start()
    {
        _characterController = GetComponent<CharacterController>();
        Animation = GetComponent<AnimationsControl>();
        PlanksInfo = GetComponent<PlankCollector>();
        GameManager.Instance.RegisterRunner(transform);

        Animation.SetIdle();

        GameManager.Instance.Playing -= OnPlay;
        GameManager.Instance.Playing += OnPlay;
        //StartCoroutine(StartCountdownRoutine());
    }

    void Update()
    {
        if (GameManager.Instance.GameFlow != GameFlow.Playing) return;

        HandleInput();
        MoveAndRotatePlayer();
        SetPlace();
    }
    void SetPlace()
    {
        int place = GameManager.Instance.GetMyPlace(transform);
        UIManager.Instance.SetPlayersPlace(place);
        Place = place;
    }
    public void IsFailing()
    {
        Animation.SetFailing();
    }

    public void CheckPlanks()
    {
        if (PlanksInfo._collectedPlanks.Count > 0)
        {
            Animation.SetRunningWithPlanks();
        }
        else
        {
            Animation.SetRunning();
        }
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

    void OnPlay()
    {
        Animation.SetRunning();
    }
}