using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-111 section 12: <c>ProgressionResultStore</c> and <c>PendingProgressionStore</c> already
/// scoped their persistence catches narrowly to the read/write/JSON-parse call (see
/// <c>docs/ai/skills</c> notes and the AUD-111 catch inventory) and were deliberately left as-is.
/// These tests pin down the recovery behavior that narrowing depends on: a corrupt on-disk ledger
/// must not throw out to the caller, and must not permanently wedge the account - a fresh write
/// after the corruption succeeds and is readable again.
/// </summary>
public class ProgressionPersistenceRecoveryTests
{
    private string testUserId;
    private string resultLedgerPath;
    private string pendingLedgerPath;

    [SetUp]
    public void SetUp()
    {
        testUserId = "aud111-test-" + Guid.NewGuid().ToString("N");
        resultLedgerPath = Path.Combine(
            Application.persistentDataPath, "accounts", testUserId + "-progression-results.json");
        pendingLedgerPath = Path.Combine(
            Application.persistentDataPath, "accounts", testUserId + "-pending-progression.json");
    }

    [TearDown]
    public void TearDown()
    {
        DeleteIfExists(resultLedgerPath);
        DeleteIfExists(pendingLedgerPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ProgressionResultStore_HasApplied_RoundTripsThroughTryMarkApplied()
    {
        Assert.IsFalse(ProgressionResultStore.HasApplied(testUserId, "result-1"));

        bool marked = ProgressionResultStore.TryMarkApplied(testUserId, "result-1");

        Assert.IsTrue(marked, "TryMarkApplied should succeed against a fresh account ledger.");
        Assert.IsTrue(ProgressionResultStore.HasApplied(testUserId, "result-1"));
    }

    [Test]
    public void ProgressionResultStore_CorruptLedgerFile_RecoversWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(resultLedgerPath));
        File.WriteAllText(resultLedgerPath, "{ not valid json ]]]");

        bool marked = false;
        Assert.DoesNotThrow(
            () => marked = ProgressionResultStore.TryMarkApplied(testUserId, "result-after-corruption"),
            "A corrupt on-disk ledger is an expected recoverable persistence failure (AUD-111) and "
                + "must not throw out to the caller.");

        Assert.IsTrue(marked, "TryMarkApplied should recover to a fresh ledger and still succeed.");
        Assert.IsTrue(ProgressionResultStore.HasApplied(testUserId, "result-after-corruption"));
    }

    [Test]
    public void PendingProgressionStore_QueueAndGetPending_RoundTrips()
    {
        bool queued = PendingProgressionStore.Queue(testUserId, "pending-1", characterId: 3, experienceGained: 42f);

        Assert.IsTrue(queued, "Queue should succeed against a fresh account ledger.");
        Assert.AreEqual(1, PendingProgressionStore.GetPending(testUserId).Count);
    }

    [Test]
    public void PendingProgressionStore_CorruptLedgerFile_RecoversWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pendingLedgerPath));
        File.WriteAllText(pendingLedgerPath, "{ not valid json ]]]");

        bool queued = false;
        Assert.DoesNotThrow(
            () => queued = PendingProgressionStore.Queue(testUserId, "pending-after-corruption", 1, 10f),
            "A corrupt on-disk pending-progression file is an expected recoverable persistence "
                + "failure (AUD-111) and must not throw out to the caller.");

        Assert.IsTrue(queued, "Queue should recover to a fresh ledger and still succeed.");
        Assert.AreEqual(1, PendingProgressionStore.GetPending(testUserId).Count);
    }
}
