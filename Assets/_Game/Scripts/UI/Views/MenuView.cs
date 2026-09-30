using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The menu screen: coins, settings button, the two upgrade buttons, name field and PLAY.</summary>
public class MenuView : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Text coinsLabel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private UpgradeButtonView boardUpgrade;
    [SerializeField] private UpgradeButtonView offlineEarnUpgrade;

    public void Bind(GameUiViewModel viewModel)
    {
        playButton.onClick.AddListener(viewModel.Play);
        settingsButton.onClick.AddListener(viewModel.OpenSettings);

        viewModel.Coins.Bind(coins => coinsLabel.text = coins.ToString());

        nameInput.characterLimit = PlayerProfile.MaxNameLength;
        viewModel.PlayerName.Bind(playerName => nameInput.SetTextWithoutNotify(playerName));
        nameInput.onEndEdit.AddListener(typed =>
        {
            viewModel.SetPlayerName(typed);
            // The saved name can differ from what was typed (trimmed, or the default for an empty field).
            nameInput.SetTextWithoutNotify(viewModel.PlayerName.Value);
        });

        boardUpgrade.Bind(viewModel.BoardUpgrade);
        offlineEarnUpgrade.Bind(viewModel.OfflineEarnUpgrade);
    }
}
