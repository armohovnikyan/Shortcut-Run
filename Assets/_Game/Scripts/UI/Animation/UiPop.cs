using System.Collections;
using UnityEngine;

/// <summary>
/// One-shot "pop in": scales the element from Start Scale to its normal size. Used by the countdown numbers.
/// </summary>
public class UiPop : MonoBehaviour
{
    [Tooltip("What is animated. Empty = this object.")]
    [SerializeField] private RectTransform target;
    [Tooltip("Size it starts from. 0 = grows from nothing, 2 = shrinks from double size.")]
    [SerializeField, Min(0f)] private float startScale = 0f;
    [SerializeField, Min(0.01f)] private float duration = 0.65f;
    [Tooltip("Custom = the curve below.")]
    [SerializeField] private EaseType ease = EaseType.BackOut;
    [Tooltip("Only used when Ease is Custom. Value 0 = Start Scale, value 1 = normal size.")]
    [SerializeField] private AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine routine;

    public void Play()
    {
        if (target == null) target = (RectTransform)transform;
        if (routine != null) StopCoroutine(routine);
        routine = null;

        if (!isActiveAndEnabled) { target.localScale = Vector3.one; return; }
        routine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = Ease.Evaluate(ease, elapsed / duration, customCurve);
            target.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, eased);
            yield return null;
        }

        target.localScale = Vector3.one;
        routine = null;
    }
}
