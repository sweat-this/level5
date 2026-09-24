/// <summary>
/// Forward-cycling cursor pagination state for the Stats screen's online (Backend V2) leaderboard.
///
/// Mirrors the server's cursor contract exactly: the cursor it was last handed
/// (<see cref="Cursor"/>) is stored and forwarded verbatim, never decoded or reconstructed. Advancing
/// past the last known page wraps back to the first page (<see cref="Reset"/>) rather than requiring
/// a total-result count the server does not provide - this is the same forward-cycling UX the local
/// (SQLite) leaderboard's <c>StatsPaging.NextPage</c> already gives, but built on a cursor instead of
/// a page-count wrap. <see cref="StatsPaging.DisplayLabel"/> and its denominator remain local-only;
/// <see cref="DisplayLabel"/> here deliberately shows no fabricated total.
/// </summary>
public sealed class OnlineLeaderboardPaginationState
{
    private string nextCursor;

    public int PageNumber { get; private set; }

    public string Cursor { get; private set; }

    /// <summary>Back to the first page with no cursor - the scope this state was built for
    /// (mode/hardcore/traffic/enemies/sniper) has changed, or no page has ever loaded yet.</summary>
    public void Reset()
    {
        PageNumber = 0;
        Cursor = null;
        nextCursor = null;
    }

    /// <summary>Records the cursor a just-completed page request returned, for the next
    /// <see cref="AdvancePage"/> call. <paramref name="serverNextCursor"/> is null when the server
    /// reported no further pages.</summary>
    public void RecordPageResult(string serverNextCursor)
    {
        nextCursor = serverNextCursor;
    }

    /// <summary>Advances to the next page using the last recorded next-cursor, or wraps back to the
    /// first page when the server reported none.</summary>
    public void AdvancePage()
    {
        if (nextCursor != null)
        {
            Cursor = nextCursor;
            PageNumber++;
        }
        else
        {
            Reset();
        }
    }

    /// <summary>"page 3", 1-based - no denominator, since the server never reports a total count.</summary>
    public string DisplayLabel()
    {
        return "page " + (PageNumber + 1);
    }
}
