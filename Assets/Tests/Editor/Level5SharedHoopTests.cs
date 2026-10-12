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

    [SetUp]
    public void SetUp()
    {
        upper = Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints);
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
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints);
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
        upper.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints);
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
}
