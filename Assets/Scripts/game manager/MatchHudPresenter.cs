using Assets.Scripts.database;
using Assets.Scripts.Utility;
using System;
using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The match HUD: the live score readout and the end-of-match summary.
///
/// Lifted out of <c>GameRules</c> unchanged. The formatting is the same code it always was - what
/// changed is who owns it, and what happens when the HUD is incomplete.
///
/// That second part was a real bug. GameRules resolved these objects, and if any was missing it
/// set a flag that switched off the score display; its own log message promised "Match results are
/// still saved." They were not - the same flag also gated the durable end-of-match work, so a scene
/// with a renamed HUD object silently never saved a score, never applied experience and never ended
/// the match. Presentation lives here now and cannot gate any of that.
///
/// State the display needs but does not own - the mode, the primary player's stats, the resolved
/// rules - is pushed in by <see cref="GameRules"/> rather than reached for, so the direction of the
/// dependency is visible.
/// </summary>
public class MatchHudPresenter : MonoBehaviour
{
    public const string ProgressionPersistenceWarning =
        "Progress could not be saved. Check local storage before playing another match.";

    // object name that displays score
    private const string displayScoreObjectName = "display_score";
    private const string displayCurrentScoreObjectName = "display_current_score";
    private const string displayHighScoreObjectName = "display_high_score";
    private const string displayMoneyObjectName = "money_display";
    private const string displayMoneyBallObjectName = "money_ball_enabled";
    private const string displayOtherMessageName = "other_message";
    private const string displayP1ScoreObjectName = "display_p1_score";
    private const string displayP2ScoreObjectName = "display_p2_score";
    private const string displayP3ScoreObjectName = "display_p3_score";
    private const string displayP4ScoreObjectName = "display_p4_score";

    /// <summary>
    /// HUD objects every gameplay scene must provide. Level5ProjectValidator asserts these
    /// exist at build time so a rename fails the build instead of the play session.
    /// </summary>
    public static readonly string[] RequiredHudObjectNames =
    {
        displayScoreObjectName,
        displayCurrentScoreObjectName,
        displayHighScoreObjectName,
        displayMoneyObjectName,
        displayMoneyBallObjectName,
        displayOtherMessageName
    };

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

    // Pushed in by GameRules before each use. Not owned here.
    private int gameModeId;
    private GameStats gameStats1;
    private ResolvedMatchRules rules;
    private int inThePocketActivateValue;
    private bool killedOnIdle;
    private bool progressionPersistenceFailed;

    /// <summary>
    /// True when every HUD object was found. The per-frame score display writes to all of them, so
    /// it is switched off rather than throwing once a frame on an incomplete HUD. Nothing outside
    /// presentation may depend on this.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Resolves the HUD objects and clears them. Resolved individually so a missing object is
    /// reported by name rather than failing partway through and leaving the rest unrun.
    /// </summary>
    public void Initialize()
    {
        List<string> fallbackNames = new List<string>();
        Transform fallbackRoot = null;
        displayScoreText = ResolveHudText(
            displayScoreText,
            displayScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(0f, -36f),
            new Vector2(600f, 300f),
            30);
        displayCurrentScoreText = ResolveHudText(
            displayCurrentScoreText,
            displayCurrentScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(0f, -198f),
            new Vector2(250f, 100f),
            30);
        displayHighScoreText = ResolveHudText(
            displayHighScoreText,
            displayHighScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(0f, -250f),
            new Vector2(350f, 80f),
            24);
        displayMoneyText = ResolveHudText(
            displayMoneyText,
            displayMoneyObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(-300f, 250f),
            new Vector2(220f, 60f),
            22);
        displayMoneyBallText = ResolveHudText(
            displayMoneyBallText,
            displayMoneyBallObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(300f, 250f),
            new Vector2(260f, 60f),
            22);
        displayOtherMessageText = ResolveHudText(
            displayOtherMessageText,
            displayOtherMessageName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(0f, 250f),
            new Vector2(500f, 80f),
            24);
        displayP1ScoreText = ResolveHudText(
            displayP1ScoreText,
            displayP1ScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(-300f, 115f),
            new Vector2(220f, 120f),
            20);
        displayP2ScoreText = ResolveHudText(
            displayP2ScoreText,
            displayP2ScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(-100f, 115f),
            new Vector2(220f, 120f),
            20);
        displayP3ScoreText = ResolveHudText(
            displayP3ScoreText,
            displayP3ScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(100f, 115f),
            new Vector2(220f, 120f),
            20);
        displayP4ScoreText = ResolveHudText(
            displayP4ScoreText,
            displayP4ScoreObjectName,
            ref fallbackRoot,
            fallbackNames,
            new Vector2(300f, 115f),
            new Vector2(220f, 120f),
            20);

        ClearText(displayScoreText);
        ClearText(displayCurrentScoreText);
        ClearText(displayHighScoreText);
        ClearText(displayMoneyText);
        ClearText(displayMoneyBallText);
        ClearText(displayOtherMessageText);
        ClearText(displayP1ScoreText);
        ClearText(displayP2ScoreText);
        ClearText(displayP3ScoreText);
        ClearText(displayP4ScoreText);

        IsComplete = HasCompleteScoreHud();
        if (!IsComplete)
        {
            Debug.LogError(
                "The match HUD could not be initialized, so the score display is switched off. "
                + "The match still plays and its results are still saved.",
                this);
            return;
        }

        if (fallbackNames.Count > 0)
        {
            Debug.LogWarning(
                "The match HUD created fallback text objects for this legacy scene: "
                + string.Join(", ", fallbackNames.ToArray()),
                this);
        }
    }

    /// <summary>Gives the display the match state it needs. Called before each use, not stored elsewhere.</summary>
    public void SetMatchContext(int modeId, GameStats primaryStats, ResolvedMatchRules matchRules, int pocketActivateValue)
    {
        gameModeId = modeId;
        gameStats1 = primaryStats;
        rules = matchRules;
        inThePocketActivateValue = pocketActivateValue;
    }

    public void SetKilledOnIdle(bool value)
    {
        killedOnIdle = value;
    }

    public void SetProgressionPersistenceFailed(bool value)
    {
        progressionPersistenceFailed = value;
        if (value && displayOtherMessageText != null)
        {
            displayOtherMessageText.text = ProgressionPersistenceWarning;
        }
    }

    /// <summary>Draws the live score for the current mode. Does nothing on an incomplete HUD.</summary>
    public void ShowLiveScore()
    {
        if (!IsComplete)
        {
            return;
        }

        SetScoreDisplayText();
    }

    /// <summary>Clears the in-play readouts at the end of a match. Safe on an incomplete HUD.</summary>
    public void ClearForMatchEnd()
    {
        ClearEndGameHudText();
    }

    /// <summary>Shows the end-of-match summary for the mode that was played.</summary>
    public void ShowMatchEndSummary()
    {
        if (displayScoreText != null)
        {
            displayScoreText.text = GetDisplayText(gameModeId);
        }
    }

    /// <summary>Blanks the money ball notice. Called per frame while money ball is off.</summary>
    public void HideMoneyBall()
    {
        if (displayMoneyBallText != null)
        {
            displayMoneyBallText.text = "";
        }
    }

    private static void ClearText(Text text)
    {
        if (text != null)
        {
            text.text = "";
        }
    }

    private static Text ResolveHudText(
        Text assigned,
        string objectName,
        ref Transform fallbackRoot,
        List<string> fallbackNames,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize)
    {
        if (assigned != null)
        {
            return assigned;
        }

        Text found = FindSceneText(objectName);
        if (found != null)
        {
            return found;
        }

        fallbackRoot ??= FindOrCreateHudRoot();
        Text created = CreateFallbackText(fallbackRoot, objectName, anchoredPosition, size, fontSize);
        fallbackNames.Add(objectName);
        return created;
    }

    private static Text FindSceneText(string objectName)
    {
        foreach (Text candidate in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (candidate != null
                && candidate.gameObject.name == objectName
                && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindOrCreateHudRoot()
    {
        foreach (Canvas candidate in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                return candidate.transform;
            }
        }

        GameObject canvasObject = new GameObject(
            "runtime_match_hud",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);

        return canvasObject.transform;
    }

    private static Text CreateFallbackText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = GetFallbackFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static Font GetFallbackFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private bool HasCompleteScoreHud()
    {
        return displayScoreText != null
            && displayCurrentScoreText != null
            && displayHighScoreText != null
            && displayMoneyText != null
            && displayMoneyBallText != null
            && displayOtherMessageText != null
            && displayP1ScoreText != null
            && displayP2ScoreText != null
            && displayP3ScoreText != null
            && displayP4ScoreText != null;
    }

    private void ClearEndGameHudText()
    {
        if (displayCurrentScoreText != null)
        {
            displayCurrentScoreText.text = "";
        }
        if (displayHighScoreText != null)
        {
            displayHighScoreText.text = "";
        }
        if (displayMoneyText != null)
        {
            displayMoneyText.text = "";
        }
        if (displayMoneyBallText != null)
        {
            displayMoneyBallText.text = "";
        }
        if (displayOtherMessageText != null)
        {
            displayOtherMessageText.text = progressionPersistenceFailed
                ? ProgressionPersistenceWarning
                : "";
        }
    }

    public void updatePlayerScore()
    {
        List<PlayerIdentifier> players = GameLevelManager.instance.getSortedGameStatsList();
        Timer.instance.ScoreClockText.text = players[0].gameStats.TotalPoints.ToString();
        string playerType;
        if (players.Count > 0 && players[0] != null)
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
        if (players.Count > 1 && players[1] != null)
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
        if (players.Count > 2 && players[2] != null)
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
        if (players.Count > 3 && players[3] != null)
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

                if (gameModeId == Modes.FreePlay)
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

        if (gameModeId == Modes.TotalPoints)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total3Pointers)
        {
            displayText = "You made " + gameStats1.ThreePointerMade + " total 3 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total4Pointers)
        {
            displayText = "You made " + gameStats1.FourPointerMade + " total 4 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total7Pointers)
        {
            displayText = "You made " + gameStats1.SevenPointerMade + " total 7 pointers\n\n" + GetStatsTotals();
        }
        // mode 5 has no entry in Modes.cs - the enum skips from 4 straight to 6 (TotalDistance).
        if (gameModeId == 5)
        {
            displayText = "Your longest shot made was " + (gameStats1.LongestShotMade).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.TotalDistance)
        {
            displayText = "Your total distance for shots made was " + (gameStats1.TotalDistance).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        // range covers modes 7-12 (SpotUp3s/SpotUp4s/SpotUpAll plus three more IDs with no
        // entries in Modes.cs) and 25 (SpotUp7s).
        if (gameModeId > 6 && gameModeId <= 12 || gameModeId == Modes.SpotUp7s)
        {
            int minutes = Mathf.FloorToInt(gameStats1.TimePlayed / 60);
            float seconds = (gameStats1.TimePlayed - (minutes * 60));
            //displayText = "Your time was " + (counterTime).ToString("0.000") + "\n\n" + getStatsTotals();
            displayText = "Your time was " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.ConsecutiveShots)
        {
            displayText = "Your most consecutive shots was " + gameStats1.MostConsecutiveShots + "\n\n" + GetStatsTotals();
        }
        //if (gameModeId == 15)
        //{
        //    displayText = "You scored " + basketBallStats.TotalPoints + " total points\n\n" + getStatsTotals();
        //}
        if (gameModeId == Modes.InThePocket || gameModeId == Modes.ThreePointContest || gameModeId == Modes.FourPointContest
            || gameModeId == Modes.AllPointContest || gameModeId == Modes.PointsByDistance
            || gameModeId == Modes.SevenPointContest || gameModeId == Modes.Lockdown)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Lockdown)
        {
            displayText = "You scored " + gameStats1.TotalPoints + " total points\nYou were blocked "
                + gameStats1.blockedShots + " times \n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.BashUpSomeNerds)
        {
            displayText = "You Bashed up " + gameStats1.EnemiesKilled + " nerds"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == Modes.BattleRoyal)
        {
            int minutes = Mathf.FloorToInt(gameStats1.TimePlayed / 60);
            float seconds = (gameStats1.TimePlayed - (minutes * 60));
            displayText = "You Bashed up " + gameStats1.EnemiesKilled + " nerds"
                + "\n\nYou survived for  : " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == Modes.VersusCpu || gameModeId == Modes.BeatThaComputahs)
        {
            List<PlayerIdentifier> players = GameLevelManager.instance != null
                ? GameLevelManager.instance.getSortedGameStatsList()
                : null;
            if (players == null || players.Count == 0)
            {
                displayText = "Game over\n---------------------------------";
                return displayText;
            }

            displayText = players[0].characterProfile.PlayerDisplayName + " wins!"
                + "\n---------------------------------"
                + "\n" + players[0].characterProfile.PlayerDisplayName + " : " + players[0].gameStats.TotalPoints;
            if (players.Count > 1)
            {
                displayText += "\n" + players[1].characterProfile.PlayerDisplayName + " : " + players[1].gameStats.TotalPoints;
            }
            if (players.Count > 2)
            {
                displayText += "\n" + players[2].characterProfile.PlayerDisplayName + " : " + players[2].gameStats.TotalPoints;
            }
            if (players.Count > 3)
            {
                displayText += "\n" + players[3].characterProfile.PlayerDisplayName + " : " + players[3].gameStats.TotalPoints;
            }
        }
        if (gameModeId == Modes.ArcadeMode)
        {
            displayText = "Arcade mode\n\n" + GetStatsTotals();
        }
        // 0 is the "no mode selected" default, not an actual mode - kept as a raw literal.
        if (gameModeId == Modes.FreePlay || gameModeId == 0)
        {
            displayText = "Free Play mode\n\n" + GetStatsTotals();
        }

        return displayText;
    }

    string GetStatsTotals()
    {
        // Percentages use gameStats1's own counts, not BasketBall.instance's mutable state.
        string scoreText;
        if (rules.IsContest && !rules.SniperEnabled)
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(gameStats1.ShotMade, gameStats1.ShotAttempt).ToString("0.00") + "%\n"
                             + "points : " + gameStats1.TotalPoints + "\n"
                             //+ "bonus points : " + gameStats1.BonusPoints + "\n"
                             + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(gameStats1.TwoPointerMade, gameStats1.TwoPointerAttempts).ToString("00.0") + "%\n"
                             + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(gameStats1.ThreePointerMade, gameStats1.ThreePointerAttempts).ToString("00.0") + "%\n"
                             + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(gameStats1.FourPointerMade, gameStats1.FourPointerAttempts).ToString("00.0") + "%\n"
                             + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(gameStats1.SevenPointerMade, gameStats1.SevenPointerAttempts).ToString("00.0") + "%\n"
                             + "money ball : " + gameStats1.MoneyBallMade + " / " + gameStats1.MoneyBallAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(gameStats1.MoneyBallMade, gameStats1.MoneyBallAttempts).ToString("00.0") + "%\n"
                             + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                             + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                             + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                             + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else if (rules.SniperEnabled)
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(gameStats1.ShotMade, gameStats1.ShotAttempt).ToString("0.00") + "%\n"
                 + "points : " + gameStats1.TotalPoints + "\n"
                 + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.TwoPointerMade, gameStats1.TwoPointerAttempts).ToString("00.0") + "%\n"
                 + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.ThreePointerMade, gameStats1.ThreePointerAttempts).ToString("00.0") + "%\n"
                 + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.FourPointerMade, gameStats1.FourPointerAttempts).ToString("00.0") + "%\n"
                 + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.SevenPointerMade, gameStats1.SevenPointerAttempts).ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                 + "sniper accuracy : " + gameStats1.SniperHits + " / " + gameStats1.SniperShots
                    + " " + UtilityFunctions.getPercentageFloat(gameStats1.SniperHits, gameStats1.SniperShots).ToString("00.0") + "%\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else
        {
            scoreText = "shots  : " + gameStats1.ShotMade + " / " + gameStats1.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(gameStats1.ShotMade, gameStats1.ShotAttempt).ToString("0.00") + "%\n"
                 + "points : " + gameStats1.TotalPoints + "\n"
                 + "2 pointers : " + gameStats1.TwoPointerMade + " / " + gameStats1.TwoPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.TwoPointerMade, gameStats1.TwoPointerAttempts).ToString("00.0") + "%\n"
                 + "3 pointers : " + gameStats1.ThreePointerMade + " / " + gameStats1.ThreePointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.ThreePointerMade, gameStats1.ThreePointerAttempts).ToString("00.0") + "%\n"
                 + "4 pointers : " + gameStats1.FourPointerMade + " / " + gameStats1.FourPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.FourPointerMade, gameStats1.FourPointerAttempts).ToString("00.0") + "%\n"
                 + "7 pointers : " + gameStats1.SevenPointerMade + " / " + gameStats1.SevenPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(gameStats1.SevenPointerMade, gameStats1.SevenPointerAttempts).ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(gameStats1.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(gameStats1.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + gameStats1.MostConsecutiveShots + "\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        return scoreText;
    }
}
