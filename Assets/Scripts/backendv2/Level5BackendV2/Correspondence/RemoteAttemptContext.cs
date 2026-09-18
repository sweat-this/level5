using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// Everything needed to submit a remote attempt's result safely, taken from the server's own
    /// <see cref="AttemptDescriptorDto"/> - never from caller-supplied UI state.
    /// </summary>
    public sealed class RemoteAttemptContext
    {
        public RemoteAttemptContext(
            Guid seriesId,
            int gameNumber,
            Guid attemptId,
            Guid playerId,
            string rulesetId,
            int rulesetVersion,
            int competitionProtocolVersion,
            IReadOnlyList<ComparisonKeySummaryDto> comparisonKeys,
            IReadOnlyList<string> requiredResultMetrics)
        {
            SeriesId = seriesId;
            GameNumber = gameNumber;
            AttemptId = attemptId;
            PlayerId = playerId;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            CompetitionProtocolVersion = competitionProtocolVersion;
            ComparisonKeys = comparisonKeys;
            RequiredResultMetrics = requiredResultMetrics;
        }

        public Guid SeriesId { get; }

        public int GameNumber { get; }

        public Guid AttemptId { get; }

        public Guid PlayerId { get; }

        public string RulesetId { get; }

        public int RulesetVersion { get; }

        public int CompetitionProtocolVersion { get; }

        public IReadOnlyList<ComparisonKeySummaryDto> ComparisonKeys { get; }

        public IReadOnlyList<string> RequiredResultMetrics { get; }

        public static RemoteAttemptContext FromDescriptor(AttemptDescriptorDto descriptor)
        {
            return new RemoteAttemptContext(
                descriptor.SeriesId,
                descriptor.GameNumber,
                descriptor.AttemptId,
                descriptor.PlayerId,
                descriptor.RulesetId,
                descriptor.RulesetVersion,
                descriptor.CompetitionProtocolVersion,
                (IReadOnlyList<ComparisonKeySummaryDto>)descriptor.ComparisonKeys ?? Array.Empty<ComparisonKeySummaryDto>(),
                (IReadOnlyList<string>)descriptor.RequiredResultMetrics ?? Array.Empty<string>());
        }
    }
}
