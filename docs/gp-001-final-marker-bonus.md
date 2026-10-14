# GP-001: preserve final-marker bonuses under overlapping shots

Audited and corrected on 2026-10-13 against fetched `origin/dev`
**`d330e23703df5d65edc25cbd7cfb49352f9b42ee`**. Unity **6000.5.7f1**
(`017862109af0`). The baseline was unchanged when fetched immediately before implementation
and again after validation. The original worktree was clean; existing worktrees were inventoried
and left untouched. No open PR targeting dev overlapped this work.

## Evidence and gameplay contract

Historical scope note: this report records PR #301's preserved fifth-registration accounting.
On 2026-10-14 the owner approved counting an enabled marker's quota-reaching attempt instead;
see the [subsequent accounting correction](gp-001-money-ball-attempt-quotas.md). The original
reproduction tables below describe #301, not the later accounting policy.

Read the documentation index, AI presets, [shot lifecycle](shot-lifecycle.md),
[mode-16 audit](gp-001-mode16-cpu-timeout-audit.md), [made-shot finality](gp-001-made-shot-finality.md),
and the current sources, fixtures and relevant authored data. Applied the repository debug and
two-pass planning workflows sequentially, without delegation.

Inspected merged [#284](https://github.com/sweat-this/level5/pull/284) (runtime/generation crossing
identity), [#299](https://github.com/sweat-this/level5/pull/299) (pre-zero human/CPU grace) and
[#300](https://github.com/sweat-this/level5/pull/300) (Playing-only make acceptance). All are in
the baseline and their owners remain unchanged. Open-issue inventory and all-state final-marker
and contest searches found no competing correction or alternate approved bonus rule.
[#245](https://github.com/sweat-this/level5/issues/245) and its dependency
[#244](https://github.com/sweat-this/level5/issues/244) own future simultaneous contest/Spot-Up
certification and course policy. This fix does not enable those modes for two humans or change
shared-course ownership. Hosted Repository Quality is currently `disabled_manually` (#232/#233).

The request, lifecycle documentation, `ShotScoringInput.IsFinalMarkerAttempt` contract, pure scorer
and existing uncontested mode-16 final-attempt test agree: the attempt reaching the marker quota
owns the bonus, subject to the existing contest and enabled-marker gates. No conflicting rule
was established. Mode 16 authors three-point contest rules and five attempts in the tested
Scrapyard course. Mode 18 authors ThreePoint, FourPoint **and** AllRanges together: it retains
the existing doubling gate through its specific contest flags. AllRanges alone remains undoubled.

There is an existing quota/accounting distinction: `MoneyBallAttempts` explicitly credits the
fifth registration in Three/Four/Seven contests, whereas final-shot doubling uses `MaxShotAttempt`.
Authored four-attempt quotas exist in `basketball_goal`, slab/snow/Sudan variants and Shore;
the standalone three/four-point position prefabs use five. This is recorded, not generalized.
The synthetic quota-three regression preserves a doubled final make with zero fifth-attempt
credits. Active money ball plus a doubled final marker still credits two money balls.

## Root cause and ownership

1. Human `BasketBall.shootBasketBall` and CPU `BasketBallAuto.shootBasketBall` classify the shot
   through `updateBasketBallStateShotTypeOnShoot`, then call the same marker/money-ball pipeline.
   Player/CPU controllers initiate these existing shooting paths; accuracy, meter and navigation
   are outside the correction.
2. `BasketballShotPipeline.ApplyShotAttempt` calls `BasketBallState.BeginShotAttempt`: advance
   `ShotAttemptGeneration`, end preceding flight eligibility and reset the old snapshot, then
   apply the existing line flags/stat counters.
3. `ApplyMarkerAndMoneyBallOnShoot` checks rules/occupancy, captures the exact participant's
   `CurrentShotMarker`, then calls `BasketBallShotMarker.RegisterAttempt(runtime)`.
   Registration increments the shared marker count and captures `finalAttemptRuntime` at quota.
4. Upper/lower hoop crossings match runtime and generation. `BasketBallShotMade.shotMade`
   consumes the crossing, checks injected Playing eligibility, updates that runtime's stats and
   captured marker, publishes one `MadeShotResult`, runs its accepted tail and resets the snapshot.
5. Baseline `BasketBallShotMade.cs:410` instead derived `IsFinalMarkerAttempt` from the marker's
   **current** shared count. Another registered attempt could grant the fourth shot a bonus or
   revoke the fifth's. The pure scorer's arithmetic was correct for the supplied Boolean.
6. Marker completion still waits for the originally captured runtime's actor/ball readiness,
   records session completion once, then requests ending if the course cleared. That reference
   identifies the completion owner, not a particular generation; it must not be used for scoring.

The new Boolean belongs to the ball's existing attempt snapshot, next to generation/marker state.
It is assigned immediately after registration, without yielding, and read by scoring. No new
marker/session authority, manager, public method signature, dependency or serialized field is needed.

## Direct reproduction

Before changing production, the new EditMode fixture ran actual registration, snapshot capture,
upper/lower callbacks and made-shot resolution using concrete human/CPU basketball components.
Three controlled misses preceded fourth/fifth registration on the same marker; another ball
registered the sixth where indicated. No pure-scoring Boolean was injected.

`A/M` below means `MoneyBallAttempts/MoneyBallMade`; values belong to the identified shot's runtime.

| Controlled case | Baseline points, A/M | Corrected points, A/M | Marker attempts/makes; accepted events |
| --- | --- | --- | --- |
| Fourth then fifth make, count five | Fourth 6, 0/1; fifth 6, 1/1 | Fourth 3, 0/0; fifth 6, 1/1 | 5/2; 2 |
| Fifth then fourth make, count five | Fourth 6, 0/1; fifth 6, 1/1 | Fourth 3, 0/0; fifth 6, 1/1 | 5/2; 2 |
| Sixth registered before either make, either order | Fourth 3, 0/0; fifth 3, 1/0; sixth 3, 0/0 | Fourth 3, 0/0; fifth 6, 1/1; sixth 3, 0/0 | 6/3; 3 |
| Fifth misses, same human or CPU ball attempts another marker | Next shot 3, cumulative 1/0 | Next shot 3, cumulative 1/0 | Old marker 5/0; next 1/1; 1 |
| Uncontested fifth | 6, 1/1 | 6, 1/1 | 5/1; 1 |
| Uncontested fifth with active money ball | 6, 2/2 | 6, 2/2 | 5/0 (fixture MaxShotMade=0); 1 |
| Synthetic quota three | 6, 0/1 | 6, 0/1 | 3/1; 1 |

All synthetic markers remain enabled until their completion owner is explicitly ticked; scoring
does not take over completion. Repeated crossings publish no extra events. The missed fifth's
upper crossing cannot authorize the new generation. Missing registration cannot borrow a bonus
from the shared count. Snapshot clearing after a made final and before a subsequent attempt is
asserted; ordinary and AllRanges-only scoring remain three, and a disabled contest marker scores zero.

The baseline production-scene test also failed directly: configured mode 16, Scrapyard, one human
and one CPU, fourth/fifth registration before either make, **human delta 6 / CPU delta 6**, marker
5 attempts / 2 makes, 2 accepted events, human A/M 0/1 and CPU 1/1. This establishes runtime
integration evidence; it is a controlled callback reproduction, not naturally overlapping flight.

Corrected production-scene output:

| Scenario | Human/CPU points delta | Attempts/makes | Cumulative accepted events | Human A/M; CPU A/M |
| --- | --- | --- | --- | --- |
| Fourth then fifth | 3 / 6 | 5/2 | 2 | 0/0; 1/1 |
| Fifth then fourth | 3 / 6 | 5/2 | 4 | 0/0; 2/2 |
| Human fourth, human sixth, CPU fifth | 6 / 6 | 6/3 | 7 | 0/0; 3/3 |

Each marker waits while the captured CPU flight is airborne, then completes once when it is ready;
remaining markers progress 5 -> 4 -> 3 -> 2. The extra attempt does not change that owner.
A pending CPU final make is rejected after Ending and again after Completed. Human **12** and CPU
**18** remain independent; SQLite saves **12**, with the same result ID and frozen duration on
repeat verification. Observed result ID `ca8971c9a9874e62b6c5d7fa88198655`, match
`match-bf7aa01b333640c4907a829c5ff7730c`, duration **0.02706146** seconds. The test requests a
controlled end, so this duration is not a natural 70-second countdown claim.

## Review pass 1: correctness, before implementation

Reviewed the proposed post-registration Boolean against out-of-order makes, shared participants,
sixth attempts, misses, retrieval, reused balls, absent snapshots, callback duplication, generation
changes, disabled/completed markers and money-ball credit. Kept `== MaxShotAttempt`, rather than
`>=`, so only the quota-reaching attempt receives the bonus. Kept the live enabled-marker gate:
completion can still make an earlier airborne shot ineligible under existing rules.

Refinement: reset the Boolean only with the existing snapshot lifecycle. Retrieval already ends
flight grace; it does not need another bonus writer because the next attempt resets the snapshot.
Accepted makes clear it in their existing tail; rejected terminal makes preserve their snapshot
and remain consumed. Keep launch-time fifth-attempt accounting and the independent active-money
credit. Do not freeze marker readiness, replace crossing identity, or alter first-ending semantics.

## Review pass 2: architecture and regression, before implementation

Reviewed per-ball versus per-marker ownership, API/assembly boundaries, serialized references,
completion readiness, #299 grace, #300 finality, Spot-Up, contest gates and #245/#244 scope.
The existing `BasketBallState` is the correct owner; reusing `finalAttemptRuntime` would confuse
completion ownership with shot identity. Keep a runtime-only property with an internal setter,
so the pipeline can capture it without exposing a new public mutation method or serializing state.
No scene, prefab, GUID, mode ID, dependency, network or persistence migration is required.

Refinement: reuse existing fixtures and disposable project rather than introduce test infrastructure
into production. Add controlled production-scene callback coverage, retain separate actual
Rigidbody/trigger and pre-zero grace regressions, and verify local-first retry identity with the
existing test. The final review also added current mode-18 combined flags to the overlap matrix;
AllRanges-only coverage alone would not describe that authored configuration.

## Final minimal implementation and exact files

Completed plan: verify current dev/overlap and intended rule -> reproduce through real registration
and resolution -> complete both reviews -> re-fetch dev -> capture/reset/read the attempt Boolean
-> validate focused contracts and production scene -> inspect diff and refresh baseline/overlap.

| Changed file | Rationale |
| --- | --- |
| `Assets/Scripts/basketball/BasketballState.cs` | Runtime-only `IsFinalMarkerAttempt`, reset with the existing snapshot. |
| `Assets/Scripts/basketball/BasketballShotPipeline.cs` | Capture quota-reaching status immediately after existing registration. |
| `Assets/Scripts/basketball/BasketBallShotMade.cs` | Supply the captured status to the existing pure scorer. |
| `Assets/Tests/Editor/Level5SharedHoopTests.cs` | Registration-to-resolution overlap, sixth attempt, miss/reuse, gates, accounting and duplicate-callback regressions. |
| `Assets/Tests/PlayMode/Level5PostRemediationCertificationPlayModeTests.cs` | Configured human/CPU overlap, completion readiness, terminal rejection and stable saved primary score. |
| `docs/gp-001-final-marker-bonus.md` | This audit, reviews, reproduction, validation and PR summary. |
| `docs/shot-lifecycle.md` | Document the added attempt snapshot and preserved gates. |
| `docs/README.md` | Index the correction. |

`ShotScoring.Score`, marker registration/completion, timer, result handling, CPU/human controllers
and authored assets are unchanged. Production logic adds one Boolean capture and replaces one read.

## Validation and commands

Evidence: ignored `TestResults/gp001-final-marker/`, with argument arrays, logs, NUnit XML,
process-exit JSON and a SHA-256 source manifest. The disposable project is
`TestResults/gp001-post-2026-10-12/project`; company/product are
`level5-audit-20261012-14b2cdc` / `level5-gp001-f4-tests`. Normal project settings/player data were
not used for runtime validation. Existing `LocalProfileTestDatabase` supplies disposable SQLite;
`CampaignBoundaryPersistenceIsolation` restores session, pending-match, pending-local and account-74
progression files including `.bak`/`.tmp`. Backend sessions are cleared inside that isolation.

| Run | Actual NUnit result | Process exit |
| --- | --- | --- |
| BaselineEdit | 5 passed, 4 failed, 0 skipped; expected failures expose fourth bonus borrowing and fifth bonus revocation | 2 |
| BaselinePlay | 0 passed, 1 failed, 0 skipped; expected fourth-shot 6 versus required 3 | 2 |
| FocusedEdit | 232 passed, 0 failed, 0 skipped | 0 |
| FinalEditDelta | 50 passed, 0 failed, 0 skipped; final shared-hoop fixture including authored mode-18 combinations | 0 |
| FocusedPlay | 18 passed, 0 failed, 0 skipped | 0 |
| PublishEdit | 236 passed, 0 failed, 0 skipped; complete affected set rerun immediately before publication | 0 |
| PublishPlay | 18 passed, 0 failed, 0 skipped; graphics-enabled affected set rerun immediately before publication | 0 |
| Repository validator | PASS | 0 |

The final EditMode set contains **236** cases: marker ownership 62, made-shot 23, pipeline 26,
match lifecycle 19, match stats 20, shared hoop 50, pure scoring 17, timer context 19. The 186
unchanged non-hoop cases initially reused the passing FocusedEdit result; PublishEdit subsequently
reran all 236 cases together. PlayMode: launch convergence 5,
made-shot composition/reload 1, mode-16 production probes 8, shared-hoop engine physics 1, Spot-Up 7s 3.
No required local test was blocked or skipped. Hosted CI was not run because its workflow is disabled.

Reproduction command shape (replace `<filter>` with the exact filters below):

```text
Unity.exe -batchmode -projectPath <disposable-project>
  -runTests -testPlatform <EditMode|PlayMode> -testFilter <filter>
  -testResults <evidence>/<run>.xml -logFile <evidence>/<run>.log
EditMode adds -nographics; PlayMode adds -force-d3d11.

FocusedEdit / PublishEdit:
Level5BasketballShotPipelineTests;Level5BasketBallShotMadeTests;Level5SharedHoopTests;Level5ShotScoringTests;Level5BasketballMarkerOwnershipTests;Level5TimerMatchEndContextTests;Level5MatchLifecycleTests;Level5MatchStatsTests

FinalEditDelta:
Level5SharedHoopTests

FocusedPlay / PublishPlay:
Level5PostRemediationCertificationPlayModeTests.Mode16;Level5BasketballShotAttemptAndLaunchConvergencePlayModeTests;Level5SharedHoopPhysicsPlayModeTests;Level5SpotUp7sPlayModeTests;Level5BasketBallShotMadeCompositionPlayModeTests

Repository validator:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository.ps1
```

The baseline tests used the pre-correction fixture revision with the baseline production files;
their complete commands/output are retained. Final source/test bytes match the disposable copies
used for validation. No full Unity suite or standalone/IL2CPP build was run. Actual-trigger physics
is covered by retained fixtures; the new overlap probe deliberately controls callbacks and actor
readiness. Physical input, natural overlapping trajectories, every arena/CPU and live authenticated
Backend delivery remain unexecuted. Existing CPU animation-role diagnostics and negative-scale
collider warnings are recorded by the scene fixture, not repaired or claimed clean.

## Acceptance and PR-ready summary

The evidence establishes attempt-specific bonus ownership, both fourth/fifth make orders, sixth
attempt isolation, human/CPU attribution, missed/made snapshot reset, preserved completion,
ordinary/All Point gates, money-ball policy, pre-zero grace, terminal score finality and stable
local-first result identity. There are no unrelated architectural or serialized changes.
Shared-course policy, AI navigation and #245/#244 certification remain separate work.

Suggested title: **Preserve final-marker bonuses under overlapping shots**.

Suggested description: Contest makes previously determined final-shot eligibility from the marker's
mutable shared attempt count. A fourth shot could inherit the fifth's bonus, and a sixth registration
could revoke the fifth's bonus. Capture quota-reaching status on the existing per-ball attempt
snapshot immediately after registration, reset it with that snapshot and read it during scoring.
Preserve contest/enabled-marker gates, fifth-attempt money-ball accounting, shared completion,
pre-zero grace, accepted-ending finality and persistence contracts. Direct baseline failures and
focused Unity regressions, including configured human/CPU mode 16, establish the correction.

Publication branch: `fix/gp001-final-marker-bonus`, targeting `dev`. The final pre-publication fetch
still resolved dev to `d330e23703df5d65edc25cbd7cfb49352f9b42ee`; open PR inventory remained empty.
The subsequent senior code review found no material findings, so no extra production changes were
introduced during delivery. Publication was explicitly requested after that review.
