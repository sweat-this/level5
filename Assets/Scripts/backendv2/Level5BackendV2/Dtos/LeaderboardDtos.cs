using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    // Mirrors Level5Backend/v2/src/Level5.Api/Controllers/LeaderboardsController.cs.

    /// <summary>
    /// One ranked row of a Backend V2 leaderboard page. <see cref="Player"/> reuses
    /// <see cref="PlayerProfileResponseDto"/> - the backend's <c>PublicPlayerSummaryDto</c> is the
    /// exact same (PlayerId, DisplayName, Tag) shape. <see cref="Modifiers"/> reuses
    /// <see cref="MatchResultModifiersDto"/> for the same reason: this is the identical backend type
    /// <c>SubmitMatchResultDto</c> already uses, not a lookalike.
    /// </summary>
    public sealed class LeaderboardEntryDto
    {
        public Guid MatchResultId { get; set; }

        public PlayerProfileResponseDto Player { get; set; }

        public string CharacterId { get; set; }

        public int LevelId { get; set; }

        public double Value { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public MatchResultModifiersDto Modifiers { get; set; }
    }

    /// <summary>
    /// A page of a Backend V2 leaderboard. <see cref="NextCursor"/> must be treated as opaque -
    /// forwarded back verbatim on the next request, never parsed or constructed - the same
    /// discipline <see cref="SeriesSummaryPageDto.NextCursor"/> already follows.
    ///
    /// <see cref="Metric"/>/<see cref="Direction"/> are response metadata for presentation/debugging
    /// only. <see cref="Items"/> arrives already sorted by the server according to that metric and
    /// direction; callers must never re-sort it or use <see cref="Metric"/>/<see cref="Direction"/>
    /// to do so.
    /// </summary>
    public sealed class LeaderboardPageDto
    {
        public int ModeId { get; set; }

        public string Metric { get; set; }

        public string Direction { get; set; }

        public List<LeaderboardEntryDto> Items { get; set; }

        public int Limit { get; set; }

        public string NextCursor { get; set; }
    }
}
