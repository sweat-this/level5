using System;
using System.IO;
using UnityEngine;

public static class CharacterProgressStore
{
    private const string AccountsFolderName = "accounts";

    public static CharacterProgressSave Load(string userId, CharacterPresetCatalog catalog)
    {
        string path = GetAccountProgressPath(userId);
        if (!File.Exists(path))
        {
            return catalog != null ? catalog.CreateDefaultProgress(userId) : CreateEmptySave(userId);
        }

        string json = File.ReadAllText(path);
        CharacterProgressSave save = JsonUtility.FromJson<CharacterProgressSave>(json);
        if (save == null)
        {
            return catalog != null ? catalog.CreateDefaultProgress(userId) : CreateEmptySave(userId);
        }

        if (string.IsNullOrEmpty(save.userId))
        {
            save.userId = userId;
        }

        if (save.characters == null)
        {
            save.characters = new System.Collections.Generic.List<PlayerCharacterProgress>();
        }

        return save;
    }

    public static void Save(CharacterProgressSave save)
    {
        if (save == null)
        {
            throw new ArgumentNullException(nameof(save));
        }

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
        string safeUserId = string.IsNullOrWhiteSpace(userId) ? "guest" : SanitizeFileName(userId);
        return Path.Combine(Application.persistentDataPath, AccountsFolderName, safeUserId + "-characters.json");
    }

    private static CharacterProgressSave CreateEmptySave(string userId)
    {
        return new CharacterProgressSave
        {
            userId = userId
        };
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
