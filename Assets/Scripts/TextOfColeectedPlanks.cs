using System.Collections;
using TMPro;
using UnityEngine;

public class TextOfColeectedPlanks : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] RectTransform rectTransform;

    [Header("Fade Settings")]
    [SerializeField] float moveDistance = 10f;   // how far up it moves
    [SerializeField] float duration = 1f;        // how long the effect takes
    [SerializeField] bool disableAfterFade = true;

    Coroutine fadeRoutine;

    void Awake()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }

    public void SetText(int count,float Y)
    {
        text.text = "+" + count.ToString();
        Vector3 pos = rectTransform.position;
        pos.y = Y;
        rectTransform.position = pos;
         if (fadeRoutine != null){
         StopCoroutine(fadeRoutine);
         }
        fadeRoutine = null;
    }

    public void Fading()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        gameObject.SetActive(true);
        SetAlpha(1f);
        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, moveDistance);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // smoothstep for a nicer easing curve
            float smoothT = t * t * (3f - 2f * t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            SetAlpha(Mathf.Lerp(1f, 0f, smoothT));

            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        SetAlpha(0f);

        if (disableAfterFade)
            gameObject.SetActive(false);
    }

    void SetAlpha(float a)
    {
        Color c = text.color;
        c.a = a;
        text.color = c;
    }
}