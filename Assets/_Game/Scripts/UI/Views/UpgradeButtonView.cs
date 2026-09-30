using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One upgrade button of the menu: level, price, and the animations that invite a press.</summary>
public class UpgradeButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private TMP_Text priceLabel;
    [Tooltip("Hidden when the upgrade is maxed (the coin next to the price).")]
    [SerializeField] private GameObject priceIcon;
    [Tooltip("The arrow and price animations.")]
    [SerializeField] private UiPulse[] animations;
    [Tooltip("On = the animations only play while the player has enough coins. Off = they always play.")]
    [SerializeField] private bool animateOnlyWhenAffordable = true;

    public void Bind(UpgradeViewModel viewModel)
    {
        button.onClick.AddListener(viewModel.Buy);

        viewModel.Level.Bind(level => levelLabel.text = $"Lvl. {level}");
        viewModel.Price.Bind(_ => ShowPrice(viewModel));
        viewModel.IsMaxed.Bind(_ => ShowPrice(viewModel));
        viewModel.CanBuy.Bind(canBuy =>
        {
            button.interactable = canBuy;
            foreach (UiPulse pulse in animations)
                if (pulse != null) pulse.enabled = canBuy || !animateOnlyWhenAffordable;
        });
    }

    private void ShowPrice(UpgradeViewModel viewModel)
    {
        bool maxed = viewModel.IsMaxed.Value;
        priceLabel.text = maxed ? "MAX" : viewModel.Price.Value.ToString();
        if (priceIcon != null) priceIcon.SetActive(!maxed);
    }
}
