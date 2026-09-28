using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// End-to-end coverage for <see cref="ProgressionService.ApplyMatchResult"/> against a real,
/// throwaway SQLite database (<see cref="DBHelperTestHarness"/>, the same seam
/// <c>Level5LocalProfileDeletionTests</c> uses). This is the regression suite for retiring the JSON
/// character-progress projection: SQLite <c>CharacterProfile</c> plus
/// <c>ProgressionResultLedger</c> is now the whole authoritative path, and none of it may create or
/// modify the legacy <c>&lt;accountId&gt;-characters.json</c> file - see
/// docs/persistence-boundaries.md.
/// </summary>
public class ProgressionServiceApplyMatchResultTests
{
    private DBHelperTestHarness.Session session;
    private string accountId;
    private int originalUserId;
    private string originalUserName;

    [SetUp]
    public void SetUp()
    {
        session = DBHelperTestHarness.Create();

        // DBHelperTestHarness relies on DBConnector.Awake()'s own "instance = this" to publish the
        // harness's connector as the live singleton - the same singleton ProgressionService reads
        // via DBConnector.instance. Empirically (Unity 6000.5.7f1, EditMode batch, no Play Mode
        // running), that assignment does not reliably stick: DBConnector.instance still read back
        // null immediately after Create() returned, even though session.Connector/session.Helper are
        // themselves fully functional (their own Connection/transactions work). No prior test caught
        // this because every existing DBHelperTestHarness consumer (e.g.
        // Level5LocalProfileDeletionTests) calls session.Helper/session.Connector directly and never
        // reads the static singleton. ProgressionService.ApplyMatchResult, being production code,
        // reads DBConnector.instance - so this test asserts it explicitly rather than trusting Awake().
        DBConnector.instance = session.Connector;
        DBHelper.instance = session.Helper;

        originalUserId = GameOptions.userid;
        originalUserName = GameOptions.userName;
        accountId = "progression-apply-test-" + Guid.NewGuid().ToString("N");
        GameOptions.userid = 0;
        GameOptions.userName = accountId;
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.userid = originalUserId;
        GameOptions.userName = originalUserName;
        DBHelperTestHarness.Destroy(session);
        CharacterProgressStore.DeleteAccountFiles(accountId);
        PendingProgressionStore.DeleteAccountFiles(accountId);
    }

    private void SeedCharacterProfile(int charid, int experience)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO CharacterProfile (accountId, charid, playerName, objectName, experience, level) "
                + "VALUES (@accountId, @charid, 'name', 'object', @experience, 0)";
            cmd.Parameters.Add(new SqliteParameter("@accountId", accountId));
            cmd.Parameters.Add(new SqliteParameter("@charid", charid));
            cmd.Parameters.Add(new SqliteParameter("@experience", experience));
            cmd.ExecuteNonQuery();
        }
    }

    private int ReadExperience(int charid)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT experience FROM CharacterProfile WHERE accountId = @accountId AND charid = @charid";
            cmd.Parameters.Add(new SqliteParameter("@accountId", accountId));
            cmd.Parameters.Add(new SqliteParameter("@charid", charid));
            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? -1 : Convert.ToInt32(result);
        }
    }

    /// <summary>
    /// The ledger table is created lazily, inside the same transaction as the first apply attempt -
    /// a failed apply rolls that transaction back, undoing the table creation along with everything
    /// else, so a fresh database can still have no ProgressionResultLedger table at all after a
    /// failed application. Tolerating that (rather than requiring a prior successful apply) keeps
    /// this helper usable from tests that specifically exercise the failure path.
    /// </summary>
    private int CountLedgerRows(string resultId)
    {
        if (!TableExists("ProgressionResultLedger"))
        {
            return 0;
        }

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ProgressionResultLedger WHERE resultId = @resultId";
            cmd.Parameters.Add(new SqliteParameter("@resultId", resultId));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private bool TableExists(string tableName)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
            cmd.Parameters.Add(new SqliteParameter("@name", tableName));
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private void EnsureProgressionLedgerTable()
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS ProgressionResultLedger ("
                + "resultId TEXT PRIMARY KEY, accountId TEXT NOT NULL, characterId INTEGER NOT NULL, "
                + "experienceAfter INTEGER NOT NULL, levelAfter INTEGER NOT NULL, "
                + "projectionApplied INTEGER NOT NULL DEFAULT 0, appliedUtc TEXT NOT NULL)";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Seeds a ledger row directly, bypassing <c>ApplyProgressionResult</c>, so a test can
    /// simulate a row written before this issue (<c>projectionApplied = 0</c>) without a migration.</summary>
    private void SeedLedgerRow(string resultId, int characterId, int experienceAfter, int levelAfter, int projectionApplied)
    {
        EnsureProgressionLedgerTable();
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO ProgressionResultLedger "
                + "(resultId, accountId, characterId, experienceAfter, levelAfter, projectionApplied, appliedUtc) "
                + "VALUES (@resultId, @accountId, @characterId, @experienceAfter, @levelAfter, @projectionApplied, @now)";
            cmd.Parameters.Add(new SqliteParameter("@resultId", resultId));
            cmd.Parameters.Add(new SqliteParameter("@accountId", accountId));
            cmd.Parameters.Add(new SqliteParameter("@characterId", characterId));
            cmd.Parameters.Add(new SqliteParameter("@experienceAfter", experienceAfter));
            cmd.Parameters.Add(new SqliteParameter("@levelAfter", levelAfter));
            cmd.Parameters.Add(new SqliteParameter("@projectionApplied", projectionApplied));
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }
    }

    private int ReadProjectionApplied(string resultId)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT projectionApplied FROM ProgressionResultLedger WHERE resultId = @resultId";
            cmd.Parameters.Add(new SqliteParameter("@resultId", resultId));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    // ---- Authoritative application -------------------------------------------------------------

    [Test]
    public void ApplyingANewResultUpdatesSqliteAndRequiresNoJsonFile()
    {
        SeedCharacterProfile(charid: 5, experience: 100);
        string resultId = "apply-new-" + Guid.NewGuid().ToString("N");

        MatchProgressionResult result = new ProgressionService().ApplyMatchResult(resultId, characterId: 5, experienceGained: 50f);

        Assert.That(result.Applied, Is.True);
        Assert.That(result.Duplicate, Is.False);
        Assert.That(ReadExperience(5), Is.EqualTo(150));
        Assert.That(CountLedgerRows(resultId), Is.EqualTo(1));
        Assert.That(
            File.Exists(CharacterProgressStore.GetAccountProgressPath(accountId)),
            Is.False,
            "applying progression must not create the legacy JSON projection file");
    }

    // ---- Duplicate result ------------------------------------------------------------------------

    [Test]
    public void ApplyingTheSameResultTwiceIsAppliedThenDuplicateAndExperienceChangesOnce()
    {
        SeedCharacterProfile(charid: 6, experience: 0);
        string resultId = "apply-dup-" + Guid.NewGuid().ToString("N");
        ProgressionService service = new ProgressionService();

        MatchProgressionResult first = service.ApplyMatchResult(resultId, characterId: 6, experienceGained: 20f);
        MatchProgressionResult second = service.ApplyMatchResult(resultId, characterId: 6, experienceGained: 20f);

        Assert.That(first.Applied, Is.True);
        Assert.That(first.Duplicate, Is.False);
        Assert.That(second.Applied, Is.True);
        Assert.That(second.Duplicate, Is.True);
        Assert.That(ReadExperience(6), Is.EqualTo(20), "experience must change exactly once across both applications");
        Assert.That(File.Exists(CharacterProgressStore.GetAccountProgressPath(accountId)), Is.False);
    }

    // ---- Pending recovery ------------------------------------------------------------------------

    [Test]
    public void AFailedApplicationQueuesForPendingRecoveryAndLaterRepairAppliesExactlyOnce()
    {
        // No CharacterProfile row seeded for this character yet - ApplyProgressionResult's lookup
        // finds nothing and returns Failed, so ApplyMatchResult must queue instead of losing the
        // award.
        string resultId = "apply-pending-" + Guid.NewGuid().ToString("N");
        MatchProgressionResult result = new ProgressionService().ApplyMatchResult(resultId, characterId: 7, experienceGained: 30f);

        Assert.That(result.Applied, Is.True, "a queued-but-not-yet-applied award still reports Applied (queued)");
        Assert.That(PendingProgressionStore.GetPending(accountId).Exists(p => p.resultId == resultId), Is.True);
        Assert.That(CountLedgerRows(resultId), Is.EqualTo(0));

        SeedCharacterProfile(charid: 7, experience: 0);
        bool repaired = new ProgressionService().RepairPendingProgression();

        Assert.That(repaired, Is.True);
        Assert.That(ReadExperience(7), Is.EqualTo(30));
        Assert.That(PendingProgressionStore.GetPending(accountId).Exists(p => p.resultId == resultId), Is.False);
        Assert.That(
            File.Exists(CharacterProgressStore.GetAccountProgressPath(accountId)),
            Is.False,
            "recovery must apply straight to SQLite, never through a JSON projection");
    }

    // ---- No new JSON writes ----------------------------------------------------------------------

    [Test]
    public void PreExistingLegacyJsonFileIsNotModifiedByApplyingProgression()
    {
        SeedCharacterProfile(charid: 8, experience: 0);
        CharacterProgressStore.Save(new CharacterProgressSave
        {
            userId = accountId,
            characters = new List<PlayerCharacterProgress>
            {
                new PlayerCharacterProgress
                {
                    characterId = "legacy-8",
                    legacyPlayerId = 8,
                    unlocked = true,
                    experience = 999,
                    level = 9
                }
            }
        });
        string path = CharacterProgressStore.GetAccountProgressPath(accountId);
        string beforeContents = File.ReadAllText(path);

        string resultId = "apply-legacy-json-" + Guid.NewGuid().ToString("N");
        new ProgressionService().ApplyMatchResult(resultId, characterId: 8, experienceGained: 15f);

        Assert.That(
            File.ReadAllText(path),
            Is.EqualTo(beforeContents),
            "a pre-existing legacy JSON file must be left untouched by progression application");
    }

    // ---- Ledger compatibility --------------------------------------------------------------------

    [Test]
    public void DuplicateDetectionWorksAgainstAHistoricalRowWithProjectionAppliedZero()
    {
        string resultId = "legacy-ledger-" + Guid.NewGuid().ToString("N");
        SeedLedgerRow(resultId, characterId: 9, experienceAfter: 77, levelAfter: 3, projectionApplied: 0);

        ProgressionApplyStatus status = session.Connector.ApplyProgressionResult(
            resultId, accountId, experienceGained: 40f, characterId: 9, out ProgressionSnapshot snapshot);

        Assert.That(status, Is.EqualTo(ProgressionApplyStatus.Duplicate));
        Assert.That(snapshot.Experience, Is.EqualTo(77));
        Assert.That(snapshot.Level, Is.EqualTo(3));
    }

    [Test]
    public void ANewlyInsertedLedgerRowWritesProjectionAppliedAsOne()
    {
        SeedCharacterProfile(charid: 10, experience: 0);
        string resultId = "new-ledger-" + Guid.NewGuid().ToString("N");

        ProgressionApplyStatus status = session.Connector.ApplyProgressionResult(
            resultId, accountId, experienceGained: 5f, characterId: 10, out ProgressionSnapshot snapshot);

        Assert.That(status, Is.EqualTo(ProgressionApplyStatus.Applied));
        Assert.That(ReadProjectionApplied(resultId), Is.EqualTo(1));
    }
}
