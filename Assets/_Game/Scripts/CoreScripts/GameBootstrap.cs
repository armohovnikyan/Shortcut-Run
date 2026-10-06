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
    [Tooltip("The game camera. It gets the run manager from here, so it needs no references of its own.")]
    [SerializeField] private StableCameraFollow gameCamera;
    [Tooltip("Plays the game's sounds. Empty = a silent game.")]
    [SerializeField] private GameAudio gameAudio;

    private GameUiViewModel uiViewModel;

    // Start, not Awake: GameManager creates the wallet, profile and upgrades in its Awake.
    private void Start()
    {
        // The camera first: it works without the UI, and tests often run without it.
        if (gameCamera != null && gameManager != null) gameCamera.Bind(gameManager.Run);
        else Debug.LogWarning($"{name}: Game Bootstrap has no Game Camera (or Game Manager) — the camera won't follow the player.", this);

        // Sound before the UI too, for the same reason; the button clicks are added once the UI is bound.
        if (gameAudio != null && gameManager != null) gameAudio.Bind(gameManager);
        else Debug.LogWarning($"{name}: Game Bootstrap has no Game Audio (or Game Manager) — the game will be silent.", this);

        if (gameManager == null || ui == null)
        {
            Debug.LogError($"{name}: Game Bootstrap needs both a Game Manager and a UI — the UI can't work.", this);
            return;
        }

        uiViewModel = new GameUiViewModel(gameManager);
        ui.Bind(uiViewModel);
        if (gameAudio != null) gameAudio.BindUi(ui);
    }

    // Views need no unbinding of their own: they and the view model live and die with the scene.
    private void OnDestroy() => uiViewModel?.Dispose();
}
