using System;
using System.Collections.Generic;
using System.IO;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="PendingRemoteAttemptResultStore"/>: the durable local store keyed by
    /// <c>(OwnerPlayerId, AttemptId)</c>, mirroring <see cref="Level5BackendV2PendingMatchResultStoreTests"/>
    /// for the separate general-match-result queue.
    /// </summary>
    public class Level5BackendV2PendingRemoteAttemptResultStoreTests
    {
        private const string FileName = "backendv2_pending_remote_attempt_results.json";

        [TearDown]
        public void TearDown()
        {
            PendingRemoteAttemptResultStore.Clear();
        }

        private static RemoteAttemptContext Context(Guid playerId, Guid attemptId, string rulesetId = "most-points")
        {
            return new RemoteAttemptContext(
                seriesId: Guid.NewGuid(),
                gameNumber: 2,
                attemptId: attemptId,
                playerId: playerId,
                rulesetId: rulesetId,
                rulesetVersion: 1,
                competitionProtocolVersion: 1,
                comparisonKeys: new[] { new ComparisonKeySummaryDto { Metric = "Score", Direction = "HigherWins" } },
                requiredResultMetrics: new[] { "Score", "Accuracy" });
        }

        private static Dictionary<string, double> Metrics(double score = 42.0)
        {
            return new Dictionary<string, double> { ["Score"] = score, ["Accuracy"] = 0.75 };
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        [Test]
        public void NothingIsPersistedByDefault()
        {
            Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(Guid.NewGuid(), out _), Is.False);
        }

        [Test]
        public void OneEntryRoundTripsExactly()
        {
            Guid owner = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();
            RemoteAttemptContext context = Context(owner, attemptId);
            Dictionary<string, double> metrics = Metrics();

            PendingRemoteAttemptResultEnqueueResult outcome =
                PendingRemoteAttemptResultStore.Enqueue(owner, context, metrics);

            Assert.That(outcome, Is.EqualTo(PendingRemoteAttemptResultEnqueueResult.Added));
            Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(owner, out PendingRemoteAttemptResultEntry entry), Is.True);
            Assert.That(entry.OwnerPlayerId, Is.EqualTo(owner));
            Assert.That(entry.Context.SeriesId, Is.EqualTo(context.SeriesId));
            Assert.That(entry.Context.GameNumber, Is.EqualTo(context.GameNumber));
            Assert.That(entry.Context.AttemptId, Is.EqualTo(attemptId));
            Assert.That(entry.Context.PlayerId, Is.EqualTo(owner));
            Assert.That(entry.Context.RulesetId, Is.EqualTo(context.RulesetId));
            Assert.That(entry.Context.RulesetVersion, Is.EqualTo(context.RulesetVersion));
            Assert.That(entry.Context.CompetitionProtocolVersion, Is.EqualTo(context.CompetitionProtocolVersion));
            Assert.That(entry.Context.ComparisonKeys, Has.Count.EqualTo(1));
            Assert.That(entry.Context.ComparisonKeys[0].Metric, Is.EqualTo("Score"));
            Assert.That(entry.Context.ComparisonKeys[0].Direction, Is.EqualTo("HigherWins"));
            Assert.That(entry.Context.RequiredResultMetrics, Is.EquivalentTo(new[] { "Score", "Accuracy" }));
            Assert.That(entry.Metrics["Score"], Is.EqualTo(42.0));
            Assert.That(entry.Metrics["Accuracy"], Is.EqualTo(0.75));
        }

        [Test]
        public void TheReconstructedContextConvertsBackToARemoteAttemptContextExactly()
        {
            Guid owner = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();
            RemoteAttemptContext context = Context(owner, attemptId);
            PendingRemoteAttemptResultStore.Enqueue(owner, context, Metrics());

            PendingRemoteAttemptResultStore.TryGetForOwner(owner, out PendingRemoteAttemptResultEntry entry);
            RemoteAttemptContext reconstructed = entry.Context.ToContext();

            Assert.That(reconstructed.SeriesId, Is.EqualTo(context.SeriesId));
            Assert.That(reconstructed.AttemptId, Is.EqualTo(context.AttemptId));
            Assert.That(reconstructed.PlayerId, Is.EqualTo(context.PlayerId));
            Assert.That(reconstructed.ComparisonKeys, Has.Count.EqualTo(1));
            Assert.That(reconstructed.RequiredResultMetrics, Is.EquivalentTo(context.RequiredResultMetrics));
        }

        [Test]
        public void EnqueueingTheExactSamePayloadTwiceIsIdempotent()
        {
            Guid owner = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();
            RemoteAttemptContext context = Context(owner, attemptId);

            PendingRemoteAttemptResultEnqueueResult first =
                PendingRemoteAttemptResultStore.Enqueue(owner, context, Metrics());
            PendingRemoteAttemptResultEnqueueResult second =
                PendingRemoteAttemptResultStore.Enqueue(owner, context, Metrics());

            Assert.That(first, Is.EqualTo(PendingRemoteAttemptResultEnqueueResult.Added));
            Assert.That(second, Is.EqualTo(PendingRemoteAttemptResultEnqueueResult.AlreadyQueued));
        }

        [Test]
        public void AConflictingPayloadUnderTheSameKeyIsRefusedAndLeavesTheExistingEntryUntouched()
        {
            Guid owner = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();
            RemoteAttemptContext context = Context(owner, attemptId);
            PendingRemoteAttemptResultStore.Enqueue(owner, context, Metrics(score: 42.0));

            // The store logs this refusal for real (it is the deliberate point of this test, not an
            // unexpected error), but Unity's strict test runner still fails a test on any unhandled
            // Debug.LogError.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^PendingRemoteAttemptResultStore refused to enqueue a remote attempt result:.*"));

            PendingRemoteAttemptResultEnqueueResult outcome =
                PendingRemoteAttemptResultStore.Enqueue(owner, context, Metrics(score: 999.0));

            Assert.That(outcome, Is.EqualTo(PendingRemoteAttemptResultEnqueueResult.ConflictingPayload));
            PendingRemoteAttemptResultStore.TryGetForOwner(owner, out PendingRemoteAttemptResultEntry entry);
            Assert.That(entry.Metrics["Score"], Is.EqualTo(42.0),
                "the original persisted payload must never be silently overwritten");
        }

        [Test]
        public void TheSameAttemptIdForDifferentOwnersStaysIsolated()
        {
            Guid ownerA = Guid.NewGuid();
            Guid ownerB = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();

            PendingRemoteAttemptResultStore.Enqueue(ownerA, Context(ownerA, attemptId), Metrics(score: 1));
            PendingRemoteAttemptResultStore.Enqueue(ownerB, Context(ownerB, attemptId), Metrics(score: 2));

            PendingRemoteAttemptResultStore.TryGetForOwner(ownerA, out PendingRemoteAttemptResultEntry entryA);
            PendingRemoteAttemptResultStore.TryGetForOwner(ownerB, out PendingRemoteAttemptResultEntry entryB);
            Assert.That(entryA.Metrics["Score"], Is.EqualTo(1));
            Assert.That(entryB.Metrics["Score"], Is.EqualTo(2));
        }

        [Test]
        public void RemovingOneEntryLeavesAnotherOwnersEntryUntouched()
        {
            Guid ownerA = Guid.NewGuid();
            Guid ownerB = Guid.NewGuid();
            Guid attemptIdA = Guid.NewGuid();
            Guid attemptIdB = Guid.NewGuid();
            PendingRemoteAttemptResultStore.Enqueue(ownerA, Context(ownerA, attemptIdA), Metrics());
            PendingRemoteAttemptResultStore.Enqueue(ownerB, Context(ownerB, attemptIdB), Metrics());

            bool removed = PendingRemoteAttemptResultStore.Remove(ownerA, attemptIdA);

            Assert.That(removed, Is.True);
            Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(ownerA, out _), Is.False);
            Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(ownerB, out _), Is.True,
                "removing one owner's entry must never affect a different owner's entry");
        }

        [Test]
        public void RemovingAnAttemptThatIsNotPersistedIsANoOp()
        {
            Assert.DoesNotThrow(() => PendingRemoteAttemptResultStore.Remove(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Test]
        public void ACorruptedPrimaryFileFallsBackToTheAtomicFileBackupCopy()
        {
            Guid owner = Guid.NewGuid();
            Guid attemptId = Guid.NewGuid();
            PendingRemoteAttemptResultStore.Enqueue(owner, Context(owner, attemptId), Metrics());

            // AtomicFile.WriteAllText always leaves a "<path>.bak" copy of the last good write.
            // Corrupting only the primary file here reproduces a torn/partial write and proves
            // PendingRemoteAttemptResultStore inherits AtomicFile's primary/backup recovery, the same
            // as PendingMatchResultStore already does.
            File.WriteAllText(GetPath(), "{ not valid json");

            bool found = PendingRemoteAttemptResultStore.TryGetForOwner(owner, out PendingRemoteAttemptResultEntry entry);

            Assert.That(found, Is.True);
            Assert.That(entry.Context.AttemptId, Is.EqualTo(attemptId));
        }

        [Test]
        public void ClearRemovesThePrimaryAndBackupFiles()
        {
            Guid owner = Guid.NewGuid();
            PendingRemoteAttemptResultStore.Enqueue(owner, Context(owner, Guid.NewGuid()), Metrics());
            Assert.That(File.Exists(GetPath()), Is.True);
            Assert.That(File.Exists(GetPath() + ".bak"), Is.True);

            PendingRemoteAttemptResultStore.Clear();

            Assert.That(File.Exists(GetPath()), Is.False);
            Assert.That(File.Exists(GetPath() + ".bak"), Is.False);
            Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(owner, out _), Is.False);
        }
    }
}
