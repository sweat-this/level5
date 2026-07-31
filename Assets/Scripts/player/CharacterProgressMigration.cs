using System;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterProgressMigration
{
    public static CharacterProgressSave FromLegacyRecords(
        string userId,
        CharacterPresetCatalog catalog,
        List<CharacterProfileRecord> legacyRecords)
    {
        CharacterProgressSave save = catalog != null
            ? catalog.CreateDefaultProgress(userId)
            : new CharacterProgressSave { userId = userId };

        if (legacyRecords == null)
        {
            return save;
        }

        foreach (CharacterProfileRecord legacyRecord in legacyRecords)
        {
            CharacterPreset preset = catalog == null ? null : catalog.FindByLegacyPlayerId(legacyRecord.PlayerId);
            string characterId = preset == null ? legacyRecord.PlayerObjectName : preset.CharacterId;
            if (string.IsNullOrEmpty(characterId))
            {
                characterId = legacyRecord.PlayerId.ToString();
            }

            PlayerCharacterProgress progress = save.characters.Find(item => item.characterId == characterId);
            if (progress == null)
            {
                progress = new PlayerCharacterProgress
                {
                    characterId = characterId,
                    legacyPlayerId = legacyRecord.PlayerId
                };
                save.characters.Add(progress);
            }

            progress.unlocked = !legacyRecord.IsLocked;
            progress.experience = legacyRecord.Experience;
            progress.level = legacyRecord.Level > 0 ? legacyRecord.Level : legacyRecord.Experience / 3000;
            progress.lastModifiedUtc = DateTime.UtcNow.ToString("o");

            if (preset != null)
            {
                progress.upgrades = EstimateUpgradeLevels(preset, legacyRecord);
            }
        }

        save.migratedFrom = "sqlite-character-profile";
        return save;
    }

    private static CharacterUpgradeLevels EstimateUpgradeLevels(CharacterPreset preset, CharacterProfileRecord legacyRecord)
    {
        CharacterStats baseStats = preset.BaseStats;
        CharacterStats step = preset.UpgradeStep;
        return new CharacterUpgradeLevels
        {
            accuracy2Pt = EstimateLevel(legacyRecord.Accuracy2Pt, baseStats?.accuracy2Pt ?? 0, step?.accuracy2Pt ?? 0),
            accuracy3Pt = EstimateLevel(legacyRecord.Accuracy3Pt, baseStats?.accuracy3Pt ?? 0, step?.accuracy3Pt ?? 0),
            accuracy4Pt = EstimateLevel(legacyRecord.Accuracy4Pt, baseStats?.accuracy4Pt ?? 0, step?.accuracy4Pt ?? 0),
            accuracy7Pt = EstimateLevel(legacyRecord.Accuracy7Pt, baseStats?.accuracy7Pt ?? 0, step?.accuracy7Pt ?? 0),
            jumpForce = EstimateLevel(legacyRecord.JumpForce, baseStats?.jumpForce ?? 0, step?.jumpForce ?? 0),
            speed = EstimateLevel(legacyRecord.Speed, baseStats?.speed ?? 0, step?.speed ?? 0),
            runSpeed = EstimateLevel(legacyRecord.RunSpeed, baseStats?.runSpeed ?? 0, step?.runSpeed ?? 0),
            runSpeedHasBall = EstimateLevel(legacyRecord.RunSpeedHasBall, baseStats?.runSpeedHasBall ?? 0, step?.runSpeedHasBall ?? 0),
            range = EstimateLevel(legacyRecord.Range, baseStats?.range ?? 0, step?.range ?? 0),
            release = EstimateLevel(legacyRecord.Release, baseStats?.release ?? 0, step?.release ?? 0),
            luck = EstimateLevel(legacyRecord.Luck, baseStats?.luck ?? 0, step?.luck ?? 0),
            shootAngle = EstimateLevel(legacyRecord.ShootAngle, baseStats?.shootAngle ?? 0, step?.shootAngle ?? 0)
        };
    }

    private static int EstimateLevel(float savedValue, float baseValue, float stepValue)
    {
        if (stepValue <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.RoundToInt((savedValue - baseValue) / stepValue));
    }
}
