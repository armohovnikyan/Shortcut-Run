using UnityEngine;
using UnityEngine.UI;

/// <summary>The settings panel: sound and vibration switches.</summary>
public class SettingsView : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private ToggleSwitch soundToggle;
    [SerializeField] private ToggleSwitch vibrationToggle;

    public void Bind(GameUiViewModel viewModel)
    {
        SettingsViewModel settings = viewModel.Settings;

        closeButton.onClick.AddListener(viewModel.CloseSettings);

        settings.SoundOn.Bind(soundToggle.SetWithoutNotify);
        settings.VibrationOn.Bind(vibrationToggle.SetWithoutNotify);
        soundToggle.ValueChanged += settings.SetSound;
        vibrationToggle.ValueChanged += settings.SetVibration;
    }
}
