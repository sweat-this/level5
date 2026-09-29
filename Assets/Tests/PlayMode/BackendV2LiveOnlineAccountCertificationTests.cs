using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Opt-in, live-backend Unity-client certification for issue #194 (the Backend V2 online-account UI's
/// own remaining gap, flagged in PR #192's test plan: "Live Backend V2 certification (register ->
/// restore -> logout across a real process restart) - not attempted this pass ... Flagged as a
/// follow-up.").
///
/// Skipped (<c>Assert.Ignore</c>) unless <c>LEVEL5_LIVE_CERTIFICATION=1</c> is set, so ordinary
/// <c>-runTests</c> runs never depend on a live Backend V2 instance. Run each session as its own
/// <c>Unity.exe</c> invocation:
///
///   set LEVEL5_LIVE_CERTIFICATION=1
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode ^
///       -testFilter BackendV2LiveOnlineAccountCertificationTests.Session1_RegisterAndPersist ^
///       -testResults session1_results.xml -logFile session1.log
///
/// Optional env var (read at runtime, same convention as <c>BackendV2LiveCorrespondenceCertificationTests</c>):
/// <c>LEVEL5_BACKENDV2_BASE_URI</c> (defaults to <c>BackendV2ApiConfig.Development()</c>'s
/// <c>https://localhost:7029/</c>).
///
/// Session1 and Session2 are meant to run as two SEPARATE <c>Unity.exe</c> invocations - a genuine
/// process restart, not a scene reload (<c>BackendV2SessionPersistenceBootstrap</c> has process-static
/// initialization state, so a same-process scene reload does not certify restoration through
/// <c>UserAccountManager.Awake</c>). Nothing is shared between them in memory; only two on-disk things
/// carry state across the process boundary: the real
/// <c>Application.persistentDataPath/backendv2_session.json</c> session file (Session1's real Register
/// click writes it via the real <c>BackendV2SessionPersistenceBootstrap</c>; Session2 never registers
/// or logs in, only restores it) and a small certification-only handoff file
/// (<see cref="HandoffPath"/>) carrying only <c>PlayerId</c>/<c>DisplayName</c>/<c>Tag</c>/<c>Username</c>
/// - never a password or token - so Session2 knows what to assert against without re-deriving it.
///
/// Drives the real default-assembly <c>AccountManager</c>/<c>AccountHubUiObjects</c>/
/// <c>OnlineAccountController</c>/<c>OnlineAccountUiObjects</c> production types via
/// <see cref="RealScenePlayModeTestSupport"/>'s reflection helpers only: a named assembly definition
/// (this one) can never reference Unity's implicit default assembly, a hard engine restriction (see
/// that class's own doc comment, and <c>BackendV2LiveCorrespondenceCertificationTests</c>'s for the
/// same rule applied to <c>CorrespondenceScreenController</c>). <c>OnlineAccountCoordinator</c> and
/// every <c>Level5.BackendV2.*</c> session/transport type, by contrast, are referenced directly -
/// <c>Level5.PlayModeTests.asmdef</c> already takes a test-only reference to <c>Level5.BackendV2</c>.
///
/// No new production API surface was added for this fixture - every seam driven here
/// (<c>AccountHubUiObjects.OnlineAccountButton</c>, <c>OnlineAccountUiObjects</c>'s serialized fields,
/// <c>OnlineAccountController</c>'s private <c>coordinator</c> field) already exists from PR #192.
/// </summary>
public class BackendV2LiveOnlineAccountCertificationTests
{
    private const string EnabledEnvVar = "LEVEL5_LIVE_CERTIFICATION";
    private const string BaseUriEnvVar = "LEVEL5_BACKENDV2_BASE_URI";
    private const string BaseUriEnvironmentEnvVar = "LEVEL5_BACKENDV2_ENVIRONMENT";

    private static readonly string EvidencePath =
        Path.Combine(Path.GetTempPath(), "level5_unity_online_account_live_cert_evidence.log");

    private static readonly string HandoffPath =
        Path.Combine(Path.GetTempPath(), "level5_online_account_cert_handoff.json");

    // ================================================================= Session 1

    /// <summary>
    /// Registers a brand-new Backend V2 account through the real UI (Account hub -> real Online
    /// Account button -> real Register button), asserts the live profile renders and the session
    /// persists to disk, then writes the non-secret handoff file Session2 needs and lets this process
    /// exit so Session2 can certify a genuine restart.
    /// </summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator Session1_RegisterAndPersist()
    {
        RequireLiveCertificationOptIn();
        ApplyConfig(ResolveConfig());

        // The account scenes open a local SQLite profile database. Point them at a throwaway file so a
        // certification run never opens or migrates the developer's real level5.db.
        LocalProfileTestDatabase localDatabase = new LocalProfileTestDatabase();
        yield return localDatabase.Open();
        try
        {
            yield return RunSession1();
        }
        finally
        {
            localDatabase.Dispose();
        }
    }

    private static IEnumerator RunSession1()
    {
        // Certification-owned local state only - never touches unrelated save/profile data. A prior
        // local run of this same test may have left a persisted session or handoff file on disk; clear
        // both so this run genuinely starts from the unauthenticated state it asserts next, matching a
        // real fresh install rather than an artifact of re-running this test locally.
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        DeleteHandoffFileIfPresent();

        string suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
        string username = "unityOnlineCert" + suffix;
        const string password = "CertPass123!";
        string displayName = "Unity Online Cert " + suffix;
        Log($"=== Session1 start === username={username}");

        // ---- Navigate through the player-facing entry point ----
        yield return LoadSceneAndSettle(Constants.SCENE_NAME_level_00_account);
        MonoBehaviour accountManager = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "AccountManager");
        Assert.That(accountManager, Is.Not.Null, "AccountManager was not found in level_00_account");

        Button onlineAccountButton = RealScenePlayModeTestSupport.GetField<Button>(accountManager, "onlineAccountButton");
        Assert.That(onlineAccountButton, Is.Not.Null, "AccountManager.onlineAccountButton was not resolved");

        Log("Clicking the real Online Account button on the Account hub (-> AccountManager.LoadOnlineAccount) ...");
        onlineAccountButton.onClick.Invoke();
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_account_online, 30f,
            "did not navigate to level_00_account_online after clicking the real Online Account button");
        Log("Account-hub navigation PASSING: the real Online Account button loaded level_00_account_online.");

        yield return null;
        yield return null;

        // ---- Register through the real UI ----
        MonoBehaviour controller = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "OnlineAccountController");
        Assert.That(controller, Is.Not.Null, "OnlineAccountController was not found after scene load");

        object onlineUi = RealScenePlayModeTestSupport.GetField(controller, "onlineUi");
        Assert.That(onlineUi, Is.Not.Null, "OnlineAccountController.onlineUi was not resolved");

        GameObject signedOutPanel = RealScenePlayModeTestSupport.GetField<GameObject>(onlineUi, "signedOutPanel");
        GameObject signedInPanel = RealScenePlayModeTestSupport.GetField<GameObject>(onlineUi, "signedInPanel");
        Assert.That(signedOutPanel.activeSelf, Is.True,
            "a fresh account with no persisted session must see the signed-out panel");

        var registerUsername = RealScenePlayModeTestSupport.GetField(onlineUi, "registerUsernameInputField");
        var registerPassword = RealScenePlayModeTestSupport.GetField(onlineUi, "registerPasswordInputField");
        var registerDisplayName = RealScenePlayModeTestSupport.GetField(onlineUi, "registerDisplayNameInputField");
        SetInputFieldText(registerUsername, username);
        SetInputFieldText(registerPassword, password);
        SetInputFieldText(registerDisplayName, displayName);

        Button registerButton = RealScenePlayModeTestSupport.GetField<Button>(onlineUi, "registerButton");
        Assert.That(registerButton, Is.Not.Null, "OnlineAccountUiObjects.registerButton was not resolved");

        // OnlineAccountController.onlineUi's private `coordinator` field is a real, directly-typed
        // Level5.BackendV2.OnlineAccountCoordinator - see the identical note in Session2.
        OnlineAccountCoordinator coordinator =
            RealScenePlayModeTestSupport.GetField<OnlineAccountCoordinator>(controller, "coordinator");
        Assert.That(coordinator, Is.Not.Null, "OnlineAccountController.coordinator was not resolved");

        Log("Clicking the real Register button (-> OnlineAccountCoordinator.Register -> live register) ...");
        registerButton.onClick.Invoke();

        // BackendV2SessionStore.Changed fires Render() synchronously the instant Register() sets the
        // session - i.e. signedInPanel can already be active before OnlineAccountCoordinator.Register's
        // own subsequent self-profile fetch (CompleteAuth -> RefreshProfileCore) has settled. Waiting
        // on the coordinator's own Profile/ProfileError (as Session2 already does for the restored-
        // session case) is what actually proves the live register-then-fetch round trip completed,
        // not merely that the panel toggled.
        yield return WaitUntil(
            () => BackendV2SessionStore.IsAuthenticated && (coordinator.Profile != null || coordinator.ProfileError != null), 30f,
            () => "live registration did not settle within 30s; registerError=\"" +
                  GetTextValue(RealScenePlayModeTestSupport.GetField(onlineUi, "registerError")) + "\"");
        Assert.That(coordinator.ProfileError, Is.Null,
            "the live self-profile fetch failed right after registration: " + coordinator.ProfileError);
        Log("Register (Unity-driven) PASSING: the real Register button created and authenticated a live Backend V2 account.");

        Guid playerId = BackendV2SessionStore.Current.PlayerId;
        Assert.That(playerId, Is.Not.EqualTo(Guid.Empty), "the active session has no PlayerId");

        yield return null; // Render() runs synchronously right after CompleteAuth in the same coroutine step
        Assert.That(signedInPanel.activeSelf, Is.True, "the signed-in panel must be active once registered");

        var displayNameText = RealScenePlayModeTestSupport.GetField(onlineUi, "displayNameText");
        var playerTagText = RealScenePlayModeTestSupport.GetField(onlineUi, "playerTagText");
        string renderedDisplayName = GetTextValue(displayNameText);
        string renderedTag = GetTextValue(playerTagText);
        Assert.That(renderedDisplayName, Is.EqualTo(displayName));
        Assert.That(renderedTag, Is.Not.Null.And.Not.Empty, "the rendered player tag must be non-empty");
        Assert.That(signedOutPanel.activeSelf, Is.False, "the signed-out panel must not be active once registered");
        Assert.That(GetTextValue(RealScenePlayModeTestSupport.GetField(onlineUi, "registerError")), Is.Empty,
            "no registration error should remain after a successful live register");
        Log($"Profile PASSING (live): displayName=\"{renderedDisplayName}\" tag=\"{renderedTag}\" playerId={playerId}.");

        // ---- Prove persistence ----
        bool persisted = BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session persistedSession);
        Assert.That(persisted, Is.True, "no session file was persisted to disk after a successful live register");
        Assert.That(persistedSession.PlayerId, Is.EqualTo(playerId));
        Log("Session persistence PASSING: BackendV2SessionPersistenceStore wrote a real disk file matching the new account.");

        WriteHandoffFile(new HandoffData
        {
            playerId = playerId.ToString(),
            displayName = renderedDisplayName,
            tag = renderedTag,
            username = username,
        });
        Log($"Session1 complete: wrote non-secret handoff file to {HandoffPath}. Process will now exit so " +
            "Session2 can certify a genuine restart.");
    }

    // ================================================================= Session 2

    /// <summary>
    /// A genuinely separate <c>Unity.exe</c> process from Session1's. Never clears the persisted
    /// session, never registers or logs in: loads the real first production scene
    /// (<c>level_00_account_loginLocal</c>) and lets <c>UserAccountManager.Awake -&gt;
    /// BackendV2SessionPersistenceBootstrap.EnsureInitialized -&gt; BackendV2SessionPersistenceStore.
    /// TryLoad -&gt; BackendV2SessionStore.Set</c> restore Session1's session - the actual process-restart
    /// proof, not a same-process scene reload standing in for one (that static bootstrap only ever runs
    /// once per process). Then re-opens the online-account screen through the Account hub again, waits
    /// for the real self-profile fetch to confirm the same live player, signs out through the real Sign
    /// Out button, confirms the persisted session is gone, and confirms Backend V2 itself rejects the
    /// old refresh token afterward.
    /// </summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator Session2_RestoreAndSignOut()
    {
        RequireLiveCertificationOptIn();
        ApplyConfig(ResolveConfig());

        // Cleanup (handoff file, local session state, config override) runs in `finally` so a failed
        // assertion anywhere below still leaves this process's local certification state clean - e.g.
        // a stale handoff file must never survive to confuse a later, independent Session2-only rerun.
        LocalProfileTestDatabase localDatabase = new LocalProfileTestDatabase();
        yield return localDatabase.Open();
        try
        {
            yield return RunSession2(localDatabase);
        }
        finally
        {
            localDatabase.Dispose();
            DeleteHandoffFileIfPresent();
            BackendV2SessionPersistenceStore.Clear();
            BackendV2SessionStore.Clear();
            BackendV2ApiConfigProvider.Reset();
            Log("Session2 cleanup: handoff file deleted, local session state cleared, config override reset.");
        }
    }

    private static IEnumerator RunSession2(LocalProfileTestDatabase localDatabase)
    {
        Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False,
            "this test must run as a fresh Unity process with no in-memory session - if this fails, " +
            "Session1 and Session2 ran in the same process, which does not certify a genuine restart");

        HandoffData handoff = ReadHandoffFile();
        Assert.That(handoff, Is.Not.Null,
            "no handoff file found at " + HandoffPath + " - Session1 must run (and complete successfully) first");
        Guid expectedPlayerId = Guid.Parse(handoff.playerId);
        Log($"Loaded handoff: playerId={expectedPlayerId} displayName=\"{handoff.displayName}\" tag=\"{handoff.tag}\"");

        // ---- Restore through production startup ----
        yield return LoadSceneAndSettle(Constants.SCENE_NAME_level_00_account_loginLocal);

        yield return WaitUntil(() => BackendV2SessionStore.IsAuthenticated, 30f,
            "Phase D FAILING: the persisted Backend V2 session (Application.persistentDataPath/" +
            "backendv2_session.json, written by Session1's real Register click) was not restored by " +
            "UserAccountManager.Awake in this fresh process within 30s");
        Assert.That(BackendV2SessionStore.Current.PlayerId, Is.EqualTo(expectedPlayerId),
            "the restored session's PlayerId does not match Session1's handoff value");
        Log("Phase D PASSING: a genuinely fresh Unity process restored the persisted Backend V2 session " +
            "through UserAccountManager.Awake -> BackendV2SessionPersistenceBootstrap.EnsureInitialized " +
            "(local-only, zero network requests) and its PlayerId matches Session1's.");

        // ---- Local profile changes must never touch the restored online session ----
        yield return RunLocalProfileIndependenceCheck(localDatabase);

        // ---- Navigate through Account hub again ----
        yield return LoadSceneAndSettle(Constants.SCENE_NAME_level_00_account);
        MonoBehaviour accountManager = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "AccountManager");
        Assert.That(accountManager, Is.Not.Null, "AccountManager was not found in level_00_account");

        Button onlineAccountButton = RealScenePlayModeTestSupport.GetField<Button>(accountManager, "onlineAccountButton");
        Assert.That(onlineAccountButton, Is.Not.Null, "AccountManager.onlineAccountButton was not resolved");

        Log("Clicking the real Online Account button again (restored session, no credentials entered) ...");
        onlineAccountButton.onClick.Invoke();
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_account_online, 30f,
            "did not navigate to level_00_account_online after clicking the real Online Account button");

        yield return null;
        yield return null;

        MonoBehaviour controller = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "OnlineAccountController");
        Assert.That(controller, Is.Not.Null, "OnlineAccountController was not found after scene load");

        object onlineUi = RealScenePlayModeTestSupport.GetField(controller, "onlineUi");
        Assert.That(onlineUi, Is.Not.Null, "OnlineAccountController.onlineUi was not resolved");

        GameObject signedOutPanel = RealScenePlayModeTestSupport.GetField<GameObject>(onlineUi, "signedOutPanel");
        GameObject signedInPanel = RealScenePlayModeTestSupport.GetField<GameObject>(onlineUi, "signedInPanel");
        Assert.That(signedOutPanel.activeSelf, Is.False,
            "the screen must recognize the restored session without requiring username/password entry");
        Assert.That(signedInPanel.activeSelf, Is.True, "the signed-in panel must be active for a restored session");

        // OnlineAccountController's own private `coordinator` field is a real, directly-typed
        // Level5.BackendV2.OnlineAccountCoordinator (that namespace IS referenced by this assembly) -
        // reflection is needed only to reach the field itself, not to interpret its value. Waiting on
        // coordinator.Profile is the real OnlineAccountCoordinator.EnterScreen() -> GET
        // api/v2/players/me/profile round trip completing against the live backend, not merely a UI
        // text read.
        OnlineAccountCoordinator coordinator =
            RealScenePlayModeTestSupport.GetField<OnlineAccountCoordinator>(controller, "coordinator");
        Assert.That(coordinator, Is.Not.Null, "OnlineAccountController.coordinator was not resolved");

        yield return WaitUntil(() => coordinator.Profile != null || coordinator.ProfileError != null, 30f,
            () => "OnlineAccountCoordinator.EnterScreen()'s self-profile fetch did not settle within 30s; " +
                  "profileError=\"" + coordinator.ProfileError + "\"");
        Assert.That(coordinator.ProfileError, Is.Null,
            "the restored session's self-profile fetch failed live: " + coordinator.ProfileError);
        Assert.That(coordinator.Profile.PlayerId, Is.EqualTo(expectedPlayerId));
        Assert.That(coordinator.Profile.DisplayName, Is.EqualTo(handoff.displayName));
        Assert.That(coordinator.Profile.Tag, Is.EqualTo(handoff.tag));
        Log("Self-profile fetch PASSING (live): the restored credential's GET api/v2/players/me/profile " +
            "returned the same player Session1 registered, proving the restored session still works " +
            "against the real backend (not merely that a JSON file could be read).");

        yield return null; // Render() runs synchronously right after EnterScreen() in the same coroutine step
        Assert.That(GetTextValue(RealScenePlayModeTestSupport.GetField(onlineUi, "displayNameText")),
            Is.EqualTo(handoff.displayName));
        Assert.That(GetTextValue(RealScenePlayModeTestSupport.GetField(onlineUi, "playerTagText")),
            Is.EqualTo(handoff.tag));
        Log("Phase D / restoration PASSING: the real online-account screen renders the same live " +
            "displayName/tag after a genuine process restart.");

        // ---- Sign out through the real UI ----
        string oldRefreshToken = BackendV2SessionStore.Current.RefreshToken;
        Assert.That(oldRefreshToken, Is.Not.Null.And.Not.Empty);

        Button signOutButton = RealScenePlayModeTestSupport.GetField<Button>(onlineUi, "signOutButton");
        Assert.That(signOutButton, Is.Not.Null, "OnlineAccountUiObjects.signOutButton was not resolved");

        Log("Clicking the real Sign Out button (-> OnlineAccountCoordinator.SignOut -> live logout) ...");
        signOutButton.onClick.Invoke();

        yield return WaitUntil(
            () => !BackendV2SessionStore.IsAuthenticated && signedOutPanel.activeSelf, 30f,
            "sign-out did not complete within 30s of a real Sign Out click");
        Log("Sign Out PASSING (live): the real Sign Out button cleared the in-memory session.");

        bool stillPersisted = BackendV2SessionPersistenceStore.TryLoad(out _);
        Assert.That(stillPersisted, Is.False,
            "the persisted session must not be restorable after a real Sign Out click");
        Log("Session-file deletion PASSING: the persisted primary/backup session cannot restore on the next launch.");

        // ---- Verify backend refresh-token revocation ----
        ApiResponse<AccessTokenResponseDto> refreshResult = null;
        yield return BackendV2Runtime.Auth.Refresh(new RefreshRequestDto(oldRefreshToken), r => refreshResult = r);
        Assert.That(refreshResult, Is.Not.Null);
        Assert.That(refreshResult.Success, Is.False,
            "Backend V2 must reject the old refresh token after a real logout, but the refresh succeeded");
        Assert.That(refreshResult.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated),
            "expected Unauthenticated for a revoked refresh token; got " + refreshResult.ErrorKind);
        Log("Refresh-token revocation PASSING (live): POST api/v2/auth/logout -> LogoutUseCase -> " +
            "AuthSession.Revoke rejected the old refresh token with Unauthenticated.");
        Log("Session2 complete.");
    }

    /// <summary>
    /// The identity-independence half of the certification: with a Backend V2 session restored by a
    /// genuinely fresh process, every local-profile operation a player can perform - continuing as guest
    /// through the real guest row, then creating a local profile through the real Create Local Profile
    /// flow - must leave that session exactly as it was: same object, same tokens, still authenticated,
    /// and no <see cref="BackendV2SessionStore.Changed"/> event (a refresh, replacement or sign-out would
    /// each raise one). The local database is the throwaway one this session opened, never the real one.
    /// </summary>
    private static IEnumerator RunLocalProfileIndependenceCheck(LocalProfileTestDatabase localDatabase)
    {
        BackendV2Session restored = BackendV2SessionStore.Current;
        Assert.That(restored, Is.Not.Null, "the independence check needs the restored online session");
        string accessTokenBefore = restored.AccessToken;
        string refreshTokenBefore = restored.RefreshToken;

        int changes = 0;
        Action<BackendV2Session> onChanged = session => changes++;
        BackendV2SessionStore.Changed += onChanged;
        int previousUserId = Level5.Core.LocalAccountIdentity.UserId;
        string previousUserName = Level5.Core.LocalAccountIdentity.UserName;

        // The loading scenes a local selection proceeds into log unrelated noise.
        LogAssert.ignoreFailingMessages = true;
        try
        {
            // ---- guest, through the real guest row ----
            List<Button> rows = null;
            yield return LocalProfileUiFlow.WaitForProfileRows(1, r => rows = r);
            Assert.That(LocalProfileUiFlow.RowName(rows[0]), Is.EqualTo("guest"),
                "an empty local database must offer the guest row");
            yield return LocalProfileUiFlow.SelectProfileRow(rows[0]);
            Assert.That(Level5.Core.LocalAccountIdentity.UserId, Is.EqualTo(74));
            Assert.That(Level5.Core.LocalAccountIdentity.UserName, Is.EqualTo("guest"));
            AssertRestoredSessionUntouched(restored, accessTokenBefore, refreshTokenBefore, changes, "selecting guest");
            yield return LocalProfileUiFlow.WaitUntil(
                () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start, 60f,
                "loading did not finish after selecting guest");
            Log("Independence PASSING (live): selecting guest through the real guest row left the restored " +
                "Backend V2 session untouched (same object, same tokens, no Changed event).");

            // ---- a new local profile, through the real Create Local Profile flow ----
            int createdUserId = 0;
            string profileName = "liveCert" + Guid.NewGuid().ToString("N").Substring(0, 8);
            yield return LocalProfileUiFlow.CreateProfileThroughHub(localDatabase, profileName, id => createdUserId = id);
            Assert.That(createdUserId, Is.GreaterThan(0));
            Assert.That(Level5.Core.LocalAccountIdentity.UserId, Is.EqualTo(createdUserId));
            Assert.That(Level5.Core.LocalAccountIdentity.UserName, Is.EqualTo(profileName));
            AssertRestoredSessionUntouched(restored, accessTokenBefore, refreshTokenBefore, changes, "creating a local profile");
            yield return LocalProfileUiFlow.WaitUntil(
                () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start, 60f,
                "loading did not finish after creating a local profile");
            Log("Independence PASSING (live): creating and selecting a local profile through the real UI left " +
                "the restored Backend V2 session untouched (same object, same tokens, no Changed event).");
        }
        finally
        {
            BackendV2SessionStore.Changed -= onChanged;
            LogAssert.ignoreFailingMessages = false;
            Level5.Core.LocalAccountIdentity.UserId = previousUserId;
            Level5.Core.LocalAccountIdentity.UserName = previousUserName;
        }
    }

    private static void AssertRestoredSessionUntouched(
        BackendV2Session restored, string accessTokenBefore, string refreshTokenBefore, int changeCount, string operation)
    {
        Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True, operation + " signed the online player out");
        Assert.That(BackendV2SessionStore.Current, Is.SameAs(restored), operation + " replaced the Backend V2 session");
        Assert.That(BackendV2SessionStore.Current.AccessToken, Is.EqualTo(accessTokenBefore));
        Assert.That(BackendV2SessionStore.Current.RefreshToken, Is.EqualTo(refreshTokenBefore));
        Assert.That(changeCount, Is.EqualTo(0), operation + " changed the Backend V2 session");
    }

    // ================================================================= shared helpers

    private static void RequireLiveCertificationOptIn()
    {
        if (Environment.GetEnvironmentVariable(EnabledEnvVar) != "1")
        {
            Assert.Ignore(
                "Live Unity-client online-account certification (issue #194) is opt-in and skipped by " +
                "default so ordinary -runTests runs never depend on a live Backend V2 instance. Set " +
                EnabledEnvVar + "=1 (and, if needed, " + BaseUriEnvVar + ") and run against a live, " +
                "reachable Backend V2 instance.");
        }
    }

    /// <summary>Same seam <c>BackendV2LiveCorrespondenceCertificationTests.ApplyConfig</c> uses: both
    /// the config provider AND BackendV2Runtime must be reset together, since
    /// UnityWebRequestTransport captures its BackendV2ApiConfig by value at construction.</summary>
    private static void ApplyConfig(BackendV2ApiConfig config)
    {
        BackendV2ApiConfigProvider.Override(config);
        BackendV2Runtime.Reset();
    }

    private static BackendV2ApiConfig ResolveConfig()
    {
        string overrideUri = Environment.GetEnvironmentVariable(BaseUriEnvVar);
        if (string.IsNullOrWhiteSpace(overrideUri))
        {
            return BackendV2ApiConfig.Development();
        }

        BackendV2Environment env = BackendV2Environment.Development;
        string rawEnv = Environment.GetEnvironmentVariable(BaseUriEnvironmentEnvVar);
        if (!string.IsNullOrWhiteSpace(rawEnv))
        {
            Enum.TryParse(rawEnv, ignoreCase: true, out env);
        }

        return BackendV2ApiConfig.Custom(new Uri(overrideUri), env);
    }

    private static IEnumerator LoadSceneAndSettle(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;
        yield return null;
        yield return null;
    }

    /// <summary>
    /// TMP_InputField/TMP_Text (TextMeshPro package) are not referenced by
    /// <c>Level5.PlayModeTests.asmdef</c> at all - unlike <c>Button</c>/<c>GameObject</c> (core
    /// UnityEngine/UnityEngine.UI, already referenced), so both are read purely via reflection on
    /// their common "text" property rather than adding a new package reference just for this fixture.
    /// </summary>
    private static void SetInputFieldText(object inputField, string text)
    {
        inputField.GetType().GetProperty("text").SetValue(inputField, text);
    }

    private static string GetTextValue(object tmpText)
    {
        return tmpText == null ? null : (string)tmpText.GetType().GetProperty("text").GetValue(tmpText);
    }

    private static void DeleteHandoffFileIfPresent()
    {
        try
        {
            if (File.Exists(HandoffPath))
            {
                File.Delete(HandoffPath);
            }
        }
        catch (Exception exception)
        {
            Log("WARNING: failed to delete handoff file: " + exception);
        }
    }

    private static void WriteHandoffFile(HandoffData data)
    {
        File.WriteAllText(HandoffPath, JsonUtility.ToJson(data));
    }

    private static HandoffData ReadHandoffFile()
    {
        if (!File.Exists(HandoffPath))
        {
            return null;
        }

        string json = File.ReadAllText(HandoffPath);
        return JsonUtility.FromJson<HandoffData>(json);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string timeoutMessage)
    {
        yield return WaitUntil(condition, timeoutSeconds, () => timeoutMessage);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, Func<string> timeoutMessage)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start >= timeoutSeconds)
            {
                Assert.Fail(timeoutMessage());
            }

            yield return null;
        }
    }

    private static void Log(string message)
    {
        Debug.Log("[BackendV2OnlineAccountLiveCert] " + message);
        File.AppendAllText(EvidencePath, message + Environment.NewLine);
    }

    [Serializable]
    private sealed class HandoffData
    {
        public string playerId;
        public string displayName;
        public string tag;
        public string username;
    }
}
