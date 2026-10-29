using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// One short phone tap. Handheld.Vibrate is a long buzz (~0.4 s) with no strength, so this talks to the
/// platform directly: Android's Vibrator (length + strength), iOS's impact feedback (Plugins/iOS/Haptics.mm).
/// Does nothing in the Editor and on other platforms. Whether to vibrate at all (the setting) is the caller's call.
/// </summary>
public static class Haptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass vibrationEffect; // null before Android 8: no strength control there
    private static bool androidReady;

    private static void InitAndroid()
    {
        androidReady = true;
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") >= 26) vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Haptics: no Android vibrator ({e.Message}).");
            vibrator = null;
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _HapticImpact(int style);
#endif

    /// <param name="milliseconds">Android only: how long. iOS taps have a fixed length.</param>
    /// <param name="strength">0..1. Android: the motor strength. iOS: below 0.5 = light tap, above = medium.</param>
    public static void Tap(int milliseconds, float strength)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!androidReady) InitAndroid();
        if (vibrator == null) return;

        if (vibrationEffect != null)
        {
            int amplitude = Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 1, 255);
            using AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude);
            vibrator.Call("vibrate", effect);
        }
        else vibrator.Call("vibrate", (long)milliseconds);
#elif UNITY_IOS && !UNITY_EDITOR
        _HapticImpact(strength < 0.5f ? 0 : 1);
#endif
    }

#if UNITY_ANDROID
    // Never called. Unity adds the VIBRATE permission to the Android build only when Handheld.Vibrate
    // appears somewhere in the scripts, and the taps above need that permission.
    private static void RequestVibratePermission() => Handheld.Vibrate();
#endif
}
