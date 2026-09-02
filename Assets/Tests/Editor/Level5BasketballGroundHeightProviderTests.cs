using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 1c: <see cref="BasketBall"/>'s no-active-Terrain drop-shadow fallback now reads a
/// live <see cref="IGroundHeightProvider"/> instead of <c>GameLevelManager.instance.TerrainHeight</c>
/// directly, bound once by <see cref="SpawnCoordinator.GiveBall"/>
/// (<see cref="BasketBall.BindGroundHeightProvider"/>) - mirroring the shape
/// <see cref="Level5ShotMeterOwnershipTests"/> and <see cref="Level5RangeMeterOwnershipTests"/>
/// already establish for this migration's other explicit-binding slices.
///
/// The critical compatibility invariant this covers beyond the usual bind/rebind/null-guard shape:
/// the provider must be read live at the point of use, never snapshotted at bind time - see
/// <see cref="ResolveDropShadowHeight_NoActiveTerrain_ReadsTheBoundProviderLiveNotSnapshotted"/>.
/// </summary>
public class Level5BasketballGroundHeightProviderTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerRegistry registry;
    private MethodInfo giveBall;

    [SetUp]
    public void SetUp()
    {
        registry = new PlayerRegistry();
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
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private SpawnCoordinator MakeCoordinator(IGroundHeightProvider provider)
    {
        // The provider parameter defaults to null on SpawnCoordinator's own constructor, so passing
        // it straight through covers both the provider and provider-less cases with one call.
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            provider);

        giveBall = typeof(SpawnCoordinator).GetMethod("GiveBall", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(giveBall, "SpawnCoordinator.GiveBall must exist");
        return coordinator;
    }

    private void InvokeGiveBall(SpawnCoordinator coordinator, int slotId, GameObject prefab, bool forCpu)
    {
        giveBall.Invoke(coordinator, new object[] { slotId, prefab, Vector3.zero, forCpu });
    }

    private PlayerIdentifier RegisterHumanParticipant(int pid)
    {
        GameObject actorGo = Spawn($"human-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, false);
        identifier.player = actorGo;
        identifier.setPlayer(actorGo);
        registry.Add(identifier);
        return identifier;
    }

    private PlayerIdentifier RegisterCpuParticipant(int pid)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, true);
        identifier.autoPlayer = actorGo;
        identifier.setAutoPlayer(actorGo);
        registry.Add(identifier);
        return identifier;
    }

    private static void InvokeStart(MonoBehaviour behaviour)
    {
        MethodInfo start = behaviour.GetType().GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(start, $"{behaviour.GetType().Name} must declare Start()");
        start.Invoke(behaviour, null);
    }

    private static float InvokeResolveDropShadowHeight(BasketBall ball)
    {
        MethodInfo resolve = typeof(BasketBall).GetMethod("ResolveDropShadowHeight", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(resolve, "BasketBall must declare ResolveDropShadowHeight()");
        return (float)resolve.Invoke(ball, null);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    // ==================== BindGroundHeightProvider ====================

    [Test]
    public void BindGroundHeightProvider_ValidProvider_Binds()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();
        FakeGroundHeightProvider provider = new FakeGroundHeightProvider { GroundHeight = 3f };

        ball.BindGroundHeightProvider(provider);

        Assert.AreSame(provider, GetPrivateField(ball, "groundHeightProvider"));
    }

    [Test]
    public void BindGroundHeightProvider_NullProvider_LogsAndLeavesUnbound()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("null ground-height provider"));
        ball.BindGroundHeightProvider(null);

        Assert.IsNull(GetPrivateField(ball, "groundHeightProvider"));
    }

    [Test]
    public void BindGroundHeightProvider_SecondCall_IsRejectedAndOriginalProviderRetained()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();
        FakeGroundHeightProvider first = new FakeGroundHeightProvider { GroundHeight = 1f };
        FakeGroundHeightProvider second = new FakeGroundHeightProvider { GroundHeight = 2f };
        ball.BindGroundHeightProvider(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound ground-height provider"));
        ball.BindGroundHeightProvider(second);

        Assert.AreSame(first, GetPrivateField(ball, "groundHeightProvider"),
            "a second BindGroundHeightProvider call must not overwrite the original binding");
    }

    // ==================== Start() composition validation ====================

    [Test]
    public void Start_OwnerBoundButNoGroundHeightProviderBound_FailsClosed()
    {
        GameObject actorGo = Spawn("actor");
        actorGo.AddComponent<CharacterProfile>();
        PlayerController controller = actorGo.AddComponent<PlayerController>();

        GameObject ballGo = Spawn("ball-no-provider");
        BasketBall ball = ballGo.AddComponent<BasketBall>();
        ballGo.AddComponent<BasketBallState>();
        ball.BindOwner(0, false, true, actorGo, controller);

        LogAssert.Expect(LogType.Error, new Regex("no bound ground-height provider"));
        InvokeStart(ball);

        Assert.IsFalse(ballGo.activeSelf,
            "a human ball with a bound owner but no bound ground-height provider must deactivate its "
            + "GameObject, the same fail-closed shape as a missing owner");
    }

    // ==================== Live-read semantics ====================

    [Test]
    public void ResolveDropShadowHeight_NoActiveTerrain_ReadsTheBoundProviderLiveNotSnapshotted()
    {
        // Proves the critical compatibility invariant: binding a provider reference must not snapshot
        // its value. GameLevelManager's own terrainHeight changes after spawn time (its Start() sets
        // it from the primary participant's actual Y) - BasketBall.Update() must observe that later
        // value, not whatever GroundHeight returned at bind time.
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();
        FakeGroundHeightProvider provider = new FakeGroundHeightProvider { GroundHeight = 10f };
        ball.BindGroundHeightProvider(provider);

        float beforeChange = InvokeResolveDropShadowHeight(ball);
        Assert.That(beforeChange, Is.EqualTo(10.02f).Within(0.0001f));

        provider.GroundHeight = 250f;
        float afterChange = InvokeResolveDropShadowHeight(ball);

        Assert.That(afterChange, Is.EqualTo(250.02f).Within(0.0001f),
            "BasketBall must read IGroundHeightProvider.GroundHeight live at the point of use, not a "
            + "value captured when the provider was bound");
    }

    // ==================== SpawnCoordinator / GiveBall wiring ====================

    [Test]
    public void GiveBall_HumanBall_ReceivesTheCoordinatorsGroundHeightProvider()
    {
        FakeGroundHeightProvider provider = new FakeGroundHeightProvider { GroundHeight = 5f };
        SpawnCoordinator coordinator = MakeCoordinator(provider);
        RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        InvokeGiveBall(coordinator, 0, humanPrefab, forCpu: false);

        GameObject ball = registry.GetBySlot(0).basketball;
        Assert.IsNotNull(ball);
        spawned.Add(ball);

        BasketBall basketBall = ball.GetComponent<BasketBall>();
        Assert.AreSame(provider, GetPrivateField(basketBall, "groundHeightProvider"));
    }

    [Test]
    public void GiveBall_CpuBall_IsUnaffectedByGroundHeightProviderWiring()
    {
        FakeGroundHeightProvider provider = new FakeGroundHeightProvider { GroundHeight = 5f };
        SpawnCoordinator coordinator = MakeCoordinator(provider);
        RegisterHumanParticipant(pid: 0);
        RegisterCpuParticipant(pid: 1);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        // No LogAssert.Expect: an unexpected Debug.LogError fails an EditMode test by default, so a
        // regression that starts requiring/touching a ground-height provider for a CPU ball fails here.
        Assert.DoesNotThrow(() => InvokeGiveBall(coordinator, 1, cpuPrefab, forCpu: true));

        GameObject ball = registry.GetBySlot(1).autoBasketball;
        Assert.IsNotNull(ball);
        spawned.Add(ball);
        Assert.IsNull(ball.GetComponent<BasketBall>(), "a CPU ball must not carry a human BasketBall component");
    }

    [Test]
    public void GiveBall_CoordinatorBuiltWithoutAProvider_HumanBallLogsOnBindAndFailsClosedOnStart()
    {
        // AUD-010 Phase 1c compatibility requirement: a coordinator built through the
        // provider-less overload (as several existing tests do) must not silently invent a fallback
        // for a human ball - it must fail clearly, exactly as an unbound owner does.
        SpawnCoordinator coordinator = MakeCoordinator(provider: null);
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        LogAssert.Expect(LogType.Error, new Regex("null ground-height provider"));
        InvokeGiveBall(coordinator, 0, humanPrefab, forCpu: false);

        GameObject ball = owner.basketball;
        Assert.IsNotNull(ball);
        spawned.Add(ball);

        BasketBall basketBall = ball.GetComponent<BasketBall>();
        Assert.IsNull(GetPrivateField(basketBall, "groundHeightProvider"));

        LogAssert.Expect(LogType.Error, new Regex("no bound ground-height provider"));
        InvokeStart(basketBall);

        Assert.IsFalse(ball.activeSelf,
            "a human ball spawned through a provider-less coordinator must fail closed in Start(), "
            + "not invent a fallback height");
    }

    [Test]
    public void SpawnCoordinator_ParticipantWithNoBasketballIsUnaffectedByGroundHeightProviderWiring()
    {
        FakeGroundHeightProvider provider = new FakeGroundHeightProvider { GroundHeight = 5f };
        SpawnCoordinator coordinator = MakeCoordinator(provider);
        RegisterHumanParticipant(pid: 0);

        // A defensive/no-ball CPU: RegisterCpu runs, but GiveBall never does for it. Nothing about
        // ground-height wiring should be reachable or required for a participant with no basketball.
        MethodInfo registerCpu = typeof(SpawnCoordinator).GetMethod("RegisterCpu", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerCpu, "SpawnCoordinator.RegisterCpu must exist");

        GameObject defenderGo = Spawn("defender");
        defenderGo.AddComponent<CharacterProfile>();
        defenderGo.AddComponent<AutoPlayerController>();
        defenderGo.AddComponent<PlayerIdentifier>();

        Assert.DoesNotThrow(() => registerCpu.Invoke(coordinator, new object[] { defenderGo, 1 }));
        Assert.IsNull(registry.GetBySlot(1).autoBasketball);
    }

    // ==================== test doubles ====================

    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight { get; set; }
    }
}
