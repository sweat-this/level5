using System;
using System.Collections.Generic;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// The match domain: typed identity, catalogs and the roster.
///
/// The identity tests are the ones that matter most. Those numbers are in save files, in high score
/// rows and on the backend; a renumbered mode does not fail loudly, it silently reads someone
/// else's history.
/// </summary>
public class Level5MatchDomainTests
{
    [Test]
    public void TypedModeIdsMatchTheLegacyNumbers()
    {
        // If this fails, either GameModeId or Modes was renumbered. Fix the new one - the old
        // numbers are the contract with persisted data.
        Assert.That((int)GameModeId.TotalPoints, Is.EqualTo(Modes.TotalPoints));
        Assert.That((int)GameModeId.Total3Pointers, Is.EqualTo(Modes.Total3Pointers));
        Assert.That((int)GameModeId.Total4Pointers, Is.EqualTo(Modes.Total4Pointers));
        Assert.That((int)GameModeId.Total7Pointers, Is.EqualTo(Modes.Total7Pointers));
        Assert.That((int)GameModeId.TotalDistance, Is.EqualTo(Modes.TotalDistance));
        Assert.That((int)GameModeId.SpotUp3s, Is.EqualTo(Modes.SpotUp3s));
        Assert.That((int)GameModeId.SpotUp4s, Is.EqualTo(Modes.SpotUp4s));
        Assert.That((int)GameModeId.SpotUpAll, Is.EqualTo(Modes.SpotUpAll));
        Assert.That((int)GameModeId.ConsecutiveShots, Is.EqualTo(Modes.ConsecutiveShots));
        Assert.That((int)GameModeId.InThePocket, Is.EqualTo(Modes.InThePocket));
        Assert.That((int)GameModeId.ThreePointContest, Is.EqualTo(Modes.ThreePointContest));
        Assert.That((int)GameModeId.FourPointContest, Is.EqualTo(Modes.FourPointContest));
        Assert.That((int)GameModeId.AllPointContest, Is.EqualTo(Modes.AllPointContest));
        Assert.That((int)GameModeId.PointsByDistance, Is.EqualTo(Modes.PointsByDistance));
        Assert.That((int)GameModeId.BashUpSomeNerds, Is.EqualTo(Modes.BashUpSomeNerds));
        Assert.That((int)GameModeId.BattleRoyal, Is.EqualTo(Modes.BattleRoyal));
        Assert.That((int)GameModeId.CageMatch, Is.EqualTo(Modes.CageMatch));
        Assert.That((int)GameModeId.VersusCpu, Is.EqualTo(Modes.VersusCpu));
        Assert.That((int)GameModeId.SevenPointContest, Is.EqualTo(Modes.SevenPointContest));
        Assert.That((int)GameModeId.SpotUp7s, Is.EqualTo(Modes.SpotUp7s));
        Assert.That((int)GameModeId.BeatThaComputahs, Is.EqualTo(Modes.BeatThaComputahs));
        Assert.That((int)GameModeId.Lockdown, Is.EqualTo(Modes.Lockdown));
        Assert.That((int)GameModeId.Arcade, Is.EqualTo(Modes.ArcadeMode));
        Assert.That((int)GameModeId.FreePlay, Is.EqualTo(Modes.FreePlay));
    }

    [Test]
    public void EveryLegacyModeConstantHasATypedId()
    {
        foreach (System.Reflection.FieldInfo field in typeof(Modes).GetFields())
        {
            if (!field.IsLiteral || field.FieldType != typeof(int))
            {
                continue;
            }

            int value = (int)field.GetRawConstantValue();
            Assert.That(
                GameModeIds.IsKnown(value),
                Is.True,
                $"Modes.{field.Name} = {value} has no GameModeId member");
        }
    }

    [Test]
    public void ModeIdsAreUnique()
    {
        HashSet<int> seen = new HashSet<int>();
        foreach (GameModeId id in GameModeIds.All())
        {
            Assert.That(seen.Add((int)id), Is.True, $"duplicate numeric value for {id}");
        }
    }

    [Test]
    public void UnknownModeNumbersReadAsNoneRatherThanThrowing()
    {
        // Old saves and hand-edited rows do contain ids this build never shipped.
        Assert.That(GameModeIds.FromInt(1234), Is.EqualTo(GameModeId.None));
        Assert.That(GameModeIds.FromInt(Modes.Lockdown), Is.EqualTo(GameModeId.Lockdown));
    }

    [Test]
    public void CatalogRejectsDuplicateModeIds()
    {
        GameModeCatalog catalog = new GameModeCatalog(new[]
        {
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Mode(GameModeId.TotalPoints)
        });

        Assert.That(catalog.Count, Is.EqualTo(1), "the duplicate must not be added");
        Assert.That(catalog.Problems, Is.Not.Empty);
    }

    [Test]
    public void CatalogRejectsDuplicateLevelIds()
    {
        LevelDefinitionCatalog catalog = new LevelDefinitionCatalog(new[]
        {
            TestDefinitions.Level(3),
            TestDefinitions.Level(3)
        });

        Assert.That(catalog.Count, Is.EqualTo(1));
        Assert.That(catalog.Problems, Is.Not.Empty);
    }

    [Test]
    public void LocalInputSlotsAreAssignedInRosterOrderSkippingCpus()
    {
        // Same answer GameOptions.GetHumanPlayerInputSlot computed by counting, but the roster
        // carries it rather than recomputing it at every call site.
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("a")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("b")),
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("c")),
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("d"))
        });

        Assert.That(roster.GetBySlotId(0).LocalInputSlot, Is.EqualTo(0));
        Assert.That(roster.GetBySlotId(1).LocalInputSlot, Is.Null);
        Assert.That(roster.GetBySlotId(2).LocalInputSlot, Is.EqualTo(1));
        Assert.That(roster.GetBySlotId(3).LocalInputSlot, Is.EqualTo(2));
        Assert.That(roster.LocalHumanCount, Is.EqualTo(3));
        Assert.That(roster.CpuCount, Is.EqualTo(1));
    }

    [Test]
    public void ARosterWithHolesInItsSlotIdsIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PlayerRoster(new[]
        {
            new PlayerSlot(0, PlayerControlType.Cpu, CharacterSelection.None),
            new PlayerSlot(2, PlayerControlType.Cpu, CharacterSelection.None)
        }));
    }

    [Test]
    public void ACpuSlotCannotHoldALocalInputSlot()
    {
        Assert.Throws<ArgumentException>(
            () => new PlayerSlot(0, PlayerControlType.Cpu, CharacterSelection.None, 0));
    }

    [Test]
    public void PrimaryLocalHumanSkipsLeadingCpus()
    {
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.Cpu(TestDefinitions.Character("cpu")),
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("me"))
        });

        Assert.That(roster.PrimaryLocalHuman.SlotId, Is.EqualTo(1));
    }

    [Test]
    public void HardcoreIsImpliedByTheHardestDifficulty()
    {
        MatchModifiers modifiers = new MatchModifiers(difficulty: MatchDifficulty.Hardcore);

        Assert.That(modifiers.HardcoreRequested, Is.False);
        Assert.That(modifiers.Hardcore, Is.True);
    }
}
