using System.Collections;
using TMPro;
using Unity.VisualScripting;
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
    [SerializeField] GameObject P_Victory;
    [SerializeField] GameObject P_GameOver;
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
    [SerializeField] TextMeshProUGUI TMP_EarnedCoins;
    [SerializeField] TextMeshProUGUI TMP_WonCoins;
    [SerializeField] TextMeshProUGUI TMP_CollectedBoards;

    float collectedCoins, earnedCoins;
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
                    Debug.LogError("AuidoManager not found in the scene!");
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
        P_MainMenu?.SetActive(false);
        P_GamePlay?.SetActive(false);
        P_WonCoins?.SetActive(false);
        P_ExitGame?.SetActive(false);
        P_Victory?.SetActive(false);
        P_GameOver?.SetActive(false);
        P_Settings?.SetActive(false);
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

    }

    public void SetPlayersPlace(int place)
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
    void OnPlay()
    {
        GameManager.Instance.RaiseOnStart();
        P_MainMenu?.SetActive(false);
        P_GamePlay?.SetActive(true);

    }

    void InPlay()
    {
        //coroutine
    }
    void OnRestart()
    {

    }
    void OnRetry()
    {
        P_GamePlay?.SetActive(false);
        P_MainMenu?.SetActive(true);
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
        P_GamePlay?.SetActive(false);
        P_MainMenu?.SetActive(true);

    }
    void OnGetWonCoins()
    {
        //set rewarded coins
        P_WonCoins?.SetActive(false);
        P_MainMenu?.SetActive(true);

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

    IEnumerator OnStart()
    {
        yield break;
        //start countdown 
        //start hint panel 
    }
}

