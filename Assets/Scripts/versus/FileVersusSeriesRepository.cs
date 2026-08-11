using System;
using System.Collections.Generic;
using System.IO;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using UnityEngine;

/// <summary>
/// Series stored as one JSON file each, under the platform's persistent data path.
///
/// One file per series rather than one document holding all of them. A correspondence series is
/// written after every single turn, and rewriting every series in the game each time is both slower
/// and a much bigger thing to lose if a write is interrupted. A file per series means a failed write
/// costs at most that series.
///
/// Writes go to a temporary file and are then moved into place, so a process that dies mid-write
/// leaves the previous version intact rather than a half-written one. This matches the care
/// <c>PendingMatchPersistenceStore</c> takes for the same reason.
/// </summary>
public sealed class FileVersusSeriesRepository : IVersusSeriesRepository
{
    private const string FolderName = "versus";
    private const string Extension = ".json";

    private readonly string root;

    /// <summary>Uses the application's persistent data path. The normal constructor.</summary>
    public FileVersusSeriesRepository()
        : this(Path.Combine(Application.persistentDataPath, FolderName))
    {
    }

    /// <summary>Uses a given folder. For tests and tools that must not touch the real save data.</summary>
    public FileVersusSeriesRepository(string root)
    {
        this.root = root;
    }

    public string Root => root;

    public bool Save(VersusSeries series)
    {
        if (series == null || !series.Id.HasValue)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(root);

            bool archived = ReadArchivedFlag(series.Id);
            string json = VersusSeriesSerializer.ToJson(series, archived, true);
            return WriteAtomically(PathFor(series.Id), json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save versus series {series.Id}: {exception}");
            return false;
        }
    }

    public VersusSeries Load(SeriesId id)
    {
        string json = ReadRaw(id);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            VersusSeries series = VersusSeriesSerializer.FromJson(json);
            if (series != null)
            {
                VersusLog.SeriesRestored(series);
            }

            return series;
        }
        catch (Exception exception)
        {
            // A corrupt document is left on disk deliberately. Deleting it would destroy the only
            // copy of a competition; leaving it means it can be recovered by hand.
            Debug.LogError($"Versus series {id} could not be read and was left in place: {exception}");
            return null;
        }
    }

    public bool Exists(SeriesId id)
    {
        return id.HasValue && File.Exists(PathFor(id));
    }

    public IReadOnlyList<SeriesSummary> ListSummaries()
    {
        List<SeriesSummary> summaries = new List<SeriesSummary>();

        if (!Directory.Exists(root))
        {
            return summaries;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(root, "*" + Extension);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not list versus series: {exception}");
            return summaries;
        }

        foreach (string file in files)
        {
            try
            {
                SeriesSummary summary = VersusSeriesSerializer.SummaryFromJson(File.ReadAllText(file));
                if (summary != null)
                {
                    summaries.Add(summary);
                }
            }
            catch (Exception exception)
            {
                // One unreadable file must not hide every other series from the list.
                Debug.LogWarning($"Skipped unreadable versus series file '{file}': {exception}");
            }
        }

        summaries.Sort(CompareByCreationDescending);
        return summaries;
    }

    public bool Delete(SeriesId id)
    {
        if (!Exists(id))
        {
            return false;
        }

        try
        {
            File.Delete(PathFor(id));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not delete versus series {id}: {exception}");
            return false;
        }
    }

    public bool Archive(SeriesId id)
    {
        string json = ReadRaw(id);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            VersusSeriesDocument document = JsonUtility.FromJson<VersusSeriesDocument>(json);
            if (document == null)
            {
                return false;
            }

            document.archived = true;
            return WriteAtomically(PathFor(id), JsonUtility.ToJson(document, true));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not archive versus series {id}: {exception}");
            return false;
        }
    }

    private string ReadRaw(SeriesId id)
    {
        if (!Exists(id))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(PathFor(id));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not read versus series {id}: {exception}");
            return null;
        }
    }

    private bool ReadArchivedFlag(SeriesId id)
    {
        string json = ReadRaw(id);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        VersusSeriesDocument document = JsonUtility.FromJson<VersusSeriesDocument>(json);
        return document != null && document.archived;
    }

    private static bool WriteAtomically(string path, string contents)
    {
        AtomicFile.WriteAllText(path, contents);
        return true;
    }

    /// <summary>
    /// The file a series lives in.
    ///
    /// Ids are generated by this project and contain nothing but letters, digits and hyphens, but
    /// the characters are filtered anyway: an id that ever arrives from a server must not be able to
    /// name a path outside this folder.
    /// </summary>
    private string PathFor(SeriesId id)
    {
        return Path.Combine(root, Sanitize(id.Value) + Extension);
    }

    private static string Sanitize(string id)
    {
        char[] characters = id.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            char character = characters[index];
            bool safe = (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character == '-'
                || character == '_';

            if (!safe)
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    private static int CompareByCreationDescending(SeriesSummary left, SeriesSummary right)
    {
        return right.CreatedAtUtc.CompareTo(left.CreatedAtUtc);
    }
}
