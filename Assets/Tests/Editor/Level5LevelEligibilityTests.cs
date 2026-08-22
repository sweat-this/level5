using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Progression;
using NUnit.Framework;

/// <summary>
/// <see cref="LevelEligibility"/> is the one place content-selectable, mode-compatible and
/// account-unlocked are combined. These tests cover the composed policy itself, that menu cycling
/// uses it (<see cref="StartMenuSelectionState"/>), and that launch revalidates with the same
/// policy (<see cref="MatchConfigurationBuilder"/>) rather than trusting whatever the menu already
/// filtered.
/// </summary>
public class Level5LevelEligibilityTests
{
    private static UnlockSnapshot UnlockLevels(params int[] unlockedLevelIds)
    {
        Dictionary<int, bool> levels = new Dictionary<int, bool>();
        foreach (int id in unlockedLevelIds)
        {
            levels[id] = true;
        }

        return new UnlockSnapshot(new Dictionary<int, bool>(), levels);
    }

    private static GameModeCompatibility Build(IEnumerable<GameModeDefinition> modes, IEnumerable<LevelDefinition> levels)
    {
        return new GameModeCompatibility(new GameModeCatalog(modes), new LevelDefinitionCatalog(levels));
    }

    // ---- LevelEligibility.CanSelect / IsAvailable ----------------------------------------------

    [Test]
    public void ASelectableCompatibleUnlockedLevelIsEligible()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1, selectable: true, locked: false);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { level });

        Assert.That(LevelEligibility.CanSelect(level, mode, compatibility, UnlockLevels(1)), Is.True);
    }

    [Test]
    public void ANonSelectableLevelIsRejectedEvenWhenUnlocked()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1, selectable: false);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { level });

        Assert.That(LevelEligibility.CanSelect(level, mode, compatibility, UnlockLevels(1)), Is.False);
    }

    [Test]
    public void AnIncompatibleLevelIsRejectedEvenWhenUnlocked()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints); // needs Basketball
        LevelDefinition combatOnly = TestDefinitions.Level(1, ArenaCapability.Combat);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { combatOnly });

        Assert.That(LevelEligibility.CanSelect(combatOnly, mode, compatibility, UnlockLevels(1)), Is.False);
    }

    [Test]
    public void ACompatibleSelectableLevelIsRejectedWhenLocked()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { level });

        // Not present in the snapshot at all - the deterministic safe default is locked.
        Assert.That(LevelEligibility.CanSelect(level, mode, compatibility, UnlockLevels()), Is.False);
    }

    [Test]
    public void ANullSnapshotSkipsTheUnlockCheck()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { level });

        Assert.That(LevelEligibility.CanSelect(level, mode, compatibility, null), Is.True);
    }

    // ---- bounded cycling -------------------------------------------------------------------------

    [Test]
    public void CyclingSkipsLockedLevelsAndWraps()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition a = TestDefinitions.Level(1);
        LevelDefinition locked = TestDefinitions.Level(2, locked: true);
        LevelDefinition c = TestDefinitions.Level(3);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { a, locked, c });
        UnlockSnapshot unlock = UnlockLevels(1, 3); // 2 stays locked

        int next = LevelEligibility.NextEligibleLevelIndex(compatibility, mode, 0, 1, unlock);

        Assert.That(next, Is.EqualTo(2), "should skip index 1 (locked) and land on index 2");
    }

    [Test]
    public void CyclingTerminatesWhenNothingIsEligible()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition a = TestDefinitions.Level(1);
        LevelDefinition b = TestDefinitions.Level(2);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { a, b });
        UnlockSnapshot noneUnlocked = UnlockLevels();

        int next = LevelEligibility.NextEligibleLevelIndex(compatibility, mode, 0, 1, noneUnlocked);

        Assert.That(next, Is.EqualTo(0), "with nothing eligible, cycling must hold position rather than loop");
    }

    [Test]
    public void ModeChangeKeepsTheCurrentLevelWhenStillEligible()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition current = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { current });

        int index = LevelEligibility.EligibleLevelIndexFor(compatibility, mode, 0, 1, UnlockLevels(1));

        Assert.That(index, Is.EqualTo(0));
    }

    // ---- StartMenuSelectionState cycling uses LevelEligibility, not the mode-only check --------

    [Test]
    public void MenuCyclingSkipsALockedLevel()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition a = TestDefinitions.Level(1);
        LevelDefinition locked = TestDefinitions.Level(2, locked: true);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { a, locked });

        StartMenuSelectionState selection = new StartMenuSelectionState { LevelIndex = 0 };
        selection.CycleLevel(compatibility, 1, UnlockLevels(1));

        Assert.That(selection.LevelIndex, Is.EqualTo(0), "the only other level is locked, so cycling should hold");
    }

    [Test]
    public void MenuCyclingWithoutASnapshotBehavesAsBeforeUnlockGating()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition a = TestDefinitions.Level(1);
        LevelDefinition locked = TestDefinitions.Level(2, locked: true);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { a, locked });

        StartMenuSelectionState selection = new StartMenuSelectionState { LevelIndex = 0 };
        selection.CycleLevel(compatibility, 1);

        Assert.That(selection.LevelIndex, Is.EqualTo(1), "with no snapshot supplied, unlock gating is skipped (migration-compatible default)");
    }

    // ---- launch revalidation: MatchConfigurationBuilder re-checks, does not trust the menu -----

    [Test]
    public void AnUnlockedLevelLaunchesNormally()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(new MatchRequest(GameModeId.TotalPoints, 1, TestDefinitions.SoloRoster()), UnlockLevels(1));

        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
    }

    [Test]
    public void ALockedSelectedLevelIsRejectedAtLaunchEvenIfRequested()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1); // selectable, but not in the unlock snapshot
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(new MatchRequest(GameModeId.TotalPoints, 1, TestDefinitions.SoloRoster()), UnlockLevels());

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Validation.HasError(MatchValidationCode.LevelLocked), Is.True);
    }

    [Test]
    public void ANonSelectableLevelIsRejectedAtLaunch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1, selectable: false);
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(new MatchRequest(GameModeId.TotalPoints, 1, TestDefinitions.SoloRoster()), UnlockLevels(1));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Validation.HasError(MatchValidationCode.LevelNotSelectable), Is.True);
    }

    [Test]
    public void AStalePersistedLevelIndexPointingAtLockedContentFailsClosedAtLaunch()
    {
        // Simulates a remembered/persisted selection that no longer reflects account unlock state -
        // e.g. the level was locked after the index was saved. The menu never re-cycled it, so this
        // proves launch is the actual gate, not menu browsing.
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition unlocked = TestDefinitions.Level(1);
        LevelDefinition stale = TestDefinitions.Level(2);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { unlocked, stale });
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { unlocked, stale }), compatibility);

        StartMenuSelectionState selection = new StartMenuSelectionState { LevelIndex = 1, ModeIndex = 0 };
        MatchRequest request = selection.BuildRequest(compatibility, TestDefinitions.SoloRoster(), new List<CheerleaderProfile>());

        MatchBuildResult result = builder.Build(request, UnlockLevels(1)); // only level 1 is unlocked; index 1 -> level 2

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Validation.HasError(MatchValidationCode.LevelLocked), Is.True);
    }

    [Test]
    public void BuildWithoutASnapshotSkipsTheUnlockRecheck()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(new MatchRequest(GameModeId.TotalPoints, 1, TestDefinitions.SoloRoster()));

        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
    }
}
