using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// What one competitive run produced, as plain numbers.
    ///
    /// This is the only thing gameplay hands to the Versus domain. It replaces "the winner is
    /// whoever this sorted list of scene components puts first", which was mode-agnostic and could
    /// only ever rank by total points.
    ///
    /// It carries the ruleset it was produced under and the version of those rules, so a result can
    /// be checked against the attempt it is being submitted for instead of being trusted. Immutable:
    /// a result that can be edited after submission is not a result.
    /// </summary>
    public sealed class AttemptResult
    {
        private static readonly int MetricCount = Enum.GetValues(typeof(AttemptMetric)).Length;

        private readonly float[] values;

        private AttemptResult(RulesetId rulesetId, int rulesetVersion, float[] values)
        {
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            this.values = values;
        }

        public RulesetId RulesetId { get; }

        public int RulesetVersion { get; }

        public float Get(AttemptMetric metric)
        {
            int index = (int)metric;
            return index >= 0 && index < values.Length ? values[index] : 0f;
        }

        /// <summary>A copy of the raw values, indexed by <see cref="AttemptMetric"/>. For serialization.</summary>
        public float[] ToArray()
        {
            float[] copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        /// <summary>Rebuilds a result from stored values. Persistence only.</summary>
        public static AttemptResult FromValues(RulesetId rulesetId, int rulesetVersion, float[] storedValues)
        {
            if (!rulesetId.HasValue)
            {
                throw new VersusDomainException("an attempt result needs the ruleset it was produced under");
            }

            if (rulesetVersion < 1)
            {
                throw new VersusDomainException(
                    $"an attempt result for '{rulesetId.Value}' needs a ruleset version of at least 1");
            }

            float[] copy = new float[MetricCount];
            if (storedValues != null)
            {
                // A document written by an older build has fewer metrics than this build knows; a
                // document written by a newer one has more. Take what overlaps rather than refusing
                // to read - the ruleset version is what decides whether the rules are compatible,
                // and it has already been checked by the time anything gets here.
                int shared = Math.Min(copy.Length, storedValues.Length);
                Array.Copy(storedValues, copy, shared);
            }

            return new AttemptResult(rulesetId, rulesetVersion, copy);
        }

        public override string ToString()
        {
            return $"{RulesetId.Value} v{RulesetVersion}: score {Get(AttemptMetric.Score)}";
        }

        /// <summary>
        /// Builds a result one metric at a time.
        ///
        /// A builder rather than a long constructor because a mode fills in three or four of the
        /// eight metrics and leaving the rest as positional zeroes is how the wrong number ends up
        /// in the wrong slot.
        /// </summary>
        public sealed class Builder
        {
            private readonly float[] values = new float[MetricCount];
            private readonly RulesetId rulesetId;
            private readonly int rulesetVersion;

            public Builder(RulesetId rulesetId, int rulesetVersion)
            {
                if (!rulesetId.HasValue)
                {
                    throw new VersusDomainException("an attempt result needs the ruleset it was produced under");
                }

                if (rulesetVersion < 1)
                {
                    throw new VersusDomainException(
                        $"an attempt result for '{rulesetId.Value}' needs a ruleset version of at least 1");
                }

                this.rulesetId = rulesetId;
                this.rulesetVersion = rulesetVersion;
            }

            public Builder Set(AttemptMetric metric, float value)
            {
                values[(int)metric] = value;
                return this;
            }

            /// <summary>Fills in accuracy from made and attempted, so no caller computes it twice.</summary>
            public Builder SetShooting(int made, int attempted)
            {
                Set(AttemptMetric.ShotsMade, made);
                Set(AttemptMetric.ShotsAttempted, attempted);
                Set(AttemptMetric.Accuracy, attempted > 0 ? made * 100f / attempted : 0f);
                return this;
            }

            public AttemptResult Build()
            {
                float[] copy = new float[values.Length];
                Array.Copy(values, copy, values.Length);
                return new AttemptResult(rulesetId, rulesetVersion, copy);
            }
        }
    }
}
