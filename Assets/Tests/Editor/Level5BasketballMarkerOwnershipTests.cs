using System.Reflection;
using System.Collections.Generic;
using Level5.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

/// <summary>
/// AUD-010 Phase 1c: marker occupancy, the launch-time marker snapshot and final-attempt ownership
/// are now the participant's own <see cref="BasketBallState"/>/<see cref="BasketBallShotMarker"/>
/// state, not an id resolved through <c>GameRules.BasketBallShotMarkersList</c> or
/// <c>GameLevelManager.instance.players[0]</c>.
///
/// <c>BasketBallShotMarker.OnTriggerExit</c> and <c>Update()</c> are not exercised directly here -
/// both reach <c>GameRules.instance</c> (display text / MarkersRemaining / IsGameOver), and standing
/// up a real <c>GameRules</c> singleton pulls in <c>MatchController</c>, <c>MatchHudPresenter</c> and
/// <c>ProgressionService</c> with no existing lightweight test seam (no test in this suite
/// instantiates <c>GameRules</c>). <c>OnTriggerEnter</c> - the new participant-resolution wiring this
/// slice adds - has no such dependency and is exercised directly below; the occupancy/overlap/
/// snapshot invariants it delegates to are exercised directly against <see cref="BasketBallState"/>
/// and <see cref="BasketBallShotMarker.RegisterAttempt"/>, which also have none.
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
}
