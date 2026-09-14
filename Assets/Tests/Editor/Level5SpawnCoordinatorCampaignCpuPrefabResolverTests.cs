using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 54: <see cref="SpawnCoordinator.ResolveParticipantPrefab"/> no longer reaches
/// directly for <c>GameOptions.levelsList</c>/<c>levelSelectedIndex</c> or <c>LevelSelected.CpuPlayer</c>.
/// It now asks one seam - <see cref="SpawnCoordinator.TryResolveCampaignCpuPrefab"/> - for the campaign's
/// CPU override, at the exact point a Beat Tha Computahs CPU participant's prefab is being decided.
///
/// These tests drive the real private <c>ResolveParticipantPrefab</c> through reflection (the sole
/// production path) against a fully controlled fake resolver, then separately exercise the real
/// production adapter (<see cref="GameLevelManager"/>'s own <c>TryResolveCampaignCpuPrefab</c>) against
/// <c>GameOptions.levelsList</c>/<c>levelSelectedIndex</c> directly - so neither the seam's contract nor
/// its one production implementation goes untested.
/// </summary>
public class Level5SpawnCoordinatorCampaignCpuPrefabResolverTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private MethodInfo resolveParticipantPrefab;
    private List<LevelSelected> savedLevelsList;
    private int savedLevelIndex;

    [SetUp]
    public void SetUp()
    {
        resolveParticipantPrefab = typeof(SpawnCoordinator).GetMethod(
            "ResolveParticipantPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(resolveParticipantPrefab, "SpawnCoordinator.ResolveParticipantPrefab must exist");

        // The production adapter tests below write GameOptions' legacy campaign globals directly;
        // save/restore keeps this fixture from leaking that state into whatever else runs in the
        // same Editor test session.
        savedLevelsList = GameOptions.levelsList;
        savedLevelIndex = GameOptions.levelSelectedIndex;
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.levelsList = savedLevelsList;
        GameOptions.levelSelectedIndex = savedLevelIndex;

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

    private static PlayerSlot CpuSlot(int slotId)
    {
        return new PlayerSlot(slotId, PlayerControlType.Cpu, CharacterSelection.None);
    }

    private static PlayerSlot HumanSlot(int slotId)
    {
        return new PlayerSlot(slotId, PlayerControlType.LocalHuman, CharacterSelection.None, localInputSlot: 0);
    }

    private static SpawnCoordinator Coordinator(
        GameModeId modeId,
        SpawnCoordinator.TryResolveCampaignCpuPrefab resolver = null)
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            new PlayerRegistry(),
            new ResolvedMatchRules(),
            new PlayerRoster(new PlayerSlot[0]),
            modeId,
            null,
            resolver);
    }

    private GameObject InvokeResolveParticipantPrefab(SpawnCoordinator coordinator, PlayerSlot slot, int slotId)
    {
        return (GameObject)resolveParticipantPrefab.Invoke(coordinator, new object[] { slot, slotId });
    }

    // ==================== campaign CPU: resolver contract ====================

    [Test]
    public void BeatThaComputahsCpu_ResolverReturnsTruePrefab_ReturnsThatExactPrefab()
    {
        GameObject campaignPrefab = Spawn("campaign-cpu-prefab");
        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, (out GameObject prefab) =>
        {
            prefab = campaignPrefab;
            return true;
        });

        GameObject result = InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.AreSame(campaignPrefab, result);
    }

    [Test]
    public void BeatThaComputahsCpu_ResolverReturnsTrueNull_ReturnsNullWithoutResourcesFallback()
    {
        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, (out GameObject prefab) =>
        {
            prefab = null;
            return true;
        });

        GameObject result = InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.IsNull(result, "a resolver-authoritative null override must not fall back to Resources.");
    }

    [Test]
    public void BeatThaComputahsCpu_ResolverReturnsFalse_UsesResourcesFallback()
    {
        GameObject expectedFallback = Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_cpu + "drblood");
        Assert.IsNotNull(expectedFallback, "precondition: the default CPU fallback prefab must exist");

        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, (out GameObject prefab) =>
        {
            prefab = null;
            return false;
        });

        GameObject result = InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.AreSame(expectedFallback, result,
            "an unavailable campaign override must fall back to the normal Resources CPU lookup.");
    }

    [Test]
    public void BeatThaComputahsCpu_NoResolverBound_UsesResourcesFallback()
    {
        GameObject expectedFallback = Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_cpu + "drblood");
        Assert.IsNotNull(expectedFallback, "precondition: the default CPU fallback prefab must exist");

        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, resolver: null);

        GameObject result = InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.AreSame(expectedFallback, result,
            "a coordinator built without a resolver must behave exactly like an unavailable override.");
    }

    [Test]
    public void BeatThaComputahsCpu_ResolverInvokedExactlyOnceForOneDecision()
    {
        int callCount = 0;
        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, (out GameObject prefab) =>
        {
            callCount++;
            prefab = null;
            return false;
        });

        InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.AreEqual(1, callCount, "the resolver must be evaluated at most once for one relevant prefab decision.");
    }

    // ==================== non-campaign paths: resolver must not be invoked ====================

    [Test]
    public void CpuInAnotherMode_ResolverNotInvoked_UsesResourcesCpuPath()
    {
        GameObject expectedFallback = Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_cpu + "drblood");
        int callCount = 0;
        SpawnCoordinator coordinator = Coordinator(GameModeId.None, (out GameObject prefab) =>
        {
            callCount++;
            prefab = null;
            return false;
        });

        GameObject result = InvokeResolveParticipantPrefab(coordinator, CpuSlot(1), 1);

        Assert.AreEqual(0, callCount, "a CPU outside Beat Tha Computahs must never consult the campaign resolver.");
        Assert.AreSame(expectedFallback, result);
    }

    [Test]
    public void HumanInBeatThaComputahs_ResolverNotInvoked_UsesResourcesHumanPath()
    {
        GameObject expectedFallback = Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_human + "drblood");
        int callCount = 0;
        SpawnCoordinator coordinator = Coordinator(GameModeId.BeatThaComputahs, (out GameObject prefab) =>
        {
            callCount++;
            prefab = null;
            return false;
        });

        GameObject result = InvokeResolveParticipantPrefab(coordinator, HumanSlot(1), 1);

        Assert.AreEqual(0, callCount, "a human participant must never consult the campaign resolver, even in Beat Tha Computahs.");
        Assert.AreSame(expectedFallback, result);
    }

    // ================ production legacy adapter: GameLevelManager.TryResolveCampaignCpuPrefab ================

    private static MethodInfo AdapterMethod()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(
            "TryResolveCampaignCpuPrefab", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager.TryResolveCampaignCpuPrefab must exist as the production campaign adapter");
        return method;
    }

    private static bool InvokeAdapter(out GameObject prefab)
    {
        object[] args = { null };
        bool available = (bool)AdapterMethod().Invoke(null, args);
        prefab = (GameObject)args[0];
        return available;
    }

    [Test]
    public void Adapter_NullLevelsList_ReportsUnavailable()
    {
        GameOptions.levelsList = null;
        GameOptions.levelSelectedIndex = 0;

        bool available = InvokeAdapter(out GameObject prefab);

        Assert.IsFalse(available);
        Assert.IsNull(prefab);
    }

    [Test]
    public void Adapter_NegativeIndex_ReportsUnavailable()
    {
        LevelSelected level = Spawn("level-selected-negative").AddComponent<LevelSelected>();
        GameOptions.levelsList = new List<LevelSelected> { level };
        GameOptions.levelSelectedIndex = -1;

        bool available = InvokeAdapter(out GameObject prefab);

        Assert.IsFalse(available);
        Assert.IsNull(prefab);
    }

    [Test]
    public void Adapter_IndexAtOrPastCount_ReportsUnavailable()
    {
        LevelSelected level = Spawn("level-selected-out-of-range").AddComponent<LevelSelected>();
        GameOptions.levelsList = new List<LevelSelected> { level };
        GameOptions.levelSelectedIndex = 1;

        bool available = InvokeAdapter(out GameObject prefab);

        Assert.IsFalse(available);
        Assert.IsNull(prefab);
    }

    [Test]
    public void Adapter_ValidSelectionWithAuthoredPrefab_ReportsSuccessWithExactPrefab()
    {
        GameObject campaignPrefab = Spawn("authored-cpu-prefab");
        LevelSelected level = Spawn("level-selected-with-prefab").AddComponent<LevelSelected>();
        level.CpuPlayer = campaignPrefab;
        GameOptions.levelsList = new List<LevelSelected> { level };
        GameOptions.levelSelectedIndex = 0;

        bool available = InvokeAdapter(out GameObject prefab);

        Assert.IsTrue(available);
        Assert.AreSame(campaignPrefab, prefab);
    }

    [Test]
    public void Adapter_ValidSelectionWithNullAuthoredPrefab_ReportsSuccessWithNull()
    {
        LevelSelected level = Spawn("level-selected-null-prefab").AddComponent<LevelSelected>();
        level.CpuPlayer = null;
        GameOptions.levelsList = new List<LevelSelected> { level };
        GameOptions.levelSelectedIndex = 0;

        bool available = InvokeAdapter(out GameObject prefab);

        Assert.IsTrue(available);
        Assert.IsNull(prefab);
    }
}
