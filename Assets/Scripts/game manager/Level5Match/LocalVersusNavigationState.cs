using Level5.Core.Match;
using Level5.Core.Versus;

/// <summary>
/// The one bit of transient navigation a local-versus turn needs: "when this match's summary is
/// dismissed, go back to the Local Versus screen instead of the Start screen."
///
/// It deliberately holds no competitive state. Which series exists, whose turn it is and what the
/// score is all live in the stored series document; <see cref="PreferredSeriesId"/> is only a hint
/// about which series the screen should open on. Losing this to a restart costs nothing, because the
/// Local Versus screen lists every stored series and shows the domain's truth.
///
/// Not a <see cref="MatchConfiguration.Source"/> check (that field is diagnostic metadata, not a
/// behavioral authority), and not <see cref="ActiveVersusAttempt"/> (which is cleared once a result
/// is accepted, exactly when the summary is on screen).
///
/// Tied to the exact match it was begun for, like <c>ActiveVersusAttempt</c>: a player who abandons a
/// local-versus match some other way and later plays an ordinary one must not be routed to a series
/// screen from it.
/// </summary>
public static class LocalVersusNavigationState
{
    private static bool pending;
    private static SeriesId preferredSeriesId;
    private static MatchConfiguration launchedFor;

    /// <summary>The series the Local Versus screen should open on, or none.</summary>
    public static SeriesId PreferredSeriesId => preferredSeriesId;

    /// <summary>
    /// True when the match currently loaded was launched from the Local Versus screen and its
    /// end-of-match navigation should lead back there.
    /// </summary>
    public static bool ReturnPending => pending
        && (launchedFor == null || ReferenceEquals(ActiveMatch.Configuration, launchedFor));

    /// <summary>Records that the next gameplay scene is a turn of <paramref name="seriesId"/>.</summary>
    public static void Begin(SeriesId seriesId)
    {
        pending = true;
        preferredSeriesId = seriesId;
        launchedFor = null;
    }

    /// <summary>Ties the pending return to the exact match that was launched for it.</summary>
    public static void Bind(MatchConfiguration configuration)
    {
        if (pending)
        {
            launchedFor = configuration;
        }
    }

    /// <summary>
    /// Hands the Local Versus screen its preferred series (or none) and forgets the return, so the
    /// hint is used exactly once.
    /// </summary>
    public static SeriesId Consume()
    {
        SeriesId preferred = preferredSeriesId;
        Clear();
        return preferred;
    }

    public static void Clear()
    {
        pending = false;
        preferredSeriesId = default;
        launchedFor = null;
    }
}
