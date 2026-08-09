# The Basketball Shot Lifecycle

Last reviewed: 2026-08-09

AUD-010 asks for two things: this document, and a single shot result consumed by everything
downstream. The result exists for the part that decides *what a shot is worth*
(`Level5.Core.ShotScoring`); the rest of the pipeline is described here as it actually is, including
the parts that are still spread out.

## The path a shot takes

```
  input                     PlayerController / AutoPlayerController
    |
    v
  launch                    BasketBall.shootBasketBall / BasketBallAuto
    |                       - snapshots the shot into BasketballState
    |                       - ShotModifiers decides accuracy / range / release   [Level5.Core]
    |                       - gameStats.ShotAttempt++ and the per-line attempt counter
    v
  flight                    physics
    |
    v
  make                      BasketBallShotMade.OnTriggerEnter
    |                       - longest-shot tracking
    |                       - ShotScoring.Score decides points and which counter moves [Level5.Core]
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
  `OnShootShotMarkerId`);
- whether the money ball was active (`MoneyBallEnabledOnShoot`).

This matters because the player can move, the marker can change state, and the money ball can be
spent while the ball is in the air. Scoring the make against the situation at launch is what makes a
shot worth what it looked like when it was taken.

AUD-015 was a bug in exactly this area: the attempt flags were set at launch but only cleared on the
make path, so a missed two followed by a made three scored both. They are now reset at the start of
each attempt by `BasketballState.ResetShotAttemptSnapshot()`.

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
- **Not done.** There is still no single *event*. `BasketBallShotMade` applies the result directly to
  a `GameStats` component, and the HUD, audio and marker updates each observe the world in their own
  way rather than subscribing to one "shot resolved" signal. Doing that properly means giving
  `GameStats` an owner, which is AUD-002 territory.
- **Not done.** `BasketBall` and `BasketBallAuto` still duplicate `shootBasketBall`, `Launch` and
  score-text formatting (AUD-017). The scoring path is now shared - both human and CPU shots reach
  the same single call site - but the launch path is not.

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
