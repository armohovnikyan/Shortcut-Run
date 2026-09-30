using TMPro;
using UnityEngine;

/// <summary>
/// The name title over the player: a canvas element that follows a point above the player's head.
/// Shows the saved player name (GameUiViewModel.PlayerName) and updates the moment it's edited.
/// </summary>
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Point the tag sits on, measured from the player's feet, in metres.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.4f, 0f);
    [Tooltip("Off = the tag is only shown in the menu.")]
    [SerializeField] private bool showDuringRace = true;

    private GameUiViewModel viewModel;
    private RectTransform rect;
    private RectTransform parentRect;
    private Canvas canvas;
    private Camera worldCamera;

    public void Bind(GameUiViewModel viewModel)
    {
        this.viewModel = viewModel;
        rect = (RectTransform)transform;
        parentRect = (RectTransform)rect.parent;
        canvas = GetComponentInParent<Canvas>(true);

        viewModel.PlayerName.Bind(playerName => nameLabel.text = playerName);
        viewModel.Screen.Bind(_ => Refresh());
        viewModel.PlayerTarget.Bind(_ => Refresh());
    }

    private void Refresh()
    {
        UiScreen screen = viewModel.Screen.Value;
        bool visible = viewModel.PlayerTarget.Value != null
                       && (screen == UiScreen.Menu || (showDuringRace && screen == UiScreen.Race));

        gameObject.SetActive(visible);
        if (visible) Follow(); // no one-frame flash at the old position
    }

    // LateUpdate: after the player moved this frame.
    private void LateUpdate() => Follow();

    private void Follow()
    {
        Transform target = viewModel?.PlayerTarget.Value;
        if (target == null) return;

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(target.position + worldOffset);
        if (screenPoint.z <= 0f) return; // behind the camera

        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, canvasCamera, out Vector2 local))
            rect.localPosition = local;
    }
}
