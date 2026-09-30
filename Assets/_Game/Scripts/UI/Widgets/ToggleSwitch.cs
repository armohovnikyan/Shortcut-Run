using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Turns a Slider into an on/off switch: a click slides the handle to the other side.
/// IsOn is the only state; the handle always shows it (1 = on, 0 = off).
/// </summary>
[RequireComponent(typeof(Slider))]
public class ToggleSwitch : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("State before anything sets it (SettingsView overrides this with the saved setting).")]
    [SerializeField] private bool startOn = true;

    [Header("Animation")]
    [SerializeField, Range(0f, 1f)] private float animationDuration = 0.125f;
    [SerializeField] private AnimationCurve slideEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    [SerializeField] private UnityEvent onToggleOn;
    [SerializeField] private UnityEvent onToggleOff;

    private Slider slider;
    private Coroutine slideRoutine;
    private bool initialized;

    public bool IsOn { get; private set; }

    /// <summary>The player clicked the switch. Not raised by SetWithoutNotify.</summary>
    public event Action<bool> ValueChanged;

    private void Awake() => Initialize();

    // Can be needed before Awake: the settings panel starts inactive, but its saved values are set at startup.
    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        slider = GetComponent<Slider>();
        slider.interactable = false; // the slider is only the visual; clicks are handled here
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        IsOn = startOn;
        slider.value = IsOn ? 1f : 0f;
    }

    // A slide cut off by closing the panel must not leave the handle half-way.
    private void OnDisable()
    {
        slideRoutine = null;
        if (initialized) slider.value = IsOn ? 1f : 0f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Initialize();
        IsOn = !IsOn;

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideRoutine());

        if (IsOn) onToggleOn?.Invoke();
        else onToggleOff?.Invoke();
        ValueChanged?.Invoke(IsOn);
    }

    /// <summary>Shows a state without animation or events (e.g. the saved setting).</summary>
    public void SetWithoutNotify(bool on)
    {
        Initialize();
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = null;

        IsOn = on;
        slider.value = on ? 1f : 0f;
    }

    private IEnumerator SlideRoutine()
    {
        float start = slider.value;
        float end = IsOn ? 1f : 0f;
        float time = 0f;

        // Unscaled: the settings panel can be open while the game is paused.
        while (time < animationDuration)
        {
            time += Time.unscaledDeltaTime;
            slider.value = Mathf.Lerp(start, end, slideEase.Evaluate(time / animationDuration));
            yield return null;
        }

        slider.value = end;
        slideRoutine = null;
    }
}
