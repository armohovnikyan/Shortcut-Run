using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One buff button of the in-run HUD: a small square with the icon and "xN" under it.
/// Off (faded, not pressable) while it can't be used: running, cooling down, none left, or after the finish.
/// The fade uses a CanvasGroup on the button (added by itself if missing) so the icon and text fade too.
/// </summary>
public class BuffUseButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Gets the buff's icon. Empty = keep the sprite set in the prefab.")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countLabel;
    [Tooltip("Optional. An Image with Image Type = Filled (Radial 360) over the icon; drains as the cooldown runs out.")]
    [SerializeField] private Image cooldownFill;
    [Tooltip("Opacity of the whole button (icon and text included) while it can't be used.")]
    [SerializeField, Range(0f, 1f)] private float offAlpha = 0.45f;

    private CanvasGroup canvasGroup;

    public void Bind(BuffViewModel viewModel)
    {
        if (!TryGetComponent(out canvasGroup)) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(viewModel.Use);
        if (icon != null && viewModel.Icon != null) icon.sprite = viewModel.Icon;

        viewModel.Count.Bind(count => countLabel.text = $"x{count}");
        viewModel.CanUse.Bind(canUse =>
        {
            button.interactable = canUse;
            canvasGroup.alpha = canUse ? 1f : offAlpha;
        });
        viewModel.WaitLeft.Bind(waitLeft =>
        {
            if (cooldownFill == null) return;
            cooldownFill.fillAmount = waitLeft;
            cooldownFill.enabled = waitLeft > 0f;
        });
    }
}
