using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The level bar at the top of the menu (6 - 7 - 8 - 9 - 10). Ready for when that part of the UI is built:
/// put it on the bar, fill the two lists left to right, and assign it to UiRoot's "Level Progress" field.
/// </summary>
public class LevelProgressView : MonoBehaviour
{
    [Tooltip("The level numbers, left to right. Normally " + nameof(LevelProgressViewModel.StagesPerBar) + " of them.")]
    [SerializeField] private TMP_Text[] stageLabels;
    [Tooltip("One bar piece per stage, left to right. Pieces of beaten levels get the Completed colour.")]
    [SerializeField] private Graphic[] stageFills;
    [SerializeField] private Color completedColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color remainingColor = Color.white;

    public void Bind(LevelProgressViewModel viewModel)
    {
        viewModel.Level.Bind(_ => Show(viewModel));
    }

    private void Show(LevelProgressViewModel viewModel)
    {
        for (int i = 0; i < stageLabels.Length; i++)
            if (stageLabels[i] != null) stageLabels[i].text = viewModel.StageNumber(i).ToString();

        for (int i = 0; i < stageFills.Length; i++)
            if (stageFills[i] != null)
                stageFills[i].color = i < viewModel.CompletedStages ? completedColor : remainingColor;
    }
}
