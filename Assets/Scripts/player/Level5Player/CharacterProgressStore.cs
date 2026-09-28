using System;
using System.IO;
using UnityEngine;

public static class CharacterProgressStore
{
    private const string AccountsFolderName = "accounts";

    public static bool TryLoadExisting(string userId, out CharacterProgressSave save)
    {
        save = null;
        string path = GetAccountProgressPath(userId);
        if (!AtomicFile.TryReadAllText(path, IsValidSaveJson, out string json))
        {
            return false;
        }

        try
        {
            save = JsonUtility.FromJson<CharacterProgressSave>(json);
            if (save == null)
            {
                return false;
            }

            Normalize(NormalizeUserId(userId), save);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load character progress from " + path + ": " + e);
            save = null;
            return false;
        }
    }

    private static bool IsValidSaveJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<CharacterProgressSave>(json) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Normalize(string userId, CharacterProgressSave save)
    {
        if (string.IsNullOrEmpty(save.userId))
        {
            save.userId = userId;
        }

        if (save.characters == null)
        {
            save.characters = new System.Collections.Generic.List<PlayerCharacterProgress>();
        }
    }

    public static void Save(CharacterProgressSave save)
    {
        if (save == null)
        {
            throw new ArgumentNullException(nameof(save));
        }

        Normalize(NormalizeUserId(save.userId), save);
        string path = GetAccountProgressPath(save.userId);
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(save, true);
        AtomicFile.WriteAllText(path, json);
    }

    public static string GetAccountProgressPath(string userId)
    {
        string safeUserId = SanitizeFileName(NormalizeUserId(userId));
        return Path.Combine(Application.persistentDataPath, AccountsFolderName, safeUserId + "-characters.json");
    }

    /// <summary>
    /// Removes this account's projection file family (primary/.bak/.tmp) after a local profile has
    /// been deleted from SQLite. Narrowly owned here so a caller (e.g. account deletion) never has to
    /// reconstruct this store's filename convention itself. Returns false if any file that existed
    /// could not be removed - SQLite deletion has already committed by the time this runs, so the
    /// caller logs a warning rather than treating this as a reason to undo the deletion.
    /// </summary>
    public static bool DeleteAccountFiles(string userId)
    {
        return AtomicFile.TryDeleteFamily(GetAccountProgressPath(userId));
    }

    private static string NormalizeUserId(string userId)
    {
        return string.IsNullOrWhiteSpace(userId) ? "guest" : userId;
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
