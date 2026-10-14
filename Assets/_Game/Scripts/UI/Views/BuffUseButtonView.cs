using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One buff button of the in-run HUD: a small square with the icon and "xN" under it.
/// Off (greyed, not pressable) while it can't be used: before GO, running, cooling down, or none left.
/// </summary>
public class BuffUseButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Gets the buff's icon. Empty = keep the sprite set in the prefab.")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countLabel;
    [Tooltip("Optional. An Image with Image Type = Filled (Radial 360) over the icon; drains as the cooldown runs out.")]
    [SerializeField] private Image cooldownFill;
    [Tooltip("Fades the whole button while it is off.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float offAlpha = 0.45f;

    public void Bind(BuffViewModel viewModel)
    {
        button.onClick.AddListener(viewModel.Use);
        if (icon != null && viewModel.Icon != null) icon.sprite = viewModel.Icon;

        viewModel.Count.Bind(count => countLabel.text = $"x{count}");
        viewModel.CanUse.Bind(canUse =>
        {
            button.interactable = canUse;
            if (canvasGroup != null) canvasGroup.alpha = canUse ? 1f : offAlpha;
        });
        viewModel.WaitLeft.Bind(waitLeft =>
        {
            if (cooldownFill == null) return;
            cooldownFill.fillAmount = waitLeft;
            cooldownFill.enabled = waitLeft > 0f;
        });
    }
}
