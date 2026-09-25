using System;
using System.Collections.Generic;
using System.Linq;

namespace Level5.BackendV2
{
    /// <summary>
    /// A narrow, JSON-round-trippable copy of <see cref="RemoteAttemptContext"/>'s fields, used only
    /// by <see cref="PendingRemoteAttemptResultStore"/>'s local persistence.
    ///
    /// <see cref="RemoteAttemptContext"/> itself is deliberately immutable (constructor-only,
    /// <c>IReadOnlyList&lt;T&gt;</c> properties) so production code can never hand a caller a context
    /// it can quietly mutate underneath a still-active attempt - weakening that just to make one
    /// caller's JSON persistence convenient would spread a settled shape decision. This DTO exists
    /// instead, with concrete <see cref="List{T}"/> collections (the same reason
    /// <see cref="SubmitMatchResultDto"/> stores <c>Metrics</c> as a concrete
    /// <see cref="Dictionary{TKey,TValue}"/> rather than an interface).
    /// </summary>
    public sealed class PendingRemoteAttemptContextDto
    {
        public PendingRemoteAttemptContextDto()
        {
        }

        public PendingRemoteAttemptContextDto(RemoteAttemptContext context)
        {
            SeriesId = context.SeriesId;
            GameNumber = context.GameNumber;
            AttemptId = context.AttemptId;
            PlayerId = context.PlayerId;
            RulesetId = context.RulesetId;
            RulesetVersion = context.RulesetVersion;
            CompetitionProtocolVersion = context.CompetitionProtocolVersion;
            ComparisonKeys = (context.ComparisonKeys ?? Array.Empty<ComparisonKeySummaryDto>())
                .Select(key => new ComparisonKeySummaryDto { Metric = key.Metric, Direction = key.Direction })
                .ToList();
            RequiredResultMetrics = new List<string>(context.RequiredResultMetrics ?? Array.Empty<string>());
        }

        public Guid SeriesId { get; set; }

        public int GameNumber { get; set; }

        public Guid AttemptId { get; set; }

        public Guid PlayerId { get; set; }

        public string RulesetId { get; set; }

        public int RulesetVersion { get; set; }

        public int CompetitionProtocolVersion { get; set; }

        public List<ComparisonKeySummaryDto> ComparisonKeys { get; set; } = new List<ComparisonKeySummaryDto>();

        public List<string> RequiredResultMetrics { get; set; } = new List<string>();

        public RemoteAttemptContext ToContext()
        {
            return new RemoteAttemptContext(
                SeriesId,
                GameNumber,
                AttemptId,
                PlayerId,
                RulesetId,
                RulesetVersion,
                CompetitionProtocolVersion,
                (IReadOnlyList<ComparisonKeySummaryDto>)(ComparisonKeys ?? new List<ComparisonKeySummaryDto>()),
                (IReadOnlyList<string>)(RequiredResultMetrics ?? new List<string>()));
        }

        public bool IsEquivalentTo(PendingRemoteAttemptContextDto other)
        {
            if (other == null)
            {
                return false;
            }

            if (SeriesId != other.SeriesId
                || GameNumber != other.GameNumber
                || AttemptId != other.AttemptId
                || PlayerId != other.PlayerId
                || RulesetId != other.RulesetId
                || RulesetVersion != other.RulesetVersion
                || CompetitionProtocolVersion != other.CompetitionProtocolVersion)
            {
                return false;
            }

            if (!ComparisonKeysEqual(ComparisonKeys, other.ComparisonKeys))
            {
                return false;
            }

            return StringListEqual(RequiredResultMetrics, other.RequiredResultMetrics);
        }

        private static bool ComparisonKeysEqual(List<ComparisonKeySummaryDto> a, List<ComparisonKeySummaryDto> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;
            if (countA != countB)
            {
                return false;
            }

            for (int i = 0; i < countA; i++)
            {
                if (a[i].Metric != b[i].Metric || a[i].Direction != b[i].Direction)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StringListEqual(List<string> a, List<string> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;
            if (countA != countB)
            {
                return false;
            }

            for (int i = 0; i < countA; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// One durably-persisted pending remote-attempt result, bound to exactly one Backend V2 player -
    /// <see cref="PendingRemoteAttemptResultStore"/>'s queue key is <c>(OwnerPlayerId, AttemptId)</c>,
    /// mirroring <see cref="PendingMatchResult"/> for the separate general-match-result queue.
    /// </summary>
    public sealed class PendingRemoteAttemptResultEntry
    {
        public PendingRemoteAttemptResultEntry()
        {
        }

        public Guid OwnerPlayerId { get; set; }

        public PendingRemoteAttemptContextDto Context { get; set; }

        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();

        public static PendingRemoteAttemptResultEntry From(
            Guid ownerPlayerId, RemoteAttemptContext context, IReadOnlyDictionary<string, double> metrics)
        {
            return new PendingRemoteAttemptResultEntry
            {
                OwnerPlayerId = ownerPlayerId,
                Context = new PendingRemoteAttemptContextDto(context),
                Metrics = metrics == null
                    ? new Dictionary<string, double>()
                    : new Dictionary<string, double>(metrics)
            };
        }

        /// <summary>Whether <paramref name="other"/> is the same logical pending result as this one -
        /// every context field equal and metrics compared by key/value, mirroring
        /// <see cref="SubmitMatchResultDto.IsEquivalentTo"/>. Used to tell an idempotent duplicate
        /// <see cref="PendingRemoteAttemptResultStore"/> enqueue apart from the one shape that must
        /// never happen locally: two different results sharing one <c>(OwnerPlayerId, AttemptId)</c>
        /// key.</summary>
        public bool IsEquivalentTo(PendingRemoteAttemptResultEntry other)
        {
            if (other == null || OwnerPlayerId != other.OwnerPlayerId)
            {
                return false;
            }

            if (Context == null || other.Context == null || !Context.IsEquivalentTo(other.Context))
            {
                return false;
            }

            return MetricsEqual(Metrics, other.Metrics);
        }

        private static bool MetricsEqual(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;
            if (countA != countB)
            {
                return false;
            }

            if (a == null)
            {
                return true;
            }

            foreach (KeyValuePair<string, double> entry in a)
            {
                if (!b.TryGetValue(entry.Key, out double otherValue) || otherValue != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
