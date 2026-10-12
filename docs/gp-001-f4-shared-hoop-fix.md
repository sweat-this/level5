# GP-001 F4: independent shared-hoop makes

Audited 2026-10-12 against fetched `dev` **`a51b3c887b4ee3fd96be73010f92f38d7544fb4b`**.
No open PR overlapped the fix. The issue inventory includes local multiplayer certification work
(#235, #243, #245, #264); no GP-001 issue or merged F4 implementation was found.

## Root cause and attempt identity

`BasketBallShotMade` shared upper/lower flags across balls and reset a hoop-wide `isColliding`
latch in `Update`. `BasketBallShotMadeCollision` authorized a lower crossing using that shared
upper flag. The unchanged implementation failed seven of ten focused callback regressions:
cross-ball authorization, both variants of two-ball scoring, stale-attempt authorization,
rebound event duplication, unclassified-attempt identity, and missing upper runtime validation.

The only production caller of `shotMade` is the lower adapter. The adapter was also the only
external production consumer of `ShotMade1`/`ShotMade2`; no serialized UnityEvent consumers were
found. Existing tests that called `shotMade` directly now establish an upper crossing first.

Human and CPU `shootBasketBall` both call `updateBasketBallStateShotTypeOnShoot`, which forwards
to `BasketballShotPipeline.ApplyShotAttempt`. Human `PlayerDunk.playerDunk` uses that same seam.
The generation advances there, when an attempt begins, before the existing coroutine/launch.

`MatchStats.ShotAttempt` is unsuitable as identity: `ApplyShotAttempt` can increment it zero or
multiple times for its existing flag combinations, and `CollisionCheckDefense` writes it outside
shot initiation. The existing distance classification can leave every flag false at the exact
seven-point boundary. This fix preserves those mechanics and statistics; it does not repair them.

## Changes and ownership

| File | Change |
| --- | --- |
| `Assets/Scripts/basketball/BasketballState.cs` | Runtime-only `ShotAttemptGeneration`; internal attempt initiation advances it and clears the snapshot. Ordinary snapshot reset leaves identity unchanged. |
| `Assets/Scripts/basketball/BasketballShotPipeline.cs` | Calls attempt initiation at the existing snapshot-reset position; all statistical conditions and ordering stay intact. |
| `Assets/Scripts/basketball/BasketBallShotMade.cs` | Small hoop-owned collection matches runtime references and generations. Consumes the crossing before scoring/events; rejects duplicate, reversed, stale, and invalid crossings. Keeps the scoring snapshot local to the invocation. |
| `Assets/Scripts/basketball/BasketBallShotMadeCollision.cs` | Forwards the lower collider's runtime to the owning hoop; fails closed on missing bindings. |

The collection has one entry per observed live basketball. Destroyed Unity runtime references are
pruned during lookup; repeated callbacks allocate no entries. Disabling the hoop drops pending
crossings but retains consumed attempts, preventing a disable/re-enable rebound award. Destruction
clears all entries; initial match binding discards crossings observed before composition.

`IBasketballRuntime`, `MadeShotResult`, and `ShotResolved` remain unchanged. Public `shotMade`
now requires a matching upper crossing. `ShotMade1` and `ShotMade2` remain writable compatibility
members, documented as non-authoritative; assigning them cannot award a make.

The accepted-make pipeline retains its order: select the colliding runtime's state/statistics,
update distance, apply existing scoring/marker bookkeeping, publish `MadeShotResult`, apply Money
Ball pickup behavior, reset the launch snapshot. No formula, scene, prefab, GUID, mode, participant,
persistence, Backend, or Platform ownership changes.

## Regression coverage and evidence levels

- **Direct method tests:** existing `Level5BasketBallShotMadeTests`, adapted to register a crossing,
  preserve distance/line scoring, streaks, marker counts, Money Ball pickups, missing context,
  bind-once behavior, and participant attribution.
- **Simulated trigger callbacks:** new `Level5SharedHoopTests` invokes both real component handlers
  with real human/CPU runtime components. Covers matching/mismatching balls, interleaving,
  no frame reset, reverse order, duplicate lower callbacks, rebounds and unrelated counter writes,
  successive and unclassified attempts, all flag shapes, invalid bindings, compatibility flags,
  never-launched balls, match binding, hoop replacement/disable, and reentrant events.
- **Actual Rigidbody/trigger physics:** new `Level5SharedHoopPhysicsPlayModeTests` loads production
  catalogs, composes Versus CPU in `level_01_scrapyard`, and uses its authored upper/lower wiring
  and spawned human/CPU balls. Controlled Rigidbody positions and `Physics.Simulate` produce two
  accepted makes in one simulation call and one rendered frame. Rebounds produce no new events;
  the next attempts produce two more. Collider centers are aligned using authored sphere offsets;
  no collider geometry or scoring callbacks are replaced.
- **Existing gameplay integration:** human/CPU launch convergence (2), Spot-Up 7s course/lifecycle
  (3), local simultaneous versus production screens/results (4), and real hoop context composition
  (1). Spot-Up's injected make now establishes an upper crossing. The composition fixture selects
  a valid shooter, restores remembered selection, and redirects SQLite rather than assuming the
  fresh menu's default Doctor Blood can shoot.

The physics fixture uses the existing scene-noise policy in the test body's Unity log scope. The
arena still logs unrelated CPU `PlayerAnimationEvents` and negative-scale collider diagnostics;
these are not certified clean by the scoring assertions. The initial fixture failures caused by
default character selection, an unavailable CPU character prefab, and log-scope setup were diagnosed
and corrected in test arrangement; no production behavior was changed for those failures.

## Validation

Unity **6000.5.7f1**, as pinned by `ProjectSettings/ProjectVersion.txt`.
Local logs/results are under `Temp/gp001-f4/` and are intentionally untracked.

| Check | Actual result |
| --- | --- |
| `scripts/validate-repository.ps1` | Passed. |
| Unity compilation | Passed through the EditMode and graphics-enabled PlayMode invocations. |
| Baseline `Level5SharedHoopTests` | 10 run: 3 passed, 7 failed against unchanged production. `red-final.xml`. |
| Final affected EditMode tests | 167 passed, 0 failed. `editmode-final.xml`. |
| Initial focused PlayMode suite | 11 run: 9 passed, 2 fixture failures; retained in `playmode.xml`. |
| Corrected arena fixtures | 2 passed, 0 failed. `arena-final.xml`. |
| Final focused PlayMode suite | All 11 passed, 0 failed, 0 skipped on final production code. `playmode-final.xml`. |
| Real player-data integrity | Player databases and game persistence files unchanged, including timestamps. EditMode rewrote Unity's own `TestResults.xml` and `Unity/.../Editor/Analytics/values` under the normal product root; these are runner/editor artifacts, not game saves. |
| Hosted CI | `Repository quality` is `disabled_manually`; no hosted pass is claimed. |

The Unity arguments used were:

```text
-batchmode -nographics -projectPath C:/Users/Patrick/Documents/github/level5
-runTests -testPlatform EditMode
-testFilter Level5SharedHoopTests;Level5BasketBallShotMadeTests;Level5BasketballShotPipelineTests;Level5MatchStatsTests;Level5ShotScoringTests;Level5BasketballMarkerOwnershipTests;Level5BasketballRuntimeIdentityGuardTests;Level5BasketBallShotMadeDependencyGuardTests;Level5BasketballGameRulesFacadeGuardTests
-testResults C:/Users/Patrick/Documents/github/level5/Temp/gp001-f4/editmode-final.xml
-logFile C:/Users/Patrick/Documents/github/level5/Temp/gp001-f4/editmode-final.log
```

PlayMode used `-batchmode` with graphics enabled, `-testPlatform PlayMode`, and this initial filter:

```text
Level5SharedHoopPhysicsPlayModeTests;Level5BasketballShotAttemptAndLaunchConvergencePlayModeTests;Level5BasketBallShotMadeCompositionPlayModeTests;Level5SpotUp7sPlayModeTests;LocalSimultaneousVersusProductionPlayModeTests
```

The corrected-run filter was
`Level5SharedHoopPhysicsPlayModeTests;Level5BasketBallShotMadeCompositionPlayModeTests`.
The final suite reused the complete initial filter with outputs `playmode-final.xml` and
`playmode-final.log`.
Before PlayMode, `ProjectSettings.asset` was saved byte-for-byte and its product name temporarily
set to `level5-gp001-f4-tests`. It was restored after each invocation. This separates persistent
JSON/session/progression files as well as the fixtures' disposable SQLite and versus repositories.
The physics fixture skips outside that isolated product namespace. To reproduce it, use the same
temporary product name and restore the exact original settings afterward; never commit that change.

## Review passes and limits

**Pass 1 — correctness:** checked runtime/attempt matching, independent same-step makes, duplicate
and bounce rejection, invalid bindings, successive attempts, and scoring attribution. Added the
pre-binding crossing reset and its regression. Consuming before events and using a local snapshot
also protect reentrant duplicate callbacks and resolution of another ball during publication.

**Pass 2 — architecture/regression:** checked collection bounds/destruction, disabled-hoop lifetime,
allocations, public compatibility members, no global authority additions, and unchanged scoring
order. Corrected only the affected fixtures' valid-character setup, persistence isolation, and log
scope; retained existing scoring/marker/Money Ball tests. Final affected EditMode and repository
validation ran after review changes; arena tests exercise the final production code.

This is controlled editor physics and gameplay integration evidence. It does not certify natural
shot flight/aiming, dunk animation/flight, physical multiplayer devices, every arena/mode, or a
standalone player build. Optional hosted Unity CI is separate from these local results.
