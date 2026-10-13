# GP-001 F10 — Arcade and Free Play longest-shot isolation

Audited on 2026-10-12 against fetched `dev` **`81952d68935e4a8397980fb4582265790d6d0e84`**; final fetch returned the same SHA. Pinned Unity: **6000.5.7f1** (`017862109af0`). Status: **Implemented–Not Certified**; focused validation results below.

## Audit and ownership

`git fetch origin dev` and `git rev-parse origin/dev` established the baseline. The checkout was clean and already matched it. Existing worktrees were inventoried. GitHub inspection returned no open PRs; the open/closed issue inventory contained no direct F10 owner. Recent merged PRs #281–#286 were inspected by title/history and affected source: characterization, Spot-Up authoring/eligibility, shared-hoop makes, campaign build integrity, and range-specific records. F10 remained present in current source.

The source trace covered `PlayerData`, `GameRules`, `MatchHudPresenter`, `DBHelper`, `HighScoreModel`, `Pause`, `GameLevelManager`, mode-98/99 menu prefabs, and existing HUD, lifecycle, statistics, progression, result-submission, and pause composition fixtures.

| State | Existing authority and behavior |
| --- | --- |
| Arcade history | `PlayerData.loadStatsFromDatabase` queries `HighScores.longestShot`, mode 98, descending, with the existing hardcore filter. The backing value was loaded but its public property was commented out. |
| Free Play history/projection | The separate mode-99 query loads `LongestShotMadeFreePlay`. During Free Play, a new run record can advance this cache before exit saves history. |
| Current run | The primary participant's `GameStats.Stats.LongestShotMade`; neither historical cache is the run stat. |
| In-play aggregate | The existing shared HUD branch compares the run value to the Free Play projection. Its adapter assigns that value to `AllTimeStats.longestShot`; it does not insert history. This legacy setter can lower an existing aggregate value and is deliberately preserved. |
| Match-end aggregate | `SaveMatchResults` uses `DBConnector.savePlayerAllTimeStats`, with pending persistence fallback and a result ledger. The database aggregation uses `MAX` for longest shot. |

F10's cause was the snapshot carrying only Free Play history, both modes displaying that field, and the shared callback unconditionally changing the Free Play cache. This did **not** establish that Arcade inserted mode-99 history rows.

## Implementation

- `PlayerData.cs`: expose the existing Arcade value through a read-only property; retain the existing query and filters.
- `MatchHudPresenter.cs`: add an optional trailing Arcade snapshot constructor argument, preserving source compatibility for existing callers. Select mode 98 history for Arcade and mode 99 for Free Play/legacy mode 0. Retain current-run rendering, Free Play XP text, one snapshot per invocation, and presentation before persistence. Missing persistence delegates safely skip the unavailable operation.
- `GameRules.cs`: populate both snapshot fields. Bind the existing callback to this rules instance so it can enforce mode ownership. Free Play/mode 0 mutate their cache before the aggregate write; Arcade mutates neither historical cache. A per-match Arcade write watermark preserves improvement-only write timing after removing the accidental Free Play cache mutation. It resets in `Start` and has no durable/history authority. Missing `PlayerData` skips projection; missing SQLite still allows the Free Play cache update.

The aggregate comparison remains separate from the displayed record: a 20-foot Arcade run with Arcade history 35 and Free Play history 12 still writes the aggregate; Arcade history 7 and Free Play history 22 does not cause that write for a 15-foot run. Subsequent unchanged HUD updates do not rewrite the aggregate; the next qualifying improvement does.

No scoring, physics, authored identity, storage schema, Backend V2 contract, experience formula, progression owner, or match-result/exit policy changed.

## Result and exit policy

**Product-policy follow-up, 2026-10-13:** the owner approved abandoning unfinished Arcade runs on Quit, Main Menu, and Restart, with ordinary results saved only at accepted match end. Existing production behavior conforms; no exit-save correction is required. The separate in-play all-time aggregate write remains unchanged. See the [manual exit policy audit](gp-001-arcade-manual-exit-policy-audit.md) for the decision, ownership trace, reviews, and validation boundaries. The original gap description below records the policy ambiguity at the time of F10.

Arcade remains eligible for ordinary mode-98 result persistence when `GameRules.SaveMatchResults` runs. Local SQLite or the existing pending-score fallback must establish durability before the existing Backend V2 queue attempt. Save flags retain idempotency. A missing online session at that moment remains a no-op; no historical result is claimed later.

Free Play remains excluded from ordinary `SaveMatchResults` score insertion. Its pause quit, return-to-menu, and restart conditions still call the existing `GameLevelManager.PersistFreePlayStatsForPause` adapter, which saves the score/history, aggregate stats, and progression using the existing pending fallbacks. Restart retains its reload behavior. Arcade's experience formula still returns zero.

**Separate historical gap:** the authored Arcade display name does not contain `free` and its ID is 98, so it does not enter Free Play's explicit pause exit/restart save condition. An unfinished Arcade session manually exited that way does not create a mode-98 history row. Fixing the HUD does not make such runs durable; changing that policy requires separate work. Legacy mode 0 retains its existing Free Play HUD projection behavior; its absence from selectable prefabs was not used to delete the fallback.

## Focused regressions

`Level5MatchHudPresenterCompositionTests` now covers distinct production snapshot mappings; independent SQLite history and hardcore selection; missing Arcade history without a Free Play fallback; both relative record orderings; Free Play and mode 0 selection; production-adapter cache isolation; the legacy aggregate threshold and exact-value write; repeat suppression and the next improvement; current-run text/clock; no premature history insertion; snapshot consistency; presentation ordering; and missing persistence context.

`Level5LongestShotRecordPlayModeTests` loads authored modes 98 and 99 on Scrapyard against `LocalProfileTestDatabase`. It checks real HUD composition, distinct loaded history, in-play cache isolation/aggregate writes, Arcade's zero XP, Free Play's XP, the first step of the real quit coroutine, Arcade death/result-save idempotency, and Free Play exit persistence/reload. It stops the quit iterator before `Application.Quit`. Persistent queue/session families are preserved/restored through the existing test isolation helper. No real player database is opened.

## Review passes

1. **Correctness:** rechecked query identity, hardcore filtering, missing history, snapshot mapping, cache isolation, exact aggregate threshold/value, save timing and null context. Refined the adapter with a transient Arcade write watermark: simply removing the cache mutation would otherwise repeat the write each HUD frame and could overwrite a later aggregate update. Added regression assertions for unchanged updates and the next improved shot. Corrected test-only SQLite setup after the first run exposed unsupported multi-row `VALUES` and missing EditMode singleton binding.
2. **Architecture/regression:** rechecked that the HUD remains a projection consumer with no direct database dependency; the adapter owns persistence; the watermark is write deduplication rather than a second historical record; the snapshot change preserves source consumers; and ordinary saves, pending queues, Backend V2 eligibility, pause policy and XP formulas remain untouched. Added the real quit-condition assertion to the authored-scene test, rather than testing only its private save callback. Its first run exposed a test assertion that queried the lazily created result ledger before any result save; corrected the assertion to check that the table was absent, then reran successfully. No production change was needed.

## Validation

Artifacts: `C:\Users\Patrick\AppData\Local\Temp\level5-gp001-f10-81952d6`.

| Check | Result |
| --- | --- |
| Repository validation | Exit 0, `Repository validation passed.` |
| Unity compilation | Passed as part of the focused test runs; no compiler errors. |
| Initial EditMode run | 161 total: 156 passed, 5 failed, 0 skipped. Five new fixture-setup failures were corrected; unchanged suites passed. |
| Corrected HUD fixture | 40 passed, 0 failed, 0 skipped. Together with the unchanged first-run suites: 161 distinct selected tests passing. |
| Initial graphics-enabled PlayMode | 0 passed, 1 failed, 0 skipped: test queried the lazy result ledger before any result save; corrected to assert its absence. |
| Corrected graphics-enabled authored-mode PlayMode | 1 passed, 0 failed, 0 skipped; both authored modes exercised in the same integration test. |
| Final diff | `git diff --check` passed; generated solution ordering excluded. |

Current hosted evidence: `gh workflow list --repo sweat-this/level5 --all` reported Repository Quality (`327878811`) as `disabled_manually`. No hosted CI pass is claimed.

Actual commands (launched using Python `subprocess` argument arrays because direct PowerShell process creation was restricted):

```powershell
$repo = 'C:\Users\Patrick\Documents\github\level5'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe'
$out = 'C:\Users\Patrick\AppData\Local\Temp\level5-gp001-f10-81952d6'

# Working directory: $repo
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate-repository.ps1

# First run; original XML retained as EditMode-initial.xml.
& $unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testFilter 'Level5MatchHudPresenterCompositionTests;Level5MatchStatsTests;Level5MatchLifecycleTests;Level5PauseCompositionTests;Level5GameManagerEdgeTests;Level5SpotUpRecordEligibilityTests;Level5BackendV2MatchResultWiringTests;Level5BackendV2MatchResultSubmissionTests;ProgressionServiceApplyMatchResultTests' -testResults "$out\EditMode.xml" -logFile "$out\EditMode.log"

# Rerun only the affected fixture after corrections.
& $unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testFilter 'Level5MatchHudPresenterCompositionTests' -testResults "$out\EditMode.xml" -logFile "$out\EditMode.log"

& $unity -batchmode -projectPath $repo -runTests -testPlatform PlayMode -testFilter 'Level5LongestShotRecordPlayModeTests' -testResults "$out\PlayMode.xml" -logFile "$out\PlayMode.log"
# Same PlayMode command rerun after correcting the lazy-ledger assertion.
# First-run XML/log retained as PlayMode-initial.xml and PlayMode-initial.log.
```

Unity was launched with elevated execution after the shell/network sandbox restrictions were established. No licensing or package error prevented discovery. The PlayMode test injects current-run statistics at the production stats-to-HUD seam; it does not certify input, aiming, physical ball flight, complete player sessions, physical devices, or live Backend submission. Full gameplay certification remains **Implemented–Not Certified**.
