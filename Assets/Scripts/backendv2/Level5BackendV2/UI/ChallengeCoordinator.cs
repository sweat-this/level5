using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// Orchestrates the "create challenge" command and the accept/decline/cancel actions on an
    /// existing challenge, against <see cref="BackendV2Runtime.Correspondence"/>.
    ///
    /// Create uses <see cref="ChallengeFormState"/>'s client-request-id retry discipline: the caller
    /// inspects the response and decides whether to call
    /// <see cref="ChallengeFormState.CompleteSubmission"/> (a definitive success or a non-retryable
    /// failure) or leave the form as-is so a later retry of this exact submission reuses the same id.
    /// </summary>
    public sealed class ChallengeCoordinator
    {
        public RowCommandState Commands { get; } = new RowCommandState();

        public IEnumerator Create(ChallengeFormState form, Action<ApiResponse<SeriesResponseDto>> completed)
        {
            if (form == null || form.OpponentId == null || string.IsNullOrEmpty(form.RulesetId))
            {
                completed?.Invoke(ApiResponse<SeriesResponseDto>.Fail(ApiErrorKind.Validation));
                yield break;
            }

            CreateChallengeDto request = new CreateChallengeDto(
                form.OpponentId.Value,
                form.TotalGames,
                form.RulesetId,
                form.GetOrBeginClientRequestId(),
                informationPolicy: form.InformationPolicy);

            ApiResponse<SeriesResponseDto> response = null;
            yield return BackendV2Runtime.Correspondence.CreateChallenge(request, r => response = r);
            completed?.Invoke(response);
        }

        /// <param name="ownerList">Refreshed on success, before the row's command guard is
        /// released - releasing it as soon as the network call settles (rather than after the
        /// refresh that actually updates/removes the row) would let a rapid second tap reach the
        /// server again for a series whose accept the UI hasn't shown as resolved yet.</param>
        public IEnumerator Accept(Guid seriesId, SeriesListCoordinator ownerList, Action<string> completed)
        {
            return RunCommand(seriesId, BackendV2Runtime.Correspondence.Accept, ownerList, completed);
        }

        public IEnumerator Decline(Guid seriesId, SeriesListCoordinator ownerList, Action<string> completed)
        {
            return RunCommand(seriesId, BackendV2Runtime.Correspondence.Decline, ownerList, completed);
        }

        public IEnumerator Cancel(Guid seriesId, SeriesListCoordinator ownerList, Action<string> completed)
        {
            return RunCommand(seriesId, BackendV2Runtime.Correspondence.Cancel, ownerList, completed);
        }

        private IEnumerator RunCommand(
            Guid seriesId,
            Func<Guid, Action<ApiResponse<SeriesResponseDto>>, IEnumerator> call,
            SeriesListCoordinator ownerList,
            Action<string> completed)
        {
            if (!Commands.TryBegin(seriesId))
            {
                completed?.Invoke("already in progress");
                yield break;
            }

            ApiResponse<SeriesResponseDto> response = null;
            yield return call(seriesId, r => response = r);

            if (response != null && response.Success && ownerList != null)
            {
                yield return ownerList.Refresh();
            }

            Commands.End(seriesId);

            completed?.Invoke(
                response != null && response.Success ? null : BackendV2ErrorMessages.Describe(response));
        }
    }
}
