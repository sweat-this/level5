using System;
using System.Data;
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Level5.BackendV2.Tests;
using Level5.Core;
using Mono.Data.Sqlite;
using NUnit.Framework;

/// <summary>
/// Exercises <see cref="DBHelper.CreateLocalProfileCoroutine"/> against a real, throwaway SQLite
/// database via <see cref="DBHelperTestHarness"/> - no prior test opened live SQLite, so this also
/// proves the harness itself works. Covers the local-id allocation rules: positive, unique, never 0,
/// never the reserved guest id, atomic with the insert, and existing rows are never touched.
/// </summary>
public class Level5LocalProfileAllocationTests
{
    private DBHelperTestHarness.Session session;

    [SetUp]
    public void SetUp()
    {
        session = DBHelperTestHarness.Create();
    }

    [TearDown]
    public void TearDown()
    {
        DBHelperTestHarness.Destroy(session);
    }

    private void SeedUser(int userid, string username)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin) "
                + "VALUES (@userid, @username, 'first', 'last', 'someone@example.com', '127.0.0.1', @now, @now)";
            cmd.Parameters.Add(new SqliteParameter("@userid", userid));
            cmd.Parameters.Add(new SqliteParameter("@username", username));
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }
    }

    private int CountUsers()
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM User";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private ApiResult<UserModel> CreateProfile(string profileName)
    {
        ApiResult<UserModel> result = null;
        CoroutineTestRunner.RunToCompletion(
            session.Helper.CreateLocalProfileCoroutine(profileName, value => result = value));
        return result;
    }

    [Test]
    public void FirstProfileOnAnEmptyDatabaseGetsAPositiveId()
    {
        ApiResult<UserModel> result = CreateProfile("alice");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.GreaterThan(0));
        Assert.That(result.Value.UserName, Is.EqualTo("alice"));
        Assert.That(CountUsers(), Is.EqualTo(1));
    }

    [Test]
    public void AllocatedIdIsNeverZero()
    {
        ApiResult<UserModel> result = CreateProfile("bob");

        Assert.That(result.Value.Userid, Is.Not.EqualTo(0));
    }

    [Test]
    public void AllocatedIdSkipsTheReservedGuestId()
    {
        // MAX(userid) would be 73, so the naive next id is 74 - the reserved guest id - and must be
        // skipped in favor of 75.
        SeedUser(73, "seventythree");

        ApiResult<UserModel> result = CreateProfile("carol");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.Not.EqualTo(UserAccountManager.GuestUserid));
        Assert.That(result.Value.Userid, Is.EqualTo(75));
    }

    [Test]
    public void AllocationWorksWhenExistingIdsSurroundTheReservedValue()
    {
        SeedUser(73, "seventythree");
        SeedUser(74, "reserved-but-somehow-present");
        SeedUser(75, "seventyfive");

        ApiResult<UserModel> result = CreateProfile("dave");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.EqualTo(76));
        Assert.That(result.Value.Userid, Is.Not.EqualTo(UserAccountManager.GuestUserid));
    }

    [Test]
    public void ExistingIdsAreUnchangedAfterANewAllocation()
    {
        SeedUser(5, "existing-one");
        SeedUser(9, "existing-two");

        ApiResult<UserModel> result = CreateProfile("newcomer");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.EqualTo(10));

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT userid FROM User WHERE username = 'existing-one'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(5));
        }

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT userid FROM User WHERE username = 'existing-two'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(9));
        }
    }

    [Test]
    public void DuplicateProfileNameIsRejectedAndNoRowIsInserted()
    {
        ApiResult<UserModel> first = CreateProfile("erin");
        Assert.That(first.Success, Is.True, first.Error);

        ApiResult<UserModel> second = CreateProfile("erin");

        Assert.That(second.Success, Is.False);
        Assert.That(CountUsers(), Is.EqualTo(1));
    }

    [Test]
    public void AFailedInsertDoesNotConsumeOrApplyAnIdentity()
    {
        // Seeding the maximum representable id forces the exhaustion guard (MAX + 1 overflows
        // int.MaxValue) to reject the allocation deterministically, inside the same transaction as
        // the would-be insert - proving a rejected candidate never reaches a committed row.
        SeedUser(int.MaxValue, "already-at-the-limit");

        ApiResult<UserModel> result = CreateProfile("frank");

        Assert.That(result.Success, Is.False);
        Assert.That(CountUsers(), Is.EqualTo(1));

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM User WHERE username = 'frank'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));
        }
    }

    [Test]
    public void TheCommittedProfileMatchesWhatASubsequentReadReturns()
    {
        ApiResult<UserModel> result = CreateProfile("gina");
        Assert.That(result.Success, Is.True, result.Error);

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT userid FROM User WHERE username = 'gina'";
            int persistedId = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.That(persistedId, Is.EqualTo(result.Value.Userid));
        }
    }

    [Test]
    public void NewlyCreatedProfileGetsItsOwnCharacterProgressionAccountScope()
    {
        int previousUserId = LocalAccountIdentity.UserId;
        string previousUserName = LocalAccountIdentity.UserName;
        try
        {
            ApiResult<UserModel> first = CreateProfile("henry");
            ApiResult<UserModel> second = CreateProfile("iris");
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(second.Success, Is.True, second.Error);

            LocalAccountIdentity.UserId = first.Value.Userid;
            LocalAccountIdentity.UserName = first.Value.UserName;
            string firstScope = CharacterProgressAccountId.GetCurrent();

            LocalAccountIdentity.UserId = second.Value.Userid;
            LocalAccountIdentity.UserName = second.Value.UserName;
            string secondScope = CharacterProgressAccountId.GetCurrent();

            Assert.That(firstScope, Is.Not.EqualTo(secondScope));
        }
        finally
        {
            LocalAccountIdentity.UserId = previousUserId;
            LocalAccountIdentity.UserName = previousUserName;
        }
    }

    [Test]
    public void PreExistingV1EraRowsRemainReadableUnchanged()
    {
        // A V1-era row always had these legacy fields populated by the server response; seeded here
        // with a non-empty password/bearerToken to mirror what an upgrading install's database file
        // could still contain before DBConnector.createDatabase()'s scrub runs on next launch.
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin, password, bearerToken) "
                + "VALUES (12345, 'legacy-player', 'Leg', 'Acy', 'legacy@example.com', '10.0.0.1', @now, @now, 'oldpw', 'oldtoken')";
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }

        System.Collections.Generic.List<UserModel> users = session.Helper.getUserProfileStats();

        Assert.That(users.Count, Is.EqualTo(1));
        Assert.That(users[0].Userid, Is.EqualTo(12345));
        Assert.That(users[0].UserName, Is.EqualTo("legacy-player"));
    }

    /// <summary>
    /// The in-memory V1 bearer session (<c>APIHelper.ClearSession</c>/<c>BearerToken</c>) is gone
    /// entirely, so it can no longer carry a stale credential across a local-profile switch - but an
    /// upgrading install's database file can still be carrying a plaintext password/bearerToken an
    /// older app version wrote to the User table (see <see cref="DBConnector"/>'s scrub comment). This
    /// is the surviving equivalent of the retired "stale V1 credential" concern: proves the next launch
    /// (a second <see cref="DBConnector.createDatabase"/> run, exactly what happens on every app start)
    /// clears that leftover credential material while leaving the row's identity - the very thing a
    /// local-profile selection reads - untouched.
    /// </summary>
    [Test]
    public void ReopeningTheDatabaseScrubsAStaleCredentialButPreservesTheProfileIdentity()
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin, password, bearerToken) "
                + "VALUES (123, 'Patrick', 'Pat', 'Rick', 'patrick@example.com', '10.0.0.2', @now, @now, 'stale-pw', 'stale-token')";
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }

        CoroutineTestRunner.RunToCompletion(session.Connector.createDatabase());

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT userid, username, password, bearerToken FROM User WHERE userid = 123";
            using (IDataReader reader = cmd.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True);
                Assert.That(Convert.ToInt32(reader["userid"]), Is.EqualTo(123));
                Assert.That(reader["username"], Is.EqualTo("Patrick"));
                Assert.That(reader["password"], Is.EqualTo(DBNull.Value));
                Assert.That(reader["bearerToken"], Is.EqualTo(DBNull.Value));
            }
        }
    }

    // ------------------------------------------------------------------
    // ID non-reuse (durable LocalProfileIdSequence high-water, independent of which User rows
    // currently exist - see docs/persistence-boundaries.md and DBHelper.AllocateNextUserId).
    // ------------------------------------------------------------------

    [Test]
    public void ADeletedProfilesIdIsNeverReissuedToTheNextCreatedProfile()
    {
        ApiResult<UserModel> a = CreateProfile("reuse-a");
        Assert.That(a.Success, Is.True, a.Error);
        int deletedId = a.Value.Userid;

        bool deleted = session.Helper.DeleteLocalProfile(a.Value, out string deleteError);
        Assert.That(deleted, Is.True, deleteError);

        ApiResult<UserModel> b = CreateProfile("reuse-b");

        Assert.That(b.Success, Is.True, b.Error);
        Assert.That(b.Value.Userid, Is.GreaterThan(deletedId));
        Assert.That(b.Value.Userid, Is.Not.EqualTo(deletedId));
    }

    [Test]
    public void DeletingTheCurrentHighestIdAmongMultipleProfilesStillDoesNotReuseIt()
    {
        ApiResult<UserModel> a = CreateProfile("multi-a");
        ApiResult<UserModel> c = CreateProfile("multi-c");
        Assert.That(a.Success, Is.True, a.Error);
        Assert.That(c.Success, Is.True, c.Error);
        Assert.That(c.Value.Userid, Is.GreaterThan(a.Value.Userid));
        int highestDeletedId = c.Value.Userid;

        bool deleted = session.Helper.DeleteLocalProfile(c.Value, out string deleteError);
        Assert.That(deleted, Is.True, deleteError);

        ApiResult<UserModel> d = CreateProfile("multi-d");

        Assert.That(d.Success, Is.True, d.Error);
        Assert.That(d.Value.Userid, Is.GreaterThan(highestDeletedId));
        Assert.That(d.Value.Userid, Is.Not.EqualTo(highestDeletedId));
    }

    // ------------------------------------------------------------------
    // Upgrade high-water: on first use against a database that predates LocalProfileIdSequence, the
    // sequence must bootstrap from the largest known numeric local-profile scope across User.userid,
    // CharacterProfile.accountId, and ProgressionResultLedger.accountId (when that table exists) - so
    // a profile deleted before the sequence table ever existed, whose progression rows still remain,
    // cannot have its identity recycled.
    // ------------------------------------------------------------------

    [Test]
    public void FirstAllocationAgainstAnUpgradedDatabaseBootstrapsFromTheHighestKnownUserid()
    {
        SeedUser(10, "upgraded-max-user");

        ApiResult<UserModel> result = CreateProfile("post-upgrade");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.GreaterThan(10));
    }

    [Test]
    public void FirstAllocationAgainstAnUpgradedDatabaseBootstrapsFromANumericCharacterProfileAccountIdHigherThanAnyUserid()
    {
        // Simulates a profile that was deleted before LocalProfileIdSequence existed: its User row is
        // gone, but its CharacterProfile rows (accountId "25") are still present - MAX(User.userid) is
        // only 10, but the new id must still exceed 25.
        SeedUser(10, "upgraded-max-user");
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO CharacterProfile (accountId, charid, playerName, objectName, experience, level) "
                + "VALUES ('25', 1, 'name', 'object', 0, 0)";
            cmd.ExecuteNonQuery();
        }

        ApiResult<UserModel> result = CreateProfile("post-upgrade-orphaned-progress");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.GreaterThan(25));
    }

    [Test]
    public void FirstAllocationAgainstAnUpgradedDatabaseBootstrapsFromANumericProgressionResultLedgerAccountIdWhenThatTableExists()
    {
        SeedUser(10, "upgraded-max-user-2");
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS ProgressionResultLedger ("
                + "resultId TEXT PRIMARY KEY, accountId TEXT NOT NULL, characterId INTEGER NOT NULL, "
                + "experienceAfter INTEGER NOT NULL, levelAfter INTEGER NOT NULL, "
                + "projectionApplied INTEGER NOT NULL DEFAULT 0, appliedUtc TEXT NOT NULL)";
            cmd.ExecuteNonQuery();
        }

        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO ProgressionResultLedger (resultId, accountId, characterId, experienceAfter, levelAfter, projectionApplied, appliedUtc) "
                + "VALUES ('orphaned-result', '30', 1, 0, 0, 0, @now)";
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }

        ApiResult<UserModel> result = CreateProfile("post-upgrade-orphaned-ledger");

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Userid, Is.GreaterThan(30));
    }
}
