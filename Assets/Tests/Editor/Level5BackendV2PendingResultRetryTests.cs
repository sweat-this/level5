using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="PendingRemoteAttemptResult"/> and <see cref="RemoteAttemptResultSubmitter.TryRetryPending"/>.
    ///
    /// Only the claim/state-transition surface is exercised here, not the network round trip inside
    /// the coroutine <c>TryRetryPending</c> starts - the same boundary
    /// <see cref="Level5BackendV2RemoteAttemptSubmissionTests"/> already draws, since
    /// <c>MonoBehaviour.StartCoroutine</c> does not advance in EditMode.
    /// </summary>
    public class Level5BackendV2PendingResultRetryTests
    {
        [TearDown]
        public void TearDown()
        {
            PendingRemoteAttemptResult.Clear();
        }

        private static RemoteAttemptContext Context(Guid attemptId)
        {
            return new RemoteAttemptContext(
                seriesId: Guid.NewGuid(),
                gameNumber: 1,
                attemptId: attemptId,
                playerId: Guid.NewGuid(),
                rulesetId: "most-points",
                rulesetVersion: 1,
                competitionProtocolVersion: 1,
                comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
                requiredResultMetrics: new[] { "Score" });
        }

        [Test]
        public void NothingIsPendingByDefault()
        {
            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);
        }

        [Test]
        public void StashKeepsTheExactMetricsDictionaryGiven()
        {
            RemoteAttemptContext context = Context(Guid.NewGuid());
            IReadOnlyDictionary<string, double> metrics = new Dictionary<string, double> { ["Score"] = 42.0 };

            PendingRemoteAttemptResult.Stash(context, metrics);

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.True);
            Assert.That(PendingRemoteAttemptResult.Context, Is.SameAs(context));
            Assert.That(PendingRemoteAttemptResult.Metrics, Is.SameAs(metrics),
                "a retry must resend the exact payload already built, never a rebuilt one");
        }

        [Test]
        public void ClearRemovesBothContextAndMetrics()
        {
            PendingRemoteAttemptResult.Stash(Context(Guid.NewGuid()), new Dictionary<string, double>());

            PendingRemoteAttemptResult.Clear();

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);
            Assert.That(PendingRemoteAttemptResult.Metrics, Is.Null);
        }

        [Test]
        public void TryRetryPendingReturnsFalseWhenNothingIsPending()
        {
            // Guard-clause path only: returns before anything would start a coroutine, so this is
            // safe to exercise directly in EditMode.
            Assert.That(RemoteAttemptResultSubmitter.TryRetryPending(), Is.False);
        }

        [Test]
        public void TryRetryPendingReturnsFalseWhileTheSameAttemptIsAlreadyClaimed()
        {
            // Simulates GameRules' own match-end retry loop already having an in-flight submission
            // for this attempt when the player also taps "retry" in the correspondence UI - the
            // shared TryClaim guard must refuse the second one. Claimed directly here (rather than by
            // calling TrySubmit/TryRetryPending, which would start a real coroutine - not safe to
            // drive in EditMode; see the class doc comment).
            Guid attemptId = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(attemptId), new Dictionary<string, double> { ["Score"] = 10.0 });
            RemoteAttemptResultSubmitter.TryClaim(attemptId);

            try
            {
                Assert.That(RemoteAttemptResultSubmitter.TryRetryPending(), Is.False);
            }
            finally
            {
                RemoteAttemptResultSubmitter.Release(attemptId);
            }
        }
    }
}
