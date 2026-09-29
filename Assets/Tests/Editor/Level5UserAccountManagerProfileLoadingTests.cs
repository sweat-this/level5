using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Level5.BackendV2.Tests;
using Level5.Core;
using Mono.Data.Sqlite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression coverage for the local-profile list load (<see cref="UserAccountManager.RefreshLocalProfiles"/>,
/// the exact logic <c>UserAccountManager.loadUserData</c> runs once the database is free) against a real,
/// throwaway SQLite file via <see cref="DBHelperTestHarness"/>.
///
/// The defect this pins: <c>loadUserData</c> used to set <see cref="DBHelper.DatabaseLocked"/> itself and
/// then call <see cref="DBHelper.getUserProfileStats"/>, which owns its own cooperative lock and - by
/// design - returns an empty list when another owner holds it. An existing, non-empty User table
/// therefore read as zero profiles, and the player saw only the guest row. Calling
/// <c>getUserProfileStats</c> directly (as a plain DBHelper test would) can never catch that, because the
/// fault was in the caller - so these tests drive the caller's own entry point and additionally scan its
/// source for the two things that reintroduce it: a caller-owned lock and the redundant
/// <c>isTableEmpty</c> precheck.
/// </summary>
public class Level5UserAccountManagerProfileLoadingTests
{
    private DBHelperTestHarness.Session session;
    private GameObject managerObject;
    private UserAccountManager manager;
    private Text messageText;
    private int previousUserId;
    private string previousUserName;
    private int previousNumOfLocalUsers;

    [SetUp]
    public void SetUp()
    {
        session = DBHelperTestHarness.Create();

        // Some harness runs leave the singleton unset (Awake is not guaranteed to have run for a
        // component added in EditMode) - RefreshLocalProfiles resolves the database through
        // DBHelper.instance exactly as production does, so register the harness's helper explicitly.
        // DBHelperTestHarness.Destroy restores whatever was registered before.
        DBHelper.instance = session.Helper;

        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        previousNumOfLocalUsers = GameOptions.numOfLocalUsers;

        managerObject = new GameObject("UserAccountManagerUnderTest");
        manager = managerObject.AddComponent<UserAccountManager>();
        messageText = managerObject.AddComponent<Text>();
        typeof(UserAccountManager)
            .GetField("messageText", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, messageText);
    }

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        LocalAccountIdentity.UserId = previousUserId;
        LocalAccountIdentity.UserName = previousUserName;
        GameOptions.numOfLocalUsers = previousNumOfLocalUsers;
        DBHelperTestHarness.Destroy(session);
    }

    private void SeedUser(int userid, string username)
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin) "
                + "VALUES (@userid, @username, '', '', '', '', @now, @now)";
            cmd.Parameters.Add(new SqliteParameter("@userid", userid));
            cmd.Parameters.Add(new SqliteParameter("@username", username));
            cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
            cmd.ExecuteNonQuery();
        }
    }

    // ------------------------------------------------------------------
    // The audited defect: a non-empty User table must not read as zero profiles
    // ------------------------------------------------------------------

    [Test]
    public void ExistingProfilesInSqliteAreLoadedWithIdsAndNamesUnchanged()
    {
        SeedUser(5, "alice");
        SeedUser(17, "bob");

        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.True, "a non-empty User table must load as profiles, not as none");
        Assert.That(manager.UserAccountData, Has.Count.EqualTo(2));
        Assert.That(manager.UserAccountData.Exists(u => u.Userid == 5 && u.UserName == "alice"), Is.True);
        Assert.That(manager.UserAccountData.Exists(u => u.Userid == 17 && u.UserName == "bob"), Is.True);
        Assert.That(GameOptions.numOfLocalUsers, Is.EqualTo(2));
        Assert.That(session.Helper.DatabaseLocked, Is.False, "the load must leave the cooperative lock free");
    }

    [Test]
    public void AV1EraRowWithNullTextColumnsStillLoadsAndDoesNotHideOtherProfiles()
    {
        // V1 registration could store NULL for any of these columns; one unguarded read used to throw
        // and blank the entire list.
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO User(userid, username) VALUES (321, 'nulls')";
            cmd.ExecuteNonQuery();
        }

        SeedUser(5, "alice");

        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.True);
        Assert.That(manager.UserAccountData, Has.Count.EqualTo(2));
        Assert.That(manager.UserAccountData.Exists(u => u.Userid == 321 && u.UserName == "nulls"), Is.True);
        Assert.That(manager.UserAccountData.Exists(u => u.Userid == 5 && u.UserName == "alice"), Is.True);
    }

    [Test]
    public void ARowWithNoUsernameIsSkippedWithoutHidingOtherProfiles()
    {
        using (IDbCommand cmd = session.Helper.Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO User(userid, username) VALUES (322, NULL)";
            cmd.ExecuteNonQuery();
        }

        SeedUser(5, "alice");

        manager.RefreshLocalProfiles();

        Assert.That(manager.UserAccountData, Has.Count.EqualTo(1));
        Assert.That(manager.UserAccountData[0].UserName, Is.EqualTo("alice"));
    }

    [Test]
    public void AProfileCreatedThroughTheRealCreatePathIsLoadedBackAfterReopeningTheList()
    {
        ApiResult<UserModel> created = null;
        CoroutineTestRunner.RunToCompletion(
            session.Helper.CreateLocalProfileCoroutine("carol", value => created = value));
        Assert.That(created.Success, Is.True, created.Error);

        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.True);
        Assert.That(manager.UserAccountData, Has.Count.EqualTo(1));
        Assert.That(manager.UserAccountData[0].Userid, Is.EqualTo(created.Value.Userid));
        Assert.That(manager.UserAccountData[0].UserName, Is.EqualTo("carol"));
        Assert.That(manager.UserAccountData[0].Userid, Is.GreaterThan(0));
        Assert.That(manager.UserAccountData[0].Userid, Is.Not.EqualTo(UserAccountManager.GuestUserid));
    }

    [Test]
    public void ARefreshIsRepeatableAndDoesNotAccumulateOrChangeAnything()
    {
        SeedUser(9, "dana");

        manager.RefreshLocalProfiles();
        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.True);
        Assert.That(manager.UserAccountData, Has.Count.EqualTo(1));
        Assert.That(manager.UserAccountData[0].Userid, Is.EqualTo(9));
        Assert.That(manager.UserAccountData[0].UserName, Is.EqualTo("dana"));
        Assert.That(GameOptions.numOfLocalUsers, Is.EqualTo(1));
    }

    [Test]
    public void LoadingTheListNeverChangesTheSelectedLocalIdentity()
    {
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = null;
        SeedUser(5, "alice");

        manager.RefreshLocalProfiles();

        Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(0));
        Assert.That(LocalAccountIdentity.UserName, Is.Null);
    }

    // ------------------------------------------------------------------
    // Local-profile wording (there is no local login any more)
    // ------------------------------------------------------------------

    [Test]
    public void StatusMessageDescribesSelectingALocalProfileWhenProfilesExist()
    {
        SeedUser(5, "alice");

        manager.RefreshLocalProfiles();

        Assert.That(messageText.text, Is.EqualTo("select local profile"));
    }

    [Test]
    public void AnEmptyUserTableLoadsAsNoProfilesWithoutAFalsePositive()
    {
        GameOptions.numOfLocalUsers = 99;

        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.False);
        Assert.That(manager.UserAccountData, Is.Empty);
        Assert.That(GameOptions.numOfLocalUsers, Is.EqualTo(0), "a stale count from an earlier load must not survive");
        Assert.That(messageText.text, Is.EqualTo("no local profiles found"));
        Assert.That(session.Helper.DatabaseLocked, Is.False);
    }

    // ------------------------------------------------------------------
    // DBHelper's cooperative-lock contract is unchanged
    // ------------------------------------------------------------------

    [Test]
    public void GetUserProfileStatsStillRefusesToReadWhileAnotherOwnerHoldsTheLock()
    {
        // The callee's guard is deliberate (it protects the shared SQLite connection) and stays: the
        // fix was to stop the caller from holding the lock, not to weaken this.
        SeedUser(5, "alice");
        session.Helper.DatabaseLocked = true;

        manager.RefreshLocalProfiles();

        Assert.That(manager.UsersLoaded, Is.False);
        Assert.That(manager.UserAccountData, Is.Empty);
        Assert.That(session.Helper.DatabaseLocked, Is.True, "another owner's lock must be left exactly as found");
    }

    [Test]
    public void DbHelperKeepsItsLockGuardOnGetUserProfileStats()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(DbHelperPath));
        Match method = Regex.Match(
            text,
            @"public List<UserModel> getUserProfileStats\(\)\s*\{\s*if \(databaseLocked\)\s*\{\s*return new List<UserModel>\(\);",
            RegexOptions.Singleline);

        Assert.That(method.Success, Is.True, "getUserProfileStats must keep its `if (databaseLocked)` guard");
    }

    // ------------------------------------------------------------------
    // Source guards for the two ways the defect comes back
    // ------------------------------------------------------------------

    private static string AccountManagerPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "account", "UserAccountManager.cs");

    private static string DbHelperPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "database", "DBHelper.cs");

    private static string MethodBody(string text, string startMarker, string endMarker)
    {
        Match match = Regex.Match(
            text,
            Regex.Escape(startMarker) + "(?<body>.*?)" + Regex.Escape(endMarker),
            RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, "Could not locate '" + startMarker + "' to scan.");
        return match.Groups["body"].Value;
    }

    [Test]
    public void TheProfileLoadPathNeverOwnsTheDatabaseLockOrPrechecksTableEmptiness()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(AccountManagerPath));

        string loadPath = MethodBody(text, "IEnumerator loadUserData()", "IEnumerator CreateUserButtons()");

        Assert.That(loadPath, Does.Contain("getUserProfileStats"));
        Assert.That(loadPath, Does.Not.Contain("isTableEmpty"), "one authoritative query - no count-then-read split");
        Assert.That(loadPath, Does.Not.Contain("DatabaseLocked"), "DBHelper owns the lock for its own read");
    }

    [Test]
    public void TheGuestRowCarriesNoPasswordState()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(AccountManagerPath));

        Assert.That(text, Does.Not.Contain(".Password"));
        Assert.That(text, Does.Not.Contain("\"select user to log in\""));
        Assert.That(text, Does.Not.Contain("\"no users found\""));
    }
}
