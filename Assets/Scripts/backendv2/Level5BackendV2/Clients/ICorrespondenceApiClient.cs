using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    public interface ICorrespondenceApiClient
    {
        IEnumerator CreateChallenge(CreateChallengeDto request, Action<ApiResponse<SeriesResponseDto>> completed);

        IEnumerator Accept(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed);

        IEnumerator Decline(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed);

        IEnumerator Cancel(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed);

        IEnumerator Get(Guid seriesId, Action<ApiResponse<SeriesResponseDto>> completed);

        IEnumerator ListIncoming(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

        IEnumerator ListOutgoing(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

        IEnumerator ListActive(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

        IEnumerator ListCompleted(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

        /// <summary>Every terminal series either participant is in (Completed, Declined, Cancelled, Expired) - the durable-history surface, distinct from <see cref="ListCompleted"/>'s "actually finished play" scope.</summary>
        IEnumerator ListHistory(int limit, string cursor, Action<ApiResponse<SeriesSummaryPageDto>> completed);

        IEnumerator StartAttempt(
            Guid seriesId, int gameNumber, Action<ApiResponse<AttemptDescriptorDto>> completed);

        IEnumerator CompleteAttempt(
            Guid seriesId,
            int gameNumber,
            Guid attemptId,
            IReadOnlyDictionary<string, double> metrics,
            Action<ApiResponse<SeriesResponseDto>> completed);
    }
}
