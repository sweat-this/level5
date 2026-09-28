using System;
using System.IO;

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

    /// <summary>
    /// Removes one atomic-file family - the primary path plus its ".bak"/".tmp" siblings - for a
    /// deleted account/profile. Best-effort per file: a locked/inaccessible file does not stop the
    /// other two from being attempted, and the return value reports whether every file in the family
    /// that existed was actually removed, so a caller can log a warning without treating a partial
    /// filesystem failure as a reason to undo an already-committed SQLite deletion (SQLite stays
    /// authoritative - see docs/persistence-boundaries.md).
    /// </summary>
    public static bool TryDeleteFamily(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        bool primaryDeleted = TryDeleteOne(path);
        bool backupDeleted = TryDeleteOne(GetBackupPath(path));
        bool temporaryDeleted = TryDeleteOne(path + ".tmp");
        return primaryDeleted && backupDeleted && temporaryDeleted;
    }

    private static bool TryDeleteOne(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

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
