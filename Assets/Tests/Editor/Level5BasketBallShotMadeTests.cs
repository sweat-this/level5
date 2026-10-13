using System.Collections.Generic;
using System.Reflection;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: <c>BasketBallShotMade</c> no longer reads <c>MatchRuntime.Rules</c>/
/// <c>MatchRuntime.RawModeId</c> directly - both arrive once through
/// <c>BindMatchContext(ResolvedMatchRules, GameModeId)</c>, the seam <c>GameLevelManager.Awake</c> now
/// supplies. Tests here register an upper crossing and call <c>shotMade(IBasketballRuntime)</c> directly against a component
/// that is never started - Unity does not pump <c>Start()</c>/<c>Update()</c> in a plain synchronous
/// NUnit test, so the swish/rim-animation subscriber (which needs a live <c>SFXBB.instance</c>) never
/// gets attached, mirroring how <see cref="Level5BasketballShotPipelineTests"/>'s own fixtures avoid
/// standing up unrelated lifecycle. Full <c>ShotScoring</c> arithmetic remains covered by
/// <c>Level5ShotScoringTests</c>; these only prove <c>BasketBallShotMade</c> wires the right bound
/// rules/mode into it and fails closed without them.
/// </summary>
public class Level5BasketBallShotMadeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private MatchLifecycle lifecycle;

    [SetUp]
    public void SetUp()
    {
        lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
        BasketBallShotMade.instance = null;
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private BasketBallShotMade MakeShotMade()
    {
        return Spawn("basketBallMadeShot1").AddComponent<BasketBallShotMade>();
    }

    private BasketBallState MakeState(bool twoAttempt = false, bool threeAttempt = false, bool fourAttempt = false, bool sevenAttempt = false)
    {
        BasketBallState state = Spawn("basketball-state").AddComponent<BasketBallState>();
        typeof(BasketBallState).GetMethod("BeginShotAttempt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(state, null);
        state.TwoAttempt = twoAttempt;
        state.ThreeAttempt = threeAttempt;
        state.FourAttempt = fourAttempt;
        state.SevenAttempt = sevenAttempt;
        return state;
    }

    private GameStats MakeStats()
    {
        return Spawn("stats").AddComponent<GameStats>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    private static void Score(BasketBallShotMade shotMade, IBasketballRuntime runtime)
    {
        typeof(BasketBallShotMade).GetMethod("RegisterUpperCrossing", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(shotMade, new object[] { runtime });
        shotMade.shotMade(runtime);
    }

    private sealed class FakeRuntime : IBasketballRuntime
    {
        public int ParticipantId { get; set; }
        public bool IsCpu { get; set; }
        public bool IsPrimary { get; set; }
        public GameObject OwnerActor { get; set; }
        public IShooterActor Actor => null;
        public BasketBallState State { get; set; }
        public GameStats Stats { get; set; }
        public float LastShotDistance { get; set; }

        public void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor)
        {
        }
    }

    // ==================== BindMatchContext ====================

    [Test]
    public void BindMatchContext_NullRules_LeavesComponentUnbound()
    {
        BasketBallShotMade shotMade = MakeShotMade();

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("received a null ResolvedMatchRules"));
        shotMade.BindMatchContext(null, GameModeId.TotalPoints, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(twoAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, ParticipantId = 1 };

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no bound match context"));
        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(0), "a null-rules bind attempt must leave the component unbound, so a made shot must not score");
    }

    [Test]
    public void BindMatchContext_SecondCall_DoesNotReplaceEstablishedContext()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        ResolvedMatchRules firstRules = new ResolvedMatchRules();
        ResolvedMatchRules secondRules = new ResolvedMatchRules();

        shotMade.BindMatchContext(firstRules, GameModeId.PointsByDistance, () => lifecycle.IsPlaying);

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("already has bound match context"));
        shotMade.BindMatchContext(secondRules, GameModeId.InThePocket, () => false);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 10f };

        Score(shotMade, runtime);

        // PointsByDistance (the first, accepted bind) with distance 10 scores floor(10 * 6 / 10) = 6,
        // not the ordinary three-line value (3) In the Pocket (the rejected second bind) would give.
        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(6), "the second BindMatchContext call must be ignored - scoring must still reflect the first bind's mode");
    }

    /// <summary>
    /// The already-bound check must run before the null-argument check: a null second call after a
    /// real bind already succeeded is still "already bound", not "left unbound" - the component still
    /// has its original valid rules either way, and the log must say so rather than misreport the
    /// component as unbound.
    /// </summary>
    [Test]
    public void BindMatchContext_SecondCallWithNullRules_ReportsAlreadyBoundNotUnbound()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        ResolvedMatchRules firstRules = new ResolvedMatchRules();
        shotMade.BindMatchContext(firstRules, GameModeId.PointsByDistance, () => lifecycle.IsPlaying);

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("already has bound match context"));
        shotMade.BindMatchContext(null, GameModeId.InThePocket, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 10f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(6), "the component must still score using its original bind (PointsByDistance) - a null second call must not leave it unbound");
    }

    // ==================== mode-branch behavior ====================

    [Test]
    public void PointsByDistanceMode_ScoresByDistanceRatherThanByLine()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.PointsByDistance, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 10f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(6), "Points by Distance must score floor(distance * 6 / 10), not the line's ordinary value");
    }

    [TestCase(ShotKind.Two, 2)]
    [TestCase(ShotKind.Three, 4)]
    [TestCase(ShotKind.Four, 6)]
    [TestCase(ShotKind.Seven, 10)]
    public void InThePocketMode_AppliesTheStreakBonus(ShotKind kind, int points)
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.InThePocket, () => lifecycle.IsPlaying);

        // The production streak-bonus threshold is 0 (see CurrentInThePocketStreakBonusThreshold), so
        // the very first made shot already qualifies - no streak needs to be seeded.
        BasketBallState state = MakeState(twoAttempt: kind == ShotKind.Two,
            threeAttempt: kind == ShotKind.Three, fourAttempt: kind == ShotKind.Four,
            sevenAttempt: kind == ShotKind.Seven);
        GameStats stats = MakeStats();
        stats.Stats.ShotAttempt = 1;
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 20f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(points), "The first make must receive the production bonus.");

        // A missed attempt breaks the predictive streak before the next make.
        stats.Stats.ShotAttempt += 2;
        MethodInfo beginAttempt = typeof(BasketBallState).GetMethod("BeginShotAttempt",
            BindingFlags.NonPublic | BindingFlags.Instance);
        beginAttempt.Invoke(state, null); // missed attempt
        beginAttempt.Invoke(state, null); // next made attempt
        state.TwoAttempt = kind == ShotKind.Two;
        state.ThreeAttempt = kind == ShotKind.Three;
        state.FourAttempt = kind == ShotKind.Four;
        state.SevenAttempt = kind == ShotKind.Seven;
        Score(shotMade, runtime);

        Assert.That(stats.Stats.ConsecutiveShotsMade, Is.EqualTo(1));
        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(2 * points), "A broken streak must not disable the bonus.");
    }

    [Test]
    public void ConsecutiveShotsMode_IsNotTreatedAsInThePocket()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(requiresConsecutiveShots: true), GameModeId.ConsecutiveShots, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 20f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(3), "Consecutive Shots is a distinct mode (RequiresConsecutiveShots) and must not enable the In the Pocket streak bonus");
    }

    [Test]
    public void OrdinaryMode_EnablesNeitherSpecialBranch()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 100f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(3), "an ordinary mode must score the line's plain value - no streak bonus, no distance scoring");
    }

    [Test]
    public void NoneMode_EnablesNeitherSpecialBranch()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.None, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, LastShotDistance = 100f };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(3), "GameModeId.None must score the line's plain value - no streak bonus, no distance scoring");
    }

    // ==================== marker-contest decisions ====================

    [Test]
    public void MarkerContestMode_OnEnabledMarker_ScoresAndCreditsTheMakeCount()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        ResolvedMatchRules rules = new ResolvedMatchRules(shotRule: ShotRule.ThreePoint, shotMarkers: ShotMarkerRequirement.ThreePoint);
        shotMade.BindMatchContext(rules, GameModeId.ThreePointContest, () => lifecycle.IsPlaying);

        BasketBallShotMarker marker = Spawn("marker").AddComponent<BasketBallShotMarker>();
        marker.MaxShotAttempt = 5;
        marker.MarkerEnabled = true;

        BasketBallState state = MakeState(threeAttempt: true);
        state.EnterShotMarker(marker);
        state.CaptureShotMarkerForAttempt();

        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(3), "an enabled marker's non-final attempt scores the plain line value");
        Assert.That(marker.ShotMade, Is.EqualTo(1), "the marker's own made-count must be credited using the bound rules' RequiresShotMarkers3s");
    }

    [Test]
    public void MarkerContestMode_OffMarker_ScoresNothingAndDoesNotCountTheMake()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        ResolvedMatchRules rules = new ResolvedMatchRules(shotRule: ShotRule.ThreePoint, shotMarkers: ShotMarkerRequirement.ThreePoint);
        shotMade.BindMatchContext(rules, GameModeId.ThreePointContest, () => lifecycle.IsPlaying);

        // Deliberately never enters a marker - PlayerOnMarkerOnShoot stays false.
        BasketBallState state = MakeState(threeAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

        Score(shotMade, runtime);

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(0), "a contest shot off the marker must score nothing, not even the made-shot counter");
    }

    // ==================== RequiresMoneyBall ====================

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void LateMoneyBallMakeCannotMutateStatsMarkerSnapshotOrPublish(bool cpu, bool completed)
    {
        var shotMade = MakeShotMade();
        var rules = new ResolvedMatchRules(shotRule: ShotRule.ThreePoint,
            shotMarkers: ShotMarkerRequirement.ThreePoint, requiresMoneyBall: true);
        shotMade.BindMatchContext(rules, GameModeId.ThreePointContest, () => lifecycle.IsPlaying);
        var marker = Spawn("marker").AddComponent<BasketBallShotMarker>();
        marker.MarkerEnabled = true;
        SetPrivateField(marker, "maxShotMade", 5);
        marker.ShotMade = 2;
        var state = MakeState(threeAttempt: true);
        state.EnterShotMarker(marker);
        state.CaptureShotMarkerForAttempt();
        state.MoneyBallEnabledOnShoot = true;
        var stats = MakeStats();
        stats.Stats.TotalPoints = 9;
        stats.Stats.TotalDistance = 12;
        stats.Stats.LongestShotMade = 12;
        var runtime = new FakeRuntime { State = state, Stats = stats, IsCpu = cpu, LastShotDistance = 40 };
        int events = 0;
        shotMade.ShotResolved += _ => events++;
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);
        if (completed) lifecycle.CompleteEnd();
        string before = JsonUtility.ToJson(stats.Stats);
        int pickups = Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None).Length;
        Score(shotMade, runtime);
        Score(shotMade, runtime);
        Assert.That(JsonUtility.ToJson(stats.Stats), Is.EqualTo(before));
        Assert.That(marker.ShotMade, Is.EqualTo(2));
        Assert.That(marker.MarkerEnabled, Is.True);
        Assert.That(state.OnShootShotMarker, Is.SameAs(marker));
        Assert.That(state.MoneyBallEnabledOnShoot, Is.True);
        Assert.That(state.ThreeAttempt, Is.True);
        Assert.That(events, Is.Zero);
        Assert.That(Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None).Length, Is.EqualTo(pickups));
        // No money template was supplied: reaching the pickup tail would also throw.
    }

    [Test]
    public void MissingEligibilityFailsClosed()
    {
        var shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints);
        var stats = MakeStats();
        var runtime = new FakeRuntime { State = MakeState(threeAttempt: true), Stats = stats };
        Score(shotMade, runtime);
        Assert.That(stats.TotalPoints, Is.Zero);
    }

    [Test]
    public void RequiresMoneyBallTrue_OnMarkerAtShoot_SpawnsTheMoneyPickup()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        GameObject moneyTemplate = Spawn("money-template");
        moneyTemplate.AddComponent<PickupObject>();
        SetPrivateField(shotMade, "moneyClone", moneyTemplate);

        shotMade.BindMatchContext(new ResolvedMatchRules(requiresMoneyBall: true), GameModeId.TotalPoints, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        state.PlayerOnMarkerOnShoot = true;
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

        PickupObject[] pickupsBefore = Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None);

        // An accepted make must finish its money tail even when publication accepts an ending.
        shotMade.ShotResolved += _ => lifecycle.RequestEnd(MatchEndReason.TimeExpired);
        Score(shotMade, runtime);

        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Ending));
        PickupObject[] pickupsAfter = Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        Assert.That(pickupsAfter.Length, Is.EqualTo(pickupsBefore.Length + 1), "RequiresMoneyBall true with the player on a marker at shoot time must spawn the money pickup");

        // The spawned clone is real Instantiate() output, not tracked by Spawn() - add it to the
        // teardown list explicitly so it does not leak into later tests in this run.
        foreach (PickupObject pickup in pickupsAfter)
        {
            if (System.Array.IndexOf(pickupsBefore, pickup) < 0)
            {
                spawned.Add(pickup.gameObject);
            }
        }
    }

    [Test]
    public void RequiresMoneyBallFalse_OnMarkerAtShoot_DoesNotSpawnTheMoneyPickup()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        GameObject moneyTemplate = Spawn("money-template");
        moneyTemplate.AddComponent<PickupObject>();
        SetPrivateField(shotMade, "moneyClone", moneyTemplate);

        shotMade.BindMatchContext(new ResolvedMatchRules(requiresMoneyBall: false), GameModeId.TotalPoints, () => lifecycle.IsPlaying);

        BasketBallState state = MakeState(threeAttempt: true);
        state.PlayerOnMarkerOnShoot = true;
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

        int pickupsBefore = Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None).Length;

        Score(shotMade, runtime);

        int pickupsAfter = Object.FindObjectsByType<PickupObject>(FindObjectsSortMode.None).Length;
        Assert.That(pickupsAfter, Is.EqualTo(pickupsBefore), "RequiresMoneyBall false must not spawn the money pickup, even with the player on a marker at shoot time - the gate must read the bound rules, not some other source");
    }

    // ==================== missing context ====================

    [Test]
    public void MissingContext_LogsErrorAndPerformsNoScoringOrMarkerOrMoneyBallMutation()
    {
        BasketBallShotMade shotMade = MakeShotMade();

        BasketBallShotMarker marker = Spawn("marker").AddComponent<BasketBallShotMarker>();
        marker.MaxShotAttempt = 5;
        marker.MarkerEnabled = true;

        BasketBallState state = MakeState(threeAttempt: true);
        state.EnterShotMarker(marker);
        state.CaptureShotMarkerForAttempt();

        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats, ParticipantId = 4 };

        bool resolvedFired = false;
        shotMade.ShotResolved += _ => resolvedFired = true;

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no bound match context"));

        Assert.DoesNotThrow(() => Score(shotMade, runtime));

        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(0), "no score may be applied when match context is missing");
        Assert.That(marker.ShotMade, Is.EqualTo(0), "no marker made-count may be applied when match context is missing");
        Assert.IsFalse(resolvedFired, "MadeShotResult must not publish when match context is missing");
    }

    [Test]
    public void MissingContext_StillResetsCompatibilityFlags()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.ShotMade1 = true;
        shotMade.ShotMade2 = true;

        BasketBallState state = MakeState(twoAttempt: true);
        GameStats stats = MakeStats();
        FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no bound match context"));
        Score(shotMade, runtime);

        Assert.IsFalse(shotMade.ShotMade1, "compatibility flags reset even when scoring is skipped");
        Assert.IsFalse(shotMade.ShotMade2);
    }

    // ==================== participant identity ====================

    [Test]
    public void ParticipantIdentity_ComesFromTheCollidingRuntimeNotAnyGlobal()
    {
        BasketBallShotMade shotMade = MakeShotMade();
        shotMade.BindMatchContext(new ResolvedMatchRules(), GameModeId.TotalPoints, () => lifecycle.IsPlaying);

        BasketBallState stateA = MakeState(twoAttempt: true);
        GameStats statsA = MakeStats();
        FakeRuntime runtimeA = new FakeRuntime { State = stateA, Stats = statsA, ParticipantId = 1, IsCpu = false };

        MadeShotResult? published = null;
        shotMade.ShotResolved += result => published = result;

        Score(shotMade, runtimeA);

        Assert.That(published, Is.Not.Null);
        Assert.That(published.Value.PlayerId, Is.EqualTo(1));
        Assert.That(published.Value.IsCpu, Is.False);
        Assert.That(statsA.Stats.TotalPoints, Is.EqualTo(2), "the shot must score onto the colliding runtime's own GameStats");

        // A second, different runtime's shot must be independently attributed - not to a shared
        // static/global identity.
        BasketBallState stateB = MakeState(twoAttempt: true);
        GameStats statsB = MakeStats();
        FakeRuntime runtimeB = new FakeRuntime { State = stateB, Stats = statsB, ParticipantId = 2, IsCpu = true };

        Score(shotMade, runtimeB);

        Assert.That(published.Value.PlayerId, Is.EqualTo(2));
        Assert.That(published.Value.IsCpu, Is.True);
        Assert.That(statsB.Stats.TotalPoints, Is.EqualTo(2));
        Assert.That(statsA.Stats.TotalPoints, Is.EqualTo(2), "the first runtime's stats must not be touched by the second shot");
    }
}
