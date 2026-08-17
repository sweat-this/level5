using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Level5.Core.Match;
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

    private bool initialized;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        // AUD-102: OnDisable unregisters every onClick but this used to not register them again,
        // so disabling and re-enabling this component left every button on the screen inert.
        if (initialized)
        {
            RegisterMenuButtonCallbacks();
        }
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
        initialized = true;
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

        UiSelectionAdapter.RegisterButton(uiObjects.nextRoundButton, pressNext);
        UiSelectionAdapter.RegisterButton(uiObjects.startMenuButton, pressStartMenu);
        UiSelectionAdapter.RegisterButton(uiObjects.QuitMenuButton, pressQuit);
    }

    private void UnregisterMenuButtonCallbacks()
    {
        EndRoundUIObjects uiObjects = EndRoundUIObjects.instance;
        if (uiObjects == null)
        {
            return;
        }

        UiSelectionAdapter.UnregisterButton(uiObjects.nextRoundButton, pressNext);
        UiSelectionAdapter.UnregisterButton(uiObjects.startMenuButton, pressStartMenu);
        UiSelectionAdapter.UnregisterButton(uiObjects.QuitMenuButton, pressQuit);
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

        // only upload when we actually hold a session - see the same gate in GameRules
        if (savedLocally && APIHelper.HasSession && !string.IsNullOrEmpty(GameOptions.userName))
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

    /// <summary>
    /// Points the match at the next campaign arena.
    ///
    /// This used to only write the level globals, which was enough when everything read them. It is
    /// not any more: gameplay reads the active configuration, so a round advance that changed only
    /// the globals would have started round two in round one's arena. The configuration moves too,
    /// and the bridge pushes the same values out for whatever still reads them.
    /// </summary>
    private void ApplyLevelSelection(int levelIndex)
    {
        LevelSelected level = levelsList[levelIndex];
        GameOptions.levelSelectedIndex = levelIndex;

        MatchConfiguration nextRound = ActiveMatch.ContinueInLevel(LevelDefinitionFactory.Create(level));
        if (nextRound != null)
        {
            ActiveMatch.Begin(nextRound);
            LegacyGameOptionsBridge.Apply(nextRound);
            return;
        }

        // No configuration to continue - the campaign was entered without one. Fall back to the
        // globals so the round still loads, the way it always did.
        Debug.LogWarning("Campaign round advanced without an active match configuration; using legacy fields.");
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
