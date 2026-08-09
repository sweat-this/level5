# Versus and Correspondence Multiplayer - Implementation Plan

Status: plan reviewed twice, implemented
Project: Level 5
Branch: `feat/match-configuration-overhaul`
Written: 2026-08-08

This is the planning record required before the implementation. It states what the repository
actually contained, what was wrong, what was built, and the two review passes the plan went through
before any code was written. The architecture as built is documented separately in
[`versus-architecture.md`](versus-architecture.md); this file is the decision record.

---

## 1. Current architecture (audit)

The repository is mid-way through a separate match-configuration overhaul
([`game-options-match-system-overhaul-plan.md`](game-options-match-system-overhaul-plan.md), phases
0-9 done). That overhaul had already built most of the foundation this work needs, so the audit
mattered more than usual: roughly half of the concepts named in the brief already existed under
other names.

### 1.1 What already exists and is sound

`Assets/Level5/Core` is a plain-C# assembly (`Level5.Core`) with no scene dependencies. Its
`Match` folder holds:

| Type | Responsibility |
| --- | --- |
| `GameModeId` | typed mode identity over the stored numeric ids (contract with save data) |
| `GameModeDefinition` | authored ScriptableObject: objective, clock, shot rule, combat mode, requirements, arena capabilities |
| `LevelDefinition`, `ArenaCapability` | authored arena data and capability flags |
| `GameModeCatalog`, `LevelDefinitionCatalog` | id lookup, duplicate-id rejection |
| `GameModeCompatibility` | one validator for mode/level/roster/character combinations |
| `MatchRequest` -> `MatchConfigurationBuilder` -> `MatchConfiguration` | request, validation, immutable resolved match |
| `ResolvedMatchRules` | the authored rules after arena/roster/modifiers have had their say |
| `PlayerRoster`, `PlayerSlot`, `PlayerControlType` | participant slots; already carries `ParticipantId` and reserves `RemoteHuman` |
| `MatchLifecycle`, `MatchPhase` | Unity-free phase machine with a once-only end transition |
| `MatchEndConditions` | "is it over" rules as plain functions |
| `ValidationResult` / `MatchValidationCode` | coded, accumulating rejection reasons |

Scene-facing: `ActiveMatch` (write-once configuration that survives a scene load), `MatchRuntime`
(read boundary with a legacy-globals fallback for directly entered scenes), `LevelRuntimeContext`
(scene composition root), `MatchController` (MonoBehaviour wrapper over `MatchLifecycle`),
`MatchHudPresenter`, `GameRules`.

Tests live in `Assets/Tests/Editor` (compiled into `Assembly-CSharp-Editor`, which auto-references
`Level5.Core`), with `TestDefinitions` builders and `Level5MatchArchitectureTests` acting as a
ratchet: allowlists of files still permitted to touch `GameOptions` or declare mode-identity
booleans, which may only get shorter.

Persistence conventions: SQLite through `DBConnector`, JSON under `Application.persistentDataPath`
through `PendingMatchPersistenceStore`, per-account progression through `ProgressionService`.
Documented in [`persistence-boundaries.md`](persistence-boundaries.md).

### 1.2 Searches for the problematic patterns named in the brief

| Pattern | Found? |
| --- | --- |
| `isVersus`, `isTwoPlayer`, `isMultiplayer`, `isOnline`, `isHost` | none |
| `player1Score` / `player2Score` | none. Score lives per player on a `GameStats` component |
| boolean-flag mode identity | yes, in `GameOptions` - already being retired by the overhaul, already ratcheted by an architecture test |
| networking in gameplay | none. There is no networking in the project at all |
| `switch` on game mode | a handful of `gameModeId ==` comparisons in `GameRules`, all being migrated |

So the boolean-explosion work the brief asks for is largely already done, and redoing it would be a
second migration competing with the first. This plan builds on the existing typed model instead.

### 1.3 What was actually missing

1. **No competitive concept above one match.** There is no attempt, no game result, no series, no
   rivalry. `GameModeId.VersusCpu` is a mode where CPUs also shoot; the "winner" is whatever
   `GameLevelManager.getSortedGameStatsList()` returns, which sorts every mode by `TotalPoints`.
2. **One global tie-break rule.** That sort is mode-agnostic. For a completion-time contest or a
   consecutive-shots mode it ranks by the wrong number and has no tie behaviour at all.
3. **No competitive ruleset identity or version.** `GameModeId` identifies gameplay, not the
   competitive rules, and there is nothing separating a rules version from the build number.
4. **The match result is a `MonoBehaviour`.** `GameStats` holds every metric as public mutable
   fields on a scene component. Nothing about it can survive a correspondence delay.
5. **Competitive state owned by UI-adjacent statics.** `EndRoundData` holds
   `currentRoundWinnerIsCpu`, `currentRoundWinnerScore`, `nextLevelIndex` - the campaign's series
   progression, owned by the end-round screen's data bag and mutated from `GameRules`.
6. **No persistence for an unfinished competition.** Every store is keyed to a finished match.

### 1.4 Problems found but deliberately left alone

- `StartManager` is 1,922 lines. Out of scope; already named in the overhaul plan.
- `GameRules` still reads `GameOptions` for campaign navigation. On the overhaul's allowlist.
- `EndRoundData`'s campaign progression is conceptually a two-participant series and could later be
  expressed as one. Converting it now would rewrite the campaign flow, which the brief forbids
  (section 4). Recorded in "remaining risks" instead.

---

## 2. Target architecture

Six layers, each depending only downward.

```
GAMEPLAY (Unity)            GameRules / GameStats / scenes
        |  produces metrics
        v
ATTEMPT                     Attempt, AttemptResult, AttemptState
        |  compared by
        v
RULESET                     CompetitiveRuleset, comparison keys, VersusCapability
        |
        v
VERSUS GAME                 VersusGame, GameResult, InformationPolicy
        |
        v
SERIES                      VersusSeries, SeriesFormat, SeriesSnapshot, SeriesScore
        |
        v
SOCIAL                      RivalryRecord (derived, never authoritative)

                            all of the above sit above
                            IVersusSeriesRepository  <- the only persistence seam
                                |                    |
                        file-backed local        future remote
```

`Level5.Core` holds every domain type and the coordinator. `Assets/Scripts/versus` holds the Unity
adapters: catalog sourcing, the file repository, the launcher, and the result reporter. Nothing in
`Level5.Core.Versus` references a scene object, a `MonoBehaviour`, or the file system.

### 2.1 Key decisions

**The ruleset is separate authored data, not a field on `GameModeDefinition`.** A
`CompetitiveRulesetDefinition` ScriptableObject names a stable `RulesetId`, a `RulesetVersion`, the
`GameModeId` that plays it, its `VersusCapability` flags, and its ordered comparison keys. A mode
with no ruleset is simply not versus-capable - which is the safe default the brief asks for in
section 9. This keeps versus out of the gameplay mode definition entirely, and adding a mode means
adding an asset, never editing a coordinator.

**Comparison is data, not code.** `CompetitiveRuleset` holds an ordered `ComparisonKey[]`
(`AttemptMetric` + direction). The first key is the primary; the rest are the mode's own tie-breaks;
equal on all of them is a draw. This is what makes "tie behaviour comes from the ruleset" true
without a per-mode `switch` or a reflection dispatch.

**`Attempt` is the attempt ticket.** The brief's `AttemptTicket` (id, participant, game, ruleset
version, issuedAt, status) is exactly the field set of `Attempt`. Creating a second type that
mirrors it would be the empty wrapper section 7 forbids. `Attempt` is issued by the series, carries
its own lifecycle, and rejects a second completion - which is the retry-exploit boundary the ticket
concept exists for.

**Sealed attempts are enforced by the domain's shape, not by UI discipline.** `VersusGame` does not
expose its attempts. It exposes `ViewFor(ParticipantId)`, which returns what that participant is
allowed to see. The opponent's result is physically absent from the returned object until the game
resolves. An architecture test keeps UI code away from the serialization DTOs that would bypass it.

**The series snapshot is the authority for an in-flight series.** `SeriesSnapshot` freezes the
format, the ordered ruleset entries (id, version, comparison keys, capabilities) and the information
policy when the series begins. Resolution reads the snapshot, never the live catalog, so editing a
ruleset cannot change a running series.

**Ruleset version is independent of build version, with a declared compatibility floor.**
`CompetitiveRuleset.MinimumCompatibleVersion` is the one extra field that makes a later migration
engine possible without one existing now: the coordinator refuses to issue an attempt when the
current build's ruleset can no longer play the version the series was snapshotted at.

### 2.2 Data model

```
RulesetId            stable kebab string, e.g. "three-point-contest"
CompetitiveRuleset   Id, Version, MinimumCompatibleVersion, ModeId, Capabilities, ComparisonKey[]
ComparisonKey        AttemptMetric + MetricDirection (HigherWins | LowerWins)
AttemptMetric        Score, ShotsMade, ShotsAttempted, Accuracy, CompletionTimeSeconds,
                     LongestStreak, TotalDistance, BonusPoints   (fixed, mode-agnostic set)

ParticipantId        stable opaque string
MatchParticipant     Id, DisplayName, ParticipantKind (LocalHuman | RemoteHuman | Cpu)
VersusParticipants   exactly two, ordered

AttemptId            stable opaque string
AttemptState         Created -> Ready -> Started -> Completed | Abandoned
Attempt              AttemptId, ParticipantId, GameIndex, RulesetId, RulesetVersion,
                     State, IssuedAt, StartedAt, CompletedAt, Result
AttemptResult        RulesetId, RulesetVersion, metric values (fixed-length array)

VersusGame           Index, RulesetSnapshot, InformationPolicy, participants,
                     attempts (private), Status, Result
VersusGameStatus     Pending -> Active -> Resolved | Forfeited | Cancelled
GameResult           Kind (Decided | Draw | Forfeit | Cancelled), WinnerId, ResolvedAt

SeriesFormat         GameCount (1|3|5|7) + RequiredWins
SeriesPlaylist       ordered PlaylistEntry (RulesetId, RulesetVersion)
SeriesSnapshot       FormatVersion, SeriesFormat, RulesetSnapshot[], InformationPolicy,
                     AlternatesFirstAttempt
VersusSeries         SeriesId, Snapshot, participants, games, Status, Score, Result
SeriesStatus         Invited -> Active -> Completed | Forfeited ; Invited -> Declined
SeriesScore          wins per participant + draws
SeriesResult         Kind, WinnerId, final score, CompletedAt

RivalryRecord        derived by folding completed SeriesResults. Never stored as truth.
```

### 2.3 State transitions

```
Attempt
  Created --Ready--> Ready --Start--> Started --Complete--> Completed
                                          \--Abandon--> Abandoned
  Illegal: complete twice, complete an abandoned attempt, submit a result whose
           ruleset id or version does not match the attempt. All throw.

VersusGame
  Pending --activate--> Active --both attempts complete--> Resolved
                             \--forfeit--> Forfeited
  Resolution runs exactly once. Resolving a resolved game throws.

VersusSeries
  Invited --Accept--> Active ; Invited --Decline--> Declined
  Active --game resolved--> Active (advance) | Completed
  Active --Forfeit--> Forfeited
  Completed is terminal: issuing an attempt, submitting a result or forfeiting all throw.
```

Series completion: the moment a participant reaches `RequiredWins`, or when the last playlist game
resolves. Remaining games are never activated and no attempt is ever issued for them, so 4-0 in a
best-of-seven stops at four games.

### 2.4 Information policies

| Policy | Before both attempts complete | On the responder's turn |
| --- | --- | --- |
| `SealedAttempt` | opponent's state only (`Pending` / `InProgress` / `Complete`). No result, no metric, no derived value | nothing extra |
| `OpenTarget` | same states | the primary comparison metric of the first attempt, and nothing else |

Under `OpenTarget` the domain also refuses to issue a second attempt before the first has completed;
otherwise the format's premise does not hold. Under `SealedAttempt` both participants may hold live
attempts at once, which is what makes local simultaneous play the same code path.

### 2.5 Persistence strategy

`IVersusSeriesRepository` is the only seam: `Save`, `Load`, `ListSummaries`, `Delete`, `Archive`.
A challenge is a series in `Invited` status, so the brief's `CreateChallenge` / `AcceptChallenge` /
`DeclineChallenge` are domain operations on `VersusSeries` plus a `Save`, not separate storage
verbs.

Serialization is domain -> DTO -> JSON. DTOs are `[Serializable]` classes of public fields, all
enums written as **strings**, no scene references, no ScriptableObject references - only ids and
values. `VersusSeriesSerializer` lives in `Level5.Core` and uses `JsonUtility`, so edit-mode tests
round-trip through real JSON rather than through object copies.

Two implementations: `InMemoryVersusSeriesRepository` (stores the serialized string, for tests and
for a future remote stub) and `FileVersusSeriesRepository` (one JSON file per series under
`Application.persistentDataPath/versus`, matching `PendingMatchPersistenceStore` conventions).

### 2.6 Files to create / modify

Created in `Assets/Level5/Core/Versus`: the domain listed in 2.2, `VersusMatchCoordinator`,
`VersusSeriesValidator`, `VersusValidationResult`, `IVersusSeriesRepository`, `IVersusClock` /
`IVersusIdSource`, `VersusDomainException`, `RivalryRecord`, and `Persistence/`.

Created in `Assets/Scripts/versus`: `VersusCatalogs` (Resources assets, code fallback),
`DefaultCompetitiveRulesets` (the fallback registry), `FileVersusSeriesRepository`,
`VersusServices` (composition root), `ActiveVersusAttempt`, `VersusLauncher`,
`GameStatsAttemptResults`, `VersusMatchReporter`.

Created in `Assets/Scripts/Dev`: `VersusDevConsole` - drives a whole local or correspondence series
without any production menu, so the flow is exercisable before menus exist.

Modified: `GameRules` (one call in the existing match-end retry loop), `docs/README.md`.

Deleted: nothing.

### 2.7 Testing strategy

Edit-mode tests in `Assets/Tests/Editor`, following the existing naming. Coverage is enumerated in
section 38 of the brief and mirrored one-for-one: rulesets, attempt lifecycle, game resolution,
best-of-1/3/5/7 including 3-3 activating game seven and 4-0 stopping at four, persistence restore at
every interruption point, sealed-attempt leak checks, open-target behaviour, and versioning. Plus an
integration test that drives a real `GameStats` through `GameStatsAttemptResults` into a resolved
game, and an architecture test that keeps UI/gameplay code off the serialization DTOs.

### 2.8 Compatibility risks

- `GameRules.HandleMatchEnded` is the only modified gameplay path. The added call returns `true`
  immediately when no versus attempt is active, so every existing mode is untouched.
- No `GameOptions` field is read or written by any new code, so the architecture ratchet stays green
  without a new allowlist entry.
- No existing type changes shape. `GameModeDefinition` is not modified.

### 2.9 Implementation order

A domain -> B single game -> C series -> D persistence -> E correspondence -> F policies ->
G real-mode integration. Tests alongside each slice.

---

## 3. Plan review pass 1 - structure

| # | Problem | Impact | Solution | Plan change |
| --- | --- | --- | --- | --- |
| 1 | `AttemptTicket` as a separate type mirroring `Attempt` | two objects owning one lifecycle; the classic duplicate-state bug | `Attempt` *is* the ticket | Removed `AttemptTicket` from the type list; documented the mapping in 2.1 |
| 2 | `VersusGame` exposing its attempts publicly | sealed-attempt leakage becomes a UI-discipline problem instead of a structural one | attempts private; `ViewFor(participant)` is the only read path | Added `ParticipantGameView`; added the DTO architecture test |
| 3 | Series resolving against the live ruleset catalog | a balance edit silently changes an active game six | resolve against `SeriesSnapshot` only; the catalog is consulted solely to check the build can still *play* the snapshotted version | Stated explicitly in 2.1 and covered by a versioning test |
| 4 | `SeriesStatus` including `Expired` and `Archived` | states with no transition semantics (section 30) - nothing in the project has a deadline, and archival is a repository concern | dropped both; `Archive` is a repository verb | `SeriesStatus` reduced to Invited/Active/Completed/Declined/Forfeited |
| 5 | Draws could hang a series (1-1-1 in a best-of-three) | a series that never completes is unrecoverable state | complete when someone reaches `RequiredWins` **or** the playlist is exhausted; an exhausted level series is a `SeriesResult` of kind `Draw` | Added to 2.3; explicit tests |
| 6 | A generic metric dictionary in `AttemptResult` | section 11 warns against an unbounded blob; also allocates per comparison | fixed `AttemptMetric` enum backed by a fixed-length float array | Stated in 2.2 |
| 7 | Rivalry stored as an aggregate | a stored aggregate drifts from the series history that produced it | `RivalryRecord` is a pure fold over completed `SeriesResult`s, rebuildable at any time | Stated in 2.2 |
| 8 | Enum values serialized as integers | reordering an enum silently reinterprets stored correspondence data (section 36) | DTO enums are strings, parsed through guarded helpers | Stated in 2.5; round-trip tests |
| 9 | Reusing `MatchValidationCode` for versus errors | couples the two domains and grows one enum with unrelated members | separate `VersusValidationCode` mirroring the existing shape | Added to the file list |
| 10 | `VersusMatchCoordinator` drifting into a God object | it is the obvious place for everything to accumulate | it may only: load, call one domain operation, save, raise one event. No rules, no comparison, no scene knowledge | Constraint written into 2.6 and enforced by review |
| 11 | Turn inbox in scope | not in the brief's Required list; would be speculative infrastructure | domain events are raised so an inbox projection is buildable; the projection itself is deferred | Moved to remaining work |
| 12 | Building production versus menus | cannot be authored without the Unity editor, and would put competitive state next to UI | a Dev-only driver component exercises the flow; production UI is follow-up | `VersusDevConsole` added to the file list |

---

## 4. Plan review pass 2 - correspondence networking readiness

Each question from section 52, answered against the revised plan.

| Question | Answer |
| --- | --- |
| Can a local participant later become remote without changing game logic? | Yes. Gameplay only produces an `AttemptResult` from `GameStats`; it never learns who the opponent is. `MatchParticipant.Kind` is the only place the distinction exists, and nothing below the coordinator reads it |
| Can an attempt survive application restart? | Yes. `Attempt` is serialized with the series; issuing an attempt saves before the scene loads |
| Can two attempts be submitted hours apart? | Yes. Nothing about resolution depends on the two attempts being in the same session; only the series document is read |
| Can opponent result visibility remain sealed? | Yes, structurally - `ViewFor` is the only read path. The one honest exception is documented in remaining risks: on a shared device, both players' own high-score rows land in the same local stats database |
| Can a ruleset update occur while a series is active? | Yes. Resolution uses the snapshot. A version the build can no longer play is refused at issue time with a coded reason rather than silently mis-scored |
| Can a backend later become authoritative without moving gameplay code? | Yes. Authority lives in whoever implements `IVersusSeriesRepository` plus the coordinator; both sit above gameplay and below nothing that gameplay reads |
| Can incomplete matches be loaded without scene state? | Yes. `Level5.Core.Versus` has no `MonoBehaviour`, no `GameObject`, no scene lookup; the whole domain is constructible in an edit-mode test |
| Can invalid duplicate submissions be rejected? | Yes. Completing a completed attempt throws; submitting for a participant that holds no live attempt throws; a result whose ruleset id or version disagrees with the attempt throws |
| Can best-of-seven stop at 4-0? | Yes, and no attempt is issued for games five to seven. Tested |
| Can one mode explicitly reject asynchronous play? | Yes. `VersusCapability` is authored per ruleset; a mode with no ruleset at all is not versus-capable, and the validator refuses a series whose mode lacks the requested capability |
| Can the same series model support local and online participants? | Yes. `VersusSeries` knows only `ParticipantId`s |

Two gaps this pass found, and their fixes:

1. **Attempt issuance had no idempotence.** Calling "start the next attempt" twice - a double-tapped
   button, or a retry after a failed save - would have created two attempts for the same
   participant and game. *Fix:* `IssueAttempt` returns the existing live attempt when one is already
   outstanding for that participant and game, rather than creating a second.
2. **A crash between "attempt issued" and "scene loaded" would strand the series.** The attempt is
   `Ready` forever with no way back. *Fix:* an outstanding `Ready`/`Started` attempt is reissued to
   the same participant on the next request, and `Abandon` exists as the explicit way out.

---

## 5. Final architecture review

Run after implementation, against the checklist in section 59 of the brief.

| Looked for | Found |
| --- | --- |
| boolean-mode regression | none. Competition kind is `VersusMode`, capabilities are a flags enum, and the existing `NoNewModeIdentityBooleans` ratchet still passes |
| duplicated state | none in the domain - the series score is computed from the games rather than kept beside them, and rivalry is a fold. `ActiveVersusAttempt` duplicates the ruleset id and version from the attempt, which is safe because both are immutable for the attempt's life |
| God objects | the coordinator does load, one domain call, save, one event, and nothing else |
| network leakage | asserted absent by `TheDomainHasNoNetworkingAndNoFileSystem` |
| persistence leakage | asserted by `OnlyStorageTouchesTheSerializationDocuments` |
| incorrect series termination | covered by twenty series tests including 4-0, 4-1, 4-2, 4-3, 3-3 into game seven, and all-drawn |
| sealed-result leakage | checked in the views, in the summaries, and in `VersusLog`, which logs no metric value at all |
| unsupported-mode bypasses | the competition kind is fixed at creation and every ruleset is checked against it then |
| weak ruleset identity/versioning | enforced in the constructor; ids must be well formed, versions start at 1, the compatibility floor cannot exceed the version |
| scene-dependent domain state | asserted by `TheDomainHasNoSceneDependencies` |
| unnecessary abstraction | reviewed; the closest call is `ParticipantSeriesView`, which earns its place by being what makes the sealed guarantee hold at series level |

**One real defect was found and fixed.** `ActiveVersusAttempt` stayed set when a player abandoned a
competitive match to the menu. The turn was still outstanding, correctly - but the *next ordinary
match* would then have been submitted as that turn, handing somebody a result they never played for.
The attempt is now tied by reference to the `MatchConfiguration` it was launched for, and
`ActiveMatch.Begin` replacing that object is exactly what "a different match is running" means.
Pinned by `AnAbandonedTurnDoesNotCaptureTheNextOrdinaryMatch`.

No unrelated problems were introduced. Pre-existing findings are recorded in section 1.4.

## 6. What was deliberately not built

Deferred, per section 49: cloud backend, matchmaking, friends, push notifications, online accounts,
tournaments, rankings, seasons, real-time multiplayer, server simulation, anti-cheat, ghost replays,
challenge links, series drafting, rivalry UI, and the turn-inbox projection. Production versus menus
are follow-up work; the Dev console exists so the architecture is exercisable without them.
