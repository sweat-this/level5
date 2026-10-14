using System;
using System.Linq;

namespace Level5.BackendV2
{
    public enum ActiveSeriesTurn
    {
        YourTurn,
        OpponentTurn,
        Unknown,
    }

    /// <summary>Uses participant-visible status and frozen policy, never attempt result metrics.</summary>
    public static class SeriesRowClassifier
    {
        internal const string CompletedAttemptStatus = "Completed";

        public static ActiveSeriesTurn ClassifyTurn(SeriesResponseDto detail) =>
            ClassifyTurn(detail, BackendV2SessionStore.Current?.PlayerId ?? Guid.Empty);

        public static ActiveSeriesTurn ClassifyTurn(SeriesResponseDto detail, Guid playerId)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            if (!string.Equals(detail.Status, "Active", StringComparison.OrdinalIgnoreCase)
                || detail.CurrentGameNumber < 1 || detail.CurrentGameNumber > detail.TotalGames
                || detail.Games == null || detail.Rules == null)
                return ActiveSeriesTurn.Unknown;
            var rounds = detail.Games.Where(g => g != null && g.GameNumber == detail.CurrentGameNumber).ToList();
            if (rounds.Count > 1 || detail.Games.Any(g => g == null || g.GameNumber < 1 || g.GameNumber > detail.CurrentGameNumber))
                return ActiveSeriesTurn.Unknown;
            // Backend rounds are created lazily by StartAttempt. A complete detail with no
            // current round confirms neither participant has started; a null Games list does not.
            GameRoundViewDto round = rounds.FirstOrDefault();
            if (round?.YourAttempt != null)
            {
                if (IsCompleted(round.YourAttempt)) return ActiveSeriesTurn.OpponentTurn;
                if (round.YourAttempt.Status != "Created" && round.YourAttempt.Status != "Ready"
                    && round.YourAttempt.Status != "Started") return ActiveSeriesTurn.Unknown;
            }
            if (detail.Rules.InformationPolicy == "SealedAttempt") return ActiveSeriesTurn.YourTurn;
            if (detail.Rules.InformationPolicy != "OpenTarget" || playerId == Guid.Empty
                || (playerId != detail.ChallengerId && playerId != detail.OpponentId))
                return ActiveSeriesTurn.Unknown;
            Guid first = detail.Rules.AlternatesFirstAttempt && detail.CurrentGameNumber % 2 == 0
                ? detail.OpponentId : detail.ChallengerId;
            return playerId == first || IsCompleted(round?.OpponentAttempt)
                ? ActiveSeriesTurn.YourTurn : ActiveSeriesTurn.OpponentTurn;
        }

        private static bool IsCompleted(AttemptViewDto attempt) =>
            string.Equals(attempt?.Status, CompletedAttemptStatus, StringComparison.OrdinalIgnoreCase);
    }
}
