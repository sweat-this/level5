using System.Collections.Generic;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// Compatibility, tested without a UI scene - which is the point of taking it out of the menu.
///
/// Each arena rule here is one of the conditions the recursive selection methods used to encode.
/// The last two tests cover the failure the recursion actually had: with nothing compatible it
/// called itself until the stack ran out.
/// </summary>
public class Level5MatchCompatibilityTests
{
    private static GameModeCompatibility Build(
        IEnumerable<GameModeDefinition> modes,
        IEnumerable<LevelDefinition> levels)
    {
        return new GameModeCompatibility(new GameModeCatalog(modes), new LevelDefinitionCatalog(levels));
    }

    [Test]
    public void AShootingModeNeedsABasketballArena()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition combatOnly = TestDefinitions.Level(1, ArenaCapability.Combat);
        LevelDefinition court = TestDefinitions.Level(2, ArenaCapability.Basketball);

        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { combatOnly, court });

        Assert.That(compatibility.CanPlay(mode, combatOnly), Is.False);
        Assert.That(compatibility.CanPlay(mode, court), Is.True);
    }

    [Test]
    public void AFightingModeNeedsACombatArena()
    {
        GameModeDefinition mode = TestDefinitions.Mode(
            GameModeId.BashUpSomeNerds,
            combatMode: CombatMode.Standard,
            enemiesOnly: true,
            requiresBasketball: false);
        LevelDefinition court = TestDefinitions.Level(1, ArenaCapability.Basketball);
        LevelDefinition arena = TestDefinitions.Level(2, ArenaCapability.Combat);

        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { court, arena });

        Assert.That(compatibility.CanPlay(mode, court), Is.False);
        Assert.That(compatibility.CanPlay(mode, arena), Is.True);
    }

    [Test]
    public void ACageModeNeedsACage()
    {
        GameModeDefinition mode = TestDefinitions.Mode(
            GameModeId.CageMatch,
            combatMode: CombatMode.Cage,
            enemiesOnly: true,
            requiresBasketball: false);
        LevelDefinition openArena = TestDefinitions.Level(1, ArenaCapability.Combat);
        LevelDefinition cage = TestDefinitions.Level(2, ArenaCapability.Combat | ArenaCapability.Cage);

        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { openArena, cage });

        Assert.That(compatibility.CanPlay(mode, openArena), Is.False);
        Assert.That(compatibility.CanPlay(mode, cage), Is.True);
    }

    [Test]
    public void ABattleRoyalArenaOnlyHostsBattleRoyal()
    {
        // The old menu enforced this both ways, and so does this: the battle royal mode needs the
        // arena, and every other mode is kept out of it.
        GameModeDefinition battleRoyal = TestDefinitions.Mode(
            GameModeId.BattleRoyal,
            combatMode: CombatMode.BattleRoyal,
            enemiesOnly: true,
            requiresBasketball: false);
        GameModeDefinition totalPoints = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition royaleArena = TestDefinitions.Level(
            1,
            ArenaCapability.Combat | ArenaCapability.BattleRoyal | ArenaCapability.Basketball);

        GameModeCompatibility compatibility = Build(
            new[] { battleRoyal, totalPoints },
            new[] { royaleArena });

        Assert.That(compatibility.CanPlay(battleRoyal, royaleArena), Is.True);
        Assert.That(compatibility.CanPlay(totalPoints, royaleArena), Is.False);
    }

    [Test]
    public void ASevenPointModeNeedsASevenPointLine()
    {
        GameModeDefinition mode = TestDefinitions.Mode(
            GameModeId.SpotUp7s,
            markers: ShotMarkerRequirement.SevenPoint,
            required: ArenaCapability.SevenPointLine);
        LevelDefinition plainCourt = TestDefinitions.Level(1, ArenaCapability.Basketball);
        LevelDefinition sevenCourt = TestDefinitions.Level(
            2,
            ArenaCapability.Basketball | ArenaCapability.SevenPointLine);

        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { plainCourt, sevenCourt });

        ValidationResult verdict = compatibility.Validate(new MatchRequest(
            GameModeId.SpotUp7s,
            1,
            TestDefinitions.SoloRoster()));

        Assert.That(verdict.IsValid, Is.False);
        Assert.That(verdict.HasError(MatchValidationCode.ArenaLacksSevenPointLine), Is.True);
        Assert.That(compatibility.CanPlay(mode, sevenCourt), Is.True);
    }

    [Test]
    public void RosterBoundsAreEnforced()
    {
        GameModeDefinition lockdown = TestDefinitions.Mode(
            GameModeId.Lockdown,
            maxPlayers: 1,
            addsImplicitDefender: true);
        LevelDefinition court = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { lockdown }, new[] { court });

        PlayerRoster tooMany = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("me")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("them"))
        });

        ValidationResult verdict = compatibility.Validate(
            new MatchRequest(GameModeId.Lockdown, 1, tooMany));

        Assert.That(verdict.HasError(MatchValidationCode.RosterTooLarge), Is.True);
    }

    [Test]
    public void RemoteAndReplayParticipantsAreRejectedUntilTheyExist()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition court = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { court });

        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("me")),
            new PlayerRosterEntry(PlayerControlType.RemoteHuman, TestDefinitions.Character("them"))
        });

        ValidationResult verdict = compatibility.Validate(
            new MatchRequest(GameModeId.TotalPoints, 1, roster));

        Assert.That(verdict.HasError(MatchValidationCode.ParticipantTypeNotSupported), Is.True);
    }

    [Test]
    public void AFightingSetupRejectsACharacterWhoCannotFight()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition court = TestDefinitions.Level(1);
        GameModeCompatibility compatibility = Build(new[] { mode }, new[] { court });

        // Enemies switched on as a modifier makes a shooting mode a fighting one, exactly as the
        // menu's character cycling treated it.
        ValidationResult verdict = compatibility.Validate(new MatchRequest(
            GameModeId.TotalPoints,
            1,
            TestDefinitions.SoloRoster("shooter", isShooter: true, isFighter: false),
            new MatchModifiers(enemiesRequested: true)));

        Assert.That(verdict.HasError(MatchValidationCode.CharacterCannotFight), Is.True);
    }

    [Test]
    public void CyclingLevelsSkipsIncompatibleArenasAndWraps()
    {
        GameModeDefinition cageMode = TestDefinitions.Mode(
            GameModeId.CageMatch,
            combatMode: CombatMode.Cage,
            enemiesOnly: true,
            requiresBasketball: false);

        LevelDefinition court = TestDefinitions.Level(1, ArenaCapability.Basketball);
        LevelDefinition cageA = TestDefinitions.Level(2, ArenaCapability.Combat | ArenaCapability.Cage);
        LevelDefinition openArena = TestDefinitions.Level(3, ArenaCapability.Combat);
        LevelDefinition cageB = TestDefinitions.Level(4, ArenaCapability.Combat | ArenaCapability.Cage);

        GameModeCompatibility compatibility = Build(
            new[] { cageMode },
            new[] { court, cageA, openArena, cageB });

        Assert.That(compatibility.NextCompatibleLevelIndex(cageMode, 0, 1), Is.EqualTo(1));
        Assert.That(compatibility.NextCompatibleLevelIndex(cageMode, 1, 1), Is.EqualTo(3));
        Assert.That(compatibility.NextCompatibleLevelIndex(cageMode, 3, 1), Is.EqualTo(1), "wraps");
        Assert.That(compatibility.NextCompatibleLevelIndex(cageMode, 1, -1), Is.EqualTo(3), "wraps backwards");
    }

    [Test]
    public void CyclingTerminatesWhenNoArenaIsCompatible()
    {
        // This is the case the recursion could not survive: changeSelectedLevelUp() called itself
        // for every level and then kept going.
        GameModeDefinition impossible = TestDefinitions.Mode(
            GameModeId.BattleRoyal,
            combatMode: CombatMode.BattleRoyal,
            enemiesOnly: true,
            requiresBasketball: false);

        GameModeCompatibility compatibility = Build(
            new[] { impossible },
            new[] { TestDefinitions.Level(1, ArenaCapability.Basketball), TestDefinitions.Level(2, ArenaCapability.Basketball) });

        Assert.That(compatibility.NextCompatibleLevelIndex(impossible, 0, 1), Is.EqualTo(0));
        Assert.That(compatibility.LevelsFor(impossible), Is.Empty);
    }

    [Test]
    public void ChangingModeKeepsTheCurrentArenaWhenItStillFits()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        GameModeCompatibility compatibility = Build(
            new[] { mode },
            new[] { TestDefinitions.Level(1), TestDefinitions.Level(2) });

        Assert.That(compatibility.CompatibleLevelIndexFor(mode, 1, 1), Is.EqualTo(1));
    }
}
