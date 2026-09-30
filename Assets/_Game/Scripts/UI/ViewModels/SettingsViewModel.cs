/// <summary>The settings panel's switches. Reads and writes GameSettings, which saves them.</summary>
public class SettingsViewModel
{
    private readonly GameSettings settings;

    public readonly Observable<bool> SoundOn;
    public readonly Observable<bool> VibrationOn;

    public SettingsViewModel(GameSettings settings)
    {
        this.settings = settings;
        SoundOn = new Observable<bool>(settings.SoundOn);
        VibrationOn = new Observable<bool>(settings.VibrationOn);
    }

    public void SetSound(bool on)
    {
        settings.SetSound(on);
        SoundOn.Value = on;
    }

    public void SetVibration(bool on)
    {
        settings.SetVibration(on);
        VibrationOn.Value = on;
    }
}
