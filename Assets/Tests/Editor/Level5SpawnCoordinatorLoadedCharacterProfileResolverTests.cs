using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 2b Slice 57: <see cref="SpawnCoordinator.InitializeHumanProfile"/> no longer implements
/// the saved-profile lookup (<c>LoadedData.instance.getSelectedCharacterProfile</c>) itself - it only
/// forwards whatever <c>loadedCharacterProfileResolver</c> delegate it was constructed with into
/// <c>CharacterProfile.PrepareHumanMatchContext</c>. Mirrors the shape of
/// <see cref="Level5SpawnCoordinatorCampaignCpuPrefabResolverTests"/>: drives the real private
/// <c>RegisterHuman</c> composition path via reflection against a fully controlled fake resolver, then
/// separately exercises the real production adapter (<see cref="GameLevelManager"/>'s own
/// <c>ResolveLoadedCharacterProfile</c>) against a live <see cref="LoadedData"/> singleton.
/// </summary>
public class Level5SpawnCoordinatorLoadedCharacterProfileResolverTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private MethodInfo registerHuman;

    [SetUp]
    public void SetUp()
    {
        ActiveMatch.Clear();

        registerHuman = typeof(SpawnCoordinator).GetMethod("RegisterHuman", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerHuman, "SpawnCoordinator.RegisterHuman must exist");
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();
        LoadedData.instance = null;

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

    private static void SetPlayerSelectedData(LoadedData data, List<CharacterProfile> profiles)
    {
        FieldInfo field = typeof(LoadedData).GetField("playerSelectedData", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "LoadedData must declare a playerSelectedData field");
        field.SetValue(data, profiles);
    }

    /// <summary>Mirrors <see cref="Level5RangeMeterOwnershipTests"/>'s <c>BuildAnyMatch</c>: only its
    /// presence matters here - <c>ActiveMatch.Begin</c> is what makes
    /// <c>MatchRuntime.HasConfiguration</c> observe true, which is what gates
    /// <c>InitializeHumanProfile</c>'s saved-profile rebuild.</summary>
    private static MatchConfiguration BuildAnyMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();

        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
            CheerleaderSelection.None,
            "loaded character profile resolver test");
    }

    private static SpawnCoordinator BuildConfiguredCoordinator(Func<int, CharacterProfile> resolver)
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            new PlayerRegistry(),
            new ResolvedMatchRules(),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            loadedCharacterProfileResolver: resolver);
    }

    private GameObject SpawnHumanParticipant(string name)
    {
        GameObject go = Spawn(name);
        go.AddComponent<CharacterProfile>();
        go.AddComponent<PlayerController>();
        go.AddComponent<PlayerIdentifier>();
        return go;
    }

    private static PlayerSlot HumanSlot(int slotId, int characterId)
    {
        CharacterSelection character = characterId == 0
            ? CharacterSelection.None
            : TestDefinitions.Character("drblood", characterId: characterId);
        return new PlayerSlot(slotId, PlayerControlType.LocalHuman, character, localInputSlot: 0);
    }

    // ==================== SpawnCoordinator forwarding ====================

    [Test]
    public void RegisterHuman_ForwardsExactSuppliedResolverIntoCharacterProfile()
    {
        ActiveMatch.Begin(BuildAnyMatch());
        Func<int, CharacterProfile> resolver = id => null;
        SpawnCoordinator coordinator = BuildConfiguredCoordinator(resolver);
        GameObject actorGo = SpawnHumanParticipant("human-forward");
        CharacterProfile profile = actorGo.GetComponent<CharacterProfile>();

        LogAssert.ignoreFailingMessages = true;
        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, HumanSlot(0, 0) });
        LogAssert.ignoreFailingMessages = false;

        Assert.AreSame(resolver, GetPrivateField(profile, "preparedProfileResolver"),
            "RegisterHuman must forward the exact supplied resolver delegate, unexamined.");
    }

    [Test]
    public void RegisterHuman_ResolverReceivesTheCorrectCharacterIdForThatSlot()
    {
        ActiveMatch.Begin(BuildAnyMatch());
        int? receivedId = null;
        Func<int, CharacterProfile> resolver = id =>
        {
            receivedId = id;
            return null;
        };
        SpawnCoordinator coordinator = BuildConfiguredCoordinator(resolver);
        GameObject actorGo = SpawnHumanParticipant("human-id");

        LogAssert.ignoreFailingMessages = true;
        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, HumanSlot(0, 42) });
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(42, receivedId, "the resolver must be asked for this human roster slot's own character id.");
    }

    [Test]
    public void RegisterHuman_ResolverReturnsAProfile_ItsExactReferenceIsConsumed()
    {
        ActiveMatch.Begin(BuildAnyMatch());
        CharacterProfile saved = Spawn("saved-profile-consumed").AddComponent<CharacterProfile>();
        saved.PlayerId = 7;
        Func<int, CharacterProfile> resolver = id => saved;
        SpawnCoordinator coordinator = BuildConfiguredCoordinator(resolver);
        GameObject actorGo = SpawnHumanParticipant("human-consume");
        CharacterProfile profile = actorGo.GetComponent<CharacterProfile>();

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, HumanSlot(0, 42) });

        Assert.AreEqual(7, profile.PlayerId, "the resolver's exact returned profile must have been consumed to rebuild this participant.");
    }

    [Test]
    public void RegisterHuman_ResolverReturnsNull_PreservesExistingFailClosedBehavior()
    {
        ActiveMatch.Begin(BuildAnyMatch());
        Func<int, CharacterProfile> resolver = id => null;
        SpawnCoordinator coordinator = BuildConfiguredCoordinator(resolver);
        GameObject actorGo = SpawnHumanParticipant("human-null-result");
        CharacterProfile profile = actorGo.GetComponent<CharacterProfile>();
        profile.PlayerId = 99;

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the selected player profile"));
        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, HumanSlot(0, 42) });

        Assert.AreEqual(99, profile.PlayerId,
            "a null resolver result must leave the profile unmodified, matching the pre-slice fail-closed behavior.");
    }

    [Test]
    public void RegisterHuman_WithoutActiveMatch_NeverInvokesTheResolver()
    {
        ActiveMatch.Clear();
        int callCount = 0;
        Func<int, CharacterProfile> resolver = id =>
        {
            callCount++;
            return null;
        };
        SpawnCoordinator coordinator = BuildConfiguredCoordinator(resolver);
        GameObject actorGo = SpawnHumanParticipant("human-direct-scene");

        registerHuman.Invoke(coordinator, new object[] { actorGo, 0, HumanSlot(0, 42) });

        Assert.AreEqual(0, callCount,
            "direct-scene/no-active-configuration registration must not request persisted profile data.");
    }

    // ================ production legacy adapter: GameLevelManager.ResolveLoadedCharacterProfile ================

    private static MethodInfo AdapterMethod()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(
            "ResolveLoadedCharacterProfile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager.ResolveLoadedCharacterProfile must exist as the production saved-profile adapter");
        return method;
    }

    private static CharacterProfile InvokeAdapter(int characterId)
    {
        return (CharacterProfile)AdapterMethod().Invoke(null, new object[] { characterId });
    }

    [Test]
    public void Adapter_NoLoadedDataInstance_ReturnsNull()
    {
        LoadedData.instance = null;

        Assert.IsNull(InvokeAdapter(1));
    }

    [Test]
    public void Adapter_MatchingProfile_ReturnsExactReference()
    {
        LoadedData data = Spawn("loaded-data-match").AddComponent<LoadedData>();
        CharacterProfile saved = Spawn("saved-profile-match").AddComponent<CharacterProfile>();
        saved.PlayerId = 5;
        SetPlayerSelectedData(data, new List<CharacterProfile> { saved });
        LoadedData.instance = data;

        Assert.AreSame(saved, InvokeAdapter(5));
    }

    [Test]
    public void Adapter_NoMatchingProfile_ReturnsNull()
    {
        LoadedData data = Spawn("loaded-data-no-match").AddComponent<LoadedData>();
        CharacterProfile other = Spawn("saved-profile-other").AddComponent<CharacterProfile>();
        other.PlayerId = 6;
        SetPlayerSelectedData(data, new List<CharacterProfile> { other });
        LoadedData.instance = data;

        Assert.IsNull(InvokeAdapter(5));
    }

    [Test]
    public void Adapter_ReplacedLoadedDataInstance_LaterCallObservesTheReplacement()
    {
        LoadedData first = Spawn("loaded-data-first").AddComponent<LoadedData>();
        CharacterProfile firstProfile = Spawn("first-profile").AddComponent<CharacterProfile>();
        firstProfile.PlayerId = 1;
        SetPlayerSelectedData(first, new List<CharacterProfile> { firstProfile });
        LoadedData.instance = first;

        Assert.AreSame(firstProfile, InvokeAdapter(1), "precondition: the first instance must be reached");

        LoadedData second = Spawn("loaded-data-second").AddComponent<LoadedData>();
        CharacterProfile secondProfile = Spawn("second-profile").AddComponent<CharacterProfile>();
        secondProfile.PlayerId = 1;
        SetPlayerSelectedData(second, new List<CharacterProfile> { secondProfile });
        LoadedData.instance = second;

        Assert.AreSame(secondProfile, InvokeAdapter(1),
            "a later call must observe the replaced singleton, not a stale reference to the first.");
    }

    [Test]
    public void Adapter_ClearedLoadedDataInstance_ReturnsToNullResult()
    {
        LoadedData data = Spawn("loaded-data-cleared").AddComponent<LoadedData>();
        CharacterProfile saved = Spawn("saved-profile-cleared").AddComponent<CharacterProfile>();
        saved.PlayerId = 3;
        SetPlayerSelectedData(data, new List<CharacterProfile> { saved });
        LoadedData.instance = data;
        Assert.AreSame(saved, InvokeAdapter(3), "precondition: the instance must be reached while assigned");

        LoadedData.instance = null;

        Assert.IsNull(InvokeAdapter(3), "clearing the instance must return the adapter to its null-result behavior.");
    }
}
