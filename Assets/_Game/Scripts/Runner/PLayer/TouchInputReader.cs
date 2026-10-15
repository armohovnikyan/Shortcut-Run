using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Only job: turn a touch drag into a turn value. Knows nothing about
/// Player, Runner, or race state — safe to poll even before the race starts.
/// TurnInput is in screen widths per second, so the same swipe turns the same
/// amount on any phone resolution and frame rate.
/// </summary>
public class TouchInputReader : MonoBehaviour, IInputReader
{
    public float TurnInput { get; private set; }

    private void Update()
    {
        TurnInput = 0f;

        if (Touchscreen.current == null || Time.deltaTime <= 0f)
            return;

        var touch = Touchscreen.current.primaryTouch;
        if (!touch.press.isPressed)
            return;

        float screenFraction = touch.delta.ReadValue().x / Screen.width;
        TurnInput = screenFraction / Time.deltaTime;
    }
}
