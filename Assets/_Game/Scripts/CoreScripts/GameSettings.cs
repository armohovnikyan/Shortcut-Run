/// <summary>
/// The player's settings (sound, vibration): the only place they are changed. Saved through GameSave.
/// Sound is applied here (master volume). Vibration is only a saved flag — whatever vibrates the phone checks it.
/// </summary>
public class GameSettings
{
    private readonly GameSave save;

    public bool SoundOn => save.Data.soundOn;
    public bool VibrationOn => save.Data.vibrationOn;

    /// <summary>Takes the saved settings (both on for a new save) and applies the sound.</summary>
    public GameSettings(GameSave save)
    {
        this.save = save;
        ApplySound();
    }

    public void SetSound(bool on)
    {
        save.Data.soundOn = on;
        save.Save();
        ApplySound();
    }

    public void SetVibration(bool on)
    {
        save.Data.vibrationOn = on;
        save.Save();
    }

    private void ApplySound() => UnityEngine.AudioListener.volume = SoundOn ? 1f : 0f;
}
