# GP-001 — Battle Royal victory and completion semantics

Audit date: 2026-10-13. Repository: `sweat-this/level5`. Target: `dev`.

**Approved policy A:** the owner selected "A — Endless survival; align metadata to Survival" on 2026-10-13, then explicitly requested an option to quit and save a long-running score. Mode 21 now resolves `Survival`. Existing spawning/combat/death behavior remains, and the pause menu offers **Save & Main Menu** and **Save & Quit** through the existing accepted-end and durable-result machinery. This report retains the pre-edit audit below and records the resulting scoped implementation.

## Baseline and existing work

`git fetch origin dev` succeeded. HEAD and `origin/dev` both resolve to **`e86d92a0d6562ded301a065cfe8a919a429002b5`**, matching the supplied historical baseline. The initial `dev` checkout was clean. Eight additional registered worktrees exist, including unrelated local-profile/input work; no registered branch identifies overlapping Battle Royal semantics work. No open PRs were returned for this repository.

Relevant merged work:

| Work | Status and relevance |
| --- | --- |
| [#45](https://github.com/sweat-this/level5/pull/45) | Introduced the validated configuration/domain and objective mapping. Its body preserves combat behavior and discusses combined Cage/BattleRoyal flags; no finite competition requirements or subsequent comments/reviews establish a policy. |
| [#281](https://github.com/sweat-this/level5/pull/281) | Initial GP-001 characterization explicitly identifies missing finite-winner requirements. |
| [#288](https://github.com/sweat-this/level5/pull/288) | Cage summary, frozen accepted-end duration and pooled enemy startup repair. Inspected body/comments/reviews; no mode-21 product-policy approval. |
| [#292](https://github.com/sweat-this/level5/pull/292) | Bounded mode-21 staging/death probe and current certification report. Inspected body/comments/reviews; no finite-victory specification. |
| [#297](https://github.com/sweat-this/level5/pull/297) | Latest merged work, approving Arcade manual-exit policy only. It does not decide Battle Royal semantics. |

Repository issue searches for `royal` and `GP-001` returned no matching issues. The recent all-state issue inventory supplies no Battle Royal policy owner. Progression issues [#270](https://github.com/sweat-this/level5/issues/270), [#271](https://github.com/sweat-this/level5/issues/271), and [#272](https://github.com/sweat-this/level5/issues/272) remain separate work. Live Repository Quality workflow `327878811` is `disabled_manually`; hosted CI is not claimed.

The [original characterization](gp-001-single-player-characterization.md) and [post-remediation report](gp-001-post-remediation-certification-2026-10-12.md) describe the previously missing decision. The older overhaul plan lists last-player-standing as a possible future policy and enum value, without a contestant/admission/elimination specification. The owner's explicit selection resolves this gate; none of the historical observations were rewritten.

## Current-state evidence and mode comparison

This table records the pre-edit baseline. Line references refer to the audited SHA. The implementation changes mode 21's objective to Survival and adds the explicit saved-stop path described below.

| Dimension / owner | Mode 21 — Battle Royal | Mode 22 — Cage Match |
| --- | --- | --- |
| Authored source | `Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_21.prefab:47` | Neighboring `mode_selected_22.prefab:47` |
| Stable ID / name / description | 21 / Battle Royal / `Survive` | 22 / Cage Match / `Survive` |
| Factory objective | `GameModeDefinitionFactory.cs:237`: `LastPlayerStanding` | `GameModeDefinitionFactory.cs:240`: `Survival` |
| Clock | CountUp; no custom duration | Same |
| Combat capabilities | `CombatMode.BattleRoyal` | `CombatMode.Cage | CombatMode.BattleRoyal` |
| Other authored rules | Enemies-only; no basketball objective; CPU shooters disallowed; `RequiresPlayerSurvive=false`; score field `enemiesKilled` | Same |
| Arena compatibility | Combat + BattleRoyal: Rumble Pit (17), Crank Zone (18) | Combat + Cage + BattleRoyal: Rumble Pit (17) |
| Roster bounds | 1–4; compatibility requires fighters | Same; not a finite enemy contestant roster |
| Spawner branch | Contestant admission immediately, then every 10 seconds, subject to concurrent population cap | Default minion/boss spawning; replenishment check starts after 5 seconds, then every 2 seconds |
| Population policy | Initial rule calculation is 2 for configured normal non-Cage play; spawner overrides standing cap to 20 before repeated admissions | Normal cap 4; hardcore enemies-only cap 8; mobile halves standing cap. Shared attack reservation cap remains 20 |
| Ordinary end | Primary player death | Primary player death |
| Live score / end presentation | Kills / kills, finalized survival time and XP | Same shared formatter; special idle-death message retains precedence |
| Ordinary local result | Existing mode-21 row with `enemiesKilled` and elapsed `time` | Separate mode-22 row through the same persistence machinery |

The false authored `RequiresPlayerSurvive` value is preserved. It is an existing rule dimension, distinct from objective metadata and the universal player-death event. Setting it to true is unnecessary for policy A.

### Configuration, objective and generated ownership

`GameModeDefinitionFactory.Convert` reads each authored selection prefab and independently resolves clock, combat flags, markers and objective. `MatchConfigurationBuilder.Resolve:150` copies `mode.Objective` and the other dimensions into immutable `ResolvedMatchRules`. `CombatMode` is a flag set because Cage consumes both flags. `MatchObjective` describes the goal; `MatchEndCause` describes an accepted ending. These contracts must remain distinct.

A repository-wide `Assets`/`Packages` search found **no production branch consuming `MatchObjective.LastPlayerStanding` to detect victory**. Occurrences are the enum declaration, factory mappings, and tests of the separate end-cause enum. Generic objective carriers are `GameModeDefinition`, the builder, `ResolvedMatchRules`, and editor migration/export. No serialized `objective:` entries were found in production assets/prefabs/scenes. The runtime catalog reads the selection prefabs through the factory rather than switching to generated definition assets.

`InferObjective:263` also returns `LastPlayerStanding` for an unlisted mode with the BattleRoyal capability. That fallback is a separate contract for future/unlisted IDs, not an additional mode-21 consumer. It remains unchanged; changing all inferred BattleRoyal modes would expand the approved scope.

`MatchDefinitionMigration.ExportCharacterizationFromCommandLine` owns `docs/generated/level5-game-mode-characterization.md`. Baseline generated parity passed despite the product-semantic discrepancy. The approved correction regenerates that matrix through the exporter, changing only mode 21's objective cell to Survival. The raw authored-data JSON needs no change because prefab bytes remain unchanged.

### Spawning, death and pooling

`EnemySpawner.Start:78–100` selects branches using combat flags, not objective. `spawnBattleRoyalContestant:211` refreshes registry counts, refuses an admission at the concurrent cap, and chooses a boss when none is registered in normal mode or a minion otherwise; hardcore admissions choose bosses. No finite admitted-total counter, closed-admission state, tournament roster or winner detector exists in this path. Admission can resume after population falls below the cap.

`EnemyPopulationRules` owns separate standing-population and attack-reservation limits. **20 is a concurrent cap, never a total contestant count or a victory threshold.** Existing main-thread count checks are not proof of a finite competition or arbitrary multithreaded race safety.

`EnemyController.OnEnable:171` registers the actor and resets its pooled state; `OnDisable:178` removes it and cancels invokes/coroutines/reservations. `ActiveEnemies` therefore tracks enabled controllers, including a dying actor before delayed release, rather than a durable elimination ledger. `EnemyHealth.Initialize/ResetForSpawn` supports reset-before-Awake ordering and restores stable health per reuse. `ActorHealth` latches death/events; `EnemyController.HandleDeath:797` credits eligible kills through `CombatCredit` and starts the death animation/release coroutine. Pool release/deactivation is not itself proof of elimination.

The idempotence of setting `ActorHealth.IsDead` must not be confused with idempotence of manually calling `HandleDeath`: that method separately credits a kill. This audit does not certify repeated arbitrary death-callback injection, nor change existing combat call sites. A future finite policy must define and test accepted elimination independently of pooling.

### Accepted end, result and Backend boundaries

`PlayerHealth` emits `OnDied`; `GameRules.OnPlayerDied:390` requests `PlayerDied`. `MatchController` delegates to `MatchLifecycle.RequestEnd:55`, which accepts the first ending and rejects subsequent requests. `GameRules.Update:338` processes that ending and retries incomplete durable work. Combat completion does not inspect active enemy count. The generic marker-completion predicate belongs to marker requests and is not a Battle Royal victory detector.

`GameRules.CaptureMatchEndState:426` freezes the first accepted timestamp and score eligibility. `HandleMatchEnded:446` finalizes time before showing the HUD and saving. `MatchHudPresenter.GetDisplayText:1108` uses the common Battle Royal/Cage kill, survival-duration and XP formatter; live displays retain their mode-specific historical kill projections.

The ordinary score model takes `TimePlayed` and `EnemiesKilled`. `GameRules.SaveMatchResults:603` saves through SQLite or the existing durable pending queue, handles all-time statistics, and queues Backend V2 once local score durability is established. Progression retains its existing account/result identity and SQLite ledger/retry ownership. No new victory eligibility, schema or result type is authorized.

`BackendV2MatchResultSubmission.TryQueue` excludes competitive attempts and requires the current authenticated Backend session. The adapter sends existing `EnemiesKilled` and `CompletionTimeSeconds` metrics. Local identity remains distinct from Backend PlayerId. This audit establishes current client boundaries only; no live Backend submission or server ranking semantics were certified.

## Approved product policy

**A — Endless survival, approved:** preserve combat/spawning/death/records/HUD and change mode 21's metadata to `MatchObjective.Survival`.

**B — Finite last-player-standing:** first specify eligible contestants; finite admission count; wave/late-entry rules; when admissions close; accepted elimination; whether every enemy must be defeated by the player; victory detection and simultaneous-death/tie precedence; victory scoring/high-score eligibility; winner presentation and exit. Neither the mode name nor active cap supplies these requirements. Produce a complete design before implementation, retaining existing lifecycle/result authorities.

**Additional approved requirement:** a player who has survived for a long time may stop and save the run without dying. Battle Royal's existing Menu and Quit actions are labeled **Save & Main Menu** and **Save & Quit**. A manual saved stop uses the appended `MatchEndCause.PlayerStopped`; it does not claim a finite victory. Earlier accepted death retains `PlayerDied`. Live Restart retains abandonment behavior; after an ending has been accepted, Restart cannot bypass outstanding saving or rotate the result identity.

The pause menu delegates through its existing persistence composition to `GameRules.TrySaveBattleRoyalForExit`. GameRules requests the accepted ending, freezes duration, and runs its existing result, all-time-statistics, progression and Backend-queue steps. SQLite success or the existing durable pending queue may satisfy persistence. If durable work fails, navigation is refused, existing retry feedback is shown, and the unscaled-time match-end retry remains active. A failed score-model conversion cannot silently skip a manually saved score and release navigation. No duplicate exit-only result pipeline was added.

## Review pass 1 — Correctness

1. **Premature victory / spawn timing:** a temporary empty registry can precede admission, follow release, or occur during scene transitions. Removed any active-count-based victory idea from the proposal. Policy A needs no winner detector; B needs explicit closed admission and accepted elimination state.
2. **Death versus pooling:** a disabled enemy may have been pooled or unloaded; a dead actor can remain registered during animation. Preserve existing lifecycle and pooling. Do not promote registry membership to tournament authority.
3. **Double end / simultaneous callbacks:** retain `MatchLifecycle` first-wins acceptance, frozen end time, per-result durable guards and progression ledgers. A adds no competing end request. B requires explicit race/tie rules before a detector is designed.
4. **Score eligibility / duplicate results:** preserve ordinary combat death results and existing kill/time/XP paths. Generic lifecycle acceptance of a synthetic `LastPlayerStanding` cause is not evidence of production finite victory or victory eligibility.
5. **HUD/timer / Cage:** preserve count-up clocks, separate combat capability flags, live kill metrics, shared survival summary, idle-death precedence and failed-save retry behavior. Do not repurpose `RequiresPlayerSurvive` or alter Cage spawning to obtain semantic parity.

Refinement: restrict the objective correction to mode 21. The subsequently requested saved stop uses existing lifecycle/result ownership. Review identified and closed two escape paths: restarting after a failed accepted save cannot rotate identity, and a null score model cannot be treated as a successfully saved manual stop. Death/save ordering retains first-wins cause; repeated Quit/Menu requests retain one result and frozen duration.

## Review pass 2 — Architecture and scope

1. Keep factory/catalog/builder/rules as configuration authority; the HUD remains a consumer, spawner remains population authority, lifecycle remains end authority, existing persistence/progression remain durable authorities.
2. Preserve mode IDs, existing public calls, enum values/order, combat flags, prefab GUIDs and serialized fields. Append `PlayerStopped` after all existing end-cause values. Extend `BindPersistenceContext` with an optional save delegate so existing calls compile unchanged; an unbound Battle Royal save gate fails closed. Do not remove `LastPlayerStanding` from either enum or modify the unknown-ID fallback as collateral cleanup.
3. Generated characterization is factory-owned and must be exported; raw prefab data and historic SHA-specific audits stay intact. Add a current decision record rather than rewriting historical observations.
4. No Backend DTO, persistence schema, progression algorithm, unrelated AI/input/graphics/UI changes, manager, package or framework is necessary. The existing pause actions and GameLevelManager composition carry the approved save option. Finite competition remains outside scope.
5. Verify production-prefab conversion and resolved rules, not synthetic definitions alone. Use existing real-scene fixtures with isolated product persistence; distinguish controlled callbacks, automated assertions, process exit and physical playability.

Final refinement: implement the approved mode-21 correction and saved-stop requirement, document the unknown-ID fallback separately, and preserve other modes' exit policies. Both review passes were performed within this audit; no independent-agent review is claimed.

## Implementation and exact file scope

Immediately before editing, `origin/dev` was fetched again and still matched `e86d92a0d6562ded301a065cfe8a919a429002b5`; the only local addition was this audit report and no open PRs were returned. Work uses branch `fix/gp001-battle-royal-survival`.

The implementation follows the approved A correction plus the explicit saved-stop requirement:

1. `Assets/Scripts/menu_start/GameModeDefinitionFactory.cs`: group the existing mode-21 case with mode 22's `Survival` return. Leave fallback and all other rule dimensions unchanged.
2. `Assets/Tests/Editor/Level5AuthoredMatchDataTests.cs`: assert both real authored combat modes resolve to Survival while preserving IDs, description, clock, exact combat flags, fighter/CPU rules, score field and arena compatibility. Exercise `MatchConfigurationBuilder.Resolve` with authored sources.
3. `Assets/Tests/PlayMode/Level5PostRemediationCertificationPlayModeTests.cs`: extend the existing mode-21 probe with Survival/exact-combat assertions and a bounded temporary-empty-population check that remains Playing and permits later existing admission. Preserve its controlled kill, real health-death event, exact SQLite kill/time assertions and persistence isolation.
4. `Assets/Tests/PlayMode/Level5CombatSummaryPlayModeTests.cs`: assert the Battle Royal launch resolves Survival; retain existing Cage/neighbor summary, XP, retry and duration regressions.
5. `docs/generated/level5-game-mode-characterization.md`: regenerate through the existing Unity exporter; expect only mode 21's objective cell to change. Run checked-in exporter parity afterward.
6. This decision record and `docs/README.md`: record owner approval, implemented scope, executed validation and current result. Preserve the historical characterization/certification reports.
7. `Assets/Level5/Core/Match/MatchPhase.cs`: append `PlayerStopped` without changing existing serialized enum values.
8. `Assets/Scripts/game manager/GameRules.cs`: provide the mode-21 saved-stop durability gate over existing match-end handling, with first-wins cause and null-score retry protection.
9. `Assets/Scripts/game manager/GameLevelManager.cs`: bind that operation through the existing Pause persistence composition.
10. `Assets/Scripts/game manager/Level5Match/Pause.cs`: label and gate Save & Main Menu / Save & Quit; refuse Restart while an accepted Battle Royal result remains unsaved. Refuse direct pause resume after a Battle Royal ending so the mobile Cancel route cannot resume combat during saving. Preserve live pause/resume, live Restart and neighboring-mode exit policies.

The combat PlayMode fixture additionally covers a four-hour live run, both exit durability steps, repeated saving, death/save ordering, failed SQLite plus failed fallback, visible retry feedback, blocked navigation/restart, automatic recovery, and durable fallback repair. Successful process quit is intentionally stopped before `Application.Quit` so NUnit can finish.

No prefab, scene, `.meta`, spawner, health, lifecycle implementation, persistence schema/store or Backend implementation changes. Existing public methods and enum values remain; the appended end cause and optional pause binding are intentional narrow additions required by the approved saved-stop path.

Any future B proposal requires a complete finite-competition specification before design/implementation. No finite competition was inferred or added.

## Validation executed on the unchanged baseline

Pinned editor: **Unity 6000.5.7f1**, revision `017862109af0`.

| Check | Result and limit |
| --- | --- |
| Repository validator | **PASS**; process exit 0; `Repository validation passed.` |
| Focused EditMode | **PASS**; 66 passed, 0 failed/skipped; process exit 0. Authored match data, match configuration, lifecycle and documentation exporter fixtures. |
| Graphics-enabled PlayMode | **PASS**; 6 passed, 0 failed/skipped; process exit 0. Five existing combat-summary regressions plus the existing mode-21 staging/death probe. |
| Mode-21 observed execution | Population 1 → 2, one boss; controlled kill credit 1; accepted `PlayerDied`; SQLite kills 1 and survival `11.03107`, matching stats and summary. Not a natural player attack or finite-winner test. |
| Mode-22 regression | Cage summary, real damage/death, failed-save/recovery, frozen duration, single score/all-time/progression ledger entries, missing summary and idle-death presentation passed in the existing fixture. Seeded statistics/timestamp; not physical combat certification. |
| Generated characterization | Existing exporter parity test passed with the current `LastPlayerStanding` mapping. No regeneration needed or performed for unchanged sources. |
| Temporary-empty-enemy acceptance | **Source-inspected only**: no active-count completion consumer exists. A dedicated dynamic empty-population check is part of the conditional A implementation, not claimed as executed. |
| Full population saturation, natural combat/input, release Player, live Backend, hosted CI | **NOT RUN**. No claim of physical gameplay certification or remote ranking/submission correctness. |

Commands from repository root:

```text
git fetch origin dev
git rev-parse HEAD origin/dev
python Logs/gp001-battle-royal-semantics/run_validation.py validator
python Logs/gp001-battle-royal-semantics/run_validation.py edit
python Logs/gp001-battle-royal-semantics/run_validation.py play
git diff --check
```

Exact argument arrays, process exits, logs and NUnit XML are retained under ignored `Logs/gp001-battle-royal-semantics/`. The wrapper invokes the existing validator via system Windows PowerShell. EditMode uses `-batchmode -nographics -runTests -testPlatform EditMode` with filter `Level5AuthoredMatchDataTests;Level5MatchConfigurationTests;Level5MatchLifecycleTests;Level5DocumentationExporterTests`. PlayMode uses graphics-enabled `-batchmode -runTests -testPlatform PlayMode` with filter `Level5CombatSummaryPlayModeTests;Level5PostRemediationCertificationPlayModeTests.Mode21ObservesStagedPopulationAndChecksCreditedKillDeathAndSummary`.

Both Unity invocations use company `level5-audit-20261012-14b2cdc` and disposable product `level5-gp001-battle-royal-semantics-20261013`; fixture SQLite is disposable and existing fixture snapshot/restore protects pending/session/account data. The wrapper restored `ProjectSettings.asset` byte for byte after each run: SHA-256 `f567cf07efd0819f869978b7d3054535b87f6b9f286b98fd6ffa79e75bdfc91a`. Unity-generated solution ordering and an imported enemy animation asset's line-ending change were restored. Accepted collider/startup warnings were not changed or reclassified as certified clean gameplay.

## Implementation validation and review corrections

Artifacts for the approved implementation are retained separately under `Logs/gp001-battle-royal-semantics/implementation/`; baseline artifacts above were preserved. Exact invocation arrays and process exits are alongside XML/logs.

- Compilation and the actual Unity characterization export passed, process exit 0. Generated diff contains only mode 21's objective cell (`LastPlayerStanding` → `Survival`).
- Focused EditMode passed **110/110**, no skipped/failed, process exit 0: authored-data/runtime-rule assertions, generated parity, match configuration/lifecycle, pause composition, architecture guards and Backend result wiring/adapter regressions.
- The initial affected PlayMode selection passed **10/11**, process exit 2. The new failure test initially invoked a button after the existing gameplay harness had disabled Pause and unregistered listeners. The corrected test re-enables the actual component and tests its real onClick handlers. Its first isolated retry then exposed an empty pending-queue file created during startup repair; before injecting the directory blocker, the test now asserts that file has no pending scores/all-time entries and removes it under the fixture's existing whole-family snapshot/restore. This is fixture initialization repair, not a production gameplay workaround.
- The corrected isolated save-failure case passed **1/1**, no skipped/failed, process exit 0. It verifies actual Quit/Menu button feedback, refused navigation and Restart/result-ID rotation, automatic unscaled retry, frozen elapsed time, one local score and one all-time/progression ledger entry, then a released exit gate.
- Final affected PlayMode selection passed **11/11**, no skipped/failed, process exit 0, after both fixture repairs. The nine combat/saved-stop cases, extended mode-21 admission probe and neighboring Arcade/Free Play policy case all passed together. Natural admissions were 1 → 2; after controlled disable to zero, the run remained Playing and scheduled admission resumed with one enemy. The later controlled kill/death stored kills 1 and survival `19.99797`, matching the displayed/statistics duration.
- Final repository validator passed, process exit 0. `git diff --check` passed. Unity-generated solution/animation import churn was restored; `ProjectSettings.asset` remains byte-identical to its original. The final local change set contains only the eleven implementation/test/documentation files listed above.

Commands for implementation:

```text
python Logs/gp001-battle-royal-semantics/run_validation.py export
python Logs/gp001-battle-royal-semantics/run_validation.py edit
python Logs/gp001-battle-royal-semantics/run_validation.py play
python Logs/gp001-battle-royal-semantics/run_validation.py retry
python Logs/gp001-battle-royal-semantics/run_validation.py retry-final
python Logs/gp001-battle-royal-semantics/run_validation.py final-play
python Logs/gp001-battle-royal-semantics/run_validation.py validator
```

The final PlayMode filter selects the nine combat-summary/saved-stop cases, the one existing mode-21 staging probe extended with an empty-population check, and the existing Arcade/Free Play history/exit regression. The four-hour case injects its start timestamp and kill statistics, verifies the player is alive when both exit durability steps succeed, and checks a later death cannot replace `PlayerStopped` or duplicate saving. The fallback case verifies failed SQLite insertion is durably queued, then repaired to one correct kill/time row. The natural admission probe verifies an empty registry does not complete the run and the original schedule resumes admission.

Successful exit iterators stop before application termination or scene navigation; the failure case drives actual button handlers and asserts the gameplay scene remains active. Full physical controller/touch interaction, successful OS process quit, human visual acceptance, release Player/IL2CPP and live Backend delivery remain NOT RUN. SQLite, result identity, queues, progression and Backend contracts retain their existing owners; no full gameplay certification is inferred from controlled callbacks.

## Review finding closure — touch resume after a saved stop

The subsequent review found one material issue: mobile double-tap Cancel calls `Pause.TogglePause` directly, bypassing the normal Cancel handler's GameOver check. Mode 21 can now end while the player is alive; after a failed saved stop that direct route could resume combat while elapsed result time was already frozen.

The correction rejects resume in the shared Pause API when mode 21 is already Ending or Completed and time is paused. It reads the existing `MatchController` lifecycle rather than adding a new flag or changing touch input architecture. Live runs retain ordinary pause/resume. Successful Menu, Quit and Restart navigation retain `SceneTransition` time restoration, independent of the refused in-place resume.

The existing failed-save PlayMode regression now verifies ordinary live resume first, then directly calls the API used by touch Cancel after `PlayerStopped`. It checks pause/time scale remain frozen across a real-time save retry, kills and elapsed time stay unchanged, navigation/Restart remain blocked, recovery persists one result, and completed stopped runs cannot resume either. This exercises the shared resume boundary without claiming physical Android/iOS gesture certification.

Post-review validation passed with Unity 6000.5.7f1 and the same isolated persistence settings: **50/50** focused Pause/lifecycle/architecture EditMode tests and **11/11** affected graphics-enabled PlayMode tests, no failures/skips, both process exits 0. The final PlayMode run includes the extended direct-resume failure/recovery assertions and all neighboring cases. The prior 110-case EditMode result remains applicable to unchanged authored/builder/export/Backend contracts. Artifacts are `implementation/review-edit.*` and `implementation/review-play.*`; the final repository validator also passed. Project settings were restored byte for byte after both Unity runs.

## PR-ready summary

Battle Royal advertised last-player-standing semantics while continuously admitting enemies and ending on player death. Align mode 21's objective with the approved endless-survival policy and add Save & Main Menu / Save & Quit so a live run can record kills and survival time through the existing match-end pipeline. Refuse exits and Restart while an accepted save is incomplete, keep ended survival runs paused across direct touch Cancel, retain first-wins death/manual-stop causes and frozen duration, and preserve Cage Match, Arcade and live Restart behavior.

Regressions cover authored/runtime rule parity, empty-population admission, long live runs, repeated saved exits, death ordering, failed saving and recovery, durable fallback, Cage summaries and neighboring Arcade/Free Play policies. Characterization is regenerated through the existing Unity exporter; no prefabs, GUIDs, save schemas or Backend DTOs change.

## Delivery

The product decision is resolved and the scoped correction plus review finding are implemented on `fix/gp001-battle-royal-survival`, targeting `dev`. The owner subsequently authorized commit, push and PR delivery; the PR-ready summary above describes the final approved scope. No issue or external discussion comment is part of this delivery.
