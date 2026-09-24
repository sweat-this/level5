using System;
using System.Collections.Generic;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2AttemptMappingTests
    {
        private const string RulesetIdValue = "most-points";

        [SetUp]
        public void SetUp()
        {
            GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
            LevelDefinition level = TestDefinitions.Level(4, objectName: "level_04_park", sceneDescriptor: "day");
            MatchCatalogs.Override(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

            CompetitiveRuleset ruleset = new CompetitiveRuleset(
                new RulesetId(RulesetIdValue),
                version: 2,
                modeId: GameModeId.TotalPoints,
                capabilities: VersusCapability.Asynchronous,
                comparisonKeys: new[] { ComparisonKey.Highest(AttemptMetric.Score) },
                minimumCompatibleVersion: 1,
                displayName: "Most Points");
            VersusCatalogs.Override(new CompetitiveRulesetCatalog(new[] { ruleset }));
        }

        [TearDown]
        public void TearDown()
        {
            MatchCatalogs.Reset();
            VersusCatalogs.Reset();
        }

        private static AttemptDescriptorDto Descriptor(
            int rulesetVersion = 2,
            int protocolVersion = RemoteAttemptDescriptorMapper.SupportedCompetitionProtocolVersion,
            string rulesetId = RulesetIdValue,
            List<string> requiredMetrics = null,
            List<ComparisonKeySummaryDto> comparisonKeys = null)
        {
            return new AttemptDescriptorDto
            {
                SeriesId = Guid.NewGuid(),
                AttemptId = Guid.NewGuid(),
                GameNumber = 1,
                PlayerId = Guid.NewGuid(),
                CompetitionProtocolVersion = protocolVersion,
                RulesetId = rulesetId,
                RulesetVersion = rulesetVersion,
                MinimumCompatibleVersion = 1,
                ModeId = rulesetId,
                InformationPolicy = "SealedAttempt",
                TotalGames = 3,
                GamesToWin = 2,
                ComparisonKeys = comparisonKeys ?? new List<ComparisonKeySummaryDto>
                {
                    new ComparisonKeySummaryDto { Metric = "Score", Direction = "HigherWins" }
                },
                RequiredResultMetrics = requiredMetrics ?? new List<string> { "Score" }
            };
        }

        [Test]
        public void AValidDescriptorProducesAPlayableConfiguration()
        {
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(), levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
            Assert.That(result.Configuration.LevelId, Is.EqualTo(4));
            Assert.That(result.Context.RulesetId, Is.EqualTo(RulesetIdValue));
            Assert.That(result.Context.RequiredResultMetrics, Is.EquivalentTo(new[] { "Score" }));
        }

        [Test]
        public void TheContextComesFromTheDescriptorNotCallerState()
        {
            AttemptDescriptorDto descriptor = Descriptor();
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                descriptor, levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Context.SeriesId, Is.EqualTo(descriptor.SeriesId));
            Assert.That(result.Context.AttemptId, Is.EqualTo(descriptor.AttemptId));
            Assert.That(result.Context.GameNumber, Is.EqualTo(descriptor.GameNumber));
        }

        [Test]
        public void AnUnsupportedProtocolVersionFailsBeforeAnyConfigurationIsProduced()
        {
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(protocolVersion: 99),
                levelId: 4,
                participantId: new ParticipantId("p1"),
                character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Configuration, Is.Null);
            Assert.That(result.Error, Does.Contain("Protocol"));
        }

        [Test]
        public void AnUnknownRulesetFails()
        {
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(rulesetId: "no-such-ruleset"),
                levelId: 4,
                participantId: new ParticipantId("p1"),
                character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("no-such-ruleset"));
        }

        [Test]
        public void AnUnplayableRulesetVersionFails()
        {
            // The catalog's ruleset above is version 2 with minimumCompatibleVersion 1; version 3
            // does not exist yet on this build.
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(rulesetVersion: 3),
                levelId: 4,
                participantId: new ParticipantId("p1"),
                character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("version"));
        }

        [Test]
        public void AnUnrecognizedRequiredMetricFails()
        {
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(requiredMetrics: new List<string> { "NotARealMetric" }),
                levelId: 4,
                participantId: new ParticipantId("p1"),
                character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("NotARealMetric"));
        }

        [Test]
        public void AnUnknownLevelFailsThroughTheOrdinaryBuilderValidation()
        {
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(), levelId: 999, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void ResultMetricsAreBuiltOnlyForTheRequiredNames()
        {
            AttemptResult.Builder builder = new AttemptResult.Builder(new RulesetId(RulesetIdValue), 2);
            builder.Set(AttemptMetric.Score, 42f);
            builder.Set(AttemptMetric.BonusPoints, 7f);
            AttemptResult result = builder.Build();

            IReadOnlyDictionary<string, double> metrics =
                RemoteAttemptResultBuilder.BuildMetrics(result, new[] { "Score" });

            Assert.That(metrics, Has.Count.EqualTo(1));
            Assert.That(metrics["Score"], Is.EqualTo(42.0));
        }

        [Test]
        public void AServerComparisonKeyOrderThatDiffersFromTheLocalRulesetFailsToMap()
        {
            // This build's catalog ruleset (SetUp above) compares only on Score. A server that
            // froze an extra tie-break under the exact same RulesetId+RulesetVersion would be
            // silently able to score a game differently than this build ever could - that must
            // refuse to launch, not launch with mismatched rules.
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(comparisonKeys: new List<ComparisonKeySummaryDto>
                {
                    new ComparisonKeySummaryDto { Metric = "Score", Direction = "HigherWins" },
                    new ComparisonKeySummaryDto { Metric = "Accuracy", Direction = "HigherWins" }
                }),
                levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Configuration, Is.Null);
        }

        [Test]
        public void AServerComparisonKeyDirectionThatDiffersFromTheLocalRulesetFailsToMap()
        {
            // Same metric, opposite direction: "LowerWins" here would hand the round to whoever
            // scored less, the inverse of what this build's own ruleset (HigherWins) decides.
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(comparisonKeys: new List<ComparisonKeySummaryDto>
                {
                    new ComparisonKeySummaryDto { Metric = "Score", Direction = "LowerWins" }
                }),
                levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Configuration, Is.Null);
        }

        [Test]
        public void AMatchingServerComparisonKeyOrderMapsSuccessfully()
        {
            // The positive counterpart to the two mismatch tests above: agreement is not itself
            // an error condition.
            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                Descriptor(comparisonKeys: new List<ComparisonKeySummaryDto>
                {
                    new ComparisonKeySummaryDto { Metric = "Score", Direction = "HigherWins" }
                }),
                levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
        }

        /// <summary>
        /// Uses Unity's actual shipped <see cref="DefaultCompetitiveRulesets"/> "most-points" entry
        /// (not a hand-rolled stand-in that merely reuses the same id) against the exact descriptor
        /// shape Backend V2's real <c>StaticRulesetCatalog</c> freezes for it
        /// (<see cref="BackendV2Fixtures.AttemptDescriptorProductionMostPoints"/>'s three ordered
        /// comparison keys), so a change to either side's real "most-points" definition that
        /// desynchronizes them is caught here instead of only by two test doubles that agree with
        /// each other but not with production.
        /// </summary>
        [Test]
        public void TheProductionMostPointsRulesetMapsTheProductionDescriptorSuccessfully()
        {
            VersusCatalogs.Override(new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll()));

            AttemptDescriptorDto descriptor = new AttemptDescriptorDto
            {
                SeriesId = Guid.NewGuid(),
                AttemptId = Guid.NewGuid(),
                GameNumber = 1,
                PlayerId = Guid.NewGuid(),
                CompetitionProtocolVersion = RemoteAttemptDescriptorMapper.SupportedCompetitionProtocolVersion,
                RulesetId = RulesetIdValue,
                RulesetVersion = 1,
                MinimumCompatibleVersion = 1,
                ModeId = "mode-most-points",
                InformationPolicy = "SealedAttempt",
                TotalGames = 3,
                GamesToWin = 2,
                ComparisonKeys = new List<ComparisonKeySummaryDto>
                {
                    new ComparisonKeySummaryDto { Metric = "Score", Direction = "HigherWins" },
                    new ComparisonKeySummaryDto { Metric = "Accuracy", Direction = "HigherWins" },
                    new ComparisonKeySummaryDto { Metric = "ShotsAttempted", Direction = "LowerWins" }
                },
                RequiredResultMetrics = new List<string> { "Score", "Accuracy", "ShotsAttempted" }
            };

            RemoteAttemptMapResult result = RemoteAttemptDescriptorMapper.Map(
                descriptor, levelId: 4, participantId: new ParticipantId("p1"), character: CharacterSelection.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
            Assert.That(result.Context.RequiredResultMetrics, Is.EquivalentTo(new[] { "Score", "Accuracy", "ShotsAttempted" }));
        }
    }
}
