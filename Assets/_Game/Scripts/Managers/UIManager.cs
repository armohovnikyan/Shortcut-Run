using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    //P stands for Panel
    [Header("PanelAndMenus")]
    [SerializeField] GameObject P_MainMenu;
    [SerializeField] GameObject P_GamePlay;
    [SerializeField] GameObject P_WonCoins;
    [SerializeField] GameObject P_Settings;
    [SerializeField] GameObject P_ExitGame;
    [Header("Game Flow Panels")]
    [SerializeField] GameObject P_GameStart;
    [SerializeField] GameObject P_PlayerPlace;
    [SerializeField] GameObject P_Countdown;
    [SerializeField] GameObject P_Victory;
    [SerializeField] GameObject P_GameOver;
    [SerializeField] GameObject P_FirstPlace;

    //[SerializeField] GameObject P_Game;
    //B stands for Button
    [Space]
    [Header("Buttons")]
    [SerializeField] Button B_Play;
    [SerializeField] Button B_Retry;
    [SerializeField] Button B_Deny;
    [SerializeField] Button B_Confirm;
    [SerializeField] Button B_OpenSettings;
    [SerializeField] Button B_CloseSettings;
    [SerializeField] Button B_OpenBackToMenu;
    [SerializeField] Button B_CloseBackToMenu;
    [SerializeField] Button B_BoardLevelUp;
    [SerializeField] Button B_OfflineCoinsEarnLevelUp;
    [SerializeField] Button B_GetCoinsAfterFinishing;
    [SerializeField] Button B_GetWonCoins;
    [Space]
    [Header("TMP")]
    [SerializeField] TextMeshProUGUI TMP_Coins_MainMenu;
    [SerializeField] TextMeshProUGUI TMP_Coins_GamePlay;
    [SerializeField] TextMeshProUGUI TMP_Coins_WonCoinsMenu;
    [SerializeField] TextMeshProUGUI TMP_Price_Board;
    [SerializeField] TextMeshProUGUI TMP_Price_OfflineEarn;
    [SerializeField] TextMeshProUGUI TMP_Level_Board;
    [SerializeField] TextMeshProUGUI TMP_Level_OfflineEarn;
    [SerializeField] TextMeshProUGUI TMP_PlayerPlace;
    [SerializeField] TextMeshProUGUI TMP_PlaceAbbreviation;
    [SerializeField] TextMeshProUGUI TMP_Countdown;
    [SerializeField] TextMeshProUGUI TMP_EarnedCoins;
    [SerializeField] TextMeshProUGUI TMP_WonCoins;
    //[SerializeField] TextMeshProUGUI TMP_CollectedBoards;

    [SerializeField] private float duration = 0.65f;
    [SerializeField] AnimationCurve curveNumber;
    [SerializeField] AnimationCurve curveGoText;
    float collectedCoins, earnedCoins;
    

    Coroutine countdownRoutine;
    UIAnimation countdownAnimation;
    UIAnimation hintPanelAnimation;

    public event Action OnCountdownFinished;
    public event Action OnDeath;
    public void RaiseOnDeath() => OnDeath?.Invoke();
    #region Singleton
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UIManager>();
                if (_instance == null)
                {
                    Debug.LogError("UIManager not found in the scene!");
                }
            }
            return _instance;
        }
    }
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    #endregion


    private void Start()
    {
        countdownAnimation = new UIAnimation(TMP_Countdown, duration, curveNumber, curveGoText);
        //hintPanelAnimation = new UIAnimation();
        P_MainMenu?.SetActive(false);
        P_GamePlay?.SetActive(false);

        P_WonCoins?.SetActive(false);
        P_Settings?.SetActive(false);
        P_ExitGame?.SetActive(false);
        
        P_Victory?.SetActive(false);
        P_GameOver?.SetActive(false);
        P_PlayerPlace?.SetActive(false);
        Initialize();
    }

    void Initialize()
    {
        ResetListeners();
        SetListeners();
        //if ()//is won coins time
        //{
        //show the won coins menu
        //}
        //else
        P_MainMenu.SetActive(true);
        OnDeath -= AfterDeath;
        OnDeath += AfterDeath;
    }

    public void SetPlayerPlace(int place)
    {
        TMP_PlayerPlace.text = place.ToString();
        switch (place)
        {
            case 1: TMP_PlaceAbbreviation.text = "st"; break;
            case 2: TMP_PlaceAbbreviation.text = "nd"; break;
            case 3: TMP_PlaceAbbreviation.text = "rd"; break;
            default: TMP_PlaceAbbreviation.text = "th"; break;
        }
    }
    void ResetListeners()
    {
        B_OpenSettings.onClick.RemoveAllListeners();
        B_CloseSettings.onClick.RemoveAllListeners();
        B_OpenBackToMenu.onClick.RemoveAllListeners();
        B_CloseBackToMenu.onClick.RemoveAllListeners();

        B_Play.onClick.RemoveAllListeners();

        B_Retry.onClick.RemoveAllListeners();
        B_Deny.onClick.RemoveAllListeners();
        B_Confirm.onClick.RemoveAllListeners();

        B_BoardLevelUp.onClick.RemoveAllListeners();
        B_OfflineCoinsEarnLevelUp.onClick.RemoveAllListeners();

        B_GetCoinsAfterFinishing.onClick.RemoveAllListeners();
        B_GetWonCoins.onClick.RemoveAllListeners();
    }
    void SetListeners()
    {
        B_OpenSettings.onClick.AddListener(OnSettings);
        B_CloseSettings.onClick.AddListener(OnCloseSettings);
        B_OpenBackToMenu.onClick.AddListener(OnGameExit);
        B_CloseBackToMenu.onClick.AddListener(OnCloseExit);

        B_Play.onClick.AddListener(OnPlay);

        B_Retry.onClick.AddListener(OnRetry);
        B_Deny.onClick.AddListener(OnCloseExit);
        B_Confirm.onClick.AddListener(ExitGame);

        B_BoardLevelUp.onClick.AddListener(OnBoardLevelUp);
        B_OfflineCoinsEarnLevelUp.onClick.AddListener(OnOfflineEarnLevelUp);

        B_GetCoinsAfterFinishing.onClick.AddListener(OnGetRewardedCoins);
        B_GetWonCoins.onClick.AddListener(OnGetRewardedCoins);
    }
    public void StartCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }
        countdownRoutine = StartCoroutine(CountdownCoroutine());
    }
    private void Update()
    {
        TrackPlace();
    }
    void TrackPlace()
    {
        Transform player = GameManager.Instance.GetPlayerTransform();
        int place = GameManager.Instance.GetMyPlace(player);
        SetPlayerPlace(place);
    }
    void OnPlay()
    {
        GameManager.Instance.RaiseOnStart();
        P_MainMenu?.SetActive(false);
        P_GamePlay?.SetActive(true);
        StartCountdown();
    }
    void AfterDeath()
    {
        P_GameStart?.SetActive(false);
        P_GameOver?.SetActive(true);
    }
    void OnRetry()
    {
        Debug.Log("Opened panel");
        GameManager.Instance.RaiseOnRestart();
        P_GameOver?.SetActive(false);
        P_GamePlay?.SetActive(false);
        P_MainMenu?.SetActive(true);
    }
    void OnLevelCompleteAtFirstPlace()
    {
        P_Countdown?.SetActive(false);
        P_PlayerPlace?.SetActive(false);
        P_Victory?.SetActive(true);
        //get coins from economy manager
    }
    void OnLevelCompleteAtLowPlaces()
    {
        P_Countdown?.SetActive(false);
        P_FirstPlace?.SetActive(false);
        P_Victory?.SetActive(true);
        //get coins from economy manager
    }
    void OnCoinsGet()
    {
        //get coins and set in the economy manager
        GameManager.Instance.RaiseOnRestart();
        //close panels
    }
    void ExitGame()
    {
        OnCloseExit();
        P_GamePlay?.SetActive(false);
        P_MainMenu.SetActive(true);
    }
    void OnGameExit()
    {
        //pause time
        GameManager.Instance.RaiseOnPause();
        P_ExitGame?.SetActive(true);
        GameManager.Instance.GameFlow = GameFlow.Pause;
    }
    void OnSettings()
    {
        GameManager.Instance.RaiseOnPause();
        P_Settings?.SetActive(true);
    }
    void OnCloseSettings()
    {
        P_Settings?.SetActive(false);
    }
    void OnCloseExit()
    {
        //restart time 
        P_ExitGame?.SetActive(false);
        GameManager.Instance.GameFlow = GameFlow.Playing;

    }
    void OnGetRewardedCoins()
    {
        //set rewarded coins.
        //P_GamePlay?.SetActive(false);
        //P_MainMenu?.SetActive(true);
        //get coins and set in the economy manager
        GameManager.Instance.RaiseOnRestart();
        //close panels
    }
    void OnGetWonCoins()
    {
        //set rewarded coins
        //P_WonCoins?.SetActive(false);
        //P_MainMenu?.SetActive(true);
        //get coins and set in the economy manager
        GameManager.Instance.RaiseOnRestart();
        //close panels
    }
    void OnBoardLevelUp()
    {
        //if money not enough deactivate button
        //if enough > level up
    }
    void OnOfflineEarnLevelUp()
    {
        //if money not enuf deactivate button
        //if enough > level up
    }
    void OnChangeName()
    {
        // save changed name
    }

    IEnumerator CountdownCoroutine()
    {
        P_Countdown?.SetActive(true);
        for (int i = 3; i > 0; i--)
        {
            yield return countdownAnimation.PlayNumber(i);
            //yield return new WaitForSeconds(interval);
        }

        yield return countdownAnimation.PlayGo();
        P_Countdown?.SetActive(false);
        P_PlayerPlace?.SetActive(true);
        OnCountdownFinished();
    }
}

