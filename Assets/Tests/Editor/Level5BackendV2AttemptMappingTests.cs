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
            List<string> requiredMetrics = null)
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
                ComparisonKeys = new List<ComparisonKeySummaryDto>
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
    }
}
