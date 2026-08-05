using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressionService
{
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
            return result;
        }

        if (DBConnector.instance == null)
        {
            result.Message = "Progression could not be saved because the local database is unavailable.";
            Debug.LogWarning(result.Message);
            return result;
        }

        string accountId = CharacterProgressAccountId.GetCurrent();
        ProgressionApplyStatus status = DBConnector.instance.ApplyProgressionResult(
            result.ResultId,
            accountId,
            result.ExperienceGained,
            result.CharacterId,
            out ProgressionSnapshot snapshot);

        if (status == ProgressionApplyStatus.Failed)
        {
            result.Message = "Progression could not be saved. It can be retried with the same result ID.";
            Debug.LogWarning(result.Message);
            return result;
        }

        result.Applied = true;
        result.Duplicate = status == ProgressionApplyStatus.Duplicate;
        bool projectionComplete = RepairPendingJsonProjections(accountId);
        result.Message = result.Duplicate
            ? "Progression result was already applied."
            : projectionComplete
                ? "Progression result applied."
                : "Progression result applied; its JSON projection is queued for repair.";
        return result;
    }

    public bool RepairPendingJsonProjections()
    {
        return RepairPendingJsonProjections(CharacterProgressAccountId.GetCurrent());
    }

    private static bool RepairPendingJsonProjections(string accountId)
    {
        if (DBConnector.instance == null)
        {
            return false;
        }

        List<ProgressionSnapshot> pending = DBConnector.instance.GetPendingProgressionProjections(accountId);
        bool allApplied = true;
        foreach (ProgressionSnapshot snapshot in pending)
        {
            if (!CharacterProgressStore.TryApplyProgressionSnapshot(
                accountId,
                snapshot.CharacterId,
                snapshot.Experience,
                snapshot.Level,
                out string error))
            {
                allApplied = false;
                Debug.LogWarning("Progression projection remains pending: " + error);
                continue;
            }

            if (!DBConnector.instance.MarkProgressionProjectionApplied(snapshot.ResultId))
            {
                allApplied = false;
            }
        }

        return allApplied;
    }
}
