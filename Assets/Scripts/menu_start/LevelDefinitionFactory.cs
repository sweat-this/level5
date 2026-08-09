using System.Collections.Generic;
using Level5.Core.Match;

/// <summary>
/// Builds a <see cref="LevelDefinition"/> from the legacy <see cref="LevelSelected"/> component (or
/// the <see cref="LevelPreset"/> already extracted from it).
///
/// The per-level booleans become capability flags. That is not just tidier: it is what lets one
/// compatibility service answer "can this mode be played here?" without knowing the name of every
/// individual flag, and what stops the next arena feature from adding another global bool.
/// </summary>
public static class LevelDefinitionFactory
{
    public static LevelDefinition Create(LevelSelected source)
    {
        return source == null ? null : Create(LevelPreset.FromLevelSelected(source));
    }

    public static LevelDefinition Create(LevelPreset source)
    {
        if (source == null)
        {
            return null;
        }

        LevelDefinitionData data = LevelDefinitionData.Default(source.LevelId);
        data.DisplayName = source.LevelDisplayName;
        data.Info = source.LevelInfo;
        data.ObjectName = source.LevelObjectName;
        data.SceneDescriptor = source.LevelDescription;
        data.Capabilities = ResolveCapabilities(source);
        data.CustomCamera = source.CustomCamera;
        data.Selectable = source.IsSelectable;
        data.Locked = source.IsLocked;

        return LevelDefinition.Create(data);
    }

    public static List<LevelDefinition> CreateAll(IEnumerable<LevelSelected> sources)
    {
        List<LevelDefinition> definitions = new List<LevelDefinition>();
        if (sources == null)
        {
            return definitions;
        }

        foreach (LevelSelected source in sources)
        {
            LevelDefinition definition = Create(source);
            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    public static List<LevelDefinition> CreateAll(IEnumerable<LevelPreset> sources)
    {
        List<LevelDefinition> definitions = new List<LevelDefinition>();
        if (sources == null)
        {
            return definitions;
        }

        foreach (LevelPreset source in sources)
        {
            LevelDefinition definition = Create(source);
            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    public static ArenaCapability ResolveCapabilities(LevelPreset source)
    {
        ArenaCapability capabilities = ArenaCapability.None;
        if (source == null)
        {
            return capabilities;
        }

        if (source.IsShootingLevel)
        {
            capabilities |= ArenaCapability.Basketball;
        }

        if (source.IsFightingLevel)
        {
            capabilities |= ArenaCapability.Combat;
        }

        if (source.IsCageMatchLevel)
        {
            capabilities |= ArenaCapability.Cage;
        }

        if (source.IsBattleRoyalLevel)
        {
            capabilities |= ArenaCapability.BattleRoyal;
        }

        if (source.LevelHasSevenPointers)
        {
            capabilities |= ArenaCapability.SevenPointLine;
        }

        if (source.LevelHasTraffic)
        {
            capabilities |= ArenaCapability.Traffic;
        }

        if (source.LevelRequiresTimeOfDay)
        {
            capabilities |= ArenaCapability.TimeOfDay;
        }

        if (source.LevelHasWeather)
        {
            capabilities |= ArenaCapability.Weather;
        }

        // No level authors a multiplayer flag today - whether the extra player spawn points exist
        // is discovered by GameLevelManager when the scene loads, and a scene without them fails
        // there with a named error. Granting the capability to every arena keeps that behaviour
        // exactly as it is; when spawn counts become authored data this is where they land.
        capabilities |= ArenaCapability.Multiplayer;

        return capabilities;
    }
}
