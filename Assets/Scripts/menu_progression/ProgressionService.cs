using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressionService
{
    private static readonly HashSet<string> AppliedResultIds = new HashSet<string>();

    public static string CreateResultId(string prefix)
    {
        string safePrefix = string.IsNullOrEmpty(prefix) ? "match" : prefix;
        return safePrefix + "-" + Guid.NewGuid().ToString("N");
    }

    public MatchProgressionResult ApplyMatchResult(MatchProgressionResult result)
    {
        if (result == null)
        {
            result = new MatchProgressionResult(CreateResultId("missing"), GameOptions.characterId, 0);
            result.Message = "ProgressionService received a null match result.";
            Debug.LogWarning(result.Message);
            return result;
        }

        string accountId = CharacterProgressAccountId.GetCurrent();
        if (AppliedResultIds.Contains(result.ResultId) || ProgressionResultStore.HasApplied(accountId, result.ResultId))
        {
            result.Duplicate = true;
            result.Message = "Progression result already applied: " + result.ResultId;
            Debug.LogWarning(result.Message);
            return result;
        }

        if (DBConnector.instance == null)
        {
            result.Message = "Progression result could not be applied because DBConnector is unavailable: " + result.ResultId;
            Debug.LogWarning(result.Message);
            return result;
        }

        if (!DBConnector.instance.savePlayerProfileProgression(result.ExperienceGained, result.CharacterId))
        {
            result.Message = "Progression result could not be applied because the database write failed: " + result.ResultId;
            Debug.LogWarning(result.Message);
            return result;
        }

        ApplyJsonProgression(accountId, result);
        AppliedResultIds.Add(result.ResultId);
        if (!ProgressionResultStore.TryMarkApplied(accountId, result.ResultId))
        {
            result.Message = "Progression result applied, but the duplicate ledger could not be updated: " + result.ResultId;
            Debug.LogWarning(result.Message);
            result.Applied = true;
            return result;
        }

        result.Applied = true;
        result.Message = "Progression result applied: " + result.ResultId;
        return result;
    }

    private static void ApplyJsonProgression(string accountId, MatchProgressionResult result)
    {
        try
        {
            if (!CharacterProgressStore.TryLoadExisting(accountId, out CharacterProgressSave save)
                || save.characters == null)
            {
                return;
            }

            PlayerCharacterProgress progress = save.characters.Find(character =>
                character != null && character.legacyPlayerId == result.CharacterId);
            if (progress == null)
            {
                return;
            }

            progress.experience += (int)result.ExperienceGained;
            progress.level = progress.experience / 3000;
            progress.lastModifiedUtc = DateTime.UtcNow.ToString("o");
            CharacterProgressStore.Save(save);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Progression JSON save could not be updated for result " + result.ResultId + ": " + e);
        }
    }
}
