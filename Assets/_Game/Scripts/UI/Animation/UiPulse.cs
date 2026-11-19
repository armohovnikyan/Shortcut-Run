using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Looping "go to the peak and come back" animation for a UI element: it can move, stretch, or both.
///   Upgrade arrow: Move Offset (0, 20) + Peak Scale (1.2, 1) = hops up and gets wider at the top.
///   Price field:   Move Offset (0, 0)  + Peak Scale (1.15, 1.15) + Scale From Visible Centre = grows a bit around
///                  the digits and coin, and returns.
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
    [Tooltip("Grow from the middle of what is drawn (texts' actual letters, images) instead of from the pivot. " +
             "For elements whose pivot sits in a corner or whose text doesn't fill its box, like the price.")]
    [SerializeField] private bool scaleFromVisibleCentre;

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
        Vector2 scale = Vector2.LerpUnclamped(Vector2.one, peakScale, amount);
        Vector2 position = restPosition + moveOffset * amount;

        // Scaling happens around the pivot; shift by how far the visible centre would drift so it stays put.
        if (scaleFromVisibleCentre && amount != 0f && TryGetVisibleCentre(out Vector2 centre))
            position += Vector2.Scale(Vector2.Scale(centre, Vector2.one - scale), restScale);

        target.anchoredPosition = position;
        target.localScale = new Vector3(restScale.x * scale.x, restScale.y * scale.y, restScale.z);
    }

    // Centre of everything drawn under the target, in the target's own (unscaled) space, measured from its pivot.
    // Texts count by their letters, not their box, so a right-aligned price centres on the digits it shows.
    private bool TryGetVisibleCentre(out Vector2 centre)
    {
        bool any = false;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled) continue;
            Vector3 a, b;
            if (graphic is TMP_Text text)
            {
                Bounds letters = text.textBounds;
                if (letters.size.x <= 0f) continue; // empty text
                a = letters.min; b = letters.max;
            }
            else
            {
                Rect rect = graphic.rectTransform.rect;
                a = rect.min; b = rect.max;
            }
            Vector2 p = target.InverseTransformPoint(graphic.transform.TransformPoint(a));
            Vector2 q = target.InverseTransformPoint(graphic.transform.TransformPoint(b));
            min = Vector2.Min(min, Vector2.Min(p, q));
            max = Vector2.Max(max, Vector2.Max(p, q));
            any = true;
        }
        centre = any ? (min + max) * 0.5f : Vector2.zero;
        return any;
    }
}
