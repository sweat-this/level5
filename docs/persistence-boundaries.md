# Persistence and Account Identity

Written 2026-08-07 for AUD-011. Describes what actually exists today, not a target design. Where a
system is half-wired or dead, that is stated rather than smoothed over - the point of this document
is that "which store is the source of truth" was previously unanswerable without reading six files.

## The stores

| Store | Location | Authority | Written by | Read by |
| --- | --- | --- | --- | --- |
| **SQLite** | `Application.persistentDataPath/level5.db` | **Sole live authority for character progression, and for local score/history and everything else the game currently shows** | `DBHelper` (~30 methods, all lock-guarded via `DBConnector`) | `LoadManager`, `StartManager`, `ProgressionManager`, `StatsManager` |
| **JSON per-account files** | `Application.persistentDataPath/accounts/<accountId>-characters.json` | **Retired, ignored compatibility artifact** - never read or written by any production path; see below | nothing in production | nothing in production - `CharacterProgressStore.DeleteAccountFiles` only *removes* this file family on profile deletion |
| **Backend V2 MatchResults** | `api/v2/match-results` via `MatchResultSubmissionCoordinator`/`BackendV2MatchResultsClient` | **Remote score authority.** The client cannot verify it | `BackendV2MatchResultSubmission.TryQueue` (called from `GameRules.SaveMatchResults` and `EndRoundMenuManager.saveGame`, once the score is already locally durable) | Server-side only today; the client does not read results back |
| **Backend V2 Leaderboards** | `api/v2/leaderboards` via `BackendV2Runtime.Leaderboards` | **Remote leaderboard authority.** The client cannot verify it | n/a (read-only from the client) | `StatsManager` (online tab) |

The legacy V1 score/leaderboard HTTP transport (`APIHelper.PostHighscore`, `PostUnsubmittedHighscores`,
`GetHighscoreByModeid`, and friends; `DBHelper.setGameScoreSubmitted`,
`getUnsubmittedHighScoreFromDatabase`) was retired once Backend V2 MatchResults/Leaderboards became the
production remote paths for ordinary and campaign results and for online leaderboard reads. There is no
production code left that calls it. The `submittedToApi` SQLite column is inert schema compatibility
only - no production reader or writer remains, and it is not removed to avoid an unnecessary migration
of existing databases.

**A match played with no Backend V2 session at the moment it becomes locally durable stays local-only
and is never retroactively claimed by a later sign-in.** `BackendV2MatchResultSubmission.TryQueue`
no-ops when `BackendV2SessionStore.Current` is null at that exact moment; there is no reconciliation
queue for historical local scores. This mirrors the same rule already documented above for Backend V2
correspondence sessions (`CharacterProgressAccountId` / session identity are never conflated either).

### SQLite is the sole live progression authority

**Resolved.** SQLite `CharacterProfile` is the only live character-progression store. Two SQLite-side
mechanisms sit alongside it, both still in production use:

- **`ProgressionResultLedger`** (SQLite table) - result-id idempotency. A repeated `resultId` is
  detected and reported as `MatchProgressionResult.Duplicate` rather than double-awarding experience;
  `DBConnector.ApplyProgressionResult`/`DBHelper.ApplyProgressionResult` own this in one transaction
  alongside the `CharacterProfile` update. Its `projectionApplied` column is inert schema
  compatibility only since this issue, matching the `submittedToApi`/`password`/`bearerToken`
  precedent above - new rows write `1` (there is no longer a pending secondary projection to track),
  and pre-existing `0` rows require no migration and are never read by anything.
- **`PendingProgressionStore`** (JSON, `<accountId>-pending-progression.json`) - durable retry for the
  authoritative SQLite write itself, when SQLite is unavailable or the write fails. Drained by
  `ProgressionService.RepairPendingProgression()` (called from `LoadManager` once the database is
  ready). This file is about retrying the *SQLite* write, not a second progression representation.

The separate JSON per-account file (`<accountId>-characters.json`, `CharacterProgressStore`) that used
to receive a best-effort "projection" of every successful SQLite write, and that
`UnlockSnapshotBuilder` used to fall back to for a character absent from the loaded SQLite profile
lists, is now dead code on both the write and read side. Neither `ProgressionService.ApplyMatchResult`
nor `UnlockSnapshotBuilder.Build` reference it any more - enforced by
`Level5ProgressionJsonDependencyGuardTests`. `CharacterProgressStore.TryLoadExisting`/`Save` remain
only as test fixtures and as the filename convention `DeleteAccountFiles` reuses when a local profile
is deleted; no production write or read path calls them for progression any more.
`CharacterRuntimeProvider`, the JSON store's other historical reader, was
deleted outright (zero production callers, zero scene/prefab GUID references) once its only reason to
exist - JSON progress lookup - was retired.

A pre-existing `<accountId>-characters.json`/`.bak`/`.tmp` file family from before this retirement is
left alone during normal gameplay - nothing deletes it on startup, and nothing reads it. It is only
ever removed as part of "Local profile deletion" below, alongside that profile's other account-scoped
files.

## Account identity

**The V1 account/auth transport (`APIHelper.PostUser`/`PostToken`/`UserNameExists`/`EmailExists`/
`GetUserByUserName`, and the bearer-session machinery that backed them - `bearerToken`, `HasSession`,
`BearerToken`, `ClearSession`) was retired.** Local profiles are created and selected entirely on
device now, with no server round trip at any point; selecting an existing local profile, creating a
new one, and continuing as guest are all zero-network operations. The old `APIHelper.HasSession`
model ("a session token proves the authenticated identity") no longer applies to local profiles at
all - there is no local session concept any more, only a local selection. What's left is a clean
two-boundary model:

| Concept | Where | Means |
| --- | --- | --- |
| `LocalAccountIdentity.UserId` / `.UserName` (`GameOptions.userid`/`userName` forward here) | static, set by `LocalAccount`/`UserAccountManager`/`AccountManager` | **Local save/profile scope.** Which local profile is selected, or the offline guest fallback. Not a credential of any kind - a local profile is a save selector, not a security principal. |
| `CharacterProgressAccountId.GetCurrent()` / `.Resolve(userId, userName)` | `GetCurrent()` reads the row above and delegates to the pure `Resolve` | **A filesystem/SQLite scope.** `Resolve` is the one mapping rule, usable for any candidate profile (not only the currently selected one, e.g. a profile being deleted) without mutating `LocalAccountIdentity`: `userId` if > 0 (as an invariant decimal string), else non-empty `userName`, else `"guest"`. Chooses which local progress a save belongs to. |
| `BackendV2SessionStore.IsAuthenticated` | `Level5.BackendV2`, access/refresh token pair | **The sole online authenticated identity.** The only test for "may we call an authenticated `api/v2/*` endpoint". Entirely independent of the row above - never assign one from the other. |

`BackendV2SessionStore` is the only session concept left in the client; see `docs/backend-v2-client.md`.
Its token pair is mirrored to its own disk location (issue #159, `BackendV2SessionPersistenceStore`,
plaintext JSON via `AtomicFile`) so a player is not signed out of correspondence on every app restart;
restoration runs at application startup (`UserAccountManager.Awake`, idempotent, zero network
requests) independent of local profile selection: selecting a different local profile, creating one,
or continuing as guest must never clear an otherwise-valid restored Backend V2 session, and a Backend
V2 session must never be inferred from `LocalAccountIdentity`/`GameOptions.userid`. See
`docs/backend-v2-correspondence-ui.md`.

**Local ids are allocated on-device, not by a server, and are durable identities that are never
reused.** `DBHelper.CreateLocalProfile` allocates from a one-row `LocalProfileIdSequence` SQLite table
(`DBHelper.AllocateNextUserId`) inside the same transaction as the `User` insert, so a rejected
candidate (duplicate name, integer exhaustion, ...) never advances the sequence and never reaches a
committed row. The sequence tracks the highest id ever committed, independent of which `User` rows
currently exist - `DBHelper.DeleteLocalProfile` never touches it - so deleting a local profile can
never make its id available again. That matters because account-scoped files
(`CharacterProgressStore`/`PendingProgressionStore`/`ProgressionResultStore`) use this numeric id in
their filename; reuse would let a newly-created profile silently inherit a deleted one's stale files.
On first use against a database that predates this table, the high-water mark is bootstrapped from the
largest known numeric local-profile scope across `User.userid`, `CharacterProfile.accountId`, and
`ProgressionResultLedger.accountId` (when that lazily-created table exists) - so a profile deleted
before the sequence table ever existed, whose progression rows can still be present, cannot have its
identity recycled either. Allocation still skips the reserved guest id (74) and rejects integer
exhaustion, and existing V1-era local rows (whose `userid` came from the server at the time they were
created) are untouched and remain valid local identities exactly as before. Pre-existing
`password`/`bearerToken` columns on the `User` table are inert schema compatibility (still scrubbed to
`NULL` on every launch); no production reader or writer of either remains, matching the precedent
already set by the retired V1 score transport's `submittedToApi` column.

### Local profile deletion

`DBHelper.DeleteLocalProfile(UserModel profile, out string error)` is the single ownership boundary
for deleting a local profile - it owns `DatabaseLocked` for the whole operation (a caller must never
pre-acquire it; that was the root cause of a prior defect where `UserAccountManager` set
`DatabaseLocked = true` before calling the old `deleteLocalUser`, which then no-op'd immediately on
its own `if (databaseLocked) return;` guard, so nothing was ever deleted and the lock was never
released) and runs one SQLite transaction that derives `accountId` via
`CharacterProgressAccountId.Resolve(profile.Userid, profile.UserName)` and deletes, in order,
`CharacterProfile`, `ProgressionResultLedger` (only when that lazily-created table exists), and
finally the target `User` row itself - requiring exactly one `User` row to be affected or the whole
transaction rolls back. Guest (`UserId = 74`, `UserName = "guest"`) is a fallback scope, not a
persistent `User` row, and `DeleteLocalProfile` rejects it deterministically rather than deleting
anything.

SQLite is authoritative for deletion: once that transaction commits, the profile is gone regardless of
what happens next. `UserAccountManager` separately cleans up the deleted account's local projection/
recovery files afterward (`CharacterProgressStore`/`PendingProgressionStore`/`ProgressionResultStore`
each expose a narrowly-owned `DeleteAccountFiles`, removing their primary/`.bak`/`.tmp` family via
`AtomicFile.TryDeleteFamily`); a partial filesystem cleanup failure only logs a warning; it never
re-creates the deleted `User` row and never blocks the caller from proceeding. The durable non-reused-
id invariant above is what makes this safe - a stale leftover file from a failed cleanup can never
later attach to a newly-created profile, because that profile will never receive the deleted id again.

Local profile deletion removes only the profile-owned state above. `HighScores` and `AllTimeStats` are
explicitly **not** currently scoped to profile deletion - they are preserved unchanged, matching the
existing repository state (`AllTimeStats` is in fact a single global row, not per-account, so there is
nothing to scope). `MatchPersistenceLedger` and all Backend V2 data (`BackendV2SessionStore`,
correspondence, MatchResults, leaderboards) are likewise untouched - local profile deletion is local-
only and never affects the online session/identity boundary described above.

### Guest account

`UserAccountManager` hardcodes `guestUserid = 74`, `guestUsername = "guest"`. A shared, well-known
local identity is not a security concern any more since it was never a credential to begin with -
"guest" is simply the local save scope nothing else claims. The retired guest password existed only
to satisfy the old V1 login call and is gone along with it. Guest is a fallback scope only - it is
never a persistent `User` row, so it can never be deleted through local profile deletion (see "Local
profile deletion" above); a UI affordance to remove the generated guest row is rejected
deterministically instead of clearing any persisted guest progression. That rejection is keyed on the
reserved scope itself (`Userid == 74`), not on the display name "guest" - a real, differently-scoped
profile (`Userid != 74`) that happens to also be named "guest" is an ordinary deletable profile, not
the reserved fallback.

## Failure handling and retry

Every write path degrades to a queue rather than losing data:

| Path | On failure | Retried by |
| --- | --- | --- |
| Match score → SQLite | `PendingMatchPersistenceStore.QueueScore` | `LoadManager` calls `PendingMatchPersistenceStore.Repair()` on load |
| All-time stats → SQLite | `PendingMatchPersistenceStore.QueueAllTime` | same |
| Progression award | `PendingProgressionStore.Queue(accountId, resultId, ...)` | `ProgressionService` drains via `GetPending` / `Remove` |
| Score → Backend V2 MatchResults | `PendingMatchResultStore.Enqueue` | `MatchResultSubmissionCoordinator`'s durable retry (see `docs/backend-v2-client.md`) |

`resultId` is what makes the progression queue idempotent - an award is removed by id once applied,
so a crash between "applied" and "removed" cannot double-grant.

### File writing

All JSON stores go through `AtomicFile` (`Assets/Scripts/Utility/Level5Utility/AtomicFile.cs`, moved
out of `CharacterProgressStore.cs` and into the `Level5.Utility` assembly by AUD-012 Phase 2b Slice
14 — same global type, same behavior, `CharacterProgressStore` itself unchanged): write to a temp
file, `File.Replace` onto the target, keep a `.bak`. Reads validate the JSON and fall back to the
backup when the primary is corrupt. This was reviewed in the first deep-audit pass and found sound.

### Database locking

`DBConnector` exposes a `databaseLocked` flag; all seven acquire/release pairs are matched, and every
one of `DBHelper`'s ~30 lock-taking methods releases on both the success and the exception path.
Newer methods use `try/finally`; older ones release before each `return` and again in `catch`. Noted
because it looks unbalanced at a glance - several methods release via the lowercase backing field
while others use the `DatabaseLocked` property, so grepping one name finds only half the pairs.

## Client/server trust boundary

The client cannot enforce any of this; it is recorded so it can be confirmed against `Level5Backend`.

- **Score fields are client-authored.** The score values submitted to Backend V2 MatchResults come
  from local `GameStats`/`HighScoreModel`. Unlike the retired V1 transport (which stamped
  `score.Userid`/`score.UserName` from `GameOptions` onto the score itself),
  `MatchResultSubmissionCoordinator.Enqueue` derives ownership from `BackendV2SessionStore.Current`'s
  `PlayerId`, never from a client-set field on the payload - but the metric values themselves are still
  client-authored. The server must derive identity from the access token and never trust a posted
  identity field. Client-side score integrity is not achievable and should not be attempted here.
- **Local profile enumeration no longer exists.** `UserNameExists`/`EmailExists`/`GetUserByUserName`
  and the account-by-username lookup they backed were retired with the rest of the V1 transport - a
  duplicate local profile name is now rejected deterministically by `DBHelper.CreateLocalProfile`
  itself, inside SQLite, with no server round trip at all.

## Unlock authority (issue #39)

**CHARACTER PROGRESSION AUTHORITY = SQLite.** Before this slice, unlock state was not actually
centralized despite `UnlockService` existing: `PlayerSelectCatalogAdapter` computed
`IsUnlocked = !profile.IsLocked` directly off the live SQLite-backed `CharacterProfile` list, while
`UnlockService` (with the correct SQLite-first/JSON-fallback precedence) had zero production
callers - it was dead code. Two independently-correct-looking answers to "is this unlocked" existed
in the codebase at once, only one of which anything actually called.

That is now consolidated into one query, built once per menu refresh rather than recomputed (with a
filesystem read) on every call:

- **`Level5.Core.Progression.UnlockSnapshot`** - a plain, immutable projection: `IsCharacterUnlocked(int)`
  / `IsLevelUnlocked(int)`. No `UnityEngine`, database, singleton, or filesystem dependency. An id it
  was not built with answers locked (a deterministic safe default, not "unknown").
- **`UnlockSnapshotBuilder`** (`Assets/Scripts/menu_start/UnlockSnapshotBuilder.cs`) - the adapter
  that builds a snapshot from live account data. Replaces `UnlockService`, which is deleted (it had
  no callers, so this changed no runtime behavior). Character answers come solely from the
  SQLite-backed `CharacterProfile` lists the menu already loaded (primary roster first, CPU roster
  only filling in what the primary roster did not answer); a character absent from both defaults
  locked. The JSON store is no longer consulted at all - see "SQLite is the sole live progression
  authority" above. See `Level5UnlockSnapshotTests.cs` for the regression coverage, including
  `AStaleLegacyJsonEntryCannotUnlockACharacterAbsentFromSqlite` proving a leftover legacy JSON file
  cannot resurrect an unlock SQLite does not know about.
  **Caught in code review before this reached `dev`:** the primary and CPU profile lists must not be
  merged as equals. `LoadManager.loadCpuSelectDataList` never sets `CharacterProfile.IsLocked` from
  SQLite the way `loadPlayerSelectDataList` does for the primary roster, so a CPU-list profile's lock
  flag is always `false` regardless of account progress - and the same character id commonly appears
  in both rosters. An id the primary roster already answered is never overwritten by the CPU pass;
  see `APrimaryLockedCharacterStaysLockedEvenWhenTheSameIdIsAlsoACpuOption` in
  `Level5UnlockSnapshotTests.cs`.
- **`Level5.Core.Match.LevelEligibility`** - composes `LevelDefinition.Selectable` (authored
  content), `GameModeCompatibility.CanPlay` (mode/arena fit) and `UnlockSnapshot.IsLevelUnlocked`
  (account state) into the one "can this level be chosen right now" answer, used by both menu
  cycling (`StartMenuSelectionState.CycleLevel`/`CycleMode`) and launch validation
  (`MatchConfigurationBuilder.Build`) - so a stale menu index or a future UI bug cannot start locked
  content, the same way character selection was already protected via
  `PlayerSelectionController.ValidateLaunch`.

**Level unlock has no durable per-account state yet, deliberately.** `LevelDefinition.Locked` is
authored, static data - nothing in the current codebase ever unlocks a level at runtime, and no
"level completed" concept exists (campaign mode advances an in-memory `levelSelectedIndex` per run
via `EndRoundMenuManager`/`CampaignRoundDecision`, never a durable per-account record). Introducing a
`LevelProgressSave` without established completion semantics would mean inventing gameplay rules
rather than migrating existing ones, so this slice stops at the query seam:
`UnlockSnapshot.IsLevelUnlocked` currently answers `!LevelDefinition.Locked` for every level, which
is exactly the previous (unenforced) authored intent, now actually enforced at selection and launch.
Durable level progress remains a follow-up, blocked on a product decision about what "completing a
level" means.

**Remote correspondence is covered (issue #198).** `RemoteAttemptLauncher.Run` requires the caller's
current `UnlockSnapshot` (the same one `RemoteCharacterSelectionResolver.ResolveCurrentPrimary(out
UnlockSnapshot)` already builds to validate the character) and checks the local `levelId` against it
twice: once as a preflight via `LevelEligibility.ValidateForLaunch`, before `StartAttempt` (so an
unknown/non-selectable/locked level makes zero Backend requests), and again inside
`RemoteAttemptDescriptorMapper.Map`, which now requires that same snapshot and passes it to
`MatchCatalogs.Builder.Build(request, unlock)` instead of the permissive `Build(request)` overload -
the exact ordinary launch-time gate this section describes above. "Whose account's unlock state
applies" is answered the same way character validation already answers it: the local account signed
into this device, never Backend V2's `PlayerId` - `levelId` is not part of the competition protocol
and this issue does not add it there.

**Local versus is covered too (issue #203).** `VersusLauncher.Launch`/`BuildMatch` now require the
caller's current `UnlockSnapshot`, with the same two-stage shape as remote correspondence: a
preflight via `LevelEligibility.ValidateForLaunch` against `MatchCatalogs.Levels.Find(levelId)`,
*before* `VersusMatchCoordinator.IssueAttempt` (so an unknown/non-selectable/locked level never
consumes a competitive attempt), and a final revalidation inside `BuildMatch`, which passes that same
snapshot to `MatchCatalogs.Builder.Build(request, unlock)` rather than the permissive `Build(request)`
overload. A null snapshot fails closed at both call sites instead of falling back to that permissive
behavior. `VersusDevConsole` (`Assets/Scripts/Dev/VersusDevConsole.cs`), still the only caller and
still development-only, builds its snapshot through the same `UnlockSnapshotBuilder` every other
launch path uses - there is no second unlock authority.

Mode/arena compatibility is deliberately *not* part of this preflight: an otherwise
selectable/unlocked level that the series' frozen ruleset cannot use is still only caught afterward,
inside `BuildMatch`'s builder revalidation - by then the attempt has already been issued and
persisted, and it is left outstanding so the same turn can be retried on a compatible arena. Local
level eligibility and mode/arena compatibility are different failures on purpose: the first is known
before anything is spent on the attempt, the second only after the frozen ruleset is known.

## Open items

- Confirm the two server-side expectations above against `Level5Backend`.
- Durable level-progress/completion persistence remains unimplemented pending a product decision on
  what "completing a level" means (see "Unlock authority" above).
