using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The in-run HUD: coins, home button, the 3-2-1-GO countdown and the player's live place.</summary>
public class HudView : MonoBehaviour
{
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private TMP_Text coinsLabel;

    [Header("Place")]
    [SerializeField] private GameObject placeRoot;
    [SerializeField] private TMP_Text placeLabel;
    [SerializeField] private TMP_Text placeSuffixLabel;

    [Header("Countdown")]
    [Tooltip("Shown from PLAY until GO! has been on screen (also holds the swipe hint).")]
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text countdownLabel;
    [Tooltip("Plays on every number and on GO!.")]
    [SerializeField] private UiPop countdownPop;
    [SerializeField] private string goText = "GO!";
    [Tooltip("Seconds GO! stays on screen after the race started.")]
    [SerializeField, Min(0f)] private float goHoldSeconds = 0.7f;

    private Coroutine hideRoutine;

    public void Bind(GameUiViewModel viewModel)
    {
        backToMenuButton.onClick.AddListener(viewModel.AskExit);

        viewModel.Coins.Bind(coins => coinsLabel.text = coins.ToString());
        viewModel.Hud.PlaceVisible.Bind(visible => placeRoot.SetActive(visible));
        viewModel.Hud.Place.Bind(place =>
        {
            placeLabel.text = place.ToString();
            placeSuffixLabel.text = HudViewModel.PlaceSuffix(place);
        });
        viewModel.Hud.Countdown.Bind(ShowCountdown);
    }

    private void ShowCountdown(int seconds)
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = null;

        if (seconds == HudViewModel.CountdownHidden)
        {
            countdownRoot.SetActive(false);
            return;
        }

        bool go = seconds == HudViewModel.CountdownGo;
        countdownLabel.text = go ? goText : seconds.ToString();
        countdownRoot.SetActive(true);
        if (countdownPop != null) countdownPop.Play();

        if (go && isActiveAndEnabled) hideRoutine = StartCoroutine(HideAfterGo());
    }

    private IEnumerator HideAfterGo()
    {
        yield return new WaitForSeconds(goHoldSeconds);
        countdownRoot.SetActive(false);
        hideRoutine = null;
    }
}
