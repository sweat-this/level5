using Assets.Scripts.database;
using Assets.Scripts.restapi;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndRoundMenuManager : MonoBehaviour
{
    [SerializeField]
    private string currentHighlightedButton;
    [SerializeField]
    private string previousHighlightedButton;

    int currentWinnerScore;
    int currentLoserScore;
    bool currentWinnerisCpu;
    bool currentLoserisCpu;
    bool tieGame = false;
    string nextLevelName;
    int completedLevelIndex;
    int targetLevelIndex;
    List<LevelSelected> levelsList = new List<LevelSelected>();
    bool isGameSaved = false;
    bool campaignEnded;
    CampaignNextAction nextAction;

    public static EndRoundMenuManager instance;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
    }
    private void OnDisable()
    {
        UnregisterMenuButtonCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Awake()
    {
        instance = this;
    }
    void Start()
    {
        levelsList = GameOptions.levelsList ?? new List<LevelSelected>();
        if (levelsList.Count == 0 || PlayerData.instance == null || PlayerData.instance.CampaignGameStats == null)
        {
            Debug.LogError("EndRoundMenuManager cannot display campaign results because campaign state is unavailable.");
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            return;
        }

        currentWinnerScore = EndRoundData.currentRoundWinnerScore;
        currentLoserScore = EndRoundData.currentRoundLoserScore;        
        currentWinnerisCpu = EndRoundData.currentRoundWinnerIsCpu;
        currentLoserisCpu = EndRoundData.currentRoundLoserIsCpu;
        tieGame = currentWinnerScore == currentLoserScore;
        completedLevelIndex = Mathf.Clamp(EndRoundData.currentLevelIndex, 0, levelsList.Count - 1);
        bool completedFinalLevel = completedLevelIndex == levelsList.Count - 1;
        nextAction = CampaignRoundDecision.Decide(
            completedFinalLevel,
            currentWinnerisCpu,
            tieGame,
            EndRoundData.numberOfContinues);
        campaignEnded = nextAction == CampaignNextAction.Complete || nextAction == CampaignNextAction.EndRun;
        
        PlayerData.instance.CampaignGameStats.campaignGamesPlayed++;
        if (tieGame)
        {
            PlayerData.instance.CampaignGameStats.campaignTies++;
        }
        else if (currentWinnerisCpu)
        {
            PlayerData.instance.CampaignGameStats.campaignLosses++;
        }
        else
        {
            PlayerData.instance.CampaignGameStats.campaignWins++;
        }

        LoadData();
        ConfigureOutcomeUi();
        if (campaignEnded)
        {
            saveGame();
        }
        UiSelectionAdapter.EnsureInputSystemUiModule();
        RegisterMenuButtonCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    private void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject != null)
        {
            currentHighlightedButton = selectedObject.name;
        }

        // save at end of frame
        previousHighlightedButton = currentHighlightedButton;
    }

    private void RegisterMenuButtonCallbacks()
    {
        EndRoundUIObjects uiObjects = EndRoundUIObjects.instance;
        if (uiObjects == null)
        {
            return;
        }

        RegisterButton(uiObjects.nextRoundButton, pressNext);
        RegisterButton(uiObjects.startMenuButton, pressStartMenu);
        RegisterButton(uiObjects.QuitMenuButton, pressQuit);
    }

    private void UnregisterMenuButtonCallbacks()
    {
        EndRoundUIObjects uiObjects = EndRoundUIObjects.instance;
        if (uiObjects == null)
        {
            return;
        }

        UnregisterButton(uiObjects.nextRoundButton, pressNext);
        UnregisterButton(uiObjects.startMenuButton, pressStartMenu);
        UnregisterButton(uiObjects.QuitMenuButton, pressQuit);
    }

    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        if (!UiSelectionAdapter.HasPersistentListeners(button))
        {
            button.onClick.AddListener(action);
        }
    }

    private void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private GameObject GetDefaultSelectedButton()
    {
        EndRoundUIObjects uiObjects = EndRoundUIObjects.instance;
        if (uiObjects == null)
        {
            return null;
        }

        if (campaignEnded)
        {
            return uiObjects.startMenuButton != null ? uiObjects.startMenuButton.gameObject : null;
        }

        return uiObjects.nextRoundButton != null ? uiObjects.nextRoundButton.gameObject : null;
    }

    public void saveGame()
    {
        if (isGameSaved || PlayerData.instance == null || PlayerData.instance.CampaignGameStats == null)
        {
            return;
        }

        HighScoreModel dBHighScoreModel = new();
        HighScoreModel user = dBHighScoreModel.convertCampaignBasketBallStatsToModel(PlayerData.instance.CampaignGameStats);
        bool savedLocally = DBConnector.instance != null && DBConnector.instance.savePlayerGameStats(user);
        isGameSaved = savedLocally || PendingMatchPersistenceStore.QueueScore(user);
        if (!isGameSaved)
        {
            Debug.LogError("Campaign results could not be saved or queued.");
            return;
        }

        if (savedLocally && !string.IsNullOrEmpty(GameOptions.userName) && GameOptions.userid != 0)
        {
            StartCoroutine(APIHelper.PostHighscore(user));
        }
        else if (savedLocally && DBHelper.instance != null)
        {
            DBHelper.instance.setGameScoreSubmitted(user.Scoreid, false);
        }

        Destroy(PlayerData.instance.GetComponent<GameStats>());
        PlayerData.instance.CampaignGameStats = PlayerData.instance.gameObject.AddComponent<GameStats>();
    }

     void LoadData()
     {
        EndRoundData.levelsList = levelsList;
        LevelSelected completedLevel = levelsList[completedLevelIndex];
        // cpu wins
        if (currentWinnerisCpu)
        {
            EndRoundUIObjects.instance.currentRoundWinnerImage.sprite = completedLevel.CpuPlayerWinImage;
            EndRoundUIObjects.instance.currentRoundLoserImage.sprite = EndRoundData.currentRoundPlayerLoserImage;
            EndRoundUIObjects.instance.currentRoundWinnerIsCpu.text = "CPU";
            EndRoundUIObjects.instance.currentRoundLoserIsCpu.text = "Player 1";
        }
        // player wins or tie game
        if (!currentWinnerisCpu || tieGame)
        {
            EndRoundUIObjects.instance.currentRoundWinnerImage.sprite = EndRoundData.currentRoundPlayerWinnerImage;
            EndRoundUIObjects.instance.currentRoundLoserImage.sprite = completedLevel.CpuPlayerLoseImage;
            EndRoundUIObjects.instance.currentRoundWinnerIsCpu.text = "Player 1";
            EndRoundUIObjects.instance.currentRoundLoserIsCpu.text = "CPU";
        }
        if (tieGame)
        {
            EndRoundUIObjects.instance.winnerText.text = "tie";
            EndRoundUIObjects.instance.loserText.text = "tie";
        }

        targetLevelIndex = nextAction == CampaignNextAction.Advance
            ? Mathf.Clamp(EndRoundData.nextLevelIndex, 0, levelsList.Count - 1)
            : completedLevelIndex;

        if (!campaignEnded)
        {
            LevelSelected targetLevel = levelsList[targetLevelIndex];
            CharacterProfile targetCpu = targetLevel.CpuPlayer != null
                ? targetLevel.CpuPlayer.GetComponent<CharacterProfile>()
                : null;
            EndRoundUIObjects.instance.nextRoundText.text = nextAction == CampaignNextAction.Advance
                ? "Start"
                : tieGame ? "Tie Game" : "Try Again";
            EndRoundUIObjects.instance.nextRoundLevel.text = targetLevel.LevelDisplayName;
            EndRoundUIObjects.instance.nextRoundOpponent.text = targetCpu != null
                ? targetCpu.PlayerDisplayName
                : string.Empty;
            nextLevelName = targetLevel.LevelObjectName + "_" + targetLevel.LevelDescription;
        }

        EndRoundUIObjects.instance.continueNumber.text = EndRoundData.numberOfContinues.ToString();
        EndRoundUIObjects.instance.currentRoundWinnerScore.text = EndRoundData.currentRoundWinnerScore.ToString();
        EndRoundUIObjects.instance.currentRoundLoserScore.text = EndRoundData.currentRoundLoserScore.ToString();

     }

    private void ConfigureOutcomeUi()
    {
        EndRoundUIObjects ui = EndRoundUIObjects.instance;
        ui.nextInfoObject.SetActive(!campaignEnded);
        ui.endMessageObject.SetActive(campaignEnded);
        if (!campaignEnded)
        {
            return;
        }

        ui.endMessageText.text = nextAction == CampaignNextAction.Complete
            ? "You beat all the Computahs. Sick."
            : "You suck. go sit on the tire.";
        UiSelectionAdapter.TrySelect(ui.startMenuButton.gameObject);
    }

    public void pressNext()
    {
        if (campaignEnded)
        {
            if (!isGameSaved) { saveGame(); }
            if (!isGameSaved)
            {
                return;
            }
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            return;
        }

        if (nextAction == CampaignNextAction.Retry && currentWinnerisCpu && !tieGame)
        {
            EndRoundData.numberOfContinues--;
        }

        ApplyLevelSelection(targetLevelIndex);
        MatchSession.BeginNewMatch();
        SceneManager.LoadScene(nextLevelName);
    }

    private void ApplyLevelSelection(int levelIndex)
    {
        LevelSelected level = levelsList[levelIndex];
        GameOptions.levelSelectedIndex = levelIndex;
        GameOptions.levelHasSevenPointers = level.LevelHasSevenPointers;
        GameOptions.levelId = level.LevelId;
        GameOptions.levelSelected = level.LevelObjectName;
        GameOptions.levelDisplayName = level.LevelDisplayName;
    }

    public void pressStartMenu()
    {
        if(!isGameSaved) { saveGame(); }
        if (!isGameSaved)
        {
            return;
        }
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
    }
    public void pressQuit()
    {
        if (!isGameSaved) { saveGame(); }
        if (!isGameSaved)
        {
            return;
        }
        Application.Quit();
    }
}
