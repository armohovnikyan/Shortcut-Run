using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One buff button of the menu: icon, how many are owned, price of one more. Looks like the upgrade buttons.</summary>
public class BuffShopButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Gets the buff's icon. Empty = keep the sprite set in the prefab.")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text priceLabel;
    [Tooltip("The price animations, like on the upgrade buttons.")]
    [SerializeField] private UiPulse[] animations;
    [Tooltip("On = the animations only play while the player has enough coins. Off = they always play.")]
    [SerializeField] private bool animateOnlyWhenAffordable = true;
    [Tooltip("Button background while the player can't pay for one more. Empty = never changes.")]
    [SerializeField] private Sprite cantBuySprite;

    private Sprite canBuySprite;

    public void Bind(BuffViewModel viewModel)
    {
        button.onClick.AddListener(viewModel.Buy);
        canBuySprite = button.image.sprite;
        if (icon != null && viewModel.Icon != null) icon.sprite = viewModel.Icon;

        viewModel.Count.Bind(count => countLabel.text = $"x{count}");
        viewModel.Price.Bind(price => priceLabel.text = price.ToString());
        viewModel.CanBuy.Bind(canBuy =>
        {
            button.interactable = canBuy;
            if (cantBuySprite != null) button.image.sprite = canBuy ? canBuySprite : cantBuySprite;
            foreach (UiPulse pulse in animations)
                if (pulse != null) pulse.enabled = canBuy || !animateOnlyWhenAffordable;
        });
    }
}
