# Backend V2 correspondence end-to-end certification (issue #159)

Companion to [`docs/backend-v2-correspondence-ui.md`](backend-v2-correspondence-ui.md) (UI flow) and
[`docs/backend-v2-client.md`](backend-v2-client.md) (typed client layer, issue #158).

## Final Unity-client live certification (2026-09-23) - closes the remaining #159 gap

This session closed the one gap every prior session (below, preserved as history) could not:
real Unity-client evidence for the actual `Play` launch, real match completion through
`GameRules`, a genuine Unity process restart, and the client's own pending-result retry -
using the `-runTests -testPlatform PlayMode` path (already known reliable in this
environment) instead of the `-executeMethod`/`EditorApplication.isPlaying` path that
previously deadlocked (see "Unity-client live-certification attempt" below).

| Field | Value |
| --- | --- |
| Unity commit (base) | `7b55cedb18c121fbc43ca63f2ec1afe95d1a68d3` (`dev`) + this session's own commit (branch `backendv2-issue159-live-unity-certification`, PR link recorded once opened) |
| Backend commit (base) | `caadc292...` (`dev`, `Level5Backend` repo) + this session's own commit (branch `backendv2-issue159-ruleset-catalog-fix`, PR #37) |
| Backend environment / base URI | `https://localhost:7029` - local `dotnet run --launch-profile https` against the local Postgres container (`level5-postgres-local`), unchanged schema |
| Account isolation method | Account A: the real Unity client, driven through real `Button.onClick`/`InputField.text` against the real `CorrespondenceScreenController`/`GameRules`/`GameLevelManager` (reflection only where those default-assembly types are otherwise unreachable from a test assembly). Account B: the existing standalone `HttpClient` counterpart harness (`Level5Backend/v2/scripts/live-certification`, extended this session with `unity-counterpart playturn`/`cleanup`) - the "equivalent isolated client" issue #159 explicitly permits in place of a second Unity install. |
| Execution path | `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter <method>` - the certification doc's own prior sessions already confirmed this path (unlike `-executeMethod` + `EditorApplication.isPlaying`) runs real Play Mode, real `UnityWebRequest` calls, to completion reliably in this environment. |
| Date/time | 2026-09-23 |
| Tester / validator | Claude Code (automated agent), this session - no human operator, no GUI/device available; this session used the opt-in PlayMode fixture path issue #159's own instructions called for as the fallback when interactive Editor access is unavailable. |
| Overall status | **All mandatory #159 scenarios now have live Unity-client evidence.** Two `[UnityTest]`s in `Assets/Tests/PlayMode/BackendV2LiveCorrespondenceCertificationTests.cs`, gated behind `LEVEL5_LIVE_CERTIFICATION=1` (skipped otherwise - ordinary `-runTests` runs never depend on a live backend), run as two genuinely separate Unity processes. Both passed on the final run of this session. |

### What each session certifies

**Session1** (`Session1_EstablishSeriesAndCompleteGameOneViaGameRules`) - one continuous Unity
process:

1. Registers Account A directly (`BackendV2Runtime.Auth.Register`, one layer below the UI, which has
   no register button), then loads the real start screen (`level_00_start`) and clicks the real
   **Multiplayer** footer button (`StartManager.LoadMultiplayerMenu`) - the real entry point, not a
   direct scene load (see "Production defect found and fixed" below for why this matters).
2. Drives the real Sign In button; confirms `BackendV2SessionPersistenceStore` persisted a real
   session file.
3. Dumps all six tabs from a real server round trip while empty.
4. Hands off to the counterpart harness (`unity-counterpart friend <tag>`) for Account B's friend
   request; reloads the scene and confirms `Resume()` restored the session without a fresh login
   (Scenario 3, Unity-driven half).
5. Clicks the real **Accept** button on the Friends tab (live, server-rendered).
6. Hands off to the counterpart harness (`unity-counterpart challenge`, ruleset `most-points`) for
   Account B's Bo3 challenge; clicks the real **Accept** button on Incoming Challenges.
7. Clicks the real **Play** button on Your Turn - `RemoteAttemptLauncher.Run -> StartAttempt ->
   RemoteAttemptDescriptorMapper.Map -> ActiveMatch.Begin -> SceneTransition.LoadScene` - and confirms
   a real scene transition into the gameplay arena.
8. **Phase B**: on the real gameplay scene, sets a deterministic winning score directly on the real
   `GameLevelManager.instance.Player1.gameStats.TotalPoints` (an existing, already-public production
   property - reflection is needed only because `GameStats` lives in the default assembly, not
   because the member is private), then calls the real, pre-existing, public
   `GameRules.instance.RequestGameOver()` - the same seam a real "give up"/end-of-match path already
   uses. Waits for the real `GameRules.HandleMatchEnded -> RemoteAttemptResultSubmitter ->
   BackendV2Runtime.Correspondence.CompleteAttempt` chain to reach the live server and succeed
   (`ActiveRemoteAttempt.IsActive` and `PendingRemoteAttemptResult.HasPending` both clear).
9. Hands off to the counterpart harness (`unity-counterpart playturn 1 lose`) for Account B's game 1
   attempt, then confirms via `BackendV2Runtime.Correspondence.Get` that the series correctly resolved
   game 1 and moved to game 2 - live, both sides' attempts real.

**Session2** (`Session2_ResumeAfterRestartRetryAndCompleteSeries`) - a **separate** Unity process,
launched only after Session1's process fully exited:

1. **Phase D**: asserts no in-memory session exists (`BackendV2SessionStore.IsAuthenticated == false`
   - the fresh-process check), loads the real start screen and clicks Multiplayer again, and confirms
   the login panel is skipped: the real `BackendV2SessionPersistenceStore.TryLoad ->
   BackendV2SessionStore.Set -> ForceRefresh -> RefreshAll` chain restored Session1's persisted
   session from disk. Fetches the series from Backend V2 fresh and confirms it is still at game 2 -
   the same series Session1 established, continued correctly across a genuine process boundary (not a
   scene reload).
2. Hands off to the counterpart harness (`unity-counterpart playturn 2 lose`) for Account B's game 2
   attempt, then clicks the real Play button for game 2 and confirms the scene transition.
3. **Phase C**: points `BackendV2Runtime` at an intentionally unreachable endpoint
   (`https://127.0.0.1:9/`, via the same `BackendV2ApiConfigProvider.Override` seam
   `BackendV2LiveCertificationRunner` already used) and ends the match via the same real
   `GameRules.RequestGameOver()` seam. Confirms the real production submission failure leaves the
   *exact* attempted result represented by `PendingRemoteAttemptResult` (same `attemptId`, same
   metrics - never rebuilt). Restores the real endpoint, reopens the correspondence screen, confirms
   the real pending-result banner and its **Resend result** button render, clicks it for real, and
   confirms the pending state clears only once Backend V2 accepts the resent payload.
4. **Phase E**: confirms the series is `Completed` live (winner = Account A), no longer appears in
   Active, and the real Completed tab renders it.

### Production defect found and fixed

Live certification found a genuine defect before Phase B could be attempted at all: Backend V2's
only advertised ruleset, `"score-only"` (`StaticRulesetCatalog`), has **no counterpart in the Unity
client's own ruleset registry** (`DefaultCompetitiveRulesets`). `RemoteAttemptDescriptorMapper.Map`
resolves a remote attempt's ruleset by looking the server's `RulesetId` up in Unity's own
`VersusCatalogs.Rulesets` - which has never had a `"score-only"` entry - so **no Unity client build
could ever actually launch a match** for the server's only ruleset, contrary to
`StaticRulesetCatalog`'s own doc comment ("every entry mirrors a real, already-shipped Unity
ruleset"). The first real Play click in this session failed exactly this way:
`"Most Points cannot be played on the chosen arena: game mode 1 is not in the mode catalog; level 1
is not in the level catalog"` (a related, second-order symptom - see below).

Fix (Level5Backend PR #37, branch `backendv2-issue159-ruleset-catalog-fix`): added a `"most-points"`
entry to `StaticRulesetCatalog` that genuinely matches Unity's own `"most-points"` ruleset (id,
`Score`/`HigherWins` comparison key) exactly. Purely additive - `"score-only"` and every existing
test/fixture depending on it is untouched. A focused regression test
(`StaticRulesetCatalogTests.cs`) covers both entries plus the unknown-ruleset rejection path. This
session's certification harness (`unity-counterpart challenge`) now creates challenges with
`rulesetId: "most-points"` instead of `"score-only"`.

A second, harness-only issue surfaced in the same failure message
(`"level 1 is not in the level catalog"`): `MatchCatalogs` (Unity's mode/level catalogs) is only
bootstrapped by `StartManager`'s own data-load coroutine
(`LegacyMatchCatalogBootstrap.EnsureBuilt`), which never runs if a test loads the multiplayer scene
directly instead of going through the real start screen first. This is not a production defect -
every real player reaches the correspondence screen through `level_00_start`'s Multiplayer button,
which always bootstraps the catalogs first - it was purely an artifact of the certification fixture
skipping that real entry point. Fixed in the fixture itself (routes through the real start screen and
clicks the real Multiplayer button, which is also a strictly more faithful "real UI event path" per
issue #159's own instructions), not in production code.

### Known limitations, confirmed still accurate

- **`PendingRemoteAttemptResult` is memory-only** (confirmed by reading the current source: plain
  static fields, no disk persistence). Terminating the client after a retryable submission failure -
  before a successful resend or a definitive `Conflict` - loses the locally completed exact result
  payload. This is a real reliability gap worth a follow-up, but issue #159 does not require a
  completed-but-unsubmitted result to survive client process termination, so no durable persistence
  was added here (would be new, out-of-scope production surface). This is a *different* concern from
  the fresh-process **session** restoration this session certified (Phase D) - that is a real,
  disk-backed, now-live-certified path; the pending-result payload is not.
- **Frozen-rules stability across a client build update** (issue #159's optional, "where supported"
  clause) remains unproven by a genuine second Unity client build/version - only a single Editor
  install was available this session, matching every prior session. Rule stability across a real
  **backend** restart was already live-certified (see "Historical" below) and is unaffected. Per
  issue #159's own guidance, this optional clause alone is not treated as a blocker.
- Duplicate/retried-request idempotency (Scenario 4), a real backend process restart between turns
  (Scenario 6), second-independent-client refresh (Scenario 7), simultaneous completion (Scenario 8),
  and sealed-result disclosure timing (Scenario 9) were not re-driven through Unity this session -
  they remain proven by the existing live `HttpClient`-harness evidence below, which this session's
  own re-validation confirmed is still accurate against the current server (no server-side change
  this session touched any of that behavior). Re-deriving already-proven backend behavior through
  Unity again would not have added evidence, per issue #159's own "do not repeat already-proven
  behavior without a specific reason" guidance.

### Reproducing this session

```powershell
# 1. Trust the dev cert (one-time) and start Backend V2 locally (see "Reproducing this live
#    certification" below for the full sequence) - confirm https://localhost:7029/health/live

# 2. From the level5 repo root, one Unity process per session (session2 only after session1's
#    process has fully exited):
$env:LEVEL5_LIVE_CERTIFICATION = "1"
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode `
    -testFilter "BackendV2LiveCorrespondenceCertificationTests.Session1_EstablishSeriesAndCompleteGameOneViaGameRules" `
    -testResults session1_results.xml -logFile session1.log

& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode `
    -testFilter "BackendV2LiveCorrespondenceCertificationTests.Session2_ResumeAfterRestartRetryAndCompleteSeries" `
    -testResults session2_results.xml -logFile session2.log

# 3. Evidence accumulates at %TEMP%\level5_unity_live_cert_evidence.log across both processes.
```

Both `[UnityTest]`s are skipped (`Assert.Ignore`, with a clear prerequisite message) when
`LEVEL5_LIVE_CERTIFICATION` is unset, so the ordinary `-runTests -testPlatform PlayMode` suite (no
env var set) is unaffected - confirmed this session: 24/24 pre-existing PlayMode tests still pass,
plus 2 skipped (the new live-certification tests).

## Historical: prior sessions (preserved, still valid)

Everything below this line predates the session above and remains accurate for the scenarios it
covers - none of it was retired without being checked first. The session above closed exactly the
gaps every one of these prior sessions' own "Remaining work" sections called out.

## Session record

| Field | Value |
| --- | --- |
| Unity commit (base) | `c300299f32ec07b5c92aea4863a67d8fffdf0f04` (`dev`) |
| Backend commit | `62107269408a015664f5dee1aa8c394bf17706ea` (`dev`, `Level5Backend` repo, both legacy and `v2/`) |
| Backend environment / base URI | `https://localhost:7029` (this project's `BackendV2ApiConfig.Development()` default) - local dev instance, started this session via `v2/scripts/setup-local-dev.ps1` + `dotnet run --launch-profile https` against the local Postgres container (`docker-compose.local-db.yml`), `level5_v2` database rebaselined onto the current single `InitialCreate` migration (issue #20) since the container's prior schema predated that rebaseline |
| Test accounts / isolated-client setup | Two freshly-registered accounts per phase run (`certA<suffix>` / `certB<suffix>`, unique per run), driven by two independent `HttpClient` instances (one per account) - see "Live certification session" below for why this replaced a Unity-native harness |
| Date/time | 2026-09-22 |
| Tester / validator | Claude Code (automated agent), this session |
| Client build type | N/A for this session's live evidence - see "What this session's live evidence does and does not cover" below. Unity 6000.5.7f1 batchmode was used only to run this repo's own EditMode/PlayMode suites (unchanged behavior, regression check), not to drive the live two-account calls |
| Device / environment | Windows 10, local machine; Backend V2 run as a local `dotnet run` process against local Postgres 18 (container `level5-postgres-local`) |
| Overall status | **PARTIAL** - see matrix below: 8 of 11 scenarios fully live-passed, 2 partially live-passed (a documented, non-UI half of each remains not-yet-exercisable this session), 1 partially live-passed with its second half explicitly BLOCKED (no second client build available). A same-day follow-up session (see "Unity-client live-certification attempt" below) built and attempted to run real Unity-client-driven automation for the remaining non-UI halves; it could not complete in this environment - see that section for the exact, investigated reason. |

## Live certification session (2026-09-22)

The prior session (2026-09-18, preserved below under "Historical: prior blocked session") could not
reach a Backend V2 instance at all. This session started one locally (see Session record above) and
completed a live, two-account certification pass against it.

### Why the live calls were not made through Unity itself

Two Unity-native approaches were tried first and both hit hard technical walls, not just
inconvenience:

- **EditMode `[UnityTest]`**: real `UnityWebRequest` calls never complete in this Unity
  version's batchmode EditMode test runner. Confirmed empirically with a throwaway diagnostic that
  manually polled a raw `UnityWebRequest.isDone` every frame for 600 frames against the (reachable,
  responding-to-curl) live backend - it stayed `InProgress`, `responseCode=0`, the entire time. The
  existing `CoroutineTestRunner` (a synchronous `while(MoveNext())` busy-loop) only works because
  every other EditMode test in this project uses `FakeApiTransport`, which completes on the first
  `MoveNext()` - it was never exercised against a real async network call before.
- **PlayMode `[UnityTest]`**: PlayMode runs a real player loop, so real `UnityWebRequest` calls do
  complete there - but `Assets/Tests/PlayMode`'s assembly definition (`Level5.PlayModeTests.asmdef`)
  cannot reference the default assembly where every `Level5.BackendV2.*` client class lives. This is
  a hard Unity engine restriction (a named assembly definition can never reference the implicit
  default assembly), already documented in this exact repo by
  `Level5BackendV2CorrespondenceScenePlayModeTests.cs`'s own comment, not something this session
  could route around without either moving the client library into its own assembly (a real
  architecture change, out of scope for a certification slice) or adding certification-only code
  into the shipped default assembly.

### What was used instead

A standalone `HttpClient`-based console tool
(`Level5Backend/v2/scripts/live-certification/`, with its own `README.md`) that replicates the
*exact same REST contract* Unity's typed clients use - same routes, same camelCase JSON DTOs (cross-
checked directly against `Level5.Api`'s own controller/DTO records, not assumed), same auth flow.
Two independent `HttpClient` instances (one per account) give genuine session isolation - including
true concurrent dispatch for the Scenario 8 race check - which Unity's own client stack cannot give
in a single process, since `BackendV2SessionStore` is a deliberate process-global singleton (this is
a single-local-player game; it was never designed to hold two sessions at once). This matches the
certification doc's own "isolated in-process clients... exercise the same backend API, auth, and
session paths" acceptable setup.

**What this session's live evidence does and does not cover:** this is black-box *server*
certification - it proves Backend V2's actual HTTP behavior (auth, idempotency, sealed-result
projection, restart survival, concurrent-completion resolution) against a live database, with real
account credentials and real HTTP round trips. It does **not** exercise Unity's own
`UnityWebRequestTransport`/`AuthApiClient`/`CorrespondenceScreenController`/`RemoteAttemptLauncher`
code paths - those still only have the `FakeApiTransport`-based unit coverage and the PlayMode scene
smoke test from the prior session (see per-scenario notes below for exactly which half of each
scenario this does and does not close). A future session with GUI/device access to actually drive
built Unity clients is still needed to certify the client-side wiring itself against a live backend.

Two phases were run, with a genuine backend process kill+restart in between (not simulated) so
Scenario 6 is real:

- **Phase 1** (`dotnet run -- phase1`): registered two fresh accounts, formed a friendship, created
  and accepted a Best-of-3 sealed challenge, played game 1 (including duplicate-request idempotency
  checks and the sealed-disclosure-timing check).
- Backend process killed (`taskkill`) and restarted fresh (`dotnet run --launch-profile https`),
  confirmed healthy via `/health/live` before continuing.
- **Phase 2** (`dotnet run -- phase2`): reconnected both accounts via fresh login (not a reused
  in-memory token), verified the series survived the restart unchanged, played the deciding game 2
  with a genuinely concurrent (`Task.WhenAll`) completion dispatch from both accounts, verified the
  completed series remains readable in both accounts' history.

Both phases passed every assertion on the first working run (after one bug fix to the harness itself
- an evidence-logging helper crashed on array-rooted JSON bodies; fixed before any certification
logic ran). Full harness stdout is reproducible via the tool's own README; the ids below are this
run's actual values, not illustrative examples.

### Live evidence captured this run

```text
Unity commit:            c300299f32ec07b5c92aea4863a67d8fffdf0f04
Backend commit:           62107269408a015664f5dee1aa8c394bf17706ea
Account A: username=certAbc6dd64d37  playerId=01a0ca81-b08f-7d82-99bb-d76038643d69
                          tag=CERTACCOUNTABC6DD64D#5189
Account B: username=certBbc6dd64d37  playerId=01a0ca81-b0c5-7b4c-b0fc-dcc1a75951af
                          tag=CERTACCOUNTBBC6DD64D#6919
Friend request id:        01a0ca81-b0de-7072-b1bd-136ffa85cc58
Series id:                 01a0ca81-b183-70c8-b1e9-4de7a86e26ad
clientRequestId (create): 073e1443-65b0-4ee0-80af-0b5fa65c5db9
Winner (series complete): 01a0ca81-b08f-7d82-99bb-d76038643d69 (Account A, as designed by the harness)
Sample conflict response (Scenario 5): HTTP 409, traceId=0HNOOKLJTCUD1:0000001A,
  title="Attempt 01a0ca81-b247-7a10-a82b-d89dcb668968 was already completed with a different
  result. The accepted result cannot be replaced."
```

Every call in both phases logged its own status code and client-generated `X-Correlation-Id`; the
one shown above additionally carries the server's own `traceId` because it was the one call this run
that produced an error response (ProblemDetails only carries `traceId` on non-2xx responses, per
`Level5.Api.ErrorHandling.ApiExceptionHandler`).

### Follow-up harness fixes (code review), re-verified live

A senior-engineer review of the harness itself (`Level5Backend/v2/scripts/live-certification/`)
found four issues, all fixed and the full two-phase run repeated end-to-end against the live backend
to confirm nothing regressed:

- The README was missing the `dotnet dev-certs https --trust` step this session actually needed
  (plain `HttpClient` honors the OS cert store, unlike `curl -k`/`Invoke-WebRequest -SkipCertificateCheck`)
  - now documented as step 1.
- The state file handoff between phases carries a plaintext password; it is now deleted in a
  `try`/`finally` so it can never survive a failed or skipped Phase 2, not only a successful one.
- `SendAsync` now clones a `JsonElement` and disposes the underlying `JsonDocument` immediately,
  instead of returning the still-open, never-disposed `JsonDocument` every call site had been holding
  onto for the rest of the run.
- Phase 2 now also exercises `POST /api/v2/auth/refresh` live for both accounts (see Scenario 3
  below) - previously `Account.RefreshToken` was captured but never actually used by the harness.

Both phases were re-run against the live backend after these fixes (fresh accounts, a second genuine
backend process kill+restart) and passed every assertion again; the ids above are from that original
run, not the re-verification run.

## Unity-client live-certification attempt (2026-09-22, continued session)

A same-day follow-up session targeted exactly the gap the section above calls out: certifying Unity's
actual `UnityWebRequestTransport` / `AuthApiClient` / `CorrespondenceScreenController` /
`RemoteAttemptLauncher` / `PendingRemoteAttemptResult` / `RemoteAttemptResultSubmitter` code paths
against a live backend, not just the server's own HTTP contract. This session had no interactive
GUI/device access either (a non-interactive CLI agent session, no screen, no click/tap capability, no
physical or emulated device) - the same constraint the prior session's "Remaining work" section
already anticipated. Unlike the prior session, this one built and attempted to run a real, narrow
Editor Play Mode automation script to get genuine Unity-client evidence without a human operator. It
did not reach that evidence; the reason is recorded precisely below rather than papered over.

| Field | Value |
| --- | --- |
| Unity commit (base) | `0e6d91302b64a284924286b46bf279eba5cce16a` (`dev`) - unchanged by this session except the new, additive `Assets/Level5/Editor/BackendV2LiveCertificationRunner.cs` |
| Backend commit (base) | `c98a25ce1df16efa3d579e34286d93af346d084c` (`dev`, `Level5Backend` repo) - unchanged except the additive `unity-counterpart` commands in `v2/scripts/live-certification/Program.cs` |
| Backend environment / base URI | `https://localhost:7029` (already running and confirmed healthy via `/health/live` at session start - not started fresh this session) |
| Unity client type | **None reached** - intended Editor Play Mode (batchmode, `-executeMethod`), never entered due to the hang documented below |
| Date/time | 2026-09-22 (same day as, immediately following, the session above) |
| Tester / validator | Claude Code (automated agent), this session - no human operator, no GUI/device available |
| Accounts used | None created - registration was the first live network call the automation intended to make, never reached |
| Session isolation method | N/A - no Unity-client session was ever established |

### What was built

- **`Assets/Level5/Editor/BackendV2LiveCertificationRunner.cs`** (new, this session): a single-purpose
  Editor batchmode automation, living in the same Editor-only, no-asmdef folder as the existing
  `BackendV2CorrespondenceSceneBootstrap.cs` (so it can reference `CorrespondenceScreenController` and
  `Level5.BackendV2.*` by type, unlike a `Level5.PlayModeTests.asmdef` test - see that class's own doc
  comment for why that restriction exists). It does not reimplement anything: it drives the real
  `CorrespondenceScreenController` by setting `InputField.text` and invoking a real `Button`'s real
  `onClick` (the exact `UnityEvent` a physical click fires), via reflection only where the controller's
  fields are private (it has no public API at all - by design, see its own doc comment). Its intended
  session: enter real Editor Play Mode (real `UnityWebRequest` calls complete there, unlike the
  batchmode EditMode test runner - see "Live certification session" above for why that already ruled
  out `[UnityTest]` EditMode for this), register a fresh account directly through
  `BackendV2Runtime.Auth.Register` (real production client, one layer below where the UI has no
  register button), load `level_00_multiplayer`, drive the real Sign In button, confirm
  `BackendV2SessionPersistenceStore` persisted a session file, reload the scene to exercise
  `CorrespondenceScreenController.Resume()`'s restore path, click through every tab, and - via a
  counterpart account - accept a live friend request and challenge and click a real "Your Turn" → Play
  button to drive `RemoteAttemptLauncher.Run` through to `SceneTransition.LoadScene`. Confirmed to
  **compile cleanly** (`Assembly-CSharp-Editor.dll` built with 0 errors, verified via a
  `-batchmode -quit` compile-only pass) and its `RunFullSession()` entry point is confirmed to
  **execute** (its first log line reliably appears - see below); it was never observed to reach
  `RunFullSession`'s "Step 0" line, i.e. never got the chance to run its own logic.
- **`Level5Backend/v2/scripts/live-certification/Program.cs`** (extended, this session): two new
  sub-commands, `unity-counterpart friend <tag>` and `unity-counterpart challenge`, reusing the exact
  same real-backend-contract helpers (`Register`, `Login`, `SendAsync`, `Expect`) the existing
  phase1/phase2 flow already uses. These play the second account ("Account B") opposite a
  Unity-driven "Account A", timed to act at the exact points a second human player would (send a
  friend request once Account A's live tag is known; create a challenge only after confirming Account
  A has already accepted the friendship live) - handed off via a small state file
  (`%TEMP%\level5_unity_live_cert_b_state.json`), the same pattern `statePath` already uses for
  phase1/phase2. `dotnet build` confirmed this extension compiles cleanly (0 errors). Account B here
  is explicitly **not** Unity-driven - exactly as already accepted for the server-side pass above -
  only Account A's actions were ever intended to be Unity-client evidence.

### What happened instead: a reproducible Editor-startup hang, not a script bug

Every attempt to actually enter Play Mode in this environment hung indefinitely, always at the
identical point in Unity's own generic startup sequence - **before** `RunFullSession`'s first log line
ever runs, i.e. before any of this session's own code executes at all:

```text
Start Indexing on Editor startup
[Indexing] Starting Initial Indexing for Assets
Created GICache directory at ... (completes fine)
[Licensing::Client] Successfully resolved entitlement details  (x2, completes fine)
[remote] error: Init socket failed
TrimDiskCacheJob: Current cache size 0mb
<nothing further, ever>
```

Five independent invocations were tried, each killed and cleaned up (stale `Temp/UnityLockfile`
removed) before the next:

1. Default: `-batchmode -projectPath . -executeMethod BackendV2LiveCertificationRunner.RunFullSession`
   - hung, unbounded (first observed at ~10 minutes stale, no further log growth).
2. With `-nographics` added - hung identically (the certification doc's own advice above is against
   `-nographics` specifically for the `-runTests` PlayMode *test runner*'s silent-completion quirk;
   this rules out graphics/rendering as the cause for this different, non-`-runTests` hang).
3. With the CLI sandbox explicitly disabled for the invocation (testing whether a sandboxed shell was
   blocking the local socket the `[remote] error: Init socket failed` line points at) - hung
   identically.
4. With the project's `Library/Search` index cache (120 MB, the directory this exact subsystem name
   maps to) deleted first, forcing a rebuild - briefly showed real work (main process memory rose to
   ~2 GB, consistent with an active rebuild) before landing at the exact same hung state a few minutes
   later.
5. A `-quit`-added diagnostic (`-batchmode -quit -projectPath . -executeMethod ...`) completed
   quickly and cleanly, and its log confirms `RunFullSession()` itself *does* run (the evidence file's
   session-start line is written, which only that method writes) - but the log never reaches the
   `TrimDiskCacheJob` line at all in that run. This isolates the hang to whatever Unity does once idle
   after startup while genuinely waiting for Play Mode to finish entering (which `-quit` short-circuits
   before it can happen) - not to this script's own code, which had already run and returned by then.

Every attempt was confirmed hung, not merely slow, by process memory dropping back to idle levels
(under 100 MB, from a startup peak over 1-2 GB) while the log file stopped growing entirely for 5+
minutes at a time with no crash, error dialog, or exit.

**Conclusion:** this is a genuine, reproducible Unity Editor startup-indexing deadlock specific to
this session's non-interactive batchmode environment (confirmed unaffected by graphics mode, CLI
sandboxing, or a from-scratch index rebuild), not a defect in
`BackendV2LiveCertificationRunner.cs` or the counterpart harness extension - both are confirmed to
compile, and the runner is confirmed to start executing. It sits entirely outside this issue's scope
to fix (it is a Unity Editor/environment concern, not a Backend V2 or correspondence-client concern),
and attempting to route around it further (e.g. patching Unity Search preferences, disabling
subsystems project-wide) risked exactly the kind of broad, unrelated environment surgery a narrow
certification slice should not do.

### Per-tab and remote-attempt-launch status

| Tab / flow | Status | Account | Server state expected | Unity UI state observed | Notes |
| --- | --- | --- | --- | --- | --- |
| Friends | BLOCKED | N/A | N/A | N/A | No Unity-client session ever reached the correspondence screen - see hang investigation above. |
| Incoming Challenges | BLOCKED | N/A | N/A | N/A | Same. |
| Outgoing / Waiting | BLOCKED | N/A | N/A | N/A | Same. |
| Your Turn | BLOCKED | N/A | N/A | N/A | Same. |
| Active Series | BLOCKED | N/A | N/A | N/A | Same. |
| Completed | BLOCKED | N/A | N/A | N/A | Same. |
| Pending result banner | BLOCKED | N/A | N/A | N/A | Same; also see Scenario 5's client-side retry half above for the additional, independent reason this would need care even once Play Mode works. |
| Remote-attempt launch (Your Turn → Play → `RemoteAttemptLauncher.Run` → `SceneTransition.LoadScene`) | BLOCKED | N/A | N/A | N/A | Same - `BackendV2LiveCertificationRunner.RunFullSession` was written specifically to drive this chain via a real Play-button click, but never got past Editor startup to attempt it. |

### What this means for the scenarios below

No Unity-client-driven evidence was collected this session - none of the PASSING claims below are
based on it, and none should be inferred from the artifacts existing. `BackendV2LiveCertificationRunner.cs`
and the `unity-counterpart` harness commands are left in the tree, unused by anything else (not part of
any build, not referenced by the architecture tests' scanned paths - see
`Level5BackendV2ArchitectureTests.CorrespondenceUiRoot`/`ClientRoot`, which do not cover
`Assets/Level5/Editor/`), ready for a future session that has either a working interactive Editor/device
setup or an environment where this specific Unity startup hang does not occur.

## Certification matrix

**Superseded for scenarios 3, 5 and 11**: this table is this (2026-09-18/2026-09-22) session's own
point-in-time record, preserved as history. The "BLOCKED (Unity-client ... half)" notes on scenarios
3, 5 and 11 below were closed by the "Final Unity-client live certification (2026-09-23)" session at
the top of this document - see that section for the live evidence (real Sign-In/Resume across a
genuine process restart, a real Resend-result click, and real Bo3 completion). Scenario 11's
client-*build*-update half (as opposed to the backend-restart half already proven here) remains the
one still-untested optional clause, per that section's "Known limitations" note.

| # | Scenario | Status | Notes / evidence |
| --- | --- | --- | --- |
| 1 | Friend lookup and accepted friendship | **PASSING (live)** | A resolved B by tag (`CERTACCOUNTBBC6DD64D#6919`), sent a friend request (id `01a0ca81-b0de-7072-b1bd-136ffa85cc58`), B saw it in `ListIncoming`, B accepted (204), both `GET /api/v2/friends` show the other side. All against the live server. |
| 2 | Create/accept a Best-of-3 sealed challenge | **PASSING (live)** | A created series `01a0ca81-b183-70c8-b1e9-4de7a86e26ad` (Bo3, `rulesetId=score-only`) with `clientRequestId=073e1443-65b0-4ee0-80af-0b5fa65c5db9`; server-returned `rules.informationPolicy == "SealedAttempt"`, `totalGames == 3`, initial `status == "PendingAcceptance"`. B saw it via `ListIncoming`, accepted, status became `"Active"`. Both accounts' `ListActive` show it. |
| 3 | Quit/reconnect between turns | **PARTIAL (live, login-path + refresh-token half only) / BLOCKED (Unity-client Resume() half)** | Live-proven: between phase 1 and phase 2 this session's whole process exited and a fresh one reconnected both accounts via `POST /api/v2/auth/login` (not a reused token) and continued the same series correctly - the "login path works correctly" outcome the scenario explicitly accepts. A code-review follow-up pass also added a live `POST /api/v2/auth/refresh` exercise right after login (rotating both accounts' credentials, with every subsequent call in phase 2 using the rotated token) - the token-rotation path the real Unity client's `AuthenticatedApiClientBase.EnsureFreshAccessToken`/`ForceRefresh` actually relies on far more often than re-login, given the 15-minute access-token lifetime; re-run and reconfirmed passing live after that fix. **BLOCKED (Unity-client Resume() half)**: a same-day follow-up session built a real Editor Play Mode automation (`BackendV2LiveCertificationRunner.RunFullSession`) specifically to drive the persisted-session-file / `CorrespondenceScreenController.Resume()` path for real, but every attempt to enter Play Mode hung in a reproducible Unity Editor startup deadlock before that script's own logic ever ran - see "Unity-client live-certification attempt" above for the full investigation (5 independent attempts, confirmed unrelated to this session's own code). No Unity-client evidence for this half exists. The prior session's `Level5BackendV2CorrespondenceScenePlayModeTests` PlayMode coverage of the unauthenticated-path-only case still stands unchanged. |
| 4 | Duplicate/retried challenge, accept, start, complete requests | **PASSING (live)** | All four sub-cases proven against the real server in one run: (a) duplicate `CreateChallenge` with the same `clientRequestId` returned the same series id, not a new one; (b) duplicate `Accept` on an already-`Active` series returned 200/`Active` again, not a conflict; (c) duplicate `StartAttempt` for the same player/game returned the same `attemptId`; (d) an identical resend of an already-accepted `CompleteAttempt` payload returned 200 (idempotent-success), unchanged. |
| 5 | Lost response after an accepted completion | **PARTIAL (live, server half only) / BLOCKED (Unity-client retry half)** | Live-proven server half: resending `CompleteAttempt` for an already-accepted attempt with a *materially different* payload was rejected `409`/conflict (traceId `0HNOOKLJTCUD1:0000001A`, see evidence above), and the originally accepted result was confirmed unchanged afterward (`currentGameNumber` had already advanced to 2). This is the actual "server handles duplicate/accepted result safely" proof the scenario asks for. **BLOCKED (Unity-client retry half)**: the client's own `PendingRemoteAttemptResult` resend-exact-original-payload path (`RemoteAttemptResultSubmitter.TryRetryPending()`) was not exercised live this session for two independent reasons: (1) the same Editor Play Mode startup deadlock documented under "Unity-client live-certification attempt" above prevented any Unity-client session from running at all; (2) even had Play Mode been reachable, safely inducing a genuinely *retryable* submission failure through Unity would have required either simulating a full completed match's `GameStats` (gameplay simulation, judged out of scope for a narrow certification script) or deliberately breaking network reachability mid-submission - both were judged to reach beyond this certification slice's scope rather than attempted under time pressure. It remains unit-tested against a fabricated 409 only, as in the prior session. |
| 6 | Server restart between turns | **PASSING (live)** | The backend process was actually killed (`taskkill`) and restarted (`dotnet run`) between phase 1 and phase 2, not simulated. After restart: series `01a0ca81-b183-70c8-b1e9-4de7a86e26ad` was still `Active`, `currentGameNumber` was still `2`, game 1's already-disclosed result was still present and correct, and `rules` (ruleset id, information policy) were byte-identical to what was returned at creation - proving Postgres-persisted state, not in-memory state that a restart would have lost. |
| 7 | Second-device/state-refresh behavior | **PASSING (live)** | Demonstrated throughout both phases: every read one account made was a fresh HTTP round trip issued *after* the other account's mutation, using an entirely independent `HttpClient`/token with no shared client-side cache of any kind - B always learned about A's actions (and vice versa) strictly from the server's current response, never from anything cached locally. |
| 8 | Simultaneous completion race | **PASSING (live)** | The deciding game (game 2) was completed by both accounts via a genuinely concurrent dispatch (`Task.WhenAll` on two independent `HttpClient`s, both `CompleteAttempt` calls in flight at once, not sequential). Both calls returned 200; the server resolved to a single consistent outcome (`status == "Completed"`, `winnerId` == Account A's id, no corrupted/duplicate state). |
| 9 | Sealed first-finisher result remains hidden | **PASSING (live)** | Direct server-projection proof, not client-side classifier inference: after A completed game 1's attempt, B's own `GET /api/v2/series/{id}` showed `opponentAttempt.status != "NotStarted"` (A had submitted something) but `opponentAttempt.result == null` (still sealed). After B then completed their own attempt, A's next `GET` showed `opponentAttempt.result` populated (disclosed). This proves the *server's* projection, exactly as the scenario requires. |
| 10 | Completed series history remains readable | **PASSING (live)** | After the series completed, both accounts' `GET /api/v2/series/completed` listed it, and a second, independent fetch on account A afterward still listed it - readable across a repeated fresh round trip, not a one-time artifact of the completion response itself. |
| 11 | Frozen rules remain stable across a client update | **PARTIAL (live, restart half only) / BLOCKED (client-build half)** | Live-proven: `FrozenRules` (ruleset id, ruleset version, information policy, comparison keys) were confirmed byte-identical before and after the real backend process restart in Scenario 6's evidence. **Still BLOCKED**: only a single Unity Editor instance was available in this environment - no second, distinct Unity client build/version exists to test whether an in-progress series' frozen rules remain stable (or fail safely) across an actual client update. This is unchanged by the same-day follow-up session (see "Unity-client live-certification attempt" above): that session could not even complete a single Unity-client run, so a second-build comparison was never in reach regardless. |

### Historical: prior blocked session (2026-09-18)

`curl -k --max-time 5 https://localhost:7029/` from that session's execution environment returned
connection failure (exit code 7, "Failed to connect" - nothing listening on that port/host from
there), both for the bare base URI and for `api/v2/players/me`. The user indicated a local dev
backend was running at this address; it was not reachable from the environment that coding session
executed in when checked at the time. Every scenario was BLOCKED for live execution that session;
what was validated instead (all `FakeApiTransport`-based unit/PlayMode coverage, not live server
behavior) is preserved in "What was validated without a live backend (prior session, 2026-09-18)"
below, since none of it became stale - it is still true and still passing (re-confirmed this
session, see "This session's re-validation" below).

## What was validated without a live backend (prior session, 2026-09-18)

Preserved verbatim from the prior blocked session - still true and re-confirmed passing this
session (see "This session's re-validation" below), since a certification client (this doc) never
retires prior coverage without checking it first:

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

## This session's re-validation (2026-09-22)

Before writing up the live results above, this session re-ran every check the prior session ran,
against the current `dev` HEAD (`c300299f...`, which had moved on since the prior session's base
commit), to confirm nothing regressed and nothing in this session's harness work touched the Unity
project itself (it did not - the live-certification harness lives entirely in the `Level5Backend`
repo, outside `Assets/`):

- Full solution compile via Unity batchmode (`-batchmode -quit -nographics`, no `-runTests`) -
  **clean**.
- Full EditMode suite: **1610/1610 passed**.
- Full PlayMode suite: **24/24 passed**.
- `./scripts/validate-repository.ps1` - **passed**.

### Unity-client live-certification attempt session's own validation (2026-09-22, continued)

After the Play Mode hang investigation above concluded, this follow-up session re-ran the same checks
against its own final state (Unity commit unchanged at `0e6d91302b64a284924286b46bf279eba5cce16a` -
only the new, additive `Assets/Level5/Editor/BackendV2LiveCertificationRunner.cs` was added, nothing
existing was modified):

- Full solution compile via Unity batchmode (`-batchmode -quit -nographics`, no `-runTests`) -
  **clean** (this same invocation shape is also how `BackendV2LiveCertificationRunner.cs` was first
  confirmed to compile, before the Play Mode hang was ever hit).
- Full EditMode suite: **1610/1610 passed**.
- Full PlayMode suite: **24/24 passed**.
- `./scripts/validate-repository.ps1` - **passed**.

Notably, `-runTests` (both EditMode and PlayMode) is unaffected by the Play Mode startup hang
documented above - both suites ran to completion normally in this same environment, including the
existing `Level5BackendV2CorrespondenceScenePlayModeTests` PlayMode scene smoke test, which itself
enters a real player loop. This narrows the hang specifically to the non-`-runTests`,
`-executeMethod` + `EditorApplication.isPlaying = true` path this session's automation needed, not to
Play Mode / real networking in this environment generally.

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

## Reproducing this live certification

The live two-account pass documented above is reproducible with
`Level5Backend/v2/scripts/live-certification/` (see that folder's own `README.md` for full usage,
prerequisites, and known limitations):

```powershell
# 1. Trust the local ASP.NET Core HTTPS dev cert (one-time per machine)
dotnet dev-certs https --trust

# 2. Start Backend V2 locally
cd Level5Backend/v2
./scripts/setup-local-dev.ps1
cd src/Level5.Api
dotnet run --launch-profile https
# confirm: curl -k https://localhost:7029/health/live

# 3. In another terminal
cd Level5Backend/v2/scripts/live-certification
dotnet run -- phase1

# 4. Restart the backend process (kill it, run `dotnet run` again) to exercise Scenario 6 for real

dotnet run -- phase2
```

## Reproducing the Unity-client live-certification attempt

`BackendV2LiveCertificationRunner.cs` (`Assets/Level5/Editor/`) and the `unity-counterpart` harness
commands (`Level5Backend/v2/scripts/live-certification/Program.cs`) are left in place for a future
session, ideally one that either has interactive Editor/device access or runs on a machine where the
Play Mode startup hang documented above does not occur:

```powershell
# 1. Backend V2 running locally (same as "Reproducing this live certification" above), reachable at
#    https://localhost:7029/health/live

# 2. From the level5 repo root - no -quit (the script calls EditorApplication.Exit itself) and no
#    -nographics (this drives real UI/coroutine behavior):
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -executeMethod `
    BackendV2LiveCertificationRunner.RunFullSession -logFile unity_live_cert.log

# 3. Evidence accumulates at %TEMP%\level5_unity_live_cert_evidence.log across the whole session,
#    interleaved with the Debug.Log output also visible in -logFile.
```

Optional environment variables (read at runtime, never hardcoded per this issue's own instructions):
`LEVEL5_BACKENDV2_BASE_URI` (defaults to `BackendV2ApiConfig.Development()`'s
`https://localhost:7029/`) and `LEVEL5_BACKEND_REPO_PATH` (defaults to assuming `Level5Backend` is a
sibling checkout of `level5`, matching every other cross-repo path this doc already assumes).

If the exact hang documented above recurs (`TrimDiskCacheJob: Current cache size 0mb` with no further
log growth for several minutes, process memory settling back under 100 MB), that is this same Unity
Editor environment issue, not a regression in the script - see "Unity-client live-certification
attempt" above for the full investigation before spending further time on it.

## Remaining Backend V2 correspondence work

**Update (2026-09-23): items 1-3 below are closed** - see "Final Unity-client live certification
(2026-09-23)" at the top of this document. Item 4 (a genuine second Unity client build/version)
remains open, per that section's "Known limitations" note; item 5 was itself the production defect
that section found and fixed (Backend V2's one entry did not actually match anything in Unity's own
catalog - the fix added a second entry that does, rather than proving the existing one). Preserved
below verbatim as this session's own point-in-time record.

What this live certification pass did **not** close, and what a future session should target:

1. **Unity client-side wiring against a live backend, still open** - a same-day follow-up session
   (see "Unity-client live-certification attempt" above) built real Editor Play Mode automation
   (`BackendV2LiveCertificationRunner.cs`) and an extended counterpart harness specifically to close
   this, but could not get it to run at all in this environment (a reproducible Unity Editor
   startup-indexing deadlock, investigated five ways, confirmed unrelated to this session's own code -
   see "Reproducing the Unity-client live-certification attempt" above to retry). `UnityWebRequestTransport`,
   `AuthApiClient`, `CorrespondenceScreenController`, `RemoteAttemptLauncher`, and
   `PendingRemoteAttemptResult` still only have `FakeApiTransport`-based unit coverage and one
   PlayMode scene smoke test (unauthenticated path only). A future session should either retry the
   automation above on an environment without this hang, or fall back to genuine interactive
   Editor/device access (the "two real installs/devices" or "one device plus one Editor session"
   setups this doc's earlier draft anticipated).
2. **Scenario 3's UI/session-persistence half** - the persisted-session-file /
   `CorrespondenceScreenController.Resume()` path specifically, requiring #1 above.
3. **Scenario 5's client-side retry half** - `PendingRemoteAttemptResult`'s exact-original-payload
   resend, requiring #1 above. Note that even with #1 resolved, safely inducing a genuinely retryable
   submission failure through Unity still needs either a fabricated `GameStats` (gameplay simulation)
   or a deliberate mid-submission network break - plan for that explicitly, it is not automatic once
   Play Mode works.
4. **Scenario 11's client-build half** - frozen-rules stability across an actual Unity client update
   needs two distinct client builds; only the backend-restart half was provable this session, and #1's
   automation - even once it runs - only ever drives a single Editor instance, so this specific half
   still needs either two real builds or two Editor copies at different commits.
5. If Backend V2's ruleset catalog (`StaticRulesetCatalog`) grows beyond its current single
   `score-only` entry, re-run the live-certification harness with the new ruleset id(s) too - this
   session only certified the one entry that exists today.

No defects were found in Backend V2 itself this session - every live assertion passed on the first
working run of the harness (after one bug in the harness's own evidence-logging code, fixed before
any certification logic ran; see "Live certification session" above).
