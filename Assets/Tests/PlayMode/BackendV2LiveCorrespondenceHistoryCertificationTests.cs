using System;
using System.Collections;
using System.IO;
using System.Linq;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Opt-in, live-backend Unity-client certification for issue #196 - the one gap PR #193's own test
/// plan explicitly left open: "Editor/PlayMode smoke check of the new History tab against a local
/// Backend V2 instance seeded with challenge-declined/challenge-cancelled/challenge-expired E2E
/// fixtures (not run in this environment)".
///
/// Skipped (<c>Assert.Ignore</c>) unless <c>LEVEL5_LIVE_CERTIFICATION=1</c> is set, so ordinary
/// <c>-runTests</c> runs never depend on a live Backend V2 instance. Each of the four wrapper
/// methods below must be run as its own <c>Unity.exe</c> invocation, immediately preceded by its own
/// <c>Level5Backend/v2/scripts/e2e.ps1 e2e-seed &lt;scenario&gt; -Force</c> against the disposable
/// <c>level5_v2_e2e</c> database - unlike <c>BackendV2LiveCorrespondenceCertificationTests</c>'s
/// Session1/2/3 (which need separate processes to certify a genuine process restart),
/// these need separate processes because the shared <c>level5_v2_e2e</c> database must be reset and
/// reseeded between scenarios, and this fixture deliberately never resets/seeds it itself (that
/// stays Backend-repo-owned tooling, per issue #196's own scope):
///
///   set LEVEL5_LIVE_CERTIFICATION=1
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode ^
///       -testFilter BackendV2LiveCorrespondenceHistoryCertificationTests.CompletedFixtureAppearsInCompletedAndHistory ^
///       -testResults history_completed_results.xml -logFile history_completed.log
///
/// Optional env var (read at runtime, same convention as the other live-cert fixtures):
/// <c>LEVEL5_BACKENDV2_BASE_URI</c> (defaults to <c>BackendV2ApiConfig.Development()</c>'s
/// <c>https://localhost:7029/</c>).
///
/// Reuses <see cref="RealScenePlayModeTestSupport"/> for scene/field resolution rather than
/// hand-rolling its own reflection helpers the way
/// <c>BackendV2LiveCorrespondenceCertificationTests</c> (written before that support class existed)
/// does - matching <c>BackendV2LiveOnlineAccountCertificationTests</c>'s newer style. Drives the real
/// default-assembly <c>CorrespondenceScreenController</c> via that support class's reflection only:
/// a named assembly definition (this one) can never reference Unity's implicit default assembly, a
/// hard engine restriction (see that class's own doc comment).
///
/// No new production API surface was added for this fixture. The four scenarios it certifies
/// (<c>series-completed</c>, <c>challenge-declined</c>, <c>challenge-cancelled</c>,
/// <c>challenge-expired</c>) and the fixture account it signs in as (<c>e2e_patrick</c>, tag
/// <c>E2E_PATRICK#0001</c>) already exist in <c>Level5Backend/v2/tools/Level5.E2E.Fixtures</c> - this
/// fixture only drives the real Unity UI against them, never seeds or resets the database itself.
/// </summary>
public class BackendV2LiveCorrespondenceHistoryCertificationTests
{
    private const string SceneNameCorrespondence = "level_00_multiplayer";
    private const string EnabledEnvVar = "LEVEL5_LIVE_CERTIFICATION";
    private const string BaseUriEnvVar = "LEVEL5_BACKENDV2_BASE_URI";
    private const string BaseUriEnvironmentEnvVar = "LEVEL5_BACKENDV2_ENVIRONMENT";

    // The existing deterministic E2E fixture account (Level5Backend/v2/tools/Level5.E2E.Fixtures/
    // FixtureIdentities.cs) - a documented, fixture-only credential, never a production one. Mirrored
    // here as a literal because Unity cannot reference that Backend-repo C# project directly.
    private const string FixtureUsername = "e2e_patrick";
    private const string FixturePassword = "E2E-Fixture-Password-Not-For-Production-1!";
    private const string FixtureTag = "E2E_PATRICK#0001";

    private static readonly string EvidencePath =
        Path.Combine(Path.GetTempPath(), "level5_unity_history_live_cert_evidence.log");

    // ================================================================= scenarios

    /// <summary>Seed with: <c>./v2/scripts/e2e.ps1 e2e-seed series-completed -Force</c>. Patrick wins
    /// the fixture's Bo1 (score 100 vs Alice's 50), so the series is <c>Completed</c> and must render
    /// in both the Completed tab and the History tab.</summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CompletedFixtureAppearsInCompletedAndHistory()
    {
        yield return CertifyTerminalFixture("Completed", expectedInCompleted: true);
    }

    /// <summary>Seed with: <c>./v2/scripts/e2e.ps1 e2e-seed challenge-declined -Force</c>. Alice
    /// declined Patrick's challenge - <c>Declined</c>, History-only.</summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator DeclinedFixtureAppearsOnlyInHistory()
    {
        yield return CertifyTerminalFixture("Declined", expectedInCompleted: false);
    }

    /// <summary>Seed with: <c>./v2/scripts/e2e.ps1 e2e-seed challenge-cancelled -Force</c>. Patrick
    /// cancelled his own outgoing challenge - <c>Cancelled</c>, History-only.</summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CancelledFixtureAppearsOnlyInHistory()
    {
        yield return CertifyTerminalFixture("Cancelled", expectedInCompleted: false);
    }

    /// <summary>Seed with: <c>./v2/scripts/e2e.ps1 e2e-seed challenge-expired -Force</c>. The fixture
    /// expires the challenge via the same domain transition (<c>VersusSeries.Expire</c>) the
    /// production sweep uses - <c>Expired</c>, History-only.</summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator ExpiredFixtureAppearsOnlyInHistory()
    {
        yield return CertifyTerminalFixture("Expired", expectedInCompleted: false);
    }

    // ================================================================= shared certification

    /// <summary>
    /// Clean session -> real scene load -> real Sign In as the seeded fixture account -> real History
    /// button -> assert the rendered row matches <paramref name="expectedStatus"/> and the fixture
    /// series's own id (never a hardcoded GUID - the fixture tool mints a real one) -> assert History
    /// is read-only -> real Completed button -> assert inclusion/exclusion per
    /// <paramref name="expectedInCompleted"/>.
    /// </summary>
    private static IEnumerator CertifyTerminalFixture(string expectedStatus, bool expectedInCompleted)
    {
        RequireLiveCertificationOptIn();
        ApplyConfig(ResolveConfig());
        Log($"=== {expectedStatus} fixture certification start === expectedInCompleted={expectedInCompleted}");

        // A prior local run may have left a persisted session on disk - clear it so this run
        // genuinely starts unauthenticated, matching a real fresh install rather than an artifact of
        // re-running this test locally.
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();

        yield return LoadSceneAndSettle();
        MonoBehaviour controller = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "CorrespondenceScreenController");
        Assert.That(controller, Is.Not.Null, "CorrespondenceScreenController not found after scene load");

        GameObject loginPanel = RealScenePlayModeTestSupport.GetField<GameObject>(controller, "loginPanel");
        Assert.That(loginPanel.activeSelf, Is.True,
            "a fresh session with no persisted state must see the login panel");

        InputField loginUsername = RealScenePlayModeTestSupport.GetField<InputField>(controller, "loginUsername");
        InputField loginPassword = RealScenePlayModeTestSupport.GetField<InputField>(controller, "loginPassword");
        loginUsername.text = FixtureUsername;
        loginPassword.text = FixturePassword;

        Button signInButton = FindButton(loginPanel.transform, "Sign InButton");
        Assert.That(signInButton, Is.Not.Null, "'Sign InButton' not found under LoginPanel");
        Log($"Clicking the real Sign In button as the seeded fixture account ({FixtureUsername}) ...");
        signInButton.onClick.Invoke();

        yield return WaitUntil(() => !loginPanel.activeSelf, 30f,
            "sign-in as the seeded fixture account did not complete live within 30s - is Backend V2 " +
            "running against level5_v2_e2e, and was the matching scenario seeded first?");
        Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True);
        Log("Login PASSING: the real Sign In button authenticated live against Backend V2 as the " +
            $"seeded fixture account (tag should be {FixtureTag}).");

        RectTransform contentRoot = RealScenePlayModeTestSupport.GetField<RectTransform>(controller, "contentRoot");
        GameObject screenRoot = RealScenePlayModeTestSupport.GetField<GameObject>(controller, "screenRoot");
        SeriesListCoordinator historyCoordinator =
            RealScenePlayModeTestSupport.GetField<SeriesListCoordinator>(controller, "history");
        SeriesListCoordinator completedCoordinator =
            RealScenePlayModeTestSupport.GetField<SeriesListCoordinator>(controller, "completed");
        Assert.That(historyCoordinator, Is.Not.Null, "coordinator field 'history' was not resolved");
        Assert.That(completedCoordinator, Is.Not.Null, "coordinator field 'completed' was not resolved");

        // DoLogin() hides the login panel (above) BEFORE its own yield return RefreshAll() even
        // starts (CorrespondenceScreenController.DoLogin), so the wait above can return while
        // RefreshAll()'s sequential per-tab fetches - including history.Refresh()/completed.Refresh()
        // - are still in flight. Waiting for both coordinators to settle here, before this test drives
        // any further UI interaction, avoids a second, concurrent Refresh() racing that still in-flight
        // one on the same ListViewState (SeriesListCoordinator.cs) - harmless with this fixture's single
        // static series, but unnecessary risk under real network variance otherwise.
        yield return WaitUntil(
            () => !historyCoordinator.State.IsLoading && !completedCoordinator.State.IsLoading, 30f,
            "the initial RefreshAll() triggered by login did not settle within 30s");

        // ---- cross-check against the real DTO before touching the UI, for a non-tautological
        //      identity assertion below (never a hardcoded GUID). ----
        ApiResponse<SeriesSummaryPageDto> historyResult = null;
        yield return BackendV2Runtime.Correspondence.ListHistory(20, null, r => historyResult = r);
        Assert.That(historyResult != null && historyResult.Success, Is.True,
            "direct ListHistory failed: " + Describe(historyResult));
        Assert.That(historyResult.Value.Items, Has.Count.EqualTo(1),
            "expected exactly one terminal series for the seeded fixture account - was the matching " +
            "scenario seeded immediately before this run, against a freshly reset level5_v2_e2e?");
        SeriesSummaryDto fixtureSeries = historyResult.Value.Items[0];
        Assert.That(fixtureSeries.Status, Is.EqualTo(expectedStatus));
        string idPrefix = fixtureSeries.Id.ToString().Substring(0, 8);
        Log($"Direct ListHistory PASSING: seriesId={fixtureSeries.Id}, status={fixtureSeries.Status}.");

        // ---- real History tab ----
        yield return SelectTabAndWaitSettled(screenRoot, "HistoryButton", historyCoordinator);
        string historyTabText = DumpContentRoot(contentRoot);
        Assert.That(historyTabText, Does.Contain(idPrefix).And.Contain(expectedStatus),
            "the real History tab did not render the seeded fixture series with its expected status:\n" +
            historyTabText);
        Log($"History tab PASSING (live): rendered row contains seriesId prefix '{idPrefix}' and " +
            $"status '{expectedStatus}':\n    {historyTabText}");

        foreach (string mutationButton in new[] { "AcceptButton", "DeclineButton", "CancelButton", "PlayButton" })
        {
            Assert.That(FindButton(contentRoot, mutationButton), Is.Null,
                $"History must be read-only, but a '{mutationButton}' was found under its rendered rows");
        }

        Log("Read-only PASSING: no Accept/Decline/Cancel/Play action is present under the rendered History tab.");

        // ---- real Completed tab ----
        yield return SelectTabAndWaitSettled(screenRoot, "CompletedButton", completedCoordinator);
        string completedTabText = DumpContentRoot(contentRoot);
        if (expectedInCompleted)
        {
            Assert.That(completedTabText, Does.Contain(idPrefix),
                "the real Completed tab did not render the seeded fixture series, but it should have:\n" +
                completedTabText);
            Log($"Completed tab PASSING (live): rendered row contains seriesId prefix '{idPrefix}':\n" +
                $"    {completedTabText}");
        }
        else
        {
            Assert.That(completedTabText, Does.Not.Contain(idPrefix),
                "the real Completed tab rendered the seeded fixture series, but a " + expectedStatus +
                " series must not appear there:\n" + completedTabText);
            Assert.That(completedTabText, Does.Contain("nothing here yet"),
                "expected the Completed tab's empty state for a " + expectedStatus + " fixture:\n" + completedTabText);
            Log("Completed tab PASSING (live): the seeded fixture series correctly does not appear " +
                "there, and the empty state renders.");
        }

        Log($"=== {expectedStatus} fixture certification complete ===");
    }

    // ================================================================= shared helpers

    private static void RequireLiveCertificationOptIn()
    {
        if (Environment.GetEnvironmentVariable(EnabledEnvVar) != "1")
        {
            Assert.Ignore(
                "Live Unity-client History certification (issue #196) is opt-in and skipped by " +
                "default so ordinary -runTests runs never depend on a live Backend V2 instance. Set " +
                EnabledEnvVar + "=1 (and, if needed, " + BaseUriEnvVar + "), seed the matching " +
                "scenario into level5_v2_e2e first (./v2/scripts/e2e.ps1 e2e-seed <scenario> -Force), " +
                "and run against a live, reachable Backend V2 instance pointed at that database.");
        }
    }

    /// <summary>Same seam every other live-cert fixture uses: both the config provider AND
    /// BackendV2Runtime must be reset together, since UnityWebRequestTransport captures its
    /// BackendV2ApiConfig by value at construction.</summary>
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
    /// Clicks the real tab button, then waits on the real <see cref="SeriesListCoordinator"/> behind
    /// it rather than scraping rendered text for a "loading..." marker: <c>RenderCurrentTab()</c> for
    /// these two tabs only runs once, after the fetch fully completes (unlike Active/Your Turn, which
    /// render a loading state mid-fetch) - so rendered text alone cannot distinguish "not started yet"
    /// from "already done". One extra settle frame after the coordinator reports done, since its own
    /// completion callback and this screen's <c>RenderCurrentTab()</c> call are still two statements
    /// in the same coroutine continuation, not guaranteed to have both run by the exact frame this
    /// poll observes <c>IsLoading == false</c>.
    /// </summary>
    private static IEnumerator SelectTabAndWaitSettled(
        GameObject screenRoot, string tabButtonName, SeriesListCoordinator coordinator)
    {
        Button tabButton = FindButton(screenRoot.transform, tabButtonName);
        Assert.That(tabButton, Is.Not.Null, $"tab button '{tabButtonName}' not found under screenRoot");

        tabButton.onClick.Invoke();

        yield return WaitUntil(() => !coordinator.State.IsLoading, 30f,
            () => $"tab '{tabButtonName}' did not settle within 30s; errorMessage=\"" +
                  coordinator.State.ErrorMessage + "\"");
        Assert.That(coordinator.State.ErrorMessage, Is.Null,
            $"'{tabButtonName}' failed to load live: " + coordinator.State.ErrorMessage);
        yield return null;
    }

    /// <summary>Row/tab buttons are named label + "Button" by convention
    /// (CorrespondenceScreenController.CreateButton) - unambiguous here because each scenario seeds
    /// exactly one fixture series and no other account state.</summary>
    private static Button FindButton(Transform root, string buttonGameObjectName)
    {
        return root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.gameObject.name == buttonGameObjectName);
    }

    private static string DumpContentRoot(RectTransform contentRoot)
    {
        Text[] texts = contentRoot.GetComponentsInChildren<Text>(true);
        return texts.Length == 0
            ? "(no rendered rows)"
            : string.Join(" | ", texts.Select(t => t.text).Where(t => !string.IsNullOrEmpty(t)));
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

    private static void Log(string message)
    {
        Debug.Log("[BackendV2HistoryLiveCert] " + message);
        File.AppendAllText(EvidencePath, message + Environment.NewLine);
    }
}
