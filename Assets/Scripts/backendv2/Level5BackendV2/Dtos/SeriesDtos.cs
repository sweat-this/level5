using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    // Mirrors Level5Backend/v2/src/Level5.Api/Controllers/SeriesController.cs (SeriesController,
    // SeriesQueriesController, SeriesAttemptsController - all under api/v2/series).

    /// <summary>
    /// The create-challenge request.
    ///
    /// <see cref="ClientRequestId"/> is optional on the wire (Backend V2's own DTO declares it
    /// nullable) but required by this client: <see cref="ICorrespondenceApiClient.CreateChallenge"/>
    /// refuses to send a request with an empty id, because retrying a create without reusing the
    /// same id is exactly the non-idempotent mistake the field exists to prevent.
    /// </summary>
    public sealed class CreateChallengeDto
    {
        public CreateChallengeDto(
            Guid opponentId,
            int totalGames,
            string rulesetId,
            Guid clientRequestId,
            int? rulesetVersion = null,
            string informationPolicy = null)
        {
            OpponentId = opponentId;
            TotalGames = totalGames;
            RulesetId = rulesetId;
            ClientRequestId = clientRequestId;
            RulesetVersion = rulesetVersion;
            InformationPolicy = informationPolicy;
        }

        public Guid OpponentId { get; }

        public int TotalGames { get; }

        public string RulesetId { get; }

        public int? RulesetVersion { get; }

        public string InformationPolicy { get; }

        public Guid ClientRequestId { get; }
    }

    public sealed class ComparisonKeyDto
    {
        public string Metric { get; set; }

        public string Direction { get; set; }
    }

    public sealed class ComparisonKeySummaryDto
    {
        public string Metric { get; set; }

        public string Direction { get; set; }
    }

    public sealed class FrozenRulesDto
    {
        public int CompetitionProtocolVersion { get; set; }

        public string RulesetId { get; set; }

        public int RulesetVersion { get; set; }

        public int MinimumCompatibleVersion { get; set; }

        public string ModeId { get; set; }

        public string InformationPolicy { get; set; }

        public bool AlternatesFirstAttempt { get; set; }

        public List<ComparisonKeyDto> ComparisonKeys { get; set; }
    }

    public sealed class AttemptViewDto
    {
        public Guid Id { get; set; }

        public string Status { get; set; }

        public Dictionary<string, double> Result { get; set; }
    }

    public sealed class GameRoundViewDto
    {
        public int GameNumber { get; set; }

        public AttemptViewDto YourAttempt { get; set; }

        public AttemptViewDto OpponentAttempt { get; set; }
    }

    public sealed class SeriesResponseDto
    {
        public Guid Id { get; set; }

        public Guid ChallengerId { get; set; }

        public Guid OpponentId { get; set; }

        public string Status { get; set; }

        public int TotalGames { get; set; }

        public int GamesToWin { get; set; }

        public int CurrentGameNumber { get; set; }

        public long Revision { get; set; }

        public Guid? WinnerId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public FrozenRulesDto Rules { get; set; }

        public List<GameRoundViewDto> Games { get; set; }
    }

    public sealed class SeriesSummaryDto
    {
        public Guid Id { get; set; }

        public Guid ChallengerId { get; set; }

        public Guid OpponentId { get; set; }

        public string Status { get; set; }

        public int CurrentGameNumber { get; set; }

        public int TotalGames { get; set; }

        public long Revision { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// A page of series summaries. <see cref="NextCursor"/> must be treated as opaque - passed back
    /// verbatim on the next request, never parsed or constructed - per Backend V2's own doc comment
    /// on this endpoint family.
    /// </summary>
    public sealed class SeriesSummaryPageDto
    {
        public List<SeriesSummaryDto> Items { get; set; }

        public int Limit { get; set; }

        public string NextCursor { get; set; }
    }

    /// <summary>
    /// What <c>StartAttempt</c> returns: the authoritative, participant-safe descriptor Unity needs
    /// to build its local <c>MatchConfiguration</c> for this attempt. See
    /// <see cref="RemoteAttemptDescriptorMapper"/> for how this becomes a match.
    /// </summary>
    public sealed class AttemptDescriptorDto
    {
        public Guid SeriesId { get; set; }

        public Guid AttemptId { get; set; }

        public int GameNumber { get; set; }

        public Guid PlayerId { get; set; }

        public int CompetitionProtocolVersion { get; set; }

        public string RulesetId { get; set; }

        public int RulesetVersion { get; set; }

        public int MinimumCompatibleVersion { get; set; }

        /// <summary>Opaque to the transport layer - it is carried, not interpreted, until the mapper
        /// resolves the ruleset locally and reads its own <c>GameModeId</c>.</summary>
        public string ModeId { get; set; }

        public string InformationPolicy { get; set; }

        public int TotalGames { get; set; }

        public int GamesToWin { get; set; }

        public List<ComparisonKeySummaryDto> ComparisonKeys { get; set; }

        public List<string> RequiredResultMetrics { get; set; }
    }

    /// <summary>The complete-attempt request. Only named metrics - never a winner, score, current
    /// game, revision, frozen rules or the opponent's result. Backend V2 owns those decisions.</summary>
    public sealed class CompleteAttemptDto
    {
        public CompleteAttemptDto(IReadOnlyDictionary<string, double> metrics)
        {
            Metrics = metrics;
        }

        public IReadOnlyDictionary<string, double> Metrics { get; }
    }
}
