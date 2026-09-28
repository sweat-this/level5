using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 2b Slice 20: <see cref="LegacyMatchCatalogBootstrap"/> is the only thing left that
/// converts the legacy start-menu prefab data into what <c>MatchCatalogs</c> (now <c>Level5.Match</c>)
/// accepts, and it is also the only thing left holding the source-reference cache that used to live
/// inside <c>MatchCatalogs</c> itself. These prove that split kept the cache's effective behavior:
/// the same legacy source lists reuse the previous conversion, <c>MatchCatalogs.Reset()</c> still
/// forces a real rebuild even when the legacy sources have not changed, and a fallback conversion
/// anomaly is still visible through <c>MatchCatalogs.ConversionAnomalies</c>.
///
/// Uses <see cref="LoadManager.TryLoadFallbackData"/> for real mode/level source lists, the same
/// technique <see cref="LoadManagerFallbackDataTests"/> uses, since this project's authored
/// <c>Resources/Match</c> folders do not exist yet (see systems-restructure-plan.md) and the fallback
/// conversion path is therefore the one actually live in production.
/// </summary>
public class LegacyMatchCatalogBootstrapTests
{
    private GameObject host;
    private LoadManager manager;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("LegacyMatchCatalogBootstrapTest");
        host.SetActive(false);
        manager = host.AddComponent<LoadManager>();
        bool loaded = manager.TryLoadFallbackData(out string error);
        Assert.IsTrue(loaded, "TryLoadFallbackData should load this project's default catalogs: " + error);
    }

    [TearDown]
    public void TearDown()
    {
        LegacyMatchCatalogBootstrap.Reset();
        MatchCatalogs.Reset();
        Object.DestroyImmediate(host);
    }

    [Test]
    public void RepeatedBootstrapWithTheSameSourceListsDoesNotRebuildTheCatalogs()
    {
        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);
        GameModeCatalog firstModes = MatchCatalogs.Modes;
        LevelDefinitionCatalog firstLevels = MatchCatalogs.Levels;

        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);

        Assert.That(
            ReferenceEquals(MatchCatalogs.Modes, firstModes),
            Is.True,
            "the same mode source-list reference should reuse the already-built catalog, not rebuild it");
        Assert.That(
            ReferenceEquals(MatchCatalogs.Levels, firstLevels),
            Is.True,
            "the same level source-list reference should reuse the already-built catalog, not rebuild it");
    }

    [Test]
    public void ResetFollowedByTheSameBootstrapInputRepopulatesTheCatalogs()
    {
        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);
        Assert.That(MatchCatalogs.IsReady, Is.True, "precondition: the first build should succeed");

        MatchCatalogs.Reset();
        Assert.That(MatchCatalogs.IsReady, Is.False, "precondition: Reset() should clear the catalogs");

        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);

        Assert.That(
            MatchCatalogs.IsReady,
            Is.True,
            "the same legacy source lists should still rebuild the catalogs after Reset(), even though "
                + "the bootstrap's own conversion cache did not see the source lists change");
    }

    [Test]
    public void ResetClearsTheBootstrapsOwnConversionCache()
    {
        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);
        GameModeCatalog firstModes = MatchCatalogs.Modes;

        LegacyMatchCatalogBootstrap.Reset();
        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);

        Assert.That(
            ReferenceEquals(MatchCatalogs.Modes, firstModes),
            Is.False,
            "LegacyMatchCatalogBootstrap.Reset() should clear its own conversion cache, forcing a fresh "
                + "conversion (and therefore a fresh catalog) even though MatchCatalogs itself was not "
                + "separately reset and the legacy source-list reference did not change");
    }

    /// <summary>
    /// Issue #198/#200: correspondence's remote launch preflight (<see cref="LevelEligibility"/>)
    /// and <see cref="RemoteAttemptLauncher"/> both gate on whatever
    /// <c>DefaultLevelId = 1</c> actually resolves to in the real fallback catalog - not on a
    /// hand-built <c>TestDefinitions.Level(...)</c> stand-in. This proves the production
    /// composition (<c>level_selected_01_scrapyard.prefab</c> -&gt;
    /// <see cref="LevelDefinitionFactory"/> -&gt; <see cref="LegacyMatchCatalogBootstrap"/> -&gt;
    /// <see cref="MatchCatalogs"/>) still resolves level id 1 as selectable and unlocked today, so
    /// a passing focused/unit suite for #198 cannot be mistaking synthetic fixture data for reality.
    /// </summary>
    [Test]
    public void ProductionFallbackDataResolvesLevelOneAsTheUnlockedSelectableScrapyard()
    {
        LegacyMatchCatalogBootstrap.EnsureBuilt(manager.ModeSelectedData, manager.LevelSelectedData);

        LevelDefinition level = MatchCatalogs.Levels.Find(1);

        Assert.That(level, Is.Not.Null, "the production fallback catalog should resolve level id 1");
        Assert.That(level.DisplayName, Is.EqualTo("The Scrapyard"));
        Assert.That(level.Selectable, Is.True);
        Assert.That(level.Locked, Is.False);

        UnlockSnapshot unlock = UnlockSnapshotBuilder.Build(
            new List<CharacterProfile>(), new List<CharacterProfile>(), MatchCatalogs.Levels);

        Assert.That(unlock.IsLevelUnlocked(1), Is.True);
    }

    [Test]
    public void FallbackConversionAnomaliesRemainVisibleThroughMatchCatalogs()
    {
        StartScreenModeSelected contradictoryMode = host.AddComponent<StartScreenModeSelected>();
        SerializedObject serialized = new SerializedObject(contradictoryMode);
        serialized.FindProperty("modeId").intValue = 9001;
        serialized.FindProperty("modeDisplayName").stringValue = "Bootstrap Test Mode";
        serialized.FindProperty("modeRequiresCounter").boolValue = true;
        serialized.FindProperty("modeRequiresCountDown").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<StartScreenModeSelected> modeSources = new List<StartScreenModeSelected> { contradictoryMode };

        // ReportProblems logs every anomaly through Debug.LogError - real, existing behavior this
        // test is not re-verifying - so the expected message has to be told to the log-message
        // assertion instead of failing the test as an unhandled error.
        LogAssert.Expect(
            LogType.Error,
            "Level 5 game mode catalog: Bootstrap Test Mode sets both modeRequiresCountDown and "
                + "modeRequiresCounter; treating it as a countdown");

        LegacyMatchCatalogBootstrap.EnsureBuilt(modeSources, manager.LevelSelectedData);

        Assert.That(MatchCatalogs.ConversionAnomalies, Is.Not.Empty);
        Assert.That(
            MatchCatalogs.ConversionAnomalies[0],
            Does.Contain("modeRequiresCountDown").And.Contain("modeRequiresCounter"));
    }
}
