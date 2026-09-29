# Backend V2 online-account live process-restart and logout certification (issue #194)

Companion to [`docs/backend-v2-online-account-ui.md`](backend-v2-online-account-ui.md) (UI flow, PR
#192) and [`docs/backend-v2-correspondence-certification.md`](backend-v2-correspondence-certification.md)
(the equivalent certification for correspondence, issue #159, whose fixture pattern this one reuses).

PR #192 shipped the Backend V2 online-account UI but explicitly flagged its own gap:

> Live Backend V2 certification (register -> restore -> logout across a real process restart) - not
> attempted this pass ... Flagged as a follow-up.

This session closed that gap.

## Session record

| Field | Value |
| --- | --- |
| Unity commit (base) | `abd93133db15dbb186bd7c0445246664d323941e` (`dev`) + this session's own commit (branch `backendv2-issue194-online-account-live-certification`) |
| Backend commit (base) | `8613c05` (`dev`) - **note**: `dev` had advanced past the originally-audited `1b905fd` via PR #46, which reverted #44 (legacy account migration) in full; this session re-based on current `dev` per its own instructions rather than the stale reference SHA |
| Backend environment / base URI | `https://localhost:7029` - local `dotnet run --launch-profile https` against the local Postgres container (`level5-postgres-local`), started via `v2/scripts/setup-local-dev.ps1` |
| Unity version | 6000.5.7f1 |
| Test accounts | One freshly-registered account per run (`unityOnlineCert<suffix>`, unique per run), registered and driven entirely through the real Unity UI - no counterpart/second client needed for this certification (unlike correspondence, this flow is single-account) |
| Date/time | 2026-09-26 |
| Tester / validator | Claude Code (automated agent), this session |
| Session1 | **PASSED** (live) |
| Session2 | **PASSED** (live) |
| PlayerId continuity | Confirmed - Session2's restored session, self-profile fetch, and rendered UI all matched Session1's handoff `PlayerId`/`DisplayName`/`Tag` |
| Ordinary EditMode/PlayMode/`validate-repository.ps1` | EditMode 1786/1786 passed; PlayMode 29/29 passed, 9 skipped (7 pre-existing live-cert/ignored + this fixture's own 2, both opt-in); repository validation passed |

## What each session certifies

**Session1** (`Session1_RegisterAndPersist`) - one Unity process:

1. Clears certification-owned local state (persisted session, handoff file) so the run starts from a
   genuinely fresh/unauthenticated state.
2. Loads the real `level_00_account` (Account hub) scene and clicks the real Online Account button
   (`AccountHubUiObjects.onlineAccountButton`, resolved via `AccountManager`) - confirms the scene
   changes to `level_00_account_online`, never skipping the hub route.
3. Sets the real Register form's username/password/display-name `TMP_InputField`s and invokes the real
   `RegisterButton.onClick` - never calling `OnlineAccountCoordinator.Register` directly.
4. Waits for the real register-then-self-profile round trip to settle
   (`OnlineAccountCoordinator.Profile`/`ProfileError`, not just the signed-in panel toggling - see
   "Production code: no defect; test race found and fixed" below for why), then asserts the live
   `PlayerId`/rendered `DisplayName`/`Tag` and that `BackendV2SessionPersistenceStore` wrote a real
   session file matching the new account.
5. Writes a small, non-secret handoff file (`PlayerId`/`DisplayName`/`Tag`/`Username` only - never a
   password, access token, or refresh token) and lets the process exit.

**Session2** (`Session2_RestoreAndSignOut`) - a **separate** Unity process, launched only after
Session1's process fully exited:

1. Asserts no in-memory session exists (the fresh-process check), then loads the real first production
   scene, `level_00_account_loginLocal`, and confirms `UserAccountManager.Awake ->
   BackendV2SessionPersistenceBootstrap.EnsureInitialized -> BackendV2SessionPersistenceStore.TryLoad ->
   BackendV2SessionStore.Set` restored Session1's persisted session - the actual process-restart proof
   (that bootstrap has process-static initialization state, so a same-process scene reload would not
   exercise this path at all).
2. Re-opens the online-account screen through the real Account hub -> Online Account button again (no
   credentials entered) and waits for `OnlineAccountCoordinator.EnterScreen()`'s real
   `GET api/v2/players/me/profile` to complete against the live backend, confirming the same
   `PlayerId`/`DisplayName`/`Tag` Session1 registered - proving the restored credential still works
   live, not merely that a JSON file could be read.
3. Captures the current refresh token in memory only, clicks the real Sign Out button, and confirms the
   in-memory session clears and `BackendV2SessionPersistenceStore.TryLoad` can no longer restore it.
4. Uses the existing typed `BackendV2Runtime.Auth.Refresh` client with the captured old refresh token
   and confirms Backend V2 rejects it (`Success == false`, `ErrorKind == ApiErrorKind.Unauthenticated`) -
   proving the real Sign Out click reached `POST /api/v2/auth/logout -> LogoutUseCase ->
   AuthSession.Revoke` server-side, not just a local session clear.
5. Deletes the handoff file and clears local certification state.

## Production code: no defect; test race found and fixed

The first Session1 attempt failed an assertion (`displayNameText.text` was empty right after
registering) that looked at first like a live production defect in the new register-then-self-profile
path. Root-caused via a raw `curl` repro against the same live Backend V2 instance (register, then
immediately `GET api/v2/players/me/profile` with the returned access token) - **the server handled
that sequence correctly** (`HTTP_STATUS:200` with the correct profile), ruling out a backend issue.

The actual cause: `OnlineAccountController` subscribes to `BackendV2SessionStore.Changed`, which fires
`Render()` **synchronously** the instant `OnlineAccountCoordinator.Register` sets the session -
before that same `Register` call's own subsequent self-profile fetch
(`CompleteAuth -> RefreshProfileCore`) has had a chance to run. So `signedInPanel.activeSelf` can
already be `true` (session authenticated) while `Profile`/`ProfileError` are both still their initial
`null` - the fixture's original wait condition (`IsAuthenticated && signedInPanel.activeSelf`) was
satisfied at that earlier point, before the profile ever populated. This is correct, intentional
production behavior (the same immediate-re-render-on-session-change the online-account UI doc already
describes for other cases), not a bug - only the test's own wait condition was too loose. Fixed by
waiting on `OnlineAccountCoordinator.Profile != null || ProfileError != null` instead (the same
approach the fixture already used for Session2's restored-session case), which is what actually proves
the live register-then-fetch round trip settled. No production code changed.

## Reproducing this session

```powershell
# 1. Start Backend V2 locally (confirm https://localhost:7029/health/live):
./v2/scripts/setup-local-dev.ps1   # from the Level5Backend repo root
# then, from v2/src/Level5.Api:
dotnet run --launch-profile https

# 2. From the level5 repo root, one Unity process per session (session2 only after
#    session1's process has fully exited):
$env:LEVEL5_LIVE_CERTIFICATION = "1"
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode `
    -testFilter "BackendV2LiveOnlineAccountCertificationTests.Session1_RegisterAndPersist" `
    -testResults session1_results.xml -logFile session1.log

& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode `
    -testFilter "BackendV2LiveOnlineAccountCertificationTests.Session2_RestoreAndSignOut" `
    -testResults session2_results.xml -logFile session2.log

# 3. Evidence accumulates at %TEMP%\level5_unity_online_account_live_cert_evidence.log across
#    both processes.
```

Both `[UnityTest]`s are skipped (`Assert.Ignore`, with a clear prerequisite message) when
`LEVEL5_LIVE_CERTIFICATION` is unset, so the ordinary `-runTests -testPlatform PlayMode` suite (no env
var set) is unaffected - confirmed this session: 29/29 pre-existing PlayMode tests still pass, plus 9
skipped (7 pre-existing live-cert/ignored + this fixture's own 2 new ones).

## Completion criteria (all met, live)

- Account hub opens the online-account screen through its actual button.
- A new Backend V2 account is registered through the actual Register UI.
- The real profile display shows the returned display name/tag.
- The resulting Backend V2 session is durably persisted.
- A fresh Unity process restores that exact player through `UserAccountManager.Awake` - no fresh login
  required after restart.
- The online-account screen fetches and displays the same live self-profile after restoration.
- The real Sign Out button clears the in-memory session.
- Persisted session state cannot be reloaded afterward.
- The old refresh token is rejected by Backend V2 after logout.
- Ordinary EditMode/PlayMode/repository validation remains green.

## Local-profile independence (follow-up to PR #205)

Session2 additionally certifies, against the session restored by the fresh process and a real backend, that
local-profile operations never touch the Backend V2 identity. After the restoration proof and before the
online-account screen is reopened, `RunLocalProfileIndependenceCheck`:

1. selects **guest** through the real guest row on `level_00_account_loginLocal`, then
2. creates a **local profile** through the real Account Hub -> Create Local Profile -> Create Profile flow,

and after each asserts the Backend V2 session is still authenticated, is the *same object* with the same
access/refresh tokens, and that no `BackendV2SessionStore.Changed` event fired (a refresh, replacement or
sign-out would each raise one). Both sessions now open a throwaway SQLite file (`LocalProfileTestDatabase`)
instead of the developer's real `level5.db`.

Run 2026-09-29 against Backend V2 `dev` `8613c05` (local Postgres container), Unity 6000.5.7f1, Unity `dev`
`d278f4b` + the local-profile list-loading fix: Session1 **PASSED**, Session2 **PASSED** (all pre-existing
proofs plus the two independence checks above).
