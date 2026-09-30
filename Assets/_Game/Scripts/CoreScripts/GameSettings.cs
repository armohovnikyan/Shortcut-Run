using UnityEngine;

/// <summary>
/// The player's settings (sound, vibration): the only place they are changed and saved.
/// Sound is applied here (master volume). Vibration is only a saved flag — whatever vibrates the phone checks it.
/// </summary>
public class GameSettings
{
    private const string SoundKey = "Settings_Sound";
    private const string VibrationKey = "Settings_Vibration";

    public bool SoundOn { get; private set; }
    public bool VibrationOn { get; private set; }

    /// <summary>Loads the saved settings (both on the first time) and applies the sound.</summary>
    public GameSettings()
    {
        SoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        VibrationOn = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        ApplySound();
    }

    public void SetSound(bool on)
    {
        SoundOn = on;
        Save(SoundKey, on);
        ApplySound();
    }

    public void SetVibration(bool on)
    {
        VibrationOn = on;
        Save(VibrationKey, on);
    }

    private void ApplySound() => AudioListener.volume = SoundOn ? 1f : 0f;

    private static void Save(string key, bool on)
    {
        PlayerPrefs.SetInt(key, on ? 1 : 0);
        PlayerPrefs.Save();
    }
}
