using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The in-run HUD: coins, home button, the 3-2-1-GO countdown, the player's live place and the buff buttons.</summary>
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

    [Header("Buffs")]
    [Tooltip("Optional while the buff system is switched off (GameManager > Buffs Enabled).")]
    [SerializeField] private BuffUseButtonView runBoost;
    [SerializeField] private BuffUseButtonView boardMagnet;

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
        BindBuff(runBoost, viewModel.RunBoost, viewModel.BuffsEnabled);
        BindBuff(boardMagnet, viewModel.BoardMagnet, viewModel.BuffsEnabled);
    }

    // Buffs switched off: the button (if there is one) is hidden and never bound.
    private static void BindBuff(BuffUseButtonView button, BuffViewModel buff, bool enabled)
    {
        if (button == null) return;
        button.gameObject.SetActive(enabled);
        if (enabled) button.Bind(buff);
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
