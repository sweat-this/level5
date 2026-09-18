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
Clients         -> IAuthApiClient, IPlayersApiClient, IFriendsApiClient, ICorrespondenceApiClient
Correspondence  -> RemoteAttemptContext, ActiveRemoteAttempt, RemoteAttemptDescriptorMapper,
                   RemoteAttemptResultBuilder, RemoteAttemptResultSubmitter
BackendV2Runtime -> composition root handing out the shared transport/session/clients
```

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
- **Known deferred decision:** refresh-token persistence across app restarts is not implemented.
  Closing the app signs the player out of Backend V2. Persisting it is a real security/product
  decision (device binding, revocation, secure storage) that belongs to a dedicated follow-up, not a
  default picked here.
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
   `ActiveRemoteAttempt`. On a network/server/validation/auth failure, leaves it active and logs the
   outcome - the turn is still outstanding server-side, and only the UI adapter (#159) should decide
   whether and how to retry it.

Submission is network I/O and cannot join `GameRules`' own synchronous match-end retry loop without
blocking it, so it runs on its own coroutine (`BackendV2CoroutineHost`, a small lazily-created
persistent `MonoBehaviour`) rather than gating `matchEndHandled`. Because `HandleMatchEnded` polls
roughly once a second until every step succeeds, `TrySubmit` guards against being called again for
the same attempt while a previous submission's network round trip is still outstanding
(`RemoteAttemptResultSubmitter.TryClaim`/`Release`) - without it, a slow connection combined with any
other step needing a retry would fire duplicate concurrent `CompleteAttempt` calls for one attempt.

## Known deferred work (issue #159)

- The UI/E2E flow itself: challenge screens, friend list, incoming/outgoing/active/completed list
  rendering, and driving `RemoteAttemptResultSubmitter`'s failure outcomes into a retry affordance.
- A decision on refresh-token persistence across app restarts.
- A retry/backoff policy for list polling screens.
- Revalidating unlock state for a network-driven match launch - the existing open item noted in
  `docs/persistence-boundaries.md` for `VersusLauncher`, which also applies to a remote launch.
