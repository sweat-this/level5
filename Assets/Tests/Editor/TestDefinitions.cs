using Level5.Core.Match;

/// <summary>
/// Builders for match domain objects in tests.
///
/// Definitions are ScriptableObjects, but nothing about them needs the asset database - the
/// authored fields are set through the same <c>Create</c> the migration uses, so a test builds one
/// the same way the editor does.
/// </summary>
public static class TestDefinitions
{
    public static GameModeDefinition Mode(
        GameModeId id,
        MatchClockMode clockMode = MatchClockMode.Countdown,
        CombatMode combatMode = CombatMode.None,
        ShotRule shotRule = ShotRule.Any,
        ShotMarkerRequirement markers = ShotMarkerRequirement.None,
        bool requiresBasketball = true,
        bool enemiesOnly = false,
        bool allowsCpuShooters = true,
        int minPlayers = 1,
        int maxPlayers = PlayerRoster.MaxSlots,
        float customTimerSeconds = 0f,
        ArenaCapability required = ArenaCapability.None,
        ArenaCapability forbidden = ArenaCapability.None,
        bool addsImplicitDefender = false,
        bool requiresCpuOpponent = false)
    {
        GameModeDefinitionData data = GameModeDefinitionData.Default((int)id);
        data.DisplayName = id.ToString();
        data.ObjectName = id.ToString().ToLowerInvariant();
        data.ClockMode = clockMode;
        data.CombatMode = combatMode;
        data.ShotRule = shotRule;
        data.ShotMarkers = markers;
        data.RequiresBasketball = requiresBasketball;
        data.EnemiesOnly = enemiesOnly;
        data.AllowsCpuShooters = allowsCpuShooters;
        data.MinPlayers = minPlayers;
        data.MaxPlayers = maxPlayers;
        data.CustomTimerSeconds = customTimerSeconds;
        data.RequiredArenaCapabilities = required;
        data.ForbiddenArenaCapabilities = forbidden;
        data.AddsImplicitDefender = addsImplicitDefender;
        data.RequiresCpuOpponent = requiresCpuOpponent;
        return GameModeDefinition.Create(data);
    }

    public static LevelDefinition Level(
        int levelId,
        ArenaCapability capabilities = ArenaCapability.Basketball | ArenaCapability.Multiplayer,
        string objectName = null,
        string sceneDescriptor = null)
    {
        LevelDefinitionData data = LevelDefinitionData.Default(levelId);
        data.DisplayName = "level " + levelId;
        data.ObjectName = objectName ?? ("level_" + levelId);
        data.SceneDescriptor = sceneDescriptor ?? string.Empty;
        data.Capabilities = capabilities;
        return LevelDefinition.Create(data);
    }

    public static CharacterSelection Character(
        string objectName,
        bool isShooter = true,
        bool isFighter = true,
        int characterId = 1)
    {
        return new CharacterSelection(characterId, objectName, objectName, isShooter, isFighter);
    }

    public static PlayerRoster SoloRoster(string objectName = "player", bool isShooter = true, bool isFighter = true)
    {
        return PlayerRoster.SingleLocalHuman(Character(objectName, isShooter, isFighter));
    }
}
