using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameRules : MonoBehaviour
{
    [SerializeField]
    private int gameModeId;

    private bool gameOver;
    [SerializeField]
    private bool gameRulesEnabled;
    private bool modeRequiresCounter;
    private bool modeRequiresCountDown;

    bool gameModeRequiresShotMarkers3s;
    bool gameModeRequiresShotMarkers4s;
    bool gameModeRequiresShotMarkers7s;

    [SerializeField]
    bool gameModeThreePointContest;
    [SerializeField]
    bool gameModeFourPointContest;
    [SerializeField]
    private bool gameModeSevenPointContest;
    bool gameModeAllPointContest;
    [SerializeField]
    float customTimer;

    bool gameModeRequiresMoneyBall;
    bool moneyBallEnabled;
    bool gameModeRequiresConsecutiveShots;

    private Timer timer;
    private GameStats gameStats1;

    // object name that displays score
    private const string displayScoreObjectName = "display_score";
    private const string displayCurrentScoreObjectName = "display_current_score";
    private const string displayHighScoreObjectName = "display_high_score";
    private const string displayMoneyObjectName = "money_display";
    private const string displayMoneyBallObjectName = "money_ball_enabled";
    private const string displayOtherMessageName = "other_message";

    // text objects
    private Text displayScoreText;
    [SerializeField]
    private Text displayCurrentScoreText;
    [SerializeField]
    private Text displayHighScoreText;
    private Text displayMoneyText;
    private Text displayMoneyBallText;
    private Text displayOtherMessageText;
    [SerializeField]
    private Text displayP1ScoreText;
    [SerializeField]
    private Text displayP2ScoreText;
    [SerializeField]
    private Text displayP3ScoreText;
    [SerializeField]
    private Text displayP4ScoreText;

    public string player1DisplayName;
    public string player2DisplayName;
    public string player3DisplayName;
    public string player4DisplayName;

    //private float timeCompleted;

    // all these specific game rules for each will need to moved to a different file eventually on refactor
    [SerializeField] private GameObject[] basketBallShotMarkerObjects;
    [SerializeField] private List<BasketBallShotMarker> _basketBallShotMarkersList;

    [SerializeField]
    private int markersRemaining;
    [SerializeField]
    private bool positionMarkersRequired;
    public bool PositionMarkersRequired => positionMarkersRequired;

    private float counterTime; // this is set when shot is made that ends game : class BasketBallShotMade (attached to rim)

    public static GameRules instance;

    [SerializeField]
    float timePlayedStart;
    [SerializeField]
    float timePlayedEnd;
    [SerializeField]
    int inThePocketActivateValue;

    [SerializeField]
    private GameObject _rakesClone;

    public bool killedOnIdle;
    private bool killedOnIdleTransitionStarted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        timePlayedStart = Time.time;
        inThePocketActivateValue = 0;
    }

    private void Start()
    {
        GameOver = false;
        gameModeId = GameOptions.gameModeSelectedId;

        // components
        // player 1 game stats
        gameStats1 = GameLevelManager.instance.Player1.gameStats;
        displayScoreText = GameObject.Find(displayScoreObjectName).GetComponent<Text>();
        displayCurrentScoreText = GameObject.Find(displayCurrentScoreObjectName).GetComponent<Text>();
        displayHighScoreText = GameObject.Find(displayHighScoreObjectName).GetComponent<Text>();
        displayMoneyText = GameObject.Find(displayMoneyObjectName).GetComponent<Text>();
        displayMoneyBallText = GameObject.Find(displayMoneyBallObjectName).GetComponent<Text>();
        displayOtherMessageText = GameObject.Find(displayOtherMessageName).GetComponent<Text>();
        timer = GameObject.Find("timer").GetComponent<Timer>();

        player1DisplayName = GameLevelManager.instance.Player1 != null ? GameLevelManager.instance.players[0].characterProfile.PlayerDisplayName : "player1";
        player2DisplayName = GameLevelManager.instance.Player2 != null ? GameLevelManager.instance.players[1].characterProfile.PlayerDisplayName : "player2";
        player3DisplayName = GameLevelManager.instance.Player3 != null ? GameLevelManager.instance.players[2].characterProfile.PlayerDisplayName : "player3";
        player4DisplayName = GameLevelManager.instance.Player4 != null ? GameLevelManager.instance.players[3].characterProfile.PlayerDisplayName : "player4";

        //updatePlayerScore();

        // rules flags
        modeRequiresCounter = GameOptions.gameModeRequiresCounter;
        modeRequiresCountDown = GameOptions.gameModeRequiresCountDown;

        gameModeRequiresShotMarkers3s = GameOptions.gameModeRequiresShotMarkers3s;
        gameModeRequiresShotMarkers4s = GameOptions.gameModeRequiresShotMarkers4s;
        gameModeRequiresShotMarkers7s = GameOptions.gameModeRequiresShotMarkers7s;
        gameModeRequiresMoneyBall = GameOptions.gameModeRequiresMoneyBall;

        gameModeThreePointContest = GameOptions.gameModeThreePointContest;
        gameModeFourPointContest = GameOptions.gameModeFourPointContest;
        gameModeSevenPointContest = GameOptions.gameModeSevenPointContest;
        gameModeAllPointContest = GameOptions.gameModeAllPointContest;
        // custom timer
        if (GameOptions.customTimer > 0)
        {
            setTimer(GameOptions.customTimer);
        }
        else
        {
            setTimer(180);
        }

        GameModeRequiresConsecutiveShots = GameOptions.gameModeRequiresConsecutiveShot;

        // init text
        displayScoreText.text = "";
        displayCurrentScoreText.text = "";
        displayHighScoreText.text = "";
        displayMoneyText.text = "";
        displayMoneyBallText.text = "";
        displayOtherMessageText.text = "";
        displayP1ScoreText.text = "";
        displayP2ScoreText.text = "";
        displayP3ScoreText.text = "";
        displayP4ScoreText.text = "";

        // init markers
        gameRulesEnabled = true;

        // enable/disable necessary shot markers for game mode
        if (gameModeRequiresShotMarkers3s || gameModeRequiresShotMarkers4s || gameModeRequiresShotMarkers7s)
        {
            positionMarkersRequired = true;
            SetPositionMarkers();
        }
        if (GameOptions.obstaclesEnabled)
        {
            Vector3 vector = new Vector3(GameLevelManager.instance.BasketballRimVector.x,
                GameLevelManager.instance.TerrainHeight,
                GameLevelManager.instance.BasketballRimVector.z);
            Instantiate(_rakesClone, vector, Quaternion.identity);
        }
    }

    // ================================================ Update ============================================
    void Update()
    {
        //// update current score
        if (gameRulesEnabled && GameOptions.numPlayers >= 1)
        {
            SetScoreDisplayText();
        }

        // if game over, empty text display
        if (gameOver && gameRulesEnabled)
        {
            displayCurrentScoreText.text = "";
            displayHighScoreText.text = "";
            displayMoneyText.text = "";
            displayMoneyBallText.text = "";
            displayOtherMessageText.text = "";
        }

        if (killedOnIdle && !killedOnIdleTransitionStarted)
        {
            killedOnIdleTransitionStarted = true;
            //Load dev after 5 seconds
            StartCoroutine(LoadGame.LoadDevLevelVersus(5));
        }

        // game over. pause / display end game / save
        if ((gameOver || GameLevelManager.instance.PlayerHealth.IsDead) && !Pause.instance.Paused && gameRulesEnabled)
        {
            displayCurrentScoreText.text = "";
            displayHighScoreText.text = "";
            displayMoneyText.text = "";
            displayMoneyBallText.text = "";
            displayOtherMessageText.text = "";

            // set end time for time played, store in basketballstats.timeplayed
            setTimePlayed();
            if (Input.touchSupported || SystemInfo.deviceType == DeviceType.Handheld)
            {
                Pause.instance.disableMobileOnlyPauseOptions();
            }
            //pause on game over
            Pause.instance.TogglePause();
            displayScoreText.text = GetDisplayText(GameModeId);

            List<PlayerIdentifier> gameStatsList = new();
            if (GameOptions.gameModeSelectedId == 26)
            {
                PlayerData.instance.updateCampaignStats(GameLevelManager.instance.players[0].gameStats);
            }
            if (GameOptions.gameModeSelectedId == 23)
            {
                gameStatsList = GameLevelManager.instance.getSortedGameStatsList();
            }
            else
            {
                gameStatsList = GameLevelManager.instance.players;
            }


            // ******** important : convert basketball stats to high score model
            HighScoreModel dBHighScoreModel = new();
            HighScoreModel user = dBHighScoreModel.convertBasketBallStatsToModel(gameStatsList);
            //user = dBHighScoreModel.convertBasketBallStatsToModel(gameStats);
            //save if at leat 1 minte played
            if (GameObject.FindGameObjectWithTag("database") != null)//&& basketBallStats.TimePlayed > 60)
            {
                // dont save free play game score
                if (gameModeId != Modes.FreePlay && gameModeId != Modes.BeatThaComputahs)
                {
                    DBConnector.instance.savePlayerGameStats(user);
                    // if username is logged in
                    if (!string.IsNullOrEmpty(GameOptions.userName) && GameOptions.userid != 0) //&& GameOptions.gameModeSelectedId != (int)Enums.ModeId.BeatThaComputahs))
                    {
                        StartCoroutine(APIHelper.PostHighscore(user));
                    }
                    // if user not logged in, set submitted score to false
                    else
                    {
                        DBHelper.instance.setGameScoreSubmitted(user.Scoreid, false);
                    }
                }

                DBConnector.instance.savePlayerAllTimeStats(GameLevelManager.instance.Player1.gameStats);
                DBConnector.instance.savePlayerProfileProgression(GameLevelManager.instance.Player1.gameStats.getExperienceGainedFromSession());

                // post to API
            }
            // alert game manager. trigger
            GameOver = true;
            if (gameModeId == Modes.BeatThaComputahs && !killedOnIdle)
            {
                GameObject.Find("footer").SetActive(false);
                DBConnector.instance.savePlayerAllTimeStats(GameLevelManager.instance.Player1.gameStats);
                DBConnector.instance.savePlayerProfileProgression(GameLevelManager.instance.Player1.gameStats.getExperienceGainedFromSession());
                StartCoroutine(LoadNextCampaignLevel(5));
            }
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
            displayMoneyBallText.text = "";
        }
    }

    private IEnumerator LoadNextCampaignLevel(int seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        List<PlayerIdentifier> players = GameLevelManager.instance.getSortedGameStatsList();
        if (players == null || players.Count < 2)
        {
            Debug.LogError("GameRules cannot advance campaign because fewer than two players were found.");
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            yield break;
        }

        if (GameOptions.levelsList == null || GameOptions.levelsList.Count == 0)
        {
            Debug.LogError("GameRules cannot advance campaign because no campaign levels are loaded.");
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            yield break;
        }

        int completedLevelIndex = Mathf.Clamp(GameOptions.levelSelectedIndex, 0, GameOptions.levelsList.Count - 1);
        EndRoundData.currentLevelIndex = completedLevelIndex;
        EndRoundData.nextLevelIndex = completedLevelIndex;

        // campaign is over
        if (completedLevelIndex < GameOptions.levelsList.Count - 1)
        {
            int nextLevelIndex = completedLevelIndex + 1;
            LevelSelected nextLevel = GameOptions.levelsList[nextLevelIndex];

            GameOptions.levelHasSevenPointers = nextLevel.LevelHasSevenPointers;
            GameOptions.levelId = nextLevel.LevelId;
            GameOptions.levelSelected = nextLevel.LevelObjectName;
            GameOptions.levelDisplayName = nextLevel.LevelDisplayName;
            GameOptions.levelSelectedIndex = nextLevelIndex;

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

        EndRoundData.currentRoundWinnerScore = players[0].gameStats.TotalPoints;
        EndRoundData.currentRoundLoserScore = players[1].gameStats.TotalPoints;

        EndRoundData.currentRoundWinnerIsCpu = players[0].isCpu;
        EndRoundData.currentRoundLoserIsCpu = players[1].isCpu;
        //Debug.Log("level : " + GameOptions.levelsList[GameOptions.levelSelectedIndex+1].LevelDisplayName + " has 7s : "+ GameOptions.levelsList[GameOptions.levelSelectedIndex+1].LevelHasSevenPointers);
        //Debug.Log(EndRoundData.currentRoundWinnerIsCpu);
        //Debug.Log(EndRoundData.currentRoundLoserIsCpu);

        //string sceneName = GameOptions.levelsList[GameOptions.levelSelectedIndex].LevelObjectName + "_" + GameOptions.levelsList[GameOptions.levelSelectedIndex].LevelDescription;
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_end_round_screen);
    }

    public void setTimePlayed()
    {
        // time played end
        timePlayedEnd = Time.time;
        // if player is killed in a game mode that requires a counter
        // if player is killed, high score is being set as time killed
        // must complete game mode to get high score
        gameStats1.TimePlayed = timePlayedEnd - timePlayedStart;
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

    public void updatePlayerScore()
    {
        List<PlayerIdentifier> players = GameLevelManager.instance.getSortedGameStatsList();
        Timer.instance.ScoreClockText.text = players[0].gameStats.TotalPoints.ToString();
        string playerType;
        if (GameOptions.numPlayers > 0 && players[0] != null)
        {
            playerType = players[0].isCpu ? "CPU" : "Player";
            if (!players[0].isCpu) { displayP1ScoreText.color = Color.green; } else { displayP1ScoreText.color = Color.white; }
            displayP1ScoreText.text = playerType + " " + (players[0].pid + 1)
                + "\n" + players[0].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : "+ players[0].characterProfile.Level
                + "\n" + "points : " + players[0].gameStats.TotalPoints
                + "\n" + players[0].gameStats.ShotMade + "/" + players[0].gameStats.ShotAttempt
                + " " + players[0].gameStats.getTotalPointAccuracy().ToString("0.00") + "%";
        }
        if (GameOptions.numPlayers > 1 && players[1] != null)
        {
            playerType = players[1].isCpu ? "CPU" : "Player";
            if (!players[1].isCpu) { displayP2ScoreText.color = Color.green; } else { displayP2ScoreText.color = Color.white; }
            displayP2ScoreText.text = playerType + " " + (players[1].pid + 1)
                + "\n" + players[1].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[1].characterProfile.Level
                + "\n" + "points : " + players[1].gameStats.TotalPoints
                + "\n" + players[1].gameStats.ShotMade + "/" + players[1].gameStats.ShotAttempt
                + " " + players[1].gameStats.getTotalPointAccuracy().ToString("0.00") + "%";
        }
        else
        {
            displayP2ScoreText.gameObject.SetActive(false);
        }
        if (GameOptions.numPlayers > 2 && players[2] != null)
        {
            playerType = players[2].isCpu ? "CPU" : "Player";
            if (!players[2].isCpu) { displayP3ScoreText.color = Color.green; } else { displayP3ScoreText.color = Color.white; }
            displayP3ScoreText.text = playerType + " " + (players[2].pid + 1)
                + "\n" + players[2].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[2].characterProfile.Level
                + "\n" + "points : " + players[2].gameStats.TotalPoints
                + "\n" + players[2].gameStats.ShotMade + "/" + players[2].gameStats.ShotAttempt
                + " " + players[2].gameStats.getTotalPointAccuracy().ToString("0.00") + "%";
        }
        else
        {
            displayP3ScoreText.gameObject.SetActive(false);
        }
        if (GameOptions.numPlayers > 3 && players[3] != null)
        {
            playerType = players[3].isCpu ? "CPU" : "Player";
            if (!players[3].isCpu) { displayP4ScoreText.color = Color.green; } else { displayP4ScoreText.color = Color.white; }
            displayP4ScoreText.text = playerType + " " + (players[3].pid + 1)
                + "\n" + players[3].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[3].characterProfile.Level
                + "\n" + "points : " + players[3].gameStats.TotalPoints
                + "\n" + players[3].gameStats.ShotMade + "/" + players[3].gameStats.ShotAttempt
                + " " + players[3].gameStats.getTotalPointAccuracy().ToString("0.00") + "%";
        }
        else
        {
            displayP4ScoreText.gameObject.SetActive(false);
        }
    }

    private void ToggleMoneyBall()
    {
        if (!moneyBallEnabled)
        {
            moneyBallEnabled = true;
            if (moneyBallEnabled)
            {
                displayMoneyBallText.text = "Money Ball enabled";
            }
        }
        else
        {
            moneyBallEnabled = false;
            displayMoneyBallText.text = "";
        }
    }

    //===================================================== Position markers set up ====================================================
    private void SetPositionMarkers()
    {
        // get all shot position marker objects
        basketBallShotMarkerObjects = GameObject.FindGameObjectsWithTag("shot_marker");

        gameModeRequiresShotMarkers3s = GameOptions.gameModeRequiresShotMarkers3s;
        gameModeRequiresShotMarkers4s = GameOptions.gameModeRequiresShotMarkers4s;
        gameModeRequiresShotMarkers7s = GameOptions.gameModeRequiresShotMarkers7s;

        //load them into list
        foreach (var marker in basketBallShotMarkerObjects)
        {
            BasketBallShotMarker temp = marker.GetComponent<BasketBallShotMarker>();
            // if 0 markers not required, disable them
            if (!gameModeRequiresShotMarkers3s && temp.ShotTypeThree && GameOptions.numPlayers >= 1)
            {
                marker.SetActive(false);
            }
            // if 4 markers not required, disable them
            if (!gameModeRequiresShotMarkers4s && temp.ShotTypeFour && GameOptions.numPlayers >= 1)
            {
                marker.SetActive(false);
            }
            // if 4 markers not required, disable them
            if (!gameModeRequiresShotMarkers7s && temp.ShotTypeSeven && GameOptions.numPlayers >= 1)
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

    // ================================================ set score display ============================================
    public void SetScoreDisplayText()
    {
        GameStats gameStats = GameLevelManager.instance.Player1.basketball.GetComponent<GameStats>();
        if (PlayerData.instance != null)
        {
            //switch (gameModeId)
            //{
            //    case Modes.TotalPoints:
            //        displayCurrentScoreText.text = "total points : " + gameStats.TotalPoints
            //        + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            //        Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
            //        displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPoints;

            //        break;
            //    case Modes.Total3Pointers:
            //        displayCurrentScoreText.text = "3s made : " + gameStats.ThreePointerMade
            //        + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            //        Timer.instance.ScoreClockText.text = gameStats.ThreePointerMade.ToString();

            //        displayHighScoreText.text = "high score : " + PlayerData.instance.ThreePointerMade;
            //        break;
            //    case Modes.Total4Pointers:
            //        break;
            //    case Modes.Total7Pointers:
            //        break;
            //    case Modes.Total4Pointers:
            //        break;
            //    case Modes.Total4Pointers:
            //        break;
            //    case Modes.Total4Pointers:
            //        break;
            //    case Modes.Total4Pointers:
            //        break;
            //    case Modes.Total4Pointers:
            //        break;

            //}
            if (gameModeId == Modes.TotalPoints)
            {
                displayCurrentScoreText.text = "total points : " + gameStats.TotalPoints
                    + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPoints;
                return;
            }
            if ( gameModeId == Modes.Lockdown)
            {
                displayCurrentScoreText.text = "total points : " + gameStats.TotalPoints
                    + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPointsLockDown;
                return;
            }
            if (gameModeId == Modes.Total3Pointers)
            {
                displayCurrentScoreText.text = "3s made : " + gameStats.ThreePointerMade
                    + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
                Timer.instance.ScoreClockText.text = gameStats.ThreePointerMade.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.ThreePointerMade;
                return;
            }
            if (gameModeId == Modes.Total4Pointers)
            {
                displayCurrentScoreText.text = "4s made : " + gameStats.FourPointerMade
                    + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
                Timer.instance.ScoreClockText.text = gameStats.FourPointerMade.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.FourPointerMade;
                return;
            }
            if (gameModeId == Modes.Total7Pointers)
            {
                displayCurrentScoreText.text = "7s made : " + gameStats.SevenPointerMade
                                                            + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
                Timer.instance.ScoreClockText.text = gameStats.SevenPointerMade.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.SevenPointerMade;
                return;
            }
            //if (gameModeId == 5)
            //{
            //    displayCurrentScoreText.text = "longest shot : " + (BasketBall.instance.BasketBallStats.LongestShotMade).ToString("0.00")
            //        + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim).ToString("00.00");
            //    displayHighScoreText.text = "high score : " + PlayerData.instance.LongestShotMade.ToString("0.00");
            //}
            if (gameModeId == Modes.TotalDistance)
            {
                displayCurrentScoreText.text = "total distance : " + (gameStats.TotalDistance).ToString("0.00")
                + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("0.00");
                Timer.instance.ScoreClockText.text = (gameStats.TotalDistance).ToString("0.00");

                displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
                return;
            }
            if (gameModeId == Modes.SpotUp3s)
            {
                displayCurrentScoreText.text = "";
                //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
                displayHighScoreText.text = "high score : " + PlayerData.instance.MakeThreePointersLowTime;

                //displayMoneyText.text = "$" + PlayerStats.instance.Money;
                return;
            }
            if (gameModeId == Modes.SpotUp4s)
            {
                displayCurrentScoreText.text = "";
                //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
                //displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
                displayHighScoreText.text = "high score : " + PlayerData.instance.MakeFourPointersLowTime;
                //displayMoneyText.text = "$" + PlayerStats.instance.Money;
            }
            if (gameModeId == Modes.SpotUp7s)
            {
                displayCurrentScoreText.text = "";
                //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
                //displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
                displayHighScoreText.text = "high score : " + PlayerData.instance.MakeSevenPointersLowTime;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                //displayMoneyText.text = "$" + PlayerStats.instance.Money;
            }
            if (gameModeId == Modes.SpotUpAll)
            {
                displayCurrentScoreText.text = "";
                //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
                displayHighScoreText.text = "high score : " + PlayerData.instance.MakeAllPointersLowTime;
                //displayMoneyText.text = "$" + PlayerStats.instance.Money;
                return;
            }
            //if (gameModeId == 10)
            //{
            //    displayCurrentScoreText.text = "";
            //    //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            //    displayHighScoreText.text = "high score : " + PlayerData.instance.MakeThreePointersMoneyBallLowTime;
            //    displayMoneyText.text = "$" + PlayerStats.instance.Money;
            //}
            //if (gameModeId == 11)
            //{
            //    displayCurrentScoreText.text = "";
            //    //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            //    //displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
            //    displayHighScoreText.text = "high score : " + PlayerData.instance.MakeFourPointersMoneyBallLowTime;
            //    displayMoneyText.text = "$" + PlayerStats.instance.Money;
            //}
            //if (gameModeId == 12)
            //{
            //    displayCurrentScoreText.text = "";
            //    //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            //    displayHighScoreText.text = "high score : " + PlayerData.instance.MakeAllPointersMoneyBallLowTime;
            //    displayMoneyText.text = "$" + PlayerStats.instance.Money;
            //}
            if (gameModeId == Modes.ConsecutiveShots)
            {
                displayCurrentScoreText.text = "Consecutive Shots"
                    + "\nCurrent : " + GameLevelManager.instance.Player1.gameStats.ConsecutiveShotsMade
                    + "\nHigh Shots : " + gameStats.MostConsecutiveShots;
                Timer.instance.ScoreClockText.text = GameLevelManager.instance.Player1.gameStats.ConsecutiveShotsMade.ToString();

                displayHighScoreText.text = "high score : " + PlayerData.instance.MostConsecutiveShots;
                //displayMoneyText.text = "$" + PlayerStats.instance.Money;
                return;
            }
            if (gameModeId == Modes.InThePocket)
            {
                displayCurrentScoreText.text = "total points : " + gameStats.TotalPoints
                    + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType
                    + "\nCurrent Consecutive: " + GameLevelManager.instance.players[0].gameStats.ConsecutiveShotsMade;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();

                // in the pocket is active, display text notifier
                if (GameLevelManager.instance.Player1.gameStats.ConsecutiveShotsMade >= inThePocketActivateValue)
                {
                    displayOtherMessageText.text = "In The Pocket";
                }
                // in the pocket not active, no notifier
                else
                {
                    displayOtherMessageText.text = "";
                }
                displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPointsBonus;
                return;
            }
            if (gameModeId == Modes.ThreePointContest)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.ThreePointContestScore;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                return;
            }
            if (gameModeId == Modes.FourPointContest)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.FourPointContestScore;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                return;
            }
            if (gameModeId == Modes.AllPointContest)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.AllPointContestScore;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                return;
            }
            if (gameModeId == Modes.PointsByDistance)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPointsByDistance;

                displayCurrentScoreText.text =
                    "current distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("00.00")
                    + "\nlast shot : " + Mathf.FloorToInt((BasketBall.instance.LastShotDistance * 6) / 10)
                    + "\ntotal points : " + gameStats.TotalPoints;

                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                return;
            }
            if (gameModeId == Modes.BashUpSomeNerds)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.EnemiesKilled;

                displayCurrentScoreText.text =
                    "nerds bashed : " + (gameStats.EnemiesKilled);
                if (Timer.instance.ScoreClockText != null)
                {
                    Timer.instance.ScoreClockText.text = (gameStats.EnemiesKilled).ToString();
                }
                return;
            }
            if (gameModeId == Modes.BattleRoyal)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.EnemiesKilledBattleRoyal;

                displayCurrentScoreText.text =
                    "nerds bashed : " + (gameStats.EnemiesKilled);
                if (Timer.instance.ScoreClockText != null)
                {
                    Timer.instance.ScoreClockText.text = (gameStats.EnemiesKilled).ToString();
                }
                return;
            }
            if (gameModeId == Modes.CageMatch)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.EnemiesKilledCageMatch;

                displayCurrentScoreText.text =
                    "nerds bashed : " + (gameStats.EnemiesKilled);
                if (Timer.instance.ScoreClockText != null)
                {
                    Timer.instance.ScoreClockText.text = (gameStats.EnemiesKilled).ToString();
                }
                return;
            }
            if (gameModeId == Modes.VersusCpu || gameModeId == Modes.BeatThaComputahs)
            {
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                updatePlayerScore();
                return;
            }
            if (gameModeId == Modes.SevenPointContest)
            {
                displayHighScoreText.text = "high score : " + PlayerData.instance.SevenPointContestScore;
                Timer.instance.ScoreClockText.text = gameStats.TotalPoints.ToString();
                return;
            }
            //if (gameModeId == 21)
            //{
            //    displayHighScoreText.text = "high score : " + PlayerData.instance.EnemiesKilled;

            //    displayCurrentScoreText.text =
            //        "nerds bashed : " + (gameStats.EnemiesKilled);
            //    if (Timer.instance.ScoreClockText != null)
            //    {
            //        Timer.instance.ScoreClockText.text = (gameStats.EnemiesKilled).ToString();
            //    }
            //}

            if (gameModeId == 0 || gameModeId == Modes.FreePlay || gameModeId == Modes.ArcadeMode)
            {
                displayCurrentScoreText.text = "longest shot : " + (gameStats1.LongestShotMade).ToString("0.00")
                                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("00.00");
                Timer.instance.ScoreClockText.text = (gameStats1.LongestShotMade).ToString("0.00");

                if (GameOptions.gameModeSelectedName.ToLower().Contains("free"))
                {
                    displayHighScoreText.text = "high score : " + PlayerData.instance.LongestShotMadeFreePlay.ToString("0.00")
                        + "\nexp gained : " + gameStats1.getExperienceGainedFromSession();
                }
                else
                {
                    displayHighScoreText.text = "high score : " + PlayerData.instance.LongestShotMadeFreePlay.ToString("0.00");
                }
                // if longest shot > saved longest shot
                if ((gameStats.LongestShotMade) > PlayerData.instance.LongestShotMadeFreePlay)
                {
                    //PlayerData.instance.saveStats();
                    PlayerData.instance.LongestShotMadeFreePlay = gameStats1.LongestShotMade;
                    // save to db
                    DBHelper.instance.updateFloatValueByTableAndField("AllTimeStats", "longestShot", PlayerData.instance.LongestShotMadeFreePlay);
                }
                return;
            }
        }
    }

    // ================================================ get end game display text ============================================
    private string GetDisplayText(int modeId)
    {
        string displayText = "";
        if (killedOnIdle)
        {
            displayText = "You dead bruh";
            displayScoreText.alignment = (TextAnchor)TextAlignment.Center;
            displayScoreText.fontSize = 150;
            return displayText;
        }

        if (gameModeId == 1)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == 2)
        {
            displayText = "You made " + gameStats1.ThreePointerMade + " total 3 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == 3)
        {
            displayText = "You made " + gameStats1.FourPointerMade + " total 4 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == 4)
        {
            displayText = "You made " + gameStats1.SevenPointerMade + " total 4 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == 5)
        {
            displayText = "Your longest shot made was " + (gameStats1.LongestShotMade).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        if (gameModeId == 6)
        {
            displayText = "Your total distance for shots made was " + (gameStats1.TotalDistance).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        if (gameModeId > 6 && gameModeId <= 12 || gameModeId == 25)
        {
            int minutes = Mathf.FloorToInt(gameStats1.TimePlayed / 60);
            float seconds = (gameStats1.TimePlayed - (minutes * 60));
            //displayText = "Your time was " + (counterTime).ToString("0.000") + "\n\n" + getStatsTotals();
            displayText = "Your time was " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n" + GetStatsTotals();
        }
        if (gameModeId == 14)
        {
            displayText = "Your most consecutive shots was " + gameStats1.MostConsecutiveShots + "\n\n" + GetStatsTotals();
        }
        //if (gameModeId == 15)
        //{
        //    displayText = "You scored " + basketBallStats.TotalPoints + " total points\n\n" + getStatsTotals();
        //}
        if (gameModeId == 15 || gameModeId == 16 || gameModeId == 17 || gameModeId == 18 || gameModeId == 19
            || gameModeId == 24 || gameModeId == 27)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == 27)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\nYou were blocked " 
                + gameStats1.blockedShots + " times \n\n" + GetStatsTotals();
        }
        if (gameModeId == 20)
        {
            displayText = "You Bashed up " + gameStats1.EnemiesKilled + " nerds"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == 21)
        {
            int minutes = Mathf.FloorToInt(gameStats1.TimePlayed / 60);
            float seconds = (gameStats1.TimePlayed - (minutes * 60));
            displayText = "You Bashed up " + gameStats1.EnemiesKilled + " nerds"
                + "\n\nYou survived for  : " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == 23 || gameModeId == 26)
        {
            List<PlayerIdentifier> players = GameLevelManager.instance.getSortedGameStatsList();
            displayText = players[0].characterProfile.PlayerDisplayName + " wins!"
                + "\n---------------------------------"
                + "\n" + players[0].characterProfile.PlayerDisplayName + " : " + players[0].gameStats.TotalPoints;
            if (GameOptions.numPlayers > 1)
            {
                displayText += "\n" + players[1].characterProfile.PlayerDisplayName + " : " + players[1].gameStats.TotalPoints;
            }
            if (GameOptions.numPlayers > 2)
            {
                displayText += "\n" + players[2].characterProfile.PlayerDisplayName + " : " + players[2].gameStats.TotalPoints;
            }
            if (GameOptions.numPlayers > 3)
            {
                displayText += "\n" + players[3].characterProfile.PlayerDisplayName + " : " + players[3].gameStats.TotalPoints;
            }
        }
        if (gameModeId == 98)
        {
            displayText = "Arcade mode\n\n" + GetStatsTotals();
        }
        if (gameModeId == 99 || gameModeId == 0)
        {
            displayText = "Free Play mode\n\n" + GetStatsTotals();
        }

        return displayText;
    }
    // ================================================ get stats total ============================================

    string GetStatsTotals()
    {
        string scoreText;
        if ((gameModeAllPointContest || gameModeFourPointContest || gameModeThreePointContest || gameModeSevenPointContest)
            && !GameOptions.sniperEnabled)
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + BasketBall.instance.getTotalPointAccuracy().ToString("0.00") + "%\n"
                             + "points : " + gameStats1.TotalPoints + "\n"
                             //+ "bonus points : " + gameStats1.BonusPoints + "\n"
                             + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                             + BasketBall.instance.getTwoPointAccuracy().ToString("00.0") + "%\n"
                             + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                             + BasketBall.instance.getThreePointAccuracy().ToString("00.0") + "%\n"
                             + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                             + BasketBall.instance.getFourPointAccuracy().ToString("00.0") + "%\n"
                             + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                             + BasketBall.instance.getSevenPointAccuracy().ToString("00.0") + "%\n"
                             + "money ball : " + gameStats1.MoneyBallMade + " / " + gameStats1.MoneyBallAttempts + "    "
                             + BasketBall.instance.getAccuracy(gameStats1.MoneyBallMade, gameStats1.MoneyBallAttempts).ToString("00.0") + "%\n"
                             + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                             + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                             + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                             + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else if (GameOptions.sniperEnabled)
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + BasketBall.instance.getTotalPointAccuracy().ToString("0.00") + "%\n"
                 + "points : " + gameStats1.TotalPoints + "\n"
                 + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                 + BasketBall.instance.getTwoPointAccuracy().ToString("00.0") + "%\n"
                 + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                 + BasketBall.instance.getThreePointAccuracy().ToString("00.0") + "%\n"
                 + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                 + BasketBall.instance.getFourPointAccuracy().ToString("00.0") + "%\n"
                 + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                 + BasketBall.instance.getSevenPointAccuracy().ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                 + "sniper accuracy : " + gameStats1.SniperHits + " / " + gameStats1.SniperShots
                    + " " + UtilityFunctions.getPercentageFloat(gameStats1.SniperHits, gameStats1.SniperShots).ToString("00.0") + "%\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + BasketBall.instance.getTotalPointAccuracy().ToString("0.00") + "%\n"
                 + "points : " + gameStats1.TotalPoints + "\n"
                 + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                 + BasketBall.instance.getTwoPointAccuracy().ToString("00.0") + "%\n"
                 + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                 + BasketBall.instance.getThreePointAccuracy().ToString("00.0") + "%\n"
                 + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                 + BasketBall.instance.getFourPointAccuracy().ToString("00.0") + "%\n"
                 + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                 + BasketBall.instance.getSevenPointAccuracy().ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        return scoreText;
    }

    public bool IsGameOver()
    {
        // if all shot markers are cleared
        if (MarkersRemaining <= 0)
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

    public bool GameModeRequiresMoneyBall => gameModeRequiresMoneyBall;

    public bool MoneyBallEnabled
    {
        get => moneyBallEnabled;
        set => moneyBallEnabled = value;
    }

    private void setTimer(float seconds)
    {
        timer.TimeStart = seconds;
    }

    public int GameModeId
    {
        get => gameModeId;
        set => gameModeId = value;
    }

    // TODO: used to allow pause toggle. never set to false. still works somehow. 
    // this needs a deeper look when i get time 
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
    public bool GameModeRequiresConsecutiveShots { get => gameModeRequiresConsecutiveShots; set => gameModeRequiresConsecutiveShots = value; }
    public bool GameModeThreePointContest { get => gameModeThreePointContest; }
    public bool GameModeFourPointContest { get => gameModeFourPointContest; }
    public bool GameModeAllPointContest { get => gameModeAllPointContest; }
    public int InThePocketActivateValue { get => inThePocketActivateValue; set => inThePocketActivateValue = value; }
    public bool GameModeSevenPointContest { get => gameModeSevenPointContest; set => gameModeSevenPointContest = value; }
}
