using UnityEngine;

/// <summary>
/// The scene's wiring: the one object that knows both the game and the UI. It builds the UI's view model
/// from the GameManager and hands it to the canvas, so the UI never references the game and the
/// GameManager never references the UI. New systems that need connecting are connected here.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UiRoot ui;

    private GameUiViewModel uiViewModel;

    // Start, not Awake: GameManager creates the wallet, profile and upgrades in its Awake.
    private void Start()
    {
        if (gameManager == null || ui == null)
        {
            Debug.LogError($"{name}: Game Bootstrap needs both a Game Manager and a UI — the UI can't work.", this);
            return;
        }

        uiViewModel = new GameUiViewModel(gameManager);
        ui.Bind(uiViewModel);
    }

    // Views need no unbinding of their own: they and the view model live and die with the scene.
    private void OnDestroy() => uiViewModel?.Dispose();
}
