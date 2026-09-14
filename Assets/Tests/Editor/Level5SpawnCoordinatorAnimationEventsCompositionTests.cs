using System;
using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 55: <see cref="SpawnCoordinator"/> no longer implements the projectile-spawn
/// or has-auto-player callbacks it hands to <see cref="PlayerAnimationEvents"/> itself - it only
/// distributes the <c>projectileSpawner</c>/<c>hasAutoPlayerReader</c> delegates it was constructed
/// with, replacing its former direct <c>ProjectilePool.Spawn</c> call and
/// <c>GameLevelManager.instance.AutoPlayer</c> read.
///
/// These tests drive the real private <c>BindPlayerAnimationEventsContext</c> through reflection (the
/// sole production path, invoked from <c>RegisterHuman</c>/<c>RegisterCpu</c>/<c>SpawnCheerleader</c>)
/// against manufactured participant hierarchies, then separately exercise the real production adapters
/// (<see cref="GameLevelManager"/>'s own <c>SpawnProjectileForAnimationEvents</c>/
/// <c>HasAutoPlayerForAnimationEvents</c>) directly - so neither the seam's contract nor its one
/// production implementation goes untested. Mirrors the shape of
/// <c>Level5SpawnCoordinatorCampaignCpuPrefabResolverTests</c> (Slice 54's equivalent seam).
/// </summary>
public class Level5SpawnCoordinatorAnimationEventsCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private MethodInfo bindPlayerAnimationEventsContext;
    private GameLevelManager savedInstance;

    [SetUp]
    public void SetUp()
    {
        bindPlayerAnimationEventsContext = typeof(SpawnCoordinator).GetMethod(
            "BindPlayerAnimationEventsContext", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(bindPlayerAnimationEventsContext, "SpawnCoordinator.BindPlayerAnimationEventsContext must exist");

        // The production-adapter tests below write GameLevelManager.instance directly; save/restore
        // keeps this fixture from leaking that state into whatever else runs in the same Editor test
        // session, mirroring how Level5SpawnCoordinatorCampaignCpuPrefabResolverTests saves/restores
        // GameOptions' campaign globals.
        savedInstance = GameLevelManager.instance;
    }

    [TearDown]
    public void TearDown()
    {
        GameLevelManager.instance = savedInstance;

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

    /// <summary>Creates a GameObject with a GameLevelManager component without ever running its Awake() -
    /// a GameObject that starts inactive defers every component's Awake() until it is activated, and
    /// these tests never activate it. Awake() would otherwise reach for scene spawn points and
    /// MatchRuntime state this fixture never sets up.</summary>
    private GameLevelManager SpawnManagerWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<GameLevelManager>();
    }

    private static SpawnCoordinator Coordinator(
        Func<GameObject, Vector3, Quaternion, GameObject> projectileSpawner = null,
        Func<bool> hasAutoPlayerReader = null)
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            new PlayerRegistry(),
            new ResolvedMatchRules(),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            null,
            null,
            projectileSpawner,
            hasAutoPlayerReader);
    }

    private void InvokeBind(SpawnCoordinator coordinator, GameObject participant)
    {
        bindPlayerAnimationEventsContext.Invoke(coordinator, new object[] { participant });
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} must exist");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} must exist");
        field.SetValue(target, value);
    }

    // ==================== binding ====================

    [Test]
    public void BindsBothCallbacksToAChildPlayerAnimationEvents()
    {
        GameObject root = Spawn("participant");
        GameObject child = Spawn("animator-child");
        child.transform.SetParent(root.transform);
        PlayerAnimationEvents events = child.AddComponent<PlayerAnimationEvents>();

        Func<GameObject, Vector3, Quaternion, GameObject> spawner = (prefab, position, rotation) => null;
        Func<bool> reader = () => true;
        SpawnCoordinator coordinator = Coordinator(spawner, reader);

        InvokeBind(coordinator, root);

        Assert.AreSame(spawner, GetPrivateField(events, "projectileSpawner"));
        Assert.AreSame(reader, GetPrivateField(events, "hasAutoPlayerReader"));
    }

    [Test]
    public void BindsBothCallbacksToEveryDescendant_IncludingInactive()
    {
        GameObject root = Spawn("participant");

        GameObject activeChild = Spawn("active-child");
        activeChild.transform.SetParent(root.transform);
        PlayerAnimationEvents activeEvents = activeChild.AddComponent<PlayerAnimationEvents>();

        GameObject inactiveChild = Spawn("inactive-child");
        inactiveChild.transform.SetParent(root.transform);
        PlayerAnimationEvents inactiveEvents = inactiveChild.AddComponent<PlayerAnimationEvents>();
        inactiveChild.SetActive(false);

        Func<GameObject, Vector3, Quaternion, GameObject> spawner = (prefab, position, rotation) => null;
        Func<bool> reader = () => false;
        SpawnCoordinator coordinator = Coordinator(spawner, reader);

        InvokeBind(coordinator, root);

        Assert.AreSame(spawner, GetPrivateField(activeEvents, "projectileSpawner"),
            "the active descendant must receive the projectile spawner.");
        Assert.AreSame(reader, GetPrivateField(activeEvents, "hasAutoPlayerReader"),
            "the active descendant must receive the auto-player reader.");
        Assert.AreSame(spawner, GetPrivateField(inactiveEvents, "projectileSpawner"),
            "an inactive descendant must still receive the projectile spawner - GetComponentsInChildren(true).");
        Assert.AreSame(reader, GetPrivateField(inactiveEvents, "hasAutoPlayerReader"),
            "an inactive descendant must still receive the auto-player reader - GetComponentsInChildren(true).");
    }

    // ==================== projectile forwarding ====================

    [Test]
    public void ProjectileForwarding_ReachesSuppliedDelegateWithExactArguments()
    {
        GameObject root = Spawn("participant");
        PlayerAnimationEvents events = root.AddComponent<PlayerAnimationEvents>();

        GameObject spawnPointObject = Spawn("projectileSpawn");
        spawnPointObject.transform.SetParent(root.transform);
        spawnPointObject.transform.position = new Vector3(4f, 5f, 6f);

        GameObject prefab = Spawn("laser-prefab");
        GameObject spawnResult = Spawn("spawn-result");

        int callCount = 0;
        GameObject capturedPrefab = null;
        Vector3 capturedPosition = default;
        Quaternion capturedRotation = default;

        Func<GameObject, Vector3, Quaternion, GameObject> spawner = (spawnedPrefab, position, rotation) =>
        {
            callCount++;
            capturedPrefab = spawnedPrefab;
            capturedPosition = position;
            capturedRotation = rotation;
            return spawnResult;
        };

        SpawnCoordinator coordinator = Coordinator(projectileSpawner: spawner);
        InvokeBind(coordinator, root);

        // The projectileSpawn/prefab fields are normally resolved inside Start(); set directly so this
        // test drives PlayerAnimationEvents' own unchanged forwarding logic without depending on Unity's
        // MonoBehaviour lifecycle running inside an EditMode test.
        SetPrivateField(events, "projectileSpawn", spawnPointObject);
        SetPrivateField(events, "projectileLaserPrefab", prefab);

        events.instantiateProjectileLazer();

        Assert.AreEqual(1, callCount, "the bound delegate must be invoked exactly once.");
        Assert.AreSame(prefab, capturedPrefab, "the exact prefab reference must be preserved.");
        Assert.AreEqual(spawnPointObject.transform.position, capturedPosition, "the exact spawn position must be preserved.");
        Assert.AreEqual(Quaternion.identity, capturedRotation, "the rotation PlayerAnimationEvents passes must be preserved unchanged.");
    }

    [Test]
    public void ProjectileForwarding_DelegateReturnValueIsIgnoredByCaller()
    {
        // PlayerAnimationEvents.SpawnProjectile discards the delegate's return value (it only needs the
        // spawn to have happened) - proven here by a delegate returning null both being invoked (callCount)
        // and not causing an exception, so this doesn't just pass because the call never happened.
        GameObject root = Spawn("participant");
        PlayerAnimationEvents events = root.AddComponent<PlayerAnimationEvents>();

        GameObject spawnPointObject = Spawn("projectileSpawn");
        spawnPointObject.transform.SetParent(root.transform);

        GameObject prefab = Spawn("bullet-prefab");

        int callCount = 0;
        Func<GameObject, Vector3, Quaternion, GameObject> spawner = (spawnedPrefab, position, rotation) =>
        {
            callCount++;
            return null;
        };
        SpawnCoordinator coordinator = Coordinator(projectileSpawner: spawner);
        InvokeBind(coordinator, root);

        SetPrivateField(events, "projectileSpawn", spawnPointObject);
        SetPrivateField(events, "projectileBulletPrefab", prefab);

        Assert.DoesNotThrow(() => events.instantiateProjectileBullet());
        Assert.AreEqual(1, callCount, "the delegate must actually have been invoked, not skipped.");
    }

    // ==================== null / direct-construction compatibility ====================

    [Test]
    public void NullProjectileSpawner_BindsNullAndFailsClosedWithoutThrowing()
    {
        GameObject root = Spawn("participant");
        PlayerAnimationEvents events = root.AddComponent<PlayerAnimationEvents>();

        GameObject spawnPointObject = Spawn("projectileSpawn");
        spawnPointObject.transform.SetParent(root.transform);
        GameObject prefab = Spawn("laser-prefab-null-spawner");

        SpawnCoordinator coordinator = Coordinator(); // both delegates default to null
        InvokeBind(coordinator, root);

        Assert.IsNull(GetPrivateField(events, "projectileSpawner"));

        SetPrivateField(events, "projectileSpawn", spawnPointObject);
        SetPrivateField(events, "projectileLaserPrefab", prefab);

        Assert.DoesNotThrow(() => events.instantiateProjectileLazer(),
            "a null bound projectileSpawner must fail closed (no spawn, no exception), matching "
            + "PlayerAnimationEvents.SpawnProjectile's existing null guard.");
    }

    [Test]
    public void NullHasAutoPlayerReader_BindsNullRatherThanADefaultAnswer()
    {
        // Unlike NullProjectileSpawner_BindsNullAndFailsClosedWithoutThrowing above, this does not drive
        // PlayerAnimationEvents.Start() end-to-end: Start() returns before ever consulting
        // hasAutoPlayerReader when no PlayerController is found on the hierarchy (see its
        // `if (playerController == null) { ...; return; }` guard), and constructing a real PlayerController
        // is outside what this composition-seam fixture needs to stand up. What this asserts - that
        // SpawnCoordinator forwards null rather than inventing a true/false default - is the part of the
        // contract this slice actually owns; Start()'s own null check (unchanged by this slice) is what
        // fails closed on that null at the point it's actually read.
        GameObject root = Spawn("participant");
        PlayerAnimationEvents events = root.AddComponent<PlayerAnimationEvents>();

        SpawnCoordinator coordinator = Coordinator(); // both delegates default to null
        InvokeBind(coordinator, root);

        Assert.IsNull(GetPrivateField(events, "hasAutoPlayerReader"),
            "a coordinator built without a reader must bind null rather than inventing a default "
            + "true/false answer - PlayerAnimationEvents.Start()'s own null check is what fails closed.");
    }

    // ==================== liveness ====================

    [Test]
    public void HasAutoPlayerReader_IsForwardedLiveNotCapturedAsBool()
    {
        GameObject root = Spawn("participant");
        PlayerAnimationEvents events = root.AddComponent<PlayerAnimationEvents>();

        bool hasAutoPlayer = false;
        Func<bool> reader = () => hasAutoPlayer;

        SpawnCoordinator coordinator = Coordinator(hasAutoPlayerReader: reader);
        InvokeBind(coordinator, root);

        Func<bool> boundReader = (Func<bool>)GetPrivateField(events, "hasAutoPlayerReader");
        Assert.IsFalse(boundReader(), "must reflect the source's current value at first evaluation.");

        hasAutoPlayer = true;

        Assert.IsTrue(boundReader(),
            "the reader SpawnCoordinator forwards must re-evaluate its source at call time, not a "
            + "bool snapshot captured when SpawnCoordinator was constructed or bound.");
    }

    // ================ production adapter: GameLevelManager.HasAutoPlayerForAnimationEvents ================

    private static MethodInfo HasAutoPlayerAdapterMethod()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(
            "HasAutoPlayerForAnimationEvents", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager.HasAutoPlayerForAnimationEvents must exist as the production adapter");
        return method;
    }

    private static bool InvokeHasAutoPlayerAdapter()
    {
        return (bool)HasAutoPlayerAdapterMethod().Invoke(null, null);
    }

    [Test]
    public void Adapter_NoInstance_ReportsFalse()
    {
        GameLevelManager.instance = null;

        Assert.IsFalse(InvokeHasAutoPlayerAdapter());
    }

    [Test]
    public void Adapter_CurrentInstanceWithNullAutoPlayer_ReportsFalse()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("gm-null-auto-player");
        manager.AutoPlayer = null;
        GameLevelManager.instance = manager;

        Assert.IsFalse(InvokeHasAutoPlayerAdapter());
    }

    [Test]
    public void Adapter_CurrentInstanceWithAutoPlayer_ReportsTrue()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("gm-with-auto-player");
        manager.AutoPlayer = Spawn("auto-player-actor");
        GameLevelManager.instance = manager;

        Assert.IsTrue(InvokeHasAutoPlayerAdapter());
    }

    [Test]
    public void Adapter_ReplacingInstance_ObservesReplacementRatherThanOriginal()
    {
        GameLevelManager original = SpawnManagerWithoutAwake("gm-original");
        original.AutoPlayer = Spawn("auto-player-original");
        GameLevelManager.instance = original;

        Assert.IsTrue(InvokeHasAutoPlayerAdapter(), "precondition: the original instance reports true.");

        GameLevelManager replacement = SpawnManagerWithoutAwake("gm-replacement");
        replacement.AutoPlayer = null;
        GameLevelManager.instance = replacement;

        Assert.IsFalse(InvokeHasAutoPlayerAdapter(),
            "the adapter must follow the current GameLevelManager.instance live, not a manager "
            + "reference captured when the coordinator (or this test) first observed it.");
    }

    // ================ production adapter: GameLevelManager.SpawnProjectileForAnimationEvents ================

    private static MethodInfo SpawnProjectileAdapterMethod()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(
            "SpawnProjectileForAnimationEvents", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager.SpawnProjectileForAnimationEvents must exist as the production adapter");
        return method;
    }

    [Test]
    public void ProjectileAdapter_NullPrefab_ReturnsNullWithoutThrowing()
    {
        object result = null;
        Assert.DoesNotThrow(() =>
            result = SpawnProjectileAdapterMethod().Invoke(null, new object[] { null, Vector3.zero, Quaternion.identity }));

        Assert.IsNull(result, "ProjectilePool.Spawn already treats a null prefab as a no-op, and the adapter must not add its own fallback.");
    }

    [Test]
    public void ProjectileAdapter_RoutesThroughProjectilePoolSpawnWithExactArguments()
    {
        GameObject prefab = Spawn("adapter-projectile-prefab");
        Vector3 position = new Vector3(1f, 2f, 3f);
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

        GameObject result = (GameObject)SpawnProjectileAdapterMethod().Invoke(null, new object[] { prefab, position, rotation });
        Assert.IsNotNull(result, "ProjectilePool.Spawn should return a live pooled instance for a valid prefab.");

        try
        {
            Assert.AreNotSame(prefab, result, "the returned instance is a pooled copy of the prefab, not the prefab itself - same as ProjectilePool.Spawn called directly.");
            Assert.AreEqual(position, result.transform.position, "the exact spawn position must be preserved.");
            Assert.Less(Quaternion.Angle(rotation, result.transform.rotation), 0.01f,
                "the exact spawn rotation must be preserved (compared by angle, not exact float equality, "
                + "since Transform.rotation may re-normalize a quaternion by less than floating-point display precision).");
            Assert.IsTrue(result.activeSelf, "ProjectilePool.Spawn activates the pooled instance before returning it.");
        }
        finally
        {
            // Release back through the pool rather than DestroyImmediate: the pool's ObjectPool<GameObject>
            // considers this instance checked out until Release() runs, and destroying it directly instead
            // would leave that bookkeeping (and the static poolsByPrefab entry keyed on `prefab`) stale for
            // the rest of this Editor session.
            ProjectilePool.Release(result);
        }
    }
}
