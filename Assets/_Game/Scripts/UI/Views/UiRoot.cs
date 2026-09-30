using UnityEngine;

/// <summary>
/// Sits on the main canvas. Receives the UI's view model (from GameBootstrap), hands it to every view,
/// and switches the panels when the view model's screen changes. Knows nothing about the game itself.
/// </summary>
public class UiRoot : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private MenuView menu;
    [Tooltip("Parent of the HUD, reward and game-over panels.")]
    [SerializeField] private GameObject gameFlowPanel;
    [SerializeField] private HudView hud;
    [SerializeField] private RewardView reward;
    [SerializeField] private GameOverView gameOver;

    [Header("Overlays")]
    [SerializeField] private SettingsView settings;
    [SerializeField] private ExitConfirmView exitConfirm;

    [Header("Optional")]
    [SerializeField] private PlayerNameTag playerNameTag;
    [Tooltip("The level bar at the top of the menu. Empty until that part of the UI exists.")]
    [SerializeField] private LevelProgressView levelProgress;

    /// <summary>Called once by GameBootstrap. The view model belongs to the caller, which also disposes it.</summary>
    public void Bind(GameUiViewModel viewModel)
    {
        menu.Bind(viewModel);
        hud.Bind(viewModel);
        reward.Bind(viewModel);
        gameOver.Bind(viewModel);
        settings.Bind(viewModel);
        exitConfirm.Bind(viewModel);
        if (playerNameTag != null) playerNameTag.Bind(viewModel);
        if (levelProgress != null) levelProgress.Bind(viewModel.LevelProgress);

        viewModel.Screen.Bind(ShowScreen);
        viewModel.SettingsOpen.Bind(open => settings.gameObject.SetActive(open));
        viewModel.ExitConfirmOpen.Bind(open => exitConfirm.gameObject.SetActive(open));
    }

    private void ShowScreen(UiScreen screen)
    {
        menu.gameObject.SetActive(screen == UiScreen.Menu);
        gameFlowPanel.SetActive(screen != UiScreen.Menu);
        hud.gameObject.SetActive(screen == UiScreen.Race);
        reward.gameObject.SetActive(screen == UiScreen.Reward);
        gameOver.gameObject.SetActive(screen == UiScreen.GameOver);
    }
}
