using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="PendingMatchResultStore"/>: the durable local queue keyed by
    /// <c>(OwnerPlayerId, ClientResultId)</c>.
    /// </summary>
    public class Level5BackendV2PendingMatchResultStoreTests
    {
        private const string FileName = "backendv2_pending_match_results.json";

        [TearDown]
        public void TearDown()
        {
            PendingMatchResultStore.Clear();
        }

        private static SubmitMatchResultDto Request(Guid clientResultId, int totalPoints = 100)
        {
            return new SubmitMatchResultDto(
                clientResultId, modeId: 3, levelId: 7, characterId: "12", clientVersion: "1.4.2",
                platform: "Handheld",
                metrics: new Dictionary<string, double> { [MatchResultMetric.TotalPoints.ToString()] = totalPoints },
                modifiers: new MatchResultModifiersDto());
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        [Test]
        public void NothingIsQueuedByDefault()
        {
            Assert.That(PendingMatchResultStore.GetRetryable(Guid.NewGuid()), Is.Empty);
        }

        [Test]
        public void OneEntryRoundTrips()
        {
            Guid owner = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();

            PendingMatchResultEnqueueResult outcome = PendingMatchResultStore.Enqueue(owner, Request(clientResultId));

            Assert.That(outcome, Is.EqualTo(PendingMatchResultEnqueueResult.Added));
            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);
            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].OwnerPlayerId, Is.EqualTo(owner));
            Assert.That(retryable[0].Request.ClientResultId, Is.EqualTo(clientResultId));
            Assert.That(retryable[0].Request.Metrics["TotalPoints"], Is.EqualTo(100));
        }

        [Test]
        public void MultipleEntriesForTheSameOwnerAllRoundTrip()
        {
            Guid owner = Guid.NewGuid();
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();

            PendingMatchResultStore.Enqueue(owner, Request(first));
            PendingMatchResultStore.Enqueue(owner, Request(second));

            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);
            Assert.That(retryable, Has.Count.EqualTo(2));
            CollectionAssert.AreEquivalent(
                new[] { first, second }, retryable.ConvertAll(entry => entry.Request.ClientResultId));
        }

        [Test]
        public void EnqueueingTheExactSameRequestTwiceDoesNotDuplicate()
        {
            Guid owner = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();

            PendingMatchResultEnqueueResult first = PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
            PendingMatchResultEnqueueResult second = PendingMatchResultStore.Enqueue(owner, Request(clientResultId));

            Assert.That(first, Is.EqualTo(PendingMatchResultEnqueueResult.Added));
            Assert.That(second, Is.EqualTo(PendingMatchResultEnqueueResult.AlreadyQueued));
            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheSameClientResultIdForDifferentOwnersStaysDistinct()
        {
            Guid ownerA = Guid.NewGuid();
            Guid ownerB = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();

            PendingMatchResultStore.Enqueue(ownerA, Request(clientResultId));
            PendingMatchResultStore.Enqueue(ownerB, Request(clientResultId));

            Assert.That(PendingMatchResultStore.GetRetryable(ownerA), Has.Count.EqualTo(1));
            Assert.That(PendingMatchResultStore.GetRetryable(ownerB), Has.Count.EqualTo(1));
        }

        [Test]
        public void AConflictingPayloadUnderTheSameKeyIsRefusedAndLeavesTheExistingEntryUntouched()
        {
            Guid owner = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId, totalPoints: 100));

            LogAssert.Expect(LogType.Error, new Regex("PendingMatchResultStore refused to enqueue a match result"));
            PendingMatchResultEnqueueResult outcome =
                PendingMatchResultStore.Enqueue(owner, Request(clientResultId, totalPoints: 999));

            Assert.That(outcome, Is.EqualTo(PendingMatchResultEnqueueResult.ConflictingPayload));
            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);
            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].Request.Metrics["TotalPoints"], Is.EqualTo(100),
                "the original queued payload must never be silently overwritten");
        }

        [Test]
        public void RemovingOneEntryLeavesUnrelatedEntriesUntouched()
        {
            Guid owner = Guid.NewGuid();
            Guid keep = Guid.NewGuid();
            Guid removeThis = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(keep));
            PendingMatchResultStore.Enqueue(owner, Request(removeThis));

            bool removed = PendingMatchResultStore.Remove(owner, removeThis);

            Assert.That(removed, Is.True);
            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);
            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].Request.ClientResultId, Is.EqualTo(keep));
        }

        [Test]
        public void MarkingADefinitiveFailureExcludesTheEntryFromRetryableWithoutRemovingIt()
        {
            Guid owner = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));

            PendingMatchResultStore.MarkDefinitiveFailure(owner, clientResultId);

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty,
                "a definitive failure must not be retried automatically");
        }

        [Test]
        public void ACorruptedPrimaryFileFallsBackToTheAtomicFileBackupCopy()
        {
            Guid owner = Guid.NewGuid();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));

            // AtomicFile.WriteAllText always leaves a "<path>.bak" copy of the last good write.
            // Corrupting only the primary file here reproduces a torn/partial write and proves
            // PendingMatchResultStore inherits AtomicFile's primary/backup recovery, the same as
            // BackendV2SessionPersistenceStore and PendingMatchPersistenceStore already do.
            File.WriteAllText(GetPath(), "{ not valid json");

            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);

            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].Request.ClientResultId, Is.EqualTo(clientResultId));
        }
    }
}
