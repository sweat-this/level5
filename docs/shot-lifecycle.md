# The Basketball Shot Lifecycle

Last reviewed: 2026-09-02

AUD-010 asks for two things: this document, and a single shot result consumed by everything
downstream. The result exists for the part that decides *what a shot is worth*
(`Level5.Core.ShotScoring`), and `BasketBallShotMade` now publishes a first scene-level
`MadeShotResult` event. The rest of the pipeline is described here as it actually is, including the
parts that are still spread out.

## The path a shot takes

```
  input                     PlayerController / AutoPlayerController
    |
    v
  launch                    BasketBall.shootBasketBall / BasketBallAuto
    |                       - snapshots the shot into BasketballState
    |                       - ShotModifiers decides accuracy / range / release   [Level5.Core]
    |                       - gameStats.Stats.ShotAttempt++ and the per-line attempt counter
    v
  flight                    physics
    |
    v
  make                      BasketBallShotMade.OnTriggerEnter
    |                       - longest-shot tracking
    |                       - ShotScoring.Score decides points and which counter moves [Level5.Core]
    |                       - publishes MadeShotResult for subscribers             [Level5.Core]
    |                       - marker ShotMade updated
    v
  display                   updateScoreText -> MatchHudPresenter
    |
    v
  match end                 GameRules.HandleMatchEnded
                            - HighScoreModel  -> SQLite / API
                            - MatchExperience -> progression                     [Level5.Core]
                            - VersusMatchReporter -> series, if there is one
```

## The snapshot, and why it exists

The decision of *what kind of shot this was* is made at launch, not at the make. `BasketballState`
records, at the moment the ball leaves the player's hands:

- which line it was taken from (`TwoAttempt` / `ThreeAttempt` / `FourAttempt` / `SevenAttempt`);
- whether the player was standing on a shot marker, and which one (`PlayerOnMarkerOnShoot`,
  `OnShootShotMarker`);
- whether the money ball was active (`MoneyBallEnabledOnShoot`).

This matters because the player can move, the marker can change state, and the money ball can be
spent while the ball is in the air. Scoring the make against the situation at launch is what makes a
shot worth what it looked like when it was taken.

AUD-015 was a bug in exactly this area: the attempt flags were set at launch but only cleared on the
make path, so a missed two followed by a made three scored both. They are now reset at the start of
each attempt by `BasketballState.ResetShotAttemptSnapshot()`.

### Participant-scoped marker ownership (AUD-010 Phase 1c)

Marker occupancy and the launch snapshot used to be an `int` id (`CurrentShotMarkerId` /
`OnShootShotMarkerId`) resolved back through `GameRules.instance.BasketBallShotMarkersList[id]` -
one shared list, indexed the same way regardless of which participant was asking. That made it
possible for one participant's marker state to be read (or overwritten) using another's id, and it
gave "no marker" and "marker 0" the same representation, since the id defaulted to 0.

Marker occupancy is now the marker reference itself, owned by each participant's own
`BasketBallState`:

- `CurrentShotMarker` - the marker this participant currently occupies, or null. Set by
  `BasketBallState.EnterShotMarker(marker)` / `ExitShotMarker(marker)`, called from
  `BasketBallShotMarker`'s own `OnTriggerEnter`/`OnTriggerExit`, resolved to the exact colliding
  participant through `IBasketballParticipantStateProvider` (implemented by the actor-side
  `PlayerIdentifier` over its existing `basketball`/`autoBasketball` references - never a role-wide
  flag, never `GameLevelManager.instance.players[0]`). The most recently entered marker wins if two
  overlap; exiting an earlier marker after entering a newer one is a no-op.
- `OnShootShotMarker` - the marker snapshotted at this attempt's launch, via
  `BasketBallState.CaptureShotMarkerForAttempt()`. Once captured it does not change for the rest of
  the attempt, even if the participant exits the marker or enters another one while the ball is
  airborne. `BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot` captures it and registers the
  attempt directly on that reference (`BasketBallShotMarker.RegisterAttempt`); `BasketBallShotMade`
  reads it directly to update the made-shot marker state. Neither indexes
  `GameRules.BasketBallShotMarkersList` by id.

**Marker final-attempt owner.** Each `BasketBallShotMarker` remembers which runtime's attempt first
reached `MaxShotAttempt` (`finalAttemptRuntime`, set by `RegisterAttempt` - a later extra attempt
before the marker disables does not overwrite it). Completion for a point-contest marker waits on
that captured runtime's own `Actor.HasBasketball` / `Actor.InAir` / `State.InAir`, not
`GameLevelManager.instance.players[0]`'s - so a secondary human or CPU's final attempt is judged on
their own state, not the primary player's.

`GameRules` still owns everything session-wide: marker discovery/filtering, `MarkersRemaining`,
match-end routing, and mutable `MoneyBallEnabled`. This slice only moved *whose* marker a shot
belongs to, not who manages the marker list or ends the match.

### Immutable match-rule reads move off GameRules (AUD-010 Phase 1c)

`GameRules` exposes a handful of properties (`GameModeRequiresMoneyBall`,
`GameModeThreePointContest`, `GameModeFourPointContest`, `GameModeSevenPointContest`,
`GameModeAllPointContest`) that only ever forwarded a `ResolvedMatchRules` value it had already
read from `MatchRuntime.Rules` - they carried no session state of their own. `BasketBallShotMade`
and `BasketBallShotMarker` now read those same values directly from `MatchRuntime.Rules`
(`RequiresMoneyBall`, `IsThreePointContest`, `IsFourPointContest`, `IsSevenPointContest`,
`IsAllPointContest`) instead of going through the `GameRules` facade, since a made shot has no
reason to route through a `MonoBehaviour` singleton to read a value that has been immutable since
before the scene loaded.

The boundary this draws:

- **immutable match rules** (`ResolvedMatchRules`, reached through `MatchRuntime.Rules`) - read
  directly by basketball code, once per made-shot operation and reused for every decision that
  operation makes, rather than resolved separately for each check.
- **mutable basketball-match/marker state** (`MoneyBallEnabled`, `PositionMarkersRequired`,
  `MarkersRemaining`, `IsGameOver()`, `RequestGameOver()`, the serialized
  `InThePocketActivateValue`) - stays on `GameRules`, unchanged by this slice.
- **`GameRules` compatibility properties** - kept on `GameRules` for callers outside basketball;
  only `Assets/Scripts/basketball` is forbidden from reaching for the migrated five (enforced by
  `Level5BasketballGameRulesFacadeGuardTests`).

`BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot` already read `MatchRuntime.Rules` directly
before this slice (it predates the `GameRules` forwarding properties being used there) and was left
as-is; its two remaining `GameRules` reads (`PositionMarkersRequired`, `MoneyBallEnabled`) are
mutable session state, not part of this migration.

`BasketBallShotMarker.Update()` resolves `IsPointContestMode()` once per frame and threads the
result into `setDisplayText(bool)` and the two marker-completion branches, rather than each of up to
three call sites resolving it (and therefore `MatchRuntime.Rules`) separately. For a directly-entered
scene, `MatchRuntime.Rules` rebuilds a fresh `ResolvedMatchRules` from the legacy globals on every
call (it is a validated match's `MatchConfiguration.Rules` that is the stable, allocation-free
reference); collapsing three potential resolutions into one keeps that cost from scaling with the
number of call sites inside a single frame.

**Marker-local presentation occupancy vs. participant gameplay occupancy.** `BasketBallState`'s
`CurrentShotMarker`/`PlayerOnMarker`/`OnShootShotMarker` above are per-participant and unaffected by
how many other participants share the marker. `BasketBallShotMarker` also tracks its own
presentation occupancy - the `_playerOnMarker`/`_autoPlayerOnMarker` flags used by `Update()` and
`setDisplayText()` to decide whether to show marker stats at all. Before this slice those flags were
set/cleared unconditionally by whichever hitbox last entered/exited, so one human exiting a marker
could clear the display flag out from under a second human still standing on it. They are now
mirrors of two runtime `HashSet<Collider>` membership sets (`humanOccupants`/`cpuOccupants`, never
serialized) - true while at least one qualifying collider of that role remains in the trigger, kept
in sync by `AddHumanOccupant`/`RemoveHumanOccupant`/`AddCpuOccupant`/`RemoveCpuOccupant`. Membership
removal on exit does not depend on the exiting collider's participant still being resolvable, since
the collider has physically left regardless. Live-lifecycle check at the time of this change: no
code destroys, deactivates, or disables a player's hitbox collider mid-match, so no stale-membership
pruning was added.

## What a made shot is worth

`Level5.Core.ShotScoring.Score` is the whole of it. There are three scoring worlds and a made shot is
in exactly one:

| World | When | Points |
| --- | --- | --- |
| Open play | not a marker contest, not Points by Distance | the line's value, raised while an In the Pocket streak is running |
| Marker contest | 3 / 4 / 7 / all point contest | the line's value, but only while standing on an enabled marker; the marker's final attempt scores double in the three, four and seven point contests |
| Points by Distance | mode 19 | `floor(distance * 6 / 10)`, whatever line it came from |

The streak bonus is +1 on a three, +2 on a four and +3 on a seven, and nothing on a two. That is not
a formula, it is the four numbers the mode was authored with.

Two behaviours are preserved deliberately and pinned by tests rather than fixed, because changing
either is a scoring change rather than a refactor:

- **A contest shot taken off the marker counts as nothing at all** - not even towards the made-shot
  counter. This is why a stray shot during a contest cannot inflate the shooting percentage.
- **The final marker shot taken with the money ball active credits two money balls.** The original
  credited one for the doubled marker shot and another for the active money ball, with no check that
  they were the same shot. Almost certainly unintended;
  `AFinalMarkerShotTakenWithTheMoneyBallActiveCreditsTwo` names it for what it does so that changing
  it later is a visible decision.

## Where it is still spread out

Being honest about what AUD-010 has and has not achieved:

- **Done.** The scoring arithmetic is one tested function with no scene dependencies. Adding a mode
  no longer means reading 130 lines of nested conditionals to work out what a shot is worth.
- **Partially done, and the remaining gap is narrower than it first looked.** `BasketBallShotMade`
  publishes `MadeShotResult`, a pure payload containing the player id, CPU flag, shot kind, score,
  shot distance and resulting total points. As of 2026-08-13 it has its first real subscriber: the
  made-shot swish sound and rim animation moved behind `ShotResolved` instead of running inline in
  `shotMade()`, proving the event actually reaches a listener.
  - `GameStats` mutations correctly stay inline - they are the state the event reports, not a
    reaction to it.
  - The shot-marker `ShotMade` counter (`GameRules.instance.BasketBallShotMarkersList[...]`) also
    correctly stays inline on closer inspection: it feeds `GameRules.IsGameOver()`'s win condition
    directly, so it is rules/scoring bookkeeping computed as part of resolving the shot, not a
    presentation reaction - the same category as `GameStats`, not a candidate for AUD-008.
  - The HUD/scoreboard (`MatchHudPresenter`) is not actually called from `shotMade()` at all -
    `GameRules.Update()` polls it every frame regardless of whether a shot just happened, reading
    live `GameStats`/rules values. That is a different problem (a per-frame poll instead of a
    push) than "UI reaches into gameplay," and converting it to a `ShotResolved` subscriber would
    need its own design pass: other things the HUD displays (timers, streak state) change without
    a made shot, so it cannot simply stop polling in favor of the one event without first covering
    those paths too.

  Net: `MadeShotResult`/`ShotResolved` now has a genuine subscriber and a proven pattern to extend,
  but there is no further single-file, zero-risk slice left in this exact area - the next real step
  is deciding whether the HUD should move off polling at all, which is a design question, not a
  mechanical extraction.
- **Mostly done, as of 2026-08-13 (AUD-017).** `Launch`'s modifier computation and score/profile-text
  formatting now live in `BasketballShotPipeline.cs`, called by both `BasketBall` and `BasketBallAuto`.
  The marker/money-ball block inside `shootBasketBall` - previously commented out on the CPU path,
  so CPU shots earned no marker-contest or money-ball credit - is shared too now
  (`BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot`), confirmed as a real gap rather than
  intentional scope and fixed accordingly. What's left per-caller is deliberate: `Launch`'s Rigidbody/
  animator/analytics tail (human has an analytics call and an `isCpu` swish gate the CPU path
  doesn't; CPU has `shootTrigger`/`Locked` resets the human path doesn't), and each
  `LaunchBasketBall` coroutine's `MeterEnded` wait condition, which the two files check on opposite
  values - confirmed intentional, left untouched.

## Why this blocks the assembly split

`player` and `basketball` reference each other 15 and 18 times, which is the largest of the cycles
keeping `Assets/Scripts` in one assembly. The shot pipeline is most of that cycle: `basketball` needs
`PlayerController` / `AutoPlayerController` to read the shot sliders, and `player` needs `BasketBall`,
`ShotMeter` and `GameStats` back.

Extracting the scoring removed none of those references - it is arithmetic, and the arithmetic was
never the coupling. The coupling is that the ball asks the player how good the shot was, and the
player asks the ball what happened. A shot-resolved event, plus moving `GameStats` out of
`basketball/`, is what would actually cut it. See the assembly-split section of
[`deep-audit-2026-08-09.md`](deep-audit-2026-08-09.md).
