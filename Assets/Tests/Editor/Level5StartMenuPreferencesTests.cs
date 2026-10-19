using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

public class Level5StartMenuPreferencesTests
{
    private GameOptionsSnapshot snapshot;

    [SetUp]
    public void SetUp() => snapshot = GameOptionsSnapshot.Capture();

    [TearDown]
    public void TearDown() => snapshot.Restore();

    private static StartMenuSelectionState Reload()
    {
        var state = new StartMenuSelectionState { Difficulty = MatchDifficulty.Hardcore };
        state.LoadPersistedPreferences();
        return state;
    }

    [Test]
    public void BrowsingDoesNotReplaceLastLaunchSelectionsOrModifiers()
    {
        var launched = new StartMenuSelectionState { ModeIndex = 2, LevelIndex = 3, FriendIndex = 4 };
        launched.SavePersistedPreferences();
        StartMenuPreferences.RememberResolvedModifiers(new ResolvedMatchRules(
            trafficEnabled: true, hardcore: true, obstaclesEnabled: true));
        var editing = Reload();
        editing.ModeIndex = 8;
        editing.LevelIndex = 9;
        editing.FriendIndex = 10;
        editing.TrafficEnabled = editing.HardcoreEnabled = editing.ObstaclesEnabled = false;
        editing.Difficulty = MatchDifficulty.Hardcore;

        var reentry = Reload();
        Assert.That(new[] { reentry.ModeIndex, reentry.LevelIndex, reentry.FriendIndex }, Is.EqualTo(new[] { 2, 3, 4 }));
        Assert.That(reentry.TrafficEnabled && reentry.HardcoreEnabled && reentry.ObstaclesEnabled, Is.True);
        Assert.That(reentry.Difficulty, Is.EqualTo(MatchDifficulty.Normal));
    }

    [Test]
    public void BridgeRemembersResolvedModifiersWithoutOverwritingMenuIndicesOrLiveEdits()
    {
        var editing = new StartMenuSelectionState
        {
            ModeIndex = 7, LevelIndex = 8, FriendIndex = 9,
            TrafficEnabled = true, HardcoreEnabled = true, ObstaclesEnabled = true
        };
        editing.SavePersistedPreferences();
        var mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        var builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1, ArenaCapability.Basketball) }));
        var result = builder.Build(new MatchRequest(mode.Id, 1, TestDefinitions.SoloRoster(),
            new MatchModifiers(trafficRequested: true, hardcoreRequested: true, obstaclesRequested: true)));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        LegacyGameOptionsBridge.Apply(result.Configuration);

        var reentry = Reload();
        Assert.That(new[] { reentry.ModeIndex, reentry.LevelIndex, reentry.FriendIndex }, Is.EqualTo(new[] { 7, 8, 9 }));
        Assert.That(reentry.TrafficEnabled, Is.False, "the arena rejected requested traffic");
        Assert.That(reentry.ObstaclesEnabled, Is.EqualTo(result.Configuration.Rules.ObstaclesEnabled));
        Assert.That(reentry.HardcoreEnabled, Is.EqualTo(result.Configuration.Rules.Hardcore));
        Assert.That(GameOptions.trafficEnabled, Is.EqualTo(result.Configuration.Rules.TrafficEnabled));
        Assert.That(GameOptions.obstaclesEnabled, Is.EqualTo(result.Configuration.Rules.ObstaclesEnabled));
        Assert.That(GameOptions.hardcoreModeEnabled, Is.EqualTo(result.Configuration.Rules.Hardcore));
        Assert.That(editing.TrafficEnabled && editing.ObstaclesEnabled, Is.True, "publication must not edit the menu draft");
        Object.DestroyImmediate(mode);
    }

    [Test]
    public void NewPlaySessionMakesTipsEligibleEvenWithoutDomainReload()
    {
        bool shown = StartFlowPresentation.TipsShown;
        try
        {
            StartFlowPresentation.MarkTipsShown();
            MethodInfo reset = typeof(StartFlowPresentation).GetMethod("ResetForNextPass");
            Assert.That(reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType,
                Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
            reset.Invoke(null, null);
            Assert.That(StartFlowPresentation.TipsShown, Is.False);
        }
        finally
        {
            StartFlowPresentation.ResetForNextPass();
            if (shown) StartFlowPresentation.MarkTipsShown();
        }
    }

    [Test]
    public void NewPlaySessionClearsMemoryEvenWithoutDomainReload()
    {
        new StartMenuSelectionState { ModeIndex = 5, LevelIndex = 6, FriendIndex = 7 }.SavePersistedPreferences();
        StartMenuPreferences.RememberResolvedModifiers(new ResolvedMatchRules(
            trafficEnabled: true, hardcore: true, obstaclesEnabled: true));
        MethodInfo reset = typeof(StartMenuPreferences).GetMethod("ResetForNewProcess", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType,
            Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
        reset.Invoke(null, null);
        var reentry = Reload();
        Assert.That(new[] { reentry.ModeIndex, reentry.LevelIndex, reentry.FriendIndex }, Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(reentry.TrafficEnabled || reentry.HardcoreEnabled || reentry.ObstaclesEnabled, Is.False);
    }
}
