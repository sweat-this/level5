using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// api/v2/series/* - the correspondence/challenge lifecycle. All calls require an access token.
    ///
    /// This is the typed command/query boundary <c>IVersusSeriesRepository</c>'s own doc comment
    /// says a remote backend must be: narrow calls (create/accept/decline/cancel/start/complete),
    /// never a whole-aggregate upload/download.
    /// </summary>
    public sealed class CorrespondenceApiClient : AuthenticatedApiClientBase, ICorrespondenceApiClient
    {
        public CorrespondenceApiClient(IApiTransport transport, BackendV2SessionManager sessionManager)
            : base(transport, sessionManager)
        {
        }

        public IEnumerator CreateChallenge(CreateChallengeDto request, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            if (request == null || request.ClientRequestId == Guid.Empty)
            {
                completed?.Invoke(ApiResponse<SeriesResponseDto>.Fail(ApiErrorKind.Validation));
                return Empty();
            }

            return SendAuthorized(ApiHttpMethod.Post, "api/v2/series", request, completed);
        }

        public IEnumerator Accept(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/series/{seriesId}/accept", null, completed);
        }

        public IEnumerator Decline(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/series/{seriesId}/decline", null, completed);
        }

        public IEnumerator Cancel(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/series/{seriesId}/cancel", null, completed);
        }

        public IEnumerator Get(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, $"api/v2/series/{seriesId}", null, completed);
        }

        public IEnumerator ListIncoming(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            return ListPage("api/v2/series/incoming", limit, cursor, completed);
        }

        public IEnumerator ListOutgoing(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            return ListPage("api/v2/series/outgoing", limit, cursor, completed);
        }

        public IEnumerator ListActive(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            return ListPage("api/v2/series/active", limit, cursor, completed);
        }

        public IEnumerator ListCompleted(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            return ListPage("api/v2/series/completed", limit, cursor, completed);
        }

        public IEnumerator ListHistory(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            return ListPage("api/v2/series/history", limit, cursor, completed);
        }

        public IEnumerator StartAttempt(
            Guid seriesId, int gameNumber, Action<ApiResponse<AttemptDescriptorDto>> completed)
        {
            return SendAuthorized(
                ApiHttpMethod.Post, $"api/v2/series/{seriesId}/games/{gameNumber}/attempts/start", null, completed);
        }

        public IEnumerator CompleteAttempt(
            Guid seriesId,
            int gameNumber,
            Guid attemptId,
            IReadOnlyDictionary<string, double> metrics,
            Action<ApiResponse<SeriesResponseDto>> completed)
        {
            string path = $"api/v2/series/{seriesId}/games/{gameNumber}/attempts/{attemptId}/complete";
            return SendAuthorized(ApiHttpMethod.Post, path, new CompleteAttemptDto(metrics), completed);
        }

        private IEnumerator ListPage(
            string path, int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed)
        {
            // The cursor is opaque per Backend V2's own contract - passed through exactly as given,
            // never parsed or built here.
            Dictionary<string, string> query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
            if (!string.IsNullOrEmpty(cursor))
            {
                query["cursor"] = cursor;
            }

            return SendAuthorized(ApiHttpMethod.Get, path, null, completed, query);
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}
