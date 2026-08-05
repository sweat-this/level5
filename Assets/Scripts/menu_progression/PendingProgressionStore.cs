using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PendingProgressionStore
{
    private const string AccountsFolderName = "accounts";

    public static bool Queue(string accountId, string resultId, int characterId, float experienceGained)
    {
        if (string.IsNullOrEmpty(resultId))
        {
            return false;
        }

        try
        {
            PendingProgressionData data = Load(accountId);
            if (!data.results.Exists(value => value != null && value.resultId == resultId))
            {
                data.results.Add(new PendingProgressionResult
                {
                    resultId = resultId,
                    characterId = characterId,
                    experienceGained = experienceGained
                });
            }

            Save(accountId, data);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not queue progression result: " + exception);
            return false;
        }
    }

    public static List<PendingProgressionResult> GetPending(string accountId)
    {
        return new List<PendingProgressionResult>(Load(accountId).results);
    }

    public static bool Remove(string accountId, string resultId)
    {
        try
        {
            PendingProgressionData data = Load(accountId);
            data.results.RemoveAll(value => value == null || value.resultId == resultId);
            Save(accountId, data);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not remove queued progression result: " + exception);
            return false;
        }
    }

    private static PendingProgressionData Load(string accountId)
    {
        string path = GetPath(accountId);
        if (!AtomicFile.TryReadAllText(path, IsValid, out string json))
        {
            return new PendingProgressionData();
        }

        PendingProgressionData data = JsonUtility.FromJson<PendingProgressionData>(json)
            ?? new PendingProgressionData();
        data.results ??= new List<PendingProgressionResult>();
        return data;
    }

    private static void Save(string accountId, PendingProgressionData data)
    {
        string path = GetPath(accountId);
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    private static bool IsValid(string json)
    {
        try
        {
            return JsonUtility.FromJson<PendingProgressionData>(json) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetPath(string accountId)
    {
        string safeAccountId = string.IsNullOrWhiteSpace(accountId) ? "guest" : accountId;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safeAccountId = safeAccountId.Replace(invalid, '_');
        }

        return Path.Combine(
            Application.persistentDataPath,
            AccountsFolderName,
            safeAccountId + "-pending-progression.json");
    }
}

[Serializable]
public class PendingProgressionData
{
    public List<PendingProgressionResult> results = new List<PendingProgressionResult>();
}

[Serializable]
public class PendingProgressionResult
{
    public string resultId;
    public int characterId;
    public float experienceGained;
}
