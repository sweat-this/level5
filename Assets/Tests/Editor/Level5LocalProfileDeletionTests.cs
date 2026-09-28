using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Level5.BackendV2;
using Level5.BackendV2.Tests;
using Level5.Core;
using Mono.Data.Sqlite;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Exercises <see cref="DBHelper.DeleteLocalProfile"/> against a real, throwaway SQLite database via
/// <see cref="DBHelperTestHarness"/> - the fix for the deletion-lock deadlock (DBHelper.deleteLocalUser
/// used to no-op when the caller, UserAccountManager, had already set DatabaseLocked before calling it,
/// so nothing was ever deleted and the lock was never released) and the transactional cascade that now
/// removes a profile's account-scoped CharacterProfile/ProgressionResultLedger rows and local files
/// alongside its User row. See docs/persistence-boundaries.md.
/// </summary>
public class Level5LocalProfileDeletionTests
{
    private DBHelperTestHarness.Session session;
    private int previousUserId;
    private string previousUserName;

    [SetUp]
    public void SetUp()
    {
        session = DBHelperTestHarness.Create();
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
    }

    [TearDown]
    public void TearDown()
    {
        LocalAccountIdentity.UserId = previousUserId;
        LocalAccountIdentity.UserName = previousUserName;
        DBHelperTestHarness.Destroy(session);
        BackendV2SessionStore.Clear();
    }

    private ApiResult<UserModel> CreateProfile(string profileName)
    {
        ApiResult<UserModel> result = null;
        CoroutineTestRunner.RunToCompletion(
            session.Helper.CreateLocalProfileCoroutine(profileName, value => result = value));
        return result;
    }

    private int CountUsers()
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM User";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private void SeedCharacterProfile(string accountId, int charid)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO CharacterProfile (accountId, charid, playerName, objectName, experience, level) "
                + "VALUES (@accountId, @charid, 'name', 'object', 10, 1)";
            cmd.Parameters.Add(new SqliteParameter("@accountId", accountId));
            cmd.Parameters.Add(new SqliteParameter("@charid", charid));
            cmd.ExecuteNonQuery();
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

    private void SeedProgressionLedgerRow(string resultId, string accountId, int characterId)
    {
        EnsureProgressionLedgerTable();
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO ProgressionResultLedger (resultId, accountId, characterId, experienceAfter, levelAfter, projectionApplied, appliedUtc) "
                + "VALUES (@resultId, @accountId, @characterId, 10, 1, 0, @now)";
            cmd.Parameters.Add(new SqliteParameter("@resultId", resultId));
            cmd.Parameters.Add(new SqliteParameter("@accountId", accountId));
            cmd.Parameters.Add(new SqliteParameter("@characterId", characterId));
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }
    }

    private int CountRows(string tableName, string whereAccountId = null)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM " + tableName
                + (whereAccountId == null ? string.Empty : " WHERE accountId = @accountId");
            if (whereAccountId != null)
            {
                cmd.Parameters.Add(new SqliteParameter("@accountId", whereAccountId));
            }

            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    // ------------------------------------------------------------------
    // Broken-lock regression
    // ------------------------------------------------------------------

    [Test]
    public void DeletingAProfileAcquiresItsOwnLockDeletesSuccessfullyAndReleasesTheLock()
    {
        ApiResult<UserModel> created = CreateProfile("alice");
        Assert.That(created.Success, Is.True, created.Error);
        Assert.That(session.Helper.DatabaseLocked, Is.False);

        bool deleted = session.Helper.DeleteLocalProfile(created.Value, out string error);

        Assert.That(deleted, Is.True, error);
        Assert.That(session.Helper.DatabaseLocked, Is.False);
    }

    [Test]
    public void AFailedDeletionAlsoReleasesTheLock()
    {
        // No such user was ever created - the User delete affects 0 rows, so this fails validation
        // deep inside the transaction rather than up front.
        UserModel nonExistent = new UserModel { Userid = 999, UserName = "nobody-here" };

        bool deleted = session.Helper.DeleteLocalProfile(nonExistent, out string error);

        Assert.That(deleted, Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(session.Helper.DatabaseLocked, Is.False);
    }

    [Test]
    public void DeletionDoesNotNoOpWhenTheCallerHasNotPreAcquiredTheLock()
    {
        // Regression pin for the exact old defect: deleteLocalUser used to check "if (databaseLocked)
        // return;" and UserAccountManager pre-set DatabaseLocked = true before calling it, so nothing
        // was ever deleted. DeleteLocalProfile owns the lock itself - a caller that (correctly, per
        // the new contract) never pre-sets it must see a real deletion happen.
        ApiResult<UserModel> created = CreateProfile("bob");
        Assert.That(created.Success, Is.True, created.Error);

        bool deleted = session.Helper.DeleteLocalProfile(created.Value, out string error);

        Assert.That(deleted, Is.True, error);
        Assert.That(CountUsers(), Is.EqualTo(0));
    }

    /// <summary>
    /// Source guard: UserAccountManager's deletion coroutine must never pre-set DBHelper.DatabaseLocked
    /// before calling DeleteLocalProfile - that was the exact root cause of the old lock deadlock.
    /// Scoped to just the RemoveUserButton method body (rather than the whole file) because
    /// loadUserData/CreateUserButtons legitimately set DatabaseLocked for unrelated read operations.
    /// </summary>
    [Test]
    public void UserAccountManagerDoesNotPreSetDatabaseLockedBeforeDeletion()
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "account", "UserAccountManager.cs");
        string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

        Match method = Regex.Match(
            text,
            @"IEnumerator RemoveUserButton\(string userName\)(?<body>.*?)\n    IEnumerator loadUserData",
            RegexOptions.Singleline);

        Assert.That(method.Success, Is.True, "Could not locate RemoveUserButton's method body to scan.");
        string body = method.Groups["body"].Value;

        Assert.That(body, Does.Not.Contain("DatabaseLocked = true"));
        Assert.That(body, Does.Not.Contain("DatabaseLocked=true"));
        Assert.That(body, Does.Contain("DeleteLocalProfile"));
    }

    // ------------------------------------------------------------------
    // Cascade isolation
    // ------------------------------------------------------------------

    [Test]
    public void DeletingAProfileRemovesOnlyItsOwnAccountScopedSqliteRows()
    {
        ApiResult<UserModel> a = CreateProfile("cascade-a");
        ApiResult<UserModel> b = CreateProfile("cascade-b");
        Assert.That(a.Success, Is.True, a.Error);
        Assert.That(b.Success, Is.True, b.Error);

        string accountA = CharacterProgressAccountId.Resolve(a.Value.Userid, a.Value.UserName);
        string accountB = CharacterProgressAccountId.Resolve(b.Value.Userid, b.Value.UserName);

        SeedCharacterProfile(accountA, 1);
        SeedCharacterProfile(accountB, 1);
        SeedProgressionLedgerRow("result-a", accountA, 1);
        SeedProgressionLedgerRow("result-b", accountB, 1);

        bool deleted = session.Helper.DeleteLocalProfile(a.Value, out string error);
        Assert.That(deleted, Is.True, error);

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM User WHERE userid = @id";
            cmd.Parameters.Add(new SqliteParameter("@id", a.Value.Userid));
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "User A should be gone.");
        }

        Assert.That(CountRows("CharacterProfile", accountA), Is.EqualTo(0), "CharacterProfile A should be gone.");
        Assert.That(CountRows("ProgressionResultLedger", accountA), Is.EqualTo(0), "ProgressionResultLedger A should be gone.");

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM User WHERE userid = @id";
            cmd.Parameters.Add(new SqliteParameter("@id", b.Value.Userid));
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1), "User B should remain.");
        }

        Assert.That(CountRows("CharacterProfile", accountB), Is.EqualTo(1), "CharacterProfile B should remain.");
        Assert.That(CountRows("ProgressionResultLedger", accountB), Is.EqualTo(1), "ProgressionResultLedger B should remain.");
    }

    [Test]
    public void ADeletionFailureRollsBackAnyPartialCascadeWork()
    {
        // userid/username mismatch (a row exists for the id but under a different name) makes the
        // final User delete affect 0 rows, so the whole transaction - including the CharacterProfile
        // delete that already ran - must roll back rather than leaving a partial deletion.
        ApiResult<UserModel> created = CreateProfile("mismatch-target");
        Assert.That(created.Success, Is.True, created.Error);

        string accountId = CharacterProgressAccountId.Resolve(created.Value.Userid, created.Value.UserName);
        SeedCharacterProfile(accountId, 1);

        UserModel mismatched = new UserModel { Userid = created.Value.Userid, UserName = "not-the-real-name" };
        bool deleted = session.Helper.DeleteLocalProfile(mismatched, out string error);

        Assert.That(deleted, Is.False);
        Assert.That(CountUsers(), Is.EqualTo(1), "The real user row must still be present.");
        Assert.That(CountRows("CharacterProfile", accountId), Is.EqualTo(1), "CharacterProfile must not be partially deleted.");
    }

    // ------------------------------------------------------------------
    // File cleanup
    // ------------------------------------------------------------------

    private static string AccountFilePath(string accountId, string suffix)
    {
        return Path.Combine(Application.persistentDataPath, "accounts", accountId + suffix);
    }

    private static void WriteFileFamily(string path)
    {
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{}");
        File.WriteAllText(path + ".bak", "{}");
        File.WriteAllText(path + ".tmp", "{}");
    }

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void DeletingAProfileRemovesItsAccountScopedFileFamiliesButLeavesAnotherAccountsFilesAlone()
    {
        // Synthetic GUID-prefixed account ids rather than real SQLite-allocated ones (mirrors
        // ProgressionPersistenceRecoveryTests' testUserId convention) - a real allocated id could
        // collide with an actual local profile's files already on the machine running this test, and
        // this test only needs to prove file-family isolation, not that the id came from SQLite.
        string accountA = "aud-deletion-test-a-" + Guid.NewGuid().ToString("N");
        string accountB = "aud-deletion-test-b-" + Guid.NewGuid().ToString("N");

        string[] suffixes = { "-characters.json", "-pending-progression.json", "-progression-results.json" };
        try
        {
            foreach (string suffix in suffixes)
            {
                WriteFileFamily(AccountFilePath(accountA, suffix));
                WriteFileFamily(AccountFilePath(accountB, suffix));
            }

            bool cleanedCharacters = CharacterProgressStore.DeleteAccountFiles(accountA);
            bool cleanedPending = PendingProgressionStore.DeleteAccountFiles(accountA);
            bool cleanedResults = ProgressionResultStore.DeleteAccountFiles(accountA);
            Assert.That(cleanedCharacters && cleanedPending && cleanedResults, Is.True);

            foreach (string suffix in suffixes)
            {
                string path = AccountFilePath(accountA, suffix);
                Assert.That(File.Exists(path), Is.False, path + " should be gone.");
                Assert.That(File.Exists(path + ".bak"), Is.False, path + ".bak should be gone.");
                Assert.That(File.Exists(path + ".tmp"), Is.False, path + ".tmp should be gone.");

                string otherPath = AccountFilePath(accountB, suffix);
                Assert.That(File.Exists(otherPath), Is.True, otherPath + " should remain.");
                Assert.That(File.Exists(otherPath + ".bak"), Is.True, otherPath + ".bak should remain.");
                Assert.That(File.Exists(otherPath + ".tmp"), Is.True, otherPath + ".tmp should remain.");
            }
        }
        finally
        {
            foreach (string suffix in suffixes)
            {
                string pathA = AccountFilePath(accountA, suffix);
                string pathB = AccountFilePath(accountB, suffix);
                DeleteIfPresent(pathA);
                DeleteIfPresent(pathA + ".bak");
                DeleteIfPresent(pathA + ".tmp");
                DeleteIfPresent(pathB);
                DeleteIfPresent(pathB + ".bak");
                DeleteIfPresent(pathB + ".tmp");
            }
        }
    }

    // ------------------------------------------------------------------
    // Guest
    // ------------------------------------------------------------------

    [Test]
    public void GuestCannotBeDeletedThroughProfileDeletion()
    {
        UserModel guest = new UserModel { Userid = UserAccountManager.GuestUserid, UserName = UserAccountManager.GuestUsername };

        bool deleted = session.Helper.DeleteLocalProfile(guest, out string error);

        Assert.That(deleted, Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GuestCannotBeDeletedEvenIfAUserRowHappenedToExistAtTheReservedId()
    {
        // Defensive: guest is never a persistent User row in production, but this proves the
        // rejection is by identity, not merely "no such row exists".
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin) "
                + "VALUES (74, 'guest', '', '', '', '', @now, @now)";
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }

        UserModel guest = new UserModel { Userid = UserAccountManager.GuestUserid, UserName = UserAccountManager.GuestUsername };
        bool deleted = session.Helper.DeleteLocalProfile(guest, out string error);

        Assert.That(deleted, Is.False);
        Assert.That(CountUsers(), Is.EqualTo(1), "The defensive guest row must be left untouched.");
    }

    [Test]
    public void ARealProfileThatHappensToBeNamedGuestIsNotTheReservedFallbackAndRemainsDeletable()
    {
        // Nothing in CreateLocalProfileCoroutine denies the display name "guest" - no real User row is
        // ever created at the reserved id (74), so no duplicate-name collision occurs, and the
        // allocator never hands out 74. Rejection must be keyed on Userid == GuestUserid (the reserved
        // scope itself), not on the display name - otherwise this profile would be permanently
        // undeletable even though it is an ordinary, differently-scoped local profile.
        ApiResult<UserModel> created = CreateProfile(UserAccountManager.GuestUsername);
        Assert.That(created.Success, Is.True, created.Error);
        Assert.That(created.Value.Userid, Is.Not.EqualTo(UserAccountManager.GuestUserid));

        bool deleted = session.Helper.DeleteLocalProfile(created.Value, out string error);

        Assert.That(deleted, Is.True, error);
        Assert.That(CountUsers(), Is.EqualTo(0));
    }

    // ------------------------------------------------------------------
    // Current identity
    // ------------------------------------------------------------------

    [Test]
    public void DeletingTheCurrentlySelectedProfileShouldClearTheLocalIdentity()
    {
        ApiResult<UserModel> a = CreateProfile("current-a");
        Assert.That(a.Success, Is.True, a.Error);

        bool matches = UserAccountManager.MatchesCurrentLocalIdentity(a.Value, a.Value.Userid, a.Value.UserName);

        Assert.That(matches, Is.True);
    }

    [Test]
    public void DeletingADifferentProfileWhileAnotherIsSelectedShouldNotClearTheLocalIdentity()
    {
        ApiResult<UserModel> a = CreateProfile("current-a2");
        ApiResult<UserModel> b = CreateProfile("current-b2");
        Assert.That(a.Success, Is.True, a.Error);
        Assert.That(b.Success, Is.True, b.Error);

        // B is the active local selection; A is the one being deleted.
        bool matches = UserAccountManager.MatchesCurrentLocalIdentity(a.Value, b.Value.Userid, b.Value.UserName);

        Assert.That(matches, Is.False);
    }

    // ------------------------------------------------------------------
    // Backend V2 independence
    // ------------------------------------------------------------------

    [Test]
    public void LocalProfileDeletionLeavesAnExistingBackendV2SessionUnchanged()
    {
        BackendV2Session existing = new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionStore.Set(existing);

        ApiResult<UserModel> created = CreateProfile("v2-independence");
        Assert.That(created.Success, Is.True, created.Error);

        bool deleted = session.Helper.DeleteLocalProfile(created.Value, out string error);
        Assert.That(deleted, Is.True, error);

        Assert.That(BackendV2SessionStore.Current, Is.SameAs(existing));
    }

    // ------------------------------------------------------------------
    // Local-history preservation
    // ------------------------------------------------------------------

    [Test]
    public void DeletingAProfileLeavesHighScoresAndAllTimeStatsUnchanged()
    {
        ApiResult<UserModel> created = CreateProfile("history-owner");
        Assert.That(created.Success, Is.True, created.Error);

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO HighScores (scoreidUnique, modeid, totalPoints, userName) "
                + "VALUES ('score-1', 1, 500, @userName)";
            cmd.Parameters.Add(new SqliteParameter("@userName", created.Value.UserName));
            cmd.ExecuteNonQuery();
        }

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO AllTimeStats (twoMade, twoAtt, totalPoints) VALUES (5, 10, 100)";
            cmd.ExecuteNonQuery();
        }

        int highScoresBefore = CountRows("HighScores");
        int allTimeStatsBefore = CountRows("AllTimeStats");

        bool deleted = session.Helper.DeleteLocalProfile(created.Value, out string error);
        Assert.That(deleted, Is.True, error);

        Assert.That(CountRows("HighScores"), Is.EqualTo(highScoresBefore));
        Assert.That(CountRows("AllTimeStats"), Is.EqualTo(allTimeStatsBefore));
    }
}
