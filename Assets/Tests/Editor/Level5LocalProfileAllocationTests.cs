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
}
