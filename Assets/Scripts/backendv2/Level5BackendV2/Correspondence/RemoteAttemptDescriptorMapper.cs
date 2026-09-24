using System;
using Level5.Core.Match;
using Level5.Core.Versus;

namespace Level5.BackendV2
{
    /// <summary>
    /// Turns a Backend V2 <see cref="AttemptDescriptorDto"/> into an ordinary local match, through
    /// the same <c>MatchRequest -&gt; MatchConfigurationBuilder -&gt; MatchConfiguration</c> pipeline
    /// every other launch path uses - deliberately the same shape as
    /// <c>VersusLauncher.BuildMatch</c>, not a parallel "remote gameplay" branch.
    ///
    /// Every frozen value (mode, ruleset, comparison keys, required metrics) comes from the
    /// descriptor. The only caller-supplied input is <c>levelId</c> and cosmetic choices
    /// (character, modifiers) - exactly the inputs <c>VersusLauncher.BuildMatch</c> already accepts
    /// as legitimately local, since the descriptor itself carries no arena/level id.
    /// </summary>
    public static class RemoteAttemptDescriptorMapper
    {
        /// <summary>The highest Competition Protocol version this build understands. Bump this only
        /// alongside the client changes needed to actually support the next version.</summary>
        public const int SupportedCompetitionProtocolVersion = 1;

        public static RemoteAttemptMapResult Map(
            AttemptDescriptorDto descriptor,
            int levelId,
            ParticipantId participantId,
            CharacterSelection character,
            MatchModifiers modifiers = null)
        {
            if (descriptor == null)
            {
                return RemoteAttemptMapResult.Failure("no attempt descriptor was provided");
            }

            if (descriptor.CompetitionProtocolVersion != SupportedCompetitionProtocolVersion)
            {
                return RemoteAttemptMapResult.Failure(
                    $"this build supports Competition Protocol v{SupportedCompetitionProtocolVersion}, "
                    + $"but the series was created under v{descriptor.CompetitionProtocolVersion}");
            }

            CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Find(new RulesetId(descriptor.RulesetId));
            if (ruleset == null)
            {
                return RemoteAttemptMapResult.Failure(
                    $"this build does not know the ruleset '{descriptor.RulesetId}'");
            }

            if (!ruleset.CanPlayVersion(descriptor.RulesetVersion))
            {
                return RemoteAttemptMapResult.Failure(
                    $"this build cannot play '{ruleset.DisplayName}' at rules version "
                    + $"{descriptor.RulesetVersion} (it supports {ruleset.MinimumCompatibleVersion} to "
                    + $"{ruleset.Version})");
            }

            string comparisonKeyMismatch = FindComparisonKeyMismatch(ruleset, descriptor.ComparisonKeys);
            if (comparisonKeyMismatch != null)
            {
                return RemoteAttemptMapResult.Failure(
                    $"this build's '{ruleset.DisplayName}' rules do not match what the server froze "
                    + $"for this series ({comparisonKeyMismatch}) - refusing to launch a match that "
                    + "could be scored differently than the server will score it");
            }

            string unsupportedMetric = FindUnsupportedMetric(descriptor.RequiredResultMetrics);
            if (unsupportedMetric != null)
            {
                return RemoteAttemptMapResult.Failure(
                    $"this build does not know the result metric '{unsupportedMetric}'");
            }

            PlayerRoster roster = PlayerRoster.Build(new[]
            {
                new PlayerRosterEntry(
                    PlayerControlType.LocalHuman,
                    character ?? CharacterSelection.None,
                    participantId.Value)
            });

            MatchRequest request = new MatchRequest(
                ruleset.ModeId,
                levelId,
                roster,
                modifiers ?? MatchModifiers.Default,
                CheerleaderSelection.None,
                "backend v2 remote attempt");

            MatchBuildResult buildResult = MatchCatalogs.Builder.Build(request);
            if (!buildResult.Succeeded)
            {
                return RemoteAttemptMapResult.Failure(
                    $"{ruleset.DisplayName} cannot be played on the chosen arena: {buildResult.Validation}");
            }

            return RemoteAttemptMapResult.Success(
                buildResult.Configuration, RemoteAttemptContext.FromDescriptor(descriptor));
        }

        /// <summary>
        /// Guards against the same RulesetId+RulesetVersion silently meaning different competitive
        /// rules on the two sides: the server's frozen, ordered comparison keys (what it will
        /// actually resolve games and series with) must match this build's own resolved ruleset
        /// exactly - same metrics, same order, same direction. A drift here would let a match launch
        /// successfully while being scored differently than the server scores it, which is a
        /// correctness defect even though nothing here fails loudly on its own. Returns a
        /// human-readable description of the first mismatch found, or <c>null</c> if they agree.
        /// </summary>
        private static string FindComparisonKeyMismatch(
            CompetitiveRuleset ruleset, System.Collections.Generic.IReadOnlyList<ComparisonKeySummaryDto> serverComparisonKeys)
        {
            System.Collections.Generic.IReadOnlyList<ComparisonKey> localComparisonKeys = ruleset.ComparisonKeys;
            int serverCount = serverComparisonKeys?.Count ?? 0;

            if (serverCount != localComparisonKeys.Count)
            {
                return $"the server froze {serverCount} comparison key(s) but this build's ruleset "
                    + $"has {localComparisonKeys.Count}";
            }

            for (int i = 0; i < serverCount; i++)
            {
                ComparisonKeySummaryDto serverKey = serverComparisonKeys[i];
                ComparisonKey localKey = localComparisonKeys[i];

                if (!Enum.TryParse(serverKey.Metric, ignoreCase: false, out AttemptMetric serverMetric)
                    || serverMetric != localKey.Metric
                    || !Enum.TryParse(serverKey.Direction, ignoreCase: false, out MetricDirection serverDirection)
                    || serverDirection != localKey.Direction)
                {
                    return $"comparison key {i} is '{serverKey.Metric} {serverKey.Direction}' on the "
                        + $"server but '{localKey.Metric} {localKey.Direction}' locally";
                }
            }

            return null;
        }

        private static string FindUnsupportedMetric(System.Collections.Generic.IReadOnlyList<string> requiredMetrics)
        {
            if (requiredMetrics == null)
            {
                return null;
            }

            foreach (string metricName in requiredMetrics)
            {
                if (!Enum.TryParse(metricName, ignoreCase: false, out AttemptMetric _))
                {
                    return metricName;
                }
            }

            return null;
        }
    }
}
