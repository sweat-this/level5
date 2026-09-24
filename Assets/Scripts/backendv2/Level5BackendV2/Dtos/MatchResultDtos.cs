using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    // Mirrors Level5Backend/v2/src/Level5.Api/Controllers/MatchResultsController.cs.

    /// <summary>
    /// Stable, named general match-result metrics. Persistence and serialization must always key on
    /// the metric's <em>name</em> (<c>ToString()</c>), never its enum ordinal - mirrors Backend V2's
    /// own <c>Level5.Domain.Results.MatchResultMetric</c> member-for-member, the same discipline
    /// <c>Level5.Core.Versus.AttemptMetric</c> already follows for the separate correspondence
    /// metric set. Deliberately its own enum, not shared with <c>AttemptMetric</c>: an ordinary
    /// match result is not a correspondence attempt result, and the two sets must be free to evolve
    /// independently (same reasoning as the backend's own doc comment on this split).
    /// </summary>
    public enum MatchResultMetric
    {
        TotalPoints,
        ShotsMade,
        TotalDistance,
        CompletionTimeSeconds,
        LongestStreak,
        EnemiesKilled
    }

    /// <summary>
    /// The four supported general match-result modifiers. Mutable get/set properties (rather than
    /// the constructor-only immutable style <c>CreateChallengeDto</c> uses) because, unlike a
    /// create-challenge request, a <see cref="SubmitMatchResultDto"/> carrying this must round-trip
    /// through <see cref="PendingMatchResultStore"/>'s local JSON persistence - both serialized out
    /// and deserialized back in - not only serialized once for the wire.
    /// </summary>
    public sealed class MatchResultModifiersDto
    {
        public bool Hardcore { get; set; }

        public bool TrafficEnabled { get; set; }

        public bool EnemiesEnabled { get; set; }

        public bool SniperEnabled { get; set; }
    }

    /// <summary>
    /// The general match-result submission request. <see cref="ClientResultId"/> together with the
    /// authenticated acting player is Backend V2's whole idempotency key - callers must reuse the
    /// exact same id (and the exact same payload) on every retry of the same logical result, never
    /// regenerate one. PlayerId is deliberately absent: Backend V2 derives the owner from the
    /// authenticated principal, never from client-supplied data.
    ///
    /// <see cref="Metrics"/> is stored as a concrete <see cref="Dictionary{TKey,TValue}"/> (not
    /// <c>IReadOnlyDictionary&lt;string,double&gt;</c>) so this type round-trips reliably through
    /// <see cref="BackendV2Json"/> in both directions - out over the wire, and back in from
    /// <see cref="PendingMatchResultStore"/>'s local queue file.
    /// </summary>
    public sealed class SubmitMatchResultDto
    {
        public SubmitMatchResultDto()
        {
        }

        public SubmitMatchResultDto(
            Guid clientResultId,
            int modeId,
            int levelId,
            string characterId,
            string clientVersion,
            string platform,
            IReadOnlyDictionary<string, double> metrics,
            MatchResultModifiersDto modifiers)
        {
            ClientResultId = clientResultId;
            ModeId = modeId;
            LevelId = levelId;
            CharacterId = characterId;
            ClientVersion = clientVersion;
            Platform = platform;
            Metrics = metrics == null ? new Dictionary<string, double>() : new Dictionary<string, double>(metrics);
            Modifiers = modifiers;
        }

        public Guid ClientResultId { get; set; }

        public int ModeId { get; set; }

        public int LevelId { get; set; }

        public string CharacterId { get; set; }

        public string ClientVersion { get; set; }

        public string Platform { get; set; }

        public Dictionary<string, double> Metrics { get; set; }

        public MatchResultModifiersDto Modifiers { get; set; }

        /// <summary>Whether <paramref name="other"/> is the same logical result payload as this one
        /// - every field equal, metrics compared by key/value rather than by dictionary reference or
        /// insertion order. Used to tell an idempotent duplicate <see cref="PendingMatchResultStore"/>
        /// enqueue apart from the one payload shape that must never happen locally: two different
        /// results sharing one <see cref="ClientResultId"/>.</summary>
        public bool IsEquivalentTo(SubmitMatchResultDto other)
        {
            if (other == null)
            {
                return false;
            }

            if (ClientResultId != other.ClientResultId
                || ModeId != other.ModeId
                || LevelId != other.LevelId
                || CharacterId != other.CharacterId
                || ClientVersion != other.ClientVersion
                || Platform != other.Platform)
            {
                return false;
            }

            if (!ModifiersEqual(Modifiers, other.Modifiers))
            {
                return false;
            }

            return MetricsEqual(Metrics, other.Metrics);
        }

        private static bool ModifiersEqual(MatchResultModifiersDto a, MatchResultModifiersDto b)
        {
            bool hardcoreA = a?.Hardcore ?? false;
            bool trafficA = a?.TrafficEnabled ?? false;
            bool enemiesA = a?.EnemiesEnabled ?? false;
            bool sniperA = a?.SniperEnabled ?? false;
            bool hardcoreB = b?.Hardcore ?? false;
            bool trafficB = b?.TrafficEnabled ?? false;
            bool enemiesB = b?.EnemiesEnabled ?? false;
            bool sniperB = b?.SniperEnabled ?? false;

            return hardcoreA == hardcoreB && trafficA == trafficB && enemiesA == enemiesB && sniperA == sniperB;
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

    public sealed class MatchResultResponseDto
    {
        public Guid Id { get; set; }

        public Guid PlayerId { get; set; }

        public Guid ClientResultId { get; set; }

        public int ModeId { get; set; }

        public int LevelId { get; set; }

        public string CharacterId { get; set; }

        public string ClientVersion { get; set; }

        public string Platform { get; set; }

        public Dictionary<string, double> Metrics { get; set; }

        public MatchResultModifiersDto Modifiers { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
