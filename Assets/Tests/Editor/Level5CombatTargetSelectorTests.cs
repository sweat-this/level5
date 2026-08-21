using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Coverage for <see cref="CombatTargetSelector"/>, the shared target-selection policy introduced
/// for the Enemy/Bodyguard AI architecture work. Pure scoring logic, so it is exercised here with
/// lightweight fakes rather than the real EnemyController/BodyGuardController prefabs.
/// </summary>
public class Level5CombatTargetSelectorTests
{
    /// <summary>Minimal ICombatAgent double - a real Component so CanAct/activeInHierarchy behave exactly like production.</summary>
    private sealed class FakeCombatAgent : MonoBehaviour, ICombatAgent
    {
        public bool CanActValue = true;
        public GameObject CombatObject => gameObject;
        public Transform CombatTransform => transform;
        public bool CanAct => CanActValue;
    }

    /// <summary>Minimal ICombatReservationState double - a sibling component, matching how EnemyDetection/BodyGuardDetection sit next to their controller.</summary>
    private sealed class FakeDetection : MonoBehaviour, ICombatReservationState
    {
        public bool HasAttackReservation { get; private set; }
        public int AttackPositionId { get; private set; } = -1;

        public void SetAttackReservation(bool active, int attackPositionId)
        {
            HasAttackReservation = active;
            AttackPositionId = attackPositionId;
        }
    }

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

    private FakeCombatAgent CreateAgent(string name, Vector3 position, bool canAct = true, bool active = true, bool reserved = false)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        go.transform.position = position;

        FakeCombatAgent agent = go.AddComponent<FakeCombatAgent>();
        agent.CanActValue = canAct;

        if (reserved)
        {
            go.AddComponent<FakeDetection>().SetAttackReservation(true, 0);
        }

        go.SetActive(active);
        return agent;
    }

    // ---------- SelectNearestValidTarget (enemy target acquisition) ----------

    [Test]
    public void NullCandidateListYieldsNoTarget()
    {
        Assert.That(CombatTargetSelector.SelectNearestValidTarget(null, Vector3.zero, null), Is.Null);
    }

    [Test]
    public void NullEntriesInTheListAreSkipped()
    {
        List<ICombatAgent> candidates = new List<ICombatAgent> { null };
        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Is.Null);
    }

    [Test]
    public void DeadCandidatesAreRejected()
    {
        FakeCombatAgent dead = CreateAgent("dead", new Vector3(1, 0, 0), canAct: false);
        List<ICombatAgent> candidates = new List<ICombatAgent> { dead };

        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Is.Null);
    }

    [Test]
    public void InactiveCandidatesAreRejected()
    {
        FakeCombatAgent inactive = CreateAgent("inactive", new Vector3(1, 0, 0), active: false);
        List<ICombatAgent> candidates = new List<ICombatAgent> { inactive };

        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Is.Null);
    }

    [Test]
    public void DestroyedCandidateIsRejectedNotThrown()
    {
        FakeCombatAgent destroyed = CreateAgent("destroyed", new Vector3(1, 0, 0));
        List<ICombatAgent> candidates = new List<ICombatAgent> { destroyed };

        // Capture the GameObject reference before destroying it - destroyed.gameObject would
        // itself throw MissingReferenceException once the component's native object is gone,
        // same as the production code path this test exists to verify.
        GameObject destroyedGameObject = destroyed.gameObject;
        Object.DestroyImmediate(destroyedGameObject);
        spawned.Remove(destroyedGameObject);

        Assert.That(() => CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Throws.Nothing);
        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Is.Null);
    }

    [Test]
    public void NearestValidCandidateWins()
    {
        FakeCombatAgent near = CreateAgent("near", new Vector3(2, 0, 0));
        FakeCombatAgent far = CreateAgent("far", new Vector3(10, 0, 0));
        List<ICombatAgent> candidates = new List<ICombatAgent> { far, near };

        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, null), Is.EqualTo(near));
    }

    [Test]
    public void CurrentTargetIsHeldAgainstANearEqualRival()
    {
        FakeCombatAgent current = CreateAgent("current", new Vector3(5, 0, 0));
        // just barely closer, but not enough to overcome the stickiness bonus
        FakeCombatAgent slightlyCloser = CreateAgent("rival", new Vector3(5 - CombatTargetSelector.TargetStickinessBonus * 0.5f, 0, 0));
        List<ICombatAgent> candidates = new List<ICombatAgent> { current, slightlyCloser };

        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, current), Is.EqualTo(current));
    }

    [Test]
    public void ACandidateThatIsGenuinelyBetterOverridesStickiness()
    {
        FakeCombatAgent current = CreateAgent("current", new Vector3(20, 0, 0));
        FakeCombatAgent muchCloser = CreateAgent("muchCloser", new Vector3(1, 0, 0));
        List<ICombatAgent> candidates = new List<ICombatAgent> { current, muchCloser };

        Assert.That(CombatTargetSelector.SelectNearestValidTarget(candidates, Vector3.zero, current), Is.EqualTo(muchCloser));
    }

    // ---------- SelectBodyguardThreat (STEP 3 threat hierarchy) ----------

    [Test]
    public void BodyguardThreatSelectorRejectsNullAndDeadAndInactive()
    {
        FakeCombatAgent dead = CreateAgent("dead", Vector3.zero, canAct: false);
        FakeCombatAgent inactive = CreateAgent("inactive", Vector3.zero, active: false);
        List<ICombatAgent> candidates = new List<ICombatAgent> { null, dead, inactive };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, Vector3.zero, Vector3.zero, null, protectionRadius: 5f);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ReservedThreatOutranksAMerelyNearbyOne()
    {
        Vector3 protectedActorPosition = Vector3.zero;

        // reserved, but standing off at range - "very high" tier
        FakeCombatAgent reserved = CreateAgent("reserved", new Vector3(4, 0, 0), reserved: true);
        // not reserved, just standing near the protected actor - "medium" tier
        FakeCombatAgent nearby = CreateAgent("nearby", new Vector3(1, 0, 0));

        List<ICombatAgent> candidates = new List<ICombatAgent> { nearby, reserved };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, guardPosition: new Vector3(10, 0, 0), protectedActorPosition, null, protectionRadius: 5f);

        Assert.That(result, Is.EqualTo(reserved),
            "a queued attacker must outrank a merely-nearby one, even if the guard has to travel further - queue order is not the whole story, but reservation state still matters");
    }

    [Test]
    public void ImminentThreatOutranksAFarAwayReservation()
    {
        Vector3 protectedActorPosition = Vector3.zero;

        // reserved AND already standing next to the protected actor - "highest" tier
        FakeCombatAgent imminent = CreateAgent("imminent", new Vector3(0.5f, 0, 0), reserved: true);
        // reserved but still approaching from range - "very high" tier
        FakeCombatAgent approaching = CreateAgent("approaching", new Vector3(8, 0, 0), reserved: true);

        List<ICombatAgent> candidates = new List<ICombatAgent> { approaching, imminent };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, guardPosition: new Vector3(20, 0, 0), protectedActorPosition, null, protectionRadius: 5f);

        Assert.That(result, Is.EqualTo(imminent));
    }

    [Test]
    public void FirstAddedCandidateIsNotAutomaticallyChosen()
    {
        // regression guard for "bodyguards select meaningful threats rather than simply attacking
        // the first queued enemy": the first entry in the candidate list is the weakest one here.
        FakeCombatAgent first = CreateAgent("first", new Vector3(50, 0, 0));
        FakeCombatAgent trueThreat = CreateAgent("trueThreat", new Vector3(1, 0, 0), reserved: true);
        List<ICombatAgent> candidates = new List<ICombatAgent> { first, trueThreat };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, guardPosition: Vector3.zero, protectedActorPosition: Vector3.zero, null, protectionRadius: 5f);

        Assert.That(result, Is.EqualTo(trueThreat));
        Assert.That(result, Is.Not.EqualTo(first));
    }

    [Test]
    public void NoValidThreatYieldsNoTarget()
    {
        List<ICombatAgent> candidates = new List<ICombatAgent>();

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, Vector3.zero, Vector3.zero, null, protectionRadius: 5f);

        Assert.That(result, Is.Null, "with nothing to fight, a bodyguard must fall back to protecting/following, not seek an arbitrary target");
    }

    [Test]
    public void BodyguardCurrentThreatIsHeldAgainstANearEqualRival()
    {
        Vector3 protectedActorPosition = Vector3.zero;
        FakeCombatAgent current = CreateAgent("current", new Vector3(3, 0, 0), reserved: true);
        FakeCombatAgent rival = CreateAgent("rival", new Vector3(2.9f, 0, 0), reserved: true);
        List<ICombatAgent> candidates = new List<ICombatAgent> { current, rival };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            candidates, guardPosition: new Vector3(10, 0, 0), protectedActorPosition, current, protectionRadius: 5f);

        Assert.That(result, Is.EqualTo(current));
    }

    // ---------- SelectBodyguardThreat actionable-pool overload (#57) ----------
    //
    // #57: CombatTargetSelector.SelectBodyguardThreat's reservation tier bonus (500, see
    // ReservedThreatScore) can outrank a merely-nearby candidate (100, NearProtectedActorScore) even
    // when the reserved candidate is far away and the nearby one is not. The overload under test
    // here lets a caller (BodyGuardController) restrict selection to a pre-filtered "actionable"
    // subset so a genuinely close threat cannot be hidden behind a distant reserved one.

    [Test]
    public void ActionablePoolOverloadPrefersAnActionableCandidateOverADistantReservedOne()
    {
        Vector3 protectedActorPosition = Vector3.zero;

        // reserved, but far from the protected actor and far from the guard - not actionable
        ICombatAgent distantReserved = CreateAgent("distantReserved", new Vector3(50, 0, 0), reserved: true);
        // unreserved, standing right next to the protected actor - actionable
        ICombatAgent nearActionable = CreateAgent("nearActionable", new Vector3(1, 0, 0));

        List<ICombatAgent> all = new List<ICombatAgent> { distantReserved, nearActionable };
        List<ICombatAgent> actionable = new List<ICombatAgent> { nearActionable };

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            all, actionable, guardPosition: new Vector3(2, 0, 0), protectedActorPosition, null,
            protectionRadius: 5f, out bool hasActionableThreat);

        Assert.That(result, Is.EqualTo(nearActionable),
            "an actionable candidate must not be hidden behind a distant reserved one, even though the reserved one scores higher in raw tier terms");
        Assert.That(hasActionableThreat, Is.True);
    }

    // ---------- IsImminentThreat (#57 review fix) ----------
    //
    // A caller's own actionable-candidate filter (BodyGuardController.IsActionableThreat) must be
    // able to treat this tier as unconditionally actionable, regardless of that caller's own
    // distance tuning - otherwise a bodyguard prefab authored with a maximumInterceptionDistance
    // smaller than ImminentThreatRange could filter the single highest-priority candidate out of
    // the actionable pool before selection ever sees it.

    [Test]
    public void IsImminentThreatIsTrueForAReservedCandidateWithinImminentThreatRange()
    {
        ICombatAgent imminent = CreateAgent("imminent", new Vector3(1f, 0, 0), reserved: true);

        Assert.That(
            CombatTargetSelector.IsImminentThreat(imminent, protectedActorPosition: Vector3.zero),
            Is.True,
            "reserved and within ImminentThreatRange (2.5) of the protected actor must be imminent");
    }

    [Test]
    public void IsImminentThreatIsFalseForAReservedCandidateOutsideImminentThreatRange()
    {
        ICombatAgent approaching = CreateAgent("approaching", new Vector3(8f, 0, 0), reserved: true);

        Assert.That(
            CombatTargetSelector.IsImminentThreat(approaching, protectedActorPosition: Vector3.zero),
            Is.False,
            "reserved but still approaching from range is not imminent - only ReservedThreatScore tier");
    }

    [Test]
    public void IsImminentThreatIsFalseForAnUnreservedCandidateEvenWhenAdjacent()
    {
        ICombatAgent adjacentButUnreserved = CreateAgent("adjacentButUnreserved", new Vector3(0.1f, 0, 0));

        Assert.That(
            CombatTargetSelector.IsImminentThreat(adjacentButUnreserved, protectedActorPosition: Vector3.zero),
            Is.False,
            "standing next to the protected actor without a reservation is NearProtectedActorScore tier, not imminent");
    }

    [Test]
    public void IsImminentThreatRejectsNullDeadAndInactiveCandidates()
    {
        ICombatAgent dead = CreateAgent("dead", Vector3.zero, canAct: false, reserved: true);
        ICombatAgent inactive = CreateAgent("inactive", Vector3.zero, active: false, reserved: true);

        Assert.That(CombatTargetSelector.IsImminentThreat(null, Vector3.zero), Is.False);
        Assert.That(CombatTargetSelector.IsImminentThreat(dead, Vector3.zero), Is.False);
        Assert.That(CombatTargetSelector.IsImminentThreat(inactive, Vector3.zero), Is.False);
    }

    [Test]
    public void ActionablePoolOverloadFallsBackToTheFullPoolWhenNothingIsActionable()
    {
        Vector3 protectedActorPosition = Vector3.zero;
        ICombatAgent onlyCandidate = CreateAgent("onlyCandidate", new Vector3(50, 0, 0), reserved: true);

        List<ICombatAgent> all = new List<ICombatAgent> { onlyCandidate };
        List<ICombatAgent> actionable = new List<ICombatAgent>();

        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            all, actionable, guardPosition: Vector3.zero, protectedActorPosition, null,
            protectionRadius: 5f, out bool hasActionableThreat);

        Assert.That(result, Is.EqualTo(onlyCandidate),
            "with nothing actionable, the controller must still track/face the best available candidate rather than going target-less");
        Assert.That(hasActionableThreat, Is.False,
            "the tracked candidate came from the full pool, not the actionable one - it must not be reported as actionable");
    }

    [Test]
    public void ActionablePoolOverloadReportsNoActionableThreatWhenNoCandidatesExistAtAll()
    {
        ICombatAgent result = CombatTargetSelector.SelectBodyguardThreat(
            new List<ICombatAgent>(), new List<ICombatAgent>(), Vector3.zero, Vector3.zero, null,
            protectionRadius: 5f, out bool hasActionableThreat);

        Assert.That(result, Is.Null);
        Assert.That(hasActionableThreat, Is.False);
    }
}
