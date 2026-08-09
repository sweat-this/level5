using System;
using System.Collections.Generic;
using Level5.Core.Match;

namespace Level5.Core.Versus
{
    /// <summary>
    /// The forms of competition a ruleset supports.
    ///
    /// A flags enum rather than four booleans because these are genuinely a set - a mode can be
    /// both locally simultaneous and asynchronous - and because a set has one name in a signature,
    /// which independent booleans never do.
    ///
    /// The default is <see cref="None"/>, and that is the point: a mode is not competitive until
    /// somebody says which competition it belongs in. Nothing is exposed to correspondence play by
    /// accident.
    /// </summary>
    [Flags]
    public enum VersusCapability
    {
        None = 0,

        /// <summary>Both participants play the same run at the same time, on one machine.</summary>
        LocalSimultaneous = 1 << 0,

        /// <summary>Participants take turns on one machine, one attempt at a time.</summary>
        LocalAlternating = 1 << 1,

        /// <summary>Attempts can be separated by hours or days. Requires no live interaction.</summary>
        Asynchronous = 1 << 2,

        /// <summary>Live networked play. Nothing implements this yet.</summary>
        OnlineRealtime = 1 << 3
    }

    /// <summary>
    /// The measurements a competitive attempt can be judged on.
    ///
    /// A fixed, mode-agnostic set rather than an open dictionary. Every ruleset picks the ones it
    /// cares about and ignores the rest, which keeps results comparable, keeps the serialized form
    /// stable, and keeps comparison allocation-free. Adding a member here is a deliberate act with
    /// a version bump behind it; it is not something a new mode does casually.
    ///
    /// Never renumber a member: the numeric value indexes the stored metric array.
    /// </summary>
    public enum AttemptMetric
    {
        /// <summary>Total points scored, including bonus points.</summary>
        Score = 0,

        /// <summary>Shots made.</summary>
        ShotsMade = 1,

        /// <summary>Shots attempted.</summary>
        ShotsAttempted = 2,

        /// <summary>Made over attempted, as a percentage from 0 to 100.</summary>
        Accuracy = 3,

        /// <summary>How long the run took, in seconds. Lower is better in a race.</summary>
        CompletionTimeSeconds = 4,

        /// <summary>The longest run of consecutive makes.</summary>
        LongestStreak = 5,

        /// <summary>Total distance of made shots.</summary>
        TotalDistance = 6,

        /// <summary>Points awarded on top of the shots themselves.</summary>
        BonusPoints = 7
    }

    /// <summary>Which end of a metric wins.</summary>
    public enum MetricDirection
    {
        HigherWins = 0,
        LowerWins = 1
    }

    /// <summary>
    /// One step in deciding who won: a metric and the end of it that wins.
    ///
    /// A ruleset holds these in order. The first is the mode's actual objective; the ones after it
    /// are its own tie-breaks. Two attempts equal on every key is a draw, and that is the only
    /// place a draw can come from - there is no global tie-break rule anywhere in this domain.
    /// </summary>
    public readonly struct ComparisonKey
    {
        public ComparisonKey(AttemptMetric metric, MetricDirection direction)
        {
            Metric = metric;
            Direction = direction;
        }

        public AttemptMetric Metric { get; }

        public MetricDirection Direction { get; }

        public static ComparisonKey Highest(AttemptMetric metric)
        {
            return new ComparisonKey(metric, MetricDirection.HigherWins);
        }

        public static ComparisonKey Lowest(AttemptMetric metric)
        {
            return new ComparisonKey(metric, MetricDirection.LowerWins);
        }

        public override string ToString()
        {
            return Metric + (Direction == MetricDirection.HigherWins ? " (highest)" : " (lowest)");
        }
    }

    /// <summary>
    /// The contract between a gameplay mode and the Versus domain.
    ///
    /// It answers exactly four questions and nothing else: who am I, which build of my rules is
    /// this, which kinds of competition can I be played as, and how are two runs at me compared.
    /// Everything about *playing* the mode stays in <see cref="GameModeDefinition"/>; this type
    /// never learns about arenas, rosters, timers or shot markers.
    ///
    /// Immutable. A ruleset that changes is a new version, not a mutated object - an active series
    /// holds a frozen copy and must keep resolving against the rules it started under.
    /// </summary>
    public sealed class CompetitiveRuleset
    {
        private readonly ComparisonKey[] comparisonKeys;

        public CompetitiveRuleset(
            RulesetId id,
            int version,
            GameModeId modeId,
            VersusCapability capabilities,
            IEnumerable<ComparisonKey> comparisonKeys,
            int minimumCompatibleVersion = 1,
            string displayName = null)
        {
            if (!id.HasValue)
            {
                throw new VersusDomainException("a competitive ruleset needs a stable id");
            }

            if (!id.IsWellFormed())
            {
                throw new VersusDomainException(
                    $"ruleset id '{id.Value}' must be lowercase letters, digits and single hyphens");
            }

            if (version < 1)
            {
                throw new VersusDomainException($"ruleset '{id.Value}' has version {version}; versions start at 1");
            }

            if (minimumCompatibleVersion < 1 || minimumCompatibleVersion > version)
            {
                throw new VersusDomainException(
                    $"ruleset '{id.Value}' declares a minimum compatible version of "
                    + $"{minimumCompatibleVersion}, which must be between 1 and its own version {version}");
            }

            this.comparisonKeys = comparisonKeys == null
                ? new ComparisonKey[0]
                : new List<ComparisonKey>(comparisonKeys).ToArray();

            if (this.comparisonKeys.Length == 0)
            {
                throw new VersusDomainException(
                    $"ruleset '{id.Value}' declares no comparison keys, so it cannot decide a winner");
            }

            Id = id;
            Version = version;
            ModeId = modeId;
            Capabilities = capabilities;
            MinimumCompatibleVersion = minimumCompatibleVersion;
            DisplayName = string.IsNullOrEmpty(displayName) ? id.Value : displayName;
        }

        public RulesetId Id { get; }

        /// <summary>
        /// Which build of these competitive rules this is. Independent of the application version:
        /// build 1.6.0 and build 1.7.0 can both play <c>three-point-contest</c> version 4.
        /// </summary>
        public int Version { get; }

        /// <summary>The gameplay mode that produces attempts for this ruleset.</summary>
        public GameModeId ModeId { get; }

        public VersusCapability Capabilities { get; }

        /// <summary>
        /// The oldest version of this ruleset the current build can still play and score correctly.
        ///
        /// This is the whole of the compatibility story for now, and deliberately so: it is one
        /// number that lets a running series be refused clearly instead of being silently mis-scored
        /// by rules it never agreed to. A migration engine, if one is ever needed, hangs off this.
        /// </summary>
        public int MinimumCompatibleVersion { get; }

        public string DisplayName { get; }

        /// <summary>Ordered: the objective first, then this mode's own tie-breaks.</summary>
        public IReadOnlyList<ComparisonKey> ComparisonKeys => comparisonKeys;

        /// <summary>The metric a target is quoted in - "beat 47" means 47 of this.</summary>
        public AttemptMetric PrimaryMetric => comparisonKeys[0].Metric;

        public bool Supports(VersusCapability capability)
        {
            return capability != VersusCapability.None && (Capabilities & capability) == capability;
        }

        public bool SupportsAsync => Supports(VersusCapability.Asynchronous);

        /// <summary>Whether this build can play a series that was snapshotted at <paramref name="version"/>.</summary>
        public bool CanPlayVersion(int version)
        {
            return version >= MinimumCompatibleVersion && version <= Version;
        }

        /// <summary>
        /// Compares two results under these rules.
        ///
        /// Returns a positive number when <paramref name="left"/> wins, negative when
        /// <paramref name="right"/> does, and zero for a draw. Both results must have been produced
        /// under this ruleset at the same version; anything else is a caller bug, not a tie.
        /// </summary>
        public int Compare(AttemptResult left, AttemptResult right)
        {
            if (left == null || right == null)
            {
                throw new VersusDomainException("both results are needed to compare an attempt");
            }

            RequireProducedByThisRuleset(left, nameof(left));
            RequireProducedByThisRuleset(right, nameof(right));

            if (left.RulesetVersion != right.RulesetVersion)
            {
                throw new VersusDomainException(
                    $"cannot compare results of ruleset '{Id.Value}' produced under different versions "
                    + $"({left.RulesetVersion} and {right.RulesetVersion})");
            }

            foreach (ComparisonKey key in comparisonKeys)
            {
                float leftValue = left.Get(key.Metric);
                float rightValue = right.Get(key.Metric);
                if (leftValue == rightValue)
                {
                    continue;
                }

                bool leftIsHigher = leftValue > rightValue;
                bool higherWins = key.Direction == MetricDirection.HigherWins;
                return leftIsHigher == higherWins ? 1 : -1;
            }

            return 0;
        }

        private void RequireProducedByThisRuleset(AttemptResult result, string parameterName)
        {
            if (result.RulesetId != Id)
            {
                throw new VersusDomainException(
                    $"{parameterName} was produced by ruleset '{result.RulesetId}', not '{Id.Value}'");
            }
        }

        public override string ToString()
        {
            return $"{Id.Value} v{Version}";
        }
    }
}
