using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Level5.BackendV2
{
    /// <summary>
    /// Classifies each row of an Active-series list into <see cref="ActiveSeriesTurn.YourTurn"/> /
    /// <see cref="ActiveSeriesTurn.OpponentTurn"/> by fetching its detail, and caches the result by
    /// series id - so the Active Series tab (shows every row, badged with whose turn it is) and the
    /// Your Turn tab (the same rows, filtered) can share one classification pass instead of each
    /// re-fetching detail independently.
    /// </summary>
    public sealed class ActiveSeriesTurnClassifier
    {
        private readonly Dictionary<Guid, ActiveSeriesTurn> turnBySeriesId = new Dictionary<Guid, ActiveSeriesTurn>();

        public bool IsLoading { get; private set; }

        public string ErrorMessage { get; private set; }

        public ActiveSeriesTurn? TurnFor(Guid seriesId)
        {
            return turnBySeriesId.TryGetValue(seriesId, out ActiveSeriesTurn turn) ? turn : (ActiveSeriesTurn?)null;
        }

        public IEnumerable<SeriesSummaryDto> YourTurnItems(IReadOnlyList<SeriesSummaryDto> items)
        {
            return items.Where(item => TurnFor(item.Id) == ActiveSeriesTurn.YourTurn);
        }

        /// <summary>Fetches detail for every item not already classified, one request at a time.
        /// Existing classifications are kept; call <see cref="Reset"/> first (e.g. after a list
        /// refresh) to reclassify everything from scratch, since a stale cached turn is exactly the
        /// kind of state Backend V2 is meant to remain authoritative over.
        ///
        /// <paramref name="onItemClassified"/> fires after each item settles (success or failure),
        /// before the next request starts - real network latency means classifying a full page
        /// sequentially can take several seconds, so the caller should use this to render each row
        /// as it becomes available rather than blocking the whole tab on the slowest request.
        /// </summary>
        public IEnumerator ClassifyAll(IReadOnlyList<SeriesSummaryDto> items, Action onItemClassified = null)
        {
            IsLoading = true;
            ErrorMessage = null;
            bool anyFailed = false;

            foreach (SeriesSummaryDto item in items)
            {
                if (turnBySeriesId.ContainsKey(item.Id))
                {
                    continue;
                }

                ApiResponse<SeriesResponseDto> response = null;
                yield return BackendV2Runtime.Correspondence.Get(item.Id, r => response = r);

                if (response != null && response.Success)
                {
                    turnBySeriesId[item.Id] = SeriesRowClassifier.ClassifyTurn(response.Value);
                }
                else
                {
                    anyFailed = true;
                }

                onItemClassified?.Invoke();
            }

            IsLoading = false;
            ErrorMessage = anyFailed ? "some series could not be checked for whose turn it is" : null;
        }

        public void Reset()
        {
            turnBySeriesId.Clear();
        }
    }
}
