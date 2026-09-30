using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The reward panel after a finished run: the coins earned and the GET button.</summary>
public class RewardView : MonoBehaviour
{
    [SerializeField] private Button getRewardButton;
    [SerializeField] private TMP_Text rewardLabel;
    [Tooltip("Only shown when the player came 1st. Can be empty.")]
    [SerializeField] private GameObject firstPlaceBadge;

    public void Bind(GameUiViewModel viewModel)
    {
        getRewardButton.onClick.AddListener(viewModel.ClaimReward);

        viewModel.Reward.Coins.Bind(coins => rewardLabel.text = coins.ToString());
        viewModel.Reward.Place.Bind(place =>
        {
            if (firstPlaceBadge != null) firstPlaceBadge.SetActive(place == 1);
        });
    }
}
