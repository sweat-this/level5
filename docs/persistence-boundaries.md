# Persistence and Account Identity

Written 2026-08-07 for AUD-011. Describes what actually exists today, not a target design. Where a
system is half-wired or dead, that is stated rather than smoothed over - the point of this document
is that "which store is the source of truth" was previously unanswerable without reading six files.

## The three stores

| Store | Location | Authority | Written by | Read by |
| --- | --- | --- | --- | --- |
| **SQLite** | `Application.persistentDataPath/level5.db` | **Authoritative for everything the game currently shows** | `DBHelper` (~30 methods, all lock-guarded via `DBConnector`) | `LoadManager`, `StartManager`, `ProgressionManager`, `StatsManager` |
| **JSON per-account files** | `Application.persistentDataPath/accounts/<accountId>-characters.json` | Never authoritative; fallback only - see below | `ProgressionService` → `CharacterProgressStore.TryApplyProgressionSnapshot` | `UnlockSnapshotBuilder`, `CharacterRuntimeProvider` (both as a *fallback only*) |
| **Server** | `Constants.API_ADDRESS_DEV_*` via `APIHelper` | Authoritative for leaderboards; the client cannot verify it | `APIHelper.PostHighscore`, `PostUnsubmittedHighscores` | `StatsManager` (online tab), `AccountManager` |

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

Three different things are easy to confuse. They are not interchangeable:

| Concept | Where | Means |
| --- | --- | --- |
| `GameOptions.userid` / `GameOptions.userName` | static, set by `LocalAccount` | **A local selection.** The user picked an account from the list, or fell back to offline guest. Proves nothing. |
| `APIHelper.HasSession` | `!string.IsNullOrEmpty(bearerToken)` | **A real session.** A token was obtained from the server. This is the only valid test for "may we call an authenticated endpoint". |
| `CharacterProgressAccountId.GetCurrent()` | derived | **A filesystem scope.** `userid` if > 0, else `userName`, else `"guest"`. Chooses which JSON file local progress goes in. |

AUD-045 fixed the case where `userid != 0` was being used as the authentication test. Two paths set
an identity without a session, and both are intentional:

1. `LocalAccount.LoginButton` writes `userName`/`userid` from the selected account and *then*
   navigates to the login screen. Backing out leaves both set.
2. `LocalAccount.LoginAsGuestCoroutine` calls `ClearSession()` on token failure and then re-sets the
   guest identity, deliberately leaving no token.

Both are correct: `AccountManager` prefills the username from them, and `CharacterProgressAccountId`
scopes offline progress by them. Clearing them would send offline progress to a nameless file. The
rule is simply that **nothing which talks to the server may read them as proof of a session**.

### Guest account

`UserAccountManager` hardcodes `guestUserid = 74`, `guestUsername = "guest"`, `guestPassword =
"guest"`. A shared credential in a shipped binary is readable by anyone, so that account must be
assumed writable by anyone. This cannot be fixed client-side; the server should treat it as
untrusted.

## Failure handling and retry

Every write path degrades to a queue rather than losing data:

| Path | On failure | Retried by |
| --- | --- | --- |
| Match score → SQLite | `PendingMatchPersistenceStore.QueueScore` | `LoadManager` calls `PendingMatchPersistenceStore.Repair()` on load |
| All-time stats → SQLite | `PendingMatchPersistenceStore.QueueAllTime` | same |
| Progression award | `PendingProgressionStore.Queue(accountId, resultId, ...)` | `ProgressionService` drains via `GetPending` / `Remove` |
| Score → server | row stays marked unsubmitted | next `PostUnsubmittedHighscores`, or the manual submit button in `StatsManager` |

`resultId` is what makes the progression queue idempotent - an award is removed by id once applied,
so a crash between "applied" and "removed" cannot double-grant.

### File writing

All JSON stores go through `AtomicFile` (defined at the bottom of `CharacterProgressStore.cs`):
write to a temp file, `File.Replace` onto the target, keep a `.bak`. Reads validate the JSON and
fall back to the backup when the primary is corrupt. This was reviewed in the first deep-audit pass
and found sound.

### Database locking

`DBConnector` exposes a `databaseLocked` flag; all seven acquire/release pairs are matched, and every
one of `DBHelper`'s ~30 lock-taking methods releases on both the success and the exception path.
Newer methods use `try/finally`; older ones release before each `return` and again in `catch`. Noted
because it looks unbalanced at a glance - several methods release via the lowercase backing field
while others use the `DatabaseLocked` property, so grepping one name finds only half the pairs.

## Client/server trust boundary

The client cannot enforce any of this; it is recorded so it can be confirmed against `Level5Backend`.

- **Score fields are client-authored.** `PostUnsubmittedHighscores` stamps `score.Userid` and
  `score.UserName` from `GameOptions` before sending, and the score values come from local
  `GameStats`. The server must derive identity from the bearer token and never trust the posted
  `Userid`. Client-side score integrity is not achievable and should not be attempted here.
- **Account enumeration is by design.** `UserNameExists`, `EmailExists`, and `GetUserByUserName` are
  unauthenticated, and `AccountManager.LoginUserCoroutine` fetches the full user record *by username*
  before it holds any credential. Given that API shape the client has no better option, but the
  server must be returning a minimal projection - no password hash, no email, no PII.

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

**Not yet covered:** `Assets/Scripts/versus/VersusLauncher.cs` calls
`MatchCatalogs.Builder.Build(request)` without an `UnlockSnapshot`, so versus/correspondence launches
do not get the new launch-time unlock re-check. This is not a regression - no unlock check existed
on that path before either - but closing it requires understanding whether "unlocked" even means the
same thing for a network-driven match (whose account's unlock state would apply?), which is
`docs/versus-architecture.md` territory and was out of scope for this slice.

## Open items

- `ProgressionManager` and `StartManager` read progression from SQLite; `ProgressionService` writes
  it to JSON. Nothing reconciles them. Today that is invisible because the JSON side is only a
  fallback, but the two will drift the moment either becomes authoritative.
- Confirm the two server-side expectations above against `Level5Backend`.
- `VersusLauncher`'s launch path does not yet revalidate level unlock state (see "Unlock authority" above).
- Durable level-progress/completion persistence remains unimplemented pending a product decision on
  what "completing a level" means (see "Unlock authority" above).
