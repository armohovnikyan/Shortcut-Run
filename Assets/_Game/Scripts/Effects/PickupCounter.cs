using TMPro;
using UnityEngine;

/// <summary>
/// The "+N" above the player's board stack: counts the boards picked up in a row, and once nothing has been
/// picked up for Hold Time it floats up and fades out (the next pickup starts again from +1).
/// Sits on a world-space canvas; follows the runner's stack and faces the camera. GameEffects creates it and
/// tells it what to follow (Follow) and when boards came in (Add) — it never looks for the game itself.
/// </summary>
public class PickupCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [Tooltip("Height of the number above the top board, in metres.")]
    [SerializeField] private float heightAboveStack = 0.6f;
    [Tooltip("Seconds without a pickup before the number goes away.")]
    [SerializeField, Min(0f)] private float holdTime = 1f;
    [Tooltip("How far the number floats up while fading out, in metres.")]
    [SerializeField] private float floatDistance = 0.6f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f;

    private Runner runner;
    private Transform view;
    private int count;
    private float sinceLastPickup;
    private float fadeTime = -1f; // < 0 = not fading

    private void Awake() => SetAlpha(0f);

    /// <summary>Called by GameEffects: whose stack to sit on, and the camera to face (may be null).</summary>
    public void Follow(Runner target, Transform viewer)
    {
        runner = target;
        view = viewer;
        count = 0;
        fadeTime = -1f;
        SetAlpha(0f);
    }

    /// <summary>Boards just came in.</summary>
    public void Add(int amount)
    {
        count += amount;
        sinceLastPickup = 0f;
        fadeTime = -1f;
        text.text = "+" + count;
        SetAlpha(1f);
    }

    private void LateUpdate()
    {
        if (runner == null || text.alpha <= 0f) return;

        float rise = 0f;
        if (fadeTime < 0f)
        {
            sinceLastPickup += Time.deltaTime;
            if (sinceLastPickup >= holdTime)
            {
                fadeTime = 0f;
                count = 0; // a pickup during the fade starts over from +1
            }
        }
        else
        {
            fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTime / fadeDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            rise = floatDistance * t;
            SetAlpha(1f - t);
        }

        transform.position = runner.BoardStackTop + Vector3.up * (heightAboveStack + rise);
        if (view != null) transform.rotation = view.rotation; // flat towards the camera
    }

    private void SetAlpha(float alpha)
    {
        if (text != null) text.alpha = alpha;
    }
}
