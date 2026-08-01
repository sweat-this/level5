using System.Collections.Generic;
using UnityEngine;

public static class CharacterProgressParityLogger
{
    private const float FloatTolerance = 0.001f;
    private static bool missingCatalogLogged;

    public static void LogMismatchWarnings(CharacterPresetCatalog catalog, List<CharacterProfileRecord> legacyRecords)
    {
        if (legacyRecords == null || legacyRecords.Count == 0)
        {
            return;
        }

        if (!CharacterProgressStore.TryLoadExisting(CharacterProgressAccountId.GetCurrent(), out CharacterProgressSave save))
        {
            return;
        }

        if (catalog == null && !missingCatalogLogged)
        {
            missingCatalogLogged = true;
            Debug.LogWarning("Character progression parity check could not compare runtime stats because no CharacterPresetCatalog is assigned.");
        }

        foreach (CharacterProfileRecord legacyRecord in legacyRecords)
        {
            PlayerCharacterProgress progress = FindProgress(save, catalog, legacyRecord);
            if (progress == null)
            {
                continue;
            }

            List<string> mismatches = new List<string>();
            CompareCoreProgress(legacyRecord, progress, mismatches);
            CompareRuntimeStats(catalog, legacyRecord, progress, mismatches);

            if (mismatches.Count > 0)
            {
                Debug.LogWarning(
                    "Character progression parity mismatch for legacy player "
                    + legacyRecord.PlayerId
                    + " ("
                    + legacyRecord.PlayerDisplayName
                    + "): "
                    + string.Join(", ", mismatches));
            }
        }
    }

    private static PlayerCharacterProgress FindProgress(
        CharacterProgressSave save,
        CharacterPresetCatalog catalog,
        CharacterProfileRecord legacyRecord)
    {
        if (save?.characters == null || legacyRecord == null)
        {
            return null;
        }

        CharacterPreset preset = catalog == null ? null : catalog.FindByLegacyPlayerId(legacyRecord.PlayerId);
        string characterId = preset == null ? legacyRecord.PlayerObjectName : preset.CharacterId;

        return save.characters.Find(progress =>
            progress != null
            && (progress.legacyPlayerId == legacyRecord.PlayerId
                || (!string.IsNullOrEmpty(characterId) && progress.characterId == characterId)));
    }

    private static void CompareCoreProgress(
        CharacterProfileRecord legacyRecord,
        PlayerCharacterProgress progress,
        List<string> mismatches)
    {
        int legacyLevel = legacyRecord.Level > 0 ? legacyRecord.Level : legacyRecord.Experience / 3000;
        if (legacyRecord.Experience != progress.experience)
        {
            mismatches.Add("experience sqlite=" + legacyRecord.Experience + " json=" + progress.experience);
        }

        if (legacyLevel != progress.level)
        {
            mismatches.Add("level sqlite=" + legacyLevel + " json=" + progress.level);
        }

        if (legacyRecord.IsLocked == progress.unlocked)
        {
            mismatches.Add("unlocked sqlite=" + (!legacyRecord.IsLocked) + " json=" + progress.unlocked);
        }

        int derivedPointsUsed = GetLegacyDerivedPointsUsed(legacyRecord);
        if (derivedPointsUsed != progress.PointsSpent)
        {
            mismatches.Add("pointsUsed legacy=" + derivedPointsUsed + " json=" + progress.PointsSpent);
        }
    }

    private static int GetLegacyDerivedPointsUsed(CharacterProfileRecord legacyRecord)
    {
        int pointsUsed = ((int)legacyRecord.Accuracy3Pt + (int)legacyRecord.Accuracy4Pt + (int)legacyRecord.Accuracy7Pt) - 210;
        int pointsUsedRange = (legacyRecord.Range - 25) / 5;
        return pointsUsed >= 90 ? pointsUsedRange : pointsUsed;
    }

    private static void CompareRuntimeStats(
        CharacterPresetCatalog catalog,
        CharacterProfileRecord legacyRecord,
        PlayerCharacterProgress progress,
        List<string> mismatches)
    {
        CharacterPreset preset = catalog == null ? null : catalog.FindByLegacyPlayerId(legacyRecord.PlayerId);
        if (preset == null)
        {
            return;
        }

        RuntimeCharacterStats runtimeStats = CharacterProgressResolver.BuildRuntimeStats(preset, progress);
        CharacterStats stats = runtimeStats.stats;
        AddMismatch("accuracy2", legacyRecord.Accuracy2Pt, stats.accuracy2Pt, mismatches);
        AddMismatch("accuracy3", legacyRecord.Accuracy3Pt, stats.accuracy3Pt, mismatches);
        AddMismatch("accuracy4", legacyRecord.Accuracy4Pt, stats.accuracy4Pt, mismatches);
        AddMismatch("accuracy7", legacyRecord.Accuracy7Pt, stats.accuracy7Pt, mismatches);
        AddMismatch("jump", legacyRecord.JumpForce, stats.jumpForce, mismatches);
        AddMismatch("speed", legacyRecord.Speed, stats.speed, mismatches);
        AddMismatch("runSpeed", legacyRecord.RunSpeed, stats.runSpeed, mismatches);
        AddMismatch("runSpeedHasBall", legacyRecord.RunSpeedHasBall, stats.runSpeedHasBall, mismatches);
        AddMismatch("range", legacyRecord.Range, stats.range, mismatches);
        AddMismatch("release", legacyRecord.Release, stats.release, mismatches);
        AddMismatch("luck", legacyRecord.Luck, stats.luck, mismatches);
        AddMismatch("shootAngle", legacyRecord.ShootAngle, stats.shootAngle, mismatches);
    }

    private static void AddMismatch(string label, float sqliteValue, float jsonValue, List<string> mismatches)
    {
        if (Mathf.Abs(sqliteValue - jsonValue) > FloatTolerance)
        {
            mismatches.Add(label + " sqlite=" + sqliteValue + " json=" + jsonValue);
        }
    }

    private static void AddMismatch(string label, int sqliteValue, int jsonValue, List<string> mismatches)
    {
        if (sqliteValue != jsonValue)
        {
            mismatches.Add(label + " sqlite=" + sqliteValue + " json=" + jsonValue);
        }
    }
}
