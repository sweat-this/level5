using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>A paged series-list fetch, matching
    /// <c>ICorrespondenceApiClient.ListIncoming</c>/<c>ListOutgoing</c>/<c>ListActive</c>/<c>ListCompleted</c>'s
    /// shared shape.</summary>
    public delegate IEnumerator SeriesPageFetcher(
        int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

    /// <summary>
    /// One instance per series tab (Incoming / Outgoing / Active / Completed), each constructed with
    /// its own <see cref="ICorrespondenceApiClient"/> list method. Handles refresh and "load more"
    /// uniformly for all four - the opaque cursor is forwarded exactly as
    /// <see cref="SeriesSummaryPageDto"/> returned it, never parsed or reconstructed.
    /// </summary>
    public sealed class SeriesListCoordinator
    {
        private const int DefaultPageSize = 20;

        private readonly SeriesPageFetcher fetchPage;

        public SeriesListCoordinator(SeriesPageFetcher fetchPage)
        {
            this.fetchPage = fetchPage ?? throw new ArgumentNullException(nameof(fetchPage));
        }

        public ListViewState<SeriesSummaryDto> State { get; } = new ListViewState<SeriesSummaryDto>();

        /// <summary>A fresh first page, replacing whatever was there before.</summary>
        public IEnumerator Refresh(int pageSize = DefaultPageSize)
        {
            State.BeginLoad();
            ApiResponse<SeriesSummaryPageDto> response = null;
            yield return fetchPage(pageSize, null, r => response = r);

            if (response != null && response.Success)
            {
                State.ReplaceWith(response.Value.Items, response.Value.NextCursor);
            }
            else
            {
                State.Fail(BackendV2ErrorMessages.Describe(response));
            }
        }

        /// <summary>The next page, appended. A no-op when there is nothing more or a fetch is
        /// already in flight.</summary>
        public IEnumerator LoadMore(int pageSize = DefaultPageSize)
        {
            if (!State.HasMore || State.IsLoading)
            {
                yield break;
            }

            string cursor = State.NextCursor;
            State.BeginLoad();
            ApiResponse<SeriesSummaryPageDto> response = null;
            yield return fetchPage(pageSize, cursor, r => response = r);

            if (response != null && response.Success)
            {
                State.AppendPage(response.Value.Items, response.Value.NextCursor);
            }
            else
            {
                State.Fail(BackendV2ErrorMessages.Describe(response));
            }
        }
    }
}
