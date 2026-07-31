using System;

public static class CharacterProgressResolver
{
    public static RuntimeCharacterStats BuildRuntimeStats(CharacterPreset preset, PlayerCharacterProgress progress)
    {
        if (preset == null)
        {
            throw new ArgumentNullException(nameof(preset));
        }

        CharacterUpgradeLevels upgrades = preset.UpgradesEnabled
            ? CharacterUpgradeLevels.Sanitize(progress?.upgrades)
            : new CharacterUpgradeLevels();
        CharacterStats bonusStats = upgrades.ToBonusStats(preset.UpgradeStep);
        CharacterStats combinedStats = CharacterStats.Add(preset.BaseStats, bonusStats);
        CharacterStats clampedStats = CharacterStats.Clamp(combinedStats, preset.MinStats, preset.MaxStats);

        return new RuntimeCharacterStats
        {
            characterId = preset.CharacterId,
            legacyPlayerId = preset.LegacyPlayerId,
            displayName = preset.DisplayName,
            experience = progress?.experience ?? 0,
            level = progress?.level ?? 0,
            pointsSpent = upgrades.TotalSpent,
            unlocked = progress?.unlocked ?? preset.UnlockedByDefault,
            stats = clampedStats
        };
    }
}
