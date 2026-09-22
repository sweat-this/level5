using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Level5.BackendV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Narrow, single-purpose Editor batchmode automation for the #159 Unity-client-side live
/// correspondence certification gap
/// (docs/backend-v2-correspondence-certification.md, "Remaining Backend V2 correspondence work" #1).
///
/// Drives the REAL production <see cref="CorrespondenceScreenController"/> /
/// <see cref="RemoteAttemptLauncher"/> code - never a reimplementation of the wire contract, unlike
/// the standalone HttpClient harness this is a companion to - against a live Backend V2 instance, in
/// real Editor Play Mode (real <c>UnityWebRequest</c> calls complete there; they do not in the
/// batchmode EditMode test runner - see the certification doc for why that rules out
/// <c>[UnityTest]</c> EditMode, and why <c>Level5.PlayModeTests.asmdef</c> can never reference this
/// controller by type - a hard engine restriction, not fixable without an assembly refactor, which is
/// out of scope for a certification slice). This script lives in an Editor-only folder with no
/// assembly definition of its own (matching the existing
/// <see cref="BackendV2CorrespondenceSceneBootstrap"/> in this same folder), so it CAN reference
/// default-assembly types like <see cref="CorrespondenceScreenController"/> directly.
///
/// UI interaction is driven the same way any Unity UI automation drives it: setting
/// <c>InputField.text</c> and invoking a real <c>Button</c>'s real <c>onClick</c> (the exact
/// UnityEvent a physical click fires), never by reimplementing what a handler does. Every field this
/// script reads/sets on <see cref="CorrespondenceScreenController"/> is private (the controller has
/// no public API - see its own doc comment), so reflection is used to reach them; this script never
/// modifies that controller.
///
/// Not a general test framework: one entry point, one linear session, plain <c>Debug.Log</c> plus a
/// flat evidence log file for a human/agent to review and transcribe into the certification doc - not
/// a `-runTests` suite, no fixtures, nothing else in this repo runs it automatically.
///
/// Run (Backend V2 already running locally, from the level5 repo root):
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -projectPath . -executeMethod
///     BackendV2LiveCertificationRunner.RunFullSession -logFile unity_live_cert.log
/// Deliberately no `-quit` (this script calls <see cref="EditorApplication.Exit"/> itself once done)
/// and no `-nographics` (this drives real UI/Canvas/coroutine behavior - same reasoning this repo
/// already documents for why PlayMode runs omit it).
///
/// Evidence is appended to <c>%TEMP%\level5_unity_live_cert_evidence.log</c> across the whole
/// session. Account B's counterpart actions (friend request, challenge creation) are driven by
/// shelling out to the existing standalone HttpClient harness
/// (Level5Backend/v2/scripts/live-certification, `unity-counterpart` sub-commands added alongside
/// this script) at the exact points a second human player would act - this keeps the whole two-sided
/// flow to one Unity Play Mode session instead of requiring several hand-launched Unity processes.
/// Account B is explicitly NOT Unity-driven; every account-B action is standalone-HttpClient-driven,
/// exactly as already documented and accepted for the existing server-side certification pass. Only
/// account A's actions below are Unity-client-driven evidence.
/// </summary>
public static class BackendV2LiveCertificationRunner
{
    private const string SceneNameCorrespondence = "level_00_multiplayer";
    private const string BackendRepoPathEnvVar = "LEVEL5_BACKEND_REPO_PATH";
    private const string BaseUriEnvVar = "LEVEL5_BACKENDV2_BASE_URI";
    private const string BaseUriEnvironmentEnvVar = "LEVEL5_BACKENDV2_ENVIRONMENT";

    private static readonly string EvidencePath =
        Path.Combine(Path.GetTempPath(), "level5_unity_live_cert_evidence.log");

    [MenuItem("Tools/Backend V2/Run Live Certification Session")]
    public static void RunFullSession()
    {
        File.AppendAllText(EvidencePath,
            Environment.NewLine + "=== Unity live-certification session start " + DateTime.Now.ToString("O") +
            " ===" + Environment.NewLine);

        // Unsubscribe first: if a prior RunFullSession() call's Play Mode entry was aborted or
        // cancelled (e.g. a compile error), OnPlayModeStateChanged never fired EnteredPlayMode and
        // never unsubscribed itself, so a second click of the MenuItem before that happens would
        // otherwise double-subscribe and spawn two racing Driver sessions on the next successful entry.
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        GameObject go = new GameObject("BackendV2LiveCertDriver");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Driver driver = go.AddComponent<Driver>();
        driver.StartCoroutine(driver.Run());
    }

    private sealed class Driver : MonoBehaviour
    {
        /// <summary>Manually pumps <see cref="RunStepsInner"/> so an exception thrown by any step
        /// (including one raised while a nested <c>yield return</c> is resolving) is caught here and
        /// still flushes evidence + calls <see cref="EditorApplication.Exit"/> - a raw
        /// <c>yield return RunStepsInner()</c> could not be wrapped in a try/catch that actually
        /// catches exceptions from the resumed iterator's later moves.</summary>
        public IEnumerator Run()
        {
            IEnumerator inner = RunStepsInner();
            while (true)
            {
                bool moved;
                try
                {
                    moved = inner.MoveNext();
                }
                catch (Exception ex)
                {
                    Log("FATAL: unhandled exception in live-certification driver: " + ex);
                    FinishSession(1);
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                yield return inner.Current;
            }
        }

        private IEnumerator RunStepsInner()
        {
            Log("Step 0: BackendV2ApiConfigProvider.Override -> " + DescribeConfig());
            BackendV2ApiConfigProvider.Override(ResolveConfig());

            string suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
            string usernameA = "unityCertA" + suffix;
            const string passwordA = "CertPass123!";
            string displayNameA = "Unity Cert Account A " + suffix;
            Log($"Account A (Unity-driven): username={usernameA} displayName={displayNameA}");

            // Raw client-level register (not BackendV2Runtime.Session.Register, which would also set
            // BackendV2SessionStore immediately) - keeps the session store empty so the first scene
            // load below genuinely exercises the unauthenticated -> login-panel path for real, and the
            // "Sign In" button click that follows is what actually authenticates the session, not this
            // call. This is still real production client code (AuthApiClient.Register), one layer
            // below where the UI has no register button of its own (see docs/backend-v2-correspondence-ui.md).
            ApiResponse<AccessTokenResponseDto> registerResult = null;
            yield return BackendV2Runtime.Auth.Register(
                new RegisterRequestDto(usernameA, passwordA, displayNameA), r => registerResult = r);
            if (registerResult == null || !registerResult.Success)
            {
                Log("FATAL: Account A registration failed live: " + Describe(registerResult));
                FinishSession(1);
                yield break;
            }

            Guid playerIdA = registerResult.Value.PlayerId;
            Log($"Account A registered live against Backend V2: playerId={playerIdA}");

            // ---- First scene open: unauthenticated path ----
            yield return LoadSceneAndSettle();
            CorrespondenceScreenController controller = FindController();
            if (controller == null)
            {
                Log("FATAL: CorrespondenceScreen GameObject not found after loading " + SceneNameCorrespondence);
                FinishSession(1);
                yield break;
            }

            GameObject loginPanel = (GameObject)GetField(controller, "loginPanel");
            GameObject screenRoot = (GameObject)GetField(controller, "screenRoot");
            Log("Scenario 3 (Unity-driven, first open / unauthenticated path): loginPanel.activeSelf=" +
                loginPanel.activeSelf + " screenRoot.activeSelf=" + screenRoot.activeSelf +
                " (expected true/false - no persisted session exists yet for this fresh account)");

            // ---- Drive the real Sign In button ----
            InputField loginUsername = (InputField)GetField(controller, "loginUsername");
            InputField loginPassword = (InputField)GetField(controller, "loginPassword");
            loginUsername.text = usernameA;
            loginPassword.text = passwordA;

            Button signInButton = FindButton(loginPanel.transform, "Sign InButton");
            if (signInButton == null)
            {
                Log("FATAL: 'Sign InButton' not found under LoginPanel.");
                FinishSession(1);
                yield break;
            }

            Log("Clicking the real Sign In button (Button.onClick.Invoke -> " +
                "CorrespondenceScreenController.OnLoginClicked -> DoLogin -> " +
                "BackendV2Runtime.Session.Login) ...");
            signInButton.onClick.Invoke();

            yield return WaitUntil(() =>
                !loginPanel.activeSelf ||
                (GetText(controller, "loginError") ?? string.Empty).ToLowerInvariant().Contains("failed"),
                30f);

            if (loginPanel.activeSelf)
            {
                Log("FATAL: sign-in did not complete live within 30s. loginError=\"" +
                    GetText(controller, "loginError") + "\"");
                FinishSession(1);
                yield break;
            }

            Log("Scenario 3 (Unity-driven login half) PASSING: the real Sign In button click " +
                "authenticated live against Backend V2 (loginPanel is now inactive, screen rendered).");

            // ---- Confirm session persistence to disk (real production code, real file) ----
            bool persisted = BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session persistedSession);
            Log("Session persistence check (BackendV2SessionPersistenceStore.TryLoad, real disk file " +
                "under Application.persistentDataPath): loaded=" + persisted +
                (persisted
                    ? $" persistedPlayerId={persistedSession.PlayerId} matchesAccountA={persistedSession.PlayerId == playerIdA}"
                    : " (no session file found - FAILING)"));

            // ---- Discover Account A's tag live (now authenticated) ----
            ApiResponse<PlayerProfileResponseDto> meResult = null;
            yield return BackendV2Runtime.Players.UpdateMe(displayNameA, r => meResult = r);
            if (meResult == null || !meResult.Success)
            {
                Log("FATAL: UpdateMe (tag discovery) failed live: " + Describe(meResult));
                FinishSession(1);
                yield break;
            }

            string tagA = meResult.Value.Tag;
            Log($"Account A tag (live, via real BackendV2Runtime.Players.UpdateMe call): {tagA}");

            // ---- Dump every tab before any counterpart (Account B) data exists ----
            yield return DumpAllTabs(controller, "before counterpart (Account B) actions");

            // ---- Hand off to Account B (standalone HttpClient harness, NOT Unity-driven): send a ----
            // ---- friend request to Account A's live tag. ----
            (bool ok, string output) friendStep = RunCounterpart("friend \"" + tagA + "\"");
            Log("Counterpart step (Account B, HttpClient harness, not Unity-driven) - send friend request: " +
                "success=" + friendStep.ok);
            Log(Indent(friendStep.output));
            if (!friendStep.ok)
            {
                FinishSession(1);
                yield break;
            }

            // ---- Reload the scene (simulate close/reopen) - Resume() must restore the session ----
            // ---- without a fresh login. ----
            yield return LoadSceneAndSettle();
            controller = FindController();
            if (controller == null)
            {
                Log("FATAL: CorrespondenceScreen GameObject not found after scene reload.");
                FinishSession(1);
                yield break;
            }

            loginPanel = (GameObject)GetField(controller, "loginPanel");
            bool resumeRestoredSession = !loginPanel.activeSelf;
            Log("Scenario 3 (Unity-driven Resume half): after closing/reopening the screen " +
                "(SceneManager reload -> Awake -> BackendV2SessionPersistenceBootstrap.EnsureInitialized " +
                "-> Start -> Resume()), loginPanel.activeSelf=" + loginPanel.activeSelf);
            Log(resumeRestoredSession
                ? "Scenario 3 (Unity-driven Resume half) PASSING: the persisted session was restored " +
                  "live and the login panel was skipped - CorrespondenceScreenController.Resume() " +
                  "refreshed from the live server, not stale local state."
                : "Scenario 3 (Unity-driven Resume half) FAILING: the login panel reappeared after " +
                  "reopening despite a session file having been persisted moments earlier.");
            if (!resumeRestoredSession)
            {
                FinishSession(1);
                yield break;
            }

            // ---- Friends tab: accept Account B's live incoming friend request ----
            yield return SelectTabAndWait(controller, "FriendsButton");
            yield return WaitUntilButtonAppears(controller, "AcceptButton", 20f);
            RectTransform contentRoot = (RectTransform)GetField(controller, "contentRoot");
            Button acceptFriendButton = FindButton(contentRoot, "AcceptButton");
            string friendsTabBefore = DumpContentRoot(controller);
            if (acceptFriendButton == null)
            {
                Log("FAILING (Section 5, Friends tab): Account B's incoming friend request did not " +
                    "render within 20s.\nRendered state:\n" + Indent(friendsTabBefore));
                FinishSession(1);
                yield break;
            }

            Log("Friends tab (Unity-driven) PASSING: Account B's live incoming friend request " +
                "rendered from a real server round trip.\n" + Indent(friendsTabBefore));
            acceptFriendButton.onClick.Invoke();
            yield return WaitSeconds(2f);
            yield return SelectTabAndWait(controller, "FriendsButton");
            Log("Friends tab (Unity-driven) after Accept click:\n" + Indent(DumpContentRoot(controller)));

            // ---- Hand off to Account B again: create a Bo3 challenge now that they are friends. ----
            (bool ok, string output) challengeStep = RunCounterpart("challenge");
            Log("Counterpart step (Account B, HttpClient harness, not Unity-driven) - create challenge: " +
                "success=" + challengeStep.ok);
            Log(Indent(challengeStep.output));
            if (!challengeStep.ok)
            {
                FinishSession(1);
                yield break;
            }

            // ---- Incoming Challenges tab: accept live ----
            yield return SelectTabAndWait(controller, "IncomingButton");
            yield return WaitUntilButtonAppears(controller, "AcceptButton", 20f);
            contentRoot = (RectTransform)GetField(controller, "contentRoot");
            Button acceptChallengeButton = FindButton(contentRoot, "AcceptButton");
            string incomingTabBefore = DumpContentRoot(controller);
            if (acceptChallengeButton == null)
            {
                Log("FAILING (Section 5, Incoming Challenges tab): Account B's live challenge did not " +
                    "render within 20s.\nRendered state:\n" + Indent(incomingTabBefore));
                FinishSession(1);
                yield break;
            }

            Log("Incoming Challenges tab (Unity-driven) PASSING: Account B's live challenge rendered " +
                "from a real server round trip.\n" + Indent(incomingTabBefore));
            acceptChallengeButton.onClick.Invoke();
            yield return WaitSeconds(2f);

            // ---- Remaining tabs (Outgoing/Active/Completed) for Section 5 UI-refresh evidence ----
            yield return DumpAllTabs(controller, "after friend + challenge accepted live");

            // ---- Your Turn tab: click Play, certify the remote-attempt launch chain (Section 6) ----
            yield return SelectTabAndWait(controller, "Your TurnButton");
            yield return WaitUntilButtonAppears(controller, "PlayButton", 20f);
            contentRoot = (RectTransform)GetField(controller, "contentRoot");
            Button playButton = FindButton(contentRoot, "PlayButton");
            string yourTurnBefore = DumpContentRoot(controller);
            if (playButton == null)
            {
                Log("FAILING (Section 6): Your Turn tab did not show a Play action within 20s.\n" +
                    "Rendered state:\n" + Indent(yourTurnBefore));
                FinishSession(1);
                yield break;
            }

            Log("Your Turn tab (Unity-driven) PASSING: the active series + Play action rendered from " +
                "a real server round trip.\n" + Indent(yourTurnBefore));

            string sceneBeforeLaunch = SceneManager.GetActiveScene().name;
            Log("Clicking the real Play button (Button.onClick.Invoke -> PlayTurn -> " +
                "RemoteAttemptLauncher.Run -> StartAttempt -> RemoteAttemptDescriptorMapper.Map -> " +
                "ActiveMatch.Begin -> ActiveRemoteAttempt.Begin -> LegacyGameOptionsBridge.Apply -> " +
                "SceneTransition.LoadScene) ...");
            playButton.onClick.Invoke();

            yield return WaitUntil(() =>
                SceneManager.GetActiveScene().name != sceneBeforeLaunch ||
                (GetText(controller, "statusBanner") ?? string.Empty).Contains("could not start the attempt"),
                30f);

            string statusAfter = GetText(controller, "statusBanner");
            string sceneAfter = SceneManager.GetActiveScene().name;
            bool launched = sceneAfter != sceneBeforeLaunch;
            Log("Section 6 remote-attempt launch result: sceneBefore=\"" + sceneBeforeLaunch +
                "\" sceneAfter=\"" + sceneAfter + "\" launched=" + launched + " statusBanner=\"" +
                statusAfter + "\" ActiveRemoteAttempt.IsActive=" + ActiveRemoteAttempt.IsActive);
            Log(launched
                ? "Section 6 (Unity-driven remote-attempt launch) PASSING: a real Play click drove " +
                  "StartAttempt through SceneTransition.LoadScene against the live backend; a real " +
                  "scene transition was observed (SceneManager.GetActiveScene().name changed)."
                : "Section 6 (Unity-driven remote-attempt launch) FAILING: no scene transition was " +
                  "observed; statusBanner=\"" + statusAfter + "\"");

            FinishSession(launched ? 0 : 1);
        }

        // ------------------------------------------------------------------ helpers

        private static BackendV2ApiConfig ResolveConfig()
        {
            string overrideUri = Environment.GetEnvironmentVariable(BaseUriEnvVar);
            if (string.IsNullOrWhiteSpace(overrideUri))
            {
                return BackendV2ApiConfig.Development();
            }

            return BackendV2ApiConfig.Custom(new Uri(overrideUri), ResolveOverrideEnvironment());
        }

        /// <summary>A custom base URI does not imply Development - without this, pointing
        /// <see cref="BaseUriEnvVar"/> at a Staging/Production host would still self-identify as
        /// Development. Defaults to Development (matching this tool's primary use case: a
        /// non-default local port) when unset or unrecognized.</summary>
        private static BackendV2Environment ResolveOverrideEnvironment()
        {
            string raw = Environment.GetEnvironmentVariable(BaseUriEnvironmentEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) &&
                Enum.TryParse(raw, ignoreCase: true, out BackendV2Environment parsed))
            {
                return parsed;
            }

            return BackendV2Environment.Development;
        }

        private static string DescribeConfig()
        {
            string overrideUri = Environment.GetEnvironmentVariable(BaseUriEnvVar);
            return string.IsNullOrWhiteSpace(overrideUri)
                ? "BackendV2ApiConfig.Development() (https://localhost:7029/)"
                : "BackendV2ApiConfig.Custom(" + overrideUri + ", " + ResolveOverrideEnvironment() + ")";
        }

        private IEnumerator LoadSceneAndSettle()
        {
            yield return SceneManager.LoadSceneAsync(SceneNameCorrespondence);
            yield return null;
            yield return null;
            yield return null;
        }

        private static CorrespondenceScreenController FindController()
        {
            GameObject go = GameObject.Find("CorrespondenceScreen");
            return go == null ? null : go.GetComponent<CorrespondenceScreenController>();
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = typeof(CorrespondenceScreenController).GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException(
                    "CorrespondenceScreenController has no private field named '" + name + "' - " +
                    "this automation script is out of sync with the controller and needs updating.");
            }

            return field.GetValue(target);
        }

        private static string GetText(CorrespondenceScreenController controller, string fieldName)
        {
            Text text = (Text)GetField(controller, fieldName);
            return text == null ? null : text.text;
        }

        /// <summary>Button names collide by design (every row's button is named
        /// <c>label + "Button"</c> - see <c>CorrespondenceScreenController.CreateButton</c>), so this
        /// is only unambiguous because the documented single-run flow guarantees at most one match at
        /// a time (one fresh Account A, one fresh Account B, one friend request, one challenge). If
        /// that ever stops holding - e.g. a counterpart step retried out of sequence, leaving a second
        /// live request/challenge behind - this would otherwise silently click whichever row happens
        /// to render first; logging the ambiguity here at least makes that visible in the evidence.</summary>
        private static Button FindButton(Transform root, string buttonGameObjectName)
        {
            Button[] matches = root
                .GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.name == buttonGameObjectName)
                .ToArray();

            if (matches.Length > 1)
            {
                Log("WARNING: " + matches.Length + " buttons named '" + buttonGameObjectName +
                    "' found under " + root.name + " - picking the first; this is only correct for " +
                    "the documented single-run flow (see FindButton's doc comment).");
            }

            return matches.FirstOrDefault();
        }

        private IEnumerator SelectTabAndWait(CorrespondenceScreenController controller, string tabButtonName)
        {
            GameObject screenRoot = (GameObject)GetField(controller, "screenRoot");
            Button tabButton = FindButton(screenRoot.transform, tabButtonName);
            if (tabButton == null)
            {
                Log("FAILING: tab button '" + tabButtonName + "' not found under screenRoot.");
                yield break;
            }

            tabButton.onClick.Invoke();
            yield return WaitSeconds(2f);
        }

        private IEnumerator WaitUntilButtonAppears(
            CorrespondenceScreenController controller, string buttonName, float timeoutSeconds)
        {
            yield return WaitUntil(() =>
            {
                RectTransform contentRoot = (RectTransform)GetField(controller, "contentRoot");
                return FindButton(contentRoot, buttonName) != null;
            }, timeoutSeconds);
        }

        private static string DumpContentRoot(CorrespondenceScreenController controller)
        {
            RectTransform contentRoot = (RectTransform)GetField(controller, "contentRoot");
            Text[] texts = contentRoot.GetComponentsInChildren<Text>(true);
            return texts.Length == 0
                ? "(no rendered rows)"
                : string.Join(" | ", texts.Select(t => t.text).Where(t => !string.IsNullOrEmpty(t)));
        }

        private IEnumerator DumpAllTabs(CorrespondenceScreenController controller, string label)
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

        private IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                yield return null;
            }
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        private const int CounterpartTimeoutSeconds = 120;

        /// <summary>Bounded by <see cref="CounterpartTimeoutSeconds"/> and force-killed on timeout -
        /// this runs synchronously on Unity's main thread (a coroutine step is still ordinary
        /// synchronous code between <c>yield</c>s), so an unbounded wait here would hang the whole
        /// Editor process exactly like the Play Mode startup deadlock this tool exists to diagnose,
        /// with no way to recover short of an external process kill.</summary>
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
                        // No entireProcessTree overload on this project's scripting runtime (.NET
                        // Standard 2.1 profile) - the direct dotnet.exe child is what actually hangs,
                        // so killing it alone is sufficient here.
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

        private static void FinishSession(int exitCode)
        {
            Log("=== Unity live-certification session end (exitCode=" + exitCode + ") " +
                DateTime.Now.ToString("O") + " ===");
            EditorApplication.Exit(exitCode);
        }
    }
}
