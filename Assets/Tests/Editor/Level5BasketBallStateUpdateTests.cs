using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-010 Phase 2b0 regression coverage for <see cref="BasketBallState.Update"/>'s point/range
/// classification, now gated by an explicitly bound <see cref="ResolvedMatchRules"/> (see
/// <see cref="BasketBallState.BindMatchRules"/>) instead of <c>MatchRuntime.Rules.RequiresBasketball</c>.
/// Exercises the real <c>Update()</c> method directly - the distance math, the exact threshold
/// operators, and the <c>RequiresBasketball=false</c> no-op path are all migration-critical and must
/// not change behavior.
/// </summary>
public class Level5BasketBallStateUpdateTests
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

    private BasketBallState MakeBoundState(ResolvedMatchRules rules, float playerDistanceAlongZ, bool isCpu = false)
    {
        GameObject stateGo = Spawn("basketball-state");
        BasketBallState state = stateGo.AddComponent<BasketBallState>();

        GameObject player = Spawn("player");
        player.transform.position = new Vector3(0, 0, playerDistanceAlongZ);

        GameObject target = Spawn("basketBall_target");
        target.transform.position = Vector3.zero;

        state.BindOwner(isCpu, player);
        state.BindMatchRules(rules);
        state.Player = player;
        state.BasketBallTarget = target;

        return state;
    }

    private static void InvokeUpdate(BasketBallState state)
    {
        MethodInfo update = typeof(BasketBallState).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(update, "BasketBallState must declare Update()");
        update.Invoke(state, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    [Test]
    public void RequiresBasketballTrueClassifiesTwoPointRange()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: 1f);

        InvokeUpdate(state);

        Assert.IsTrue(state.TwoPoints);
        Assert.IsFalse(state.ThreePoints);
        Assert.IsFalse(state.FourPoints);
        Assert.IsFalse(state.SevenPoints);
        Assert.That(state.CurrentShotType, Is.EqualTo(2));
    }

    [Test]
    public void RequiresBasketballTrueClassifiesThreePointRange()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: Constants.DISTANCE_3point);

        InvokeUpdate(state);

        Assert.IsFalse(state.TwoPoints);
        Assert.IsTrue(state.ThreePoints);
        Assert.IsFalse(state.FourPoints);
        Assert.IsFalse(state.SevenPoints);
        Assert.That(state.CurrentShotType, Is.EqualTo(3));
    }

    [Test]
    public void RequiresBasketballTrueClassifiesFourPointRange()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: Constants.DISTANCE_4point);

        InvokeUpdate(state);

        Assert.IsFalse(state.ThreePoints);
        Assert.IsTrue(state.FourPoints);
        Assert.IsFalse(state.SevenPoints);
        Assert.That(state.CurrentShotType, Is.EqualTo(4));
    }

    [Test]
    public void RequiresBasketballTrueClassifiesSevenPointRange()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: Constants.DISTANCE_7point + 1f);

        InvokeUpdate(state);

        Assert.IsFalse(state.FourPoints);
        Assert.IsTrue(state.SevenPoints);
        Assert.That(state.CurrentShotType, Is.EqualTo(7));
    }

    /// <summary>
    /// Pins the pre-existing boundary gap exactly at <c>DISTANCE_7point</c>: <c>FourPoints</c> requires
    /// <c>&lt; DISTANCE_7point</c> and <c>SevenPoints</c> requires <c>&gt; DISTANCE_7point</c>, so a shot
    /// from exactly that distance satisfies neither. Not a bug this migration fixes - AUD-010 Phase 2b0
    /// is a rule-source migration only and must not change threshold operators.
    /// </summary>
    [Test]
    public void RequiresBasketballTrueLeavesExactSevenPointBoundaryUnclassified()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: Constants.DISTANCE_7point);

        InvokeUpdate(state);

        Assert.IsFalse(state.FourPoints, "exact DISTANCE_7point fails the FourPoints '< DISTANCE_7point' bound");
        Assert.IsFalse(state.SevenPoints, "exact DISTANCE_7point fails the SevenPoints '> DISTANCE_7point' bound");
    }

    [Test]
    public void RequiresBasketballFalseLeavesPreloadedStateUnchanged()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: false);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: 1f);

        state.TwoPoints = true;
        state.ThreePoints = true;
        state.FourPoints = false;
        state.SevenPoints = true;
        state.PlayerDistanceFromRim = 999f;
        SetPrivateField(state, "_currentShotType", 42);

        InvokeUpdate(state);

        Assert.IsTrue(state.TwoPoints, "RequiresBasketball=false must not clear pre-existing state");
        Assert.IsTrue(state.ThreePoints, "RequiresBasketball=false must not clear pre-existing state");
        Assert.IsFalse(state.FourPoints, "RequiresBasketball=false must not clear pre-existing state");
        Assert.IsTrue(state.SevenPoints, "RequiresBasketball=false must not clear pre-existing state");
        Assert.That(state.PlayerDistanceFromRim, Is.EqualTo(999f), "RequiresBasketball=false must not recompute distance");
        Assert.That(state.CurrentShotType, Is.EqualTo(42), "RequiresBasketball=false must not reclassify shot type");
    }

    [Test]
    public void RequiresBasketballGateAppliesEquallyToCpuRole()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(requiresBasketball: true);
        BasketBallState state = MakeBoundState(rules, playerDistanceAlongZ: Constants.DISTANCE_3point, isCpu: true);

        InvokeUpdate(state);

        Assert.IsTrue(state.ThreePoints, "the RequiresBasketball gate is shared state behavior, not role-dependent");
        Assert.That(state.CurrentShotType, Is.EqualTo(3));
    }
}
