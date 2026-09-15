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

    /// <summary>AUD-012 Phase 2b Slice 62 dependency-cut fields - see <see cref="BindGameLevelManagerContext"/>.</summary>
    private Func<List<PlayerIdentifier>> sortedGameStatsListReader;
    private Func<PlayerIdentifier> primaryPlayerReader;
    private Func<PlayerIdentifier> firstRegisteredPlayerReader;
    private Func<Text> scoreClockTextReader;

    /// <summary>
    /// AUD-012 Phase 2b Slice 62: replaces this class's former direct <c>GameLevelManager.instance</c>/
    /// <c>Timer.instance</c> reads, its last two loose <c>Assembly-CSharp</c> integration points, so
    /// <c>MatchHudPresenter</c> can move out of <c>Assembly-CSharp</c>. Called once from
    /// <c>GameRules.Start()</c>, the same composition point that already resolves this scene's
    /// <c>Timer</c> via <c>SceneObjects.Find&lt;Timer&gt;</c> - <paramref name="scoreClockTextReader"/>
    /// reads that existing captured reference's <c>ScoreClockText</c> live rather than reaching the
    /// <c>Timer.instance</c> static a second way. <paramref name="sortedGameStatsListReader"/>,
    /// <paramref name="primaryPlayerReader"/> and <paramref name="firstRegisteredPlayerReader"/>
    /// resolve <c>GameLevelManager.instance</c> fresh on every call, matching every other adapter in
    /// this migration. A presenter built without this binding (any direct-construction test) fails
    /// exactly as the former direct reads did when their singleton was absent - none of the four
    /// delegates are null-guarded at their call sites below, preserving the former unconditional
    /// dereferences.
    /// </summary>
    public void BindGameLevelManagerContext(
        Func<List<PlayerIdentifier>> sortedGameStatsListReader,
        Func<PlayerIdentifier> primaryPlayerReader,
        Func<PlayerIdentifier> firstRegisteredPlayerReader,
        Func<Text> scoreClockTextReader)
    {
        this.sortedGameStatsListReader = sortedGameStatsListReader;
        this.primaryPlayerReader = primaryPlayerReader;
        this.firstRegisteredPlayerReader = firstRegisteredPlayerReader;
        this.scoreClockTextReader = scoreClockTextReader;
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 64: the immutable HUD-specific projection of the persisted high-score
    /// values <see cref="SetScoreDisplayText"/> reads, in place of a direct <c>PlayerData.instance</c>
    /// read. Contains exactly the properties executable HUD code consumes - no unused/commented-out
    /// <c>PlayerData</c> field, no save/load behavior, no <c>PlayerData</c>/<c>DBHelper</c>/SQLite/JSON/
    /// account type.
    /// </summary>
    public sealed class HighScoreSnapshot
    {
        public HighScoreSnapshot(
            float totalPoints,
            float totalPointsLockDown,
            float threePointerMade,
            float fourPointerMade,
            float sevenPointerMade,
            float totalDistance,
            float makeThreePointersLowTime,
            float makeFourPointersLowTime,
            float makeSevenPointersLowTime,
            float makeAllPointersLowTime,
            int mostConsecutiveShots,
            float totalPointsBonus,
            float threePointContestScore,
            float fourPointContestScore,
            float sevenPointContestScore,
            float allPointContestScore,
            float totalPointsByDistance,
            int enemiesKilled,
            int enemiesKilledBattleRoyal,
            int enemiesKilledCageMatch,
            float longestShotMadeFreePlay)
        {
            TotalPoints = totalPoints;
            TotalPointsLockDown = totalPointsLockDown;
            ThreePointerMade = threePointerMade;
            FourPointerMade = fourPointerMade;
            SevenPointerMade = sevenPointerMade;
            TotalDistance = totalDistance;
            MakeThreePointersLowTime = makeThreePointersLowTime;
            MakeFourPointersLowTime = makeFourPointersLowTime;
            MakeSevenPointersLowTime = makeSevenPointersLowTime;
            MakeAllPointersLowTime = makeAllPointersLowTime;
            MostConsecutiveShots = mostConsecutiveShots;
            TotalPointsBonus = totalPointsBonus;
            ThreePointContestScore = threePointContestScore;
            FourPointContestScore = fourPointContestScore;
            SevenPointContestScore = sevenPointContestScore;
            AllPointContestScore = allPointContestScore;
            TotalPointsByDistance = totalPointsByDistance;
            EnemiesKilled = enemiesKilled;
            EnemiesKilledBattleRoyal = enemiesKilledBattleRoyal;
            EnemiesKilledCageMatch = enemiesKilledCageMatch;
            LongestShotMadeFreePlay = longestShotMadeFreePlay;
        }

        public float TotalPoints { get; }
        public float TotalPointsLockDown { get; }
        public float ThreePointerMade { get; }
        public float FourPointerMade { get; }
        public float SevenPointerMade { get; }
        public float TotalDistance { get; }
        public float MakeThreePointersLowTime { get; }
        public float MakeFourPointersLowTime { get; }
        public float MakeSevenPointersLowTime { get; }
        public float MakeAllPointersLowTime { get; }
        public int MostConsecutiveShots { get; }
        public float TotalPointsBonus { get; }
        public float ThreePointContestScore { get; }
        public float FourPointContestScore { get; }
        public float SevenPointContestScore { get; }
        public float AllPointContestScore { get; }
        public float TotalPointsByDistance { get; }
        public int EnemiesKilled { get; }
        public int EnemiesKilledBattleRoyal { get; }
        public int EnemiesKilledCageMatch { get; }
        public float LongestShotMadeFreePlay { get; }
    }

    // AUD-012 Phase 2b Slice 64 dependency-cut fields - see <see cref="BindPersistenceContext"/>.
    private Func<HighScoreSnapshot> highScoreSnapshotReader;
    private Action<float> persistLongestShotMadeFreePlay;

    /// <summary>
    /// AUD-012 Phase 2b Slice 64: replaces this class's former direct <c>PlayerData.instance</c> reads
    /// (~25) and its one <c>DBHelper.instance.updateFloatValueByTableAndField</c> write - the
    /// persistence-layer blocker Slice 62 explicitly deferred, so <c>MatchHudPresenter</c> can move out
    /// of <c>Assembly-CSharp</c>. Called once from <c>GameRules.Start()</c>, alongside but separate from
    /// <see cref="BindGameLevelManagerContext"/>: that binds game-manager-cycle state, this binds the
    /// persistence-layer ownership boundary - a different concern, so a different bind call.
    /// <paramref name="highScoreSnapshotReader"/> resolves <c>PlayerData.instance</c> fresh on every
    /// call and returns null when it is absent - the exact prior "no live PlayerData -> no
    /// persistence-backed rendering" behavior, now expressed as a null snapshot guard instead of an
    /// inline null check. <paramref name="persistLongestShotMadeFreePlay"/> wraps the exact prior
    /// PlayerData-mutation-then-DBHelper-write pair, unchanged in order and value.
    /// </summary>
    public void BindPersistenceContext(
        Func<HighScoreSnapshot> highScoreSnapshotReader,
        Action<float> persistLongestShotMadeFreePlay)
    {
        this.highScoreSnapshotReader = highScoreSnapshotReader;
        this.persistLongestShotMadeFreePlay = persistLongestShotMadeFreePlay;
    }

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
        List<PlayerIdentifier> players = sortedGameStatsListReader();
        scoreClockTextReader().text = players[0].gameStats.Stats.TotalPoints.ToString();
        string playerType;
        if (players.Count > 0 && players[0] != null)
        {
            MatchStats stats0 = players[0].gameStats.Stats;
            playerType = players[0].isCpu ? "CPU" : "Player";
            if (!players[0].isCpu) { displayP1ScoreText.color = Color.green; } else { displayP1ScoreText.color = Color.white; }
            displayP1ScoreText.text = playerType + " " + (players[0].pid + 1)
                + "\n" + players[0].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : "+ players[0].characterProfile.Level
                + "\n" + "points : " + stats0.TotalPoints
                + "\n" + stats0.ShotMade + "/" + stats0.ShotAttempt
                + " " + stats0.TotalPointAccuracy.ToString("0.00") + "%";
        }
        if (players.Count > 1 && players[1] != null)
        {
            MatchStats stats1 = players[1].gameStats.Stats;
            playerType = players[1].isCpu ? "CPU" : "Player";
            if (!players[1].isCpu) { displayP2ScoreText.color = Color.green; } else { displayP2ScoreText.color = Color.white; }
            displayP2ScoreText.text = playerType + " " + (players[1].pid + 1)
                + "\n" + players[1].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[1].characterProfile.Level
                + "\n" + "points : " + stats1.TotalPoints
                + "\n" + stats1.ShotMade + "/" + stats1.ShotAttempt
                + " " + stats1.TotalPointAccuracy.ToString("0.00") + "%";
        }
        else
        {
            displayP2ScoreText.gameObject.SetActive(false);
        }
        if (players.Count > 2 && players[2] != null)
        {
            MatchStats stats2 = players[2].gameStats.Stats;
            playerType = players[2].isCpu ? "CPU" : "Player";
            if (!players[2].isCpu) { displayP3ScoreText.color = Color.green; } else { displayP3ScoreText.color = Color.white; }
            displayP3ScoreText.text = playerType + " " + (players[2].pid + 1)
                + "\n" + players[2].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[2].characterProfile.Level
                + "\n" + "points : " + stats2.TotalPoints
                + "\n" + stats2.ShotMade + "/" + stats2.ShotAttempt
                + " " + stats2.TotalPointAccuracy.ToString("0.00") + "%";
        }
        else
        {
            displayP3ScoreText.gameObject.SetActive(false);
        }
        if (players.Count > 3 && players[3] != null)
        {
            MatchStats stats3 = players[3].gameStats.Stats;
            playerType = players[3].isCpu ? "CPU" : "Player";
            if (!players[3].isCpu) { displayP4ScoreText.color = Color.green; } else { displayP4ScoreText.color = Color.white; }
            displayP4ScoreText.text = playerType + " " + (players[3].pid + 1)
                + "\n" + players[3].characterProfile.PlayerDisplayName
                //+ "\n" + "lvl : " + players[3].characterProfile.Level
                + "\n" + "points : " + stats3.TotalPoints
                + "\n" + stats3.ShotMade + "/" + stats3.ShotAttempt
                + " " + stats3.TotalPointAccuracy.ToString("0.00") + "%";
        }
        else
        {
            displayP4ScoreText.gameObject.SetActive(false);
        }
    }

    // ================================================ set score display ============================================
    public void SetScoreDisplayText()
    {
        MatchStats stats = gameStats1.Stats;
        // AUD-012 Phase 2b Slice 64: resolved once per call and used for the entire invocation -
        // matches the exact prior "no live PlayerData -> no persistence-backed rendering" behavior
        // (see BindPersistenceContext), now as a null-snapshot guard clause instead of an inline
        // PlayerData.instance != null wrapping the whole method body.
        HighScoreSnapshot highScores = highScoreSnapshotReader();
        if (highScores == null)
        {
            return;
        }

        //switch (gameModeId)
        //{
        //    case Modes.TotalPoints:
        //        displayCurrentScoreText.text = "total points : " + gameStats.Stats.TotalPoints
        //        + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
        //        scoreClockTextReader().text = gameStats.Stats.TotalPoints.ToString();
        //        displayHighScoreText.text = "high score : " + PlayerData.instance.TotalPoints;

        //        break;
        //    case Modes.Total3Pointers:
        //        displayCurrentScoreText.text = "3s made : " + gameStats.Stats.ThreePointerMade
        //        + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
        //        scoreClockTextReader().text = gameStats.Stats.ThreePointerMade.ToString();

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
            displayCurrentScoreText.text = "total points : " + stats.TotalPoints
                + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            scoreClockTextReader().text = stats.TotalPoints.ToString();

            displayHighScoreText.text = "high score : " + highScores.TotalPoints;
            return;
        }
        if ( gameModeId == Modes.Lockdown)
        {
            displayCurrentScoreText.text = "total points : " + stats.TotalPoints
                + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            scoreClockTextReader().text = stats.TotalPoints.ToString();

            displayHighScoreText.text = "high score : " + highScores.TotalPointsLockDown;
            return;
        }
        if (gameModeId == Modes.Total3Pointers)
        {
            displayCurrentScoreText.text = "3s made : " + stats.ThreePointerMade
                + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            scoreClockTextReader().text = stats.ThreePointerMade.ToString();

            displayHighScoreText.text = "high score : " + highScores.ThreePointerMade;
            return;
        }
        if (gameModeId == Modes.Total4Pointers)
        {
            displayCurrentScoreText.text = "4s made : " + stats.FourPointerMade
                + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            scoreClockTextReader().text = stats.FourPointerMade.ToString();

            displayHighScoreText.text = "high score : " + highScores.FourPointerMade;
            return;
        }
        if (gameModeId == Modes.Total7Pointers)
        {
            displayCurrentScoreText.text = "7s made : " + stats.SevenPointerMade
                                                        + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType;
            scoreClockTextReader().text = stats.SevenPointerMade.ToString();

            displayHighScoreText.text = "high score : " + highScores.SevenPointerMade;
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
            displayCurrentScoreText.text = "total distance : " + (stats.TotalDistance).ToString("0.00")
            + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("0.00");
            scoreClockTextReader().text = (stats.TotalDistance).ToString("0.00");

            displayHighScoreText.text = "high score : " + highScores.TotalDistance.ToString("0.00");
            return;
        }
        if (gameModeId == Modes.SpotUp3s)
        {
            displayCurrentScoreText.text = "";
            //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            displayHighScoreText.text = "high score : " + highScores.MakeThreePointersLowTime;

            //displayMoneyText.text = "$" + PlayerStats.instance.Money;
            return;
        }
        if (gameModeId == Modes.SpotUp4s)
        {
            displayCurrentScoreText.text = "";
            //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            //displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
            displayHighScoreText.text = "high score : " + highScores.MakeFourPointersLowTime;
            //displayMoneyText.text = "$" + PlayerStats.instance.Money;
        }
        if (gameModeId == Modes.SpotUp7s)
        {
            displayCurrentScoreText.text = "";
            //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            //displayHighScoreText.text = "high score : " + PlayerData.instance.TotalDistance.ToString("0.00");
            displayHighScoreText.text = "high score : " + highScores.MakeSevenPointersLowTime;
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            //displayMoneyText.text = "$" + PlayerStats.instance.Money;
        }
        if (gameModeId == Modes.SpotUpAll)
        {
            displayCurrentScoreText.text = "";
            //                                                 + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.BallDistanceFromRim * 6).ToString("0.00");
            displayHighScoreText.text = "high score : " + highScores.MakeAllPointersLowTime;
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
                + "\nCurrent : " + primaryPlayerReader().gameStats.Stats.ConsecutiveShotsMade
                + "\nHigh Shots : " + stats.MostConsecutiveShots;
            scoreClockTextReader().text = primaryPlayerReader().gameStats.Stats.ConsecutiveShotsMade.ToString();

            displayHighScoreText.text = "high score : " + highScores.MostConsecutiveShots;
            //displayMoneyText.text = "$" + PlayerStats.instance.Money;
            return;
        }
        if (gameModeId == Modes.InThePocket)
        {
            displayCurrentScoreText.text = "total points : " + stats.TotalPoints
                + "\ncurrent shot : " + BasketBall.instance.BasketBallState.CurrentShotType
                + "\nCurrent Consecutive: " + firstRegisteredPlayerReader().gameStats.Stats.ConsecutiveShotsMade;
            scoreClockTextReader().text = stats.TotalPoints.ToString();

            // in the pocket is active, display text notifier
            if (primaryPlayerReader().gameStats.Stats.ConsecutiveShotsMade >= inThePocketActivateValue)
            {
                displayOtherMessageText.text = "In The Pocket";
            }
            // in the pocket not active, no notifier
            else
            {
                displayOtherMessageText.text = "";
            }
            displayHighScoreText.text = "high score : " + highScores.TotalPointsBonus;
            return;
        }
        if (gameModeId == Modes.ThreePointContest)
        {
            displayHighScoreText.text = "high score : " + highScores.ThreePointContestScore;
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            return;
        }
        if (gameModeId == Modes.FourPointContest)
        {
            displayHighScoreText.text = "high score : " + highScores.FourPointContestScore;
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            return;
        }
        if (gameModeId == Modes.AllPointContest)
        {
            displayHighScoreText.text = "high score : " + highScores.AllPointContestScore;
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            return;
        }
        if (gameModeId == Modes.PointsByDistance)
        {
            displayHighScoreText.text = "high score : " + highScores.TotalPointsByDistance;

            displayCurrentScoreText.text =
                "current distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("00.00")
                + "\nlast shot : " + Mathf.FloorToInt((BasketBall.instance.LastShotDistance * 6) / 10)
                + "\ntotal points : " + stats.TotalPoints;

            scoreClockTextReader().text = stats.TotalPoints.ToString();
            return;
        }
        if (gameModeId == Modes.BashUpSomeNerds)
        {
            displayHighScoreText.text = "high score : " + highScores.EnemiesKilled;

            displayCurrentScoreText.text =
                "nerds bashed : " + (stats.EnemiesKilled);
            if (scoreClockTextReader() != null)
            {
                scoreClockTextReader().text = (stats.EnemiesKilled).ToString();
            }
            return;
        }
        if (gameModeId == Modes.BattleRoyal)
        {
            displayHighScoreText.text = "high score : " + highScores.EnemiesKilledBattleRoyal;

            displayCurrentScoreText.text =
                "nerds bashed : " + (stats.EnemiesKilled);
            if (scoreClockTextReader() != null)
            {
                scoreClockTextReader().text = (stats.EnemiesKilled).ToString();
            }
            return;
        }
        if (gameModeId == Modes.CageMatch)
        {
            displayHighScoreText.text = "high score : " + highScores.EnemiesKilledCageMatch;

            displayCurrentScoreText.text =
                "nerds bashed : " + (stats.EnemiesKilled);
            if (scoreClockTextReader() != null)
            {
                scoreClockTextReader().text = (stats.EnemiesKilled).ToString();
            }
            return;
        }
        if (gameModeId == Modes.VersusCpu || gameModeId == Modes.BeatThaComputahs)
        {
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            updatePlayerScore();
            return;
        }
        if (gameModeId == Modes.SevenPointContest)
        {
            displayHighScoreText.text = "high score : " + highScores.SevenPointContestScore;
            scoreClockTextReader().text = stats.TotalPoints.ToString();
            return;
        }
        //if (gameModeId == 21)
        //{
        //    displayHighScoreText.text = "high score : " + PlayerData.instance.EnemiesKilled;

        //    displayCurrentScoreText.text =
        //        "nerds bashed : " + (gameStats.Stats.EnemiesKilled);
        //    if (scoreClockTextReader() != null)
        //    {
        //        scoreClockTextReader().text = (gameStats.Stats.EnemiesKilled).ToString();
        //    }
        //}

        if (gameModeId == 0 || gameModeId == Modes.FreePlay || gameModeId == Modes.ArcadeMode)
        {
            displayCurrentScoreText.text = "longest shot : " + (gameStats1.Stats.LongestShotMade).ToString("0.00")
                                                             + "\ncurrent distance : " + (BasketBall.instance.BasketBallState.PlayerDistanceFromRim * 6).ToString("00.00");
            scoreClockTextReader().text = (gameStats1.Stats.LongestShotMade).ToString("0.00");

            if (gameModeId == Modes.FreePlay)
            {
                displayHighScoreText.text = "high score : " + highScores.LongestShotMadeFreePlay.ToString("0.00")
                    + "\nexp gained : " + gameStats1.getExperienceGainedFromSession();
            }
            else
            {
                displayHighScoreText.text = "high score : " + highScores.LongestShotMadeFreePlay.ToString("0.00");
            }
            // if longest shot > saved longest shot
            if ((stats.LongestShotMade) > highScores.LongestShotMadeFreePlay)
            {
                // AUD-012 Phase 2b Slice 64: was PlayerData.instance.LongestShotMadeFreePlay = ...
                // followed by DBHelper.instance.updateFloatValueByTableAndField(...) - both now live
                // behind this one delegate (see BindPersistenceContext), same value, same order.
                persistLongestShotMadeFreePlay(gameStats1.Stats.LongestShotMade);
            }
            return;
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

        MatchStats stats = gameStats1.Stats;

        if (gameModeId == Modes.TotalPoints)
        {
            displayText = "You scored " + stats.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total3Pointers)
        {
            displayText = "You made " + stats.ThreePointerMade + " total 3 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total4Pointers)
        {
            displayText = "You made " + stats.FourPointerMade + " total 4 pointers\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Total7Pointers)
        {
            displayText = "You made " + stats.SevenPointerMade + " total 7 pointers\n\n" + GetStatsTotals();
        }
        // mode 5 has no entry in Modes.cs - the enum skips from 4 straight to 6 (TotalDistance).
        if (gameModeId == 5)
        {
            displayText = "Your longest shot made was " + (stats.LongestShotMade).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.TotalDistance)
        {
            displayText = "Your total distance for shots made was " + (stats.TotalDistance).ToString("0.00") + " ft.\n\n" + GetStatsTotals();
        }
        // range covers modes 7-12 (SpotUp3s/SpotUp4s/SpotUpAll plus three more IDs with no
        // entries in Modes.cs) and 25 (SpotUp7s).
        if (gameModeId > 6 && gameModeId <= 12 || gameModeId == Modes.SpotUp7s)
        {
            int minutes = Mathf.FloorToInt(stats.TimePlayed / 60);
            float seconds = (stats.TimePlayed - (minutes * 60));
            //displayText = "Your time was " + (counterTime).ToString("0.000") + "\n\n" + getStatsTotals();
            displayText = "Your time was " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.ConsecutiveShots)
        {
            displayText = "Your most consecutive shots was " + stats.MostConsecutiveShots + "\n\n" + GetStatsTotals();
        }
        //if (gameModeId == 15)
        //{
        //    displayText = "You scored " + basketBallStats.TotalPoints + " total points\n\n" + getStatsTotals();
        //}
        if (gameModeId == Modes.InThePocket || gameModeId == Modes.ThreePointContest || gameModeId == Modes.FourPointContest
            || gameModeId == Modes.AllPointContest || gameModeId == Modes.PointsByDistance
            || gameModeId == Modes.SevenPointContest || gameModeId == Modes.Lockdown)
        {
            displayText = "You scored " + stats.TotalPoints + " total points\n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.Lockdown)
        {
            displayText = "You scored " + stats.TotalPoints + " total points\nYou were blocked "
                + stats.BlockedShots + " times \n\n" + GetStatsTotals();
        }
        if (gameModeId == Modes.BashUpSomeNerds)
        {
            displayText = "You Bashed up " + stats.EnemiesKilled + " nerds"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == Modes.BattleRoyal)
        {
            int minutes = Mathf.FloorToInt(stats.TimePlayed / 60);
            float seconds = (stats.TimePlayed - (minutes * 60));
            displayText = "You Bashed up " + stats.EnemiesKilled + " nerds"
                + "\n\nYou survived for  : " + minutes.ToString("0") + ":" + seconds.ToString("00.000") + "\n\n"
                + "\n\nexperience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        if (gameModeId == Modes.VersusCpu || gameModeId == Modes.BeatThaComputahs)
        {
            List<PlayerIdentifier> players = sortedGameStatsListReader != null
                ? sortedGameStatsListReader()
                : null;
            if (players == null || players.Count == 0)
            {
                displayText = "Game over\n---------------------------------";
                return displayText;
            }

            displayText = players[0].characterProfile.PlayerDisplayName + " wins!"
                + "\n---------------------------------"
                + "\n" + players[0].characterProfile.PlayerDisplayName + " : " + players[0].gameStats.Stats.TotalPoints;
            if (players.Count > 1)
            {
                displayText += "\n" + players[1].characterProfile.PlayerDisplayName + " : " + players[1].gameStats.Stats.TotalPoints;
            }
            if (players.Count > 2)
            {
                displayText += "\n" + players[2].characterProfile.PlayerDisplayName + " : " + players[2].gameStats.Stats.TotalPoints;
            }
            if (players.Count > 3)
            {
                displayText += "\n" + players[3].characterProfile.PlayerDisplayName + " : " + players[3].gameStats.Stats.TotalPoints;
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
        MatchStats stats = gameStats1.Stats;
        string scoreText;
        if (rules.IsContest && !rules.SniperEnabled)
        {
            scoreText = "shots  : " + stats.ShotMade + " / " + stats.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(stats.ShotMade, stats.ShotAttempt).ToString("0.00") + "%\n"
                             + "points : " + stats.TotalPoints + "\n"
                             //+ "bonus points : " + stats.BonusPoints + "\n"
                             + "2 pointers : " + stats.TwoPointerMade + " / " + stats.TwoPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(stats.TwoPointerMade, stats.TwoPointerAttempts).ToString("00.0") + "%\n"
                             + "3 pointers : " + stats.ThreePointerMade + " / " + stats.ThreePointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(stats.ThreePointerMade, stats.ThreePointerAttempts).ToString("00.0") + "%\n"
                             + "4 pointers : " + stats.FourPointerMade + " / " + stats.FourPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(stats.FourPointerMade, stats.FourPointerAttempts).ToString("00.0") + "%\n"
                             + "7 pointers : " + stats.SevenPointerMade + " / " + stats.SevenPointerAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(stats.SevenPointerMade, stats.SevenPointerAttempts).ToString("00.0") + "%\n"
                             + "money ball : " + stats.MoneyBallMade + " / " + stats.MoneyBallAttempts + "    "
                             + UtilityFunctions.getPercentageFloat(stats.MoneyBallMade, stats.MoneyBallAttempts).ToString("00.0") + "%\n"
                             + "longest shot distance : " + (Math.Round(stats.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                             + "total shots made distance : " + (Math.Round(stats.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                             + "most consecutive shots : " + stats.MostConsecutiveShots + "\n"
                             + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else if (rules.SniperEnabled)
        {
            scoreText = "shots  : " + stats.ShotMade + " / " + stats.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(stats.ShotMade, stats.ShotAttempt).ToString("0.00") + "%\n"
                 + "points : " + stats.TotalPoints + "\n"
                 + "2 pointers : " + stats.TwoPointerMade + " / " + stats.TwoPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.TwoPointerMade, stats.TwoPointerAttempts).ToString("00.0") + "%\n"
                 + "3 pointers : " + stats.ThreePointerMade + " / " + stats.ThreePointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.ThreePointerMade, stats.ThreePointerAttempts).ToString("00.0") + "%\n"
                 + "4 pointers : " + stats.FourPointerMade + " / " + stats.FourPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.FourPointerMade, stats.FourPointerAttempts).ToString("00.0") + "%\n"
                 + "7 pointers : " + stats.SevenPointerMade + " / " + stats.SevenPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.SevenPointerMade, stats.SevenPointerAttempts).ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(stats.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(stats.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + stats.MostConsecutiveShots + "\n"
                 + "sniper accuracy : " + stats.SniperHits + " / " + stats.SniperShots
                    + " " + UtilityFunctions.getPercentageFloat(stats.SniperHits, stats.SniperShots).ToString("00.0") + "%\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        else
        {
            scoreText = "shots  : " + stats.ShotMade + " / " + stats.ShotAttempt + " " + UtilityFunctions.getPercentageFloat(stats.ShotMade, stats.ShotAttempt).ToString("0.00") + "%\n"
                 + "points : " + stats.TotalPoints + "\n"
                 + "2 pointers : " + stats.TwoPointerMade + " / " + stats.TwoPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.TwoPointerMade, stats.TwoPointerAttempts).ToString("00.0") + "%\n"
                 + "3 pointers : " + stats.ThreePointerMade + " / " + stats.ThreePointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.ThreePointerMade, stats.ThreePointerAttempts).ToString("00.0") + "%\n"
                 + "4 pointers : " + stats.FourPointerMade + " / " + stats.FourPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.FourPointerMade, stats.FourPointerAttempts).ToString("00.0") + "%\n"
                 + "7 pointers : " + stats.SevenPointerMade + " / " + stats.SevenPointerAttempts + "    "
                 + UtilityFunctions.getPercentageFloat(stats.SevenPointerMade, stats.SevenPointerAttempts).ToString("00.0") + "%\n"
                 + "longest shot distance : " + (Math.Round(stats.LongestShotMade, 2)).ToString("0.00") + " ft.\n"
                 + "total shots made distance : " + (Math.Round(stats.TotalDistance, 2)).ToString("0.00") + " ft.\n"
                 + "most consecutive shots : " + stats.MostConsecutiveShots + "\n"
                 + "experience gained : " + gameStats1.getExperienceGainedFromSession();
        }
        return scoreText;
    }
}
