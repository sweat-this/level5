# Phase 1d Plan: Invert the Game-Manager Edge

Produced via `docs/ai/skills/implementation-plan-red-team.md`, against `dev` at `409e54643`
(Phase 1a–1c, PR #65, already merged). 1d.1–1d.3 below are implemented, not just planned — see
**Implementation status** at the end for what's landed and what verification actually ran.

## Re-measured scale

[Systems Restructure Plan](systems-restructure-plan.md) measured this edge on 2026-08-17 at 15 back
(9 for basketball). Re-running its own methodology — declared types per top-level folder, foreign
type mentions counted with comments stripped, `Legacy~` excluded — against current `dev` gives:

| From | To | Refs |
| --- | --- | --- |
| game manager | player | 68 |
| game manager | basketball | 46 |

By file and type:

**game manager → player**
`GameLevelManager.cs`: PlayerIdentifier 9, PlayerController 5, PlayerHealth 4, PlayerAttackQueue 4,
AutoPlayerController 2, CharacterProfile 2. `GameRules.cs`: PlayerIdentifier 8, PlayerHealth 6,
CharacterProfile 2. `PlayerRegistry.cs`: PlayerIdentifier 8. `SpawnCoordinator.cs`: PlayerIdentifier
12, AutoPlayerDefense 2, CharacterProfile 1. `MatchHudPresenter.cs`: PlayerIdentifier 2. `Timer.cs`:
PlayerIdentifier 1.

**game manager → basketball**
`MatchHudPresenter.cs`: BasketBall 10, BasketBallState 9, GameStats 4. `GameRules.cs`:
BasketBallShotMarker 6, GameStats 6. `GameLevelManager.cs`: GameStats 3, BasketBall 2. `Pause.cs`:
BasketBall 3, GameStats 3.

This confirms the scale the task was scoped around (~68 / ~46, dominated by PlayerIdentifier and
GameStats/BasketBall) rather than the 15/9 the top-level doc still shows — that doc's numbers predate
the growth and should be updated once this phase lands.

**A methodology gap found during this pass, not previously documented:** the count only catches
spelled type names. It misses reach-through member chains on an already-typed object — chiefly
`PlayerIdentifier.basketBallState`, `.playerController`, `.characterProfile`, `.gameStats`, all
lower-camel-case field access that never spells the foreign type. `Timer.ReportTimeExpired()` is the
clearest case: it reads `player.basketBallState.Thrown`, `player.playerController.Grounded`, and
`player.gameStats.Stats.ConsecutiveShotsMade` — real coupling to three foreign types — and registers
as exactly one `PlayerIdentifier` mention. The 68/46 count is a floor. The dependency test this phase
adds (§4) has to catch the chain pattern too, or it passes while the actual violation stays invisible.

## What the scale actually is, categorized by behavioral purpose

Reading every cluster's call sites (not just its type name) splits the count three ways:

**A — Roster/identity bookkeeping.** Game manager holding or passing `PlayerIdentifier` references to
know who is playing and in which slot. `PlayerRegistry` (8 refs), most of `GameLevelManager`'s
`players`/`Player1..4`/`getSortedGameStatsList()` (9 refs), all of `SpawnCoordinator`'s registration
(15 refs), `GameRules`' campaign-portrait read and match-end stats-list gathering (10 refs),
`PlayerHealth.OnDied` event subscription (already push, not poll — the pattern the plan wants
elsewhere, already in place here). This is game manager's job, not a violation. ~55 of the 68.

**B — Dead reach-through.** Fields assigned via `GetComponent<T>()` with zero readers anywhere in the
codebase, confirmed by grep, not inference:
- `GameLevelManager._characterProfile` (`CharacterProfile`, private, no accessor, never read).
- `GameLevelManager._autoPlayerController` (`AutoPlayerController`, private, no accessor, never read).
- `GameLevelManager._playerController2` / the `PlayerController2` property (serialized field, never
  assigned in code, zero external readers of the property).
- `GameLevelManager._gameStats` / the `GameStats` property (never assigned anywhere, zero readers —
  the getter can only ever return null).
- `GameRules.player1DisplayName` … `player4DisplayName` (public fields, set once in `Start()`, zero
  readers anywhere else in the codebase — not even inside `GameRules` itself).

This is 8 of the 68 by grep, plus 4 more (`player1DisplayName`..`4`) the grep doesn't count because
the read is a member chain, not a spelled type — same undercounting gap as Timer.

**C — Live, working, narrowly-fixable coupling.** Smaller than first assumed — see the correction
below. `GameStats` (basketball/) is a compatibility facade over `Level5.Core.Match.MatchStats`, and
several call sites already narrow to `MatchStats` locally (`MatchStats stats0 = players[0].gameStats.Stats;`
in `MatchHudPresenter`) — but `GameStats` is not a pure data pass-through everywhere. Its
`getExperienceGainedFromSession()` calls `MatchExperience.Calculate(BuildExperienceInput())`, which reads
`MatchRuntime.Rules` — a dependency `MatchStats` architecturally cannot have, by its own doc comment
("`BuildExperienceInput` stays on `GameStats`... this assembly cannot reference [Assembly-CSharp]").
Every `GameStats`-typed field in `GameRules` and `MatchHudPresenter` that calls this method (`gameStats1`
in both classes, `primaryGameStats` in `GameRules.ApplyMatchProgressionResult`, and the
`SetMatchContext(..., GameStats primaryStats, ...)` parameter that feeds `gameStats1`) **cannot narrow
to `MatchStats`** without either duplicating that computation elsewhere or changing `MatchHudPresenter`'s
public API — real design work, not a mechanical retype. Checked precisely, call site by call site (see
§Slice 1d.2): none of `GameRules`' 6 `GameStats` mentions can safely narrow. Of `MatchHudPresenter`'s 4,
only the one redundant local in `SetScoreDisplayText()` can. Only in `Pause.updateFreePlayStats()` is the
`GameStats` read (`.ExperienceGained`, a plain property, not the compute method) narrowable — and even
there, only after fixing the actual bug in this bucket: it reaches `BasketBall.instance.GameStats`, the
exact reassignable-singleton pattern AUD-016 already fixed elsewhere, instead of
`GameLevelManager.instance.Player1.gameStats` like `GameRules.GetPrimaryGameStats()` already does.

**D — Real coupling, disproportionate fix risk or explicitly out of scope already.**
- `Timer.ReportTimeExpired()`'s three-way reach-through. The obvious fix (push the check to the
  player/ball side, which already legitimately reads `GameRules`/`Timer`) moves a match-ending decision
  into a different component's `Update()`. `Timer.cs`'s own comment two lines away documents a real,
  previously-shipped bug caused by exactly this class of Unity Update-order ambiguity. Small coupling,
  disproportionate regression surface. Not touched in 1d.
- `GameRules`' `BasketBallShotMarker` ownership (6 refs). [shot-lifecycle.md](shot-lifecycle.md)
  already concluded this "correctly stays inline... it feeds `GameRules.IsGameOver()`'s win condition
  directly" — rules/scoring bookkeeping, not a candidate. Not touched.
- `MatchHudPresenter`'s direct `BasketBall.instance.BasketBallState`/`.LastShotDistance` reads for a
  handful of per-mode live-text branches, and its direct `GameLevelManager.instance.getSortedGameStatsList()`
  read for the versus/local-multiplayer scoreboard text. shot-lifecycle.md already named converting the
  HUD off polling as "a design question, not a mechanical extraction," specifically because other things
  the HUD shows (timers, streak state) would need the same treatment first. Not touched in 1d.
- `GameLevelManager`'s live facade — `PlayerController1` (14 external callers, one of them
  `GameLevelManager` itself, toggling run on a keybind in its own `Update()`), `PlayerHealth` (9
  callers), `PlayerAttackQueue` (2 callers), `AutoPlayer` (2 callers). Removing these requires migrating
  ~25 call sites that live in *other* folders, not game manager's — that's each of those folders'
  work, not a game-manager-owned slice, and well past this phase's blast radius. Not touched in 1d.

## Slices

Each ships and verifies independently, in this order.

### 1d.1 — Delete the dead reach-through (Category B)

Remove, with no behavior change: `GameLevelManager._characterProfile` and its `GetComponent` call;
`_autoPlayerController` and its assignment; `_playerController2` and the `PlayerController2` property;
`_gameStats` and the `GameStats` property; `GameRules.player1DisplayName`..`player4DisplayName` and the
four assignment lines in `Start()`. Removes 6 of the 68 counted player-edge refs outright (2
CharacterProfile, 2 AutoPlayerController, 2 of 5 PlayerController — the `_playerController2`
declaration and the `PlayerController2` property; the field, the `GetComponent` assignment, and the
`Player1` property survive, since those are Category A) plus the 4 uncounted display-name reads. It
also removes all 3 of `GameLevelManager`'s basketball-edge `GameStats` mentions (the dead field
declaration and the property, which spells `GameStats` twice on one line) — that cut belongs here, not
to 1d.2's narrowing pass, since there's nothing left to narrow once the dead field is gone.

Before deleting `_playerController2`: confirm via `git grep`/prefab search that no scene or prefab
relies on the serialized value being present in the inspector for a reason code doesn't show (a
debug-only inspector reference, say). Given zero code readers this is very unlikely to matter, but the
check is cheap and the field is `[SerializeField]`.

**Verify:** full compile; existing EditMode suite green (no test should reference the deleted
members — confirm via `Level5GameStatsFacadeTests`, `Level5MatchStatsTests` still passing); one smoke
PlayMode session confirming the run-toggle keybind and stats-toggle keybind in `GameLevelManager.Update()`
still work (they read the surviving `_playerController1`/`BasketBall.instance`, untouched).

### 1d.2 — Fix the two reach-through bypasses; narrow only where the call site genuinely allows it (Category C)

Traced every one of the remaining `GameStats` mentions individually (§Category C) rather than assuming
uniform behavior, since the first draft of this plan assumed all of them were narrowable and was wrong
— `GameRules.gameStats1`/`primaryGameStats` and `MatchHudPresenter.gameStats1` all reach
`.getExperienceGainedFromSession()`, which needs the real `GameStats` facade, not `MatchStats`. The
actionable scope is smaller than first planned:

- **`GameRules.cs`: no change.** All 6 `GameStats` mentions are load-bearing: `gameStats1` and
  `SetMatchContext`'s parameter feed `MatchHudPresenter`'s experience-gained text;
  `SaveMatchResults`'/`GetPrimaryGameStats`'s `GameStats` return/params feed
  `DBConnector.savePlayerAllTimeStats(GameStats)`; `ApplyMatchProgressionResult`'s param calls
  `.getExperienceGainedFromSession()` directly. None of this is a bypass — it's the real dependency.
- **`MatchHudPresenter.cs`: narrow one local, leave `gameStats1` alone.** `SetScoreDisplayText()`
  currently re-fetches `GameStats gameStats = GameLevelManager.instance.Player1.basketball.GetComponent<GameStats>();`
  every call, then reads only `.Stats`-shaped counters off it (`.Stats.TotalPoints`,
  `.Stats.FourPointerMade`, etc — never `.getExperienceGainedFromSession()` on *this* local, only on the
  `gameStats1` field elsewhere in the same class). Replace it with
  `MatchStats stats = gameStats1.Stats;` — reusing the value already pushed via `SetMatchContext`,
  exactly like `GetStatsTotals()`/`GetDisplayText()` already do two methods away — and retarget every
  `gameStats.X` read in that method to `stats.X`. Removes both `GameStats` mentions on that one line
  (declared type and generic argument). `gameStats1` itself keeps its declared type: it's the one field
  in this class that still needs `.getExperienceGainedFromSession()`.
- **`Pause.cs`: fix the singleton reach, then narrow what that unlocks.** `updateFreePlayStats()` reads
  `BasketBall.instance.GameStats` three times — the exact reassignable-singleton pattern AUD-016 already
  fixed elsewhere, unrelated to why it's slower than `GameRules`' path. Redirect all three to
  `GameLevelManager.instance.Player1.gameStats`, the same path `GameRules.GetPrimaryGameStats()` already
  uses. Two of the three feed `DBConnector.savePlayerAllTimeStats`/`PendingMatchPersistenceStore.QueueAllTime`
  and must stay `GameStats`-typed at that call. The third — `.ExperienceGained`, a plain property, not
  the compute method — narrows to `.gameStats.Stats.ExperienceGained`.

  **Preserved, not fixed:** `Pause`'s free-play path reads the raw `.ExperienceGained` field, while
  `GameRules.ApplyMatchProgressionResult` recomputes it fresh via `.getExperienceGainedFromSession()`
  for every other mode. Whether these should agree is a progression-scoring question, not an
  architecture one — surfaced here as a finding, not changed as a side effect of this phase.

Net effect: 2 of `MatchHudPresenter`'s 4 basketball-edge `GameStats` mentions removed (one line, two
mentions), 2 remain (`gameStats1` field, `SetMatchContext` parameter). 1 of `Pause`'s 3
`GameStats` mentions removed; all 3 of `Pause`'s `BasketBall` mentions removed (redirected, not merely
retyped). `GameRules` is unchanged. Combined with 1d.1's 3 (`GameLevelManager`'s dead `GameStats`), this
slice plus 1d.1 remove 9 of the 46 basketball-edge refs — not the 19 the first draft claimed before this
call-site check.

**Explicitly not touched:** the `DBConnector.savePlayerAllTimeStats(GameStats)` /
`PendingMatchPersistenceStore.QueueAllTime(string, GameStats)` call boundary, and
`GameRules`/`MatchHudPresenter`'s experience-gained coupling to `GameStats`. Both are real, and neither
is a mechanical retype: the first is a persistence-layer API change outside this plan's boundary (see
Deferred); the second would mean either giving `MatchStats` a `MatchRuntime`-reading method (breaking
the assembly boundary `GameStats.cs`'s own doc comment says is deliberate) or splitting
`SetMatchContext`'s push into two channels (a real API redesign). Neither is in scope here.

**Verify:** full compile; `Level5GameStatsFacadeTests`, `Level5GameStatsApplyMadeShotTests`,
`Level5MatchStatsTests` green; manual PlayMode pass across every HUD-visible mode this touches (see
§5) since `MatchHudPresenter.SetScoreDisplayText` formats text for every mode, plus one free-play match
played to the Pause-driven end screen specifically, since that's the path `updateFreePlayStats()` owns.

### 1d.3 — The ratchet dependency test

Add `Assets/Tests/Editor/Level5GameManagerEdgeTests.cs`, same shape as the existing
`Level5MatchArchitectureTests` (allowlist-ratchet, `TheAllowlistHasNoStaleEntries` companion check,
comment-stripped source scan). Two things it must check, given the gap found in §1:

1. **Spelled foreign types.** For every `.cs` file directly under `Assets/Scripts/game manager/`
   (excluding `Legacy~`), no file outside an explicit allowlist may mention `PlayerController`,
   `PlayerHealth`, `PlayerAttackQueue`, `AutoPlayerController`, `AutoPlayerDefense`, `CharacterProfile`,
   `BasketBall`, `BasketBallAuto`, `BasketBallState`, `BasketBallShotMarker`, or `GameStats` (a
   `PlayerIdentifier` mention is not restricted — that's the accepted roster-identity type, §Category A).
2. **Reach-through chains.** No file outside the allowlist may match
   `\.basketBallState\.|\.playerController\.|\.autoPlayerController\.|\.gameStats\.|\.characterProfile\.`
   — this is what makes Timer's pattern (and any new one shaped like it) visible even when no type name
   is spelled.

Two separate file-level allowlists, one per check — matching `Level5MatchArchitectureTests`' own
granularity (by file, not by line/cluster within a file), since that's what the established pattern in
this repo already does and a per-cluster allowlist would be far more fragile to maintain. Verified by
directly grepping current file content for each check (comments stripped) rather than predicted by hand
— the plan's own §Category C correction is exactly why this pass trusts the grep over the plan's prose:

**Spelled-type-name allowlist**, seeded from every Category A/D cluster:
- `PlayerRegistry.cs`, `SpawnCoordinator.cs` — roster/spawn identity, the class's whole job.
- `GameLevelManager.cs` — `PlayerController1`/`PlayerHealth`/`PlayerAttackQueue`/`AutoPlayer` live
  facade (14+9+2+2 external callers; migrating them is other folders' work) and `BasketBall.instance`
  for the stats-toggle keybind.
- `GameRules.cs` — `PlayerHealth.OnDied` subscription, campaign-portrait `CharacterProfile` read,
  match-end roster gathering, `BasketBallShotMarker` win-condition bookkeeping (shot-lifecycle.md), and
  the `GameStats` mentions §1d.2 found are all load-bearing (`.getExperienceGainedFromSession()`, the
  `DBConnector` boundary).
- `MatchHudPresenter.cs` — `BasketBall.instance.BasketBallState` live-text reads,
  `getSortedGameStatsList()` scoreboard formatting (shot-lifecycle.md's deferred HUD design pass), and
  the two surviving `GameStats` mentions (`gameStats1`, `SetMatchContext`'s parameter) for the same
  experience-gained reason as `GameRules`.

**Reach-through-chain allowlist** — checked directly against current file content (comments stripped),
not assumed: `GameLevelManager.cs` (`getSortedGameStatsList()`'s `x.gameStats.Stats.TotalPoints`),
`GameRules.cs` (the campaign-portrait read, and `LoadNextCampaignLevel`'s
`players[0].gameStats.Stats.TotalPoints`/`players[1]...` for the end-round winner/loser score — the
same match-end/campaign-transition bookkeeping as the rest of that file, not previously called out by
name in this plan but the same category), `MatchHudPresenter.cs` (`updatePlayerScore()` and
`GetDisplayText`'s versus branch — the deferred scoreboard-formatting cluster; `SetScoreDisplayText`'s
`ConsecutiveShots`/`InThePocket` branches reaching `GameLevelManager.instance.Player1.gameStats.Stats...`
directly, the deferred live-polling cluster), `SpawnCoordinator.cs` (`InitializeHumanProfile`'s
`identifier.characterProfile.intializeShooterStatsFromProfile(...)`), `Timer.cs` (the three-way
reach-through). **Not on this list, and not to be added speculatively:** `PlayerRegistry.cs`, `Pause.cs`
— neither has a chain match today; adding either preemptively would defeat the ratchet for exactly the
files where new coupling is most likely to reappear unnoticed. `GameRules.cs`'s two commented-out lines
inside `IsGameOver()` (dead, `//`-prefixed) don't count either way once comments are stripped, same as
the existing test's own comment-handling.

Both allowlists get their exact final contents from re-scanning the repository after 1d.1 and 1d.2 land,
not from this plan's prose — matching what the test itself will assert, not a prediction of it.

This is the phase's actual exit condition: not zero references, but a test that fails the build the
moment a *new* file or a *new* chain shows up outside this list — the plan's own exit criterion
("asserted by a dependency test that fails on a new back-reference") reads exactly this way already for
the GameOptions migration, and needs no new mechanism here, just the second regex.

**Verify:** the new test passes against the state after 1d.1/1d.2 (allowlist matches exactly what's
left); confirm it fails if a reference is temporarily reintroduced by hand during review, then remove
that temporary reference before landing.

## Deferred, named explicitly (not silently dropped)

- **Timer's reach-through.** Real coupling, small payoff, real Unity Update-order regression risk
  given the area's documented history. Revisit only if it causes an actual bug, per the top-level plan's
  own "why is input late" precedent for pulling deferred work forward on evidence rather than schedule.
- **MatchHudPresenter off polling.** Needs the design pass shot-lifecycle.md already called for —
  timers, streak state, and the scoreboard formatting all need the same treatment together, not this
  cluster alone. Separate future slice.
- **GameLevelManager's live PlayerController1/PlayerHealth/PlayerAttackQueue/AutoPlayer facade.**
  Inverting it means migrating ~25 external call sites in the folders that own them (basketball/,
  combat/, enemy/ and others) off `GameLevelManager.instance.X`. That work belongs to whichever future
  slice touches those folders, not to a game-manager-scoped phase.
- **`DBConnector`/`PendingMatchPersistenceStore`'s `GameStats`-typed persistence API.** A `MatchStats`
  overload is small and safe but is a persistence-boundary change; tracked separately, HIGH-VALUE, not
  required here.
- **`GameRules.BasketBallShotMarker` ownership.** Already decided, by shot-lifecycle.md, to stay
  inline. Not revisited by this plan.

## Non-goals

No new interfaces, events, or abstractions are introduced by 1d.1–1d.3 — every change is a delete, a
retype, or a redirect to a path the codebase already uses elsewhere. Nothing in `Assets/Scripts/player/`
or `Assets/Scripts/basketball/` changes. No scene or prefab edit is required (§1d.1's serialized-field
check is verification, not a scene edit — the field is deleted from code, not reassigned in a scene).
No persistence, save-format, or network contract changes.

## Acceptance criteria by step

| Step | Done when |
| --- | --- |
| 1d.1 | The five dead members are gone; compile is clean; no test references them; run-toggle and stats-toggle keybinds still work in a PlayMode smoke session. |
| 1d.2 | `MatchHudPresenter.SetScoreDisplayText` no longer calls `GetComponent<GameStats>()`, reading `gameStats1.Stats` instead; `Pause.updateFreePlayStats` no longer reaches `BasketBall.instance.GameStats`, reading `GameLevelManager.instance.Player1.gameStats` instead, narrowed to `MatchStats` at the one call site that only needs `.ExperienceGained`; `GameRules` is unchanged (confirmed load-bearing, not a bypass); existing GameStats/MatchStats EditMode suites pass; every mode in §5's matrix displays HUD text and saves match results identically to before. |
| 1d.3 | `Level5GameManagerEdgeTests` exists, passes, and demonstrably fails when a disallowed reference or chain is reintroduced by hand; the allowlist contains only the clusters named in this plan, each with its reason. |

## Automated validation

- `./scripts/validate-repository.ps1` after each slice.
- Full Unity compile, pinned Unity version, after each slice.
- EditMode: full suite, with particular attention to `Level5GameStatsFacadeTests`,
  `Level5GameStatsApplyMadeShotTests`, `Level5MatchStatsTests`, `Level5MatchArchitectureTests`, and the
  new `Level5GameManagerEdgeTests`.
- PlayMode: `GameplayLevelUnpauseTests` (touches `Pause.cs` directly) plus the existing PlayMode suite.

## Manual Play Mode matrix

Per the top-level plan's own Phase 1 matrix (free play, marker contests, points by distance, In The
Pocket, CPU shooter, local multiplayer), scoped to what 1d.1/1d.2 actually touch:

| Mode | Why it's in this matrix |
| --- | --- |
| Free play | `Pause.updateFreePlayStats()` is exclusively the free-play end-of-match path (1d.2); also exercises `SetScoreDisplayText`'s `FreePlay` branch, which reads both the retyped local and the untouched `gameStats1.getExperienceGainedFromSession()` in the same branch. |
| Local multiplayer (2+ humans) / CPU shooter / Versus CPU | `SetScoreDisplayText`'s `VersusCpu`/`BeatThaComputahs` branch reads the retyped local once, then calls the untouched `updatePlayerScore()` — exercises the exact boundary between what 1d.2 changed and the deferred scoreboard-formatting cluster right next to it. This is also where AUD-016's reassignable-singleton bug lived before, on the `Pause` side. |
| Marker contest (3s/4s/7s/all) | Each contest branch in `SetScoreDisplayText` reads `Timer.instance.ScoreClockText.text = stats.TotalPoints...` off the retyped local; `GameRules`' own `BasketBallShotMarker` path is unchanged and runs alongside it in the same match. |
| Points by Distance | Exercises the `MatchHudPresenter` branch that mixes the retyped local's `.TotalDistance`/`.TotalPoints` with an untouched `BasketBall.instance.BasketBallState.PlayerDistanceFromRim`/`LastShotDistance` read in the same branch — the boundary between what 1d.2 changed and what it deliberately left alone. |

## Review pass record

**Pass 1 (architecture/Unity failure modes).** First draft included a slice adding a
`PlayerIdentifier.IsEligibleForTimeExpiredCheck(...)`-style method so `Timer` could ask one question
instead of chaining three reach-throughs. Rejected: it still leaves `Timer` depending on player/basketball
state for a match-ending decision, so it doesn't actually invert anything — and the alternative that
would (moving the check into the player/ball side's own `Update()`, since that side already legitimately
reads `Timer`/`GameRules`) risks the exact Unity Update-order bug `Timer.cs`'s own comments already
document as having shipped once in this area. Moved to Deferred rather than forced into a slice.

First draft also included a "convert `GameLevelManager`'s `GetComponent`-in-`Start()` caching to
self-registration into `LevelRuntimeContext`" slice, reasoning that `LevelRuntimeContext` already exists
as the composition root player-folder components could register into. Dropped on inspection: it changes
initialization-order-sensitive wiring for the sake of an internal mechanism, while leaving every external
accessor's type unchanged — it doesn't remove a single counted reference, since `PlayerController` /
`PlayerHealth` / `PlayerAttackQueue` still have to be declared on `GameLevelManager` either way for the
25 external callers. Risk with no coupling payoff; cut.

**Pass 2 (scope/compatibility/regression).** First draft proposed retyping `AllTimeStatsSnapshot.From`
and `DBConnector.savePlayerAllTimeStats` to accept `MatchStats` directly as part of 1d.2, since it's a
small, mechanical change sitting right next to the lines already being touched. Moved to Deferred: it's
a persistence-layer public API change, and `docs/persistence-boundaries.md` — not this plan — owns that
boundary. Bundling it would mix an unrelated contract change into a game-manager-scoped phase.

Also reconsidered whether `MatchHudPresenter`'s `PlayerIdentifier`-based scoreboard formatting
(`updatePlayerScore`, the versus branch of `GetDisplayText`) should get its own narrow fix separate from
the `BasketBall.instance` live-text cluster, since it doesn't share the same root cause. Decided against
splitting it out: shot-lifecycle.md already scoped "move the HUD off polling" as one design pass because
the same class's other display state (timers, streak) needs the same treatment together — fixing the
roster-formatting half alone would leave `MatchHudPresenter` in a permanently half-migrated state with no
single follow-up phase that finishes it. Left as one deferred item, not two.

**Pre-implementation audit, before any code changed.** Re-verified every claim in this plan against the
actual files rather than trusting the first draft's prose, and found one real bug in the plan itself:
`GameStats.getExperienceGainedFromSession()` is not a pure data pass-through — it reads `MatchRuntime`,
which `MatchStats` cannot reference by the assembly boundary `GameStats.cs`'s own doc comment already
documents. The first draft's 1d.2 assumed every `GameStats`-typed declaration in the four files could
narrow to `MatchStats`; traced call site by call site, `GameRules.gameStats1`/`primaryGameStats` and
`MatchHudPresenter.gameStats1` all call this method and cannot narrow. §Category C, §1d.2, the
acceptance-criteria table, and the Play Mode matrix were rewritten to the corrected, smaller scope: only
`MatchHudPresenter`'s one redundant local and `Pause`'s singleton-reach-plus-one-field narrow; `GameRules`
is untouched. Combined 1d.1+1d.2 basketball-edge reduction corrected from a claimed 19 of 46 to an actual
9 of 46 — smaller, but every number in the plan now traces to a specific call site rather than an
assumed uniform pattern.

Also re-verified the §1d.3 allowlists directly against current file content instead of composing them
from memory of the categorization pass: found `GameRules.LoadNextCampaignLevel` reaches
`players[0].gameStats.Stats.TotalPoints`/`players[1]...` for the campaign end-round score (same
match-end-bookkeeping category as the rest of that file, just not previously named), and confirmed
`PlayerRegistry.cs` and `Pause.cs` currently have zero reach-through-chain matches and must stay off
that allowlist so the ratchet actually protects them going forward.

## Implementation status

1d.1 and 1d.2 are landed exactly as this plan describes (five dead members deleted; the
`SetScoreDisplayText` local and `Pause`'s singleton reach fixed; `GameRules` untouched). 1d.3 is landed
as `Assets/Tests/Editor/Level5GameManagerEdgeTests.cs`, with both allowlists built from the actual
post-1d.1/1d.2 file scan in this section rather than predicted.

**Verified:** `./scripts/validate-repository.ps1` passes. A full Unity EditMode run (6000.5.7f1, the
pinned version) against 1d.1+1d.2 with `Level5GameManagerEdgeTests` not yet added: **459/459 tests
passed**, confirming the retype/redirect changes compile and regress nothing existing. Getting a clean
batchmode run took two corrections worth recording: `-quit` combined with `-runTests` makes Unity exit
before the test runner starts at all (looks like a pass — exit 0, clean log — and silently runs
nothing); and the first attempt hit a licensing handshake error that resolved on retry. Both are now in
this repo's memory (`unity-batchmode-playmode-nographics.md`) so they don't have to be rediscovered.

**Code review (2026-08-20) found three real problems, all fixed:**
1. `Level5GameManagerEdgeTests.SpelledTypeAllowlist` omitted `Pause.cs`, even though `Pause.cs` keeps
   one live `GameStats` mention by this plan's own design (§1d.2's persistence-boundary note). The
   EditMode run that followed confirmed this concretely: 463 total, 462 passed, 1 failed -
   `NoNewFileReachesForRestrictedPlayerOrBasketballTypes` failing on exactly `Pause.cs`. Fixed by adding
   `Pause.cs` to the allowlist with the same persistence-boundary reason `GameRules.cs` already carries.
2. `Pause.updateFreePlayStats()`'s new `GameLevelManager.instance.Player1.gameStats` reach (replacing
   `BasketBall.instance.GameStats`) dereferenced `Player1` with no null guard, while
   `GameRules.GetPrimaryGameStats()` guards this identical chain for the identical reason. Not a
   behavior regression - the original `BasketBall.instance.GameStats` was equally unguarded - but an
   inconsistency worth closing since the fix was explicitly modeled on `GetPrimaryGameStats()`'s
   pattern in the first place. Fixed with the same guard, returning early rather than throwing.
3. Two generated Unity EditMode artifacts (`editmode-test-results.xml`, `editmode-test-run3.log`, from
   the verification runs themselves) were left untracked in the working tree - the kind of
   machine-local file AGENTS.md says not to commit. Deleted.

**A second code-review pass (--fix) found one more real gap, fixed:** `Pause.updateFreePlayStats()`'s
guard checked `primaryPlayer == null` but not `primaryPlayer.gameStats == null`, so
`primaryGameStats.Stats.ExperienceGained` at the end of the method could still throw if
`GameLevelManager.instance.Player1` exists but its `gameStats` field hasn't been populated yet -
reachable if `SpawnCoordinator.SpawnBasketballs()` fails after `SpawnPlayers()` already registered the
roster (both run in one try/catch in `GameLevelManager.Awake()`, so a missing basketball prefab leaves
a player registered with no ball/`GameStats` attached). `savePlayerAllTimeStats`/`QueueAllTime` already
null-check their `GameStats` parameter internally, so only the last line was actually at risk - but the
guard's own comment claimed parity with `GameRules.GetPrimaryGameStats()`'s three-part check
(`instance != null && Player1 != null && Player1.gameStats != null`) while only implementing two of the
three parts. Extended to match exactly.

**Verified, confirmed:** `./scripts/validate-repository.ps1` passes. Five full Unity 6000.5.7f1
EditMode batchmode runs total across this work: run 1 (1d.1+1d.2, test not yet added) **459/459
passed**; run 2 (test added, allowlist bug present) **462/463**, with the one failure being the exact
bug the first code-review pass found; run 3 (after that fix) **463/463 passed**; run 4 (after the
second code-review pass's guard fix) **463/463 passed** again, confirming the tightened guard changed
no test-observable behavior. Getting a clean batchmode run at all took two corrections worth recording:
`-quit` combined with `-runTests` makes Unity exit before the test runner starts (looks like a pass -
exit 0, clean log - and silently runs nothing); and the first attempt hit a licensing handshake error
that resolved on retry. Both are now in this repo's memory (`unity-batchmode-playmode-nographics.md`)
so they don't have to be rediscovered.

**Not run, and cannot be run in this environment:** every manual Play Mode check in §5's matrix. There
is no human at a controller in this session. Free play, local multiplayer/versus CPU, marker contests,
and Points by Distance all need an actual playtest — of the changed HUD text and the free-play
end-of-match save specifically — before this lands on `dev`.
