# Level 5 Arcade manual exit persistence policy audit

Audited 2026-10-13 against fetched `origin/dev` **`4693d184fcaef455cbfd60c1af17822e149a2994`** in `sweat-this/level5`. HEAD matched that SHA and the initial working tree was clean. Unity remains **6000.5.7f1**, revision `017862109af0`.

**Approved policy A; no production correction required.** On 2026-10-13 the product owner selected: "A — Abandon unfinished runs; save ordinary results only at accepted match end." Arcade Quit, Main Menu, and Restart already create no mode-98 partial result, so current behavior conforms to this policy. No production code, tests, assets, serialized contracts, schemas, or Backend contracts changed.

## Approved product policy

The [original characterization](gp-001-single-player-characterization.md), [F10 report](gp-001-f10-longest-shot-isolation.md), [PR #287](https://github.com/sweat-this/level5/pull/287), and [post-remediation audit](gp-001-post-remediation-certification-2026-10-12.md) previously left unfinished Arcade persistence unspecified. The product owner's explicit selection in this task resolves that ambiguity; existing tests alone did not establish the policy.

The approved requirements for ordinary Arcade mode 98 are:

- Quit, Main Menu, and Restart abandon unfinished runs without creating an ordinary score/history row or partial Backend submission.
- Accepted match ends retain the existing ordinary result eligibility, local-first durability, retries, and authenticated Backend eligibility.
- Arcade continues to award zero XP, and mode-98/mode-99 histories remain independent.
- Restart retains the existing match-session ID rotation and scene loading behavior.

The existing in-play all-time longest-shot write remains unchanged. Abandonment here concerns unfinished run results; approval of A does not authorize rolling back already-written aggregate statistics. Any future requirement that every aggregate change disappear on abandonment needs separate scope and approval.

Partial-result saving (option B), new incomplete-result formats, and altered ranking/submission contracts are outside the approved scope. The product-policy gate is resolved, and no behavior-changing implementation is necessary.

## Repository and issue inventory

`git fetch origin dev` succeeded; `git rev-parse HEAD origin/dev` returned the audited SHA twice. Existing worktrees were inventoried and left untouched. GitHub returned **zero open PRs** during the audit. The open/closed issue inventory and targeted Arcade/manual-exit searches revealed no prior issue establishing this policy; the Arcade issue search returned only unrelated CPU initialization issue #71. Policy authority is now the product owner's explicit selection recorded above.

| Relevant work | Current significance |
| --- | --- |
| [#287](https://github.com/sweat-this/level5/pull/287), merged as `dee29c6c36e21a77d26f267fb9ee124a2e0baac5` | Isolated mode-98/mode-99 longest-shot history; explicitly retained manual-exit behavior. No PR comments or reviews supplied a subsequent policy decision. |
| [#288](https://github.com/sweat-this/level5/pull/288) | Later `GameRules` change freezes accepted end time and presents it before retries; does not add Arcade exit persistence. |
| [#289](https://github.com/sweat-this/level5/pull/289) through [#292](https://github.com/sweat-this/level5/pull/292) | Instruction corrections and post-remediation evidence; retained the ambiguity. |
| [#293](https://github.com/sweat-this/level5/pull/293) | Windows/IL2CPP campaign queue deserialization correction; no Arcade exit policy change. |
| [#294](https://github.com/sweat-this/level5/pull/294), [#295](https://github.com/sweat-this/level5/pull/295), [#296](https://github.com/sweat-this/level5/pull/296) | CPU startup, Spot-Up instructions, and fallback HUD visibility corrections; no resolution of this finding. |
| [#185](https://github.com/sweat-this/level5/issues/185), closed | Existing exclusion of active versus/correspondence attempts from general Backend results; remains a separate contract. |
| [#232](https://github.com/sweat-this/level5/issues/232), [#233](https://github.com/sweat-this/level5/issues/233) | Existing hosted validation work; no hosted pass is claimed here. |

Comparing the relevant persistence surfaces from #287 through current dev showed only the documented `GameRules` end-time/presentation correction. `Pause`, its `GameLevelManager` persistence adapter, database persistence, and `BackendV2MatchResultSubmission` had no later change adding Arcade manual-exit saving. Current source conforms to the newly approved abandon policy.

## Runtime ownership and data flow

| Responsibility | Existing owner and current behavior |
| --- | --- |
| Navigation and pause UI | [`Pause`](../Assets/Scripts/game%20manager/Level5Match/Pause.cs), with `SceneTransition` for scene loading/time restoration. Quit and Main Menu call `TryLeaveMatch`; Restart checks outstanding competitive attempts before proceeding. |
| Explicit-exit gate | `Pause.TryLeaveMatch` delegates to `VersusQuitPolicy.TryPrepareForExplicitExit`. Ordinary Arcade has no active versus attempt, so this returns true. It does not establish ordinary Arcade score durability. |
| Free Play persistence composition | [`GameLevelManager.PersistFreePlayStatsForPause`](../Assets/Scripts/game%20manager/GameLevelManager.cs) captures elapsed time through `GameRules`, converts the roster, saves/queues score and aggregate, then applies progression. `Pause` calls it only when the database connector exists and mode name contains `free` or ID equals 99. |
| End acceptance and result eligibility | [`GameRules`](../Assets/Scripts/game%20manager/GameRules.cs) requests an end through [`MatchController`](../Assets/Scripts/game%20manager/Level5Match/MatchController.cs) and [`MatchLifecycle`](../Assets/Level5/Core/Match/MatchLifecycle.cs). The first accepted reason wins. `CaptureMatchEndState` freezes elapsed time and eligibility. Arcade is an ordinary eligible mode; Spot-Up completion restrictions are separate. |
| Conversion into a durable score | [`HighScoreModel.convertBasketBallStatsToModel`](../Assets/Scripts/models/HighScoreModel.cs) snapshots current mode/arena/character and primary stats. Every conversion generates a fresh GUID `Scoreid`. |
| Match aggregate/progression identity | [`MatchSession`](../Assets/Scripts/game%20manager/Level5Match/MatchSession.cs) owns a separate `match-<GUID>` ID. `Pause` and `GameRules` capture it. `ActiveMatch.Begin` rotates it on launch; Restart rotates it before scene load. It is not the score GUID and cannot be passed unchanged as Backend `ClientResultId`. |
| Normal-end durable work | `GameRules.HandleMatchEnded` owns presentation, score conversion, `SaveMatchResults`, progression, campaign transition, and competitive reporters. Guards prevent reentry/completed-work repetition. Incomplete durable work remains Ending and is retried. |
| SQLite and local retries | [`DBConnector`](../Assets/Scripts/database/DBConnector.cs) delegates to `DBHelper`. Scores deduplicate by `scoreidUnique`; aggregate totals use transactional `MatchPersistenceLedger`. [`PendingMatchPersistenceStore`](../Assets/Scripts/database/PendingMatchPersistenceStore.cs) atomically queues scores by score GUID and aggregates by match result ID, then repairs through the same database adapters. |
| Backend queue | [`BackendV2MatchResultSubmission.TryQueue`](../Assets/Scripts/backendv2/BackendV2MatchResultSubmission.cs) runs from the normal-end score branch only after SQLite or pending-score durability. It requires a current authenticated session, excludes active competitive attempts, and uses the score GUID as `ClientResultId`. Queue ownership is the authenticated Backend player, independent of local profile scope. No later sign-in claims an earlier offline result. |
| Progression | `GameRules` calls `ProgressionService` at normal end, with existing SQLite result-ledger and account-scoped pending fallback. `MatchExperience.Calculate` returns zero for Arcade. Zero XP does not imply no ledger/pending work. |

Normal Arcade death follows health callback/poll fallback → accepted lifecycle end → `GameRules.HandleMatchEnded` → score conversion → SQLite or pending-score queue → optional Backend queue. Aggregate and progression completion join the existing retry flow. Ordinary Free Play score insertion is excluded from `SaveMatchResults` and instead uses its explicit pause adapter.

Arcade Quit and Main Menu pass the competitive exit gate, skip the Free Play save condition, and navigate. Quit waits for database unlock before `Application.Quit`; Main Menu uses the loading scene. Restart skips saving, reloads player data where available, restores pause state, rotates the session ID, and reloads the gameplay scene. A player-data reload exception returns before ID rotation/navigation. Restart has no new durability wait. Repeated Arcade exit actions do not reach a partial-score save path.

## Mode history and aggregate statistics

[`PlayerData.loadStatsFromDatabase`](../Assets/Scripts/PlayerData.cs) loads separate `HighScores.longestShot` maxima for modes 98 and 99 with the current hardcore filter. `MatchHudPresenter` displays the corresponding mode history. The current run statistic is `GameStats.Stats.LongestShotMade`.

The shared HUD path still compares a run improvement against the Free Play projection threshold. The `GameRules` adapter can assign `AllTimeStats.longestShot` during Arcade play, while leaving both historical caches and score rows untouched. Its transient Arcade watermark suppresses repeat writes; it is not a durable historical result. The legacy exact-value aggregate setter can lower an existing aggregate and is preserved by F10. Normal-end aggregate application instead uses the established ledger and maximum semantics.

Consequently, an unfinished Arcade run can leave an all-time longest-shot change without producing a mode-98 history row, Backend result, or XP award. Calling that aggregate change an Arcade history save would be incorrect.

## Correctness review and first refinement

Completed against the no-change disposition and the conditional implementation plan, then reconciled with the approved policy A; there is no production patch to certify.

1. **Duplicate saves and identity:** rejected copying or broadening the Free Play adapter. It reconverts on every invocation, creating new score GUIDs. Session/aggregate idempotency does not deduplicate independently generated score rows. If B is approved, the existing normal-end authority must own a single logical result, including an exit racing normal death and repeated actions.
2. **Backend/local divergence:** Free Play's adapter does not call the normal-end Backend queue. Reusing it would silently select different submission semantics. The existing general adapter sends six generic metrics and does not send `LongestShot`; normal mode-98 enqueue eligibility therefore does not establish remote longest-shot ranking correctness. Partial-result Backend/ranking eligibility must be decided before choosing a path. Server ranking behavior was not audited or changed here.
3. **Failure handling:** normal-end SQLite refusal can fall back to durable pending score/aggregate queues; both failing leaves retry work incomplete. In contrast, the Free Play adapter ignores fallback return values, its caller skips it entirely when the connector is absent, and the pause database wait can time out and continue navigation. None supplies a new Arcade durability guarantee.
4. **Score, history, XP:** retained mode 98/99 filtering and the existing aggregate projection. Arcade's zero-XP calculation remains unchanged. Abandoning history must not be described as rolling back every aggregate write.

First refinement: retain the current save owners and distinguish score GUIDs from session IDs. Approval of A resolves the gate without exposing partial results to the identity, submission, or failure risks above. No partial-result implementation is proposed.

## Architecture and regression review and final refinement

1. **Ownership and contracts:** `Pause` remains navigation/composition; it must not independently reproduce `GameRules` normal-end persistence. No additional manager, service, save schema, incomplete-result DTO, or generic framework is justified.
2. **Lifecycle and scene ordering:** preserve first-wins end acceptance and frozen end time. The ordinary exit gate currently does not protect unfinished normal-end retries; any approved saving change needs a concrete decision about refusing navigation when neither SQLite nor fallback is durable. Restart must save any authorized old result before rotating identity and loading the next scene.
3. **Compatibility and scope:** retain public methods, serialized fields/GUIDs, mode-0 compatibility, Free Play behavior, competitive exclusions, and all other modes. Do not repair unrelated aggregate setter behavior or Free Play durability in this task.
4. **Test isolation:** the Pause fixture protects only its primary pending JSON itself, so run it in a disposable product namespace. The authored PlayMode fixture additionally uses disposable SQLite and `CampaignBoundaryPersistenceIsolation`, including primary/`.bak`/`.tmp` session, match queues, and account-74 pending progression. These boundaries do not certify physical gameplay or live Backend delivery.
5. **Requirements:** the product owner explicitly selected A. Existing source conforms for all three exit actions; current controlled runtime tests support the Quit/death/history distinction. Option B remains outside scope.

Final refinement: deliver the approved no-change policy record with existing baseline validation. No new persistence or test implementation is required to correct behavior. Keep the validation boundaries below explicit; no speculative production change remains in the plan.

## Completed plan and deferred work

1. Recorded the owner's policy A for all three exits and the distinction between unfinished results and the existing all-time aggregate projection.
2. Confirmed that current `Pause` conditions omit Arcade persistence, `GameRules` retains accepted-end result authority, and Restart retains identity rotation. No production correction is required.
3. Completed correctness and architecture/regression reviews; retained existing owners and contracts and removed partial saving from the implementation scope.
4. Retained the executed validator, 103 focused EditMode cases, and one authored Arcade/Free Play PlayMode case. The policy-record update changes only documentation, so these results remain applicable without rerunning Unity.
5. Deferred additional Arcade-specific Menu/Restart/racing-exit and fault-injection tests, physical sessions, and live Backend certification. Existing source/composition/shared tests provide narrower evidence; this no-change completion does not claim the full runtime matrix was executed. New test implementation is unnecessary for the accepted no-production-change outcome.

Future partial-result saving, aggregate rollback, remote longest-shot ranking, and ordinary match-end navigation during failed durability require separate authorized work. They are not conditions for matching the approved unfinished-run policy.

## Validation evidence

Local artifacts: `Logs/gp001-arcade-exit-audit/`. The ignored `run_validation.py` retains argument arrays in `*-command.json`, process exit codes, Unity logs, and NUnit XML. It changes only the product name during each Unity run to `level5-gp001-arcade-exit-audit-20261013`, then restores `ProjectSettings.asset` byte for byte. The original SHA-256 is `f567cf07efd0819f869978b7d3054535b87f6b9f286b98fd6ffa79e75bdfc91a`. Player saves, SQLite, sessions, and retry files use disposable paths. The authored test also restores its fixture-scoped persistence families.

| Check | Current execution result |
| --- | --- |
| Repository validator | PASS, process exit 0, `Repository validation passed.` |
| Focused EditMode | PASS, 103 passed, 0 failed, 0 skipped; process exit 0. Pause composition, lifecycle, Backend wiring/submission, core identity, and Spot-Up eligibility/shared persistence fixtures. |
| Graphics-enabled authored Arcade/Free Play PlayMode | PASS, 1 passed, 0 failed, 0 skipped; process exit 0. Both authored modes exercised, including controlled Arcade death, repeated end handling, actual persisted row counts, the first Quit step, independent history reload, and XP distinction. |
| Additional Arcade exit/failure matrix | NOT RUN; no behavior change requires new tests. Existing PlayMode covers the first real Quit iterator step and controlled Arcade death; Menu/Restart behavior is established by source inspection, with narrower composition/shared forwarding/fallback coverage. No full Arcade runtime fault matrix is claimed. |
| Physical input, full Quit process, played Menu/Restart sessions, real disk exhaustion/crash, live Backend | NOT RUN; no such certification claim. |
| Production changes | None. Documentation only. |
| Final diff | `git diff --check` passed. Unity-generated solution ordering reverted; `ProjectSettings.asset` restored after each run. |

Commands from repository root:

```text
git fetch origin dev
git rev-parse HEAD origin/dev
python Logs/gp001-arcade-exit-audit/run_validation.py validator
python Logs/gp001-arcade-exit-audit/run_validation.py edit
python Logs/gp001-arcade-exit-audit/run_validation.py play
```

The wrapper runs `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository.ps1`. Its EditMode Unity arguments are `-batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode -testFilter Level5PauseCompositionTests;Level5MatchLifecycleTests;Level5BackendV2MatchResultWiringTests;Level5BackendV2MatchResultSubmissionTests;Level5CoreTests;Level5SpotUpRecordEligibilityTests -testResults <out>/edit.xml -logFile <out>/edit.log`. PlayMode uses `-batchmode` with graphics enabled, `-testPlatform PlayMode`, and filter `Level5LongestShotRecordPlayModeTests`; output names are `play.xml` and `play.log`. Exact quoted argument arrays are in the retained JSON files.

The authored PlayMode test injects statistics at the production stats-to-HUD seam and invokes a controlled death callback. It stops Quit before `Application.Quit`. Passing it establishes those bounded persistence assertions, not physical shooting, complete navigation sessions, or live networking. Shared Spot-Up failure tests establish the common save/queue machinery, not an Arcade-specific fault-injection matrix.

## Delivery scope and acceptance

Changed files are this audit, its link in `docs/README.md`, and a policy follow-up in the F10 report. No production behavior or test expectations were modified. Publication is limited to these documentation changes; no issue or external comment was created.

- Latest dev verified; relevant current source and issue/PR inventory inspected.
- Existing authorities, public/serialized contracts, mode-history isolation, and zero-XP calculation preserved.
- Both review passes and the final no-change plan completed.
- Product policy A explicitly established; current ordinary Arcade exit/result behavior conforms by source inspection and bounded executed regression evidence.
- Complete Arcade-specific runtime exit/failure certification remains **NOT RUN**; source conformance and baseline test results do not imply physical or exhaustive runtime certification.
- No claim of newly implemented partial saving, complete failure certification, physical gameplay acceptance, or hosted CI success.

The policy decision is resolved. Preserving current production behavior satisfies the approved abandon policy, so this task requires documentation only.
