using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// api/v2/match-results - general ordinary-match-result ingestion. All calls require an access
    /// token. Deliberately separate from <see cref="ICorrespondenceApiClient"/>: this is not a
    /// <c>VersusSeries</c>/attempt-result command and never mutates a server-owned competitive
    /// aggregate, matching the backend controller's own doc comment.
    /// </summary>
    public sealed class MatchResultsApiClient : AuthenticatedApiClientBase, IMatchResultsApiClient
    {
        public MatchResultsApiClient(IApiTransport transport, BackendV2SessionManager sessionManager)
            : base(transport, sessionManager)
        {
        }

        public IEnumerator Submit(SubmitMatchResultDto request, Action<ApiResponse<MatchResultResponseDto>> completed)
        {
            if (request == null || request.ClientResultId == Guid.Empty)
            {
                completed?.Invoke(ApiResponse<MatchResultResponseDto>.Fail(ApiErrorKind.Validation));
                return Empty();
            }

            return SendAuthorized(ApiHttpMethod.Post, "api/v2/match-results", request, completed);
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}
