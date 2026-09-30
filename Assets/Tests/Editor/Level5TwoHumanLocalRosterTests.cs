using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// The two-human roster shape the local-multiplayer input foundation relies on, and the Core arena
/// rule that gates it. This certifies the existing <see cref="PlayerRoster"/>/<see cref="PlayerSlot"/>
/// contract rather than adding a multiplayer roster type: a roster still only says which participant
/// uses local input slot N, never which physical device that is.
/// </summary>
public class Level5TwoHumanLocalRosterTests
{
    private static PlayerRoster TwoHumanRoster()
    {
        return PlayerRoster.Build(new[]
        {
            new PlayerRosterEntry(PlayerControlType.LocalHuman, TestDefinitions.Character("p1", characterId: 11), "participant-one"),
            new PlayerRosterEntry(PlayerControlType.LocalHuman, TestDefinitions.Character("p2", characterId: 22), "participant-two")
        });
    }

    private static GameModeCompatibility CompatibilityFor(LevelDefinition level)
    {
        return new GameModeCompatibility(
            new GameModeCatalog(new[] { TestDefinitions.Mode(GameModeId.TotalPoints) }),
            new LevelDefinitionCatalog(new[] { level }));
    }

    private static ValidationResult Validate(PlayerRoster roster, LevelDefinition level)
    {
        return CompatibilityFor(level).Validate(new MatchRequest(GameModeId.TotalPoints, level.LevelId, roster));
    }

    // ==================== roster shape ====================

    [Test]
    public void ATwoHumanRosterBuildsTwoIndependentLocalInputSlots()
    {
        PlayerRoster roster = TwoHumanRoster();

        Assert.That(roster.Count, Is.EqualTo(2));
        Assert.That(roster.LocalHumanCount, Is.EqualTo(2));
        Assert.That(roster.CpuCount, Is.EqualTo(0));

        PlayerSlot first = roster.GetBySlotId(0);
        PlayerSlot second = roster.GetBySlotId(1);

        Assert.That(first.ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(second.ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(first.LocalInputSlot, Is.EqualTo(0));
        Assert.That(second.LocalInputSlot, Is.EqualTo(1));
    }

    [Test]
    public void ATwoHumanRosterKeepsEachParticipantsCharacterAndIdentity()
    {
        PlayerRoster roster = TwoHumanRoster();

        PlayerSlot first = roster.GetBySlotId(0);
        PlayerSlot second = roster.GetBySlotId(1);

        Assert.That(first.Character.ObjectName, Is.EqualTo("p1"));
        Assert.That(second.Character.ObjectName, Is.EqualTo("p2"));
        Assert.That(first.Character.CharacterId, Is.EqualTo(11));
        Assert.That(second.Character.CharacterId, Is.EqualTo(22));
        Assert.That(first.ParticipantId, Is.EqualTo("participant-one"));
        Assert.That(second.ParticipantId, Is.EqualTo("participant-two"));
        Assert.That(first.ParticipantId, Is.Not.EqualTo(second.ParticipantId));
    }

    [Test]
    public void ARosterCarriesNoDeviceInformation()
    {
        // The Core roster stays framework-independent: a local input slot is an index, not a device.
        foreach (System.Reflection.PropertyInfo property in typeof(PlayerSlot).GetProperties())
        {
            Assert.That(
                property.PropertyType.Namespace,
                Does.Not.StartWith("UnityEngine.InputSystem"),
                $"PlayerSlot.{property.Name} must not expose an Input System type");
        }
    }

    // ==================== arena rule ====================

    [Test]
    public void TwoLocalHumansAreRejectedOnAnArenaWithoutTheMultiplayerCapability()
    {
        LevelDefinition level = TestDefinitions.Level(1, ArenaCapability.Basketball);

        ValidationResult result = Validate(TwoHumanRoster(), level);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.True, result.ToString());
    }

    [Test]
    public void TwoLocalHumansAreAcceptedOnAnArenaWithTheMultiplayerCapability()
    {
        LevelDefinition level = TestDefinitions.Level(1, ArenaCapability.Basketball | ArenaCapability.Multiplayer);

        ValidationResult result = Validate(TwoHumanRoster(), level);

        Assert.That(result.IsValid, Is.True, result.ToString());
    }

    [Test]
    public void TwoLocalHumansAreRejectedByAModeWhoseCapacityIsOne()
    {
        // The arena is fine; it is the mode's own MaxPlayers that refuses the second participant.
        LevelDefinition level = TestDefinitions.Level(1, ArenaCapability.Basketball | ArenaCapability.Multiplayer);
        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { TestDefinitions.Mode(GameModeId.TotalPoints, maxPlayers: 1) }),
            new LevelDefinitionCatalog(new[] { level }));

        ValidationResult result = compatibility.Validate(new MatchRequest(GameModeId.TotalPoints, level.LevelId, TwoHumanRoster()));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasError(MatchValidationCode.RosterTooLarge), Is.True, result.ToString());
        Assert.That(result.HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.False);
    }

    [Test]
    public void OneLocalHumanNeverNeedsTheMultiplayerCapability()
    {
        LevelDefinition level = TestDefinitions.Level(1, ArenaCapability.Basketball);

        ValidationResult result = Validate(TestDefinitions.SoloRoster("solo"), level);

        Assert.That(result.IsValid, Is.True, result.ToString());
    }

    [Test]
    public void OneHumanAndACpuDoesNotCountAsLocalMultiplayer()
    {
        LevelDefinition level = TestDefinitions.Level(1, ArenaCapability.Basketball);
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("human")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("cpu"))
        });

        Assert.That(Validate(roster, level).HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.False);
    }
}
