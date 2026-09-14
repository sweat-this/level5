using System;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// AUD-012 Phase 2b Slice 52: <c>MatchRuntime</c> moved into <c>Level5.Match</c> by replacing its direct
/// <c>GameOptions</c>/<c>Modes</c> reads with a <see cref="LegacyMatchRuntimeSnapshot"/> resolved
/// through one installable reader. These tests cover what that seam has to keep true: a validated
/// <see cref="ActiveMatch"/> configuration still wins and does not require the reader at all, the
/// legacy fallback still reconstructs exactly what it used to (roster, rules, mode/level identity,
/// primary character, cheerleader, the out-of-roster input-slot formula, and the Lockdown implicit
/// defender that used to compare against <c>Modes.Lockdown</c>), fallback reads stay live rather than
/// snapshotting once per scene, and the reader's install/reset lifecycle fails closed rather than
/// silently fabricating rules. <c>Level5MatchBridgeParityTests.TheRuntimeReadsBackTheSameRulesTheBridgeWrote</c>
/// already covers the bridge/runtime rules round-trip and is not repeated here.
/// </summary>
public class Level5MatchRuntimeLegacyFallbackTests
{
    private GameOptionsSnapshot gameOptionsSnapshot;

    [SetUp]
    public void SetUp()
    {
        gameOptionsSnapshot = GameOptionsSnapshot.Capture();
        ActiveMatch.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();
        gameOptionsSnapshot.Restore();

        // A lifecycle test may have reset this fixture's own reader; put back the assembly-wide
        // production install every other fixture depends on (Level5MatchRuntimeFallbackTestBootstrap).
        MatchRuntime.InstallLegacyFallbackReader(Level5MatchRuntimeFallbackTestSupport.ProductionReader());
    }

    private static MatchConfiguration Configure(
        GameModeDefinition mode,
        LevelDefinition level,
        PlayerRoster roster = null)
    {
        roster ??= TestDefinitions.SoloRoster();
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(new MatchRequest(mode.Id, level.LevelId, roster, MatchModifiers.Default));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        return result.Configuration;
    }

    // ==================== configured path ====================

    [Test]
    public void ConfiguredMatchAnswersEverythingFromActiveMatchNotLegacyGlobals()
    {
        LevelDefinition level = TestDefinitions.Level(
            9,
            ArenaCapability.Basketball | ArenaCapability.SevenPointLine | ArenaCapability.Weather | ArenaCapability.TimeOfDay,
            objectName: "level_09");
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.VersusCpu);
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("configured-primary", characterId: 42)),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("configured-cpu", characterId: 7))
        });

        // Legacy globals deliberately disagree with the configuration, so a read that used them by
        // mistake would be caught immediately rather than passing by coincidence.
        GameOptions.gameModeSelectedId = 999;
        GameOptions.levelDisplayName = "stale legacy level";
        GameOptions.numPlayers = 1;

        ActiveMatch.Begin(Configure(mode, level, roster));

        Assert.That(MatchRuntime.HasConfiguration, Is.True);
        Assert.That(MatchRuntime.Roster.Count, Is.EqualTo(2));
        Assert.That(MatchRuntime.Roster.GetBySlotId(0).Character.ObjectName, Is.EqualTo("configured-primary"));
        Assert.That(MatchRuntime.ModeId, Is.EqualTo(GameModeId.VersusCpu));
        Assert.That(MatchRuntime.LevelDisplayName, Is.EqualTo("level 9"));
        Assert.That(MatchRuntime.LevelId, Is.EqualTo(9));
        Assert.That(MatchRuntime.LevelHasWeather, Is.True);
        Assert.That(MatchRuntime.LevelRequiresTimeOfDay, Is.True);
        Assert.That(MatchRuntime.ParticipantCount, Is.EqualTo(2));
    }

    [Test]
    public void ConfiguredMatchReadsSucceedWithNoFallbackReaderInstalled()
    {
        // The configured path must not need the fallback at all - a well-formed roster's primary
        // character never falls through to it either.
        MatchRuntime.ResetLegacyFallbackReader();
        try
        {
            GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
            LevelDefinition level = TestDefinitions.Level(1);
            PlayerRoster roster = PlayerRoster.SingleLocalHuman(TestDefinitions.Character("well-formed", characterId: 5));
            ActiveMatch.Begin(Configure(mode, level, roster));

            Assert.DoesNotThrow(() =>
            {
                _ = MatchRuntime.Rules;
                _ = MatchRuntime.Roster;
                _ = MatchRuntime.ModeId;
                _ = MatchRuntime.LevelDisplayName;
                _ = MatchRuntime.Cheerleader;
                _ = MatchRuntime.ParticipantCount;
                _ = MatchRuntime.PrimaryCharacterDisplayName;
                _ = MatchRuntime.PrimaryCharacterObjectName;
                _ = MatchRuntime.PrimaryCharacterId;
                _ = MatchRuntime.LocalInputSlotFor(0);
            });
        }
        finally
        {
            MatchRuntime.InstallLegacyFallbackReader(Level5MatchRuntimeFallbackTestSupport.ProductionReader());
        }
    }

    [Test]
    public void PrimaryCharacterAccessorsFallBackToLegacyGlobalsEvenWithAConfigurationActive()
    {
        // Preserves an existing oddity, not introduced by this move: PrimaryCharacter*/LocalInputSlotFor
        // consult the legacy fallback whenever the roster itself has nothing to say, regardless of
        // whether ActiveMatch has a configuration.
        GameOptions.characterDisplayName = "legacy display name";
        GameOptions.characterObjectName = "legacy_object";
        GameOptions.characterId = 77;

        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = PlayerRoster.SingleLocalHuman(CharacterSelection.None);
        ActiveMatch.Begin(Configure(mode, level, roster));

        Assert.That(MatchRuntime.PrimaryCharacterDisplayName, Is.EqualTo("legacy display name"));
        Assert.That(MatchRuntime.PrimaryCharacterObjectName, Is.EqualTo("legacy_object"));
        Assert.That(MatchRuntime.PrimaryCharacterId, Is.EqualTo(77));
    }

    // ==================== legacy fallback path ====================

    [Test]
    public void LegacyFallbackReconstructsTheRosterFromGameOptions()
    {
        GameOptions.numPlayers = 3;
        GameOptions.player1IsCpu = false;
        GameOptions.player2IsCpu = true;
        GameOptions.player3IsCpu = false;
        GameOptions.characterObjectNames = new System.Collections.Generic.List<string> { "me", "cpu1", "friend" };
        GameOptions.characterId = 11;
        GameOptions.characterDisplayName = "Dr Blood";

        PlayerRoster roster = MatchRuntime.Roster;

        Assert.That(roster.Count, Is.EqualTo(3));
        Assert.That(roster.GetBySlotId(0).IsCpu, Is.False);
        Assert.That(roster.GetBySlotId(0).Character.CharacterId, Is.EqualTo(11));
        Assert.That(roster.GetBySlotId(0).Character.DisplayName, Is.EqualTo("Dr Blood"));
        Assert.That(roster.GetBySlotId(0).LocalInputSlot, Is.EqualTo(0));
        Assert.That(roster.GetBySlotId(1).IsCpu, Is.True);
        Assert.That(roster.GetBySlotId(1).Character.ObjectName, Is.EqualTo("cpu1"));
        Assert.That(roster.GetBySlotId(2).IsCpu, Is.False);
        Assert.That(roster.GetBySlotId(2).Character.ObjectName, Is.EqualTo("friend"));
        // Slot 2 is the second local human once slot 1 (a CPU) is skipped.
        Assert.That(roster.GetBySlotId(2).LocalInputSlot, Is.EqualTo(1));
    }

    [Test]
    public void LegacyFallbackClampsPlayerCountIntoRange()
    {
        GameOptions.numPlayers = 99;

        Assert.That(MatchRuntime.Roster.Count, Is.EqualTo(PlayerRoster.MaxSlots));
    }

    [Test]
    public void LegacyFallbackReconstructsModeAndLevelIdentity()
    {
        GameOptions.gameModeSelectedId = (int)GameModeId.SevenPointContest;
        GameOptions.gameModeSelectedName = "Seven Point Contest";
        GameOptions.levelId = 4;
        GameOptions.levelDisplayName = "The Dome";
        GameOptions.levelRequiresTimeOfDay = true;
        GameOptions.levelRequiresWeather = true;
        GameOptions.levelHasSevenPointers = true;

        Assert.That(MatchRuntime.ModeId, Is.EqualTo(GameModeId.SevenPointContest));
        Assert.That(MatchRuntime.RawModeId, Is.EqualTo((int)GameModeId.SevenPointContest));
        Assert.That(MatchRuntime.ModeDisplayName, Is.EqualTo("Seven Point Contest"));
        Assert.That(MatchRuntime.LevelId, Is.EqualTo(4));
        Assert.That(MatchRuntime.LevelDisplayName, Is.EqualTo("The Dome"));
        Assert.That(MatchRuntime.LevelRequiresTimeOfDay, Is.True);
        Assert.That(MatchRuntime.LevelHasWeather, Is.True);
        Assert.That(MatchRuntime.LevelHasSevenPointers, Is.True);
        // Level is always null when unconfigured - CustomCamera has no per-level authored value to fall
        // back to, and never did.
        Assert.That(MatchRuntime.CustomCamera, Is.False);
    }

    [Test]
    public void LegacyFallbackReconstructsTheCheerleader()
    {
        GameOptions.cheerleaderObjectName = "cheer_obj";
        GameOptions.cheerleaderDisplayName = "Cheer Display";

        CheerleaderSelection cheerleader = MatchRuntime.Cheerleader;

        Assert.That(cheerleader.ObjectName, Is.EqualTo("cheer_obj"));
        Assert.That(cheerleader.DisplayName, Is.EqualTo("Cheer Display"));
        Assert.That(cheerleader.BonusThreeAccuracy, Is.EqualTo(0), "a directly entered scene has no cheerleader bonuses");
    }

    [Test]
    public void LegacyFallbackAddsTheLockdownImplicitDefenderWithoutTheModesClass()
    {
        // Modes.Lockdown == 27 == GameModeId.Lockdown; this used to be MatchRuntime's own Assembly-CSharp
        // edge into Modes.cs. The builder that replaced it (GameOptions.cs, already Assembly-CSharp)
        // keeps the exact same comparison - this proves the answer is unchanged, not just that it compiles.
        GameOptions.gameModeSelectedId = 27;

        Assert.That(MatchRuntime.Rules.AddsImplicitDefender, Is.True);
    }

    [Test]
    public void LocalInputSlotForFallsBackToTheLegacyFormulaForASlotOutsideTheRoster()
    {
        GameOptions.numPlayers = 1;
        GameOptions.player1IsCpu = false;
        GameOptions.player2IsCpu = true;
        GameOptions.player3IsCpu = false;
        GameOptions.player4IsCpu = false;

        // Slot 2 is outside the one-slot reconstructed roster, so this falls through to
        // GameOptions.GetHumanPlayerInputSlot(2) - not CPU, and slots 0/1 before it contain exactly one
        // non-CPU (slot 0), so it resolves to input slot 1.
        Assert.That(MatchRuntime.LocalInputSlotFor(2), Is.EqualTo(GameOptions.GetHumanPlayerInputSlot(2)));
        Assert.That(MatchRuntime.LocalInputSlotFor(2), Is.EqualTo(1));
    }

    [Test]
    public void LocalInputSlotForFallsBackToTheLegacyFormulaEvenWhenConfiguredButSlotIsOutsideTheRoster()
    {
        // The one place a configured match can still need the fallback reader: preserved from before
        // this move (see PrimaryCharacterDisplayName/ObjectName/Id for the identical, pre-existing,
        // ungated-on-configuration shape), not introduced by it.
        GameOptions.player1IsCpu = false;
        GameOptions.player2IsCpu = true;
        GameOptions.player3IsCpu = false;
        GameOptions.player4IsCpu = false;

        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();
        ActiveMatch.Begin(Configure(mode, level, roster));

        // Slot 3 is outside the one-slot configured roster. Of the three slots before it, only slots
        // 0 and 2 are non-CPU (slot 1 is), so it resolves to input slot 2.
        Assert.That(MatchRuntime.LocalInputSlotFor(3), Is.EqualTo(GameOptions.GetHumanPlayerInputSlot(3)));
        Assert.That(MatchRuntime.LocalInputSlotFor(3), Is.EqualTo(2));
    }

    // ==================== reader invocation count ====================

    [Test]
    public void PrimaryCharacterDisplayNameBuildsTheFallbackSnapshotAtMostOnce()
    {
        GameOptions.characterDisplayName = string.Empty;
        GameOptions.numPlayers = 1;
        int callCount = 0;
        InstallCountingReader(() => callCount++);

        _ = MatchRuntime.PrimaryCharacterDisplayName;

        Assert.That(
            callCount,
            Is.EqualTo(1),
            "resolving the roster (to find slot zero's character) and resolving the character-field "
            + "fallback must share one snapshot, not build two");
    }

    [Test]
    public void PrimaryCharacterDisplayNameBuildsTheFallbackSnapshotAtMostOnceWhenConfiguredButPrimaryIsEmpty()
    {
        GameOptions.characterDisplayName = "legacy fallback name";
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        ActiveMatch.Begin(Configure(mode, level, PlayerRoster.SingleLocalHuman(CharacterSelection.None)));
        int callCount = 0;
        InstallCountingReader(() => callCount++);

        Assert.That(MatchRuntime.PrimaryCharacterDisplayName, Is.EqualTo("legacy fallback name"));
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void LocalInputSlotForBuildsTheFallbackSnapshotAtMostOnce()
    {
        GameOptions.numPlayers = 1;
        int callCount = 0;
        InstallCountingReader(() => callCount++);

        _ = MatchRuntime.LocalInputSlotFor(3);

        Assert.That(
            callCount,
            Is.EqualTo(1),
            "resolving the roster (slot 3 isn't in it) and resolving the out-of-roster input-slot "
            + "formula must share one snapshot, not build two");
    }

    private static void InstallCountingReader(Action onRead)
    {
        Func<LegacyMatchRuntimeSnapshot> production = Level5MatchRuntimeFallbackTestSupport.ProductionReader();
        MatchRuntime.InstallLegacyFallbackReader(() =>
        {
            onRead();
            return production();
        });
    }

    // ==================== liveness ====================

    [Test]
    public void LegacyFallbackReadsAreLiveNotSnapshottedForTheWholeScene()
    {
        GameOptions.numPlayers = 1;
        GameOptions.characterDisplayName = "First";

        Assert.That(MatchRuntime.Roster.GetBySlotId(0).Character.DisplayName, Is.EqualTo("First"));

        GameOptions.characterDisplayName = "Second";

        Assert.That(
            MatchRuntime.Roster.GetBySlotId(0).Character.DisplayName,
            Is.EqualTo("Second"),
            "a second fallback read must reflect the legacy global as it is now, not a value captured "
            + "by an earlier read");
    }

    // ==================== reader lifecycle ====================

    [Test]
    public void MissingFallbackReaderProducesAnExplicitErrorRatherThanDefaultRules()
    {
        MatchRuntime.ResetLegacyFallbackReader();
        try
        {
            Assert.Throws<InvalidOperationException>(() => _ = MatchRuntime.Rules);
        }
        finally
        {
            MatchRuntime.InstallLegacyFallbackReader(Level5MatchRuntimeFallbackTestSupport.ProductionReader());
        }
    }

    [Test]
    public void ResetIsIdempotentAndReinstallingRecoversTheRealMapping()
    {
        MatchRuntime.ResetLegacyFallbackReader();
        MatchRuntime.ResetLegacyFallbackReader();
        Assert.Throws<InvalidOperationException>(() => _ = MatchRuntime.Roster);

        MatchRuntime.InstallLegacyFallbackReader(Level5MatchRuntimeFallbackTestSupport.ProductionReader());

        GameOptions.numPlayers = 1;
        Assert.DoesNotThrow(() => _ = MatchRuntime.Roster);
    }

    [Test]
    public void InstallLegacyFallbackReaderRejectsANullReader()
    {
        Assert.Throws<ArgumentNullException>(() => MatchRuntime.InstallLegacyFallbackReader(null));
    }
}
