using System;
using System.Collections.Generic;
using System.Linq;

namespace Level5.BackendV2
{
    public enum InboxCategory { Incoming, YourTurn, Waiting, Completed, Unconfirmed }

    public sealed class CorrespondenceInboxRow
    {
        public SeriesSummaryDto Series { get; }
        public InboxCategory Category { get; }
        public string AttentionKey { get; }
        public CorrespondenceInboxRow(SeriesSummaryDto series, InboxCategory category)
        {
            Series = series;
            Category = category;
            AttentionKey = category == InboxCategory.Incoming ? "challenge:" + series.Id.ToString("N")
                : category == InboxCategory.YourTurn ? "turn:" + series.Id.ToString("N") + ":" + series.CurrentGameNumber
                : category == InboxCategory.Completed ? "completed:" + series.Id.ToString("N") : null;
        }
    }

    /// <summary>Ephemeral projection of existing paged caches, never a second series store.</summary>
    public static class CorrespondenceInbox
    {
        public static IReadOnlyList<CorrespondenceInboxRow> Build(Guid player,
            ActiveSeriesTurnClassifier turns, params ListViewState<SeriesSummaryDto>[] sources)
        {
            var selected = new Dictionary<Guid, (SeriesSummaryDto series, bool fresh, bool conflict)>();
            foreach (var source in sources)
                foreach (var series in source.Items)
                {
                    if (series == null || series.Id == Guid.Empty
                        || (series.ChallengerId != player && series.OpponentId != player)) continue;
                    bool fresh = source.HasLoaded && !source.IsLoading && source.ErrorMessage == null;
                    if (!selected.TryGetValue(series.Id, out var prior) || series.Revision > prior.series.Revision)
                        selected[series.Id] = (series, fresh, false);
                    else if (series.Revision == prior.series.Revision)
                        selected[series.Id] = (series, fresh || prior.fresh, prior.conflict
                            || series.Status != prior.series.Status || series.CurrentGameNumber != prior.series.CurrentGameNumber
                        || series.ChallengerId != prior.series.ChallengerId || series.OpponentId != prior.series.OpponentId
                        || series.TotalGames != prior.series.TotalGames);
                }
            var rows = new List<CorrespondenceInboxRow>();
            foreach (var entry in selected.Values)
            {
                var series = entry.series;
                if (series.Status == "Declined" || series.Status == "Cancelled" || series.Status == "Expired") continue;
                InboxCategory category = InboxCategory.Unconfirmed;
                if (entry.fresh && !entry.conflict)
                {
                    if (series.Status == "PendingAcceptance" || series.Status == "Pending")
                        category = series.OpponentId == player ? InboxCategory.Incoming : InboxCategory.Waiting;
                    else if (series.Status == "Completed") category = InboxCategory.Completed;
                    else if (series.Status == "Active")
                    {
                        ActiveSeriesTurn? turn = turns.TurnFor(series);
                        if (turn == ActiveSeriesTurn.YourTurn) category = InboxCategory.YourTurn;
                        else if (turn == ActiveSeriesTurn.OpponentTurn) category = InboxCategory.Waiting;
                    }
                }
                rows.Add(new CorrespondenceInboxRow(series, category));
            }
            return rows.OrderBy(r => r.Category).ThenByDescending(r => r.Series.CreatedAt).ThenBy(r => r.Series.Id).ToList();
        }

        public static Guid? OpponentId(Guid player, Guid challenger, Guid opponent) =>
            player == challenger && player != opponent ? opponent
            : player == opponent && player != challenger ? challenger : (Guid?)null;

        public static string OpponentLabel(SeriesSummaryDto series, Guid player)
        {
            Guid? id = OpponentId(player, series.ChallengerId, series.OpponentId);
            PlayerProfileResponseDto identity = id == series.ChallengerId ? series.Challenger : series.Opponent;
            return id.HasValue && identity?.PlayerId == id.Value
                ? identity.DisplayName + " (" + identity.Tag + ")" : id?.ToString() ?? "unknown participant";
        }
    }
}
