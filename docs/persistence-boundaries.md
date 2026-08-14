# Persistence and Account Identity

Written 2026-08-07 for AUD-011. Describes what actually exists today, not a target design. Where a
system is half-wired or dead, that is stated rather than smoothed over - the point of this document
is that "which store is the source of truth" was previously unanswerable without reading six files.

## The three stores

| Store | Location | Authority | Written by | Read by |
| --- | --- | --- | --- | --- |
| **SQLite** | `Application.persistentDataPath/level5.db` | **Authoritative for everything the game currently shows** | `DBHelper` (~30 methods, all lock-guarded via `DBConnector`) | `LoadManager`, `StartManager`, `ProgressionManager`, `StatsManager` |
| **JSON per-account files** | `Application.persistentDataPath/accounts/<accountId>-characters.json` | Never authoritative; fallback only - see below | `ProgressionService` → `CharacterProgressStore.TryApplyProgressionSnapshot` | `UnlockService`, `CharacterRuntimeProvider` (both as a *fallback only*) |
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

## Open items

- `ProgressionManager` and `StartManager` read progression from SQLite; `ProgressionService` writes
  it to JSON. Nothing reconciles them. Today that is invisible because the JSON side is only a
  fallback, but the two will drift the moment either becomes authoritative.
- Confirm the two server-side expectations above against `Level5Backend`.
