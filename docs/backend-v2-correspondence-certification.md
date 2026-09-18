# Backend V2 correspondence end-to-end certification (issue #159)

Companion to [`docs/backend-v2-correspondence-ui.md`](backend-v2-correspondence-ui.md) (UI flow) and
[`docs/backend-v2-client.md`](backend-v2-client.md) (typed client layer, issue #158).

## Session record

| Field | Value |
| --- | --- |
| Unity commit (base) | `2d7014525ddd8b33b18bd17e62a683378c4ae58a` (`dev`) |
| Backend commit | not established - see "Blocked" below |
| Backend environment / base URI | `https://localhost:7029` (this project's `BackendV2ApiConfig.Development()` default) |
| Test accounts / isolated-client setup | not established - see "Blocked" below |
| Date/time | 2026-09-18 |
| Validator | Claude Code (automated agent), this implementation session |
| Overall status | **BLOCKED** - no reachable Backend V2 instance during this session |

## Blocked: no reachable backend

`curl -k --max-time 5 https://localhost:7029/` from this session's execution environment returned
connection failure (exit code 7, "Failed to connect" - nothing listening on that port/host from
here), both for the bare base URI and for `api/v2/players/me`. The user indicated a local dev
backend was running at this address; it was not reachable from the environment this coding session
executes in when checked at the time above.

**Every scenario below is therefore BLOCKED for live execution**, not silently skipped or assumed
to pass. What *was* validated in its place, without a live backend, is noted per scenario.

## Certification matrix

| # | Scenario | Status | Notes / what was validated instead |
| --- | --- | --- | --- |
| 1 | Friend lookup and accepted friendship | BLOCKED | `FriendsCoordinator` orchestration (resolve-by-tag -> send request -> refresh; accept -> refresh incoming+friends) covered by `Level5BackendV2FriendsCoordinatorTests` against `FakeApiTransport`. No live two-account friendship was formed. |
| 2 | Create/accept a Best-of-3 sealed challenge | BLOCKED | `ChallengeCoordinator.Create`/`Accept` covered by `Level5BackendV2ChallengeCoordinatorTests` against fakes, including the `clientRequestId` retry-reuse contract. `SeriesDetail` fixture used in tests carries `informationPolicy: "SealedAttempt"`, Bo3 (`totalGames: 3`). No live challenge was created or accepted. |
| 3 | Quit/reconnect between turns | BLOCKED | `CorrespondenceScreenController.Resume()`'s "not authenticated -> show login" path is exercised by `Level5BackendV2CorrespondenceScenePlayModeTests` (PlayMode, real scene load, no live backend needed for that path since no session exists in a fresh test run). The full quit-mid-attempt-then-reconnect-and-resume-the-same-turn flow needs a live series and was not run. |
| 4 | Duplicate/retried challenge, accept, start, complete requests | BLOCKED | Idempotency mechanics tested against fakes: `Level5BackendV2CorrespondenceClientTests.CreateChallengeSendsTheSameClientRequestIdOnEveryAttempt` (#158, pre-existing), `Level5BackendV2ChallengeCoordinatorTests.CreateSendsTheFormsClientRequestIdAndLeavesItForACallerDecidedRetry` (new), `RemoteAttemptResultSubmitter`'s `TryClaim`/`Release` guard (`Level5BackendV2RemoteAttemptSubmissionTests`, pre-existing) plus its `PendingRemoteAttemptResult` resend-same-payload guarantee (`Level5BackendV2PendingResultRetryTests`, new). None of this proves the *server* actually treats a duplicate `clientRequestId`/duplicate accept/start/complete as idempotent - that is exactly what a live run would confirm and this could not. |
| 5 | Lost response after an accepted completion | BLOCKED | `RemoteAttemptResultSubmitter`'s `Conflict`-clears-pending behavior is unit-tested against a fabricated `409` response, not a real one from a completion that the server actually accepted while the client's response was lost. |
| 6 | Server restart between turns | BLOCKED | No server to restart. |
| 7 | Second-device/state-refresh behavior | BLOCKED | The screen's "never trust stale UI state" design (refresh-on-open, refresh-after-every-command, no client-side caching of a Backend V2 decision) is documented in `docs/backend-v2-correspondence-ui.md`; not exercised against two concurrent live clients. |
| 8 | Simultaneous completion race | BLOCKED | Needs two live attempts completing concurrently against a real server; not executable without one. |
| 9 | Sealed first-finisher result remains hidden | BLOCKED | `SeriesRowClassifier`/`ActiveSeriesTurnClassifier` are proven, by test, to read only `YourAttempt` and never `OpponentAttempt.Result` (`Level5BackendV2SeriesRowClassifierTests.OpponentAttemptResultIsNeverReadByClassification`, `Level5BackendV2ActiveSeriesTurnClassifierTests`). This proves the **client** never reads or renders a field it shouldn't; it does not prove the **server's** sealed-result projection actually withholds `OpponentAttempt`/its `Result` from the non-finishing participant before disclosure - that assertion needs a live two-account run. |
| 10 | Completed series history remains readable | BLOCKED | `SeriesListCoordinator` bound to `ListCompleted` and the Completed tab's rendering are implemented and unit-tested for the coordinator layer; no live completed series exists to read back. |
| 11 | Frozen rules remain stable across a client update | BLOCKED | `RemoteAttemptDescriptorMapper` (issue #158, unchanged here) already refuses an unsupported protocol version, unknown ruleset, or unplayable ruleset version before launch, and `Level5BackendV2AttemptMappingTests` covers that. Whether a real in-progress series' frozen rules survive an actual client rebuild needs a live series across two builds. |

## What was actually validated this session

Everything not requiring a live backend:

- Full EditMode suite (`Assets/Tests/Editor`): **1610/1610 passed** (final run, after two independent
  code-review passes - see below), run 2026-09-18 against this session's final changes (see "Local
  validation" below to reproduce). Real bugs this session's own test runs and both review passes
  caught, all fixed before this report:
  - `BackendV2SessionPersistenceStore.Clear()` was not deleting `AtomicFile`'s `.bak` backup, so a
    stale session survived every `Clear()` (caught by the EditMode run leaking a file across the
    whole suite).
  - An over-broad `UnityWebRequest` architecture-test regex flagged `PlayersApiClient`'s legitimate
    `UnityWebRequest.EscapeURL` call as a violation.
  - `RemoteAttemptLauncher.Run` could silently overwrite and lose an earlier, still-unretried failed
    result (`ActiveRemoteAttempt`/`PendingRemoteAttemptResult` are single global slots) if the player
    started a different remote attempt before resolving it - now refused up front with a clear error.
  - The architecture tests only scanned `Assets/Scripts/backendv2/`, missing the #159 UI/launcher
    files that had to live in the default assembly - extended to cover them explicitly.
  - The runtime-synthesized `Multiplayer` footer button cloned `accountMenuButton`'s stale
    Explicit-mode navigation links, making it unreachable by keyboard/gamepad despite working for
    mouse/touch - `StartManager.SpliceIntoExplicitNavigation` now patches it into the chain.
  - `RenderCurrentTab()` destroyed old rows without detaching them first; since `Destroy()` is
    deferred, old and new rows coexisted for one frame, causing a one-frame doubled-height layout
    flash on every tab switch/refresh - fixed by detaching (`SetParent(null)`) before destroying.
  - The runtime-built `CanvasScaler` was left at its default `ConstantPixelSize` instead of matching
    every other menu scene's `ScaleWithScreenSize` (1920x1080, balanced match) - the screen would
    have rendered at the wrong physical size on any non-reference resolution, including most mobile
    devices.
  - `ActiveSeriesTurnClassifier.ClassifyAll` fetched series detail strictly sequentially with no
    progress shown until the whole page finished, making the Active/Your Turn tabs' first render take
    `n × round-trip-time` with nothing visible in between - now renders progressively as each row's
    turn becomes known.
  - `FriendsCoordinator`/`ChallengeCoordinator` released a row's in-flight command guard immediately
    after the network call settled, before the list refresh that actually updates the UI - a rapid
    double-tap in that window could send a duplicate command to an already-resolved row - guard now
    held through the refresh.
  - `BackendV2SessionPersistenceStore.TryLoad`'s "structurally valid JSON but semantically incomplete"
    branch did not clear the file (unlike the expired-token branch, which did), so a corrupted-but-
    parseable session file would block auto-restore forever without ever self-healing - now cleared
    consistently.
- Full PlayMode suite (`Assets/Tests/PlayMode`): **24/24 passed** (final run, after both review
  passes and all fixes above), run 2026-09-18, including the new
  `Level5BackendV2CorrespondenceScenePlayModeTests` smoke test - the correspondence scene loads,
  `CorrespondenceScreenController` builds its runtime UI (Canvas nested under its own GameObject,
  fixed after the first pass found it parented as a stray root-level object instead), and the
  unauthenticated path (no session in a fresh test run) shows the login panel, all without an
  unhandled `Debug.LogError`/exception (what a `[UnityTest]` failing on unexpected log output
  actually asserts).
- `./scripts/validate-repository.ps1` - passed (no tracked generated files, no missing `.meta`, no
  synchronous networking APIs).
- Full solution compile via Unity batchmode (`-batchmode -quit -nographics`, no `-runTests`) - clean.

## Local validation

Run these to reproduce (from the repository root):

```powershell
# EditMode suite (no -quit, no -nographics is fine for EditMode specifically)
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode_results.xml -logFile editmode_test.log

# PlayMode suite (no -quit, and -nographics must be OMITTED for PlayMode)
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode_results.xml -logFile playmode_test.log

./scripts/validate-repository.ps1
```

Do not pass `-quit` together with `-runTests` (the test runner never starts) or `-nographics` for a
PlayMode run (it silently completes without writing results) - both look like a clean pass from the
exit code alone. Always parse the results XML.

## Certifying this once a backend is reachable

1. Confirm `curl -k https://<host>:<port>/` (or the actual health/base endpoint) responds.
2. Point the client at it: either run the game with the default `BackendV2ApiConfig.Development()`
   (already `https://localhost:7029`) if that is where the backend is, or supply a
   `BackendV2ApiConfig.Custom(...)` via `BackendV2ApiConfigProvider.Override` for a different
   environment.
3. Create two accounts (or use two already-provisioned ones) - either two real installs/devices, or
   two sequential logged-in states driven through the built game, or two isolated in-process client
   pairs via `BackendV2Runtime.Override`/two separate session stores (whichever is more reliable
   against the actual backend's auth model - decide at execution time).
4. Walk the matrix above scenario by scenario, filling in a genuine pass/fail/blocked and evidence
   (screenshots, logs, correlation ids from `ApiResponse.CorrelationId`/server traceIds) for each -
   never carry a BLOCKED forward as a pass without re-running it.
5. Update the "Session record" table at the top with the real backend commit and accounts used.
