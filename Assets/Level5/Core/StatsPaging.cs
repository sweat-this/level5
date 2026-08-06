using UnityEngine;

/// <summary>
/// Page arithmetic for the stats browser.
///
/// The page size used to be the literal 10 in eight places - both page-count helpers, both
/// increment helpers, the SQL LIMIT, and the offset calculation - while a `ResultsPerPage` constant
/// sat unused nearby. The wrap-around also assumed at least one page, so paging left through an
/// empty result set produced page -1 and a "page 0 / 0" display.
/// </summary>
public static class StatsPaging
{
    /// <summary>Rows shown on one page of the stats table.</summary>
    public const int ResultsPerPage = 10;

    /// <summary>
    /// Number of pages needed for <paramref name="totalResults"/> rows. Always at least 1, so an
    /// empty table reads "page 1 / 1" rather than "page 0 / 0" and the wrap-around below has a
    /// valid page to land on.
    /// </summary>
    public static int PageCount(int totalResults)
    {
        if (totalResults <= 0)
        {
            return 1;
        }

        return ((totalResults - 1) / ResultsPerPage) + 1;
    }

    /// <summary>Next page, wrapping to the first. Result is always a valid page index.</summary>
    public static int NextPage(int currentPage, int totalResults)
    {
        int lastPage = PageCount(totalResults) - 1;
        int page = Clamp(currentPage, lastPage);
        return page >= lastPage ? 0 : page + 1;
    }

    /// <summary>Previous page, wrapping to the last. Result is always a valid page index.</summary>
    public static int PreviousPage(int currentPage, int totalResults)
    {
        int lastPage = PageCount(totalResults) - 1;
        int page = Clamp(currentPage, lastPage);
        return page <= 0 ? lastPage : page - 1;
    }

    /// <summary>Row offset for a page, for use as a bound SQL parameter. Never negative.</summary>
    public static int OffsetFor(int page)
    {
        return Mathf.Max(0, page) * ResultsPerPage;
    }

    /// <summary>"page 3 / 7", 1-based for display.</summary>
    public static string DisplayLabel(int currentPage, int totalResults)
    {
        int pageCount = PageCount(totalResults);
        int page = Clamp(currentPage, pageCount - 1);
        return "page " + (page + 1) + " / " + pageCount;
    }

    private static int Clamp(int page, int lastPage)
    {
        return Mathf.Clamp(page, 0, Mathf.Max(0, lastPage));
    }
}
