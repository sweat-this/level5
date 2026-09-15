using System;
using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-012 Phase 2b Slice 62: <see cref="MatchHudPresenter"/> no longer reads
/// <c>GameLevelManager.instance</c>/<c>Timer.instance</c> itself - <see cref="MatchHudPresenter.updatePlayerScore"/>
/// and the <c>VersusCpu</c>/<c>BeatThaComputahs</c> branch of the private <c>GetDisplayText</c> only
/// consult whatever delegates <see cref="MatchHudPresenter.BindGameLevelManagerContext"/> was called
/// with. Mirrors the shape of <c>Level5TimerMatchEndContextTests</c>: composition-forwarding tests
/// driving the real methods directly (both are public; <c>GetDisplayText</c> via reflection), then
/// separate production-adapter tests against live <see cref="GameLevelManager"/>/<see cref="GameRules"/>
/// singletons.
///
/// Slice 62 itself deliberately did not exercise <see cref="MatchHudPresenter.SetScoreDisplayText"/>'s
/// many game-mode branches - every one of them was gated behind a live <c>PlayerData.instance</c>, the
/// persistence-layer blocker that slice explicitly deferred (see docs/systems-restructure-plan.md,
/// Slice 62). AUD-012 Phase 2b Slice 64 cuts that blocker - see the "persistence-context binding"
/// region below for the <c>HighScoreSnapshot</c>/persistence-action tests it adds.
/// </summary>
public class Level5MatchHudPresenterCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameLevelManager savedGameLevelManagerInstance;
    private GameRules savedGameRulesInstance;
    private PlayerData savedPlayerDataInstance;
    private DBHelper savedDBHelperInstance;
    private BasketBall savedBasketBallInstance;

    [SetUp]
    public void SetUp()
    {
        savedGameLevelManagerInstance = GameLevelManager.instance;
        savedGameRulesInstance = GameRules.instance;
        savedPlayerDataInstance = PlayerData.instance;
        savedDBHelperInstance = DBHelper.instance;
        savedBasketBallInstance = BasketBall.instance;
    }

    [TearDown]
    public void TearDown()
    {
        GameLevelManager.instance = savedGameLevelManagerInstance;
        GameRules.instance = savedGameRulesInstance;
        PlayerData.instance = savedPlayerDataInstance;
        DBHelper.instance = savedDBHelperInstance;
        BasketBall.instance = savedBasketBallInstance;

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private GameLevelManager SpawnManagerWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<GameLevelManager>();
    }

    /// <summary>
    /// Same "inactive GameObject" trick as <see cref="SpawnManagerWithoutAwake"/>: adding the
    /// component while the GameObject is inactive defers <c>PlayerData.Awake</c>/<c>Start</c>
    /// indefinitely, so this fixture never calls <c>DontDestroyOnLoad</c> outside Play Mode or touches
    /// <c>Start</c>'s database-tag load path. The tests that need a live <c>PlayerData.instance</c> set
    /// it explicitly instead of relying on <c>Awake</c>'s singleton assignment.
    /// </summary>
    private PlayerData SpawnPlayerDataWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<PlayerData>();
    }

    /// <summary>
    /// Several SetScoreDisplayText mode branches (TotalPoints, FreePlay/ArcadeMode/mode 0, among
    /// others) dereference <c>BasketBall.instance.BasketBallState</c> directly - a live player/
    /// basketball dependency this slice does not touch (see docs/phase1d-game-manager-edge-plan.md).
    /// <c>BasketBall</c> sets its own <c>instance</c> only from <c>Start()</c>, which this fixture's
    /// plain (non-PlayMode) tests never tick, so it must be wired here instead. Neither
    /// <c>BasketBall</c> nor <c>BasketBallState</c> declares an <c>Awake</c>/<c>OnEnable</c>, so a plain
    /// <c>AddComponent</c> is safe with no inactive-GameObject deferral needed.
    /// </summary>
    private BasketBall SpawnLiveBasketBall(string name)
    {
        GameObject go = Spawn(name);
        BasketBall ball = go.AddComponent<BasketBall>();
        BasketBallState state = go.AddComponent<BasketBallState>();
        SetPrivateField(ball, "basketBallState", state);
        return ball;
    }

    /// <summary>
    /// Builds a <see cref="MatchHudPresenter.HighScoreSnapshot"/> with every field defaulted to zero
    /// except the ones a test names - keeps each focused test's arrange step to the one or two fields
    /// it actually cares about instead of retyping all twenty-one positional constructor arguments.
    /// </summary>
    private static MatchHudPresenter.HighScoreSnapshot MakeSnapshot(
        float totalPoints = 0f,
        float totalPointsLockDown = 0f,
        float threePointerMade = 0f,
        float fourPointerMade = 0f,
        float sevenPointerMade = 0f,
        float totalDistance = 0f,
        float makeThreePointersLowTime = 0f,
        float makeFourPointersLowTime = 0f,
        float makeSevenPointersLowTime = 0f,
        float makeAllPointersLowTime = 0f,
        int mostConsecutiveShots = 0,
        float totalPointsBonus = 0f,
        float threePointContestScore = 0f,
        float fourPointContestScore = 0f,
        float sevenPointContestScore = 0f,
        float allPointContestScore = 0f,
        float totalPointsByDistance = 0f,
        int enemiesKilled = 0,
        int enemiesKilledBattleRoyal = 0,
        int enemiesKilledCageMatch = 0,
        float longestShotMadeFreePlay = 0f)
    {
        return new MatchHudPresenter.HighScoreSnapshot(
            totalPoints, totalPointsLockDown, threePointerMade, fourPointerMade, sevenPointerMade,
            totalDistance, makeThreePointersLowTime, makeFourPointersLowTime, makeSevenPointersLowTime,
            makeAllPointersLowTime, mostConsecutiveShots, totalPointsBonus, threePointContestScore,
            fourPointContestScore, sevenPointContestScore, allPointContestScore, totalPointsByDistance,
            enemiesKilled, enemiesKilledBattleRoyal, enemiesKilledCageMatch, longestShotMadeFreePlay);
    }

    private Text SpawnText(string name)
    {
        return Spawn(name).AddComponent<Text>();
    }

    private MatchHudPresenter MakePresenter()
    {
        return Spawn("hud").AddComponent<MatchHudPresenter>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    private static string InvokeGetDisplayText(MatchHudPresenter hud, int modeId)
    {
        MethodInfo method = typeof(MatchHudPresenter).GetMethod("GetDisplayText", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "MatchHudPresenter must declare GetDisplayText(int)");
        return (string)method.Invoke(hud, new object[] { modeId });
    }

    private PlayerIdentifier MakePlayer(string name, int pid, string displayName)
    {
        GameObject go = Spawn(name);
        PlayerIdentifier player = go.AddComponent<PlayerIdentifier>();
        player.pid = pid;
        player.isCpu = false;
        player.characterProfile = go.AddComponent<CharacterProfile>();
        player.characterProfile.PlayerDisplayName = displayName;
        player.gameStats = go.AddComponent<GameStats>();
        player.gameStats.Stats.TotalPoints = 7;
        return player;
    }

    // ==================== updatePlayerScore() forwarding ====================

    [Test]
    public void UpdatePlayerScore_ForwardsThroughSortedGameStatsListReaderAndScoreClockTextReader()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "displayP1ScoreText", SpawnText("p1"));
        SetPrivateField(hud, "displayP2ScoreText", SpawnText("p2"));
        SetPrivateField(hud, "displayP3ScoreText", SpawnText("p3"));
        SetPrivateField(hud, "displayP4ScoreText", SpawnText("p4"));

        PlayerIdentifier player = MakePlayer("player1", 0, "Blood");
        Text scoreClock = SpawnText("score-clock");

        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier> { player },
            primaryPlayerReader: () => player,
            firstRegisteredPlayerReader: () => player,
            scoreClockTextReader: () => scoreClock);

        hud.updatePlayerScore();

        Assert.That(scoreClock.text, Is.EqualTo("7"),
            "the score clock must show the exact TotalPoints of the sortedGameStatsListReader's first entry.");
        Assert.That((Text)GetPrivateField(hud, "displayP1ScoreText"), Has.Property("text").Contains("Blood"),
            "player 1's display must include the character's display name from the forwarded PlayerIdentifier.");
    }

    // ==================== GetDisplayText's VersusCpu branch forwarding ====================

    [Test]
    public void GetDisplayText_VersusCpuMode_NoPlayers_ReportsGameOverWithoutThrowing()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.VersusCpu);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => null);

        string result = InvokeGetDisplayText(hud, Modes.VersusCpu);

        Assert.That(result, Does.StartWith("Game over"),
            "an empty sortedGameStatsListReader result must report 'Game over' rather than throwing - unchanged from the former GameLevelManager.instance.getSortedGameStatsList() empty-list behavior.");
    }

    [Test]
    public void GetDisplayText_VersusCpuMode_ForwardsThroughSortedGameStatsListReader()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.VersusCpu);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());

        PlayerIdentifier winner = MakePlayer("winner", 0, "Champ");
        bool readerCalled = false;
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => { readerCalled = true; return new List<PlayerIdentifier> { winner }; },
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => null);

        string result = InvokeGetDisplayText(hud, Modes.VersusCpu);

        Assert.IsTrue(readerCalled, "GetDisplayText's VersusCpu branch must consult the bound sortedGameStatsListReader.");
        Assert.That(result, Does.Contain("Champ wins!"));
    }

    /// <summary>
    /// Regression test for a defect caught in code review: the first version of
    /// <c>ReadSortedGameStatsListForHud</c> guarded only "is the delegate bound" (always true in
    /// production), not "is <c>GameLevelManager.instance</c> actually alive" - silently turning the
    /// original <c>GetDisplayText</c> ternary's null-safe "no live GameLevelManager -&gt; 'Game over'"
    /// fallback into an unhandled <see cref="NullReferenceException"/>. Binds the real production
    /// adapter (not a hand-written stand-in) so a regression in the adapter itself, not just in this
    /// fixture's understanding of it, would be caught here.
    /// </summary>
    [Test]
    public void GetDisplayText_VersusCpuMode_BoundReaderButNoLiveGameLevelManager_ReportsGameOverWithoutThrowing()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.VersusCpu);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        GameLevelManager.instance = null;

        MethodInfo adapter = typeof(GameRules).GetMethod("ReadSortedGameStatsListForHud", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(adapter, "GameRules.ReadSortedGameStatsListForHud must exist");
        Func<List<PlayerIdentifier>> realAdapter = () => (List<PlayerIdentifier>)adapter.Invoke(null, null);

        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: realAdapter,
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => null);

        string result = null;
        Assert.DoesNotThrow(() => result = InvokeGetDisplayText(hud, Modes.VersusCpu),
            "a bound sortedGameStatsListReader whose underlying GameLevelManager.instance is null must not throw.");
        Assert.That(result, Does.StartWith("Game over"));
    }

    // ================ production adapters: GameRules' MatchHudPresenter composition ================

    private static MethodInfo GameRulesAdapterMethod(string name, BindingFlags extra = BindingFlags.Static)
    {
        MethodInfo method = typeof(GameRules).GetMethod(name, BindingFlags.NonPublic | extra);
        Assert.IsNotNull(method, $"GameRules.{name} must exist as a production MatchHudPresenter adapter");
        return method;
    }

    [Test]
    public void ReadSortedGameStatsListForHud_NoLiveGameLevelManager_ReturnsNullRatherThanThrowing()
    {
        GameLevelManager.instance = null;

        object result = GameRulesAdapterMethod("ReadSortedGameStatsListForHud").Invoke(null, null);

        Assert.IsNull(result);
    }

    [Test]
    public void ReadSortedGameStatsListForHud_LiveGameLevelManager_ForwardsResult()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadSortedGameStatsListForHud").Invoke(null, null);

        Assert.That(result, Is.EqualTo(manager.getSortedGameStatsList()));
    }

    [Test]
    public void ReadPrimaryPlayerForHud_LiveGameLevelManager_ForwardsPlayer1()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadPrimaryPlayerForHud").Invoke(null, null);

        Assert.AreSame(manager.Player1, result);
    }

    [Test]
    public void ReadFirstRegisteredPlayerForHud_LiveGameLevelManager_ForwardsFirstRegisteredEntry()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadFirstRegisteredPlayerForHud").Invoke(null, null);

        Assert.AreSame(manager.players[0], result);
    }

    [Test]
    public void ReadScoreClockTextForHud_ReadsThisInstancesOwnTimerFieldNotTheStatic()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        Text ownTimerScoreClock = SpawnText("own-timer-score-clock");
        Timer ownTimer = Spawn("own-timer").AddComponent<Timer>();
        ownTimer.ScoreClockText = ownTimerScoreClock;
        SetPrivateField(rules, "timer", ownTimer);

        object result = GameRulesAdapterMethod("ReadScoreClockTextForHud", BindingFlags.Instance).Invoke(rules, null);

        Assert.AreSame(ownTimerScoreClock, result,
            "the adapter must read this GameRules instance's own resolved timer field, not the Timer.instance static.");
    }

    // ================ AUD-012 Phase 2b Slice 64: persistence-context binding ================
    //
    // MatchHudPresenter.SetScoreDisplayText's ~30 game-mode branches were previously gated behind a
    // live PlayerData.instance, deliberately left untested by the fixtures above (see their class
    // summary) because that persistence-layer coupling was Slice 62's explicit blocker. Slice 64 cuts
    // it, so these branches are now directly testable through a bound HighScoreSnapshot reader with no
    // live PlayerData/DBHelper required.

    [Test]
    public void ReadHighScoreSnapshotForHud_NoLivePlayerData_ReturnsNull()
    {
        PlayerData.instance = null;

        object result = GameRulesAdapterMethod("ReadHighScoreSnapshotForHud").Invoke(null, null);

        Assert.IsNull(result);
    }

    /// <summary>
    /// The primary protection against field-mapping drift: every active HUD-consumed PlayerData value
    /// set to a distinct number, so a wrong/omitted/transposed mapping in the production adapter shows
    /// up as a wrong number rather than a coincidental match.
    /// </summary>
    [Test]
    public void ReadHighScoreSnapshotForHud_LivePlayerData_MapsEveryHudConsumedProperty()
    {
        PlayerData data = SpawnPlayerDataWithoutAwake("player-data");
        SetPrivateField(data, "_totalPoints", 101f);
        SetPrivateField(data, "_totalPointsLockDown", 102f);
        SetPrivateField(data, "_threePointerMade", 103f);
        SetPrivateField(data, "_fourPointerMade", 104f);
        SetPrivateField(data, "_sevenPointerMade", 105f);
        SetPrivateField(data, "_totalDistance", 106f);
        SetPrivateField(data, "_makeThreePointersLowTime", 107f);
        SetPrivateField(data, "_makeFourPointersLowTime", 108f);
        SetPrivateField(data, "_makeSevenPointersLowTime", 109f);
        SetPrivateField(data, "_makeAllPointersLowTime", 110f);
        SetPrivateField(data, "_mostConsecutiveShots", 11);
        SetPrivateField(data, "_totalPointsBonus", 112f);
        SetPrivateField(data, "_threePointContestScore", 113f);
        SetPrivateField(data, "_fourPointContestScore", 114f);
        SetPrivateField(data, "_sevenPointContestScore", 115f);
        SetPrivateField(data, "_allPointContestScore", 116f);
        SetPrivateField(data, "_totalPointsByDistance", 117f);
        SetPrivateField(data, "_enemiesKilled", 18);
        SetPrivateField(data, "_enemiesKilledBattleRoyal", 19);
        SetPrivateField(data, "_enemiesKilledCageMatch", 20);
        SetPrivateField(data, "_longestShotMadeFreePlay", 121f);
        PlayerData.instance = data;

        MatchHudPresenter.HighScoreSnapshot snapshot =
            (MatchHudPresenter.HighScoreSnapshot)GameRulesAdapterMethod("ReadHighScoreSnapshotForHud").Invoke(null, null);

        Assert.That(snapshot.TotalPoints, Is.EqualTo(101f));
        Assert.That(snapshot.TotalPointsLockDown, Is.EqualTo(102f));
        Assert.That(snapshot.ThreePointerMade, Is.EqualTo(103f));
        Assert.That(snapshot.FourPointerMade, Is.EqualTo(104f));
        Assert.That(snapshot.SevenPointerMade, Is.EqualTo(105f));
        Assert.That(snapshot.TotalDistance, Is.EqualTo(106f));
        Assert.That(snapshot.MakeThreePointersLowTime, Is.EqualTo(107f));
        Assert.That(snapshot.MakeFourPointersLowTime, Is.EqualTo(108f));
        Assert.That(snapshot.MakeSevenPointersLowTime, Is.EqualTo(109f));
        Assert.That(snapshot.MakeAllPointersLowTime, Is.EqualTo(110f));
        Assert.That(snapshot.MostConsecutiveShots, Is.EqualTo(11));
        Assert.That(snapshot.TotalPointsBonus, Is.EqualTo(112f));
        Assert.That(snapshot.ThreePointContestScore, Is.EqualTo(113f));
        Assert.That(snapshot.FourPointContestScore, Is.EqualTo(114f));
        Assert.That(snapshot.SevenPointContestScore, Is.EqualTo(115f));
        Assert.That(snapshot.AllPointContestScore, Is.EqualTo(116f));
        Assert.That(snapshot.TotalPointsByDistance, Is.EqualTo(117f));
        Assert.That(snapshot.EnemiesKilled, Is.EqualTo(18));
        Assert.That(snapshot.EnemiesKilledBattleRoyal, Is.EqualTo(19));
        Assert.That(snapshot.EnemiesKilledCageMatch, Is.EqualTo(20));
        Assert.That(snapshot.LongestShotMadeFreePlay, Is.EqualTo(121f));
    }

    /// <summary>
    /// Proves the in-memory PlayerData mutation happens before the DBHelper dereference, using no live
    /// DBHelper at all: with DBHelper.instance left null, the DB call throws, but only after the
    /// mutation the production adapter performs first is already visible on the PlayerData instance.
    /// Per AGENTS.md's validation budget, this is the smallest existing mechanism that proves the
    /// ordering without a broad SQLite integration fixture for one call.
    /// </summary>
    [Test]
    public void PersistLongestShotMadeFreePlayForHud_MutatesPlayerDataBeforeReachingDBHelper()
    {
        PlayerData data = SpawnPlayerDataWithoutAwake("player-data-persist");
        SetPrivateField(data, "_longestShotMadeFreePlay", 5f);
        PlayerData.instance = data;
        DBHelper.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => GameRulesAdapterMethod("PersistLongestShotMadeFreePlayForHud").Invoke(null, new object[] { 12.5f }));

        Assert.That(thrown.InnerException, Is.TypeOf<NullReferenceException>(),
            "with no live DBHelper, the DB dereference itself must be what throws.");
        Assert.That(data.LongestShotMadeFreePlay, Is.EqualTo(12.5f),
            "the in-memory PlayerData mutation must occur before the DBHelper dereference, so it is "
            + "visible even though the DB call itself throws with no live DBHelper.");
    }

    [Test]
    public void SetScoreDisplayText_NoHighScoreSnapshot_RendersNothingWithoutThrowing()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.FreePlay);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        SetPrivateField(hud, "displayCurrentScoreText", SpawnText("current"));
        SetPrivateField(hud, "displayHighScoreText", SpawnText("high"));
        bool persistCalled = false;
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => SpawnText("clock"));
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => null,
            persistLongestShotMadeFreePlay: value => persistCalled = true);

        Assert.DoesNotThrow(() => hud.SetScoreDisplayText());

        Assert.That(((Text)GetPrivateField(hud, "displayHighScoreText")).text, Is.Empty,
            "no persistence-backed rendering must occur when the bound reader returns a null snapshot - "
            + "the exact prior 'no live PlayerData' behavior.");
        Assert.IsFalse(persistCalled, "the persistence action must not run when nothing was rendered.");
    }

    [Test]
    public void SetScoreDisplayText_SecondInvocation_ObservesReplacedSnapshot()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.ThreePointContest);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        SetPrivateField(hud, "displayHighScoreText", SpawnText("high"));
        Text clock = SpawnText("clock");
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => clock);
        MatchHudPresenter.HighScoreSnapshot snapshotA = MakeSnapshot(threePointContestScore: 10f);
        MatchHudPresenter.HighScoreSnapshot snapshotB = MakeSnapshot(threePointContestScore: 20f);
        int callCount = 0;
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => { callCount++; return callCount == 1 ? snapshotA : snapshotB; },
            persistLongestShotMadeFreePlay: value => { });

        hud.SetScoreDisplayText();
        Assert.That(((Text)GetPrivateField(hud, "displayHighScoreText")).text, Does.Contain("10"));

        hud.SetScoreDisplayText();
        Assert.That(((Text)GetPrivateField(hud, "displayHighScoreText")).text, Does.Contain("20"),
            "a later SetScoreDisplayText call must observe a replaced/refreshed snapshot.");
    }

    [Test]
    public void SetScoreDisplayText_OneInvocation_CallsHighScoreSnapshotReaderExactlyOnce()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.ThreePointContest);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        SetPrivateField(hud, "displayHighScoreText", SpawnText("high"));
        Text clock = SpawnText("clock");
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => clock);
        int readerCallCount = 0;
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => { readerCallCount++; return MakeSnapshot(threePointContestScore: 5f); },
            persistLongestShotMadeFreePlay: value => { });

        hud.SetScoreDisplayText();

        Assert.That(readerCallCount, Is.EqualTo(1),
            "one SetScoreDisplayText call must resolve exactly one snapshot, not one per mode branch.");
    }

    // ==================== representative rendering parity across distinct fields ====================

    private void AssertHighScoreRendered(int modeId, MatchHudPresenter.HighScoreSnapshot snapshot, string expectedSubstring)
    {
        BasketBall.instance = SpawnLiveBasketBall("basketball");
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", modeId);
        // FreePlay's branch calls GameStats.getExperienceGainedFromSession(), which logs an error (and
        // fails the test under Unity's default LogAssert behavior) unless match rules are bound -
        // same fixture convention Level5BasketballShotPipelineTests uses.
        GameStats gameStats1 = Spawn("stats-holder").AddComponent<GameStats>();
        gameStats1.BindMatchRules(new ResolvedMatchRules());
        SetPrivateField(hud, "gameStats1", gameStats1);
        SetPrivateField(hud, "displayCurrentScoreText", SpawnText("current"));
        SetPrivateField(hud, "displayHighScoreText", SpawnText("high"));
        Text clock = SpawnText("clock");
        PlayerIdentifier player = MakePlayer("primary", 0, "Primary");
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier> { player },
            primaryPlayerReader: () => player,
            firstRegisteredPlayerReader: () => player,
            scoreClockTextReader: () => clock);
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => snapshot,
            persistLongestShotMadeFreePlay: value => { });

        hud.SetScoreDisplayText();

        Assert.That(((Text)GetPrivateField(hud, "displayHighScoreText")).text, Does.Contain(expectedSubstring));
    }

    [Test]
    public void SetScoreDisplayText_TotalPointsMode_RendersSnapshotTotalPoints()
    {
        AssertHighScoreRendered(Modes.TotalPoints, MakeSnapshot(totalPoints: 42f), "42");
    }

    [Test]
    public void SetScoreDisplayText_ConsecutiveShotsMode_RendersSnapshotMostConsecutiveShots()
    {
        AssertHighScoreRendered(Modes.ConsecutiveShots, MakeSnapshot(mostConsecutiveShots: 8), "8");
    }

    [Test]
    public void SetScoreDisplayText_SpotUp3sMode_RendersSnapshotMakeThreePointersLowTime()
    {
        AssertHighScoreRendered(Modes.SpotUp3s, MakeSnapshot(makeThreePointersLowTime: 12.5f), "12.5");
    }

    [Test]
    public void SetScoreDisplayText_ThreePointContestMode_RendersSnapshotThreePointContestScore()
    {
        AssertHighScoreRendered(Modes.ThreePointContest, MakeSnapshot(threePointContestScore: 7f), "7");
    }

    [Test]
    public void SetScoreDisplayText_BashUpSomeNerdsMode_RendersSnapshotEnemiesKilled()
    {
        AssertHighScoreRendered(Modes.BashUpSomeNerds, MakeSnapshot(enemiesKilled: 9), "9");
    }

    [Test]
    public void SetScoreDisplayText_FreePlayMode_RendersSnapshotLongestShotMadeFreePlay()
    {
        AssertHighScoreRendered(Modes.FreePlay, MakeSnapshot(longestShotMadeFreePlay: 3f), "3.00");
    }

    // ==================== longest-shot persistence decision (FreePlay/ArcadeMode/mode 0) ====================

    private void AssertLongestShotPersistence(float currentLongestShot, float snapshotLongestShot, bool expectPersistCall)
    {
        BasketBall.instance = SpawnLiveBasketBall("basketball");
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.FreePlay);
        GameStats stats = Spawn("stats-holder").AddComponent<GameStats>();
        stats.Stats.LongestShotMade = currentLongestShot;
        // See AssertHighScoreRendered's comment: FreePlay's branch needs bound match rules so
        // getExperienceGainedFromSession() does not log an error and fail the test.
        stats.BindMatchRules(new ResolvedMatchRules());
        SetPrivateField(hud, "gameStats1", stats);
        SetPrivateField(hud, "displayCurrentScoreText", SpawnText("current"));
        SetPrivateField(hud, "displayHighScoreText", SpawnText("high"));
        Text clock = SpawnText("clock");
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => clock);
        int persistCallCount = 0;
        float persistedValue = float.NaN;
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => MakeSnapshot(longestShotMadeFreePlay: snapshotLongestShot),
            persistLongestShotMadeFreePlay: value => { persistCallCount++; persistedValue = value; });

        hud.SetScoreDisplayText();

        Assert.That(persistCallCount, Is.EqualTo(expectPersistCall ? 1 : 0));
        if (expectPersistCall)
        {
            Assert.That(persistedValue, Is.EqualTo(currentLongestShot),
                "the persistence action must receive the exact current match longest-shot value.");
        }
    }

    [Test]
    public void SetScoreDisplayText_FreePlayMode_LongestShotSmallerThanSnapshot_DoesNotPersist()
    {
        AssertLongestShotPersistence(currentLongestShot: 5f, snapshotLongestShot: 10f, expectPersistCall: false);
    }

    [Test]
    public void SetScoreDisplayText_FreePlayMode_LongestShotEqualToSnapshot_DoesNotPersist()
    {
        AssertLongestShotPersistence(currentLongestShot: 10f, snapshotLongestShot: 10f, expectPersistCall: false);
    }

    [Test]
    public void SetScoreDisplayText_FreePlayMode_LongestShotGreaterThanSnapshot_PersistsExactValueExactlyOnce()
    {
        AssertLongestShotPersistence(currentLongestShot: 15f, snapshotLongestShot: 10f, expectPersistCall: true);
    }

    /// <summary>
    /// Protects the subtle ordering the spec calls out explicitly: the current invocation must render
    /// from the snapshot value it read at the start of the call (the pre-write record), and the
    /// persistence action must run only afterward - never causing this same render to show the value
    /// it is about to write.
    /// </summary>
    [Test]
    public void SetScoreDisplayText_LongestShotBeatsRecord_RendersPreWriteSnapshotValueBeforePersisting()
    {
        BasketBall.instance = SpawnLiveBasketBall("basketball");
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.FreePlay);
        GameStats stats = Spawn("stats-holder").AddComponent<GameStats>();
        stats.Stats.LongestShotMade = 15f;
        stats.BindMatchRules(new ResolvedMatchRules());
        SetPrivateField(hud, "gameStats1", stats);
        SetPrivateField(hud, "displayCurrentScoreText", SpawnText("current"));
        Text highScoreText = SpawnText("high");
        SetPrivateField(hud, "displayHighScoreText", highScoreText);
        Text clock = SpawnText("clock");
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => clock);
        List<string> events = new List<string>();
        hud.BindPersistenceContext(
            highScoreSnapshotReader: () => MakeSnapshot(longestShotMadeFreePlay: 10f),
            persistLongestShotMadeFreePlay: value => events.Add("persisted:" + value));

        hud.SetScoreDisplayText();

        Assert.That(highScoreText.text, Does.Contain("10.00"),
            "the render must use the snapshot value read at the start of the call, not a value the "
            + "persistence action is about to write.");
        Assert.That(events, Is.EqualTo(new[] { "persisted:15" }),
            "the persistence action must run after rendering, exactly once, with the exact current "
            + "longest-shot value.");
    }
}
