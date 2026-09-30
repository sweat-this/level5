using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// The two characters last chosen for a simultaneous series, so they survive the Local Versus screen
/// being rebuilt.
///
/// Every gameplay scene returns to a freshly loaded <c>level_00_local_versus</c> with a new
/// <see cref="LocalVersusScreenModel"/>, so a pick held only on the model is gone after each game of a
/// Best-of-N. This holds selection only - character ids, keyed by series - and no competitive state: the
/// series document stays the only authority on everything else. One series is remembered; choosing for a
/// different series replaces it, and a process restart forgets it (the defaults are a fine start).
/// </summary>
public static class LocalVersusSimultaneousPicks
{
    private static SeriesId seriesId;
    private static int firstCharacterId;
    private static int secondCharacterId;

    // Static state survives with domain reload disabled; a pick must never outlive a session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Clear();
    }

    public static void Remember(SeriesId series, int first, int second)
    {
        seriesId = series;
        firstCharacterId = first;
        secondCharacterId = second;
    }

    /// <summary>The remembered ids for this series, or false when it is not the series remembered.</summary>
    public static bool TryRecall(SeriesId series, out int first, out int second)
    {
        first = firstCharacterId;
        second = secondCharacterId;
        return series.HasValue && series.Equals(seriesId);
    }

    public static void Clear()
    {
        seriesId = default;
        firstCharacterId = 0;
        secondCharacterId = 0;
    }
}
