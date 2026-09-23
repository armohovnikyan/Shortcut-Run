using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

[RequireComponent(typeof(CharacterController))]
public class Player : Runner
{
    [SerializeField] private float turnSpeed;
    private CharacterController _controller;
    private float turnDelta;
    protected override void Awake()
    {
        base.Awake();
        _controller = GetComponent<CharacterController>();
    }
    private void Start()
    {

    }
    private void Update()
    {

        BeginRace();
        InputReader();
        Rotate();
    }
    protected override void BeginRace()
    {
        Vector3 dir = transform.forward * baseSpeed;
        _controller.Move(dir * Time.deltaTime);
    }
    protected override void EndRace()
    {
    }

    private void InputReader()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta.ReadValue();
                turnDelta = delta.x * 0.2f;
            }
        }
    }
    private void Rotate()
    {
        if (Mathf.Abs(turnDelta) > 0.001f)
        {
            float rotationAmount = turnDelta * turnSpeed * Time.deltaTime;
            transform.Rotate(0, rotationAmount, 0);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            FinishRun();
        }
    }
}