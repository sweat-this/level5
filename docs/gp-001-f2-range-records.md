# GP-001 F2: range-specific local shot records

Audited fetched `dev` at **`abf9b362cb141ba49b2d4d88a904f5dea3f1219c`** on 2026-10-12.
Pinned editor: **Unity 6000.5.7f1 (017862109af0)**. Implementation branch: `fix/gp001-f2-range-records`.
There were no open PRs at the overlap audit. Recent merged GP-001 work covered F1/F3/F4/F5;
no open issue identified an overlapping F2 implementation.

## Correction and ownership

`PlayerData.loadStatsFromDatabase` selected aggregate `maxShotMade` for modes 2, 3, and 4.
Their production selection prefabs also authored `highScoreField: maxShotMade`, which
`StatsManager.getModeSelectDataList` carries into the local SQLite leaderboard.
Both consumers now select the existing independent columns:

| Stable mode ID | Mode | SQLite field |
| --- | --- | --- |
| 2 | Total 3 Pointers | `threeMade` |
| 3 | Total 4 Pointers | `fourMade` |
| 4 | Total 7 Pointers | `sevenMade` |

The production-consumer regression exposed a second, pre-existing blocker in this required flow:
`StatsManager.changeHighScoreDataDisplay` pre-acquired `DBHelper.DatabaseLocked`, causing the
row and count methods to return immediately. Removing that caller-owned lock lets DBHelper
retain ownership and release it in its existing `finally` blocks. Query predicates are unchanged.

`DBConnector` already creates nullable INTEGER range columns, and `InsertGameScore` persists
`HighScoreModel.ThreeMade`, `FourMade`, and `SevenMade` independently. No schema or migration
changed. The DBHelper row projection now renders an unknown qualifying count as an empty score.
The row remains in the existing SQL result and pagination count. Descending SQLite ordering puts
nulls below known nonnegative counts; equal known scores retain `time ASC` ordering.

The personal-best reader already handles SQL nulls and continues to return zero when no known
record exists. Historical valid range values work directly. Unknown values cannot be recovered
from aggregate makes and remain unknown; no backfill is attempted.

`MatchStats` remains the counter authority. `HighScoreModel.MaxShotMade` and Backend V2
`ShotsMade` remain aggregate metrics. `MatchHudPresenter` already uses the correct current
range counters and PlayerData snapshot properties, so its production implementation is unchanged.
Mode IDs, descriptions, clocks, scoring, filters, GUIDs, and other serialized fields are unchanged.

## Changed files

- `Assets/Scripts/PlayerData.cs`: select the three qualifying personal-best columns.
- `Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_0{2,3,4}.prefab`:
  change only `highScoreField`.
- `Assets/Scripts/database/DBHelper.cs`: guard historical null qualifying counts in row projection.
- `Assets/Scripts/menu_stats/StatsManager.cs`: remove the lock that prevented actual refresh queries.
- `Assets/Tests/Editor/Level5RangeShotRecordTests.cs` and its new `.meta`: 22 cases exercising
  authored definitions, actual SQLite ordering, time ties, zero/null history, Hardcore/filter
  semantics, PlayerData loading, real StatsManager selection/refresh and TMP binding, durable
  independent counters, all-time sentinels, aggregate online mapping, and unrelated ranking fields.
- `Assets/Tests/PlayMode/Level5RangeShotRecordPlayModeTests.cs` and its new `.meta`: production
  Scrapyard HUD checks for all three authored modes, using disposable SQLite and the existing
  queue/session file isolation. Runtime result remains pending as described below.
- `docs/generated/level5-authored-game-data.json`: three exporter-produced field replacements.
- This report: audit, review, commands, evidence, and outstanding validation.

## Generated data

`Level5DocumentationExporter.ExportFromCommandLine` completed successfully in the pinned Unity
editor. Its full output also exposed inherited snapshot drift: 23 multiplayer capability fields,
the Spot-Up 7s marker flag, and three scene entries. These unrelated hunks were rejected.
An automated patch selection retained only the three unchanged `highScoreField` hunks produced
by Unity. No JSON values were hand-authored. The full output and scoped patch are retained with
local evidence. The characterization matrix and its schema were not changed.

## Validation

Local evidence is retained under `TestResults/gp001-f2` (original runner output path:
`artifacts~/gp001-f2`). No real player SQLite file was opened or altered.

| Check | Result |
| --- | --- |
| `scripts/validate-repository.ps1` | Passed, exit 0, including final test assets |
| Pinned Unity compilation and exporter | Passed; `export-final.log`, successful exit 0 |
| Initial affected EditMode inventory | 89 passed, 16 failed; failures were in the new fixture |
| Repaired focused EditMode run | 32/32 passed; `edit-range-final.xml` |
| Final affected EditMode run | 107/107 passed, 0 skipped; `edit-final.xml` |
| Existing related PlayMode suite | 3/3 passed; `play.xml` |
| Authored range-mode arena HUD check | BLOCKED: no result XML; see below |

The initial test fixes removed an NUnit attribute unsupported by the pinned test framework,
matched the exact existing EditMode deferred-Destroy diagnostic, preserved the authored
`MakeCount` objective, and used the existing zero-based SQL page index. The final fixture also
checks filtered StatsManager refresh and real ordering for unaffected modes.

The arena fixture's earlier diagnostic runs reached disposable schema creation, score seeding,
and production PlayerData loading, then produced no test XML. Review found and corrected an
ambiguous reflection call to the overloaded `LevelDefinitionFactory.Create`; the fixture now
selects the `LevelSelected` overload explicitly. Subsequent graphics and headless diagnostics
stalled before fixture progress, so the underlying runtime blockage remains unconfirmed.
`[remote] error: Init socket failed` is present in the graphics log but is not established as the
cause. These runs provide compilation evidence, not a passed HUD scenario.

The earlier queue-isolating run had an external crash-recovery snapshot, restored and verified
after stopping the identified test process. The final diagnostics temporarily used a unique
Unity product name (`level5-gp001-f2-validation`) so the whole persistent-data directory was
isolated; ProjectSettings was restored afterward. Unrelated Unity processes were left alone.
The fixture uses one production-authored arena and shooter rather than loading every selection
asset. The PR must remain a draft until the arena fixture completes successfully in a functioning
Unity environment.

An earlier PlayMode selection ran only the three existing tests because the new test's GUID was
malformed. The GUID was corrected; those three passes are not counted as range-mode HUD proof.
GitHub's workflow API currently reports `Repository quality` as `disabled_manually`. No hosted
repository or Unity coverage is assumed; the repository-variable listing also did not establish
an enabled Unity capability. The local repository validator result remains valid independently.

Exact runner commands (PowerShell, `$project` is the Level 5 checkout and `$evidence` is the
original evidence directory; processes were launched hidden and inspected separately):

```powershell
$project = 'C:\Users\Patrick\Documents\github\level5'
$evidence = Join-Path $project 'artifacts~/gp001-f2'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository.ps1
& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\export-final.log" -quit -executeMethod Level5DocumentationExporter.ExportFromCommandLine
& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\edit-final.log" -runTests -testPlatform EditMode -testFilter 'Level5RangeShotRecordTests;Level5AuthoredMatchDataTests;Level5DocumentationExporterTests;Level5MatchStatsTests;Level5MatchHudPresenterCompositionTests;StatsTextMeshProMigrationTests;Level5BackendV2MatchResultAdapterTests;MenuActionWrapperExceptionTests' -testResults "$evidence\edit-final.xml"
& $unity -batchmode -force-d3d11 -projectPath $project -logFile "$evidence\play.log" -runTests -testPlatform PlayMode -testFilter 'Level5RangeShotRecordPlayModeTests;Level5GameplayPlayModeTests' -testResults "$evidence\play.xml"
& $unity -batchmode -force-d3d11 -projectPath $project -logFile "$evidence\play-range-bounded.log" -runTests -testPlatform PlayMode -testFilter Level5RangeShotRecordPlayModeTests -testResults "$evidence\play-range-bounded.xml"
& $unity -batchmode -force-d3d11 -job-worker-count 4 -projectPath $project -logFile "$evidence\play-range-overload.log" -runTests -testPlatform PlayMode -testFilter Level5RangeShotRecordPlayModeTests -testResults "$evidence\play-range-overload.xml"
& $unity -batchmode -nographics -projectPath $project -logFile "$evidence\play-range-headless.log" -runTests -testPlatform PlayMode -testFilter Level5RangeShotRecordPlayModeTests -testResults "$evidence\play-range-headless.xml"
```

## Review passes

**Correctness:** traced both consumers through production mode selection, durable independent
counts, SQL projection/order, filters, null history, and TMP row binding. The actual StatsManager
regression exposed and drove correction of its inherited lock conflict. Qualifying counters and
aggregate metrics remain separate.

**Architecture and regressions:** verified that no new ranking owner, persistence layer, schema,
public metric, or scoring rule was introduced. Reviewed one-field prefab diffs and scoped
generated-data hunks. Strengthened unaffected-mode SQL and filtered-refresh coverage. Runtime
test review moved singleton snapshots before fallible setup and added the existing retry-queue
and session isolation for unexpected scene-end paths. Runtime HUD evidence remains required.

## Separate online follow-up

Backend `dev` was independently verified at **`9850828e003bbf78c27d3f17d51730300cbfef43`** using
`git ls-remote`. Its
[`StaticLeaderboardPolicyCatalog.cs`](https://github.com/sweat-this/Level5Backend/blob/9850828e003bbf78c27d3f17d51730300cbfef43/v2/src/Level5.Infrastructure/Leaderboards/StaticLeaderboardPolicyCatalog.cs#L35)
ranks modes 2, 3, and 4 using `MatchResultMetric.ShotsMade`. The unchanged Unity adapter supplies
`HighScoreModel.MaxShotMade` for that metric. Online range-specific ranking therefore needs a
separate Backend-owned policy/contract decision. Neither Backend nor Platform was modified.
