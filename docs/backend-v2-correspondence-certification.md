# Backend V2 correspondence end-to-end certification (issue #159)

Companion to [`docs/backend-v2-correspondence-ui.md`](backend-v2-correspondence-ui.md) (UI flow) and
[`docs/backend-v2-client.md`](backend-v2-client.md) (typed client layer, issue #158).

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
| Overall status | **PARTIAL** - see matrix below: 8 of 11 scenarios fully live-passed, 2 partially live-passed (a documented, non-UI half of each remains not-yet-exercisable this session), 1 partially live-passed with its second half explicitly BLOCKED (no second client build available) |

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

## Certification matrix

| # | Scenario | Status | Notes / evidence |
| --- | --- | --- | --- |
| 1 | Friend lookup and accepted friendship | **PASSING (live)** | A resolved B by tag (`CERTACCOUNTBBC6DD64D#6919`), sent a friend request (id `01a0ca81-b0de-7072-b1bd-136ffa85cc58`), B saw it in `ListIncoming`, B accepted (204), both `GET /api/v2/friends` show the other side. All against the live server. |
| 2 | Create/accept a Best-of-3 sealed challenge | **PASSING (live)** | A created series `01a0ca81-b183-70c8-b1e9-4de7a86e26ad` (Bo3, `rulesetId=score-only`) with `clientRequestId=073e1443-65b0-4ee0-80af-0b5fa65c5db9`; server-returned `rules.informationPolicy == "SealedAttempt"`, `totalGames == 3`, initial `status == "PendingAcceptance"`. B saw it via `ListIncoming`, accepted, status became `"Active"`. Both accounts' `ListActive` show it. |
| 3 | Quit/reconnect between turns | **PARTIAL (live, login-path + refresh-token half only)** | Live-proven: between phase 1 and phase 2 this session's whole process exited and a fresh one reconnected both accounts via `POST /api/v2/auth/login` (not a reused token) and continued the same series correctly - the "login path works correctly" outcome the scenario explicitly accepts. A code-review follow-up pass also added a live `POST /api/v2/auth/refresh` exercise right after login (rotating both accounts' credentials, with every subsequent call in phase 2 using the rotated token) - the token-rotation path the real Unity client's `AuthenticatedApiClientBase.EnsureFreshAccessToken`/`ForceRefresh` actually relies on far more often than re-login, given the 15-minute access-token lifetime; re-run and reconfirmed passing live after that fix. **Not exercised this session**: the persisted-session-file / `CorrespondenceScreenController.Resume()` UI half (needs a live Unity scene/UI session, which this session's harness deliberately does not touch - see "Why the live calls were not made through Unity itself" above). The prior session's `Level5BackendV2CorrespondenceScenePlayModeTests` PlayMode coverage of the unauthenticated-path-only case still stands unchanged. |
| 4 | Duplicate/retried challenge, accept, start, complete requests | **PASSING (live)** | All four sub-cases proven against the real server in one run: (a) duplicate `CreateChallenge` with the same `clientRequestId` returned the same series id, not a new one; (b) duplicate `Accept` on an already-`Active` series returned 200/`Active` again, not a conflict; (c) duplicate `StartAttempt` for the same player/game returned the same `attemptId`; (d) an identical resend of an already-accepted `CompleteAttempt` payload returned 200 (idempotent-success), unchanged. |
| 5 | Lost response after an accepted completion | **PARTIAL (live, server half only)** | Live-proven server half: resending `CompleteAttempt` for an already-accepted attempt with a *materially different* payload was rejected `409`/conflict (traceId `0HNOOKLJTCUD1:0000001A`, see evidence above), and the originally accepted result was confirmed unchanged afterward (`currentGameNumber` had already advanced to 2). This is the actual "server handles duplicate/accepted result safely" proof the scenario asks for. **Not exercised this session**: the client's own `PendingRemoteAttemptResult` resend-exact-original-payload path (`RemoteAttemptResultSubmitter`) - that is client code living in the default Unity assembly and needs a live scene/UI session to drive for real; it remains unit-tested against a fabricated 409 only, as in the prior session. |
| 6 | Server restart between turns | **PASSING (live)** | The backend process was actually killed (`taskkill`) and restarted (`dotnet run`) between phase 1 and phase 2, not simulated. After restart: series `01a0ca81-b183-70c8-b1e9-4de7a86e26ad` was still `Active`, `currentGameNumber` was still `2`, game 1's already-disclosed result was still present and correct, and `rules` (ruleset id, information policy) were byte-identical to what was returned at creation - proving Postgres-persisted state, not in-memory state that a restart would have lost. |
| 7 | Second-device/state-refresh behavior | **PASSING (live)** | Demonstrated throughout both phases: every read one account made was a fresh HTTP round trip issued *after* the other account's mutation, using an entirely independent `HttpClient`/token with no shared client-side cache of any kind - B always learned about A's actions (and vice versa) strictly from the server's current response, never from anything cached locally. |
| 8 | Simultaneous completion race | **PASSING (live)** | The deciding game (game 2) was completed by both accounts via a genuinely concurrent dispatch (`Task.WhenAll` on two independent `HttpClient`s, both `CompleteAttempt` calls in flight at once, not sequential). Both calls returned 200; the server resolved to a single consistent outcome (`status == "Completed"`, `winnerId` == Account A's id, no corrupted/duplicate state). |
| 9 | Sealed first-finisher result remains hidden | **PASSING (live)** | Direct server-projection proof, not client-side classifier inference: after A completed game 1's attempt, B's own `GET /api/v2/series/{id}` showed `opponentAttempt.status != "NotStarted"` (A had submitted something) but `opponentAttempt.result == null` (still sealed). After B then completed their own attempt, A's next `GET` showed `opponentAttempt.result` populated (disclosed). This proves the *server's* projection, exactly as the scenario requires. |
| 10 | Completed series history remains readable | **PASSING (live)** | After the series completed, both accounts' `GET /api/v2/series/completed` listed it, and a second, independent fetch on account A afterward still listed it - readable across a repeated fresh round trip, not a one-time artifact of the completion response itself. |
| 11 | Frozen rules remain stable across a client update | **PARTIAL (live, restart half only) / BLOCKED (client-build half)** | Live-proven: `FrozenRules` (ruleset id, ruleset version, information policy, comparison keys) were confirmed byte-identical before and after the real backend process restart in Scenario 6's evidence. **Still BLOCKED**: no second Unity client build/version was available this session to test whether an in-progress series' frozen rules remain stable (or fail safely) across an actual client update - that requires two distinct client builds, which this certification slice did not produce. |

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

## Remaining Backend V2 correspondence work

What this live certification pass did **not** close, and what a future session should target:

1. **Unity client-side wiring against a live backend** - everything this session certified is
   black-box server behavior over raw HTTP. `UnityWebRequestTransport`, `AuthApiClient`,
   `CorrespondenceScreenController`, `RemoteAttemptLauncher`, and `PendingRemoteAttemptResult` still
   only have `FakeApiTransport`-based unit coverage and one PlayMode scene smoke test (unauthenticated
   path only). A session with GUI/device access to run actual Unity builds against this same local
   backend is needed to certify that wiring for real - the "two real installs/devices" or "one device
   plus one Editor session" setups this doc's earlier draft anticipated, which this environment could
   not provide.
2. **Scenario 3's UI/session-persistence half** - the persisted-session-file /
   `CorrespondenceScreenController.Resume()` path specifically, requiring #1 above.
3. **Scenario 5's client-side retry half** - `PendingRemoteAttemptResult`'s exact-original-payload
   resend, requiring #1 above.
4. **Scenario 11's client-build half** - frozen-rules stability across an actual Unity client update
   needs two distinct client builds; only the backend-restart half was provable this session.
5. If Backend V2's ruleset catalog (`StaticRulesetCatalog`) grows beyond its current single
   `score-only` entry, re-run the live-certification harness with the new ruleset id(s) too - this
   session only certified the one entry that exists today.

No defects were found in Backend V2 itself this session - every live assertion passed on the first
working run of the harness (after one bug in the harness's own evidence-logging code, fixed before
any certification logic ran; see "Live certification session" above).
