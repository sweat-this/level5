# Backend V2 correspondence — History tab live certification (issue #196)

Companion to [`docs/backend-v2-correspondence-ui.md`](backend-v2-correspondence-ui.md) (UI flow,
including the History tab) and
[`docs/backend-v2-correspondence-certification.md`](backend-v2-correspondence-certification.md)
(the broader #159 correspondence certification, of which this is a focused follow-up). PR #193
added the History tab (`ICorrespondenceApiClient.ListHistory`,
`CorrespondenceScreenController`'s History tab) but its own test plan left one box unchecked: a
live Editor/PlayMode check against a local Backend V2 instance seeded with Backend V2's
deterministic terminal-series E2E fixtures. This document records that check.

## Session record

| Field | Value |
| --- | --- |
| Level 5 (Unity) commit (base) | `314ba9b3f13cd1a217156caf794935970cbe8028` (`dev`) + this session's own commit (branch `backendv2-history-live-certification`) |
| Backend V2 commit | `8613c05359e4a2d7cc81a2c8779a4378121157e6` (`dev`, `Level5Backend` repo) - one commit ahead of the SHA this issue's brief originally audited (`1b905fd...`), but that commit (`#46`) is a revert of unrelated legacy-account-migration work (`#44`) with no effect on correspondence/History |
| Unity version | 6000.5.7f1 |
| Backend environment / base URI | `https://localhost:7029` - local `dotnet run --launch-profile https` against the local Postgres container (`level5-postgres-local`) |
| E2E database | `level5_v2_e2e` (not the persistent `level5_v2` dev database), reset and reseeded once per scenario via `./v2/scripts/e2e.ps1 e2e-seed <scenario> -Force` |
| Fixture account | `e2e_patrick`, tag `E2E_PATRICK#0001` (`Level5Backend/v2/tools/Level5.E2E.Fixtures/FixtureIdentities.cs`) |
| Execution path | `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter <method>`, one process per scenario, each preceded by its own `e2e-seed` reset |
| Date | 2026-09-27 |
| Tester / validator | Claude Code (automated agent), this session - no human operator, no GUI/device available; used the same opt-in PlayMode fixture pattern the rest of #159's live certification already established |
| Overall status | **All four scenarios PASSING (live).** |

## What was certified

Each scenario: clean session (`BackendV2SessionPersistenceStore.Clear()` /
`BackendV2SessionStore.Clear()`) → real scene load (`level_00_multiplayer`) → real Sign In as
`e2e_patrick` through the real login panel (username/password fields, Sign In button) → real
**History** button click → assert the rendered content area (not merely coordinator state) shows
exactly the seeded fixture series with its expected status, and that no
Accept/Decline/Cancel/Play action renders anywhere in History → real **Completed** button click →
assert inclusion/exclusion. A direct `BackendV2Runtime.Correspondence.ListHistory` call cross-checks
each fixture's real series id and status before touching the UI, so the identity assertion is never
a hardcoded GUID.

## Results

| Scenario | Expected History status | Rendered History status | Completed tab | Result |
| --- | --- | --- | --- | --- |
| `series-completed` | `Completed` | `Completed` | Included (correctly) | **PASSING (live)** |
| `challenge-declined` | `Declined` | `Declined` | Excluded, empty state rendered (correctly) | **PASSING (live)** |
| `challenge-cancelled` | `Cancelled` | `Cancelled` | Excluded, empty state rendered (correctly) | **PASSING (live)** |
| `challenge-expired` | `Expired` | `Expired` | Excluded, empty state rendered (correctly) | **PASSING (live)** |

Evidence log excerpts from the final re-verification pass (below) - seriesId truncated to the same
8-character prefix the rendered UI itself uses; full ids were cross-checked but are not secrets and
are included in full below since they are disposable, per-run fixture data with no bearing on
production:

```text
=== Completed fixture certification start === expectedInCompleted=True
Direct ListHistory PASSING: seriesId=01a0e3e4-2470-763b-a33d-4b5f2ad07b3b, status=Completed.
History tab PASSING (live): rendered row contains seriesId prefix '01a0e3e4' and status 'Completed':
    History (1) | series 01a0e3e4 - game 1/1 - Completed
Read-only PASSING: no Accept/Decline/Cancel/Play action is present under the rendered History tab.
Completed tab PASSING (live): rendered row contains seriesId prefix '01a0e3e4':
    Completed (1) | series 01a0e3e4 - game 1/1

=== Declined fixture certification start === expectedInCompleted=False
Direct ListHistory PASSING: seriesId=01a0e3e4-b7e5-799d-be91-4095ed47f0ec, status=Declined.
History tab PASSING (live): rendered row contains seriesId prefix '01a0e3e4' and status 'Declined':
    History (1) | series 01a0e3e4 - game 1/1 - Declined
Completed tab PASSING (live): the seeded fixture series correctly does not appear there, and the empty state renders.

=== Cancelled fixture certification start === expectedInCompleted=False
Direct ListHistory PASSING: seriesId=01a0e3e5-4b50-71f1-b5e2-0ed9644a455f, status=Cancelled.
History tab PASSING (live): rendered row contains seriesId prefix '01a0e3e5' and status 'Cancelled':
    History (1) | series 01a0e3e5 - game 1/1 - Cancelled
Completed tab PASSING (live): the seeded fixture series correctly does not appear there, and the empty state renders.

=== Expired fixture certification start === expectedInCompleted=False
Direct ListHistory PASSING: seriesId=01a0e3e5-dd76-7a00-8f7a-e36543b5a5c8, status=Expired.
History tab PASSING (live): rendered row contains seriesId prefix '01a0e3e5' and status 'Expired':
    History (1) | series 01a0e3e5 - game 1/1 - Expired
Completed tab PASSING (live): the seeded fixture series correctly does not appear there, and the empty state renders.
```

No production defect was found - every assertion passed on every live run of every scenario (both
the initial pass and the re-verification pass below), no Backend or Unity production code changed
beyond the two stale doc-comment lines in `SeriesListCoordinator.cs` (unrelated to certification
results themselves).

### Code-review follow-up, re-verified live

A senior-engineer review of this fixture (before merge) found one Low/Risk finding: `DoLogin()`
hides the login panel *before* its own `RefreshAll()` call even starts
(`CorrespondenceScreenController.cs`), so `CertifyTerminalFixture`'s wait for the login panel to
disappear could return while `RefreshAll()`'s own `history.Refresh()`/`completed.Refresh()` fetches
were still in flight - a later tab click would then start a second, concurrent `Refresh()` racing
that still-in-flight one on the same `SeriesListCoordinator.State`. Harmless with this fixture's
single static series (both fetches return identical data), but unnecessary risk under real network
variance. Fixed: the fixture now resolves the `history`/`completed` coordinators once and waits for
both to report `!IsLoading` right after login, before doing anything else. All four scenarios were
re-run live end-to-end after the fix (evidence above is from that re-verification pass) and all four
passing.

## Also validated this session (non-live)

- `Level5BackendV2CorrespondenceClientTests` (focused): 6/6 passed, including the new
  `ListHistoryUsesTheHistoryRouteParsesATerminalStatusAndTreatsCursorAsOpaque`.
- `Level5BackendV2SeriesListCoordinatorTests` (focused): 4/4 passed.
- `Level5BackendV2CorrespondenceScenePlayModeTests` (focused): 1/1 passed.
- Full EditMode suite: **1787/1787 passed**.
- Full PlayMode suite: **29/29 passed, 13 skipped** (every opt-in live-certification fixture,
  including this issue's own four new tests, correctly skips with `LEVEL5_LIVE_CERTIFICATION`
  unset).
- `./scripts/validate-repository.ps1` - passed.
- Clean Unity batchmode compile (`-batchmode -quit -nographics`, no `-runTests`) - clean.
- `dotnet build` on `Level5Backend/v2/Level5BackendV2.sln` - clean (0 errors).

## Reproducing this certification

```powershell
# 1. Backend V2 pointed at level5_v2_e2e, not the persistent level5_v2:
cd Level5Backend
$cs = ./v2/scripts/db.ps1 connection-string
$cs = $cs -replace "Database=level5_v2;", "Database=level5_v2_e2e;"
$env:ConnectionStrings__DefaultConnection = $cs
cd v2/src/Level5.Api
dotnet run --launch-profile https
# confirm: curl -k https://localhost:7029/health/live

# 2. In another terminal, for each scenario (series-completed, challenge-declined,
#    challenge-cancelled, challenge-expired):
cd Level5Backend
./v2/scripts/e2e.ps1 e2e-seed <scenario> -Force

# 3. From the level5 repo root, one Unity process per scenario:
$env:LEVEL5_LIVE_CERTIFICATION = "1"
& "<UnityPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode `
    -testFilter "BackendV2LiveCorrespondenceHistoryCertificationTests.<Method>" `
    -testResults <name>_results.xml -logFile <name>.log

# 4. Evidence accumulates at %TEMP%\level5_unity_history_live_cert_evidence.log across all four runs.
```

`<Method>` per scenario: `CompletedFixtureAppearsInCompletedAndHistory` (after seeding
`series-completed`), `DeclinedFixtureAppearsOnlyInHistory` (`challenge-declined`),
`CancelledFixtureAppearsOnlyInHistory` (`challenge-cancelled`),
`ExpiredFixtureAppearsOnlyInHistory` (`challenge-expired`).

No fixture passwords, tokens, or connection-string secrets are recorded above or needed to
reproduce this - the fixture-only credential is `FixtureIdentities.Password` in
`Level5Backend/v2/tools/Level5.E2E.Fixtures/FixtureIdentities.cs`.
