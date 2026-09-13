using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 50a: <see cref="SpawnCoordinator.BindPlayerCollisionsContext"/> now resolves
/// a human's fall-respawn destination through <c>this human's own roster slot</c>
/// (<see cref="SpawnCoordinator.SpawnLocations.ForSlot"/>) instead of unconditionally binding
/// <see cref="SpawnCoordinator.SpawnLocations.Player1"/> to every registered human. Slice 50 introduced
/// the regression: every human, regardless of which slot spawned them, respawned at slot 0's spawn
/// point after a fall.
///
/// Mirrors <see cref="Level5PlayerHealthMatchRulesTests"/>'s shape - drives the real private
/// <c>RegisterHuman</c> composition path via reflection rather than a stand-in, and asserts on
/// <see cref="PlayerCollisions"/>'s own bound state and trigger behaviour rather than reimplementing
/// the mapping.
/// </summary>
public class Level5SpawnCoordinatorFallRespawnCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerRegistry registry;
    private SpawnCoordinator coordinator;
    private SpawnCoordinator.SpawnLocations locations;
    private MethodInfo registerHuman;

    [SetUp]
    public void SetUp()
    {
        // RegisterHuman also drives InitializeHumanProfile/PrepareHumanMatchContext; clearing keeps
        // that path deterministic regardless of what ran before this test (mirrors
        // Level5PlayerHealthMatchRulesTests).
        ActiveMatch.Clear();

        registry = new PlayerRegistry();
        locations = new SpawnCoordinator.SpawnLocations
        {
            Player1 = SpawnAt("player_spawn_location1", new Vector3(1f, 0f, 0f)),
            Player2 = SpawnAt("player_spawn_location2", new Vector3(2f, 0f, 0f)),
            Player3 = SpawnAt("player_spawn_location3", new Vector3(3f, 0f, 0f)),
            Player4 = SpawnAt("player_spawn_location4", new Vector3(4f, 0f, 0f)),
        };

        coordinator = new SpawnCoordinator(
            locations,
            registry,
            Rules(),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None);

        registerHuman = typeof(SpawnCoordinator).GetMethod("RegisterHuman", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerHuman, "SpawnCoordinator.RegisterHuman must exist");
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();

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

    private GameObject SpawnAt(string name, Vector3 position)
    {
        GameObject go = Spawn(name);
        go.transform.position = position;
        return go;
    }

    private static ResolvedMatchRules Rules()
    {
        return new ResolvedMatchRules(enemiesEnabled: false, obstaclesEnabled: false, sniper: SniperMode.None);
    }

    private static Transform GetFallRespawnDestination(PlayerCollisions collisions)
    {
        FieldInfo field = typeof(PlayerCollisions).GetField("fallRespawnDestination", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "PlayerCollisions must declare a fallRespawnDestination field");
        return (Transform)field.GetValue(collisions);
    }

    private static void InvokeStart(PlayerCollisions collisions)
    {
        MethodInfo method = typeof(PlayerCollisions).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "PlayerCollisions must declare Start");
        method.Invoke(collisions, null);
    }

    private static void InvokeOnTriggerEnter(PlayerCollisions collisions, Collider other)
    {
        MethodInfo method = typeof(PlayerCollisions).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "PlayerCollisions must declare OnTriggerEnter");
        method.Invoke(collisions, new object[] { other });
    }

    private static PlayerSlot LocalHumanSlot(int slotId)
    {
        return new PlayerSlot(slotId, PlayerControlType.LocalHuman, CharacterSelection.None, localInputSlot: 0);
    }

    /// <summary>
    /// A production-shaped human participant: <see cref="PlayerIdentifier"/>/<see cref="CharacterProfile"/>/
    /// <see cref="PlayerController"/> on the root, exactly like <c>SpawnCoordinator.RegisterHuman</c>
    /// expects, and <see cref="PlayerCollisions"/> authored on a hitbox child tagged "playerHitbox" -
    /// mirroring where every production player prefab carries it (see
    /// <see cref="SpawnCoordinator.BindPlayerCollisionsContext"/>'s own doc comment).
    /// </summary>
    private GameObject SpawnHumanParticipant(string name, out PlayerCollisions collisions)
    {
        GameObject actorGo = Spawn(name);
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject hitbox = new GameObject(name + "-hitbox");
        hitbox.transform.SetParent(actorGo.transform);
        hitbox.tag = "playerHitbox";
        hitbox.AddComponent<BoxCollider>();
        collisions = hitbox.AddComponent<PlayerCollisions>();

        return actorGo;
    }

    private BoxCollider MakeFallRespawnerCollider(string name)
    {
        GameObject go = Spawn(name);
        go.tag = "fallRespawner";
        return go.AddComponent<BoxCollider>();
    }

    // ==================== slot -> spawn composition ====================

    [TestCase(0, TestName = "SpawnCoordinator_RegisterHuman_Slot0_BindsPlayer1AsFallRespawnDestination")]
    [TestCase(1, TestName = "SpawnCoordinator_RegisterHuman_Slot1_BindsPlayer2AsFallRespawnDestination")]
    [TestCase(2, TestName = "SpawnCoordinator_RegisterHuman_Slot2_BindsPlayer3AsFallRespawnDestination")]
    [TestCase(3, TestName = "SpawnCoordinator_RegisterHuman_Slot3_BindsPlayer4AsFallRespawnDestination")]
    public void RegisterHuman_BindsThisHumansOwnSlotSpawnAsFallRespawnDestination(int slotId)
    {
        GameObject actorGo = SpawnHumanParticipant($"human-slot-{slotId}", out PlayerCollisions collisions);
        PlayerSlot slot = LocalHumanSlot(slotId);

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, slot });

        Transform expected = locations.ForSlot(slotId).transform;
        Assert.AreSame(expected, GetFallRespawnDestination(collisions),
            $"slot {slotId} must bind its own roster-slot spawn point, not another slot's.");
    }

    [Test]
    public void RegisterHuman_NullSlot_FallsBackToPlayer1ForCompatibility()
    {
        GameObject actorGo = SpawnHumanParticipant("human-null-slot", out PlayerCollisions collisions);

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, null });

        Assert.AreSame(locations.Player1.transform, GetFallRespawnDestination(collisions),
            "a null slot must preserve the original single-human Player1 behaviour.");
    }

    [Test]
    public void RegisterHuman_SlotWithNoMatchingSpawnPoint_BindsNullDestinationSafely()
    {
        // No production roster reaches slot ids past MaxSlots, but ForSlot itself returns null for
        // anything it does not recognise (see SpawnLocations.ForSlot's default case) - composition
        // must pass that through rather than throwing or silently substituting Player1.
        GameObject actorGo = SpawnHumanParticipant("human-out-of-range-slot", out PlayerCollisions collisions);
        PlayerSlot slot = LocalHumanSlot(99);

        Assert.DoesNotThrow(() => registerHuman.Invoke(coordinator, new object[] { actorGo, 0, slot }));

        Assert.IsNull(GetFallRespawnDestination(collisions));
    }

    [Test]
    public void RegisterHuman_TwoHumans_EachBindsItsOwnSlotIndependently()
    {
        GameObject primary = SpawnHumanParticipant("human-primary", out PlayerCollisions primaryCollisions);
        GameObject secondary = SpawnHumanParticipant("human-secondary", out PlayerCollisions secondaryCollisions);

        registerHuman.Invoke(coordinator, new object[] { primary, 0, LocalHumanSlot(0) });
        registerHuman.Invoke(coordinator, new object[] { secondary, 1, LocalHumanSlot(1) });

        Assert.AreSame(locations.Player1.transform, GetFallRespawnDestination(primaryCollisions));
        Assert.AreSame(locations.Player2.transform, GetFallRespawnDestination(secondaryCollisions),
            "the second human must not have been bound to the primary's (Player1) destination.");
    }

    // ==================== behaviour: the triggering participant moves itself ====================

    [Test]
    public void OnTriggerEnter_FallRespawner_MovesOwnParticipantToItsOwnBoundDestination()
    {
        GameObject actorGo = SpawnHumanParticipant("human-slot-1", out PlayerCollisions collisions);
        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, LocalHumanSlot(1) });
        InvokeStart(collisions);

        actorGo.transform.position = new Vector3(50f, 50f, 50f);
        Collider fallRespawner = MakeFallRespawnerCollider("fall-respawner");

        InvokeOnTriggerEnter(collisions, fallRespawner);

        Assert.AreEqual(locations.Player2.transform.position, actorGo.transform.position,
            "the participant that entered the fall-respawner must move to its own bound (Player2) destination.");
    }

    [Test]
    public void OnTriggerEnter_FallRespawner_DoesNotMoveAnotherParticipant()
    {
        GameObject triggering = SpawnHumanParticipant("human-slot-1-triggering", out PlayerCollisions triggeringCollisions);
        GameObject bystander = SpawnHumanParticipant("human-slot-0-bystander", out PlayerCollisions bystanderCollisions);
        registerHuman.Invoke(coordinator, new object[] { triggering, 0, LocalHumanSlot(1) });
        registerHuman.Invoke(coordinator, new object[] { bystander, 1, LocalHumanSlot(0) });
        InvokeStart(triggeringCollisions);
        InvokeStart(bystanderCollisions);

        Vector3 bystanderStart = new Vector3(9f, 9f, 9f);
        bystander.transform.position = bystanderStart;
        Collider fallRespawner = MakeFallRespawnerCollider("fall-respawner");

        InvokeOnTriggerEnter(triggeringCollisions, fallRespawner);

        Assert.AreEqual(bystanderStart, bystander.transform.position,
            "a participant that did not enter the fall-respawner must not move.");
    }

    [Test]
    public void OnTriggerEnter_FallRespawner_NullDestination_DoesNotThrowOrTeleport()
    {
        GameObject actorGo = SpawnHumanParticipant("human-out-of-range-slot", out PlayerCollisions collisions);
        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, LocalHumanSlot(99) });
        InvokeStart(collisions);
        Assert.IsNull(GetFallRespawnDestination(collisions), "precondition: this slot must have bound no destination.");

        Vector3 start = new Vector3(7f, 7f, 7f);
        actorGo.transform.position = start;
        Collider fallRespawner = MakeFallRespawnerCollider("fall-respawner");

        Assert.DoesNotThrow(() => InvokeOnTriggerEnter(collisions, fallRespawner));
        Assert.AreEqual(start, actorGo.transform.position, "a null bound destination must not teleport the participant.");
    }
}
