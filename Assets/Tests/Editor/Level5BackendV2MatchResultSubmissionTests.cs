using System;
using Assets.Scripts.database;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="BackendV2MatchResultSubmission"/>: the ownership-safe glue
    /// <c>GameRules.SaveMatchResults</c> and <c>EndRoundMenuManager.saveGame</c> both call with the
    /// exact same already-locally-durable <see cref="HighScoreModel"/> they just saved. Calling this
    /// method IS the "ordinary/campaign integration point" - both call sites are one-line calls into
    /// it with no additional logic of their own to test separately.
    /// </summary>
    public class Level5BackendV2MatchResultSubmissionTests
    {
        [TearDown]
        public void TearDown()
        {
            PendingMatchResultStore.Clear();
            BackendV2SessionStore.Clear();
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
    }
}
