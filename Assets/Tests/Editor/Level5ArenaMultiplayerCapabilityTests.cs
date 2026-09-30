using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <c>ArenaCapability.Multiplayer</c> is authored per arena on its <c>LevelSelected</c> prefab and
/// carried through <c>LevelPreset</c> and <c>LevelDefinitionFactory</c>. These pin that no arena gets
/// the capability by default, and that <c>GameModeCompatibility</c> - the only launch authority -
/// then refuses a two-human roster on an arena that has not been marked.
/// </summary>
public class Level5ArenaMultiplayerCapabilityTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        created.Clear();
    }

    private LevelSelected Authored(int levelId, bool multiplayer)
    {
        GameObject go = new GameObject("level_selected_" + levelId);
        created.Add(go);
        LevelSelected level = go.AddComponent<LevelSelected>();
        Set(level, "levelId", levelId);
        Set(level, "levelDisplayName", "Arena " + levelId);
        Set(level, "levelObjectName", "level_" + levelId);
        Set(level, "isShootingLevel", true);
        Set(level, "isSelectable", true);
        Set(level, "levelSupportsMultiplayer", multiplayer);
        return level;
    }

    private static void Set(LevelSelected level, string field, object value)
    {
        FieldInfo info = typeof(LevelSelected).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(info, Is.Not.Null, $"LevelSelected has no serialized field '{field}'");
        info.SetValue(level, value);
    }

    private static PlayerRoster TwoHumans()
    {
        return PlayerRoster.Build(new[]
        {
            new PlayerRosterEntry(PlayerControlType.LocalHuman, TestDefinitions.Character("p1", characterId: 11), "participant-one"),
            new PlayerRosterEntry(PlayerControlType.LocalHuman, TestDefinitions.Character("p2", characterId: 22), "participant-two")
        });
    }

    private static ValidationResult Validate(LevelDefinition level, PlayerRoster roster)
    {
        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { TestDefinitions.Mode(GameModeId.TotalPoints) }),
            new LevelDefinitionCatalog(new[] { level }));
        return compatibility.Validate(new MatchRequest(GameModeId.TotalPoints, level.LevelId, roster));
    }

    // ==================== authoring / conversion ====================

    [Test]
    public void AnArenaThatDoesNotAuthorMultiplayerDoesNotGetIt()
    {
        LevelDefinition level = LevelDefinitionFactory.Create(Authored(7, multiplayer: false));

        Assert.That(level.Supports(ArenaCapability.Multiplayer), Is.False);
        Assert.That(level.Supports(ArenaCapability.Basketball), Is.True, "the other authored capabilities still convert");
    }

    [Test]
    public void AnArenaThatAuthorsMultiplayerCarriesItThroughConversion()
    {
        LevelDefinition level = LevelDefinitionFactory.Create(Authored(8, multiplayer: true));

        Assert.That(level.Supports(ArenaCapability.Multiplayer), Is.True);
    }

    [Test]
    public void TheCapabilityIsResolvedFromTheAuthoredFlagAloneForEveryFlagCombination()
    {
        // Whatever else an arena authors, only its own multiplayer flag decides the capability.
        foreach (bool multiplayer in new[] { false, true })
        {
            LevelPreset preset = LevelPreset.FromLevelSelected(Authored(9, multiplayer));

            Assert.That(
                (LevelDefinitionFactory.ResolveCapabilities(preset) & ArenaCapability.Multiplayer) != 0,
                Is.EqualTo(multiplayer));
        }

        Assert.That(LevelDefinitionFactory.ResolveCapabilities(null), Is.EqualTo(ArenaCapability.None));
    }

    [Test]
    public void ABatchOfArenasIsNotBlanketEnabled()
    {
        List<LevelDefinition> levels = LevelDefinitionFactory.CreateAll(new[]
        {
            Authored(1, multiplayer: true),
            Authored(2, multiplayer: false),
            Authored(3, multiplayer: false)
        });

        Assert.That(levels.FindAll(l => l.Supports(ArenaCapability.Multiplayer)).Count, Is.EqualTo(1));
        Assert.That(levels.Find(l => l.LevelId == 1).Supports(ArenaCapability.Multiplayer), Is.True);
    }

    [Test]
    public void ADefaultLevelDefinitionDoesNotClaimMultiplayer()
    {
        LevelDefinitionData data = LevelDefinitionData.Default(42);

        Assert.That((data.Capabilities & ArenaCapability.Multiplayer) != 0, Is.False);
    }

    // ==================== launch authority ====================

    [Test]
    public void TwoHumansAreRefusedOnAnAuthoredArenaThatIsNotMarkedMultiplayer()
    {
        LevelDefinition level = LevelDefinitionFactory.Create(Authored(4, multiplayer: false));

        ValidationResult result = Validate(level, TwoHumans());

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.True, result.ToString());
    }

    [Test]
    public void TwoHumansAreAcceptedOnAnAuthoredArenaMarkedMultiplayer()
    {
        LevelDefinition level = LevelDefinitionFactory.Create(Authored(5, multiplayer: true));

        Assert.That(Validate(level, TwoHumans()).IsValid, Is.True);
    }

    [Test]
    public void OneHumanIsAcceptedOnAnArenaThatIsNotMarkedMultiplayer()
    {
        LevelDefinition level = LevelDefinitionFactory.Create(Authored(6, multiplayer: false));

        Assert.That(Validate(level, TestDefinitions.SoloRoster("solo")).IsValid, Is.True);
    }
}
