# Persistence and Account Identity

Written 2026-08-07 for AUD-011. Describes what actually exists today, not a target design. Where a
system is half-wired or dead, that is stated rather than smoothed over - the point of this document
is that "which store is the source of truth" was previously unanswerable without reading six files.

## The stores

| Store | Location | Authority | Written by | Read by |
| --- | --- | --- | --- | --- |
| **SQLite** | `Application.persistentDataPath/level5.db` | **Authoritative for local score/history and everything else the game currently shows** | `DBHelper` (~30 methods, all lock-guarded via `DBConnector`) | `LoadManager`, `StartManager`, `ProgressionManager`, `StatsManager` |
| **JSON per-account files** | `Application.persistentDataPath/accounts/<accountId>-characters.json` | Never authoritative; fallback only - see below | `ProgressionService` → `CharacterProgressStore.TryApplyProgressionSnapshot` | `UnlockSnapshotBuilder`, `CharacterRuntimeProvider` (both as a *fallback only*) |
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

### The SQLite / JSON split is the thing to know

These are two independent progression systems. SQLite is live and authoritative. The JSON store is a
fallback only, written by `ProgressionService` → `CharacterProgressStore.TryApplyProgressionSnapshot`
and read by `UnlockService`/`CharacterRuntimeProvider` only when a character is not found in the
SQLite-backed data first.

**Resolved 2026-08-13.** The never-called seeding path was deleted rather than wired, because making
JSON authoritative would have been a real progression-authority change nobody had requested, and
`CharacterProgressMigration`/`CharacterProgressStore.Load` had zero callers to begin with - deleting
them changes no runtime behavior. `CharacterProgressStore.TryLoadExisting`, `Save`, and
`TryApplyProgressionSnapshot` are unchanged and remain the only entry points into the JSON store.
SQLite stays the sole source of truth; the JSON store stays a plain fallback that is never seeded from
it. Reordering `UnlockService`/`CharacterRuntimeProvider` to check the JSON store first would still
return empty progress for existing players - that risk is unchanged by this fix and worth remembering
if either reader is touched again.

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
| `CharacterProgressAccountId.GetCurrent()` | derived from the row above | **A filesystem/SQLite scope.** `UserId` if > 0, else `UserName`, else `"guest"`. Chooses which local progress a save belongs to. |
| `BackendV2SessionStore.IsAuthenticated` | `Level5.BackendV2`, access/refresh token pair | **The sole online authenticated identity.** The only test for "may we call an authenticated `api/v2/*` endpoint". Entirely independent of the row above - never assign one from the other. |

`BackendV2SessionStore` is the only session concept left in the client; see `docs/backend-v2-client.md`.
Its token pair is mirrored to its own disk location (issue #159, `BackendV2SessionPersistenceStore`,
plaintext JSON via `AtomicFile`) so a player is not signed out of correspondence on every app restart;
restoration runs at application startup (`UserAccountManager.Awake`, idempotent, zero network
requests) independent of local profile selection: selecting a different local profile, creating one,
or continuing as guest must never clear an otherwise-valid restored Backend V2 session, and a Backend
V2 session must never be inferred from `LocalAccountIdentity`/`GameOptions.userid`. See
`docs/backend-v2-correspondence-ui.md`.

**Local ids are allocated on-device, not by a server.** `DBHelper.CreateLocalProfile` computes
`MAX(userid) + 1` inside a single SQLite transaction with the insert (so a rejected candidate never
reaches a committed row), skipping the reserved guest id (74) and rejecting integer exhaustion.
Existing V1-era local rows (whose `userid` came from the server at the time they were created) are
untouched and remain valid local identities exactly as before - this only changes where a *new* row's
id comes from. Pre-existing `password`/`bearerToken` columns on the `User` table are inert schema
compatibility (still scrubbed to `NULL` on every launch); no production reader or writer of either
remains, matching the precedent already set by the retired V1 score transport's `submittedToApi`
column.

### Guest account

`UserAccountManager` hardcodes `guestUserid = 74`, `guestUsername = "guest"`. A shared, well-known
local identity is not a security concern any more since it was never a credential to begin with -
"guest" is simply the local save scope nothing else claims. The retired guest password existed only
to satisfy the old V1 login call and is gone along with it.

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
  no callers, so this changed no runtime behavior). Character precedence is unchanged from
  `UnlockService`'s: the SQLite-backed `CharacterProfile` lists the menu already loaded are checked
  first; the JSON store (`CharacterProgressStore`) fills in only characters absent from those lists,
  and never overrides a known SQLite answer. See `Level5UnlockSnapshotTests.cs` for the regression
  coverage proving disagreement resolves toward SQLite in both directions.
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

- `ProgressionManager` and `StartManager` read progression from SQLite; `ProgressionService` writes
  it to JSON. Nothing reconciles them. Today that is invisible because the JSON side is only a
  fallback, but the two will drift the moment either becomes authoritative.
- Confirm the two server-side expectations above against `Level5Backend`.
- Durable level-progress/completion persistence remains unimplemented pending a product decision on
  what "completing a level" means (see "Unlock authority" above).
