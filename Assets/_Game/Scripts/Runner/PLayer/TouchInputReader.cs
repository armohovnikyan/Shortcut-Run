using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Only job: turn a touch drag into a turn value. Knows nothing about
/// Player, Runner, or race state — safe to poll even before the race starts.
/// </summary>
public class TouchInputReader : MonoBehaviour, IInputReader
{
    [Space]
    [SerializeField] private float sensitivity = 0.2f;

    public float TurnInput { get; private set; }

    private void Update()
    {
        TurnInput = 0f;

        if (Touchscreen.current == null || Touchscreen.current.touches.Count == 0)
            return;

        var touch = Touchscreen.current.touches[0];
        if (touch.phase.ReadValue() == TouchPhase.Moved)
            TurnInput = touch.delta.ReadValue().x * sensitivity;
    }
}