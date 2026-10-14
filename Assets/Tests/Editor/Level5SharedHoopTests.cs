using System.Collections.Generic;
using System.Reflection;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Simulated callbacks on the production paired triggers; no physics or player data.</summary>
public class Level5SharedHoopTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private BasketBallShotMade upper;
    private BasketBallShotMadeCollision lower;
    private MatchLifecycle lifecycle;

    [SetUp]
    public void SetUp()
    {
        lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => lifecycle.IsPlaying);
        lower = Spawn("basketBallMadeShot2").AddComponent<BasketBallShotMadeCollision>();
        SetField(lower, "basketBallShotMade", upper);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
            if (go != null) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private IBasketballRuntime Ball(bool cpu = false)
    {
        GameObject go = Spawn(cpu ? "cpu-ball" : "human-ball");
        go.tag = cpu ? "basketballAuto" : "basketball";
        BasketBallState state = go.AddComponent<BasketBallState>();
        GameStats stats = go.AddComponent<GameStats>();
        go.AddComponent<SphereCollider>();
        Component ball = cpu ? (Component)go.AddComponent<BasketBallAuto>() : go.AddComponent<BasketBall>();
        // EditMode does not run Start; supply the same component references it would initialize.
        SetField(ball, "basketBallState", state);
        SetField(ball, "gameStats", stats);
        SetField(ball, "participantId", cpu ? 2 : 1);
        SetField(ball, "isCpu", cpu);
        IBasketballRuntime runtime = (IBasketballRuntime)ball;
        Begin(runtime);
        return runtime;
    }

    private static void Begin(IBasketballRuntime ball, bool classified = true)
    {
        BasketballShotPipeline.ApplyShotAttempt(ball, false, classified, false, false);
    }

    private static Collider ColliderOf(IBasketballRuntime ball) => ((Component)ball).GetComponent<Collider>();
    private static void Trigger(Component component, Collider collider)
    {
        component.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, new object[] { collider });
    }
    private void Upper(IBasketballRuntime ball) => Trigger(upper, ColliderOf(ball));
    private void Lower(IBasketballRuntime ball) => Trigger(lower, ColliderOf(ball));

    private sealed class MoneyBallState : IMoneyBallState
    {
        public bool MoneyBallEnabled { get; set; }
    }

    private BasketBallShotMarker ContestMarker(ResolvedMatchRules rules, int quota = 5)
    {
        Object.DestroyImmediate(upper.gameObject);
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        upper.BindMatchContext(rules, GameModeId.ThreePointContest, () => lifecycle.IsPlaying);
        SetField(lower, "basketBallShotMade", upper);
        var marker = Spawn("shared-marker").AddComponent<BasketBallShotMarker>();
        marker.MaxShotAttempt = quota;
        marker.MarkerEnabled = true;
        return marker;
    }

    private static ResolvedMatchRules ContestRules(bool allRanges = false) => new ResolvedMatchRules(
        shotRule: allRanges ? ShotRule.ThreePoint | ShotRule.FourPoint | ShotRule.AllRanges : ShotRule.ThreePoint,
        shotMarkers: ShotMarkerRequirement.ThreePoint);

    private static void MarkerAttempt(IBasketballRuntime ball, BasketBallShotMarker marker,
        ResolvedMatchRules rules, bool moneyBall = false)
    {
        ball.State.EnterShotMarker(marker);
        Begin(ball);
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(ball,
            new MoneyBallState { MoneyBallEnabled = moneyBall }, rules);
    }

    private void Make(IBasketballRuntime ball)
    {
        Upper(ball);
        Lower(ball);
        Upper(ball);
        Lower(ball); // Repeated callbacks cannot add points, makes or events.
    }

    private static void TraceMarker(BasketBallShotMarker marker, int events, params IBasketballRuntime[] balls)
    {
        TestContext.WriteLine($"marker attempts={marker.ShotAttempt} makes={marker.ShotMade} enabled={marker.MarkerEnabled} events={events}");
        foreach (var ball in balls)
            TestContext.WriteLine($"cpu={ball.IsCpu} points={ball.Stats.TotalPoints} moneyAttempts={ball.Stats.Stats.MoneyBallAttempts} moneyMade={ball.Stats.Stats.MoneyBallMade} makes={ball.Stats.Stats.ShotMade}");
    }

    [Test]
    public void QuotaReachingAttemptCountsBeforeMakeOrMiss(
        [Values(3, 4, 5, 7)] int quota, [Values] bool cpu, [Values] bool missed, [Values] bool activeMoneyBall)
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules, quota);
        var human = Ball();
        var final = cpu ? Ball(true) : human;
        for (int i = 1; i < quota; i++) MarkerAttempt(human, marker, rules);
        int earlierAttempts = human.Stats.Stats.MoneyBallAttempts;
        MarkerAttempt(final, marker, rules, activeMoneyBall);
        bool capturedFinal = final.State.IsFinalMarkerAttempt;
        int attemptsBeforeResolution = final.Stats.Stats.MoneyBallAttempts;
        if (!missed) Make(final);
        TraceMarker(marker, missed ? 0 : 1, human, final);
        Assert.That(earlierAttempts, Is.Zero, "Only the authored final attempt is a marker money ball");
        Assert.That(capturedFinal, Is.True);
        Assert.That(attemptsBeforeResolution, Is.EqualTo(activeMoneyBall ? 2 : 1));
        Assert.That(final.Stats.Stats.MoneyBallAttempts, Is.EqualTo(attemptsBeforeResolution));
        Assert.That(final.Stats.TotalPoints, Is.EqualTo(missed ? 0 : 6));
        Assert.That(final.Stats.Stats.MoneyBallMade, Is.EqualTo(missed ? 0 : activeMoneyBall ? 2 : 1));
        Assert.That(marker.ShotMade, Is.EqualTo(missed || activeMoneyBall ? 0 : 1),
            "Active money ball retains the existing MaxShotMade assignment (zero in this fixture)");
        if (cpu) Assert.That(human.Stats.Stats.MoneyBallAttempts, Is.Zero);
        Begin(final); // A miss or accepted make cannot lend eligibility to a new attempt.
        Assert.That(final.State.IsFinalMarkerAttempt, Is.False);
    }

    [Test]
    public void DisabledMarkerDoesNotCreditFinalAttemptButActiveMoneyBallRemainsIndependent(
        [Values(4, 5)] int quota, [Values] bool activeMoneyBall)
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules, quota);
        marker.MarkerEnabled = false;
        var ball = Ball();
        for (int i = 1; i < quota; i++) MarkerAttempt(ball, marker, rules);
        MarkerAttempt(ball, marker, rules, activeMoneyBall);
        Make(ball);
        TraceMarker(marker, 1, ball);
        Assert.That(ball.Stats.Stats.MoneyBallAttempts, Is.EqualTo(activeMoneyBall ? 1 : 0));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.EqualTo(activeMoneyBall ? 1 : 0));
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
    }

    [Test]
    public void CompletedFourAttemptMarkerCannotCreditLaterFifthOrSixthRegistration([Values] bool cpu)
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules, 4);
        var ball = Ball(cpu);
        for (int i = 0; i < 4; i++) MarkerAttempt(ball, marker, rules);
        int finalAttempts = ball.Stats.Stats.MoneyBallAttempts;
        marker.MarkerEnabled = false; // Real completion readiness is exercised in PlayMode.
        MarkerAttempt(ball, marker, rules);
        MarkerAttempt(ball, marker, rules);
        Make(ball);
        TraceMarker(marker, 1, ball);
        Assert.That(finalAttempts, Is.EqualTo(1));
        Assert.That(ball.Stats.Stats.MoneyBallAttempts, Is.EqualTo(finalAttempts));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.Zero);
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
    }

    [Test]
    public void FinalMarkerBonusAndAccountingKeepOwnershipRegardlessOfMakeOrderOrExtraAttempt(
        [Values(4, 5)] int quota, [Values] bool finalFirst, [Values] bool extraAttempt, [Values] bool allRanges)
    {
        var rules = ContestRules(allRanges); // Mode 18 authors combined Three/Four/AllRanges flags.
        var marker = ContestMarker(rules, quota);
        var penultimate = Ball();
        var final = Ball(true);
        for (int i = 0; i < quota - 2; i++) MarkerAttempt(penultimate, marker, rules);
        MarkerAttempt(penultimate, marker, rules);
        MarkerAttempt(final, marker, rules);
        IBasketballRuntime extra = null;
        if (extraAttempt)
        {
            extra = Ball();
            MarkerAttempt(extra, marker, rules);
        }
        int events = 0;
        upper.ShotResolved += _ => events++;
        Make(finalFirst ? final : penultimate);
        Make(finalFirst ? penultimate : final);
        if (extraAttempt) Make(extra);
        TraceMarker(marker, events, penultimate, final);
        if (extraAttempt) TraceMarker(marker, events, extra);
        Assert.That(penultimate.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(final.Stats.TotalPoints, Is.EqualTo(6));
        Assert.That(penultimate.Stats.Stats.MoneyBallAttempts, Is.Zero);
        Assert.That(penultimate.Stats.Stats.MoneyBallMade, Is.Zero);
        Assert.That(final.Stats.Stats.MoneyBallAttempts, Is.EqualTo(1));
        Assert.That(final.Stats.Stats.MoneyBallMade, Is.EqualTo(1));
        if (extraAttempt)
        {
            Assert.That(extra.Stats.TotalPoints, Is.EqualTo(3));
            Assert.That(extra.Stats.Stats.MoneyBallAttempts, Is.Zero);
            Assert.That(extra.Stats.Stats.MoneyBallMade, Is.Zero);
        }
        Assert.That(marker.ShotAttempt, Is.EqualTo(quota + (extraAttempt ? 1 : 0)));
        Assert.That(marker.ShotMade, Is.EqualTo(extraAttempt ? 3 : 2));
        Assert.That(events, Is.EqualTo(extraAttempt ? 3 : 2));
        Assert.That(marker.MarkerEnabled, Is.True, "Scoring must not take ownership of completion");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FinalMarkerBonusDoesNotSurviveMissAndNextAttemptOnAnotherMarker(bool cpu)
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules);
        var ball = Ball(cpu);
        for (int i = 0; i < 5; i++) MarkerAttempt(ball, marker, rules);
        Upper(ball); // Fifth misses; its upper crossing must not authorize the next generation.
        var next = Spawn("next-marker").AddComponent<BasketBallShotMarker>();
        next.MaxShotAttempt = 5;
        next.MarkerEnabled = true;
        MarkerAttempt(ball, next, rules);
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
        int events = 0;
        upper.ShotResolved += _ => events++;
        Make(ball);
        TraceMarker(marker, events, ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(ball.Stats.Stats.MoneyBallAttempts, Is.EqualTo(1));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.Zero);
        Assert.That(marker.ShotMade, Is.Zero);
        Assert.That(next.ShotMade, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(1));
    }

    [TestCase(false, 5, 6, 1, 1)]
    [TestCase(true, 5, 6, 2, 2)]
    [TestCase(false, 3, 6, 1, 1)]
    public void UncontestedFinalMarkerRetainsMoneyBallCreditsAndAuthoredQuotaPolicy(
        bool moneyBall, int quota, int points, int moneyAttempts, int moneyMade)
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules, quota);
        var ball = Ball();
        for (int i = 1; i < quota; i++) MarkerAttempt(ball, marker, rules);
        MarkerAttempt(ball, marker, rules, moneyBall);
        int events = 0;
        upper.ShotResolved += _ => events++;
        Make(ball);
        TraceMarker(marker, events, ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(points));
        Assert.That(ball.Stats.Stats.MoneyBallAttempts, Is.EqualTo(moneyAttempts));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.EqualTo(moneyMade));
        Assert.That(ball.State.OnShootShotMarker, Is.Null);
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
        Assert.That(events, Is.EqualTo(1));
    }

    [TestCase(ShotRule.Any, true, 3)]
    [TestCase(ShotRule.AllRanges, true, 3)]
    [TestCase(ShotRule.ThreePoint, false, 0)]
    public void FinalMarkerSnapshotPreservesModeAndEnabledMarkerGates(ShotRule shotRule, bool enabled, int points)
    {
        var rules = new ResolvedMatchRules(shotRule: shotRule, shotMarkers: ShotMarkerRequirement.ThreePoint);
        var marker = ContestMarker(rules);
        var ball = Ball();
        for (int i = 0; i < 5; i++) MarkerAttempt(ball, marker, rules);
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.True);
        marker.MarkerEnabled = enabled;
        Make(ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(points));
        Assert.That(ball.Stats.Stats.MoneyBallAttempts, Is.EqualTo(shotRule == ShotRule.ThreePoint ? 1 : 0));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.Zero);
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
    }

    [Test]
    public void MissingRegistrationCannotBorrowFinalBonusFromSharedMarkerCount()
    {
        var rules = ContestRules();
        var marker = ContestMarker(rules);
        var ball = Ball();
        for (int i = 0; i < 5; i++) MarkerAttempt(ball, marker, rules);
        Begin(ball);
        ball.State.CaptureShotMarkerForAttempt(); // Legacy/incomplete snapshot with no registration.
        Make(ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(ball.Stats.Stats.MoneyBallMade, Is.Zero);
    }

    [Test]
    public void MatchingCrossingsScoreOnceAndPublishOnce()
    {
        IBasketballRuntime a = Ball();
        int events = 0;
        upper.ShotResolved += _ => events++;
        Upper(a);
        Lower(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
        Assert.That(a.Stats.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void OneBallsUpperCannotAuthorizeAnotherBallsLower()
    {
        IBasketballRuntime a = Ball();
        IBasketballRuntime b = Ball(true);
        Upper(a);
        Lower(b);
        Assert.That(b.Stats.Stats.ShotMade, Is.Zero);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DistinctBallsScoreWithoutAnyFrameReset(bool interleaved)
    {
        IBasketballRuntime a = Ball();
        IBasketballRuntime b = Ball(true);
        List<MadeShotResult> results = new List<MadeShotResult>();
        upper.ShotResolved += results.Add;
        Upper(a);
        if (interleaved) Upper(b);
        Lower(a);
        if (!interleaved) Upper(b);
        Lower(b);
        Assert.That(a.Stats.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(b.Stats.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0].PlayerId, Is.EqualTo(1));
        Assert.That(results[0].IsCpu, Is.False);
        Assert.That(results[1].PlayerId, Is.EqualTo(2));
        Assert.That(results[1].IsCpu, Is.True);
    }

    [Test]
    public void LowerBeforeUpperDoesNotScore()
    {
        IBasketballRuntime a = Ball();
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.Zero);
        Upper(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.Zero);
    }

    [Test]
    public void ReboundAndChangedStatCounterCannotScoreTheSameAttemptAgain()
    {
        IBasketballRuntime a = Ball();
        int events = 0;
        upper.ShotResolved += _ => events++;
        Upper(a);
        Lower(a);
        // A rendered frame and unrelated stat accounting must not re-arm the shot.
        upper.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(upper, null);
        a.Stats.Stats.ShotAttempt++;
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void NewAttemptCanScoreButCannotUseThePreviousAttemptsUpper()
    {
        IBasketballRuntime a = Ball();
        Upper(a);
        Begin(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.Zero);
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
        Begin(a);
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(2));
    }

    [Test]
    public void UnclassifiedAttemptsStillHaveDistinctCrossingIdentity()
    {
        IBasketballRuntime a = Ball();
        int events = 0;
        upper.ShotResolved += _ => events++;
        Begin(a, false);
        Upper(a);
        Begin(a, false);
        Lower(a);
        Assert.That(events, Is.Zero);
        Upper(a);
        Lower(a);
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void MissingRuntimeFailsClosedAtBothTriggers()
    {
        GameObject go = Spawn("unbound-ball");
        go.tag = "basketball";
        Collider collider = go.AddComponent<SphereCollider>();
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no basketball runtime binding"));
        Trigger(upper, collider);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no basketball runtime binding"));
        Trigger(lower, collider);
    }

    [Test]
    public void ReplacementHoopDoesNotInheritPendingCrossings()
    {
        IBasketballRuntime a = Ball();
        Upper(a);
        Object.DestroyImmediate(upper.gameObject);
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => lifecycle.IsPlaying);
        SetField(lower, "basketBallShotMade", upper);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.Zero);
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
    }

    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, false, false, true)]
    [TestCase(false, false, false, false)]
    [TestCase(true, false, true, true)]
    public void EveryAttemptShapeAdvancesIdentityOnce(bool two, bool three, bool four, bool seven)
    {
        IBasketballRuntime a = Ball();
        ulong before = a.State.ShotAttemptGeneration;
        BasketballShotPipeline.ApplyShotAttempt(a, two, three, four, seven);
        Assert.That(a.State.ShotAttemptGeneration, Is.EqualTo(before + 1));
        a.State.ResetShotAttemptSnapshot();
        Assert.That(a.State.ShotAttemptGeneration, Is.EqualTo(before + 1));
    }

    [Test]
    public void NullOrIncompleteRuntimeAndMissingUpperBindingFailClosed()
    {
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("invalid basketball runtime binding"));
        Assert.DoesNotThrow(() => upper.shotMade(null));
        IBasketballRuntime a = Ball();
        SetField(a, "gameStats", null);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("invalid basketball runtime binding"));
        Upper(a);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("invalid basketball runtime binding"));
        Lower(a);
        SetField(lower, "basketBallShotMade", null);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no bound upper made-shot trigger"));
        Lower(a);
    }

    [Test]
    public void CompatibilityFlagsCannotAuthorizeAMake()
    {
        IBasketballRuntime a = Ball();
        upper.ShotMade1 = true;
        upper.ShotMade2 = true;
        Lower(a);
        Assert.That(a.Stats.Stats.TotalPoints, Is.Zero);
    }

    [Test]
    public void MatchBindingDiscardsCrossingsObservedBeforeComposition()
    {
        IBasketballRuntime a = Ball();
        Object.DestroyImmediate(upper.gameObject);
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        SetField(lower, "basketBallShotMade", upper);
        Upper(a);
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => lifecycle.IsPlaying);
        Lower(a);
        Assert.That(a.Stats.Stats.TotalPoints, Is.Zero);
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.TotalPoints, Is.EqualTo(3));
    }

    [Test]
    public void NeverLaunchedBallCannotScore()
    {
        IBasketballRuntime a = Ball();
        BasketBallState fresh = Spawn("fresh-state").AddComponent<BasketBallState>();
        SetField(a, "basketBallState", fresh);
        Upper(a);
        Lower(a);
        Assert.That(a.Stats.Stats.TotalPoints, Is.Zero);
    }

    [Test]
    public void DisablingHoopDropsPendingCrossingsAndRetainsConsumedAttempts()
    {
        IBasketballRuntime a = Ball();
        IBasketballRuntime b = Ball(true);
        Upper(a);
        Lower(a);
        Upper(b);
        // Simulate lifecycle in EditMode, where ordinary MonoBehaviour callbacks do not run.
        typeof(BasketBallShotMade).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(upper, null);
        Upper(a);
        Lower(a);
        Lower(b);
        Assert.That(a.Stats.Stats.ShotMade, Is.EqualTo(1));
        Assert.That(b.Stats.Stats.ShotMade, Is.Zero);
        Upper(b);
        Lower(b);
        Assert.That(b.Stats.Stats.ShotMade, Is.EqualTo(1));
    }

    [Test]
    public void ReentrantSubscriberCannotDuplicateOrResetAnotherBallsSnapshot()
    {
        IBasketballRuntime a = Ball();
        IBasketballRuntime b = Ball(true);
        Upper(a);
        Upper(b);
        int events = 0;
        upper.ShotResolved += result =>
        {
            events++;
            if (result.PlayerId == a.ParticipantId)
            {
                Lower(a);
                Lower(b);
                Assert.That(a.State.ThreeAttempt, Is.True, "A's snapshot survives until its own scoring tail");
            }
        };
        Lower(a);
        Assert.That(events, Is.EqualTo(2));
        Assert.That(a.State.ThreeAttempt, Is.False);
        Assert.That(b.State.ThreeAttempt, Is.False);
        Assert.That(a.Stats.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(b.Stats.Stats.TotalPoints, Is.EqualTo(3));
    }

    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public void TerminalPhaseRejectsPendingRepeatedAndNewAttempts(bool cpu, bool completed, bool upperBeforeEnd)
    {
        IBasketballRuntime ball = Ball(cpu);
        if (upperBeforeEnd) Upper(ball);
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);
        if (completed) lifecycle.CompleteEnd();
        if (!upperBeforeEnd) Upper(ball);
        int events = 0;
        upper.ShotResolved += _ => events++;
        string before = JsonUtility.ToJson(ball.Stats.Stats);
        Lower(ball);
        Upper(ball);
        Lower(ball);
        typeof(BasketBallShotMade).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(upper, null);
        Upper(ball);
        Lower(ball);
        Assert.That(JsonUtility.ToJson(ball.Stats.Stats), Is.EqualTo(before));
        Begin(ball);
        before = JsonUtility.ToJson(ball.Stats.Stats);
        Upper(ball);
        Lower(ball);
        Assert.That(JsonUtility.ToJson(ball.Stats.Stats), Is.EqualTo(before));
        Assert.That(events, Is.Zero);
    }

    [Test]
    public void ReentrantEndingRejectsAnotherBallsPendingMake()
    {
        IBasketballRuntime human = Ball();
        IBasketballRuntime cpu = Ball(true);
        Upper(human);
        Upper(cpu);
        int events = 0;
        upper.ShotResolved += _ =>
        {
            events++;
            lifecycle.RequestEnd(MatchEndReason.TimeExpired);
            Lower(cpu);
            Lower(human);
        };
        Lower(human);
        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That(human.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(cpu.Stats.TotalPoints, Is.Zero);
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void PreparingAndCountdownCannotAcceptMakesButNewPlayingAttemptCan()
    {
        lifecycle = new MatchLifecycle();
        IBasketballRuntime ball = Ball();
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
        lifecycle.BeginCountdown();
        Begin(ball);
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
        lifecycle.BeginPlay();
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.Zero, "rejected countdown attempt remains consumed");
        Begin(ball);
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
    }

    [Test]
    public void RejectedAttemptStaysConsumedIfEligibilityLaterChanges()
    {
        Object.DestroyImmediate(upper.gameObject);
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        bool eligible = false;
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => eligible);
        SetField(lower, "basketBallShotMade", upper);
        IBasketballRuntime ball = Ball();
        Upper(ball);
        Lower(ball);
        eligible = true; // Adversarial provider: real lifecycle cannot return to Playing after Ending.
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.Zero);
        Begin(ball);
        Upper(ball);
        Lower(ball);
        Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
    }
}
