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

    public static bool TryApplyProgressionSnapshot(
        string userId,
        int characterId,
        int experience,
        int level,
        out string error)
    {
        error = string.Empty;
        try
        {
            string normalizedUserId = NormalizeUserId(userId);
            CharacterProgressSave save;
            if (!TryLoadExisting(normalizedUserId, out save))
            {
                save = CreateEmptySave(normalizedUserId);
            }

            Normalize(normalizedUserId, save);
            PlayerCharacterProgress progress = save.characters.Find(value =>
                value != null && value.legacyPlayerId == characterId);
            if (progress == null)
            {
                progress = new PlayerCharacterProgress
                {
                    characterId = "legacy-" + characterId,
                    legacyPlayerId = characterId,
                    unlocked = true
                };
                save.characters.Add(progress);
            }

            progress.experience = Math.Max(0, experience);
            progress.level = Math.Max(0, level);
            progress.lastModifiedUtc = DateTime.UtcNow.ToString("o");
            Save(save);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError("Failed to update the character progress projection: " + exception);
            return false;
        }
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

public static class AtomicFile
{
    public static bool TryReadAllText(string path, out string contents)
    {
        return TryReadAllText(path, value => true, out contents);
    }

    public static bool TryReadAllText(string path, Func<string, bool> validator, out string contents)
    {
        contents = null;
        if (string.IsNullOrEmpty(path) || validator == null)
        {
            return false;
        }

        if (TryRead(path, out string primaryContents) && IsValid(primaryContents, validator))
        {
            contents = primaryContents;
            return true;
        }

        if (TryRead(GetBackupPath(path), out string backupContents) && IsValid(backupContents, validator))
        {
            contents = backupContents;
            return true;
        }

        return false;
    }

    public static void WriteAllText(string path, string contents)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        string backupPath = GetBackupPath(path);
        File.WriteAllText(temporaryPath, contents ?? string.Empty);

        try
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporaryPath, path, backupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(temporaryPath, path, true);
                }
            }
            else
            {
                File.Move(temporaryPath, path);
                File.Copy(path, backupPath, true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool TryRead(string path, out string contents)
    {
        contents = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            contents = File.ReadAllText(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsValid(string contents, Func<string, bool> validator)
    {
        try
        {
            return validator(contents);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetBackupPath(string path)
    {
        return path + ".bak";
    }
}
