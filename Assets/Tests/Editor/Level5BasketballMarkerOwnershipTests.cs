using System.Reflection;
using System.Collections.Generic;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

/// <summary>
/// AUD-010 Phase 1c: marker occupancy, the launch-time marker snapshot and final-attempt ownership
/// are now the participant's own <see cref="BasketBallState"/>/<see cref="BasketBallShotMarker"/>
/// state, not an id resolved through <c>GameRules.BasketBallShotMarkersList</c> or
/// <c>GameLevelManager.instance.players[0]</c>.
///
/// AUD-010 Phase 1c (session-boundary slice): <c>BasketBallShotMarker.OnTriggerExit</c> and
/// <c>Update()</c> used to be untestable here because both reached <c>GameRules.instance</c> directly
/// (display text / <c>MarkersRemaining</c> / <c>IsGameOver()</c>), and standing up a real
/// <c>GameRules</c> singleton pulls in <c>MatchController</c>, <c>MatchHudPresenter</c> and
/// <c>ProgressionService</c> with no lightweight test seam. Now that the marker reaches that state
/// through a bound <see cref="IShotMarkerSession"/> instead, both are exercised directly below against
/// <see cref="FakeShotMarkerSession"/> - no real <c>GameRules</c> needed. <c>Start()</c> itself is
/// still not driven end-to-end here (it also calls <c>GameObject.Find("basketBall_target")</c>, which
/// this suite does not stand up), except for the composition-guard test that intentionally invokes it
/// with no bound session to prove the marker fails closed.
/// </summary>
public class Level5BasketballMarkerOwnershipTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

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
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private BasketBallState MakeState(string name)
    {
        return Spawn(name).AddComponent<BasketBallState>();
    }

    private BasketBallShotMarker MakeMarker(string name, int maxShotAttempt = 5)
    {
        BasketBallShotMarker marker = Spawn(name).AddComponent<BasketBallShotMarker>();
        marker.gameObject.tag = "shot_marker";
        marker.MaxShotAttempt = maxShotAttempt;
        SetPrivateField(marker, "detectCollisions", true);
        return marker;
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

    private static void InvokeOnTriggerEnter(BasketBallShotMarker marker, Collider other)
    {
        MethodInfo method = typeof(BasketBallShotMarker).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "BasketBallShotMarker must declare OnTriggerEnter");
        method.Invoke(marker, new object[] { other });
    }

    private static void InvokeOnTriggerExit(BasketBallShotMarker marker, Collider other)
    {
        MethodInfo method = typeof(BasketBallShotMarker).GetMethod("OnTriggerExit", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "BasketBallShotMarker must declare OnTriggerExit");
        method.Invoke(marker, new object[] { other });
    }

    /// <summary>
    /// Prepares an already-created marker for direct exercise of session-dependent code
    /// (<c>setDisplayText</c>/<c>Update</c>/<c>OnTriggerExit</c>/<c>CompleteMarker</c>) without driving
    /// <c>Start()</c> itself, which also depends on <c>GameObject.Find("basketBall_target")</c> - see
    /// the file header. Binds <paramref name="session"/>, stands in a real UI <c>Text</c> for
    /// <c>displayCurrentMarkerStats</c> (the same technique <c>Level5ShotMeterOwnershipTests</c>/
    /// <c>Level5RangeMeterOwnershipTests</c> already use), and a real <c>SpriteRenderer</c> for the
    /// completion path's opacity write.
    /// </summary>
    private static void PrepareMarkerForSessionDependentBehavior(BasketBallShotMarker marker, IShotMarkerSession session)
    {
        marker.BindShotMarkerSession(session);
        SetPrivateField(marker, "displayCurrentMarkerStats", marker.gameObject.AddComponent<Text>());
        SetPrivateField(marker, "spriteRenderer", marker.gameObject.AddComponent<SpriteRenderer>());
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{target.GetType().Name} must declare a method named '{methodName}'");
        method.Invoke(target, args);
    }

    private GameObject MakeHumanParticipant(string name, BasketBallState state)
    {
        GameObject actor = Spawn($"{name}-actor");
        PlayerIdentifier identifier = actor.AddComponent<PlayerIdentifier>();
        identifier.basketball = state.gameObject;

        GameObject hitbox = Spawn($"{name}-hitbox");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "playerHitbox";
        hitbox.AddComponent<BoxCollider>();
        return hitbox;
    }

    private GameObject MakeCpuParticipant(string name, BasketBallState state)
    {
        GameObject actor = Spawn($"{name}-actor");
        PlayerIdentifier identifier = actor.AddComponent<PlayerIdentifier>();
        identifier.autoBasketball = state.gameObject;

        GameObject hitbox = Spawn($"{name}-hitbox");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "autoPlayerHitbox";
        hitbox.AddComponent<BoxCollider>();
        return hitbox;
    }

    // ==================== BasketBallState transition invariants (section 3) ====================

    [Test]
    public void EnterShotMarker_SetsCurrentMarkerAndPlayerOnMarker()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker marker = MakeMarker("marker");

        state.EnterShotMarker(marker);

        Assert.AreSame(marker, state.CurrentShotMarker);
        Assert.IsTrue(state.PlayerOnMarker);
    }

    [Test]
    public void EnterShotMarker_MostRecentlyEnteredMarkerWins()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker markerA = MakeMarker("A");
        BasketBallShotMarker markerB = MakeMarker("B");

        state.EnterShotMarker(markerA);
        state.EnterShotMarker(markerB);

        Assert.AreSame(markerB, state.CurrentShotMarker);
    }

    [Test]
    public void ExitShotMarker_OverlapSequenceKeepsTheNewerMarkerUntilItExits()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker markerA = MakeMarker("A");
        BasketBallShotMarker markerB = MakeMarker("B");

        state.EnterShotMarker(markerA);
        state.EnterShotMarker(markerB);
        state.ExitShotMarker(markerA);

        Assert.AreSame(markerB, state.CurrentShotMarker, "exiting a marker that is no longer current must not clear the newer occupancy");
        Assert.IsTrue(state.PlayerOnMarker);

        state.ExitShotMarker(markerB);

        Assert.IsNull(state.CurrentShotMarker);
        Assert.IsFalse(state.PlayerOnMarker);
    }

    [Test]
    public void ExitShotMarker_NoMarkerModeNeverEntered_IsANoOp()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker marker = MakeMarker("marker");

        state.ExitShotMarker(marker);

        Assert.IsNull(state.CurrentShotMarker);
        Assert.IsFalse(state.PlayerOnMarker);
    }

    [Test]
    public void CaptureShotMarkerForAttempt_SnapshotsCurrentMarker()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker marker = MakeMarker("marker");
        state.EnterShotMarker(marker);

        state.CaptureShotMarkerForAttempt();

        Assert.AreSame(marker, state.OnShootShotMarker);
        Assert.IsTrue(state.PlayerOnMarkerOnShoot);
    }

    [Test]
    public void CaptureShotMarkerForAttempt_WithNoCurrentMarker_LeavesSnapshotEmpty()
    {
        BasketBallState state = MakeState("state");

        state.CaptureShotMarkerForAttempt();

        Assert.IsNull(state.OnShootShotMarker);
        Assert.IsFalse(state.PlayerOnMarkerOnShoot);
    }

    [Test]
    public void OnShootShotMarker_SurvivesExitingTheMarkerAfterLaunch()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker marker = MakeMarker("marker");
        state.EnterShotMarker(marker);
        state.CaptureShotMarkerForAttempt();

        state.ExitShotMarker(marker);

        Assert.AreSame(marker, state.OnShootShotMarker, "the launch snapshot must survive the participant leaving the marker mid-flight");
        Assert.IsNull(state.CurrentShotMarker);
    }

    [Test]
    public void OnShootShotMarker_SurvivesEnteringAnotherMarkerAfterLaunch()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker markerA = MakeMarker("A");
        BasketBallShotMarker markerB = MakeMarker("B");
        state.EnterShotMarker(markerA);
        state.CaptureShotMarkerForAttempt();

        state.EnterShotMarker(markerB);

        Assert.AreSame(markerA, state.OnShootShotMarker, "the launch snapshot must not follow the participant onto a new marker mid-flight");
        Assert.AreSame(markerB, state.CurrentShotMarker);
    }

    [Test]
    public void ResetShotAttemptSnapshot_ClearsLaunchSnapshotButPreservesCurrentOccupancy()
    {
        BasketBallState state = MakeState("state");
        BasketBallShotMarker marker = MakeMarker("marker");
        state.EnterShotMarker(marker);
        state.CaptureShotMarkerForAttempt();
        state.MoneyBallEnabledOnShoot = true;

        state.ResetShotAttemptSnapshot();

        Assert.IsNull(state.OnShootShotMarker, "reset must clear the launch snapshot");
        Assert.IsFalse(state.PlayerOnMarkerOnShoot);
        Assert.IsFalse(state.MoneyBallEnabledOnShoot);
        Assert.AreSame(marker, state.CurrentShotMarker, "reset must not clear current occupancy - a participant still standing on a marker can shoot again");
        Assert.IsTrue(state.PlayerOnMarker);
    }

    // ==================== BasketBallShotMarker.RegisterAttempt (section 10) ====================

    [Test]
    public void RegisterAttempt_IncrementsShotAttemptOnExactMarkerOnly()
    {
        BasketBallShotMarker markerA = MakeMarker("A", maxShotAttempt: 5);
        BasketBallShotMarker markerB = MakeMarker("B", maxShotAttempt: 5);

        markerA.RegisterAttempt(new FakeRuntime());

        Assert.That(markerA.ShotAttempt, Is.EqualTo(1));
        Assert.That(markerB.ShotAttempt, Is.EqualTo(0), "registering an attempt on one marker must not touch another marker's counter");
    }

    [Test]
    public void RegisterAttempt_CapturesFinalAttemptRuntimeExactlyWhenCounterFirstReachesMax()
    {
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 2);
        FakeRuntime first = new FakeRuntime { ParticipantId = 1 };
        FakeRuntime finalAttempt = new FakeRuntime { ParticipantId = 2 };

        marker.RegisterAttempt(first);
        marker.RegisterAttempt(finalAttempt);

        Assert.AreSame(finalAttempt, GetPrivateField(marker, "finalAttemptRuntime"));
    }

    [Test]
    public void RegisterAttempt_ExtraAttemptAfterMaxDoesNotReplaceFinalAttemptRuntime()
    {
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 1);
        FakeRuntime finalAttempt = new FakeRuntime { ParticipantId = 1 };
        FakeRuntime extra = new FakeRuntime { ParticipantId = 2 };

        marker.RegisterAttempt(finalAttempt);
        marker.RegisterAttempt(extra); // sixth-style extra attempt before the marker disables

        Assert.AreSame(finalAttempt, GetPrivateField(marker, "finalAttemptRuntime"),
            "an extra attempt taken after MaxShotAttempt must not overwrite the captured final-attempt runtime");
        Assert.That(marker.ShotAttempt, Is.EqualTo(2), "excess attempts are still counted - only the captured runtime is protected");
    }

    [Test]
    public void RegisterAttempt_SecondaryParticipantFinalAttemptIsIndependentOfPrimaryState()
    {
        // The regression this pins: a secondary human/CPU's final marker attempt must be judged on
        // that participant's own Actor/State, never GameLevelManager.instance.players[0]'s.
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 1);
        FakeShooterActor primaryActor = new FakeShooterActor { HasBasketball = true, InAir = true };
        FakeRuntime primary = new FakeRuntime { ParticipantId = 0, Actor = primaryActor, State = MakeState("primary-state") };

        FakeShooterActor secondaryActor = new FakeShooterActor { HasBasketball = false, InAir = false };
        BasketBallState secondaryState = MakeState("secondary-state");
        secondaryState.InAir = false;
        FakeRuntime secondary = new FakeRuntime { ParticipantId = 1, Actor = secondaryActor, State = secondaryState };

        marker.RegisterAttempt(secondary);

        IBasketballRuntime captured = (IBasketballRuntime)GetPrivateField(marker, "finalAttemptRuntime");
        Assert.AreSame(secondary, captured);
        Assert.IsFalse(captured.Actor.HasBasketball, "the captured runtime's readiness must read the secondary participant's own actor");
        Assert.IsFalse(captured.State.InAir);
        // The primary participant (still holding the ball, still airborne) was never touched or read.
        Assert.IsTrue(primary.Actor.HasBasketball);
    }

    [Test]
    public void RegisterAttempt_HumanFinalAttemptCapturesTheHumanRuntime()
    {
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 1);
        FakeRuntime human = new FakeRuntime { ParticipantId = 0, IsCpu = false, Actor = new FakeShooterActor(), State = MakeState("human-state") };

        marker.RegisterAttempt(human);

        Assert.AreSame(human, GetPrivateField(marker, "finalAttemptRuntime"));
        Assert.IsFalse(((IBasketballRuntime)GetPrivateField(marker, "finalAttemptRuntime")).IsCpu);
    }

    [Test]
    public void RegisterAttempt_CpuFinalAttemptCapturesTheCpuRuntime()
    {
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 1);
        FakeRuntime cpu = new FakeRuntime { ParticipantId = 1, IsCpu = true, Actor = new FakeShooterActor(), State = MakeState("cpu-state") };

        marker.RegisterAttempt(cpu);

        Assert.AreSame(cpu, GetPrivateField(marker, "finalAttemptRuntime"));
        Assert.IsTrue(((IBasketballRuntime)GetPrivateField(marker, "finalAttemptRuntime")).IsCpu);
    }

    // ==================== OnTriggerEnter participant resolution (section 5) ====================

    [Test]
    public void OnTriggerEnter_HumanHitbox_UpdatesOnlyThatParticipantsBasketBallState()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        BasketBallState humanA = MakeState("human-a-state");
        BasketBallState humanB = MakeState("human-b-state");
        GameObject hitboxA = MakeHumanParticipant("human-a", humanA);
        MakeHumanParticipant("human-b", humanB);

        InvokeOnTriggerEnter(marker, hitboxA.GetComponent<Collider>());

        Assert.AreSame(marker, humanA.CurrentShotMarker);
        Assert.IsTrue(humanA.PlayerOnMarker);
        Assert.IsNull(humanB.CurrentShotMarker, "a marker enter must resolve to the exact colliding participant, not another participant of the same role");
        Assert.IsFalse(humanB.PlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_CpuHitbox_UpdatesOnlyThatParticipantsBasketBallState()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        BasketBallState cpuA = MakeState("cpu-a-state");
        BasketBallState cpuB = MakeState("cpu-b-state");
        GameObject hitboxA = MakeCpuParticipant("cpu-a", cpuA);
        MakeCpuParticipant("cpu-b", cpuB);

        InvokeOnTriggerEnter(marker, hitboxA.GetComponent<Collider>());

        Assert.AreSame(marker, cpuA.CurrentShotMarker);
        Assert.IsTrue(cpuA.PlayerOnMarker);
        Assert.IsNull(cpuB.CurrentShotMarker);
        Assert.IsFalse(cpuB.PlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_TwoHumansSameMarker_SecondEntryDoesNotContaminateTheFirstsExitedState()
    {
        // Section 15's "two humans" case: A enters, A exits (via the pure BasketBallState API - see
        // the file header for why OnTriggerExit itself is not exercised here), B enters.
        BasketBallShotMarker marker = MakeMarker("marker");
        BasketBallState humanA = MakeState("human-a-state");
        BasketBallState humanB = MakeState("human-b-state");
        GameObject hitboxA = MakeHumanParticipant("human-a", humanA);
        GameObject hitboxB = MakeHumanParticipant("human-b", humanB);

        InvokeOnTriggerEnter(marker, hitboxA.GetComponent<Collider>());
        humanA.ExitShotMarker(marker);
        InvokeOnTriggerEnter(marker, hitboxB.GetComponent<Collider>());

        Assert.IsFalse(humanA.PlayerOnMarker);
        Assert.IsNull(humanA.CurrentShotMarker);
        Assert.IsTrue(humanB.PlayerOnMarker);
        Assert.AreSame(marker, humanB.CurrentShotMarker);
    }

    [Test]
    public void OnTriggerEnter_MissingPlayerIdentifier_LogsAndIgnoresTransition()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject orphanHitbox = Spawn("orphan-hitbox");
        orphanHitbox.tag = "playerHitbox";
        orphanHitbox.AddComponent<BoxCollider>();

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the human participant's BasketBallState"));

        InvokeOnTriggerEnter(marker, orphanHitbox.GetComponent<Collider>());

        // no exception - the transition was ignored, not substituted with another participant, and
        // the role-wide presentation flag must not flip true for a transition nothing actually applied.
        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_IdentifierWithNoBasketballReference_LogsAndIgnoresTransition()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject actor = Spawn("incomplete-actor");
        actor.AddComponent<PlayerIdentifier>(); // .basketball left unset
        GameObject hitbox = Spawn("incomplete-hitbox");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "playerHitbox";
        hitbox.AddComponent<BoxCollider>();

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the human participant's BasketBallState"));

        InvokeOnTriggerEnter(marker, hitbox.GetComponent<Collider>());

        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_CpuIdentifierWithNoAutoBasketballReference_LogsAndIgnoresTransition()
    {
        // CPU-role mirror of OnTriggerEnter_IdentifierWithNoBasketballReference_LogsAndIgnoresTransition:
        // TryGetBasketballState's cpuRoute branch reads .autoBasketball instead of .basketball, and
        // that branch deserves the same missing-reference coverage as the human one.
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject actor = Spawn("incomplete-cpu-actor");
        actor.AddComponent<PlayerIdentifier>(); // .autoBasketball left unset
        GameObject hitbox = Spawn("incomplete-cpu-hitbox");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "autoPlayerHitbox";
        hitbox.AddComponent<BoxCollider>();

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the CPU participant's BasketBallState"));

        InvokeOnTriggerEnter(marker, hitbox.GetComponent<Collider>());

        Assert.IsFalse(marker.AutoPlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_SelectedBasketballWithoutBasketBallStateComponent_LogsAndIgnoresTransition()
    {
        // Distinct from OnTriggerEnter_IdentifierWithNoBasketballReference_LogsAndIgnoresTransition:
        // here the provider resolves and the basketball reference itself is non-null, but that
        // GameObject has no BasketBallState component - IBasketballParticipantStateProvider.
        // TryGetBasketballState must still return false rather than a null-state pass-through.
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject actor = Spawn("actor-ball-no-state");
        PlayerIdentifier identifier = actor.AddComponent<PlayerIdentifier>();
        identifier.basketball = Spawn("ball-without-basketballstate");
        GameObject hitbox = Spawn("hitbox-ball-no-state");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "playerHitbox";
        hitbox.AddComponent<BoxCollider>();

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the human participant's BasketBallState"));

        InvokeOnTriggerEnter(marker, hitbox.GetComponent<Collider>());

        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void OnTriggerEnter_SelectedAutoBasketballWithoutBasketBallStateComponent_LogsAndIgnoresTransition()
    {
        // CPU-role mirror of OnTriggerEnter_SelectedBasketballWithoutBasketBallStateComponent_LogsAndIgnoresTransition.
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject actor = Spawn("actor-cpu-ball-no-state");
        PlayerIdentifier identifier = actor.AddComponent<PlayerIdentifier>();
        identifier.autoBasketball = Spawn("auto-ball-without-basketballstate");
        GameObject hitbox = Spawn("hitbox-cpu-ball-no-state");
        hitbox.transform.parent = actor.transform;
        hitbox.tag = "autoPlayerHitbox";
        hitbox.AddComponent<BoxCollider>();

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the CPU participant's BasketBallState"));

        InvokeOnTriggerEnter(marker, hitbox.GetComponent<Collider>());

        Assert.IsFalse(marker.AutoPlayerOnMarker);
    }

    // ==================== marker-local presentation occupancy membership (section 8 of the plan) ====================
    //
    // AddHumanOccupant/RemoveHumanOccupant/AddCpuOccupant/RemoveCpuOccupant are exercised directly via
    // reflection rather than through OnTriggerExit: OnTriggerExit also calls setDisplayText(), which
    // reaches GameRules.instance (see the file header for why a real GameRules is not stood up in this
    // suite). These private methods are the exact seam OnTriggerExit delegates to for membership
    // mutation, so they exercise the same multi-collider-safety invariants without that dependency.

    [Test]
    public void AddHumanOccupant_FirstOccupant_SetsPlayerOnMarkerTrue()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider hitbox = Spawn("hitbox-a").AddComponent<BoxCollider>();

        InvokePrivateMethod(marker, "AddHumanOccupant", hitbox);

        Assert.IsTrue(marker.PlayerOnMarker);
    }

    [Test]
    public void AddHumanOccupant_TwoCollidersCoexist()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider a = Spawn("hitbox-a").AddComponent<BoxCollider>();
        Collider b = Spawn("hitbox-b").AddComponent<BoxCollider>();

        InvokePrivateMethod(marker, "AddHumanOccupant", a);
        InvokePrivateMethod(marker, "AddHumanOccupant", b);

        Assert.IsTrue(marker.PlayerOnMarker);
    }

    [Test]
    public void RemoveHumanOccupant_OneOfTwoExiting_LeavesOccupancyTrue()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider a = Spawn("hitbox-a").AddComponent<BoxCollider>();
        Collider b = Spawn("hitbox-b").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", a);
        InvokePrivateMethod(marker, "AddHumanOccupant", b);

        InvokePrivateMethod(marker, "RemoveHumanOccupant", a);

        Assert.IsTrue(marker.PlayerOnMarker, "human B is still inside - one occupant exiting must not clear another's presence");
    }

    [Test]
    public void RemoveHumanOccupant_FinalOccupantExiting_ClearsOccupancy()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider a = Spawn("hitbox-a").AddComponent<BoxCollider>();
        Collider b = Spawn("hitbox-b").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", a);
        InvokePrivateMethod(marker, "AddHumanOccupant", b);
        InvokePrivateMethod(marker, "RemoveHumanOccupant", a);

        InvokePrivateMethod(marker, "RemoveHumanOccupant", b);

        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void AddCpuOccupant_FirstOccupant_SetsAutoPlayerOnMarkerTrue()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider hitbox = Spawn("cpu-hitbox-a").AddComponent<BoxCollider>();

        InvokePrivateMethod(marker, "AddCpuOccupant", hitbox);

        Assert.IsTrue(marker.AutoPlayerOnMarker);
    }

    [Test]
    public void RemoveCpuOccupant_OneOfTwoExiting_LeavesOccupancyTrue()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider a = Spawn("cpu-hitbox-a").AddComponent<BoxCollider>();
        Collider b = Spawn("cpu-hitbox-b").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddCpuOccupant", a);
        InvokePrivateMethod(marker, "AddCpuOccupant", b);

        InvokePrivateMethod(marker, "RemoveCpuOccupant", a);

        Assert.IsTrue(marker.AutoPlayerOnMarker, "CPU B is still inside - one occupant exiting must not clear another's presence");
    }

    [Test]
    public void RemoveCpuOccupant_FinalOccupantExiting_ClearsOccupancy()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider a = Spawn("cpu-hitbox-a").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddCpuOccupant", a);

        InvokePrivateMethod(marker, "RemoveCpuOccupant", a);

        Assert.IsFalse(marker.AutoPlayerOnMarker);
    }

    [Test]
    public void HumanAndCpuMembership_AreIndependent()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider human = Spawn("human-hitbox").AddComponent<BoxCollider>();
        Collider cpu = Spawn("cpu-hitbox").AddComponent<BoxCollider>();

        InvokePrivateMethod(marker, "AddHumanOccupant", human);
        InvokePrivateMethod(marker, "AddCpuOccupant", cpu);

        Assert.IsTrue(marker.PlayerOnMarker);
        Assert.IsTrue(marker.AutoPlayerOnMarker);
    }

    [Test]
    public void RemoveHumanOccupant_CannotClearCpuPresence()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider human = Spawn("human-hitbox").AddComponent<BoxCollider>();
        Collider cpu = Spawn("cpu-hitbox").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", human);
        InvokePrivateMethod(marker, "AddCpuOccupant", cpu);

        InvokePrivateMethod(marker, "RemoveHumanOccupant", human);

        Assert.IsFalse(marker.PlayerOnMarker);
        Assert.IsTrue(marker.AutoPlayerOnMarker, "removing the human occupant must not clear CPU presence");
    }

    [Test]
    public void RemoveCpuOccupant_CannotClearHumanPresence()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider human = Spawn("human-hitbox").AddComponent<BoxCollider>();
        Collider cpu = Spawn("cpu-hitbox").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", human);
        InvokePrivateMethod(marker, "AddCpuOccupant", cpu);

        InvokePrivateMethod(marker, "RemoveCpuOccupant", cpu);

        Assert.IsTrue(marker.PlayerOnMarker, "removing the CPU occupant must not clear human presence");
        Assert.IsFalse(marker.AutoPlayerOnMarker);
    }

    [Test]
    public void AddHumanOccupant_DuplicateEntry_IsIdempotent()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider hitbox = Spawn("hitbox").AddComponent<BoxCollider>();

        InvokePrivateMethod(marker, "AddHumanOccupant", hitbox);
        InvokePrivateMethod(marker, "AddHumanOccupant", hitbox);
        InvokePrivateMethod(marker, "RemoveHumanOccupant", hitbox);

        Assert.IsFalse(marker.PlayerOnMarker, "a duplicate add must not require two removes to clear occupancy");
    }

    [Test]
    public void RemoveHumanOccupant_DuplicateExit_IsHarmless()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider hitbox = Spawn("hitbox").AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", hitbox);
        InvokePrivateMethod(marker, "RemoveHumanOccupant", hitbox);

        Assert.DoesNotThrow(() => InvokePrivateMethod(marker, "RemoveHumanOccupant", hitbox));
        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void RemoveHumanOccupant_WithoutPriorEntry_IsHarmless()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        Collider hitbox = Spawn("hitbox").AddComponent<BoxCollider>();

        Assert.DoesNotThrow(() => InvokePrivateMethod(marker, "RemoveHumanOccupant", hitbox));
        Assert.IsFalse(marker.PlayerOnMarker);
    }

    [Test]
    public void RemoveHumanOccupant_ClearsMembershipEvenWhenParticipantResolutionFails()
    {
        // Pins the invariant that exit-side membership removal does not depend on participant
        // resolution succeeding: the collider has physically left the marker regardless of whether
        // its PlayerIdentifier wiring can still be resolved (see RemoveHumanOccupant / OnTriggerExit).
        // No PlayerIdentifier on this hitbox's hierarchy - ResolveParticipantState would fail for it,
        // exactly like OnTriggerEnter_MissingPlayerIdentifier_LogsAndIgnoresTransition - but removal
        // must not gate on that.
        BasketBallShotMarker marker = MakeMarker("marker");
        GameObject orphanHitbox = Spawn("orphan-hitbox");
        orphanHitbox.tag = "playerHitbox";
        Collider hitbox = orphanHitbox.AddComponent<BoxCollider>();
        InvokePrivateMethod(marker, "AddHumanOccupant", hitbox);
        Assert.IsTrue(marker.PlayerOnMarker);

        InvokePrivateMethod(marker, "RemoveHumanOccupant", hitbox);

        Assert.IsFalse(marker.PlayerOnMarker, "membership removal must succeed even though this collider's participant cannot be resolved");
    }

    [Test]
    public void OnTriggerEnter_TwoHumanCollidersOnSameMarker_OccupancyStaysTrueUntilLastColliderExits()
    {
        // The regression this pins end-to-end: two humans on one marker, one leaves - the marker must
        // not clear presentation occupancy out from under the participant who remains.
        BasketBallShotMarker marker = MakeMarker("marker");
        BasketBallState humanA = MakeState("human-a-state");
        BasketBallState humanB = MakeState("human-b-state");
        GameObject hitboxA = MakeHumanParticipant("human-a", humanA);
        GameObject hitboxB = MakeHumanParticipant("human-b", humanB);

        InvokeOnTriggerEnter(marker, hitboxA.GetComponent<Collider>());
        InvokeOnTriggerEnter(marker, hitboxB.GetComponent<Collider>());

        Assert.IsTrue(marker.PlayerOnMarker);

        InvokePrivateMethod(marker, "RemoveHumanOccupant", hitboxA.GetComponent<Collider>());

        Assert.IsTrue(marker.PlayerOnMarker, "human B's collider is still inside the marker");

        InvokePrivateMethod(marker, "RemoveHumanOccupant", hitboxB.GetComponent<Collider>());

        Assert.IsFalse(marker.PlayerOnMarker);
    }

    // ==================== IsPointContestMode / AUD-010 Phase 1c rule-source migration ====================
    //
    // Unlike Update()/OnTriggerExit above, IsPointContestMode() reaches only MatchRuntime.Rules, not
    // GameRules.instance, so it needs none of the GameRules seam this file's header explains is
    // missing. Driven the same way Level5CpuBaselineInitializationTests drives other
    // ResolvedMatchRules-dependent code: GameOptionsSnapshot around the mutation, ActiveMatch.Clear()
    // so MatchRuntime.Rules falls back to the legacy GameOptions globals instead of a real
    // MatchConfiguration.

    private static bool InvokeIsPointContestMode()
    {
        MethodInfo method = typeof(BasketBallShotMarker).GetMethod("IsPointContestMode", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "BasketBallShotMarker must declare IsPointContestMode");
        return (bool)method.Invoke(null, null);
    }

    [Test]
    public void IsPointContestMode_OrdinaryShootingMode_ReturnsFalse()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = false;

            Assert.IsFalse(InvokeIsPointContestMode(), "ordinary shooting must not be classified as a marker contest");
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void IsPointContestMode_ThreePointContest_ReturnsTrue()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = true;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = false;

            Assert.IsTrue(InvokeIsPointContestMode());
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void IsPointContestMode_FourPointContest_ReturnsTrue()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = true;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = false;

            Assert.IsTrue(InvokeIsPointContestMode());
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void IsPointContestMode_SevenPointContest_ReturnsTrue()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = true;
            GameOptions.gameModeAllPointContest = false;

            Assert.IsTrue(InvokeIsPointContestMode());
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void IsPointContestMode_AllPointContest_ReturnsTrue()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = true;

            Assert.IsTrue(InvokeIsPointContestMode());
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    // ==================== BasketBallShotMarker.BindShotMarkerSession (AUD-010 Phase 1c) ====================

    [Test]
    public void BindShotMarkerSession_ValidProvider_Binds()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 3 };

        marker.BindShotMarkerSession(session);

        Assert.AreSame(session, GetPrivateField(marker, "markerSession"));
    }

    [Test]
    public void BindShotMarkerSession_NullProvider_LogsAndLeavesUnbound()
    {
        BasketBallShotMarker marker = MakeMarker("marker");

        LogAssert.Expect(LogType.Error, new Regex("null shot-marker session"));
        marker.BindShotMarkerSession(null);

        Assert.IsNull(GetPrivateField(marker, "markerSession"));
    }

    [Test]
    public void BindShotMarkerSession_SecondCall_IsRejectedAndOriginalProviderRetained()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession first = new FakeShotMarkerSession { MarkersRemaining = 3 };
        FakeShotMarkerSession second = new FakeShotMarkerSession { MarkersRemaining = 1 };
        marker.BindShotMarkerSession(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound shot-marker session"));
        marker.BindShotMarkerSession(second);

        Assert.AreSame(first, GetPrivateField(marker, "markerSession"),
            "a second BindShotMarkerSession call must not overwrite the original binding");
    }

    // ==================== Start() composition guard (AUD-010 Phase 1c) ====================

    [Test]
    public void Start_NoBoundSession_LogsActionableErrorAndFailsMarkerClosed()
    {
        BasketBallShotMarker marker = MakeMarker("marker"); // MakeMarker forces detectCollisions true; Start() must override it.

        LogAssert.Expect(LogType.Error, new Regex("no bound IShotMarkerSession"));
        InvokePrivateMethod(marker, "Start");

        Assert.IsFalse(marker.enabled, "an unbound marker must disable itself so Update() never runs");
        Assert.IsFalse((bool)GetPrivateField(marker, "detectCollisions"),
            "an unbound marker must not process collisions - OnTriggerEnter/Exit gate on this flag directly, not on enabled");
    }

    // ==================== markerSession-backed presentation reads (AUD-010 Phase 1c) ====================

    [Test]
    public void SetDisplayText_NotOnMarker_ReadsMarkersRemainingFromTheBoundSession()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 4 };
        PrepareMarkerForSessionDependentBehavior(marker, session);

        InvokePrivateMethod(marker, "setDisplayText", false);

        Text text = (Text)GetPrivateField(marker, "displayCurrentMarkerStats");
        Assert.That(text.text, Does.Contain("markers remaining : 4"),
            "marker presentation must consume the bound session's live count, not a concrete manager");
    }

    // ==================== CompleteMarker / IShotMarkerSession completion (AUD-010 Phase 1c) ====================

    [Test]
    public void CompleteMarker_NonFinalMarker_DecrementsRecordsOneCompletionAndDoesNotRequestEnd()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 2 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;

        InvokePrivateMethod(marker, "CompleteMarker", false);

        Assert.That(session.MarkersRemaining, Is.EqualTo(1));
        Assert.That(session.CompletionRecords, Is.EqualTo(1));
        Assert.That(session.EndRequests, Is.EqualTo(0), "markers remain - match end must not be requested");
        Assert.IsFalse(marker.MarkerEnabled);
    }

    [Test]
    public void CompleteMarker_FinalMarker_DecrementsToZeroRecordsOneCompletionAndRequestsEndOnce()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 1 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;

        InvokePrivateMethod(marker, "CompleteMarker", false);

        Assert.That(session.MarkersRemaining, Is.EqualTo(0));
        Assert.That(session.CompletionRecords, Is.EqualTo(1));
        Assert.That(session.EndRequests, Is.EqualTo(1));
    }

    [Test]
    public void CompleteMarker_HidesSprite()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 2 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;
        SpriteRenderer spriteRenderer = (SpriteRenderer)GetPrivateField(marker, "spriteRenderer");

        InvokePrivateMethod(marker, "CompleteMarker", false);

        Assert.That(spriteRenderer.color.a, Is.EqualTo(0f));
    }

    [Test]
    public void CompleteMarker_RequestsMatchEndOnlyAfterPresentationAlreadyReflectsCompletion()
    {
        // Pins the ordering CompleteMarker's own doc comment describes: markerEnabled/decrement/sprite/
        // display must all already be applied before RequestMatchEnd is called. Uses only observable
        // marker state (the display text, the sprite alpha) rather than production instrumentation.
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 1 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;
        Text text = (Text)GetPrivateField(marker, "displayCurrentMarkerStats");
        SpriteRenderer spriteRenderer = (SpriteRenderer)GetPrivateField(marker, "spriteRenderer");
        string textAtEndRequest = null;
        float alphaAtEndRequest = -1f;
        session.OnRequestMatchEnd = () =>
        {
            textAtEndRequest = text.text;
            alphaAtEndRequest = spriteRenderer.color.a;
        };

        InvokePrivateMethod(marker, "CompleteMarker", false);

        Assert.That(session.EndRequests, Is.EqualTo(1), "precondition: end must have actually been requested for this assertion to mean anything");
        Assert.That(textAtEndRequest, Does.Contain("markers remaining : 0"),
            "presentation must already reflect the completed count before match end is requested");
        Assert.That(alphaAtEndRequest, Is.EqualTo(0f),
            "the sprite must already be hidden before match end is requested");
    }

    // ==================== Update() completion branches (AUD-010 Phase 1c) ====================

    [Test]
    public void Update_PointContestMode_ReadyFinalAttempt_CompletesMarkerExactlyOnce()
    {
        BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 1);
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 2 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;
        FakeShooterActor readyActor = new FakeShooterActor { HasBasketball = false, InAir = false };
        BasketBallState readyState = MakeState("shooter-state");
        readyState.InAir = false;
        marker.RegisterAttempt(new FakeRuntime { ParticipantId = 0, Actor = readyActor, State = readyState });

        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = true;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = false;

            InvokePrivateMethod(marker, "Update");

            Assert.That(session.CompletionRecords, Is.EqualTo(1));
            Assert.That(session.MarkersRemaining, Is.EqualTo(1));
            Assert.That(session.EndRequests, Is.EqualTo(0), "one marker remaining after this one - match end must not be requested");
            Assert.IsFalse(marker.MarkerEnabled);
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void Update_NonPointMode_ShotMadeReachesMax_CompletesMarkerExactlyOnce()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 1 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.MarkerEnabled = true;
        SetPrivateField(marker, "maxShotMade", 3);
        marker.ShotMade = 3;

        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            GameOptions.gameModeAllPointContest = false;

            InvokePrivateMethod(marker, "Update");

            Assert.That(session.CompletionRecords, Is.EqualTo(1));
            Assert.That(session.MarkersRemaining, Is.EqualTo(0));
            Assert.That(session.EndRequests, Is.EqualTo(1), "this was the last marker - match end must be requested exactly once");
            Assert.IsFalse(marker.MarkerEnabled);
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    // ==================== OnTriggerExit real path (AUD-010 Phase 1c) ====================
    //
    // Previously not exercised here at all (see file header) because setDisplayText reached
    // GameRules.instance. Now exercised directly against a bound FakeShotMarkerSession, preserving the
    // existing multi-occupant invariant OnTriggerEnter already pins above.

    [Test]
    public void OnTriggerExit_HumanHitbox_UpdatesParticipantStateAndPreservesMultiOccupantPresence()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 3 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        BasketBallState humanA = MakeState("human-a-state");
        BasketBallState humanB = MakeState("human-b-state");
        GameObject hitboxA = MakeHumanParticipant("human-a", humanA);
        GameObject hitboxB = MakeHumanParticipant("human-b", humanB);
        InvokeOnTriggerEnter(marker, hitboxA.GetComponent<Collider>());
        InvokeOnTriggerEnter(marker, hitboxB.GetComponent<Collider>());
        Assert.IsTrue(marker.PlayerOnMarker);

        InvokeOnTriggerExit(marker, hitboxA.GetComponent<Collider>());

        Assert.IsFalse(humanA.PlayerOnMarker);
        Assert.IsNull(humanA.CurrentShotMarker);
        Assert.IsTrue(marker.PlayerOnMarker, "human B's collider is still inside the marker");
        Assert.IsTrue(humanB.PlayerOnMarker);

        InvokeOnTriggerExit(marker, hitboxB.GetComponent<Collider>());

        Assert.IsFalse(marker.PlayerOnMarker);
        Assert.IsFalse(humanB.PlayerOnMarker);
        Assert.IsNull(humanB.CurrentShotMarker);
    }

    [Test]
    public void OnTriggerExit_CpuHitbox_UpdatesParticipantStateAndClearsLocked()
    {
        BasketBallShotMarker marker = MakeMarker("marker");
        FakeShotMarkerSession session = new FakeShotMarkerSession { MarkersRemaining = 3 };
        PrepareMarkerForSessionDependentBehavior(marker, session);
        marker.locked = true;
        BasketBallState cpuState = MakeState("cpu-state");
        GameObject hitbox = MakeCpuParticipant("cpu", cpuState);
        InvokeOnTriggerEnter(marker, hitbox.GetComponent<Collider>());
        Assert.IsTrue(marker.AutoPlayerOnMarker);

        InvokeOnTriggerExit(marker, hitbox.GetComponent<Collider>());

        Assert.IsFalse(marker.AutoPlayerOnMarker);
        Assert.IsFalse(cpuState.PlayerOnMarker);
        Assert.IsNull(cpuState.CurrentShotMarker);
        Assert.IsFalse(marker.locked, "OnTriggerExit's CPU branch must clear the locked flag");
    }

    // ==================== test doubles ====================

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; }
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir { get; set; }
        public bool InDunkState => false;
        public float DistanceFromRim => 0f;
        public ShooterAttributes ShooterAttributes => default;
        public int Clutch => 0;
        public float ShotMeterSliderValue => 0f;
        public bool ShotMeterEnded => true;
        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() { }
    }

    private sealed class FakeRuntime : IBasketballRuntime
    {
        public int ParticipantId { get; set; }
        public bool IsCpu { get; set; }
        public bool IsPrimary { get; set; }
        public GameObject OwnerActor { get; set; }
        public IShooterActor Actor { get; set; } = new FakeShooterActor();
        public BasketBallState State { get; set; }
        public GameStats Stats { get; set; }
        public float LastShotDistance => 0f;

        public void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor)
        {
        }
    }

    /// <summary>
    /// AUD-010 Phase 1c: the fake <see cref="IShotMarkerSession"/> this file's marker-session tests
    /// drive <see cref="BasketBallShotMarker"/> against. <see cref="RecordMarkerCompleted"/> mirrors
    /// <c>GameRules</c>' own explicit implementation exactly (decrement, then
    /// <see cref="MatchEndConditions.MarkersCleared"/>) rather than reimplementing the rule
    /// differently. <see cref="OnRequestMatchEnd"/> is an optional test-only hook - not production
    /// instrumentation - used only by the ordering test to observe the marker's own state at the
    /// moment match end is requested.
    /// </summary>
    private sealed class FakeShotMarkerSession : IShotMarkerSession
    {
        public int MarkersRemaining { get; set; }
        public int CompletionRecords { get; private set; }
        public int EndRequests { get; private set; }
        public System.Action OnRequestMatchEnd;

        public bool RecordMarkerCompleted()
        {
            CompletionRecords++;
            MarkersRemaining--;
            return MatchEndConditions.MarkersCleared(MarkersRemaining);
        }

        public void RequestMatchEnd()
        {
            EndRequests++;
            OnRequestMatchEnd?.Invoke();
        }
    }
}
