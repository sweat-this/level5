using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Level5.BackendV2
{
    /// <summary>Shared, participant-safe classifications tied to the exact loaded revision/game.</summary>
    public sealed class ActiveSeriesTurnClassifier
    {
        private readonly Dictionary<Guid, (long revision, int game, ActiveSeriesTurn turn)> turns =
            new Dictionary<Guid, (long, int, ActiveSeriesTurn)>();
        private int generation;
        private long ownerGeneration = -1;
        public bool IsLoading { get; private set; }
        public string ErrorMessage { get; private set; }

        public ActiveSeriesTurn? TurnFor(Guid id) =>
            ownerGeneration == BackendV2SessionStore.IdentityGeneration && turns.TryGetValue(id, out var entry)
                ? entry.turn : (ActiveSeriesTurn?)null;

        public ActiveSeriesTurn? TurnFor(SeriesSummaryDto item) =>
            item != null && turns.TryGetValue(item.Id, out var entry)
                && entry.revision == item.Revision && entry.game == item.CurrentGameNumber
                ? TurnFor(item.Id) : null;

        public IEnumerable<SeriesSummaryDto> YourTurnItems(IReadOnlyList<SeriesSummaryDto> items) =>
            items.Where(item => TurnFor(item) == ActiveSeriesTurn.YourTurn);

        public IEnumerator ClassifyAll(IReadOnlyList<SeriesSummaryDto> items, Action onItemClassified = null)
        {
            long owner = BackendV2SessionStore.IdentityGeneration;
            Guid? player = BackendV2SessionStore.Current?.PlayerId;
            if (ownerGeneration != owner) Reset();
            ownerGeneration = owner;
            int request = ++generation;
            IsLoading = true;
            ErrorMessage = null;
            bool failed = false;
            foreach (SeriesSummaryDto item in items)
            {
                if (TurnFor(item).HasValue) continue;
                ApiResponse<SeriesResponseDto> response = null;
                yield return BackendV2Runtime.Correspondence.Get(item.Id, r => response = r);
                if (request != generation) yield break;
                if (owner != BackendV2SessionStore.IdentityGeneration) { Reset(); yield break; }
                SeriesResponseDto detail = response != null && response.Success ? response.Value : null;
                ActiveSeriesTurn turn = detail != null && detail.Id == item.Id
                    && detail.Revision == item.Revision && detail.CurrentGameNumber == item.CurrentGameNumber
                    && player.HasValue && (detail.ChallengerId == player || detail.OpponentId == player)
                    ? SeriesRowClassifier.ClassifyTurn(detail, player.Value) : ActiveSeriesTurn.Unknown;
                if (turn != ActiveSeriesTurn.Unknown)
                    turns[item.Id] = (item.Revision, item.CurrentGameNumber, turn);
                else { turns.Remove(item.Id); failed = true; }
                onItemClassified?.Invoke();
            }
            IsLoading = false;
            ErrorMessage = failed ? "some series could not be confirmed; refresh to check again" : null;
        }

        public void Reset()
        {
            generation++;
            turns.Clear();
            IsLoading = false;
            ErrorMessage = null;
            ownerGeneration = -1;
        }
    }
}
