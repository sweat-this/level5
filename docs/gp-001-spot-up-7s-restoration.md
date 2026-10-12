# GP-001 F1: Spot-Up 7s marker restoration

Audited 2026-10-11 against freshly fetched `dev` **`22d1da65ab4da7d9f45728a527ce0e450dd377dd`**. The worktree was clean. Open PR inventory was empty. Recent merged #281 published GP-001; no subsequent implementation existed. Issue #244 owns two-human Spot-Up certification and does not own this ordinary single-player repair. GP-001 F3 record eligibility remains out of scope.

## Correction and ownership

`Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_25.prefab` authored `modeRequiresShotMarkers7s: 0`. All three marker requirements were disabled, so `GameRules.Start` skipped `SetPositionMarkers`; the course never received its active list or remaining-marker count.

The only production change sets `modeRequiresShotMarkers7s: 1`. The existing `GameModeDefinitionFactory` derives `ShotMarkerRequirement.SevenPoint` and `ArenaCapability.SevenPointLine`. `GameModeCompatibility`, `LevelEligibility`, and `MatchConfigurationBuilder` retain selection and launch authority. `GameRules` still filters markers, owns the session count, and routes match end; each `BasketBallShotMarker` still owns its authored make quota and one-time completion latch.

Mode ID 25, ContestCompletion, the 180-second countdown, disabled point-contest flags, scoring, results, and public contracts are unchanged. Spot-Up 3s/4s retain their count-up clocks; Spot-Up All retains its count-up clock and existing three/four-point marker composition. No seven-point requirement was added to Spot-Up All.

## Effective Scrapyard composition and separate content decision

Representative shipping arena: level 1, `Assets/Scenes/level_01_scrapyard.unity`. Its production menu entry authors `levelHasSevenPointers: 1` and is selectable. It references `Assets/Resources/Prefabs/basketball/basketball_goal.prefab` (GUID `303a3ee97d40b31408ebaffa49026b63`). The scene does not override `maxShotMade`.

| Seven-point marker | Component provenance | Effective make quota |
| --- | --- | --- |
| `top_key.1` | `8816558116011339195` | 4 |
| `left_wing.2` | `3519853404512029345` | 4 |
| `left_wing.3` | `2617159862600707801` | 4 |
| `left_corner` | Nested `four_point_positions.prefab`, component `4840104462935277022`, overridden in `basketball_goal.prefab` | 4 |
| `top_key` | Nested `four_point_positions.prefab`, component `2098421257199349828`, overridden in `basketball_goal.prefab` | 4 |
| `right_corner` | Nested `four_point_positions.prefab`, component `5177487164573276349`, overridden in `basketball_goal.prefab` | 4 |

The shared hoop's `seven_point_positions` is a nested instance of `four_point_positions.prefab` (GUID `a8098587dc00ed745ad6e7b541c80fd2`), with three surviving inherited markers plus three added markers. The inherited markers' base five-make/four-point settings are overridden to four makes/seven points in the shared hoop. Both real-scene runs observed all six qualifying markers active; unrelated three/four-point markers were inactive. Runtime distance classification is also preserved.

The mode-25 description promises five makes per marker. Current Scrapyard content requires **24 makes across six markers**, rather than 30. The shared hoop's direct components and nested overrides, plus the mode-25 description, are the affected authored sources; no quota, scene, timer, or description was edited.

Smallest follow-up: approve whether four makes or five makes is intended. If four is intended, correct mode-25 copy. If five is intended, separately review the six effective shared marker quotas and their arena/mode consumers before changing content; changing the nested base prefab alone would not override the hoop's explicit four-make settings. This repair does not certify the advertised five-make course.

## Separately observed end-reason finding

The initial real-course test expected ObjectiveComplete but observed Unknown after successful termination. This is existing source behavior: `IShotMarkerSession.RequestMatchEnd` → `GameRules.RequestGameOver` → `RequestEnd(MatchEndReason.Unknown)`. `HandleMatchEnded` later requests ObjectiveComplete, but `MatchLifecycle` preserves the first accepted reason. The final regression explicitly characterizes Unknown while independently requiring one ending, one completed result flow, correct make/point totals, and the ordinary time summary. Timeout preserves TimeExpired.

No end-reason code was changed. A separate small follow-up can supply ObjectiveComplete at the marker-session end request and verify lifecycle consumers, without changing the general legacy RequestGameOver contract. This is separate from GP-001 F3's incomplete-course record eligibility.

## Regression coverage

- `Level5AuthoredMatchDataTests`: load actual production prefabs for Spot-Up 3s/4s/7s/All; assert their marker, objective, clock, and non-point-contest rules. Mode 25 must derive SevenPointLine, reject a plain court, remain eligible on authored Scrapyard through account-aware cycling, and retain the requirement through validated launch.
- `Level5BasketballMarkerOwnershipTests`: use mode-25 resolved rules and the shared prefab's authored quota with existing marker/session helpers. Excess missed attempts cannot complete a make-quota marker. Each quota completes once; only the final marker requests ending once.
- `Level5SpotUp7sPlayModeTests`: launch authored mode 25 on real Scrapyard through the existing builder/scene harness. Observe effective scene quotas, seven-point activation and unrelated-marker exclusion. Inject attempts through `BasketballShotPipeline` and makes through the production hoop callback; verify misses, every quota, final-marker ending/completion, score totals, and summary. A second launch advances elapsed time to the unchanged 180-second boundary and verifies expiry with an uncleared course. Result writes use the existing disposable SQLite helper.

## Validation evidence

Pinned editor: Unity **6000.5.7f1**, revision **017862109af0**. Graphics remain enabled for PlayMode. Existing scene tests deliberately suppress unrelated scene log errors; assertions must establish the tested behavior. A passing run does not establish an error-free scene.

Commands below show the exact Unity arguments, launched with PowerShell `Start-Process -WindowStyle Hidden`; tests omit `-quit` so the runner controls shutdown. Logs/XML are local under `Logs/spot-up-7s/`.

```powershell
$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe'
$Project = 'C:/Users/Patrick/Documents/github/level5'
./scripts/validate-repository.ps1

& $Unity -batchmode -quit -projectPath $Project `
  -executeMethod MatchDefinitionMigration.ExportCharacterizationFromCommandLine `
  -logFile "$Project/Logs/spot-up-7s/export-fixed.log"

$Filter = 'Level5AuthoredMatchDataTests;Level5MatchCompatibilityTests;Level5MatchConfigurationTests;Level5PlayerSelectCompatibilityParityTests;Level5BasketballMarkerOwnershipTests;Level5BasketballShotMarkerSessionTests;Level5MatchLifecycleTests;Level5TimerMatchEndContextTests;Level5DocumentationExporterTests.CheckedInModeCharacterizationMatchesCurrentAuthoredSources;Level5BasketBallShotMadeTests'
& $Unity -batchmode -projectPath $Project -runTests -testPlatform EditMode `
  -testFilter $Filter -testResults "$Project/Logs/spot-up-7s/edit.xml" `
  -logFile "$Project/Logs/spot-up-7s/edit.log"

& $Unity -batchmode -projectPath $Project -runTests -testPlatform PlayMode `
  -testFilter 'Level5SpotUp7sPlayModeTests;Level5ShotMarkerSessionCompositionPlayModeTests' `
  -testResults "$Project/Logs/spot-up-7s/play.xml" `
  -logFile "$Project/Logs/spot-up-7s/play.log"

& $Unity -batchmode -projectPath $Project -runTests -testPlatform EditMode `
  -testFilter Level5AuthoredMatchDataTests `
  -testResults "$Project/Logs/spot-up-7s/edit-authored-final.xml" `
  -logFile "$Project/Logs/spot-up-7s/edit-authored-final.log"

& $Unity -batchmode -projectPath $Project -runTests -testPlatform PlayMode `
  -testFilter Level5SpotUp7sPlayModeTests.AuthoredCourseCountsMakesAndEndsOnceAfterTheFinalMarker `
  -testResults "$Project/Logs/spot-up-7s/play-course-reviewed.xml" `
  -logFile "$Project/Logs/spot-up-7s/play-course-reviewed.log"
```

| Check | Outcome | Evidence limit |
| --- | --- | --- |
| Repository validation | PASS, exit 0 | Repository invariants |
| Unity compile and exporter | PASS, exit 0 | Existing obsolete-API warnings remain; generated diff is only mode 25's marker cell |
| Focused EditMode selection | 154 distinct tests passed across retained checks and targeted reruns; zero final failures/skips | First selection: 151 passed / 3 failed because new neighboring fixtures used unpadded filenames. Authored-only retry: 10 passed / 3 failed because the new expectations assumed mode-25 clocks and seven-point markers for All. After matching actual authored semantics, final authored fixture: 13 passed / 0 failed / 0 skipped. The other 141 tests retain their original passes. |
| Characterization drift test | PASS in the initial focused selection | `CheckedInModeCharacterizationMatchesCurrentAuthoredSources`; no authored/export change afterward |
| Real-scene PlayMode selection | Three distinct tests passed across retained checks and targeted rerun; zero final failures/skips | Initial run: timeout and binding passed, course failed only on the existing Unknown end cause. Reviewed course-only rerun: one passed. |
| Live course | PASS, six markers × four makes; 24 makes / 168 points; final marker decremented to zero; one ending and one completion; summary displayed | Production attempt/hoop callbacks injected; aiming, physical trigger passage and player input were not executed |
| Live timeout | PASS, unchanged 180-second limit, six unfinished markers, zero makes, TimeExpired and completed result summary | Elapsed time advanced to the boundary; no three-minute wall-clock wait |
| Hosted CI | BLOCKED: Repository Quality is `disabled_manually` | Read from GitHub workflow API; no hosted Unity or repository pass is claimed |
| Manual player/physics certification | NOT RUN | Remains necessary for full authored gameplay certification; five-make intent also requires approval |

The first export attempt failed compilation because a new test used the wrong attempt-counter name. The test now calls the existing `BasketballShotPipeline.ApplyShotAttempt`; the successful export is `export-fixed.log`. The intermediate authored retry used `edit-authored-fixed.xml` / `edit-authored-fixed.log`, with the same authored-only command as the final retry and those output filenames. No production change was made to satisfy these fixture failures.

## Review passes

Correctness review traced authoring → factory → capability/eligibility → resolved rules → real scene filtering → attempts/makes → marker-session decrement → final-marker/timeout routing → results. The quota/copy discrepancy is separate from F1. F3's incomplete-course record behavior is deliberately preserved.

Scope/regression review checked the serialized and generated diffs and neighboring Spot-Up behavior. The production diff is one boolean, and exporter output changes only mode 25's marker cell from None to SevenPoint. No runtime owner, public contract, stable identity, shared marker prefab, scene, Backend, Platform, CPU, or persistence schema changed. Unity's incidental solution-file ordering is excluded.

Runtime status: implemented; full player-input/shot-physics and advertised five-make certification remain outstanding. Automated real-scene integration status is reported separately above.
