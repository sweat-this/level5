using System;
using System.Linq;

namespace Level5.BackendV2
{
    /// <summary>Which side of the current game an Active-series row is waiting on.</summary>
    public enum ActiveSeriesTurn
    {
        YourTurn,
        OpponentTurn,
    }

    /// <summary>
    /// Classifies an Active-series row into "your turn" or "opponent's turn" from a
    /// <see cref="SeriesResponseDto"/> detail fetch.
    ///
    /// Incoming/Outgoing/Active/Completed bucket membership itself needs no classification here -
    /// <c>ICorrespondenceApiClient.ListIncoming</c>/<c>ListOutgoing</c>/<c>ListActive</c>/
    /// <c>ListCompleted</c> already define those buckets by which endpoint returned the row. This
    /// class only answers the one thing a series summary cannot: within the Active bucket, whose
    /// turn the current game is.
    ///
    /// Reads only presence/absence and <see cref="AttemptViewDto.Status"/> of <c>YourAttempt</c> -
    /// never <c>OpponentAttempt</c>, which is exactly the field the server's own sealed-result
    /// projection may still be hiding. Defaults to <see cref="ActiveSeriesTurn.YourTurn"/> on any
    /// ambiguity: if that is wrong, starting the attempt is safely refused server-side, whereas
    /// wrongly defaulting to <see cref="ActiveSeriesTurn.OpponentTurn"/> would hide an actionable
    /// turn from the player.
    /// </summary>
    public static class SeriesRowClassifier
    {
        /// <summary>The <see cref="AttemptViewDto.Status"/> value meaning "this attempt's result has
        /// been submitted." Confirmed against <c>Assets/Tests/Editor/BackendV2Fixtures.cs</c>'
        /// series-status values ("Pending"/"Active") and this codebase's other server-string
        /// conventions; not yet observed directly for a completed attempt in this repo - the first
        /// live-backend smoke check for #159 should submit one result and confirm this string, per
        /// docs/backend-v2-correspondence-certification.md.</summary>
        internal const string CompletedAttemptStatus = "Completed";

        public static ActiveSeriesTurn ClassifyTurn(SeriesResponseDto detail)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            GameRoundViewDto currentGame =
                detail.Games?.FirstOrDefault(round => round.GameNumber == detail.CurrentGameNumber);

            bool yourAttemptIsDone = currentGame?.YourAttempt != null
                && string.Equals(currentGame.YourAttempt.Status, CompletedAttemptStatus, StringComparison.OrdinalIgnoreCase);

            return yourAttemptIsDone ? ActiveSeriesTurn.OpponentTurn : ActiveSeriesTurn.YourTurn;
        }
    }
}
