using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ProgressionResultStore
{
    private const string AccountsFolderName = "accounts";

    public static bool HasApplied(string userId, string resultId)
    {
        if (string.IsNullOrEmpty(resultId))
        {
            return false;
        }

        ProgressionResultLedger ledger = Load(userId);
        return ledger.appliedResultIds.Contains(resultId);
    }

    public static bool TryMarkApplied(string userId, string resultId)
    {
        if (string.IsNullOrEmpty(resultId))
        {
            return false;
        }

        try
        {
            ProgressionResultLedger ledger = Load(userId);
            if (!ledger.appliedResultIds.Contains(resultId))
            {
                ledger.appliedResultIds.Add(resultId);
            }

            Save(userId, ledger);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to mark progression result as applied: " + e);
            return false;
        }
    }

    private static ProgressionResultLedger Load(string userId)
    {
        string path = GetAccountResultPath(userId);
        if (!AtomicFile.TryReadAllText(path, IsValidLedgerJson, out string json))
        {
            return new ProgressionResultLedger();
        }

        try
        {
            ProgressionResultLedger ledger = JsonUtility.FromJson<ProgressionResultLedger>(json);
            if (ledger == null)
            {
                return new ProgressionResultLedger();
            }

            ledger.Normalize();
            return ledger;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load progression result ledger from " + path + ": " + e);
            return new ProgressionResultLedger();
        }
    }

    private static bool IsValidLedgerJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<ProgressionResultLedger>(json) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Save(string userId, ProgressionResultLedger ledger)
    {
        string path = GetAccountResultPath(userId);
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        ledger.Normalize();
        AtomicFile.WriteAllText(path, JsonUtility.ToJson(ledger, true));
    }

    private static string GetAccountResultPath(string userId)
    {
        string safeUserId = SanitizeFileName(string.IsNullOrWhiteSpace(userId) ? "guest" : userId);
        return Path.Combine(Application.persistentDataPath, AccountsFolderName, safeUserId + "-progression-results.json");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}

[Serializable]
public class ProgressionResultLedger
{
    public List<string> appliedResultIds = new List<string>();

    public void Normalize()
    {
        if (appliedResultIds == null)
        {
            appliedResultIds = new List<string>();
        }
    }
}
