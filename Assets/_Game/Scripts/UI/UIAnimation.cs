using System.Collections;
using TMPro;
using UnityEngine;

public class UIAnimation
{
    private float duration;
    private AnimationCurve numberScaleCurve;

    private AnimationCurve goScaleCurve;

    private TextMeshProUGUI countdownText;
    private RectTransform rectTransform;
    
    public UIAnimation(TextMeshProUGUI text, float duration, AnimationCurve numberScaleCurve, AnimationCurve goScaleCurve)
    {
        countdownText = text;
        rectTransform = countdownText.rectTransform;
        this.duration = duration;
        this.numberScaleCurve = numberScaleCurve;
        this.goScaleCurve = goScaleCurve;
    }

    public IEnumerator PlayNumber(int value)
    {
        
        countdownText.text = value.ToString();
        yield return Animate(numberScaleCurve);
    }

    public IEnumerator PlayGo()
    {
        countdownText.text = "GO!";
        yield return Animate(goScaleCurve);
    }

    private IEnumerator Animate(AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float scale = curve.Evaluate(t);
            rectTransform.localScale = Vector3.one * scale;

            yield return null;
        }

        rectTransform.localScale = Vector3.one * curve.Evaluate(1f);
    }
}