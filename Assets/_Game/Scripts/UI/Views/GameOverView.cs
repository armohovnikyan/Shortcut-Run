using UnityEngine;
using UnityEngine.UI;

/// <summary>The game-over panel (the player fell during the race): RETRY.</summary>
public class GameOverView : MonoBehaviour
{
    [SerializeField] private Button retryButton;

    public void Bind(GameUiViewModel viewModel)
    {
        retryButton.onClick.AddListener(viewModel.Retry);
    }
}
