# Backend V2 client (issue #158)

A typed Unity boundary to Backend V2 (`sweat-this/Level5Backend`, `v2/src/Level5.Api`). Unity talks
to Backend V2 only through the clients described here - never through legacy `APIHelper`, and never
by treating Backend V2 as remote storage for a local `VersusSeries`.

All code lives under `Assets/Scripts/backendv2/Level5BackendV2/` (assembly `Level5.BackendV2`).

## Why not `APIHelper`

`APIHelper` (`Assets/Scripts/RESTApi/APIHelper.cs`) is the legacy v1 client: a static class, one
global request lock, `JsonUtility`/`Newtonsoft.Json` mixed per call, a single bearer-token field, and
endpoints that predate Backend V2's contracts entirely (leaderboards, legacy accounts). Extending it
with `api/v2/*` methods would mean two API versions sharing one lock, one session field and one error
model that was never designed for Backend V2's RFC 7807 responses or its separate access/refresh
token pair. It stays exactly as it is; Backend V2 gets its own boundary instead.

## Why not `IVersusSeriesRepository`

`IVersusSeriesRepository`'s own doc comment already says this: it is the seam for **local** storage
only. A remote backend does not implement `Save(VersusSeries)`/`Load(SeriesId)` over the network -
that would make the Unity client authoritative for state the server must own (participants, frozen
rules, attempt identity, accepted results, game/series resolution). See
`docs/versus-architecture.md` §10 for the full correction. Remote correspondence instead goes
through the narrow command/query clients below.

## Architecture

```
Config          -> BackendV2ApiConfig, BackendV2ApiConfigProvider (environment + base URI)
Transport       -> IApiTransport, UnityWebRequestTransport, ApiRequest/ApiResponse<T>/ApiProblem,
                   ApiResponseMapper (pure status-code/ProblemDetails classification), BackendV2Json
Session         -> BackendV2Session, BackendV2SessionStore (in-memory), BackendV2SessionManager,
                   AuthenticatedApiClientBase (refresh-on-expiry, retry once)
Dtos            -> wire DTOs, mirrored field-for-field from the Backend V2 controllers
Clients         -> IAuthApiClient, IPlayersApiClient, IFriendsApiClient, ICorrespondenceApiClient,
                   IMatchResultsApiClient
Correspondence  -> RemoteAttemptContext, ActiveRemoteAttempt, RemoteAttemptDescriptorMapper,
                   RemoteAttemptResultBuilder, RemoteAttemptResultSubmitter
Results         -> PendingMatchResult, PendingMatchResultStore, MatchResultSubmissionCoordinator
BackendV2Runtime -> composition root handing out the shared transport/session/clients
```

`BackendV2MatchResultAdapter` and `BackendV2MatchResultSubmission` live outside `Level5.BackendV2.asmdef`
(next to `RemoteAttemptLauncher`, in `Assets/Scripts/backendv2/`) for the same reason: they touch
`HighScoreModel`, which has no assembly definition of its own, and a custom assembly definition
cannot reference the implicit default assembly. See "Ordinary match-result submission" below.

Every client method is an `IEnumerator` taking a callback (`Action<ApiResponse<T>>`), matching
`APIHelper`'s own coroutine convention rather than introducing async/await or a task library. Unlike
`APIHelper`, there is **no global request lock** - each call gets its own `UnityWebRequest` and calls
may run concurrently.

### Testability

`ApiResponseMapper` and every DTO's JSON shape are pure, Unity-free logic, exercised directly in
EditMode tests against hand-built `RawApiResponse`s and fixture JSON
(`Assets/Tests/Editor/BackendV2Fixtures.cs`). Client logic (auth headers, refresh
retry, `clientRequestId` handling) is tested against an in-memory `FakeApiTransport`, driven by
`CoroutineTestRunner.RunToCompletion` - a small helper that recursively drains nested `IEnumerator`s
the same way Unity's real coroutine engine does, so nested `yield return otherClient.Method(...)`
calls work correctly with no scene and no real network.

## Environment configuration

`BackendV2ApiConfig` (base URI, environment, timeout, insecure-localhost allowance) replaces the
legacy `Constants.API_ADDRESS_DEV_*` pattern for this boundary - that pattern is one hardcoded
absolute URL per endpoint with no dev/staging/prod concept, which does not scale to a typed client.

- `BackendV2ApiConfig.Development()` - defaults to `https://localhost:7029/`, this repo's own
  Backend V2 Kestrel HTTPS dev port (`v2/src/Level5.Api/Properties/launchSettings.json`), with
  insecure localhost allowed.
- `BackendV2ApiConfig.Custom(baseUri, environment, ...)` - for Staging/Production. No real
  staging/production URL exists yet, so build configuration supplies it explicitly rather than this
  class guessing one.
- The base URI must be absolute and `https`, unless it is an explicitly allowed insecure localhost
  endpoint.
- `BackendV2ApiConfigProvider.Current` / `.Override(...)` / `.Reset()` is the composition-root
  accessor, matching `VersusRuntime`/`MatchCatalogs`.

## Token / session handling

- `BackendV2SessionStore` holds the session **in memory only** - `AccessToken`, `ExpiresAt`,
  `PlayerId`, `RefreshToken`, `RefreshTokenExpiresAt`. It never touches `GameOptions` or
  `UserModel`, for the same reason `APIHelper.bearerToken` doesn't: a token that leaks onto a model
  that gets serialized, logged or displayed stops being a secret.
- Refresh-token persistence across app restarts is implemented (`BackendV2SessionPersistenceStore`,
  plaintext `AtomicFile` JSON - see "Still deferred / known limitations" below for the accepted
  security tradeoff that implies). Restoration is wired in at application startup
  (`UserAccountManager.Awake`, with `CorrespondenceScreenController.Awake` as an idempotent
  fallback - see `docs/backend-v2-correspondence-ui.md`'s "Refresh-token persistence" section) and
  performs zero network requests; the server only confirms a restored session once something makes
  an actual authorized request, through the normal refresh path below.
- `BackendV2SessionManager` drives register/login/logout, plus two refresh entry points that share
  one single-flight refresh internally (`RefreshNow`): `EnsureFreshAccessToken`, which no-ops unless
  the access token looks close to expiring by this client's own clock (used for the proactive
  pre-request check), and `ForceRefresh`, which refreshes unconditionally (used once the server has
  already rejected the token - authoritative information a local expiry estimate cannot override).
  Concurrent callers that arrive while a refresh is already in flight wait for that same refresh and
  share its outcome rather than issuing a second one against the same refresh token - without this,
  two authorized calls firing close together near expiry (a normal access pattern, e.g. a screen
  listing several things at once) could race two refreshes and have a losing one's failure clear a
  session a winning one had just set.
- `AuthenticatedApiClientBase` calls `EnsureFreshAccessToken` before every authorized request,
  fails fast with `ApiErrorKind.Unauthenticated` (no network call) if that leaves no session at all,
  and otherwise sends the request. On a `401` classified as `ApiErrorKind.Expired` it calls
  `ForceRefresh` and retries the same request exactly once before surfacing the failure.
- `api/v2/auth/logout` is called with **no** bearer token, matching the backend: it must work with
  an already-expired access token, since it only needs the refresh token in the body.

## Typed clients and endpoint coverage

| Client | Endpoints |
| --- | --- |
| `IAuthApiClient` | `register`, `login`, `refresh`, `logout` |
| `IPlayersApiClient` | `by-tag/{tag}`, `me` (get - raw GUID, patch) |
| `IFriendsApiClient` | list, remove, send/list-incoming/list-outgoing/accept/decline/cancel request |
| `ICorrespondenceApiClient` | create/accept/decline/cancel/get, list incoming/outgoing/active/completed, start/complete attempt |
| `IMatchResultsApiClient` | `Submit` (general, non-correspondence match results) |

List pagination (`SeriesSummaryPageDto.NextCursor`) is opaque: forwarded exactly as received, never
parsed or reconstructed, per Backend V2's own contract for that field.

`CreateChallenge` requires a non-empty, caller-generated `clientRequestId`
(`ClientRequestIdGenerator.NewId()`) and refuses to send the request at all without one - retrying a
create without reusing the same id is exactly the non-idempotent mistake that field exists to
prevent. A caller retrying the same logical "create this challenge" action after a network failure
should reuse the same `CreateChallengeDto` (and therefore the same id), not build a fresh one.

## Error mapping

`ApiResponseMapper` classifies every response into an `ApiErrorKind`
(`Validation`/`Unauthenticated`/`Expired`/`Forbidden`/`NotFound`/`Conflict`/`RateLimited`/
`ServerError`/`Network`/`Timeout`/`MalformedResponse`), preserving the server's `ApiProblem`
(`status`, `title`, `type`, `code`, `traceId`) where one was sent. A `401` is classified as `Expired`
only when the request itself required auth (worth a refresh+retry); on an unauthenticated call
(login, refresh) it is `Unauthenticated` (the credentials themselves were rejected). Nothing here
surfaces raw server exception text, credentials or tokens to the player - callers log `ErrorKind`
and `Problem.Code`, not the raw body. Every `ApiResponse<T>` also carries the `CorrelationId` the
request was sent with (the same value as its `X-Correlation-Id` header), independent of whether a
server response - and therefore a server-side `traceId` - ever came back, so a failure log line can
always be matched to a specific call.

## Remote attempt launch: descriptor -> match configuration

`RemoteAttemptDescriptorMapper.Map` turns Backend V2's `AttemptDescriptorDto` into an ordinary local
match, through the exact same `MatchRequest -> MatchConfigurationBuilder -> MatchConfiguration`
pipeline every other launch path uses (the same shape as `VersusLauncher.BuildMatch` - not a parallel
"remote gameplay" branch):

1. Reject an unsupported `CompetitionProtocolVersion`.
2. Resolve `RulesetId` against the local `CompetitiveRulesetCatalog` (`VersusCatalogs.Rulesets`) and
   reject an unknown ruleset.
3. Reject a `RulesetVersion` this build cannot play (`CompetitiveRuleset.CanPlayVersion`).
4. Reject any `RequiredResultMetrics` name this build does not recognize as an `AttemptMetric`.
5. Build the same one-human `PlayerRoster` + `MatchRequest(ruleset.ModeId, ...)` that
   `VersusLauncher.BuildMatch` builds, and validate it through `MatchCatalogs.Builder.Build`.

Every frozen value (mode, ruleset, comparison keys, required metrics) comes from the descriptor -
**never** from caller-supplied UI state. `levelId` and cosmetic choices (character, modifiers) are
the only caller-supplied inputs, because the descriptor itself carries no arena/level id (matching
`VersusLauncher.BuildMatch`'s own signature). Any failure above happens *before* a
`MatchConfiguration` is produced, so a scene load is never attempted for an unsupported attempt.

`ActiveRemoteAttempt` (the Backend V2 counterpart to `ActiveVersusAttempt`) then carries the
resulting `RemoteAttemptContext` for the duration of the match, tied to the exact
`MatchConfiguration` instance by reference so abandoning to the menu never submits an unrelated
later match as the remote turn.

`BackendV2ParticipantIdentity.Current()` is the explicit adapter between a Backend V2 player id (a
`Guid`) and the local opaque-string `ParticipantId` - deliberately a single, obvious conversion point
rather than scattered `PlayerId.ToString()` calls, and never mixed with the legacy integer account id.

## Result submission

`RemoteAttemptResultSubmitter.TrySubmit(stats, modeId, completionTimeSeconds)` is called once, from
`GameRules.HandleMatchEnded`, right alongside the existing `VersusMatchReporter.TryReport` call. It
no-ops for every match that is not an active remote attempt. When one is active, it:

1. Builds the local `AttemptResult` via `GameStatsAttemptResults.Build` (unchanged - the same
   gameplay-to-versus contract local versus already uses).
2. Converts it to named metrics via `RemoteAttemptResultBuilder`, using exactly the
   `RequiredResultMetrics` names the descriptor specified (`AttemptMetric` and Backend V2's
   `ResultMetric` share every member name by design, so this is a lookup, not a translation table).
3. Calls `ICorrespondenceApiClient.CompleteAttempt` with **only** that named-metric dictionary -
   never a winner, score, current game, revision, frozen rules or the opponent's result. Backend V2
   owns those decisions.
4. On success or a conflict (the domain already decided this attempt), clears
   `ActiveRemoteAttempt` and `PendingRemoteAttemptResult`. On a network/server/validation/auth
   failure, leaves both active and logs the outcome - the turn is still outstanding server-side.

Before the network call, the exact metrics dictionary built in step 2 is stashed in
`PendingRemoteAttemptResult` (alongside its `RemoteAttemptContext`) so a failure's retry has
something to resend - the coroutine-local dictionary would otherwise be lost the moment `Submit`
returns, and by the time a player is looking at a "resend result" prompt the `GameStats` it came from
may no longer exist. `RemoteAttemptResultSubmitter.TryRetryPending()` is the correspondence UI's
retry entry point: it resends exactly what is pending, through the same `TryClaim`/`Release` guard,
and never rebuilds a second, possibly different payload.

Submission is network I/O and cannot join `GameRules`' own synchronous match-end retry loop without
blocking it, so it runs on its own coroutine (`BackendV2CoroutineHost`, a small lazily-created
persistent `MonoBehaviour`) rather than gating `matchEndHandled`. Because `HandleMatchEnded` polls
roughly once a second until every step succeeds, `TrySubmit` guards against being called again for
the same attempt while a previous submission's network round trip is still outstanding
(`RemoteAttemptResultSubmitter.TryClaim`/`Release`) - without it, a slow connection combined with any
other step needing a retry would fire duplicate concurrent `CompleteAttempt` calls for one attempt.

## Ordinary match-result submission and durable retry

General (non-correspondence) match results - every score `GameRules.SaveMatchResults` and
`EndRoundMenuManager.saveGame` already make locally durable - are also submitted to Backend V2's
`POST api/v2/match-results`, durably and with retry across process restarts. This is a separate
system from correspondence result submission above: different endpoint, different idempotency
contract (`(PlayerId, ClientResultId)` with a 409 on a *different* payload replay, not "the domain
already decided this"), different queue, different retry classification.

```
HighScoreModel (already produced by GameRules/EndRoundMenuManager, local durability already done)
      |
      v
BackendV2MatchResultAdapter.TryAdapt   (default assembly - reuses Scoreid as ClientResultId)
      |
      v
BackendV2MatchResultSubmission.TryQueue   (default assembly - gates on a Backend V2 session existing
      |                                     right now; never creates a pending result otherwise)
      v
MatchResultSubmissionCoordinator.Enqueue   (Level5.BackendV2 - persists, then attempts delivery)
      |
      v
PendingMatchResultStore (backendv2_pending_match_results.json)  --Submit-->  IMatchResultsApiClient
```

### Ownership and durability ordering

A pending result is created only when `BackendV2SessionStore.Current` is non-null at the exact
moment the score becomes locally durable, and is owned by that player from then on -
`BackendV2MatchResultSubmission` never creates one retroactively if a player signs in later, and
`MatchResultSubmissionCoordinator` never sends, deletes or reassigns an entry owned by a different
player than the one currently signed in. Neither ever reads `GameOptions.userid`/`userName` - V1
identity and Backend V2 identity are never conflated.

Ordering is fixed and never reordered: the score is made locally durable (SQLite, or
`PendingMatchPersistenceStore` as a fallback) *before* `BackendV2MatchResultSubmission.TryQueue` is
even called - both call sites gate the call on that success - and the exact resulting
`SubmitMatchResultDto` is persisted to `PendingMatchResultStore` *before* the first delivery attempt.
Backend V2 delivery is never part of `GameRules`' synchronous match-end completion gate
(`matchEndHandled`); once durably queued, the match finishes normally regardless of network outcome.

### Field mapping

`BackendV2MatchResultAdapter` maps `HighScoreModel` directly (never `GameStats`, and never a second,
independent conversion): `Scoreid` is parsed as the `ClientResultId` GUID (a malformed `Scoreid` is
refused and logged, never replaced with a freshly-generated id); `Modeid`/`Levelid`/`Version`/
`Platform` map directly; `Characterid` (an `int`) becomes `CharacterId` via
`ToString(CultureInfo.InvariantCulture)` - an adapter decision for the current opaque-string wire
contract, not a migration of the game's numeric character identity. All six supported metrics are
always sent (`MaxShotMade` -> `ShotsMade`, `Time` -> `CompletionTimeSeconds`, `ConsecutiveShots` ->
`LongestStreak`, the rest name-for-name) - Backend V2's own mode-to-ranking-metric leaderboard policy
is never reproduced client-side. All four modifiers map from the model's `!= 0` int flags.

### Pending queue and retry

`PendingMatchResultStore` is `AtomicFile` + `BackendV2Json` (Newtonsoft, not `JsonUtility` - the
metrics `Dictionary<string,double>` needs it), its own file
(`backendv2_pending_match_results.json`), never `pending-match-persistence.json` (that file owns
local SQLite recovery, a different concern). The queue key is `(OwnerPlayerId, ClientResultId)`;
enqueuing the exact same request twice is a no-op, and a different payload under the same key is
refused and logged as a local integrity error rather than silently overwriting the queued entry.

`MatchResultSubmissionCoordinator` classifies every outcome (`Classify`, pure and directly
EditMode-testable against a hand-built `ApiResponse<T>` for every `ApiErrorKind`, the same seam
`ApiResponseMapper.Map` gets): success removes the entry; `Validation`/`Conflict` mark it as a
definitive failure (kept, logged with its `ClientResultId`, never retried automatically - a 409 here
means a *different* payload already exists under this key, not an idempotent replay); everything
else (`Network`/`Timeout`/`ServerError`/`RateLimited`/`Unauthenticated`/...) is left pending for the
next trigger. `Drain` sends every retryable entry for the current player sequentially, refuses a
second concurrent call while one is in flight, and re-checks the queue once more before finishing so
a result `Enqueue`d mid-drain (e.g. a second match ending) is never stranded until some unrelated
later trigger.

### Triggers

1. Every ordinary score `GameRules.SaveMatchResults` makes locally durable (excluding `FreePlay` and
   `BeatThaComputahs`, same as local/V1 persistence already excludes them) calls
   `BackendV2MatchResultSubmission.TryQueue` once.
2. The campaign aggregate `EndRoundMenuManager.saveGame` produces (mode 26) calls it once per
   aggregate, from the exact `HighScoreModel` `convertCampaignBasketBallStatsToModel` already built -
   never per campaign round.
3. `LoadManager.LoadAllDataCoroutine` calls `MatchResultSubmissionCoordinator.TriggerDrain()`
   unconditionally, independent of local SQLite readiness (unlike
   `PendingMatchPersistenceStore.Repair()`, gated on `databaseReady`) - the restart-recovery entry
   point, deliberately not placed in `BackendV2SessionPersistenceBootstrap`.

## Issue #159: the correspondence UI

`CorrespondenceScreenController` (`Assets/Scripts/menu_multiplayer/`, scene
`Assets/Scenes/level_00_multiplayer.unity`, reached from a `Multiplayer` footer button on
`level_00_start`) is the UI/E2E flow deferred above. It is built entirely from plain-C#
app-layer/coordinator types in `Level5.BackendV2` - `FriendsCoordinator`, `SeriesListCoordinator`,
`ChallengeCoordinator`, `ChallengeFormState`, `ActiveSeriesTurnClassifier`, `SeriesRowClassifier`,
`ListViewState<T>`, `RowCommandState` - each independently EditMode-testable against
`FakeApiTransport`, wired to a runtime-constructed uGUI screen. See
`docs/backend-v2-correspondence-ui.md` for the UI flow, retry behavior and known limitations, and
`docs/backend-v2-correspondence-certification.md` for end-to-end certification results.

`RemoteAttemptLauncher` (`Assets/Scripts/versus/`, next to `VersusLauncher` - it needs
`LegacyGameOptionsBridge`, which has no assembly definition and so is unreachable from
`Level5.BackendV2.asmdef`) is the `StartAttempt -> RemoteAttemptDescriptorMapper -> ActiveMatch /
ActiveRemoteAttempt / LegacyGameOptionsBridge / SceneTransition` sequence that was previously
missing - `RemoteAttemptDescriptorMapper` had no production caller before this.

**Still deferred / known limitations**, not silently dropped:

- Refresh-token persistence across app restarts is now implemented
  (`BackendV2SessionPersistenceStore`, plaintext `AtomicFile` JSON - the same storage convention
  every other local save in this project already uses, and the security tradeoff that implies).
- No automatic retry/backoff policy for list polling: a list refreshes on tab open and on an
  explicit action (refresh, load more, after a command), never on a timer. A player must reopen a
  tab (or retry a failed action) to see server-side changes made elsewhere.
- Revalidating unlock state for a network-driven match launch remains open - the existing gap noted
  in `docs/persistence-boundaries.md` for `VersusLauncher` applies identically to
  `RemoteAttemptLauncher`.
- No level/character picker for a remote attempt yet: `RemoteAttemptLauncher` launches with a fixed
  default level and no character customization. This mirrors local versus play, which also has no
  production launch UI yet (`VersusLauncher.Launch` previously had only `VersusDevConsole`, a dev
  tool, as a caller).
- The correspondence screen is built at runtime from code rather than authored as a scene/prefab
  (every other menu screen in this project is Editor-authored uGUI). This was the safe choice
  without interactive Editor/prefab-authoring access during this change - see the doc comment on
  `CorrespondenceScreenController` - and a production pass should move it to the normal
  scene/prefab convention.
- No gamepad-optimized input via `UiSelectionAdapter`/the Input System UI module - the screen uses a
  plain `StandaloneInputModule` `EventSystem`, so mouse/keyboard and legacy-Input-Manager-driven
  gamepad input work, but not the same first-class controller navigation other menu screens have.
