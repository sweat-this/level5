using System;
using System.IO;
using UnityEngine;

public static class CharacterProgressStore
{
    private const string AccountsFolderName = "accounts";

    public static CharacterProgressSave Load(string userId, CharacterPresetCatalog catalog)
    {
        if (TryLoadExisting(userId, out CharacterProgressSave save))
        {
            return save;
        }

        string normalizedUserId = NormalizeUserId(userId);
        return catalog != null ? catalog.CreateDefaultProgress(normalizedUserId) : CreateEmptySave(normalizedUserId);
    }

    public static bool TryLoadExisting(string userId, out CharacterProgressSave save)
    {
        save = null;
        string path = GetAccountProgressPath(userId);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
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
        File.WriteAllText(path, json);
    }

    public static string GetAccountProgressPath(string userId)
    {
        string safeUserId = SanitizeFileName(NormalizeUserId(userId));
        return Path.Combine(Application.persistentDataPath, AccountsFolderName, safeUserId + "-characters.json");
    }

    private static CharacterProgressSave CreateEmptySave(string userId)
    {
        return new CharacterProgressSave
        {
            userId = userId
        };
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
