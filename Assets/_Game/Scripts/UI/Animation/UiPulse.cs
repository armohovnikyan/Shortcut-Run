using UnityEngine;

/// <summary>
/// Looping "go to the peak and come back" animation for a UI element: it can move, stretch, or both.
///   Upgrade arrow: Move Offset (0, 20) + Peak Scale (1.2, 1) = hops up and gets wider at the top.
///   Price field:   Move Offset (0, 0)  + Peak Scale (1.15, 1.15) = grows a bit and returns.
/// Runs on unscaled time, so it keeps playing while the game is paused.
/// </summary>
public class UiPulse : MonoBehaviour
{
    [Tooltip("What is animated. Empty = this object.")]
    [SerializeField] private RectTransform target;

    [Header("Peak (the furthest point of the animation)")]
    [Tooltip("How far it moves at the peak, in canvas units. (0, 20) = 20 up.")]
    [SerializeField] private Vector2 moveOffset;
    [Tooltip("Size at the peak. (1, 1) = no change, (1.2, 1) = 20% wider.")]
    [SerializeField] private Vector2 peakScale = Vector2.one;

    [Header("Timing")]
    [Tooltip("Seconds for one full trip: rest → peak → rest.")]
    [SerializeField, Min(0.01f)] private float duration = 0.45f;
    [Tooltip("Seconds it waits at rest before the next trip.")]
    [SerializeField, Min(0f)] private float pause = 0.1f;

    [Header("Curve")]
    [Tooltip("Shape of the way to the peak; the way back is the same, mirrored. Custom = the curve below.")]
    [SerializeField] private EaseType ease = EaseType.SineInOut;
    [Tooltip("Only used when Ease is Custom. Time 0 = rest, time 1 = peak; value 0 = rest, value 1 = peak.")]
    [SerializeField] private AnimationCurve customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Vector2 restPosition;
    private Vector3 restScale;
    private float time;
    private bool hasRest;

    private void OnEnable()
    {
        if (target == null) target = (RectTransform)transform;
        restPosition = target.anchoredPosition;
        restScale = target.localScale;
        hasRest = true;
        time = 0f;
    }

    // Back to rest, so an animation stopped half-way never leaves the element moved or stretched.
    private void OnDisable()
    {
        if (hasRest && target != null) Apply(0f);
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float local = time % (duration + pause);

        float amount = 0f;
        if (local < duration)
        {
            float progress = local / duration;                                      // 0 → 1 over the trip
            float toPeak = progress < 0.5f ? progress * 2f : (1f - progress) * 2f;  // 0 → 1 → 0
            amount = Ease.Evaluate(ease, toPeak, customCurve);
        }
        Apply(amount);
    }

    private void Apply(float amount)
    {
        target.anchoredPosition = restPosition + moveOffset * amount;

        Vector2 scale = Vector2.LerpUnclamped(Vector2.one, peakScale, amount);
        target.localScale = new Vector3(restScale.x * scale.x, restScale.y * scale.y, restScale.z);
    }
}
