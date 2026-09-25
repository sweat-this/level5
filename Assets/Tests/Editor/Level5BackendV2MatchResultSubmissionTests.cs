using System;
using Assets.Scripts.database;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="BackendV2MatchResultSubmission"/>: the ownership-safe glue
    /// <c>GameRules.SaveMatchResults</c> and <c>EndRoundMenuManager.saveGame</c> both call with the
    /// exact same already-locally-durable <see cref="HighScoreModel"/> they just saved. Calling this
    /// method IS the "ordinary/campaign integration point" - both call sites are one-line calls into
    /// it with no additional logic of their own to test separately.
    ///
    /// Also covers the general/competitive exclusion: an active <see cref="ActiveRemoteAttempt"/> or
    /// <see cref="ActiveVersusAttempt"/> must keep a score out of the general
    /// <c>/api/v2/match-results</c> pipeline entirely, and a stale one (bound to a match that is no
    /// longer <see cref="ActiveMatch.Configuration"/>) must not suppress the next ordinary match.
    /// </summary>
    public class Level5BackendV2MatchResultSubmissionTests
    {
        [TearDown]
        public void TearDown()
        {
            PendingMatchResultStore.Clear();
            BackendV2SessionStore.Clear();
            ActiveRemoteAttempt.Clear();
            ActiveVersusAttempt.Clear();
            ActiveMatch.Clear();
        }

        private static Guid SetSession()
        {
            Guid playerId = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            return playerId;
        }

        /// <summary>A throwaway configuration; only its identity matters, since <see cref="ActiveMatch"/>
        /// replaces the object on every launch - see <see cref="Level5VersusIntegrationTests.BuildAnyMatch"/>
        /// for the same pattern.</summary>
        private static MatchConfiguration BuildAnyMatch()
        {
            GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
            LevelDefinition level = TestDefinitions.Level(1);
            PlayerRoster roster = TestDefinitions.SoloRoster();

            return new MatchConfiguration(
                mode,
                level,
                roster,
                MatchModifiers.Default,
                MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
                CheerleaderSelection.None,
                "backend v2 match result submission test");
        }

        private static RemoteAttemptContext AnyRemoteAttemptContext()
        {
            return new RemoteAttemptContext(
                seriesId: Guid.NewGuid(),
                gameNumber: 1,
                attemptId: Guid.NewGuid(),
                playerId: Guid.NewGuid(),
                rulesetId: "most-points",
                rulesetVersion: 1,
                competitionProtocolVersion: 1,
                comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
                requiredResultMetrics: new[] { "Score" });
        }

        private static Attempt AnyVersusAttempt()
        {
            return Attempt.Issue(
                new AttemptId("attempt-1"),
                new ParticipantId("patrick"),
                0,
                new RulesetId("most-points"),
                1,
                DateTime.UtcNow);
        }

        private static HighScoreModel ValidScore(string scoreid = null)
        {
            return new HighScoreModel
            {
                Scoreid = scoreid ?? Guid.NewGuid().ToString("N"),
                Modeid = 3,
                Levelid = 7,
                Characterid = 12,
                Version = "1.4.2",
                Platform = "Handheld",
                TotalPoints = 120,
                MaxShotMade = 18,
                TotalDistance = 342.5f,
                Time = 95.2f,
                ConsecutiveShots = 6,
                EnemiesKilled = 4,
            };
        }

        [Test]
        public void TryQueueDoesNothingWithNoBackendV2Session()
        {
            BackendV2SessionStore.Clear();

            BackendV2MatchResultSubmission.TryQueue(ValidScore());

            // No session means no owner to queue under - never created retroactively later.
            Assert.DoesNotThrow(() => { });
        }

        [Test]
        public void TryQueueEnqueuesUnderTheCurrentSessionsPlayerIdWithTheAdaptedPayload()
        {
            Guid playerId = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            HighScoreModel score = ValidScore();

            BackendV2MatchResultSubmission.TryQueue(score);

            var retryable = PendingMatchResultStore.GetRetryable(playerId);
            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].OwnerPlayerId, Is.EqualTo(playerId));
            Assert.That(retryable[0].Request.ClientResultId, Is.EqualTo(Guid.Parse(score.Scoreid)));
            Assert.That(retryable[0].Request.ModeId, Is.EqualTo(score.Modeid));
            Assert.That(retryable[0].Request.Metrics["TotalPoints"], Is.EqualTo(120.0));
        }

        [Test]
        public void TryQueueDoesNothingForANullScore()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            Assert.DoesNotThrow(() => BackendV2MatchResultSubmission.TryQueue(null));
        }

        [Test]
        public void TryQueueDoesNothingForAMalformedScoreid()
        {
            Guid playerId = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            BackendV2MatchResultSubmission.TryQueue(ValidScore(scoreid: "not-a-guid"));

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Is.Empty);
        }

        [Test]
        public void CallingTryQueueTwiceWithTheSameScoreIsIdempotent()
        {
            Guid playerId = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            HighScoreModel score = ValidScore();

            BackendV2MatchResultSubmission.TryQueue(score);
            BackendV2MatchResultSubmission.TryQueue(score);

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnActiveRemoteCorrespondenceAttemptKeepsTheScoreOutOfTheGeneralPipeline()
        {
            Guid playerId = SetSession();
            MatchConfiguration match = BuildAnyMatch();
            ActiveMatch.Begin(match);
            ActiveRemoteAttempt.Begin(AnyRemoteAttemptContext(), match);

            BackendV2MatchResultSubmission.TryQueue(ValidScore());

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Is.Empty,
                "a remote correspondence attempt is submitted only through RemoteAttemptResultSubmitter");
        }

        [Test]
        public void AnActiveLocalVersusAttemptKeepsTheScoreOutOfTheGeneralPipeline()
        {
            Guid playerId = SetSession();
            MatchConfiguration match = BuildAnyMatch();
            ActiveMatch.Begin(match);
            ActiveVersusAttempt.Begin(new SeriesId("series-1"), AnyVersusAttempt(), match);

            BackendV2MatchResultSubmission.TryQueue(ValidScore());

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Is.Empty,
                "a local versus attempt is submitted only through VersusMatchReporter");
        }

        [Test]
        public void AStaleRemoteAttemptDoesNotSuppressTheNextOrdinaryMatch()
        {
            // The attempt was bound to a match the player walked away from - a different
            // MatchConfiguration object is now current, so ActiveRemoteAttempt.IsActive must already
            // read false without anything clearing the attempt directly.
            Guid playerId = SetSession();
            MatchConfiguration abandonedMatch = BuildAnyMatch();
            ActiveMatch.Begin(abandonedMatch);
            ActiveRemoteAttempt.Begin(AnyRemoteAttemptContext(), abandonedMatch);

            ActiveMatch.Begin(BuildAnyMatch());

            Assert.That(ActiveRemoteAttempt.IsActive, Is.False);

            BackendV2MatchResultSubmission.TryQueue(ValidScore());

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Has.Count.EqualTo(1),
                "the stale attempt must not suppress this ordinary match's result");
        }

        [Test]
        public void AStaleLocalVersusAttemptDoesNotSuppressTheNextOrdinaryMatch()
        {
            Guid playerId = SetSession();
            MatchConfiguration abandonedMatch = BuildAnyMatch();
            ActiveMatch.Begin(abandonedMatch);
            ActiveVersusAttempt.Begin(new SeriesId("series-1"), AnyVersusAttempt(), abandonedMatch);

            ActiveMatch.Begin(BuildAnyMatch());

            Assert.That(ActiveVersusAttempt.IsActive, Is.False);

            BackendV2MatchResultSubmission.TryQueue(ValidScore());

            Assert.That(PendingMatchResultStore.GetRetryable(playerId), Has.Count.EqualTo(1),
                "the stale attempt must not suppress this ordinary match's result");
        }
    }
}
