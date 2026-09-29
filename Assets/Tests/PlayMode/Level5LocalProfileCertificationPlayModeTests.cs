#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.BackendV2;
using Level5.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Player-facing certification of the local-profile flows left by the V1 account/auth retirement
/// (PR #205), driven through the real scenes and the real buttons (<see cref="LocalProfileUiFlow"/>)
/// against a throwaway SQLite file (<see cref="LocalProfileTestDatabase"/> - never the developer's own
/// <c>level5.db</c>):
///
///   Account Hub -> Create Local Profile -> profile-name field -> Create Profile -> SQLite commit ->
///   loading scene;  restart/reopen -> loginLocal -> profile row renders -> row button -> loading scene;
///   empty database -> guest row -> loading scene.
///
/// Also certifies, on every path, that the Backend V2 online identity is a separate world: a signed-in
/// session is never replaced, cleared, refreshed or even touched (no <see cref="BackendV2SessionStore.Changed"/>
/// event, no request through the Backend V2 transport) by anything a local profile does. The retired V1
/// account transport cannot be observed firing because it no longer exists - that is asserted directly.
/// </summary>
public class Level5LocalProfileCertificationPlayModeTests
{
    private const int ReservedGuestUserId = 74;

    private static readonly string[] RetiredV1AccountMembers =
    {
        "PostToken", "PostUser", "UserExists", "UserNameExists", "EmailExists", "GetUserByUserName",
        "HasSession", "BearerToken", "ClearSession",
    };

    private LocalProfileTestDatabase database;
    private RecordingApiTransport transport;
    private BackendV2Session onlineSession;
    private int sessionChangedCount;
    private readonly List<string> loadedSceneNames = new List<string>();
    private int previousUserId;
    private string previousUserName;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();

        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = null;

        // A signed-in Backend V2 player must survive every local-profile operation untouched.
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);
        onlineSession = new BackendV2Session(
            "local-profile-cert-access-token",
            DateTimeOffset.UtcNow.AddMinutes(10),
            Guid.NewGuid(),
            "local-profile-cert-refresh-token",
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionStore.Set(onlineSession);
        sessionChangedCount = 0;
        BackendV2SessionStore.Changed += OnSessionChanged;

        loadedSceneNames.Clear();
        SceneManager.sceneLoaded += OnSceneLoaded;

        database = new LocalProfileTestDatabase();
        yield return database.Open();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        BackendV2SessionStore.Changed -= OnSessionChanged;

        // Scenes first: LoadManager/UserAccountManager coroutines must not outlive the database.
        yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("local-profile-cert-cleanup");
        database.Dispose();

        LocalAccountIdentity.UserId = previousUserId;
        LocalAccountIdentity.UserName = previousUserName;

        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
        LogAssert.ignoreFailingMessages = false;
    }

    private void OnSessionChanged(BackendV2Session session)
    {
        sessionChangedCount++;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loadedSceneNames.Add(scene.name);
    }

    // ------------------------------------------------------------------
    // Create Local Profile through the real UI
    // ------------------------------------------------------------------

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator CreateLocalProfileThroughTheRealUiCommitsToSqliteAndSelectsTheNewProfile()
    {
        AssertRetiredV1AccountTransportIsGone();

        int userId = 0;
        yield return LocalProfileUiFlow.CreateProfileThroughHub(database, "certPlayer", id => userId = id);

        // SQLite is the durable record: the row is committed, correctly named, with a valid local id.
        List<KeyValuePair<int, string>> users = database.ReadUsers();
        Assert.That(users, Has.Count.EqualTo(1));
        Assert.That(users[0].Value, Is.EqualTo("certPlayer"));
        Assert.That(users[0].Key, Is.EqualTo(userId));
        Assert.That(userId, Is.GreaterThan(0));
        Assert.That(userId, Is.Not.EqualTo(0));
        Assert.That(userId, Is.Not.EqualTo(ReservedGuestUserId));

        // The local selection is the created profile, and it scopes progression.
        Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(userId));
        Assert.That(LocalAccountIdentity.UserName, Is.EqualTo("certPlayer"));
        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo(userId.ToString()));

        // Normal loading proceeds for the new scope, and that scope's progression can be initialized.
        yield return LocalProfileUiFlow.WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start, 60f,
            () => "loading did not finish for the new profile: " + ProgressionDiagnostics());
        Assert.That(database.EnsureCharacterProfilesForAccount(userId.ToString()), Is.True);

        AssertOnlineIdentityUntouched();
    }

    // ------------------------------------------------------------------
    // Restart/reopen -> existing profile renders -> real row button selects it
    // ------------------------------------------------------------------

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator AProfileCreatedInOneSessionIsListedAndSelectedFromItsRowAfterARestart()
    {
        // --- session 1: create through the real UI, then give the new scope some durable progression ---
        int userId = 0;
        yield return LocalProfileUiFlow.CreateProfileThroughHub(database, "returningPlayer", id => userId = id);
        string scopeBefore = CharacterProgressAccountId.GetCurrent();
        Assert.That(scopeBefore, Is.EqualTo(userId.ToString()));
        yield return LocalProfileUiFlow.WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start, 60f,
            () => "loading did not finish for the new profile: " + ProgressionDiagnostics());
        database.SeedCharacterProfile(scopeBefore, 1);
        database.SeedCharacterProfile("999999", 1); // an unrelated scope that must stay untouched
        Assert.That(database.ReadCharacterProfileCountThroughProduction(userId), Is.EqualTo(1));

        // --- restart: nothing in memory survives, only what SQLite committed ---
        yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("local-profile-cert-restart");
        database.Close();
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = null;
        loadedSceneNames.Clear();
        yield return database.Open();
        Assert.That(database.ReadUsers().Select(u => u.Value), Is.EquivalentTo(new[] { "returningPlayer" }));

        // --- session 2: the profile list renders the existing profile, real row button selects it ---
        yield return LocalProfileUiFlow.LoadScene(Constants.SCENE_NAME_level_00_account_loginLocal);
        List<Button> loginButtons = null;
        yield return LocalProfileUiFlow.WaitForProfileRows(1, rows => loginButtons = rows);
        Assert.That(LocalProfileUiFlow.RowName(loginButtons[0]), Is.EqualTo("returningPlayer"));
        Assert.That(
            LocalProfileUiFlow.ReadManagerProperty("UserAccountManager", "UsersLoaded"), Is.True,
            "the existing profile must load as a profile");

        yield return LocalProfileUiFlow.SelectProfileRow(loginButtons[0]);

        Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(userId), "the same local id must be selected after a restart");
        Assert.That(LocalAccountIdentity.UserName, Is.EqualTo("returningPlayer"));
        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo(scopeBefore));
        Assert.That(loadedSceneNames, Does.Not.Contain(Constants.SCENE_NAME_level_00_account_loginExisting),
            "selecting a local profile must never route through the retired login/password screen");

        // Same scope, same progression: normal loading after the restart reads what session 1 wrote,
        // under the same local identity, without re-scoping or duplicating it.
        yield return LocalProfileUiFlow.WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start, 60f,
            () => "loading after the restart did not finish: " + ProgressionDiagnostics());
        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo(scopeBefore));
        Assert.That(database.HasCharacterProfilesForAccount(scopeBefore), Is.True);
        Assert.That(database.ReadCharacterProfileCountThroughProduction(userId), Is.EqualTo(1));
        Assert.That(database.CountRows("CharacterProfile", "accountId", scopeBefore), Is.EqualTo(1));
        Assert.That(database.CountRows("CharacterProfile", "accountId", "999999"), Is.EqualTo(1));
        Assert.That(database.ReadUsers(), Has.Count.EqualTo(1), "re-selection must not create or duplicate a profile");

        AssertOnlineIdentityUntouched();
    }

    // ------------------------------------------------------------------
    // Existing (V1-era) rows: the exact audited defect in the real scene
    // ------------------------------------------------------------------

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator SeededExistingProfilesAllRenderAndEachRowSelectsItsOwnIdentity()
    {
        // Ids the way a V1-era database holds them: server-assigned, not from the local allocator.
        DateTime now = DateTime.UtcNow;
        database.SeedUser(500, "legacyOne", now.AddMinutes(-2));
        database.SeedUser(1234, "legacyTwo", now.AddMinutes(-1));

        yield return LocalProfileUiFlow.LoadScene(Constants.SCENE_NAME_level_00_account_loginLocal);
        List<Button> loginButtons = null;
        yield return LocalProfileUiFlow.WaitForProfileRows(2, rows => loginButtons = rows);

        Assert.That(loginButtons.Select(LocalProfileUiFlow.RowName), Is.EquivalentTo(new[] { "legacyOne", "legacyTwo" }),
            "a non-empty User table must list every profile - not just the guest row");

        Button second = loginButtons.Single(b => LocalProfileUiFlow.RowName(b) == "legacyTwo");
        yield return LocalProfileUiFlow.SelectProfileRow(second);

        Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(1234), "the row's stored id must be used unchanged");
        Assert.That(LocalAccountIdentity.UserName, Is.EqualTo("legacyTwo"));
        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo("1234"));
        Assert.That(database.ReadUsers().Select(u => u.Key), Is.EquivalentTo(new[] { 500, 1234 }),
            "selecting a profile must never rewrite ids");

        AssertOnlineIdentityUntouched();
    }

    // ------------------------------------------------------------------
    // Guest
    // ------------------------------------------------------------------

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator GuestRowUsesTheReservedLocalGuestIdentityWithNoPasswordOrNetwork()
    {
        yield return LocalProfileUiFlow.LoadScene(Constants.SCENE_NAME_level_00_account_loginLocal);
        List<Button> loginButtons = null;
        yield return LocalProfileUiFlow.WaitForProfileRows(1, rows => loginButtons = rows);

        Assert.That(LocalProfileUiFlow.RowName(loginButtons[0]), Is.EqualTo("guest"), "an empty database offers exactly the guest row");
        Assert.That(LocalProfileUiFlow.ReadManagerProperty("UserAccountManager", "UsersLoaded"), Is.False);

        yield return LocalProfileUiFlow.SelectProfileRow(loginButtons[0]);

        Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(ReservedGuestUserId));
        Assert.That(LocalAccountIdentity.UserName, Is.EqualTo("guest"));
        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo(ReservedGuestUserId.ToString()));
        Assert.That(loadedSceneNames, Does.Not.Contain(Constants.SCENE_NAME_level_00_account_loginExisting));
        Assert.That(database.ReadUsers(), Is.Empty, "guest is a fallback scope, never a persisted User row");

        AssertOnlineIdentityUntouched();
    }

    // ------------------------------------------------------------------
    // Independence assertions
    // ------------------------------------------------------------------

    private string ProgressionDiagnostics()
    {
        return "scene=" + SceneManager.GetActiveScene().name + " locked=" + database.DatabaseLocked
            + " characterProfileRows=" + database.CountRows("CharacterProfile")
            + " identity=" + LocalAccountIdentity.UserId + "/" + LocalAccountIdentity.UserName;
    }

    private void AssertOnlineIdentityUntouched()
    {
        Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True, "a local-profile operation signed the online player out");
        Assert.That(BackendV2SessionStore.Current, Is.SameAs(onlineSession), "the Backend V2 session was replaced");
        Assert.That(BackendV2SessionStore.Current.PlayerId, Is.EqualTo(onlineSession.PlayerId));
        Assert.That(sessionChangedCount, Is.EqualTo(0), "a local-profile operation changed the Backend V2 session");
        Assert.That(transport.Requests, Is.Empty, "a local-profile operation made a Backend V2 request");
    }

    /// <summary>
    /// The V1 account transport cannot fire because it is gone. Reflection (not a source scan) proves
    /// the running assembly no longer carries any of it.
    /// </summary>
    private static void AssertRetiredV1AccountTransportIsGone()
    {
        Type apiHelper = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Assets.Scripts.restapi.APIHelper", false))
            .FirstOrDefault(t => t != null);
        Assert.That(apiHelper, Is.Not.Null, "APIHelper (which still owns the anonymous utility services) was not found");

        foreach (string member in RetiredV1AccountMembers)
        {
            Assert.That(
                apiHelper.GetMember(
                    member,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance),
                Is.Empty,
                "retired V1 account member " + member + " has returned to APIHelper");
        }
    }

    private sealed class RecordingApiTransport : IApiTransport
    {
        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            completed?.Invoke(RawApiResponse.NetworkError());
            yield break;
        }
    }
}
#endif
