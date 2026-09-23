using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Opt-in, live-backend Unity-client certification for issue #159's remaining Unity-client-side gap
/// (docs/backend-v2-correspondence-certification.md, "Remaining Backend V2 correspondence work").
///
/// <c>Assets/Level5/Editor/BackendV2LiveCertificationRunner.cs</c>'s <c>-executeMethod</c> +
/// <c>EditorApplication.isPlaying = true</c> entry point hung in a reproducible Editor-startup
/// deadlock in this environment (see that class's own doc comment and the certification doc's
/// "Unity-client live-certification attempt" section) - <c>before</c> its own logic ever ran, i.e. a
/// deadlock in that specific non-<c>-runTests</c> entry path, not in Play Mode or real networking
/// generally (the certification doc's own re-validation already shows <c>-runTests -testPlatform
/// PlayMode</c> runs cleanly to completion in the same environment, including a real player loop
/// with real <c>UnityWebRequest</c> calls). This file reuses that runner's driving technique
/// (reflection-driven <c>Button.onClick</c>/<c>InputField.text</c> against the real production
/// <c>CorrespondenceScreenController</c>) but as ordinary <c>[UnityTest]</c> methods on the known-
/// reliable path instead.
///
/// Skipped (<c>Assert.Ignore</c>) unless <c>LEVEL5_LIVE_CERTIFICATION=1</c> is set, so ordinary
/// <c>-runTests</c> runs never depend on a live Backend V2 instance. Run each method as its own
/// <c>Unity.exe</c> invocation (see each method's own doc comment for why):
///
///   set LEVEL5_LIVE_CERTIFICATION=1
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode ^
///       -testFilter BackendV2LiveCorrespondenceCertificationTests.Session1_EstablishSeriesAndCompleteGameOneViaGameRules ^
///       -testResults session1_results.xml -logFile session1.log
///
/// Optional env vars (read at runtime, matching <c>BackendV2LiveCertificationRunner</c>'s own):
/// <c>LEVEL5_BACKENDV2_BASE_URI</c> (defaults to <c>BackendV2ApiConfig.Development()</c>'s
/// <c>https://localhost:7029/</c>) and <c>LEVEL5_BACKEND_REPO_PATH</c> (defaults to assuming
/// <c>Level5Backend</c> is a sibling checkout of <c>level5</c>).
///
/// Session1 and Session2 are meant to run as two SEPARATE <c>Unity.exe</c> invocations (issue #159's
/// Phase D: a genuine process restart, not a scene reload - see each method's own doc comment).
/// Nothing is shared between them in memory; only two things on disk carry state across the process
/// boundary, both already-real production/tooling paths: the real
/// <c>Application.persistentDataPath/backendv2_session.json</c> session file (Session1's real Sign-In
/// click writes it via the real <c>BackendV2SessionPersistenceBootstrap</c>; Session2 never logs in,
/// only restores it), and the standalone HttpClient counterpart harness's own <c>%TEMP%</c> state
/// file (<c>Level5Backend/v2/scripts/live-certification</c>, which plays "Account B" - the isolated
/// second client issue #159 explicitly permits in place of a second Unity install).
///
/// Drives the real default-assembly <c>CorrespondenceScreenController</c>/<c>GameRules</c>/
/// <c>GameLevelManager</c> production types via reflection only: a named assembly definition (this
/// one) can never reference Unity's implicit default assembly, a hard engine restriction, not a
/// project choice (see <c>Level5BackendV2CorrespondenceScenePlayModeTests.cs</c>'s own doc comment
/// for the same rule). Every <c>Level5.BackendV2.*</c> type is referenced directly instead -
/// <c>Level5.PlayModeTests.asmdef</c> takes an additive, test-only reference to
/// <c>Level5.BackendV2</c> for this file.
///
/// No new production API surface was added for this fixture. <c>GameRules.RequestGameOver()</c> and
/// <c>GameStats.TotalPoints</c> are pre-existing, already-public production members (the same "give
/// up"/scoring seams a real menu button and real gameplay already use) - deterministic test
/// statistics are established by setting the latter via reflection (needed only because the type
/// lives in the default assembly) before calling the former, exactly as issue #159's own certification
/// instructions anticipate ("an existing public seam or test-only reflection to request match
/// completion or establish deterministic test statistics").
/// </summary>
public class BackendV2LiveCorrespondenceCertificationTests
{
    private const string SceneNameCorrespondence = "level_00_multiplayer";
    private const string EnabledEnvVar = "LEVEL5_LIVE_CERTIFICATION";
    private const string BackendRepoPathEnvVar = "LEVEL5_BACKEND_REPO_PATH";
    private const string BaseUriEnvVar = "LEVEL5_BACKENDV2_BASE_URI";
    private const string BaseUriEnvironmentEnvVar = "LEVEL5_BACKENDV2_ENVIRONMENT";
    private const int CounterpartTimeoutSeconds = 120;

    private static readonly string EvidencePath =
        Path.Combine(Path.GetTempPath(), "level5_unity_live_cert_evidence.log");

    // ================================================================= Session 1

    /// <summary>
    /// Phase A + Phase B. Registers Account A directly against a live Backend V2, drives the real
    /// correspondence UI (login, all six tabs, friend accept, challenge accept) through real
    /// <c>Button.onClick</c>/<c>InputField.text</c>, launches a remote attempt through the real
    /// <c>Play</c> button (<c>RemoteAttemptLauncher.Run</c> -&gt; <c>SceneTransition.LoadScene</c>),
    /// then certifies the actual match-completion path: sets deterministic winning stats on the real
    /// gameplay scene's <c>GameStats</c> and calls the real, pre-existing, public
    /// <c>GameRules.RequestGameOver()</c> - never a new seam - and waits for the real
    /// <c>GameRules.HandleMatchEnded -&gt; RemoteAttemptResultSubmitter -&gt; CompleteAttempt</c> chain
    /// to actually reach the live server. Hands off to the standalone HttpClient counterpart harness
    /// for Account B's own game-1 turn so the series genuinely advances to game 2, then leaves the
    /// process's real session file on disk for Session2 to restore.
    /// </summary>
    [UnityTest]
    [Timeout(600000)]
    public IEnumerator Session1_EstablishSeriesAndCompleteGameOneViaGameRules()
    {
        RequireLiveCertificationOptIn();
        ApplyConfig(ResolveConfig());

        // A prior local run of this same test may have left a persisted session file on disk (the
        // same real file Session2 deliberately relies on for Phase D) - clear it so THIS run
        // genuinely starts from the unauthenticated state it asserts next, matching a real fresh
        // install rather than an artifact of re-running this test locally.
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();

        string suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
        string usernameA = "unityCertA" + suffix;
        const string passwordA = "CertPass123!";
        string displayNameA = "Unity Cert Account A " + suffix;
        Log($"=== Session1 start === usernameA={usernameA}");

        // Raw client-level register (not BackendV2Runtime.Session.Register, which would also set
        // BackendV2SessionStore immediately) - keeps the session store empty so the first scene load
        // below genuinely exercises the unauthenticated -> login-panel path, and the Sign In click
        // that follows is what actually authenticates, not this call.
        ApiResponse<AccessTokenResponseDto> registerResult = null;
        yield return BackendV2Runtime.Auth.Register(
            new RegisterRequestDto(usernameA, passwordA, displayNameA), r => registerResult = r);
        Assert.That(registerResult != null && registerResult.Success, Is.True,
            "live account registration failed: " + Describe(registerResult));
        Log($"Account A registered live: playerId={registerResult.Value.PlayerId}");

        yield return BootstrapFromStartScreenIntoCorrespondence();
        object controller = FindController();
        Assert.That(controller, Is.Not.Null, "CorrespondenceScreen not found after scene load");

        GameObject loginPanel = (GameObject)GetMember(controller, "loginPanel");
        Assert.That(loginPanel.activeSelf, Is.True,
            "a fresh account with no persisted session must see the login panel");

        InputField loginUsername = (InputField)GetMember(controller, "loginUsername");
        InputField loginPassword = (InputField)GetMember(controller, "loginPassword");
        loginUsername.text = usernameA;
        loginPassword.text = passwordA;
        Button signInButton = FindButton(loginPanel.transform, "Sign InButton");
        Assert.That(signInButton, Is.Not.Null, "'Sign InButton' not found under LoginPanel");
        Log("Clicking the real Sign In button (Button.onClick -> DoLogin -> BackendV2Runtime.Session.Login) ...");
        signInButton.onClick.Invoke();

        yield return WaitUntil(() => !loginPanel.activeSelf, 30f,
            "sign-in did not complete live within 30s");
        Log("Login (Unity-driven) PASSING: the real Sign In button authenticated live against Backend V2.");

        bool persisted = BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session persistedSession);
        Assert.That(persisted, Is.True, "no session file was persisted to disk after a successful live login");
        Assert.That(persistedSession.PlayerId, Is.EqualTo(registerResult.Value.PlayerId));
        Log("Session persistence PASSING: BackendV2SessionPersistenceStore wrote a real disk file matching Account A.");

        ApiResponse<PlayerProfileResponseDto> meResult = null;
        yield return BackendV2Runtime.Players.UpdateMe(displayNameA, r => meResult = r);
        Assert.That(meResult != null && meResult.Success, Is.True, "tag discovery failed: " + Describe(meResult));
        string tagA = meResult.Value.Tag;
        Log($"Account A tag (live): {tagA}");

        yield return DumpAllTabs(controller, "before counterpart (Account B) actions");

        var friendStep = RunCounterpart("friend \"" + tagA + "\"");
        Assert.That(friendStep.ok, Is.True, "counterpart 'friend' step failed:\n" + friendStep.output);
        Log("Counterpart sent a live friend request to Account A.");

        // Reload the scene (close/reopen) - Resume() must restore the session without a fresh login.
        yield return LoadSceneAndSettle();
        controller = FindController();
        Assert.That(controller, Is.Not.Null, "CorrespondenceScreen not found after scene reload");
        loginPanel = (GameObject)GetMember(controller, "loginPanel");
        Assert.That(loginPanel.activeSelf, Is.False,
            "Scenario 3 (Unity-driven Resume half) FAILING: the login panel reappeared after reopening " +
            "despite a session file having been persisted moments earlier");
        Log("Scenario 3 (Unity-driven Resume half) PASSING: the persisted session was restored live and " +
            "the login panel was skipped.");

        yield return SelectTabAndWait(controller, "FriendsButton");
        yield return WaitUntilButtonAppears(controller, "AcceptButton", 20f);
        RectTransform contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Button acceptFriendButton = FindButton(contentRoot, "AcceptButton");
        Assert.That(acceptFriendButton, Is.Not.Null,
            "Account B's incoming friend request did not render within 20s:\n" + DumpContentRoot(controller));
        Log("Friends tab PASSING: Account B's live incoming friend request rendered from a real server round trip.");
        acceptFriendButton.onClick.Invoke();
        yield return WaitSeconds(2f);

        var challengeStep = RunCounterpart("challenge");
        Assert.That(challengeStep.ok, Is.True, "counterpart 'challenge' step failed:\n" + challengeStep.output);
        Log("Counterpart created a live Bo3 challenge to Account A.");

        yield return SelectTabAndWait(controller, "IncomingButton");
        yield return WaitUntilButtonAppears(controller, "AcceptButton", 20f);
        contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Button acceptChallengeButton = FindButton(contentRoot, "AcceptButton");
        Assert.That(acceptChallengeButton, Is.Not.Null,
            "Account B's live challenge did not render within 20s:\n" + DumpContentRoot(controller));
        Log("Incoming Challenges tab PASSING: Account B's live challenge rendered from a real server round trip.");
        acceptChallengeButton.onClick.Invoke();
        yield return WaitSeconds(2f);

        yield return DumpAllTabs(controller, "after friend + challenge accepted live");

        yield return SelectTabAndWait(controller, "Your TurnButton");
        yield return WaitUntilButtonAppears(controller, "PlayButton", 20f);
        contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Button playButton = FindButton(contentRoot, "PlayButton");
        Assert.That(playButton, Is.Not.Null,
            "Your Turn tab did not show a Play action within 20s:\n" + DumpContentRoot(controller));
        Log("Your Turn tab PASSING: the active series + Play action rendered from a real server round trip.");

        string sceneBeforeLaunch = SceneManager.GetActiveScene().name;
        Log("Clicking the real Play button (-> RemoteAttemptLauncher.Run -> StartAttempt -> " +
            "RemoteAttemptDescriptorMapper.Map -> ActiveMatch.Begin -> SceneTransition.LoadScene) ...");
        ExpectKnownCharacterProfileLimitationLog();
        playButton.onClick.Invoke();
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name != sceneBeforeLaunch ||
                  !string.IsNullOrEmpty(GetText(controller, "statusBanner")) &&
                  GetText(controller, "statusBanner").Contains("could not start the attempt"),
            30f,
            () => "no scene transition observed after a real Play click; statusBanner=\"" +
                  GetText(controller, "statusBanner") + "\"");
        Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo(sceneBeforeLaunch),
            "Play click did not launch: statusBanner=\"" + GetText(controller, "statusBanner") + "\"");
        Log("Remote-attempt launch PASSING: a real Play click drove StartAttempt through " +
            "SceneTransition.LoadScene against the live backend.");

        yield return WaitForGameplayReady();
        SetDeterministicScore(winning: true);
        InvokeRequestGameOver();
        Log("Called the real, pre-existing, public GameRules.RequestGameOver() with deterministic winning " +
            "stats set on the real gameplay scene's GameStats.");

        yield return WaitUntil(
            () => !ActiveRemoteAttempt.IsActive && !PendingRemoteAttemptResult.HasPending, 30f,
            "GameRules.HandleMatchEnded -> RemoteAttemptResultSubmitter -> CompleteAttempt did not " +
            "succeed live within 30s (ActiveRemoteAttempt still active or a result is still pending)");
        Log("Phase B PASSING: the real GameRules match-end path submitted game 1's result to the live " +
            "Backend V2 CompleteAttempt endpoint and it was accepted.");

        ApiResponse<SeriesSummaryPageDto> activeBefore = null;
        yield return BackendV2Runtime.Correspondence.ListActive(20, null, r => activeBefore = r);
        Assert.That(activeBefore != null && activeBefore.Success, Is.True, "ListActive failed: " + Describe(activeBefore));
        Guid seriesId = activeBefore.Value.Items.Single().Id;
        Log($"seriesId={seriesId}");

        var playTurn1 = RunCounterpart("playturn 1 lose");
        Assert.That(playTurn1.ok, Is.True, "counterpart 'playturn 1 lose' step failed:\n" + playTurn1.output);
        Log("Counterpart (Account B) completed game 1's attempt live (losing).");

        ApiResponse<SeriesResponseDto> seriesAfterGame1 = null;
        yield return BackendV2Runtime.Correspondence.Get(seriesId, r => seriesAfterGame1 = r);
        Assert.That(seriesAfterGame1 != null && seriesAfterGame1.Success, Is.True,
            "Get(series) after game 1 failed: " + Describe(seriesAfterGame1));
        Assert.That(seriesAfterGame1.Value.Status, Is.EqualTo("Active"));
        Assert.That(seriesAfterGame1.Value.CurrentGameNumber, Is.EqualTo(2),
            "game 1 must have resolved (both attempts complete) and the series moved to game 2");
        Log("Series PASSING: game 1 resolved live with both real (Unity) and counterpart (HttpClient) " +
            "attempts; series correctly advanced to game 2. Session1 complete; process will now exit so " +
            "Session2 can certify a genuine restart.");
    }

    // ================================================================= Session 2

    /// <summary>
    /// Phase D + Phase C + Phase E, run as a genuinely separate <c>Unity.exe</c> process from
    /// Session1's (fresh process statics - this method never runs in the same process as Session1's).
    /// Never logs in: opens the correspondence screen and lets the real
    /// <c>BackendV2SessionPersistenceStore.TryLoad -&gt; BackendV2SessionStore.Set -&gt; ForceRefresh
    /// -&gt; RefreshAll</c> chain restore the session Session1 persisted to disk (Phase D). Then plays
    /// game 2 through the real Play button, injects a controlled transport failure (an unreachable
    /// base URI, via the same <c>BackendV2ApiConfigProvider.Override</c> configuration seam
    /// <c>BackendV2LiveCertificationRunner</c> already uses) right before ending the match, confirms
    /// the failed result is represented by <c>PendingRemoteAttemptResult</c>, restores the real
    /// endpoint, reopens the correspondence screen, and clicks the real "Resend result" button
    /// (Phase C). Finally confirms the Bo3 completed, Active no longer lists it, and the real
    /// Completed tab does (Phase E).
    /// </summary>
    [UnityTest]
    [Timeout(600000)]
    public IEnumerator Session2_ResumeAfterRestartRetryAndCompleteSeries()
    {
        RequireLiveCertificationOptIn();
        ApplyConfig(ResolveConfig());

        Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False,
            "this test must run as a fresh Unity process with no in-memory session - if this fails, " +
            "Session1 and Session2 ran in the same process, which does not certify a genuine restart");

        yield return BootstrapFromStartScreenIntoCorrespondence();
        object controller = FindController();
        Assert.That(controller, Is.Not.Null, "CorrespondenceScreen not found after scene load");

        GameObject loginPanel = (GameObject)GetMember(controller, "loginPanel");
        yield return WaitUntil(() => !loginPanel.activeSelf, 30f,
            "Phase D FAILING: the login panel is still showing 30s after scene load - the persisted " +
            "session (Application.persistentDataPath/backendv2_session.json, written by Session1's real " +
            "Sign-In click) was not restored by this fresh process");
        Log("Phase D PASSING: a genuinely fresh Unity process restored the persisted Backend V2 session " +
            "(BackendV2SessionPersistenceStore.TryLoad -> BackendV2SessionStore.Set -> ForceRefresh -> " +
            "RefreshAll) and skipped the login panel entirely.");

        Guid playerIdA = BackendV2SessionStore.Current.PlayerId;
        ApiResponse<SeriesSummaryPageDto> activeResult = null;
        yield return BackendV2Runtime.Correspondence.ListActive(20, null, r => activeResult = r);
        Assert.That(activeResult != null && activeResult.Success, Is.True, "ListActive failed: " + Describe(activeResult));
        SeriesSummaryDto activeSeries = activeResult.Value.Items.Single();
        Guid seriesId = activeSeries.Id;
        Assert.That(activeSeries.CurrentGameNumber, Is.EqualTo(2),
            "the series restored from Backend V2 must still be at game 2, continuing where Session1 left it");
        Log($"Phase D PASSING: fetched current series state from Backend V2 after restart - seriesId={seriesId}, " +
            "currentGameNumber=2, continuing the same series Session1 established.");

        var playTurn2 = RunCounterpart("playturn 2 lose");
        Assert.That(playTurn2.ok, Is.True, "counterpart 'playturn 2 lose' step failed:\n" + playTurn2.output);
        Log("Counterpart (Account B) completed game 2's attempt live (losing) ahead of Account A's turn.");

        yield return SelectTabAndWait(controller, "Your TurnButton");
        yield return WaitUntilButtonAppears(controller, "PlayButton", 20f);
        RectTransform contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Button playButton = FindButton(contentRoot, "PlayButton");
        Assert.That(playButton, Is.Not.Null,
            "Your Turn tab did not show a Play action for game 2 within 20s:\n" + DumpContentRoot(controller));

        string sceneBeforeLaunch = SceneManager.GetActiveScene().name;
        Log("Clicking the real Play button for game 2 ...");
        ExpectKnownCharacterProfileLimitationLog();
        playButton.onClick.Invoke();
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name != sceneBeforeLaunch ||
                  !string.IsNullOrEmpty(GetText(controller, "statusBanner")) &&
                  GetText(controller, "statusBanner").Contains("could not start the attempt"),
            30f,
            () => "no scene transition observed after a real Play click for game 2; statusBanner=\"" +
                  GetText(controller, "statusBanner") + "\"");
        Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo(sceneBeforeLaunch),
            "Play click for game 2 did not launch: statusBanner=\"" + GetText(controller, "statusBanner") + "\"");
        yield return WaitForGameplayReady();

        // ---- Phase C: inject a controlled transport failure before ending the match ----
        Uri unreachable = new Uri("https://127.0.0.1:9/");
        ApplyConfig(BackendV2ApiConfig.Custom(unreachable, BackendV2Environment.Development, requestTimeoutSeconds: 5));
        Log($"Phase C: pointed Backend V2 client at an intentionally unreachable endpoint ({unreachable}).");

        // The production submitter logs this failure for real (RemoteAttemptResultSubmitter.Submit) -
        // it is the deliberate point of this phase, not an unexpected error, but Unity's strict
        // PlayMode test runner still fails the test on any unhandled Debug.LogError.
        LogAssert.Expect(LogType.Error, new Regex(@"^Submitting the remote attempt .* result failed: Network.*"));
        SetDeterministicScore(winning: true);
        InvokeRequestGameOver();
        Log("Ended the match via the real GameRules.RequestGameOver() while the transport is unreachable.");

        yield return WaitUntil(() => PendingRemoteAttemptResult.HasPending, 30f,
            "Phase C FAILING: the result submission did not fail/stash as pending within 30s against the " +
            "unreachable endpoint");
        RemoteAttemptContext pendingContext = PendingRemoteAttemptResult.Context;
        var pendingMetrics = PendingRemoteAttemptResult.Metrics;
        Assert.That(pendingContext.SeriesId, Is.EqualTo(seriesId));
        Assert.That(pendingContext.GameNumber, Is.EqualTo(2));
        Assert.That(pendingMetrics, Is.Not.Null.And.Not.Empty);
        Log("Phase C PASSING: the production result-submission failure left the exact attempted result " +
            $"represented by PendingRemoteAttemptResult (attemptId={pendingContext.AttemptId}, " +
            $"metrics=[{string.Join(", ", pendingMetrics.Select(kv => kv.Key + "=" + kv.Value))}]).");

        // PendingRemoteAttemptResult.HasPending is set synchronously the instant TrySubmit stashes the
        // payload - BEFORE the network call even starts (RemoteAttemptResultSubmitter.TrySubmit),
        // not after it fails. The WaitUntil above therefore returns almost immediately, while the
        // original failing request against the unreachable endpoint is still in flight and still
        // holds RemoteAttemptResultSubmitter's submission claim (TryClaim/Release). Restoring the
        // endpoint and clicking Resend before that first request actually settles would make
        // TryRetryPending's own TryClaim fail silently (already claimed) and start no new submission
        // at all. The configured 5s request timeout bounds the worst case; the observed real failure
        // (connection refused) settles in ~2s.
        yield return WaitSeconds(7f);

        // ---- Restore the valid endpoint and reopen the correspondence screen ----
        ApplyConfig(ResolveConfig());
        Log("Restored the real Backend V2 endpoint: " + BackendV2ApiConfigProvider.Current.BaseUri);

        yield return LoadSceneAndSettle();
        controller = FindController();
        Assert.That(controller, Is.Not.Null, "CorrespondenceScreen not found after reopening");
        loginPanel = (GameObject)GetMember(controller, "loginPanel");
        Assert.That(loginPanel.activeSelf, Is.False,
            "the still-valid in-memory session should not require a fresh login just from reopening the screen");

        yield return WaitUntilButtonAppears(controller, "Resend resultButton", 20f);
        contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Button resendButton = FindButton(contentRoot, "Resend resultButton");
        Assert.That(resendButton, Is.Not.Null,
            "the pending-result banner's real 'Resend result' button did not render within 20s:\n" +
            DumpContentRoot(controller));
        Log("Pending-result banner PASSING: visible on the real correspondence screen after reopening.");

        IApiTransport liveTransport = BackendV2Runtime.Transport;
        object liveTransportConfig = GetMember(liveTransport.GetType(), liveTransport, "config");
        Log("Clicking the real 'Resend result' button (-> RemoteAttemptResultSubmitter.TryRetryPending, " +
            "resending the exact stashed payload) ... BackendV2Runtime.Transport actual config.BaseUri=" +
            GetMember(liveTransportConfig.GetType(), liveTransportConfig, "BaseUri"));
        resendButton.onClick.Invoke();

        yield return WaitUntil(
            () => !PendingRemoteAttemptResult.HasPending && !ActiveRemoteAttempt.IsActive, 30f,
            "the pending result did not clear within 30s after a real Resend-result click against the " +
            "restored endpoint");
        Log("Phase C PASSING: the real Resend-result click resubmitted the exact stashed payload and " +
            "Backend V2 accepted it - the pending state cleared only after that definitive server outcome.");

        // ---- Phase E: the Bo3 must now be complete ----
        ApiResponse<SeriesResponseDto> finalSeries = null;
        yield return BackendV2Runtime.Correspondence.Get(seriesId, r => finalSeries = r);
        Assert.That(finalSeries != null && finalSeries.Success, Is.True, "final Get(series) failed: " + Describe(finalSeries));
        Assert.That(finalSeries.Value.Status, Is.EqualTo("Completed"),
            "the Bo3 must be Completed after Account A won game 2 (already leading 1-0)");
        Assert.That(finalSeries.Value.WinnerId, Is.EqualTo(playerIdA));
        Log($"Phase E PASSING: series {seriesId} is Completed live, winner={finalSeries.Value.WinnerId} (Account A).");

        ApiResponse<SeriesSummaryPageDto> activeAfter = null;
        yield return BackendV2Runtime.Correspondence.ListActive(20, null, r => activeAfter = r);
        Assert.That(activeAfter != null && activeAfter.Success, Is.True);
        Assert.That(activeAfter.Value.Items.Any(i => i.Id == seriesId), Is.False,
            "a completed series must no longer appear in Active");

        yield return SelectTabAndWait(controller, "CompletedButton");
        string completedTabText = DumpContentRoot(controller);
        Assert.That(completedTabText, Does.Not.Contain("(no rendered rows)"),
            "the real Completed tab did not render the terminal series:\n" + completedTabText);
        Log("Phase E PASSING: the completed series left Active and the real Unity Completed tab renders it:\n" +
            Indent(completedTabText));

        var cleanup = RunCounterpart("cleanup");
        Log("Counterpart harness cleanup: success=" + cleanup.ok);
    }

    // ================================================================= shared helpers

    private static void RequireLiveCertificationOptIn()
    {
        if (Environment.GetEnvironmentVariable(EnabledEnvVar) != "1")
        {
            Assert.Ignore(
                "Live Unity-client correspondence certification (issue #159) is opt-in and skipped by " +
                "default so ordinary -runTests runs never depend on a live Backend V2 instance. Set " +
                EnabledEnvVar + "=1 (and, if needed, " + BaseUriEnvVar + " / " + BackendRepoPathEnvVar +
                ") and run against a live, reachable Backend V2 instance - see " +
                "docs/backend-v2-correspondence-certification.md.");
        }
    }

    /// <summary>Points every Level5.BackendV2 client at <paramref name="config"/> - both the config
    /// provider AND BackendV2Runtime must be updated together: UnityWebRequestTransport captures its
    /// BackendV2ApiConfig by value at construction, so changing only the provider would not affect an
    /// already-built (lazily cached) transport. BackendV2Runtime.Reset() forces the next access to
    /// rebuild from the provider's new value; BackendV2SessionStore (the actual token) is untouched.</summary>
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

    private static IEnumerator LoadSceneAndSettle()
    {
        yield return SceneManager.LoadSceneAsync(SceneNameCorrespondence);
        yield return null;
        yield return null;
        yield return null;
    }

    /// <summary>
    /// The real, first-ever entry into the correspondence screen for this process: loads the actual
    /// start screen (not the multiplayer scene directly) and clicks the real Multiplayer footer
    /// button (<c>StartManager.LoadMultiplayerMenu</c>), exactly the path a real player uses. This is
    /// not just more faithful than jumping straight to <see cref="SceneNameCorrespondence"/> - it is
    /// required: <c>MatchCatalogs</c> (mode/level catalogs <c>RemoteAttemptDescriptorMapper.Map</c>
    /// needs to launch a match) is only bootstrapped by <c>StartManager</c>'s own data-load coroutine
    /// (<c>LegacyMatchCatalogBootstrap.EnsureBuilt</c>), which never runs if a test loads the
    /// multiplayer scene in isolation - discovered live during this fixture's own development
    /// (Play click failed with "game mode 1 is not in the mode catalog; level 1 is not in the level
    /// catalog"). <c>MatchCatalogs</c> is a process-static cache, so this only needs to run once per
    /// process; later reopens of the correspondence screen use <see cref="LoadSceneAndSettle"/>
    /// directly, matching a real player closing/reopening just that screen rather than the whole app.
    /// </summary>
    private static IEnumerator BootstrapFromStartScreenIntoCorrespondence()
    {
        yield return SceneManager.LoadSceneAsync("level_00_start");
        yield return null;
        yield return null;
        yield return null;

        Type catalogsType = FindType("MatchCatalogs");
        yield return WaitUntil(() => (bool)GetMember(catalogsType, null, "IsReady"), 30f,
            "MatchCatalogs did not become ready (StartManager's mode/level catalog bootstrap) within " +
            "30s after loading level_00_start");

        GameObject multiplayerButtonGo = GameObject.Find("multiplayer_menu");
        Assert.That(multiplayerButtonGo, Is.Not.Null,
            "the real Multiplayer footer button ('multiplayer_menu') was not found on the start screen");
        Button multiplayerButton = multiplayerButtonGo.GetComponent<Button>();
        Log("Clicking the real Multiplayer footer button (-> StartManager.LoadMultiplayerMenu) ...");
        multiplayerButton.onClick.Invoke();

        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNameCorrespondence, 30f,
            "did not navigate to the correspondence scene after clicking the real Multiplayer button");
        yield return null;
        yield return null;
        yield return null;
    }

    private static object FindController()
    {
        GameObject go = GameObject.Find("CorrespondenceScreen");
        if (go == null)
        {
            return null;
        }

        return go.GetComponents<MonoBehaviour>()
            .FirstOrDefault(c => c.GetType().Name == "CorrespondenceScreenController");
    }

    private static Type FindType(string simpleName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(simpleName);
            if (t != null)
            {
                return t;
            }
        }

        throw new InvalidOperationException($"Type '{simpleName}' was not found in any loaded assembly.");
    }

    private static object GetMember(object target, string name)
    {
        return GetMember(target.GetType(), target, name);
    }

    private static object GetMember(Type type, object target, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField(name, flags);
        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo prop = type.GetProperty(name, flags);
        if (prop != null)
        {
            return prop.GetValue(target);
        }

        throw new InvalidOperationException($"{type.Name} has no field or property named '{name}'.");
    }

    private static void SetMember(Type type, object target, string name, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField(name, flags);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo prop = type.GetProperty(name, flags);
        if (prop != null)
        {
            prop.SetValue(target, value);
            return;
        }

        throw new InvalidOperationException($"{type.Name} has no field or property named '{name}'.");
    }

    /// <summary>Waits until GameLevelManager.instance, GameRules.instance and
    /// GameLevelManager.instance.Player1.gameStats are all non-null - the real gameplay scene's own
    /// readiness signal, reached the same way any other Update()-driven system would poll for it.</summary>
    private static IEnumerator WaitForGameplayReady()
    {
        Type glmType = FindType("GameLevelManager");
        Type grType = FindType("GameRules");
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 30f)
        {
            object glm = GetMember(glmType, null, "instance");
            object gr = GetMember(grType, null, "instance");
            if (glm != null && gr != null)
            {
                object player1 = GetMember(glmType, glm, "Player1");
                if (player1 != null && GetMember(player1.GetType(), player1, "gameStats") != null)
                {
                    yield break;
                }
            }

            yield return null;
        }

        throw new TimeoutException(
            "the gameplay scene did not become ready (GameLevelManager.instance/GameRules.instance/" +
            "Player1.gameStats) within 30s of the scene transition");
    }

    /// <summary>Sets a deterministic total score directly on the real gameplay scene's live
    /// <c>GameStats</c> component via <c>GameStats.TotalPoints</c> - an existing, already-public
    /// production property - so the real <c>GameRules.HandleMatchEnded</c> path (triggered separately
    /// by <see cref="InvokeRequestGameOver"/>) builds a real, deterministic result instead of needing
    /// a full basketball-gameplay simulation, exactly as issue #159's certification instructions
    /// anticipate ("establish deterministic test statistics").</summary>
    private static void SetDeterministicScore(bool winning)
    {
        Type glmType = FindType("GameLevelManager");
        object glm = GetMember(glmType, null, "instance");
        object player1 = GetMember(glmType, glm, "Player1");
        object gameStats = GetMember(player1.GetType(), player1, "gameStats");
        SetMember(gameStats.GetType(), gameStats, "TotalPoints", winning ? 999 : 1);
    }

    /// <summary>
    /// A remote attempt launches with <c>CharacterSelection.None</c> (docs/backend-v2-correspondence-
    /// ui.md's already-documented, accepted MVP limitation: "no level/character picker exists yet for
    /// a remote attempt"), which makes <c>SpawnCoordinator.InitializeHumanProfile</c> -&gt;
    /// <c>CharacterProfile.intializeShooterStatsFromProfile</c> log (not throw) "could not resolve the
    /// selected player profile for character id 0" - confirmed harmless to gameplay (the player and
    /// its <c>GameStats</c> are already spawned before this call; only cosmetic/shooter-stat profile
    /// loading is skipped, falling back to defaults). Unity's strict PlayMode test runner otherwise
    /// fails the test on any unhandled <c>Debug.LogError</c>, so this expects (not silences) exactly
    /// that already-known, pre-existing, unrelated log once per Play click.
    /// </summary>
    private static void ExpectKnownCharacterProfileLimitationLog()
    {
        LogAssert.Expect(LogType.Error, "CharacterProfile could not resolve the selected player profile for character id 0.");
    }

    private static void InvokeRequestGameOver()
    {
        Type grType = FindType("GameRules");
        object grInstance = GetMember(grType, null, "instance");
        MethodInfo method = grType.GetMethod("RequestGameOver", BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
        {
            throw new InvalidOperationException("GameRules has no public instance method 'RequestGameOver'.");
        }

        method.Invoke(grInstance, null);
    }

    private static string GetText(object controller, string fieldName)
    {
        Text text = (Text)GetMember(controller, fieldName);
        return text == null ? null : text.text;
    }

    private static Button FindButton(Transform root, string buttonGameObjectName)
    {
        return root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.gameObject.name == buttonGameObjectName);
    }

    private static IEnumerator SelectTabAndWait(object controller, string tabButtonName)
    {
        GameObject screenRoot = (GameObject)GetMember(controller, "screenRoot");
        Button tabButton = FindButton(screenRoot.transform, tabButtonName);
        if (tabButton == null)
        {
            throw new InvalidOperationException($"tab button '{tabButtonName}' not found under screenRoot.");
        }

        tabButton.onClick.Invoke();
        yield return WaitSeconds(2f);
    }

    private static IEnumerator WaitUntilButtonAppears(object controller, string buttonName, float timeoutSeconds)
    {
        yield return WaitUntil(() =>
        {
            RectTransform contentRoot = (RectTransform)GetMember(controller, "contentRoot");
            return FindButton(contentRoot, buttonName) != null;
        }, timeoutSeconds, $"button '{buttonName}' did not appear within {timeoutSeconds}s");
    }

    private static string DumpContentRoot(object controller)
    {
        RectTransform contentRoot = (RectTransform)GetMember(controller, "contentRoot");
        Text[] texts = contentRoot.GetComponentsInChildren<Text>(true);
        return texts.Length == 0
            ? "(no rendered rows)"
            : string.Join(" | ", texts.Select(t => t.text).Where(t => !string.IsNullOrEmpty(t)));
    }

    private static IEnumerator DumpAllTabs(object controller, string label)
    {
        (string button, string name)[] tabs =
        {
            ("FriendsButton", "Friends"),
            ("IncomingButton", "Incoming Challenges"),
            ("OutgoingButton", "Outgoing / Waiting"),
            ("Your TurnButton", "Your Turn"),
            ("ActiveButton", "Active Series"),
            ("CompletedButton", "Completed"),
        };

        Log("--- Tab dump (" + label + ") ---");
        foreach ((string button, string name) tab in tabs)
        {
            yield return SelectTabAndWait(controller, tab.button);
            Log(tab.name + ": " + DumpContentRoot(controller));
        }
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

    private static IEnumerator WaitSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    /// <summary>Shells out to the standalone HttpClient counterpart harness (Account B), bounded and
    /// force-killed on timeout - this runs synchronously between yields on the test runner's own
    /// coroutine, so an unbounded wait here would hang the whole run with no way to recover.</summary>
    private static (bool ok, string output) RunCounterpart(string arguments)
    {
        string repoOverride = Environment.GetEnvironmentVariable(BackendRepoPathEnvVar);
        string harnessDir = !string.IsNullOrWhiteSpace(repoOverride)
            ? Path.Combine(repoOverride, "v2", "scripts", "live-certification")
            : Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Level5Backend", "v2", "scripts", "live-certification"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run -- unity-counterpart " + arguments,
            WorkingDirectory = harnessDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using Process process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            bool exited = process.WaitForExit(CounterpartTimeoutSeconds * 1000);
            if (!exited)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception killEx)
                {
                    Log("WARNING: failed to kill timed-out counterpart process: " + killEx);
                }

                return (false, "counterpart harness did not exit within " + CounterpartTimeoutSeconds +
                    "s and was killed. Output so far:" + Environment.NewLine + stdout +
                    (string.IsNullOrEmpty(stderr) ? string.Empty : Environment.NewLine + "[stderr] " + stderr));
            }

            string combined = stdout + (string.IsNullOrEmpty(stderr) ? string.Empty : Environment.NewLine + "[stderr] " + stderr);
            return (process.ExitCode == 0, combined);
        }
        catch (Exception ex)
        {
            return (false, "failed to launch counterpart harness at " + harnessDir + ": " + ex);
        }
    }

    private static string Describe<T>(ApiResponse<T> r)
    {
        if (r == null)
        {
            return "null response";
        }

        return "success=" + r.Success + " errorKind=" + r.ErrorKind +
            " problemTitle=" + r.Problem?.Title + " problemCode=" + r.Problem?.Code +
            " traceId=" + r.Problem?.TraceId + " correlationId=" + r.CorrelationId;
    }

    private static string Indent(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "    (empty)";
        }

        return string.Join(Environment.NewLine, text.Split('\n').Select(line => "    " + line.TrimEnd('\r')));
    }

    private static void Log(string message)
    {
        Debug.Log("[BackendV2LiveCert] " + message);
        File.AppendAllText(EvidencePath, message + Environment.NewLine);
    }
}
