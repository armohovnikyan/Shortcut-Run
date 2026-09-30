using UnityEngine;

// The Inspector dropdown for UI animations. Custom = use the animation's own curve field.
public enum EaseType
{
    Linear,
    SineInOut,
    QuadIn,
    QuadOut,
    QuadInOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    BackIn,
    BackOut,
    BackInOut,
    ElasticOut,
    BounceOut,
    Custom
}

/// <summary>Pure math: progress 0..1 in → eased progress out (Back and Elastic go past 1 on purpose).</summary>
public static class Ease
{
    private const float BackOvershoot = 1.70158f;

    public static float Evaluate(EaseType type, float t, AnimationCurve custom = null)
    {
        t = Mathf.Clamp01(t);

        switch (type)
        {
            case EaseType.SineInOut:  return 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
            case EaseType.QuadIn:     return t * t;
            case EaseType.QuadOut:    return 1f - (1f - t) * (1f - t);
            case EaseType.QuadInOut:  return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
            case EaseType.CubicIn:    return t * t * t;
            case EaseType.CubicOut:   return 1f - Mathf.Pow(1f - t, 3f);
            case EaseType.CubicInOut: return t < 0.5f ? 4f * t * t * t : 1f - 4f * Mathf.Pow(1f - t, 3f);
            case EaseType.BackIn:     return BackIn(t);
            case EaseType.BackOut:    return 1f - BackIn(1f - t);
            case EaseType.BackInOut:  return t < 0.5f ? 0.5f * BackIn(2f * t) : 1f - 0.5f * BackIn(2f - 2f * t);
            case EaseType.ElasticOut: return ElasticOut(t);
            case EaseType.BounceOut:  return BounceOut(t);
            case EaseType.Custom:     return custom != null ? custom.Evaluate(t) : t;
            default:                  return t;
        }
    }

    private static float BackIn(float t) => t * t * ((BackOvershoot + 1f) * t - BackOvershoot);

    private static float ElasticOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
    }

    private static float BounceOut(float t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;

        if (t < 1f / d) return n * t * t;
        if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
        if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
        t -= 2.625f / d;
        return n * t * t + 0.984375f;
    }
}
