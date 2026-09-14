using System;
using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 57: <see cref="SpawnCoordinator.BindPlayerCollisionsContext"/> no longer
/// implements the killed-on-idle write (<c>GameRules.instance.killedOnIdle = true</c>) itself - it only
/// forwards whatever <c>markKilledOnIdle</c> delegate it was constructed with into
/// <c>PlayerCollisions.BindKilledOnIdleCallback</c>. Mirrors the shape of
/// <see cref="Level5SpawnCoordinatorFallRespawnCompositionTests"/>: drives the real private
/// <c>RegisterHuman</c>/<c>RegisterCpu</c> composition paths via reflection, then separately exercises
/// the real production adapter (<see cref="GameLevelManager"/>'s own <c>MarkKilledOnIdle</c>) against a
/// live <see cref="GameRules"/> singleton.
/// </summary>
public class Level5SpawnCoordinatorKilledOnIdleCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private MethodInfo registerHuman;
    private MethodInfo registerCpu;

    [SetUp]
    public void SetUp()
    {
        // RegisterHuman also drives InitializeHumanProfile/PrepareHumanMatchContext; clearing keeps
        // that path deterministic regardless of what ran before this test (mirrors
        // Level5SpawnCoordinatorFallRespawnCompositionTests) - this fixture supplies no
        // loadedCharacterProfileResolver, so a stray configured match would otherwise log an unrelated
        // "no human match context prepared" error.
        ActiveMatch.Clear();

        registerHuman = typeof(SpawnCoordinator).GetMethod("RegisterHuman", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerHuman, "SpawnCoordinator.RegisterHuman must exist");
        registerCpu = typeof(SpawnCoordinator).GetMethod("RegisterCpu", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerCpu, "SpawnCoordinator.RegisterCpu must exist");
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();
        GameRules.instance = null;

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

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    private static SpawnCoordinator Coordinator(Action markKilledOnIdle)
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            new PlayerRegistry(),
            new ResolvedMatchRules(),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            markKilledOnIdle: markKilledOnIdle);
    }

    /// <summary>Mirrors <see cref="Level5SpawnCoordinatorFallRespawnCompositionTests.SpawnHumanParticipant"/>:
    /// a production-shaped human participant with <see cref="PlayerCollisions"/> authored on a hitbox
    /// child, exactly where every production player prefab carries it.</summary>
    private GameObject SpawnHumanParticipant(string name, out PlayerCollisions collisions)
    {
        GameObject actorGo = Spawn(name);
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject hitbox = new GameObject(name + "-hitbox");
        hitbox.transform.SetParent(actorGo.transform);
        hitbox.tag = "playerHitbox";
        collisions = hitbox.AddComponent<PlayerCollisions>();

        return actorGo;
    }

    private GameObject SpawnCpuParticipant(string name)
    {
        GameObject actorGo = Spawn(name);
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();
        return actorGo;
    }

    // ==================== SpawnCoordinator forwarding ====================

    [Test]
    public void RegisterHuman_ForwardsExactSuppliedActionIntoPlayerCollisions()
    {
        Action callback = () => { };
        SpawnCoordinator coordinator = Coordinator(callback);
        GameObject actorGo = SpawnHumanParticipant("human-forward", out PlayerCollisions collisions);

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, (PlayerSlot)null });

        Assert.AreSame(callback, GetPrivateField(collisions, "markKilledOnIdle"),
            "RegisterHuman must forward the exact supplied markKilledOnIdle delegate, unexamined.");
    }

    [Test]
    public void RegisterHuman_NoCallbackSupplied_LeavesPlayerCollisionsUnbound()
    {
        SpawnCoordinator coordinator = Coordinator(null);
        GameObject actorGo = SpawnHumanParticipant("human-unbound", out PlayerCollisions collisions);

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, (PlayerSlot)null });

        Assert.IsNull(GetPrivateField(collisions, "markKilledOnIdle"),
            "a coordinator built without a callback must leave PlayerCollisions unbound rather than inventing one.");
    }

    [Test]
    public void RegisterCpu_HasNoKilledOnIdleBindingPoint()
    {
        // AutoPlayerCollisions is the CPU twin of PlayerCollisions and never read GameRules.killedOnIdle
        // to begin with (unlike its human twin) - this is unchanged by this slice. Proven structurally:
        // the type declares no such binding method for SpawnCoordinator to call in the first place.
        MethodInfo bindMethod = typeof(AutoPlayerCollisions).GetMethod("BindKilledOnIdleCallback");
        Assert.IsNull(bindMethod, "AutoPlayerCollisions must not gain a killed-on-idle binding point - that behavior is human-only.");
    }

    [Test]
    public void RegisterCpu_DoesNotThrowOrRequireTheCallback()
    {
        SpawnCoordinator coordinator = Coordinator(() => { });
        GameObject actorGo = SpawnCpuParticipant("cpu-unaffected");

        Assert.DoesNotThrow(() => registerCpu.Invoke(coordinator, new object[] { actorGo, 0 }));
    }

    // ================ production legacy adapter: GameLevelManager.MarkKilledOnIdle ================

    private static MethodInfo AdapterMethod()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod("MarkKilledOnIdle", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager.MarkKilledOnIdle must exist as the production killed-on-idle adapter");
        return method;
    }

    private static void InvokeAdapter()
    {
        AdapterMethod().Invoke(null, null);
    }

    [Test]
    public void Adapter_SetsCurrentGameRulesInstanceKilledOnIdle()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;

        InvokeAdapter();

        Assert.IsTrue(rules.killedOnIdle, "the adapter must set the current GameRules.instance's killedOnIdle field to true.");
    }

    [Test]
    public void Adapter_AbsentGameRulesInstance_PreservesExistingFailureSemantics()
    {
        GameRules.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => InvokeAdapter(),
            "an absent GameRules.instance must still throw exactly as the former direct write did - "
            + "this slice must not silently make the write safe.");
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    [Test]
    public void Adapter_ReplacedGameRulesInstance_LaterCallAffectsTheReplacement()
    {
        GameRules first = Spawn("game-rules-first").AddComponent<GameRules>();
        GameRules.instance = first;
        InvokeAdapter();
        Assert.IsTrue(first.killedOnIdle, "precondition: the first instance must be reached");

        GameRules second = Spawn("game-rules-second").AddComponent<GameRules>();
        GameRules.instance = second;
        InvokeAdapter();

        Assert.IsTrue(second.killedOnIdle,
            "a later call must observe the replaced singleton, not a stale reference to the first.");
    }
}
