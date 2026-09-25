using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="PendingRemoteAttemptResult"/> (the durable, owner-scoped facade over
    /// <see cref="PendingRemoteAttemptResultStore"/>) and <see cref="RemoteAttemptResultSubmitter.TryRetryPending"/>.
    ///
    /// Only the claim/state-transition surface is exercised here, not the network round trip inside
    /// the coroutine <c>TryRetryPending</c> starts - the same boundary
    /// <see cref="Level5BackendV2RemoteAttemptSubmissionTests"/> already draws, since
    /// <c>MonoBehaviour.StartCoroutine</c> does not advance in EditMode. Store-level round-tripping
    /// (persistence, conflict handling, backup recovery) has its own focused coverage in
    /// <see cref="Level5BackendV2PendingRemoteAttemptResultStoreTests"/>; this file covers the facade's
    /// current-player scoping and the retry claim guard built on top of it.
    /// </summary>
    public class Level5BackendV2PendingResultRetryTests
    {
        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
            PendingRemoteAttemptResultStore.Clear();
        }

        private static RemoteAttemptContext Context(Guid playerId, Guid attemptId)
        {
            return new RemoteAttemptContext(
                seriesId: Guid.NewGuid(),
                gameNumber: 1,
                attemptId: attemptId,
                playerId: playerId,
                rulesetId: "most-points",
                rulesetVersion: 1,
                competitionProtocolVersion: 1,
                comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
                requiredResultMetrics: new[] { "Score" });
        }

        private static Guid SignIn()
        {
            Guid playerId = Guid.NewGuid();
            SignInAs(playerId);
            return playerId;
        }

        private static void SignInAs(Guid playerId)
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        // --- current-player scoping -------------------------------------------------------------

        [Test]
        public void NothingIsPendingByDefault()
        {
            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);
        }

        [Test]
        public void NothingIsPendingWithNoAuthenticatedSessionEvenIfAnEntryIsPersisted()
        {
            Guid owner = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, Guid.NewGuid()), new Dictionary<string, double> { ["Score"] = 1 });

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);
            Assert.That(PendingRemoteAttemptResult.Context, Is.Null);
            Assert.That(PendingRemoteAttemptResult.Metrics, Is.Null);
        }

        [Test]
        public void AStashedResultIsVisibleToItsOwningPlayerAndRoundTripsExactly()
        {
            Guid owner = SignIn();
            Guid attemptId = Guid.NewGuid();
            RemoteAttemptContext context = Context(owner, attemptId);
            Dictionary<string, double> metrics = new Dictionary<string, double> { ["Score"] = 42.0 };

            PendingRemoteAttemptResultEnqueueResult outcome = PendingRemoteAttemptResult.Stash(context, metrics);

            Assert.That(outcome, Is.EqualTo(PendingRemoteAttemptResultEnqueueResult.Added));
            Assert.That(PendingRemoteAttemptResult.HasPending, Is.True);
            RemoteAttemptContext readBack = PendingRemoteAttemptResult.Context;
            Assert.That(readBack.SeriesId, Is.EqualTo(context.SeriesId));
            Assert.That(readBack.GameNumber, Is.EqualTo(context.GameNumber));
            Assert.That(readBack.AttemptId, Is.EqualTo(context.AttemptId));
            Assert.That(readBack.PlayerId, Is.EqualTo(context.PlayerId));
            Assert.That(readBack.RulesetId, Is.EqualTo(context.RulesetId));
            Assert.That(readBack.RequiredResultMetrics, Is.EquivalentTo(context.RequiredResultMetrics));
            Assert.That(PendingRemoteAttemptResult.Metrics["Score"], Is.EqualTo(42.0),
                "a retry must resend the exact payload already built, never a rebuilt one");
        }

        [Test]
        public void AStashedResultIsInvisibleToADifferentSignedInPlayer()
        {
            Guid owner = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, Guid.NewGuid()), new Dictionary<string, double> { ["Score"] = 1 });

            SignIn(); // a different player signs in on this device

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False,
                "player B must never see player A's pending result just by being the one signed in");
            Assert.That(PendingRemoteAttemptResult.Context, Is.Null);
            Assert.That(PendingRemoteAttemptResult.Metrics, Is.Null);
        }

        [Test]
        public void SigningBackInAsTheOwningPlayerRestoresVisibility()
        {
            Guid owner = SignIn();
            PendingRemoteAttemptResult.Stash(
                Context(owner, Guid.NewGuid()), new Dictionary<string, double> { ["Score"] = 1 });

            SignIn(); // a different player signs in
            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);

            SignInAs(owner);
            Assert.That(PendingRemoteAttemptResult.HasPending, Is.True,
                "switching back to the owning player must restore visibility of their pending result");
        }

        [Test]
        public void ClearRemovesTheEntryForItsOwnerAndAttemptOnly()
        {
            Guid owner = SignIn();
            Guid attemptId = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, attemptId), new Dictionary<string, double> { ["Score"] = 1 });

            PendingRemoteAttemptResult.Clear(owner, attemptId);

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.False);
            Assert.That(PendingRemoteAttemptResult.Metrics, Is.Null);
        }

        [Test]
        public void ClearingADifferentOwnersEntryLeavesTheCurrentPlayersPendingResultUntouched()
        {
            Guid otherOwner = Guid.NewGuid();
            Guid otherAttemptId = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(otherOwner, otherAttemptId), new Dictionary<string, double> { ["Score"] = 1 });

            Guid owner = SignIn();
            Guid attemptId = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, attemptId), new Dictionary<string, double> { ["Score"] = 2 });

            PendingRemoteAttemptResult.Clear(otherOwner, otherAttemptId);

            Assert.That(PendingRemoteAttemptResult.HasPending, Is.True,
                "clearing an unrelated (owner, attemptId) must never touch a different pending entry");
        }

        // --- TryRetryPending: claim guard ---------------------------------------------------------

        [Test]
        public void TryRetryPendingReturnsFalseWhenNothingIsPending()
        {
            SignIn();

            // Guard-clause path only: returns before anything would start a coroutine, so this is
            // safe to exercise directly in EditMode.
            Assert.That(RemoteAttemptResultSubmitter.TryRetryPending(), Is.False);
        }

        [Test]
        public void TryRetryPendingReturnsFalseWhenThePendingResultBelongsToADifferentPlayer()
        {
            Guid owner = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, Guid.NewGuid()), new Dictionary<string, double> { ["Score"] = 1 });

            SignIn(); // a different player is signed in

            Assert.That(RemoteAttemptResultSubmitter.TryRetryPending(), Is.False,
                "a player must never be able to retry another player's pending result");
        }

        [Test]
        public void TryRetryPendingReturnsFalseWhileTheSameAttemptIsAlreadyClaimed()
        {
            // Simulates GameRules' own match-end retry loop already having an in-flight submission
            // for this attempt when the player also taps "retry" in the correspondence UI - the
            // shared TryClaim guard must refuse the second one. Claimed directly here (rather than by
            // calling TrySubmit/TryRetryPending, which would start a real coroutine - not safe to
            // drive in EditMode; see the class doc comment).
            Guid owner = SignIn();
            Guid attemptId = Guid.NewGuid();
            PendingRemoteAttemptResult.Stash(
                Context(owner, attemptId), new Dictionary<string, double> { ["Score"] = 10.0 });
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
