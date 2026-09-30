using UnityEngine;
using UnityEngine.UI;

/// <summary>The "Back to menu?" question shown over a paused run.</summary>
public class ExitConfirmView : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button confirmButton;

    public void Bind(GameUiViewModel viewModel)
    {
        closeButton.onClick.AddListener(viewModel.CancelExit);
        denyButton.onClick.AddListener(viewModel.CancelExit);
        confirmButton.onClick.AddListener(viewModel.ConfirmExit);
    }
}
