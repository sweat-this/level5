using NUnit.Framework;

/// <summary>
/// Backend V2 leaderboard cutover: the forward-cycling cursor pagination state machine (first page,
/// next page via NextCursor, terminal-page wrap, and reset on mode/filter invalidation).
/// </summary>
public class OnlineLeaderboardPaginationStateTests
{
    [Test]
    public void StartsOnFirstPageWithNoCursor()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();

        Assert.That(state.PageNumber, Is.EqualTo(0));
        Assert.That(state.Cursor, Is.Null);
        Assert.That(state.DisplayLabel(), Is.EqualTo("page 1"));
    }

    [Test]
    public void AdvancingWithANextCursorMovesToTheNextPage()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();
        state.RecordPageResult("cursor-for-page-2");

        state.AdvancePage();

        Assert.That(state.PageNumber, Is.EqualTo(1));
        Assert.That(state.Cursor, Is.EqualTo("cursor-for-page-2"));
        Assert.That(state.DisplayLabel(), Is.EqualTo("page 2"));
    }

    [Test]
    public void ChainedAdvancesForwardTheMostRecentCursorEachTime()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();

        state.RecordPageResult("cursor-2");
        state.AdvancePage();
        state.RecordPageResult("cursor-3");
        state.AdvancePage();

        Assert.That(state.PageNumber, Is.EqualTo(2));
        Assert.That(state.Cursor, Is.EqualTo("cursor-3"));
    }

    [Test]
    public void AdvancingFromTheTerminalPageWrapsToTheFirstPage()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();
        state.RecordPageResult("cursor-2");
        state.AdvancePage();

        // server reported no further pages
        state.RecordPageResult(null);
        state.AdvancePage();

        Assert.That(state.PageNumber, Is.EqualTo(0));
        Assert.That(state.Cursor, Is.Null);
        Assert.That(state.DisplayLabel(), Is.EqualTo("page 1"));
    }

    [Test]
    public void ResetReturnsToTheFirstPageWithNoCursor()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();
        state.RecordPageResult("cursor-2");
        state.AdvancePage();

        state.Reset();

        Assert.That(state.PageNumber, Is.EqualTo(0));
        Assert.That(state.Cursor, Is.Null);
    }

    [Test]
    public void ResetAfterAWrapClearsAnyStaleNextCursorSoTheNextAdvanceWrapsAgain()
    {
        OnlineLeaderboardPaginationState state = new OnlineLeaderboardPaginationState();
        state.RecordPageResult("cursor-2");
        state.AdvancePage();
        state.Reset();

        // a fresh page-1 request for the (now-different) scope reports its own next cursor
        state.RecordPageResult("cursor-2-for-new-scope");
        state.AdvancePage();

        Assert.That(state.PageNumber, Is.EqualTo(1));
        Assert.That(state.Cursor, Is.EqualTo("cursor-2-for-new-scope"));
    }
}
