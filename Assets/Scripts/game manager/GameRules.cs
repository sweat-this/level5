using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using Level5.Core.Match;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameRules : MonoBehaviour
{
    private const int MaxProgressionSaveAttempts = 3;
    [SerializeField]
    private int gameModeId;

    private bool gameOver;
    bool moneyBallEnabled;

    private const string timerObjectName = "timer";

    /// <summary>The clock object every gameplay scene must provide. Asserted at build time.</summary>
    public static readonly string[] RequiredSceneObjectNames = { timerObjectName };

    private Timer timer;
    private GameStats gameStats1;

    /// <summary>
    /// The score display. GameRules tells it what the match is; it decides how that looks.
    /// Nothing about ending or saving a match depends on it being complete.
    /// </summary>
    private MatchHudPresenter hud;

    //private float timeCompleted;

    // all these specific game rules for each will need to moved to a different file eventually on refactor
    [SerializeField] private GameObject[] basketBallShotMarkerObjects;
    [SerializeField] private List<BasketBallShotMarker> _basketBallShotMarkersList;

    [SerializeField]
    private int markersRemaining;
    [SerializeField]
    private bool positionMarkersRequired;
    public bool PositionMarkersRequired => positionMarkersRequired;

    public static GameRules instance;
    private ProgressionService progressionService;
    private string matchProgressionResultId;

    /// <summary>
    /// The rules this match is being played under. Read once in Start from the validated
    /// configuration instead of copying a dozen mutable globals into private fields, which is what
    /// used to make it ambiguous whether GameOptions or GameRules was the current answer.
    /// </summary>
    private ResolvedMatchRules resolvedRules;

    /// <summary>
    /// The rules this match is played under, resolved on first use.
    ///
    /// Lazy rather than assigned in Start because other components ask GameRules these questions
    /// and Unity does not order Start between components; whoever asks first would otherwise get a
    /// null. Immutable for the life of the match, so resolving early or late gives the same answer.
    /// </summary>
    private ResolvedMatchRules rules => resolvedRules ??= MatchRuntime.Rules;

    /// <summary>The lifecycle owner. GameRules asks it to end the match rather than deciding alone.</summary>
    private MatchController matchController;

    [SerializeField]
    float timePlayedStart;
    [SerializeField]
    float timePlayedEnd;
    [SerializeField]
    int inThePocketActivateValue;

    [SerializeField]
    private GameObject _rakesClone;

    /// <summary>
    /// The player was killed by an instant-kill attack. Presentation only: it changes the
    /// end-of-match display. It used to also trigger a load of the developer test level.
    /// </summary>
    public bool killedOnIdle;
    private bool matchEndHandled;
    private bool matchEndHandling;
    private bool matchScoreSaveCompleted;
    private bool matchAllTimeStatsSaved;
    private bool matchProgressionApplied;
    private bool progressionPersistenceFailed;
    private int progressionSaveAttempts;
    private bool campaignStatsUpdated;
    private bool campaignTransitionStarted;
    private float nextMatchEndRetryTime;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Every gameplay scene needs a lifecycle owner, and none of them has the component yet.
        // Adding it here rather than editing every scene keeps the migration to code; a scene that
        // gains a MatchController of its own is picked up instead of being duplicated.
        matchController = FindAnyObjectByType<MatchController>()
            ?? gameObject.AddComponent<MatchController>();

        // The score display. Created here for the same reason as the controller: no gameplay scene
        // carries the component yet, and this keeps the migration to code.
        hud = GetComponent<MatchHudPresenter>() ?? gameObject.AddComponent<MatchHudPresenter>();

        progressionService = new ProgressionService();
        matchProgressionResultId = MatchSession.EnsureCurrentMatch();
        timePlayedStart = Time.time;
        inThePocketActivateValue = 0;
    }

    private void Start()
    {
        GameOver = false;
        matchEndHandled = false;
        matchEndHandling = false;
        matchScoreSaveCompleted = false;
        matchAllTimeStatsSaved = false;
        matchProgressionApplied = false;
        progressionPersistenceFailed = false;
        progressionSaveAttempts = 0;
        campaignStatsUpdated = false;
        campaignTransitionStarted = false;

        gameModeId = GameModeIds.ToInt(MatchRuntime.ModeId);

        // components
        // player 1 game stats
        gameStats1 = GameLevelManager.instance.Player1.gameStats;
        if (GameLevelManager.instance.PlayerHealth != null)
        {
            GameLevelManager.instance.PlayerHealth.OnDied += OnPlayerDied;
        }
        hud.Initialize();
        timer = SceneObjects.Find<Timer>(timerObjectName, this);

        //updatePlayerScore();

        // rules, from the resolved configuration rather than a dozen separate globals
        // GameRules is the single owner of the match clock; Timer no longer computes it
        setTimer(rules.MatchLengthSeconds);


        // enable/disable necessary shot markers for game mode
        if (rules.RequiresAnyShotMarkers)
        {
            positionMarkersRequired = true;
            SetPositionMarkers();
        }
        if (rules.ObstaclesEnabled)
        {
            Vector3 vector = new Vector3(GameLevelManager.instance.BasketballRimVector.x,
                GameLevelManager.instance.TerrainHeight,
                GameLevelManager.instance.BasketballRimVector.z);
            Instantiate(_rakesClone, vector, Quaternion.identity);
        }
    }



    private void OnDestroy()
    {
        if (GameLevelManager.instance != null && GameLevelManager.instance.PlayerHealth != null)
        {
            GameLevelManager.instance.PlayerHealth.OnDied -= OnPlayerDied;
        }

        // Released so the static cannot outlive the scene. GameRules is scene-scoped, so without
        // this it points at a destroyed object for the whole of the next menu.
        if (instance == this)
        {
            instance = null;
        }
    }

    // ================================================ Update ============================================
    void Update()
    {
        bool matchEnding = gameOver || IsPlayerDead();

        //// update current score
        if (!matchEnding)
        {
            hud.SetMatchContext(gameModeId, gameStats1, rules, inThePocketActivateValue);
            hud.ShowLiveScore();
        }

        // if game over, empty text display
        if (matchEnding)
        {
            hud.ClearForMatchEnd();
        }

        // killedOnIdle used to load the developer test level here, five seconds after the player
        // was hit by an instant-kill projectile. That is a live path in a shipping build -
        // projectile_bullet_instantkill_enemy sets the flag, and level_23_dev is in the build
        // settings - so a real death in a real arena dropped the player into the dev scene.
        //
        // The flag itself is kept: it is what makes the end-of-match display read "You dead bruh"
        // rather than a score summary. It now does only that, and the death runs the same
        // end-of-match path as every other death.

        // game over. pause / display end game / save
        if (!matchEndHandled
            && matchEnding
            && Time.unscaledTime >= nextMatchEndRetryTime)
        {
            HandleMatchEnded();
        }

        //// enable moneyball if game requires moneyball
        //if (GameLevelManager.instance.Controls.Player.action.triggered && GameModeRequiresMoneyBall)
        //{
        //    ToggleMoneyBall();
        //}

        // if not enough money and moneyball required, disabled by default
        if (GameModeRequiresMoneyBall)
        {
            moneyBallEnabled = false;
            //displayMoneyBallText.text = "";
        }
        if (!moneyBallEnabled)
        {
            hud.HideMoneyBall();
        }
    }

    private void OnPlayerDied()
    {
        RequestEnd(MatchEndReason.PlayerDied);
    }

    /// <summary>
    /// The compatibility seam. Everything that used to end a match calls this; it now goes through
    /// the lifecycle owner so repeated requests from the clock, a marker and a death converge on
    /// one transition instead of each setting a flag.
    /// </summary>
    public void RequestGameOver()
    {
        RequestEnd(MatchEndReason.Unknown);
    }

    /// <summary>Ends the match, saying why. Returns true only for the request that ended it.</summary>
    public bool RequestEnd(MatchEndReason reason)
    {
        // GameOver stays the flag the rest of the game polls until those callers migrate; the
        // controller is what decides, and it accepts the transition only once.
        bool accepted = matchController == null || matchController.RequestEnd(reason);
        GameOver = true;
        return accepted;
    }

    private bool IsPlayerDead()
    {
        return GameLevelManager.instance != null
            && GameLevelManager.instance.PlayerHealth != null
            && GameLevelManager.instance.PlayerHealth.IsDead;
    }

    private void HandleMatchEnded()
    {
        if (matchEndHandled || matchEndHandling)
        {
            return;
        }

        matchEndHandling = true;
        RequestEnd(new MatchEndReason(IsPlayerDead() ? MatchEndCause.PlayerDied : MatchEndCause.ObjectiveComplete));

        try
        {
            TryShowMatchEndPresentation();
            setTimePlayed();

            List<PlayerIdentifier> gameStatsList = GetMatchEndStatsList();
            TryUpdateCampaignStats();

            // ******** important : convert basketball stats to high score model
            HighScoreModel user = CreateHighScoreModel(gameStatsList);
            //user = dBHighScoreModel.convertBasketBallStatsToModel(gameStats);

            GameStats primaryGameStats = GetPrimaryGameStats();
            bool persistenceComplete = SaveMatchResults(user, primaryGameStats);
            bool progressionComplete = ApplyMatchProgressionResult(primaryGameStats);
            bool transitionComplete = TryStartCampaignTransition();

            // If this match was one participant's turn in a versus series, hand the numbers over.
            // No-ops for every ordinary match, and joins the same retry loop as the saves above so a
            // turn is never lost to a write that failed once.
            bool versusComplete = VersusMatchReporter.TryReport(
                primaryGameStats,
                MatchRuntime.ModeId,
                timePlayedEnd - timePlayedStart);

            matchEndHandled = persistenceComplete && progressionComplete && transitionComplete && versusComplete;
            if (matchEndHandled)
            {
                // Only now is the match finished. Work that failed and will be retried leaves the
                // controller in Ending, which is exactly what "not done yet" should look like.
                if (matchController != null)
                {
                    matchController.CompleteEnd();
                }
            }
            else
            {
                nextMatchEndRetryTime = Time.unscaledTime + 1f;
            }
        }
        catch (Exception e)
        {
            nextMatchEndRetryTime = Time.unscaledTime + 1f;
            Debug.LogError("GameRules failed while handling match end. It will retry shortly. " + e);
        }
        finally
        {
            matchEndHandling = false;
        }
    }

    private HighScoreModel CreateHighScoreModel(List<PlayerIdentifier> gameStatsList)
    {
        try
        {
            HighScoreModel dBHighScoreModel = new HighScoreModel();
            return dBHighScoreModel.convertBasketBallStatsToModel(gameStatsList);
        }
        catch (Exception e)
        {
            Debug.LogWarning("GameRules could not create a high score model. Match score save will be skipped. " + e);
            return null;
        }
    }

    private void TryShowMatchEndPresentation()
    {
        try
        {
            hud.ClearForMatchEnd();
            if (Pause.instance != null && (Input.touchSupported || SystemInfo.deviceType == DeviceType.Handheld))
            {
                Pause.instance.disableMobileOnlyPauseOptions();
            }
            //pause on game over
            if (Pause.instance != null && !Pause.instance.Paused)
            {
                Pause.instance.TogglePause();
            }

            hud.SetMatchContext(gameModeId, gameStats1, rules, inThePocketActivateValue);
            hud.SetKilledOnIdle(killedOnIdle);
            hud.ShowMatchEndSummary();
        }
        catch (Exception e)
        {
            Debug.LogWarning("GameRules could not update match-end presentation. Durable match-end work will continue. " + e);
        }
    }


    private List<PlayerIdentifier> GetMatchEndStatsList()
    {
        if (GameLevelManager.instance == null)
        {
            return new List<PlayerIdentifier>();
        }

        if (gameModeId == Modes.VersusCpu)
        {
            List<PlayerIdentifier> sortedStats = GameLevelManager.instance.getSortedGameStatsList();
            return sortedStats ?? new List<PlayerIdentifier>();
        }

        return GameLevelManager.instance.players ?? new List<PlayerIdentifier>();
    }

    private void TryUpdateCampaignStats()
    {
        if (campaignStatsUpdated || gameModeId != Modes.BeatThaComputahs)
        {
            return;
        }

        if (PlayerData.instance == null
            || GameLevelManager.instance == null
            || GameLevelManager.instance.players == null
            || GameLevelManager.instance.players.Count == 0)
        {
            Debug.LogWarning("GameRules skipped campaign stats update because campaign player stats were unavailable.");
            campaignStatsUpdated = true;
            return;
        }

        PlayerData.instance.updateCampaignStats(GameLevelManager.instance.players[0].gameStats);
        campaignStatsUpdated = true;
    }

    private bool SaveMatchResults(HighScoreModel user, GameStats primaryGameStats)
    {
        // dont save free play game score
        if (!matchScoreSaveCompleted && gameModeId != Modes.FreePlay && gameModeId != Modes.BeatThaComputahs)
        {
            if (user == null)
            {
                Debug.LogWarning("GameRules skipped match score save because no high score model was available.");
                matchScoreSaveCompleted = true;
            }
            else
            {
                bool savedLocally = DBConnector.instance != null
                    && DBConnector.instance.savePlayerGameStats(user);
                matchScoreSaveCompleted = savedLocally || PendingMatchPersistenceStore.QueueScore(user);

                // only upload when we actually hold a session. gating on GameOptions.userid meant
                // uploading for a user picked from the local list but never authenticated, and for
                // an offline guest fallback - both with no Authorization header on the request.
                try
                {
                    if (savedLocally && APIHelper.HasSession && !string.IsNullOrEmpty(GameOptions.userName))
                    {
                        StartCoroutine(APIHelper.PostHighscore(user));
                    }
                    // if user not logged in, set submitted score to false
                    else if (savedLocally && DBHelper.instance != null)
                    {
                        DBHelper.instance.setGameScoreSubmitted(user.Scoreid, false);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("GameRules saved the match score but could not update submission state. " + e);
                }
            }
        }

        if (gameModeId == Modes.FreePlay || gameModeId == Modes.BeatThaComputahs)
        {
            matchScoreSaveCompleted = true;
        }

        if (primaryGameStats == null)
        {
            Debug.LogWarning("GameRules skipped all-time stats save because no primary player stats were available.");
            matchAllTimeStatsSaved = true;
            return matchScoreSaveCompleted;
        }

        if (!matchAllTimeStatsSaved)
        {
            bool savedLocally = DBConnector.instance != null
                && DBConnector.instance.savePlayerAllTimeStats(primaryGameStats);
            matchAllTimeStatsSaved = savedLocally
                || PendingMatchPersistenceStore.QueueAllTime(matchProgressionResultId, primaryGameStats);
        }

        return matchScoreSaveCompleted && matchAllTimeStatsSaved;
    }

    private GameStats GetPrimaryGameStats()
    {
        if (GameLevelManager.instance != null
            && GameLevelManager.instance.Player1 != null
            && GameLevelManager.instance.Player1.gameStats != null)
        {
            return GameLevelManager.instance.Player1.gameStats;
        }

        return gameStats1;
    }

    private bool ApplyMatchProgressionResult(GameStats primaryGameStats)
    {
        if (matchProgressionApplied)
        {
            return true;
        }

        if (primaryGameStats == null)
        {
            Debug.LogWarning("GameRules skipped progression because no primary player stats were available.");
            matchProgressionApplied = true;
            return true;
        }

        if (progressionService == null)
        {
            progressionService = new ProgressionService();
        }

        MatchProgressionResult appliedResult = progressionService.ApplyMatchResult(
            matchProgressionResultId,
            MatchRuntime.PrimaryCharacterId,
            primaryGameStats.getExperienceGainedFromSession());
        matchProgressionApplied = appliedResult.Applied;
        if (!matchProgressionApplied)
        {
            progressionSaveAttempts++;
            if (progressionSaveAttempts >= MaxProgressionSaveAttempts)
            {
                progressionPersistenceFailed = true;
                hud.SetProgressionPersistenceFailed(true);
                Debug.LogError(MatchHudPresenter.ProgressionPersistenceWarning);
                matchProgressionApplied = true;
            }
        }

        return matchProgressionApplied;
    }

    private bool TryStartCampaignTransition()
    {
        // killedOnIdle used to be excluded here because that death loaded the dev level instead,
        // so the campaign never needed to advance. With that load gone, excluding it would leave a
        // campaign run sitting on the end-of-match screen with no way forward. An instant-kill
        // death now advances the campaign exactly like any other loss.
        if (gameModeId != Modes.BeatThaComputahs || campaignTransitionStarted)
        {
            return true;
        }

        GameObject footer = GameObject.Find("footer");
        if (footer != null)
        {
            footer.SetActive(false);
        }

        StartCoroutine(LoadNextCampaignLevel(5));
        campaignTransitionStarted = true;
        return true;
    }

    private IEnumerator LoadNextCampaignLevel(int seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        List<PlayerIdentifier> players = GameLevelManager.instance.getSortedGameStatsList();
        if (players == null || players.Count < 2)
        {
            Debug.LogError("GameRules cannot advance campaign because fewer than two players were found.");
            SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_start);
            yield break;
        }

        if (GameOptions.levelsList == null || GameOptions.levelsList.Count == 0)
        {
            Debug.LogError("GameRules cannot advance campaign because no campaign levels are loaded.");
            SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_start);
            yield break;
        }

        int completedLevelIndex = Mathf.Clamp(GameOptions.levelSelectedIndex, 0, GameOptions.levelsList.Count - 1);
        EndRoundData.currentLevelIndex = completedLevelIndex;
        EndRoundData.nextLevelIndex = completedLevelIndex;

        // campaign is over
        if (completedLevelIndex < GameOptions.levelsList.Count - 1)
        {
            int nextLevelIndex = completedLevelIndex + 1;
            EndRoundData.nextLevelIndex = nextLevelIndex;
        }

        LevelSelected completedLevel = GameOptions.levelsList[completedLevelIndex];
        EndRoundData.currentRoundCpuWinnerImage = null;
        EndRoundData.currentRoundCpuLoserImage = null;
        CharacterProfile completedCpuProfile = completedLevel.CpuPlayer == null
            ? null
            : completedLevel.CpuPlayer.GetComponent<CharacterProfile>();
        if (completedCpuProfile != null)
        {
            EndRoundData.currentRoundCpuWinnerImage = completedCpuProfile.winPortrait;
            EndRoundData.currentRoundCpuLoserImage = completedCpuProfile.losePortrait;
        }

        EndRoundData.currentRoundWinnerScore = players[0].gameStats.Stats.TotalPoints;
        EndRoundData.currentRoundLoserScore = players[1].gameStats.Stats.TotalPoints;

        EndRoundData.currentRoundWinnerIsCpu = players[0].isCpu;
        EndRoundData.currentRoundLoserIsCpu = players[1].isCpu;
        //Debug.Log("level : " + GameOptions.levelsList[GameOptions.levelSelectedIndex+1].LevelDisplayName + " has 7s : "+ GameOptions.levelsList[GameOptions.levelSelectedIndex+1].LevelHasSevenPointers);
        //Debug.Log(EndRoundData.currentRoundWinnerIsCpu);
        //Debug.Log(EndRoundData.currentRoundLoserIsCpu);

        //string sceneName = GameOptions.levelsList[GameOptions.levelSelectedIndex].LevelObjectName + "_" + GameOptions.levelsList[GameOptions.levelSelectedIndex].LevelDescription;
        SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_end_round_screen);
    }

    public void setTimePlayed()
    {
        // time played end
        timePlayedEnd = Time.time;
        // if player is killed in a game mode that requires a counter
        // if player is killed, high score is being set as time killed
        // must complete game mode to get high score
        GameStats primaryGameStats = GetPrimaryGameStats();
        if (primaryGameStats == null)
        {
            Debug.LogWarning("GameRules could not set time played because no primary player stats were available.");
            return;
        }

        primaryGameStats.TimePlayed = timePlayedEnd - timePlayedStart;
        //if (GameOptions.gameModeRequiresPlayerSurvive
        //    && GameLevelManager.instance.PlayerHealth.IsDead)
        //{
        //    gameStats1.TimePlayed = 0;
        //}
        //else
        //{
        //    gameStats1.TimePlayed = timePlayedEnd - timePlayedStart;
        //}
    }

    //===================================================== toggle money ball ====================================================



    //===================================================== Position markers set up ====================================================
    private void SetPositionMarkers()
    {
        // get all shot position marker objects
        basketBallShotMarkerObjects = GameObject.FindGameObjectsWithTag("shot_marker");

        //load them into list
        foreach (var marker in basketBallShotMarkerObjects)
        {
            BasketBallShotMarker temp = marker.GetComponent<BasketBallShotMarker>();
            // disable the marker rings this mode does not use
            if (!rules.RequiresShotMarkers3s && temp.ShotTypeThree)
            {
                marker.SetActive(false);
            }
            if (!rules.RequiresShotMarkers4s && temp.ShotTypeFour)
            {
                marker.SetActive(false);
            }
            if (!rules.RequiresShotMarkers7s && temp.ShotTypeSeven)
            {
                marker.SetActive(false);
            }
            // add all active and enabled markers to list
            if (temp.isActiveAndEnabled)
            {
                BasketBallShotMarkersList.Add(temp);
                temp.PositionMarkerId = BasketBallShotMarkersList.Count - 1;
            }
        }
        // sort markers list by positionid
        BasketBallShotMarkersList.Sort(SortByMarkerId);
        // number of markers to complete ( all active and enabled sshot markers based on game options
        markersRemaining = BasketBallShotMarkersList.Count;
    }

    static int SortByMarkerId(BasketBallShotMarker p1, BasketBallShotMarker p2)
    {
        return p1.PositionMarkerId.CompareTo(p2.PositionMarkerId);
    }


    // ================================================ get stats total ============================================


    /// <summary>
    /// Whether the contest is complete. Named for what it is asked, not for what it decides - the
    /// decision itself is <see cref="MatchEndConditions.MarkersCleared"/>.
    /// </summary>
    public bool IsGameOver()
    {
        if (MatchEndConditions.MarkersCleared(MarkersRemaining))
        {
            ////set counter timer
            //float bonusTime = Timer.instance.Seconds;
            //Debug.Log("Timer.instance.Seconds : " + Timer.instance.Seconds);
            //if (gameModeThreePointContest || gameModeFourPointContest || gameModeAllPointContest)
            //{
            //    //// add remaining counter time FLOOR to total points  as bonus points
            //    GameLevelManager.instance.Player1.gameStats.BonusPoints = (int)(Mathf.Floor(bonusTime/2));
            //    Debug.Log("bonusTime : "+bonusTime);
            //    Debug.Log("(int)(Mathf.Floor(bonusTime) : " + (int)(Mathf.Floor(bonusTime/2)));
            //    // add bonus points
            //    GameLevelManager.instance.Player1.gameStats.TotalPoints += GameLevelManager.instance.players[0].gameStats.BonusPoints;
            //}
            //// if game has a time counter
            //if (modeRequiresCounter)
            //{
            //    // set timer score
            //    SetRequiresCounterLowScore();
            //}
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool GameModeRequiresMoneyBall => rules.RequiresMoneyBall;

    public bool MoneyBallEnabled
    {
        get => moneyBallEnabled;
        set => moneyBallEnabled = value;
    }

    private void setTimer(float seconds)
    {
        if (timer == null)
        {
            return;
        }

        timer.TimeStart = seconds;
    }

    public int GameModeId
    {
        get => gameModeId;
        set => gameModeId = value;
    }

    // Gates pause/cancel (Pause.cs) once a match ends. Reset false in Start() for every fresh
    // GameRules instance; set true only through RequestEnd(), which HandleMatchEnded() guards
    // against re-entry - see AUD-019. It does not need to reset to false again within a match.
    public bool GameOver
    {
        get => gameOver;
        set
        {
            gameOver = value;
            if (GameLevelManager.instance != null)
            {
                GameLevelManager.instance.GameOver = value;
            }
        }
    }

    public List<BasketBallShotMarker> BasketBallShotMarkersList
    {
        get => _basketBallShotMarkersList;
        set => _basketBallShotMarkersList = value;
    }
    public int MarkersRemaining
    {
        get => markersRemaining;
        set => markersRemaining = value;
    }
    // Views onto the resolved rules, kept because callers still ask GameRules these questions.
    public bool GameModeRequiresConsecutiveShots => rules.RequiresConsecutiveShots;
    public bool GameModeThreePointContest => rules.IsThreePointContest;
    public bool GameModeFourPointContest => rules.IsFourPointContest;
    public bool GameModeAllPointContest => rules.IsAllPointContest;
    public int InThePocketActivateValue { get => inThePocketActivateValue; set => inThePocketActivateValue = value; }
    public bool GameModeSevenPointContest => rules.IsSevenPointContest;
}
