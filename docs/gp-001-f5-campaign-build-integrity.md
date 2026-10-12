# Campaign finale and build scene integrity

GP-001 F5 is audited against fetched `dev` **`bbf548d14bbeddf5c79e418a015d48bcb0b21dab`**, on 2026-10-12. Unity is **6000.5.7f1**, revision **017862109af0**.

The project owner explicitly chose **Dev as the final round**, then confirmed shipping Dev when asked whether that overrides the original request to exclude it. This supersedes the proposed Cocaine Island/replacement choices and authorizes the build-setting change and an exception for exactly `Assets/Scenes/level_23_dev.unity`. The existing 21-round route, hidden arena status, and Pony opponent remain authored in `PlayerData.levelsList`.

## Root cause and route inventory

The final reference in production `Assets/Resources/Prefabs/critical/player_data.prefab` resolves by Unity GUID and component fileID to `LevelSelected` ID 23. Its production naming contract, `LevelObjectName + "_" + LevelDescription`, yields `level_23_dev`. The scene existed but was disabled, while the project validator rejected all enabled scenes with developer names. The prior validator checked only ordinarily selectable levels, so it skipped both hidden campaign entries.

`StartManager` forces Scrapyard, copies the production route into session state, and resets continues. `GameRules.LoadNextCampaignLevel` records the completed index and next list position. `EndRoundMenuManager` decides advance/retry/complete/end-run, continues `ActiveMatch` through `LegacyGameOptionsBridge`, and loads the authored destination directly. Finality depends on route position, not arena ID.

The following inventory was resolved from every serialized GUID/fileID, including validation of the referenced `LevelSelected` script and CPU prefab. Scene basenames come from component fields, not prefab filenames. All destination scene assets exist. Before this correction only index 20 was disabled; afterward all 21 destinations are enabled. Indexes are zero based.

| Index | Level ID | Arena | Scene | CPU prefab suffix |
| --- | --- | --- | --- | --- |
| 0 | 1 | The Scrapyard | `level_01_scrapyard` | `ian` |
| 1 | 3 | Avenue B | `level_05_aveb` | `flash` |
| 2 | 2 | Circle K | `level_02_circlek` | `thom` |
| 3 | 8 | Tammys | `level_08_tammys` | `woody` |
| 4 | 6 | Caffe | `level_06_caffe` | `ak47` |
| 5 | 13 | Rustys | `level_13_rustys` | `jcleger` |
| 6 | 19 | The Back Yard | `level_19_cedar_crest` | `zilla_baby` |
| 7 | 14 | The Dome | `level_14_dome` | `oldreal` |
| 8 | 20 | Jacksonville | `level_20_jacksonville` | `wob1` |
| 9 | 7 | The Sudan | `level_07_sudan` | `raymond` |
| 10 | 21 | Seaside | `level_21_shore` | `wob1` |
| 11 | 9 | Party Mansion | `level_09_party_mansion` | `christy` |
| 12 | 16 | Boner Mountain | `level_16_boner_mountain` | `werewolf` |
| 13 | 5 | The Slab | `level_04_slab` | `rad_tony` |
| 14 | 24 | Boneyard, hidden | `level_24_aveb_cemetary` | `johnny_dracula` |
| 15 | 4 | Norf | `level_03_snow` | `drblood_white` |
| 16 | 10 | Time Jail | `level_10_time_jail` | `executioner` |
| 17 | 11 | Nexus | `level_11_forest` | `wob1` |
| 18 | 22 | Rumble Pit | `level_22_rumble_pit_shooting` | `johnny_dracula` |
| 19 | 15 | Cocaine Island | `level_15_cocaine_island` | `drblood` |
| 20 | 23 | Dev, hidden approved finale | `level_23_dev` | `pony` |

CPU paths share `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_` and `.prefab`. Boneyard and Dev remain excluded from ordinary arena selection. Dev still does not author local multiplayer capability.

## Changes and regression coverage

`EditorBuildSettings.asset` enables the existing final scene. The production route prefab and all scene/prefab metadata, CPU configuration, input, rules, and persistence contracts are unchanged.

`Level5ProjectValidator.CollectCampaignRouteErrors` loads production `player_data.prefab`, inspects its ordered references, and validates every destination independently of ordinary selectability. Diagnostics include index, level identity, source asset, and resolved scene. Missing or empty routes, null references, incomplete naming fields, unlisted/disabled/missing scene assets, ambiguous scene names, and unapproved developer scenes fail validation. `ValidateOrThrow` calls the check, so the menu validation command and `IPreprocessBuildWithReport` enforce the same rule. The developer-scene exception matches only the approved full path; other developer scenes remain rejected. Disabling the final scene again deterministically fails at `LevelsList[20]`.

`Level5CampaignRouteTests` checks actual route ordering, Scrapyard entry, Dev endpoint, hidden Boneyard, CPU references, victory decisions, build membership, and malformed/disabled/unapproved destination failures. `Level5SceneContractTests` retains developer-code isolation and scene composition checks with the approved exception. Dev's shared `DevFunctions` dependency is also present in Scrapyard; the new dependency assertion checks that the finale adds no developer scripts beyond that shipping bootstrap. Its developer input handlers remain guarded by `UNITY_EDITOR || DEVELOPMENT_BUILD`.

`CampaignBoundaryPlayModeTests`, in the existing campaign fixture file, drives real authored Cocaine Island and Dev scenes, production match termination, the end-round UI, and menu return. Scores are injected at existing participant-stat seams. It checks penultimate victory, final completion, tie retry without consuming a continue, CPU-loss retry consuming one continue, exhaustion ending the run, exactly one terminal SQLite aggregate, persistence after reopening SQLite, and a stubbed Backend request observing the already committed aggregate. Existing core outcome cases, authored-data/parity tests, CPU resolution tests, Backend wiring tests, and gameplay lifecycle tests supplement these checks. The unchanged local simultaneous-versus fixture remains available; its full series execution is not established by the focused seam tests.

The runtime fixture uses `LocalProfileTestDatabase`, preserves JSON queue/session files and local identity, and stubs Backend transport. The local runner temporarily isolates the Unity product name as `level5-gp001-f5-test`, restoring the original settings afterward. Existing scene-log suppression is applied to the coroutine scope as well as setup; assertions inspect actual participants, outcomes, rows, and submitted requests. This bounded check does not certify physical input, shooting, animations, or a live Backend.

Changed files are `ProjectSettings/EditorBuildSettings.asset`, `Assets/Level5/Editor/Level5ProjectValidator.cs`, `Assets/Tests/Editor/Level5SceneContractTests.cs`, new `Assets/Tests/Editor/Level5CampaignRouteTests.cs` and its `.meta`, `Assets/Tests/PlayMode/CampaignRoundPlayModeTests.cs`, this report, and its link in `docs/README.md`.

## Validation results

Local evidence is recorded under ignored `TestResults/gp001-f5`:

| Check | Actual result | Evidence |
| --- | --- | --- |
| Repository validation | Passed, exit 0 | `repo-command.txt` and terminal output |
| Normal project validation | Passed, exit 0 | `validate.log` |
| Focused EditMode | 66 passed, 0 failed/skipped | `edit.xml`, `edit.log` |
| Existing campaign/gameplay PlayMode seams | 4 passed, 0 failed/skipped | `seam.xml`, `seam.log` |
| Authored campaign boundary in Editor PlayMode, before final catalog-only fixture correction | 3 passed, 0 failed/skipped | `play-before-catalog.xml`, archived PlayMode log |
| Nondevelopment Windows x64 player | Succeeded, 0 errors, 00:02:52.3068162 | `build.log`, `player/level5-local-versus-cert.exe` |
| Standalone Windows boundary, before final catalog-only fixture correction | 0 passed, 3 failed during fixture composition | `player-before-catalog.xml`, `player.log` |
| Final fixture managed compilation | Passed, exit 0; existing obsolete-API warnings | `managed-compile.log`, `managed-compile-command.txt`, `managed-compile.rsp` |
| Final Editor boundary rerun | BLOCKED before test execution: IL post-processing worker connection/start failure; no new XML | `play.log` |
| Final standalone boundary rerun | BLOCKED by the same compilation prerequisite; not run | No final player result |

The release build uses the existing `LocalVersusCertificationBuild` helper with `BuildOptions.None` and every enabled build scene, including Dev. Its isolated product name changes only the persistence destination. The standalone Unity Test Framework player is a separate development/test build; it must not be described as execution of that nondevelopment release executable.

The first standalone test player failed before gameplay because the fixture reflected `GameModeDefinitionFactory.Create`, a wrapper not retained by player stripping. Inspection of `Library/Bee/artifacts/WinPlayerBuildProgram/ManagedStripped/Assembly-CSharp.dll` with the existing Mono.Cecil package confirmed that method is absent while `LevelDefinitionFactory.Create` and `GameRules.RequestGameOver` remain. The final fixture gets its mode from the existing production `MatchCatalogs.Modes`; it still converts the route's actual level reference with the retained `LevelDefinitionFactory.Create`, as `pressNext` does. Review confirmed that the ordinary level catalog excludes hidden Dev, so it must not be used for this lookup. The fixture checks its destination against the actual route's naming fields. Its Editor rerun stalled with `Connectivity with IL Post Processor runner cannot be established yet` and, after one reset of only the identified worker/trigger, `IL Post Processor runner process failed to start`. Only this task's runner was stopped. No repeated unchanged infrastructure retry was attempted.

The final test assembly was compiled separately with the pinned Unity Roslyn compiler and the existing Editor Bee response file, changing only output paths into the ignored evidence directory. This proves managed compilation, not Unity IL post-processing or runtime execution. Earlier XML was renamed explicitly to avoid presenting stale results as the final rerun.

Commands below use PowerShell from the repository root. The local Python wrapper invoked these exact argument lists and recorded expanded paths in `*-command.txt` files. Test commands ran with the temporary isolated product name described above.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe'
$project = 'C:\Users\Patrick\Documents\github\level5'
$evidence = "$project\TestResults\gp001-f5"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository.ps1

& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\validate.log" -quit -executeMethod Level5ProjectValidator.ValidateFromMenu

& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\edit.log" -runTests -testPlatform EditMode -testFilter 'Level5CampaignRouteTests;Level5CoreTests;Level5AuthoredMatchDataTests;Level5SceneContractTests;Level5BackendV2MatchResultWiringTests;Level5SpawnCoordinatorCampaignCpuPrefabResolverTests' -testResults "$evidence\edit.xml"

& $unity -batchmode -force-d3d11 -projectPath $project -logFile "$evidence\seam.log" -runTests -testPlatform PlayMode -testFilter 'CampaignRoundPlayModeTests;Level5GameplayPlayModeTests' -testResults "$evidence\seam.xml"

& $unity -batchmode -force-d3d11 -projectPath $project -logFile "$evidence\play.log" -runTests -testPlatform PlayMode -testFilter CampaignBoundaryPlayModeTests -testResults "$evidence\play.xml"

$env:LEVEL5_LV_CERT_PLAYER_DIR = "$evidence\player"
& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\build.log" -executeMethod LocalVersusCertificationBuild.BuildWindows64

& $unity -batchmode -force-d3d11 -projectPath $project -logFile "$evidence\player.log" -runTests -testPlatform StandaloneWindows64 -testFilter CampaignBoundaryPlayModeTests -testResults "$evidence\player.xml"

# Existing Editor response file, with only -out/-refout redirected into $evidence:
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Data\netcorerun\netcorerun.exe' 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' -noconfig "@$evidence\managed-compile.rsp"
```

## Review passes

Correctness review confirmed every route reference, the distinct arena IDs/scene numbers, hidden-level handling, finality by list position, continues, the terminal save guard, and local durability before Backend submission. It added rejection of ambiguous scene basenames and incomplete naming fields. A first EditMode run passed 63/64 checks; its new assertion incorrectly prohibited the existing shared guarded `DevFunctions` bootstrap. The corrected assertion compares the finale's developer dependencies with Scrapyard and retains the existing isolation check.

Runtime fixture review corrected access to the local identity properties, entered through the production loading scene, preserved `EndRoundData` and local identity during teardown, and applied scene-log suppression inside Unity's coroutine log scope. Earlier exploratory runs either produced no XML and were stopped, or failed on scene logs before reaching the assertions. They are not counted as passes. The subsequent Editor run passed all three complete boundary tests, including the actual end-round UI, continue transitions, SQLite reopen, downstream stub submission, and menu return. Player review then replaced the stripped mode factory wrapper with the production mode catalog, preserving direct authored-level conversion for hidden destinations. Final managed compilation passed, but runtime verification of that last fixture correction is blocked as described above.

Architecture review keeps route ownership in `PlayerData`, outcome ownership in `CampaignRoundDecision`, and advancement/aggregate saving in `EndRoundMenuManager`. Production runtime code is unchanged. No scene registry, route schema, manager, durable progression implementation, or multiplayer capability change is introduced. The sole policy exception is the explicitly approved finale path.

## Coordination and remaining evidence

The initial checkout was clean and HEAD, `origin/dev`, and FETCH_HEAD matched the audited SHA. There were no open PRs. Recent merges #281–284 contain characterization and F1/F3/F4 repairs, with no F5 correction. Campaign issue search and #270 comments supplied no approved endpoint; the owner decision above resolves that ambiguity. Durable progression #270–272 remains separate. Hosted Repository Quality workflow `327878811` is still `disabled_manually`; this work does not claim hosted CI coverage.

The fixture's scene execution emitted `PlayerDunk` unassigned-basketball, `PlayerAnimationEvents` missing-controller, and `BasketBallAuto.Start` null-reference errors, alongside HUD fallback warnings. The bounded assertions use the repository's existing scene-log suppression and establish campaign boundary behavior, not clean scene startup or physical gameplay. Reproduce these logs through ordinary launch before deciding any separate scene/composition repair. Physical input, shooting/animation quality, a complete played 21-round campaign, live Backend delivery, and nondevelopment release-executable interaction remain outside the evidence established here.

**Status: implemented, not fully certified.** All 21 authored destinations are enabled and existing, the deterministic regression check passes, and the nondevelopment player builds. The final fixture needs its three Editor cases and three standalone cases rerun after the Unity compilation worker is available. Publication should remain a draft until that runtime gap is resolved; hosted CI is disabled and cannot supply the missing proof.
