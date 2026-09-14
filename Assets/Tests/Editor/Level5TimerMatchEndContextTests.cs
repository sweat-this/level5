using System;
using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 60: <see cref="Timer"/> no longer reads <c>GameRules.instance</c>/
/// <c>GameLevelManager.instance.Player1</c> itself - <see cref="Timer.Update"/> and
/// <see cref="Timer.ReportTimeExpired"/> (private, driven indirectly here) only consult whatever
/// delegates <see cref="Timer.BindMatchEndContext"/> was called with. Mirrors the shape of
/// <c>Level5SpawnCoordinatorKilledOnIdleCompositionTests</c>: composition-forwarding tests driving the
/// real <c>Update()</c> method via reflection, then separate production-adapter tests against live
/// <see cref="GameRules"/>/<see cref="GameLevelManager"/> singletons. Does not re-prove
/// <see cref="Level5.Core.Match.MatchEndConditions.TimeExpired"/>'s own arithmetic - that is already
/// covered by <c>Level5MatchLifecycleTests</c> as a pure function; this fixture only proves the new
/// delegate plumbing around it.
/// </summary>
public class Level5TimerMatchEndContextTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private Timer savedTimerInstance;
    private GameRules savedGameRulesInstance;
    private GameLevelManager savedGameLevelManagerInstance;

    [SetUp]
    public void SetUp()
    {
        // Saved/restored, not reset to null, so this fixture does not leak state into whatever else
        // runs in the same Editor test session - mirrors
        // Level5SpawnCoordinatorAnimationEventsCompositionTests' handling of GameLevelManager.instance.
        savedTimerInstance = Timer.instance;
        savedGameRulesInstance = GameRules.instance;
        savedGameLevelManagerInstance = GameLevelManager.instance;
        Timer.instance = null;
    }

    [TearDown]
    public void TearDown()
    {
        Timer.instance = savedTimerInstance;
        GameRules.instance = savedGameRulesInstance;
        GameLevelManager.instance = savedGameLevelManagerInstance;

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

    private Timer MakeTimer()
    {
        // Timer.Awake() self-assigns the static singleton and self-destroys a duplicate; SetUp clears
        // the static first so each test gets a fresh instance.
        return Spawn("timer").AddComponent<Timer>();
    }

    /// <summary>Creates a GameObject with a GameLevelManager component without ever running its Awake() -
    /// a GameObject that starts inactive defers every component's Awake() until it is activated, and
    /// these tests never activate it. Awake() would otherwise reach for scene spawn points and
    /// MatchRuntime state this fixture never sets up. Mirrors
    /// Level5SpawnCoordinatorAnimationEventsCompositionTests.SpawnManagerWithoutAwake.</summary>
    private GameLevelManager SpawnManagerWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<GameLevelManager>();
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

    private static void InvokeUpdate(Timer timer)
    {
        MethodInfo update = typeof(Timer).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(update, "Timer must declare Update()");
        update.Invoke(timer, null);
    }

    /// <summary>
    /// Arranges the gate at the bottom of <see cref="Timer.Update"/> so <see cref="Timer.ReportTimeExpired"/>
    /// fires deterministically regardless of <c>Time.deltaTime</c> (unreliable in a batched EditMode
    /// run): pre-set <c>timeRemaining</c> below zero and leave <c>modeRequiresCountDown</c>/
    /// <c>modeRequiresCounter</c> both false, so neither recompute branch inside <c>Update()</c>
    /// overwrites it.
    /// </summary>
    private static void ArrangeToReachReportTimeExpired(Timer timer)
    {
        SetPrivateField(timer, "timeRemaining", -1f);
        SetPrivateField(timer, "modeRequiresCountDown", false);
        SetPrivateField(timer, "modeRequiresCounter", false);
        SetPrivateField(timer, "timerEnabled", true);
    }

    // ==================== Update()'s top-level gate ====================

    [Test]
    public void Update_NoBindingAtAll_ClockNeverCounts()
    {
        Timer timer = MakeTimer();

        InvokeUpdate(timer);

        Assert.That(timer.CurrentTime, Is.EqualTo(0f),
            "an unbound Timer must behave exactly like the former GameRules.instance == null guard: the clock never counts.");
    }

    [Test]
    public void Update_BoundGameOverReaderReturnsNull_ClockNeverCounts()
    {
        Timer timer = MakeTimer();
        bool primaryPlayerReaderCalled = false;
        timer.BindMatchEndContext(
            gameOverReader: () => null,
            gameModeRequiresConsecutiveShotsReader: () => false,
            primaryPlayerReader: () => { primaryPlayerReaderCalled = true; return null; },
            requestMatchEnd: reason => { });

        InvokeUpdate(timer);

        Assert.That(timer.CurrentTime, Is.EqualTo(0f),
            "a null gameOverReader result means 'no live GameRules' and must idle the whole clock, exactly as before.");
        Assert.IsFalse(primaryPlayerReaderCalled,
            "the rest of Update() (including ReportTimeExpired) must not run when there is no live GameRules to ask.");
    }

    [Test]
    public void Update_BoundGameOverReaderReturnsFalse_ReachesReportTimeExpiredsPlayerLookup()
    {
        Timer timer = MakeTimer();
        ArrangeToReachReportTimeExpired(timer);
        bool primaryPlayerReaderCalled = false;
        bool requestMatchEndCalled = false;
        timer.BindMatchEndContext(
            gameOverReader: () => false,
            gameModeRequiresConsecutiveShotsReader: () => false,
            primaryPlayerReader: () => { primaryPlayerReaderCalled = true; return null; },
            requestMatchEnd: reason => { requestMatchEndCalled = true; });

        InvokeUpdate(timer);

        Assert.IsTrue(primaryPlayerReaderCalled,
            "a non-null gameOverReader result must let Update() proceed to ReportTimeExpired's player lookup.");
        Assert.IsFalse(requestMatchEndCalled,
            "ReportTimeExpired must still return without reporting when the bound primaryPlayerReader has no player to give it - unchanged from the former GameLevelManager.instance.Player1 == null guard.");
    }

    [Test]
    public void ReportTimeExpired_ExpiredMatch_ForwardsExactReasonToTheBoundRequestMatchEnd()
    {
        Timer timer = MakeTimer();
        ArrangeToReachReportTimeExpired(timer);

        GameObject playerGo = Spawn("expired-player");
        PlayerIdentifier player = playerGo.AddComponent<PlayerIdentifier>();
        player.playerController = playerGo.AddComponent<PlayerController>();
        player.playerController.Grounded = true;
        player.basketBallState = playerGo.AddComponent<BasketBallState>();
        player.basketBallState.Thrown = false;
        player.gameStats = playerGo.AddComponent<GameStats>();

        MatchEndReason? reported = null;
        timer.BindMatchEndContext(
            gameOverReader: () => false,
            gameModeRequiresConsecutiveShotsReader: () => false,
            primaryPlayerReader: () => player,
            requestMatchEnd: reason => reported = reason);

        InvokeUpdate(timer);

        Assert.IsTrue(reported.HasValue, "an unthrown ball with a grounded player must report the match as expired.");
        Assert.That(reported.Value.Cause, Is.EqualTo(MatchEndCause.TimeExpired));
    }

    [Test]
    public void ReportTimeExpired_ConsecutiveShotsModeReadsTheBoundReaderNotAHardcodedFalse()
    {
        Timer timer = MakeTimer();
        ArrangeToReachReportTimeExpired(timer);

        GameObject playerGo = Spawn("streak-player");
        PlayerIdentifier player = playerGo.AddComponent<PlayerIdentifier>();
        player.playerController = playerGo.AddComponent<PlayerController>();
        player.playerController.Grounded = false;
        player.basketBallState = playerGo.AddComponent<BasketBallState>();
        player.basketBallState.Thrown = true;
        player.gameStats = playerGo.AddComponent<GameStats>();
        player.gameStats.Stats.ConsecutiveShotsMade = 1;

        MatchEndReason? reported = null;
        timer.BindMatchEndContext(
            gameOverReader: () => false,
            gameModeRequiresConsecutiveShotsReader: () => true,
            primaryPlayerReader: () => player,
            requestMatchEnd: reason => reported = reason);

        InvokeUpdate(timer);

        // requiresConsecutiveShots=true routes TimeExpired through the streak branch
        // (consecutiveShotsMade < ConsecutiveShotsToPlayOn), ignoring ballThrown/playerGrounded
        // entirely - proving gameModeRequiresConsecutiveShotsReader's result, not a stray hard-coded
        // false, is what reaches MatchEndConditions.TimeExpired.
        Assert.IsTrue(reported.HasValue, "a streak of 1 (< ConsecutiveShotsToPlayOn) must still end the match even with the ball thrown and the player airborne.");
        Assert.That(reported.Value.Detail, Is.Not.Empty, "the consecutive-shots reason carries a detail string, unlike the plain TimeExpired reason.");
    }

    // ================ production adapters: GameLevelManager's Timer composition ================

    private static MethodInfo AdapterMethod(string name)
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"GameLevelManager.{name} must exist as a production Timer adapter");
        return method;
    }

    [Test]
    public void ReadGameOverForTimer_NoLiveGameRules_ReturnsNull()
    {
        GameRules.instance = null;

        object result = AdapterMethod("ReadGameOverForTimer").Invoke(null, null);

        Assert.IsNull(result, "an absent GameRules.instance must resolve to null, the same signal Update() treats as 'idle the clock'.");
    }

    [Test]
    public void ReadGameOverForTimer_LiveGameRules_ForwardsCurrentGameOverValue()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;
        rules.GameOver = true;

        object result = AdapterMethod("ReadGameOverForTimer").Invoke(null, null);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void RequestMatchEndForTimer_LiveGameRules_ForwardsToRequestEnd()
    {
        // GameOver's setter also writes GameLevelManager.instance.GameOver when that singleton is
        // live; nulled here so this assertion is deterministic regardless of test execution order.
        GameLevelManager.instance = null;
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;

        AdapterMethod("RequestMatchEndForTimer").Invoke(null, new object[] { MatchEndReason.TimeExpired });

        Assert.IsTrue(rules.GameOver, "RequestEnd's own GameOver side effect must have run, proving the adapter reached the live instance.");
    }

    [Test]
    public void RequestMatchEndForTimer_AbsentGameRulesInstance_PreservesExistingFailureSemantics()
    {
        GameRules.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => AdapterMethod("RequestMatchEndForTimer").Invoke(null, new object[] { MatchEndReason.TimeExpired }),
            "an absent GameRules.instance must still throw exactly as the former direct RequestEnd call did - this slice must not silently make it safe.");
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    [Test]
    public void ReadPrimaryPlayerForTimer_NoLiveGameLevelManager_ReturnsNull()
    {
        GameLevelManager.instance = null;

        object result = AdapterMethod("ReadPrimaryPlayerForTimer").Invoke(null, null);

        Assert.IsNull(result);
    }

    [Test]
    public void ReadPrimaryPlayerForTimer_LiveGameLevelManager_ForwardsPlayer1()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;

        GameObject playerGo = Spawn("registered-player");
        PlayerIdentifier player = playerGo.AddComponent<PlayerIdentifier>();
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = AdapterMethod("ReadPrimaryPlayerForTimer").Invoke(null, null);

        Assert.AreSame(manager.Player1, result,
            "the adapter must forward the current GameLevelManager.instance.Player1 exactly, live.");
    }
}
