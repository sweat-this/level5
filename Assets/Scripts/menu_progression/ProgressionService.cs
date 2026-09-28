using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressionService
{
    /// <summary>Compatibility delegate: the algorithm itself lives on <see cref="MatchSession"/> now.</summary>
    public static string CreateResultId(string prefix)
    {
        return MatchSession.CreateResultId(prefix);
    }

    /// <summary>
    /// AUD-011: convenience overload so callers don't each build a <see cref="MatchProgressionResult"/>
    /// by hand - <see cref="GameRules"/> and <see cref="Pause"/> both used to. Safe regardless of how
    /// many callers reach the same session's result: <paramref name="resultId"/> normally comes from
    /// the shared, per-session <c>MatchSession.EnsureCurrentMatch()</c>, and <see cref="ApplyMatchResult(MatchProgressionResult)"/>
    /// is already idempotent per result id at the database layer, so a second application for the
    /// same id is reported as <see cref="MatchProgressionResult.Duplicate"/> rather than double-granted.
    /// </summary>
    public MatchProgressionResult ApplyMatchResult(string resultId, int characterId, float experienceGained)
    {
        return ApplyMatchResult(new MatchProgressionResult(resultId, characterId, experienceGained));
    }

    public MatchProgressionResult ApplyMatchResult(MatchProgressionResult result)
    {
        if (result == null)
        {
            result = new MatchProgressionResult(CreateResultId("missing"), GameOptions.characterId, 0);
            result.Message = "ProgressionService received a null match result.";
            return result;
        }

        string accountId = CharacterProgressAccountId.GetCurrent();
        if (DBConnector.instance == null)
        {
            result.Applied = PendingProgressionStore.Queue(
                accountId,
                result.ResultId,
                result.CharacterId,
                result.ExperienceGained);
            result.Message = result.Applied
                ? "Progression is queued until the local database is available."
                : "Progression could not be saved or queued.";
            Debug.LogWarning(result.Message);
            return result;
        }

        RepairPendingResults(accountId);
        ProgressionApplyStatus status = DBConnector.instance.ApplyProgressionResult(
            result.ResultId,
            accountId,
            result.ExperienceGained,
            result.CharacterId,
            out _);

        if (status == ProgressionApplyStatus.Failed)
        {
            result.Applied = PendingProgressionStore.Queue(
                accountId,
                result.ResultId,
                result.CharacterId,
                result.ExperienceGained);
            result.Message = result.Applied
                ? "Progression is queued for local repair."
                : "Progression could not be saved or queued.";
            Debug.LogWarning(result.Message);
            return result;
        }

        PendingProgressionStore.Remove(accountId, result.ResultId);
        result.Applied = true;
        result.Duplicate = status == ProgressionApplyStatus.Duplicate;
        result.Message = result.Duplicate
            ? "Progression result was already applied."
            : "Progression result applied.";
        return result;
    }

    public bool RepairPendingProgression()
    {
        string accountId = CharacterProgressAccountId.GetCurrent();
        return RepairPendingResults(accountId);
    }

    private static bool RepairPendingResults(string accountId)
    {
        if (DBConnector.instance == null)
        {
            return false;
        }

        bool allApplied = true;
        List<PendingProgressionResult> pendingResults;
        try
        {
            pendingResults = PendingProgressionStore.GetPending(accountId);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not load pending progression results: " + exception);
            return false;
        }

        foreach (PendingProgressionResult pending in pendingResults)
        {
            ProgressionApplyStatus status = DBConnector.instance.ApplyProgressionResult(
                pending.resultId,
                accountId,
                pending.experienceGained,
                pending.characterId,
                out _);
            if (status == ProgressionApplyStatus.Failed
                || !PendingProgressionStore.Remove(accountId, pending.resultId))
            {
                allApplied = false;
            }
        }

        return allApplied;
    }
}
