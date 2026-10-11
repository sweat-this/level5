# GP-001 Characterize All Selectable Single-Player Modes

Audit date: 2026-10-11. Repository: `sweat-this/level5`. Baseline: latest fetched `dev` at **`c4353a905d845f65e83b0a1c253168e7264d4b8b`**. Unity: **6000.5.7f1**, revision **017862109af0**.

The production menu exposes 24 single-player mode IDs. Every one is accounted for below. None is Certified by this audit: 158 focused EditMode tests and six PlayMode tests passed, but complete authored-mode playthroughs were not executed. Ten source-confirmed findings affect configuration, scoring records, result eligibility, concurrent hoop detection, campaign build membership, and presentation.

This is a gameplay-read-only audit. Its publication changes documentation only. No gameplay, public contracts, serialized assets, scenes, persistence formats, multiplayer capabilities, or GitHub issues were changed. Source links are pinned to the audit commit.

## Baseline and repository coordination

| Item | Evidence |
| --- | --- |
| Branch and SHA | `git fetch origin dev`; HEAD, `origin/dev`, and FETCH_HEAD matched `c4353a905d845f65e83b0a1c253168e7264d4b8b` |
| Final remote check | `git ls-remote origin refs/heads/dev` returned the same SHA |
| Unity | [ProjectVersion.txt](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/ProjectSettings/ProjectVersion.txt#L1) |
| Open PRs | `gh pr list --repo sweat-this/level5 --state open --limit 100 --json number,title,headRefName,baseRefName,url` returned `[]` |
| Issue inventory | Open and closed issues inspected with `gh issue list --repo sweat-this/level5 --state all --limit 300 --json number,title,state,url`; relevant ownership bodies inspected |
| Hosted Repository Quality | Workflow ID `327878811` was `disabled_manually`; optional Unity configuration is not evidence that Unity CI ran |
| Repository state after gameplay audit | Clean; `git diff --exit-code` returned 0 |

Recent merged changes were considered before proposing work:

- [PR #280](https://github.com/sweat-this/level5/pull/280), the baseline commit, reconciled arena characterization and added drift detection.
- [PR #218](https://github.com/sweat-this/level5/pull/218) certified the local simultaneous Most Points slice and repaired its launch-error presentation.
- [PR #217](https://github.com/sweat-this/level5/pull/217) prevented atomic pair submission with missing participant stats.
- [PR #216](https://github.com/sweat-this/level5/pull/216) introduced the two-human vertical slice and atomic pair results.
- [PR #215](https://github.com/sweat-this/level5/pull/215) established shared two-human camera behavior and truthful arena multiplayer capability.
- [PR #214](https://github.com/sweat-this/level5/pull/214) repaired defects exposed by production Local Versus certification.

Earlier merged progression, SQLite durability, local eligibility, and Backend V2 exclusion work was traced through current source. The single-player findings below do not reopen those completed migrations or imply ownership by the multiplayer certification backlog.

## Available validation and executed checks

The installed editor successfully compiled and ran focused EditMode and graphics-enabled PlayMode checks. Existing characterization, parity, and test infrastructure was sufficient; no helper was introduced.

| Check | Outcome | What it establishes |
| --- | --- | --- |
| `scripts/validate-repository.ps1` | Exit 0 | Repository hygiene invariants |
| First successful focused EditMode run | 71 passed, zero failed/skipped | One current-authored-source characterization drift check plus configuration, parity, eligibility, scoring, lifecycle |
| Additional focused EditMode run | 87 passed, zero failed/skipped | Authored data, compatibility, timer context, marker sessions, CPU shot selection, stats |
| Graphics-enabled PlayMode run | Six passed, zero failed/skipped | Human/CPU shooting coroutine convergence, ordinary match/versus boundary, singleton teardown, reporter seam, campaign decision seam |
| Initial sandboxed Unity launch | Exit 1 before test discovery; no XML | Package Manager could not connect its IPC endpoint after 30 seconds |
| Successful retry boundary | Elevated editor process | Resolved the startup restriction; the failed launch itself is not a passed check |

The PlayMode campaign check exercises a round-decision seam. It does not traverse the authored campaign. The human and CPU launch checks execute their shooting coroutines, not complete mode sessions.

### Exact process invocations

Commands below express the actual Unity process arguments in PowerShell. The execution wrapper supplied these arguments through Python subprocess invocation because direct shell process creation was restricted.

```powershell
$repo = 'C:\Users\Patrick\Documents\github\level5'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe'
$out = 'C:\Users\Patrick\AppData\Local\Temp\level5-gp001-c4353a9'

# Executed from $repo
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate-repository.ps1

& $unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testFilter 'Level5DocumentationExporterTests.CheckedInModeCharacterizationMatchesCurrentAuthoredSources;Level5MatchConfigurationTests;Level5MatchBridgeParityTests;Level5LevelEligibilityTests;Level5ShotScoringTests;Level5MatchLifecycleTests' -testResults "$out\editmode-elevated.xml" -logFile "$out\editmode-elevated.log"

& $unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testFilter 'Level5AuthoredMatchDataTests;Level5MatchCompatibilityTests;Level5TimerMatchEndContextTests;Level5BasketballShotMarkerSessionTests;Level5CpuShotSelectionTests;Level5MatchStatsTests' -testResults "$out\editmode-paths.xml" -logFile "$out\editmode-paths.log"

& $unity -batchmode -projectPath $repo -runTests -testPlatform PlayMode -testFilter 'Level5BasketballShotAttemptAndLaunchConvergencePlayModeTests;CampaignRoundPlayModeTests;Level5GameplayPlayModeTests' -testResults "$out\playmode.xml" -logFile "$out\playmode.log"
```

| Run | Executed fixture counts |
| --- | --- |
| EditMode 71 | Documentation drift 1; eligibility 15; bridge parity 9; configuration 10; lifecycle 19; shot scoring 17 |
| EditMode 87 | Authored data 8; basketball marker sessions 8; CPU selection 29; compatibility 11; stats 20; timer context 11 |
| PlayMode 6 | Campaign round decision 1; human/CPU shot convergence 2; gameplay seams 3 |

The XML and logs remain in the local output directory above; they are not checked-in artifacts. The initial failed launch log is `editmode.log`. The successful XML roots report 71/71, 87/87, and 6/6 respectively.

Not executed: full test suites, complete authored-mode playthroughs, all arena/modifier combinations, player builds, physical-device checks, or live Backend V2 submission. These omissions prevent gameplay certification. Hosted validation recovery is owned by [#232](https://github.com/sweat-this/level5/issues/232) and [#233](https://github.com/sweat-this/level5/issues/233).

## Authored source and production availability

Production availability comes from loaded menu assets:

1. [LoadManager](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_loading/LoadManager.cs#L619) loads `Prefabs/menu_start/mode_selected_objects`, obtains `StartScreenModeSelected` roots, and sorts by stable mode ID.
2. [LegacyMatchCatalogBootstrap](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_start/LegacyMatchCatalogBootstrap.cs#L31) converts those sources through `GameModeDefinitionFactory`.
3. [MatchCatalogs](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchCatalogs.cs#L83) prefers authored `Resources/Match/Modes` definitions when present. That directory is absent on this baseline, so the menu-prefab conversion is the production authored path.
4. [StartMenuSelectionState.CycleMode](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_start/StartMenuSelectionState.cs#L90) cycles the whole resulting mode catalog and adjusts arena eligibility. It does not apply a mode-unlock or mode-visibility gate.

[GameModeDefinition](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/GameModeDefinition.cs#L1) holds the typed rules; [GameModeCatalog](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/GameModeCatalog.cs#L1) indexes definitions. The legacy sources remain authored inputs rather than proof that every numeric constant is selectable.

The existing [generated characterization matrix](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/docs/generated/level5-game-mode-characterization.md#L9) matched current loaded sources in the executed drift test. Its absence of conversion anomalies establishes representational parity, not gameplay correctness.

| Availability category | Result |
| --- | --- |
| Production-menu-selectable modes | 24 IDs listed below |
| Progression-gated modes | None in the mode-cycle path; character progression is separate |
| Hidden authored modes | None among the 24 |
| Legacy-only raw branches | Sentinel/default 0; longest-shot 5; Money Ball Spot-Up 10, 11, 12; no production menu prefabs |
| Developer configuration | Development utility builds an ID-23 variant; no additional stable mode ID |
| Hidden ordinary arenas | 23 Dev and 24 Boneyard; both occur in the authored campaign route |
| Numeric holes | Not additional shipping modes |

The development utility is [LoadGame.cs](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/Utility/LoadGame.cs#L34). Unity-ignored `Legacy~` files do not establish production availability.

## Complete authored mode inventory

Each row links to its actual prefab at identity line 47. Fields following that line contain description, clock, shot/combat flags, score field, survival, ball, and CPU settings. Derived objectives, roster limits, and arena requirements come from [GameModeDefinitionFactory](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_start/GameModeDefinitionFactory.cs#L46).

Shared defaults: basketball enabled; combat `None`; shot rule `Any`; no markers; roster bounds 1–4; selected CPU shooters disabled. Money Ball is false in all 24 modes. `RequiresPlayerSurvive` is true only for 7, 8, and 20. Default countdown duration is [180 seconds](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/MatchClock.cs#L11); that value does not impose a time limit on count-up or clockless modes.

Arena codes are authored compatibility requirements, not executed scene certification:

| Code | Required compatibility |
| --- | --- |
| B | Basketball; excludes Battle Royal arenas |
| B7 | B plus SevenPointLine |
| C | Combat; excludes Battle Royal arenas |
| BR | Combat plus BattleRoyal |
| Cage | Combat plus Cage and BattleRoyal |

| ID and authored source | Display name | Primary MatchObjective | Clock | Distinct authored rules | Arena and roster |
| --- | --- | --- | --- | --- | --- |
| [1](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_01.prefab#L47) | Total Points | Score | Down 180 | Defaults | B; 1–4 |
| [2](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_02.prefab#L47) | Total 3 pointers | MakeCount | Down 180 | Defaults | B; 1–4 |
| [3](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_03.prefab#L47) | Total 4 pointers | MakeCount | Down 180 | Defaults | B; 1–4 |
| [4](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_04.prefab#L47) | Total 7 pointers | MakeCount | Down 180 | Defaults | B; 1–4 |
| [6](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_06.prefab#L47) | Total Distance | Distance | Down 180 | Defaults | B; 1–4 |
| [7](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_07.prefab#L47) | spot up some 3s | ContestCompletion | Up | Three-point make-quota markers; survive flag | B; 1–4 |
| [8](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_08.prefab#L47) | spot up some 4s | ContestCompletion | Up | Four-point make-quota markers; survive flag | B; 1–4 |
| [9](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_09.prefab#L47) | `spot up all ` | ContestCompletion | Up | Three- and four-point make-quota markers | B; 1–4 |
| [14](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_14.prefab#L47) | Consecutive shots | ConsecutiveShots | Down 60 | Consecutive-shot overtime | B; 1–4 |
| [15](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_15.prefab#L47) | In the Pocket | Score | Down 180 | ID-specific streak bonus | B; 1–4 |
| [16](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_16.prefab#L47) | 3 point Contest | ContestCompletion | Down 70 | ThreePoint rule/markers; optional CPU shooters | B; 1–4 |
| [17](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_17.prefab#L47) | 4 point contest | ContestCompletion | Down 75 | FourPoint rule/markers | B; 1–4 |
| [18](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_18.prefab#L47) | all point contest | ContestCompletion | Down 140 | ThreePoint + FourPoint + AllRanges; three/four markers | B; 1–4 |
| [19](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_19.prefab#L47) | Points by Distance | Score | Down 180 | ID-specific distance scoring | B; 1–4 |
| [20](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_20.prefab#L47) | Bash Up Some Nerds | Score | None | Standard combat; enemies-only; no ball; survive flag | C; 1–4 |
| [21](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_21.prefab#L47) | Battle Royal | LastPlayerStanding | Up | BattleRoyal; enemies-only; no ball | BR; 1–4 |
| [22](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_22.prefab#L47) | Cage Match | Survival | Up | Cage + BattleRoyal; enemies-only; no ball | Cage; 1–4 |
| [23](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_23.prefab#L47) | Versus | Score | Down 180 | CPU shooters and CPU opponent required | B; 2–4 |
| [24](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_24.prefab#L47) | 7 point contest | ContestCompletion | Down 90 | SevenPoint rule/markers | B7; 1–4 |
| [25](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_25.prefab#L47) | Spot up some 7s | ContestCompletion | Down 180 | No authored markers or contest flags | B; 1–4 |
| [26](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_26.prefab#L47) | Beat tha computahs | CampaignProgression | Down 60 per round | CPU required; authored route | B; 2–4 |
| [27](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_27.prefab#L47) | Lockdown | Score | Down 120 | One ball; implicit defensive CPU | B; exactly one selected participant |
| [98](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_98.prefab#L47) | Arcade mode | Score | None | Arcade stat treatment; zero XP | B; 1–4 |
| [99](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_99.prefab#L47) | free play | Score | None | Explicit exit/restart persistence | B; 1–4 |

The 1–4 bounds describe definitions, not certification of additional humans. This audit concerns one human, with CPU participants only where supported. Mode 27 also authors CPU support, but its one selected slot and implicit defender govern its actual path. Modes 23/26 reconcile a required CPU opponent. [SpawnCoordinator](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/SpawnCoordinator.cs#L277) skips CPU slots when the mode disallows shooters.

The current ordinary arena inventory has 21 selectable entries. All author unlocked state:

| Compatibility | Current selectable arena IDs |
| --- | --- |
| B | 1–11, 13–16, 19–22: 19 arenas |
| B7 | Same 19; all currently author SevenPointLine |
| C | The basketball/combat entries above except basketball-only 22: 18 arenas |
| BR | 17 Rumble Pit; 18 Crank Zone |
| Cage | 17 Rumble Pit only |

[GameModeCompatibility](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/GameModeCompatibility.cs#L210) owns arena filtering. [LevelEligibility](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/LevelEligibility.cs#L1) additionally applies selectability and unlock state. Capability flags do not prove that every scene contains correct runtime geometry or bindings.

## Runtime ownership and execution path

### Configuration and composition

[StartManager.loadGame](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_start/StartManager.cs#L1675) reconciles player/CPU selection, preflights devices, builds configuration with the unlock snapshot, begins `ActiveMatch`, applies `LegacyGameOptionsBridge`, and loads the scene. Campaign launch forces its first arena.

[GameLevelManager.Awake](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameLevelManager.cs#L75) captures rules and roster, binds the hoop, validates spawn locations and device composition, and spawns participants and balls. Invalid composition can disable the manager. [SpawnCoordinator](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/SpawnCoordinator.cs#L312) gives balls to spawned participants, respecting CPU support. Lockdown supplies one ball and an implicit defender targeting the primary human.

Common modifiers are enemies, traffic, obstacles, sniper bullet/machine-gun/laser, Easy/Normal/Hard/Hardcore, and Hardcore behavior. [MatchConfigurationBuilder.Resolve](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/MatchConfigurationBuilder.cs#L134) forces enemies for combat/no-ball modes, masks traffic by arena support, and carries obstacles/sniper through. Requested enemies also affect character fighter eligibility. Modifier combinations were not exhaustively executed.

### Basketball and CPU

Human and CPU shots use shared launch-time snapshots and attempt/launch infrastructure. The two convergence PlayMode tests exercise their actual shooting coroutines. CPU choice uses the current shot-selection policy, authored preference, accuracy/range, and score deficit; 29 CPU selection tests passed. This does not certify navigation, marker participation, or a complete CPU opponent session.

[BasketBallShotMade](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/basketball/BasketBallShotMade.cs#L315) builds scoring input from the bound rules, stable mode identity, shot snapshot, and marker. [MatchStats.ApplyMadeShot](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/MatchStats.cs#L176) updates counted makes and streak state before final scoring.

- Ordinary baskets award 2/3/4/7 points.
- Points by Distance uses floored converted-distance scoring; distance statistics use Unity distance multiplied by six.
- Pocket has its own bonus input and preserved zero activation threshold.
- Point contests require enabled markers and use attempt quotas; Spot-Up uses make quotas.
- Mode 18's ThreePoint + FourPoint + AllRanges flags are meaningful. They preserve three/four marker behavior and final-attempt doubling; seven-point markers are not authored.
- Money Ball remains false in every selectable mode. Latent Money Ball oddities are not current shipping-mode defects.

### Combat and campaign

Mode 20 uses standard combat spawning. Mode 21 invokes repeated Battle Royal contestant spawning under an alive-population cap. Mode 22 carries both Cage and BattleRoyal and follows the Cage/default-population branch. [EnemySpawner](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/enemy/EnemySpawner.cs#L78) demonstrates why those combined flags must be retained.

The [campaign roster](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/critical/player_data.prefab#L52) resolves, in order, to arena IDs:

```text
1, 3, 2, 8, 6, 13, 19, 14, 20, 7, 21, 9, 16, 5, 24, 4, 10, 11, 22, 15, 23
```

Campaign rounds use an authored CPU resolver and points ordering. Ties retry; wins advance or complete; losses retry while continues remain. New campaign launch supplies two continues, or zero in Hardcore. [EndRoundMenuManager](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_end_round/EndRoundMenuManager.cs#L260) preflights devices, consumes a loss continue when appropriate, continues configuration into the next level, begins a new match session, and loads the scene. Continuation does not reapply ordinary menu selectability.

### Termination and recovery

| Trigger | Current path | Evidence and remaining limit |
| --- | --- | --- |
| Ordinary timeout | Primary player's thrown-ball/grounded predicates defer ending | Timer context tests passed; complete scenes unexecuted |
| Consecutive-shot timeout | Streak of at least three permits overtime until broken | Shared scoring/stats/end predicates checked |
| Last marker | Marker completion requests ending | Marker/session checks passed; authored courses unexecuted |
| Primary-player death | Death event and health polling request ending | Source traced; shared lifecycle checked |
| CPU death/elimination | No general finite-roster winner contract established | Mode-specific runtime behavior unverified |
| Duplicate end requests | First request wins; later calls are no-ops | Lifecycle tests passed |
| End work failure | Completion flags retain finished work; unfinished persistence retries | No full crash/storage-fault certification |
| Ordinary timed-mode quit/restart | Aborts attempt without synthesizing a completed score | Source behavior; no assumed requirement to save aborted scores |
| Free Play quit/restart | Explicit stats persistence followed by exit/new session | Source traced; durability faults unexecuted |
| Campaign retry | Decision-based retry with tie/loss continue distinction | Decision seam checked, full route unexecuted |
| Missing components | Mixed logged disable/skip and unchecked access paths | Malformed composition risks; no blanket claim of broken shipping scenes |

[MatchEndConditions](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/MatchEndConditions.cs#L33), [Timer](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/Timer.cs#L252), and [MatchLifecycle](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/MatchLifecycle.cs#L55) own the relevant predicates and first-ending semantics. This is Unity-main-thread evidence, not certification of arbitrary multithreaded completion.

### Presentation and result boundaries

[MatchHudPresenter](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchHudPresenter.cs#L1018) selects end summaries by mode. [GameRules](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L408) coordinates presentation, score persistence, all-time stats, progression, campaign transition, and optional versus reporting. Presentation failure is handled separately from persistence.

[HighScoreModel](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/Models/HighScoreModel.cs#L198) records stable mode/arena IDs and primary-player stats, including points, total makes, per-range makes, distance, streak, elapsed time, kills, and modifiers. Ordinary result saving uses SQLite or a durable pending record before attempting Backend V2 enqueue. Free Play skips ordinary completion-score saving; campaign saves an aggregate through the end-round path. All-time stats and progression have separate session/result deduplication.

[GameRules.SaveMatchResults](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L565) excludes competition-only simultaneous matches from ordinary player-specific score/all-time handling. [BackendV2MatchResultSubmission](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/backendv2/BackendV2MatchResultSubmission.cs#L42) excludes active competitive attempts and requires a current authenticated session. Its adapter carries generic existing metrics; it does not make server authority or mode-specific ranking correctness a proven fact. No authenticated session means no enqueue at that instant, not retroactive submission after later login.

The durable submission coordinator serializes queue draining, removes successful submissions, marks definitive failures, and retains ambiguous failures. SQLite score IDs, all-time session ledgers, and progression result IDs provide existing idempotency boundaries. Live network behavior and crash recovery were not executed.

[Pause](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/Pause.cs#L510) explicitly persists Free Play on quit/menu return/restart. Ordinary Arcade does not satisfy that condition. Competitive-turn exit durability and restart restrictions are separate behavior and are not grounds for broadening this single-player audit.

## Mode execution and classification matrix

**S** means source-traced. Shared checks refer to relevant focused mechanics, never a complete execution of the listed mode. Confirmed Defective includes explicit presentation defects; it does not authorize changing uncertain gameplay intent.

| ID | Metric and presentation | Normal ending | Classification | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Total points | Timeout/death | Implemented–Not Certified | S + shared checks |
| 2 | HUD threes; record all makes | Timeout/death | Confirmed Defective: F2 | S + shared checks |
| 3 | HUD fours; record all makes | Timeout/death | Confirmed Defective: F2 | S + shared checks |
| 4 | HUD sevens; record all makes | Timeout/death | Confirmed Defective: F2 | S + shared checks |
| 6 | Total made-shot distance | Timeout/death | Implemented–Not Certified | S + shared checks |
| 7 | Elapsed time; ascending record | Markers/death | Confirmed Defective: F3 | S + shared checks |
| 8 | Elapsed time; ascending record | Markers/death | Confirmed Defective: F3 | S + shared checks |
| 9 | Elapsed time; ascending record | Markers/death | Confirmed Defective: F3 | S + shared checks |
| 14 | Best qualifying streak | Timeout/overtime/death | Confirmed Defective: F7 copy | S + shared checks |
| 15 | Points with Pocket bonus | Timeout/death | Confirmed Defective: F9 description/rule mismatch | S + shared checks |
| 16 | Marker-contest points | Markers/timeout/death | Confirmed Defective: F4 with CPUs, F8 copy | S + shared checks |
| 17 | Marker-contest points | Markers/timeout/death | Confirmed Defective: F8 copy | S + shared checks |
| 18 | Combined three/four contest points | Markers/timeout/death | Confirmed Defective: F8 copy | S + shared checks |
| 19 | Distance-derived points | Timeout/death | Implemented–Not Certified | S + shared checks |
| 20 | Enemy kills | Death; no finite completion established | Implemented–Not Certified | S; shared lifecycle only |
| 21 | Kills and survival time | Death; finite victory unspecified | Implemented–Not Certified | S; shared lifecycle only |
| 22 | Live kills; missing ordinary summary | Death | Confirmed Defective: F6 | S; shared lifecycle only |
| 23 | Participant points and winner display | Timeout/death | Confirmed Defective: F4 | S + shared checks |
| 24 | Seven-point contest points | Markers/timeout/death | Confirmed Defective: F7/F8 copy | S + shared checks |
| 25 | Open shooting; result presented/ranked as time | Timeout/death | Confirmed Defective: F1/F3 | S + shared checks |
| 26 | Round points and campaign aggregate | Advance/retry/terminal result | Confirmed Defective: F4/F5 | S + shared checks; campaign decision seam |
| 27 | Points and blocked-shot summary | Timeout/death | Implemented–Not Certified | S + shared checks; defender unexecuted |
| 98 | Longest shot and Arcade summary | Death/exit; exit differs from Free Play | Confirmed Defective: F10 | S + shared checks |
| 99 | Longest shot and Free Play stats/XP | Exit/restart/death | Implemented–Not Certified | S + shared checks |

No selectable mode is Certified or Intentionally Unsupported. The latter applies to explicit rejected configurations: incompatible arenas, remote-human/replay-ghost participants in this ordinary launch path, and invalid roster sizes. Legacy-only and developer entry points do not add shipping selectable modes.

## Shared mechanic families and unique paths

| Family | IDs | Shared coverage | Distinct unexecuted path |
| --- | --- | --- | --- |
| Timed open basketball | 1, 2, 3, 4, 6 | Launch, scoring, timer, lifecycle | Range-specific ranking and distance presentation |
| Spot-Up | 7, 8, 9, 25 | Marker snapshots, make quotas, completion | Authored courses; incomplete-record exclusion; mode 25 configuration |
| Point contest | 16, 17, 18, 24 | Attempts, restricted scoring, final-attempt doubling | Combined mode-18 flags; optional CPU in 16; seven-point geometry |
| Streak | 14 | Streak updates and overtime predicate | Miss/reset through a complete overtime session |
| Pocket and distance points | 15, 19 | Shared scoring with ID-specific inputs | Pocket intent; distance rounding/presentation |
| Combat | 20, 21, 22 | Primary death and lifecycle | Standard replenishment, Battle Royal spawning, Cage summary |
| CPU competition | 23, 26 | Selection and human/CPU launch convergence | Concurrent hoop crossings, score ordering, full matches |
| Campaign | 26 | Round decision and configuration continuation | Entire route, continues, aggregate save, final scene |
| Defensive challenge | 27 | One human/ball and implicit defender composition | Defender behavior and blocking through completion |
| Open-ended play | 98, 99 | Shared basketball/HUD/stat paths | Arcade record isolation; exit durability; XP distinction |

## Confirmed findings

All ten findings below are confirmed from source on the pinned baseline. Reproduction steps specify triggering sequences; complete gameplay reproductions were not executed during this audit. Generic passing tests do not cover these affected compositions.

### F1 Spot-Up 7s lacks its advertised marker course

**Affected:** 25. **Evidence level:** static authored configuration and traced setup.

**Reproduce:** Select Spot up some 7s and launch a basketball arena. Inspect the resolved definition and marker setup: no seven-point marker requirement is requested, and the default 180-second countdown applies.

**Impact and root cause:** The advertised “Make 5 7s from each position marker” course cannot run through normal marker setup because every marker flag is false.

**Evidence:** [mode 25 fields](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_25.prefab#L50), [GameRules marker setup](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L291), [factory arena requirements](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_start/GameModeDefinitionFactory.cs#L315).

**Smallest safe correction:** Author the seven-point marker requirement and regenerate characterization. Preserve stable ID, countdown, and make-quota semantics unless timing intent is separately approved. Do not set point-contest flags merely because the mode uses seven-point markers.

### F2 Range-specific records count nonqualifying makes

**Affected:** 2, 3, 4. **Evidence level:** static query/model mismatch plus exercised shared stats behavior.

**Reproduce:** In Total 3 pointers, make only two-pointers and finish. The summary reports zero threes, but the record field receives total makes. Repeat with wrong-range shots in modes 3/4.

**Impact and root cause:** Nonqualifying baskets improve range-specific records. All three queries use `maxShotMade`, populated from the sum of all shot ranges.

**Evidence:** [PlayerData queries](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/PlayerData.cs#L133), [HighScoreModel mapping](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/Models/HighScoreModel.cs#L214), [MatchStats sum](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Core/Match/MatchStats.cs#L196), [per-range summary](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchHudPresenter.cs#L1038).

**Smallest safe correction:** Rank and display existing `threeMade`, `fourMade`, and `sevenMade` fields locally. Preserve generic total-shot telemetry. Backend mode-ranking semantics require separate confirmation; do not silently change a public metric.

### F3 Failed Spot-Up attempts can become fastest records

**Affected:** 7, 8, 9; also 25's current timeout/death path. **Evidence level:** static completion/persistence/query trace.

**Reproduce:** Enable enemies on a compatible arena, die shortly after starting while markers remain, then inspect the fastest-time record.

**Impact and root cause:** A short failed attempt can outrank a completed course. Death uses ordinary score saving; elapsed time is recorded without successful-course eligibility; records sort ascending.

**Evidence:** [ordinary score save](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L578), [elapsed-time recording and inactive guard](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L777), [ascending queries](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/PlayerData.cs#L141).

**Smallest safe correction:** Gate completion-time high-score and submission eligibility on successful course completion. Keep all-time stats/XP separate. Do not encode failure as zero time. Historical record repair is a separate data-policy decision.

### F4 Shared hoop state can drop another ball's make

**Affected:** 16 with CPU shooters, 23, 26. **Evidence level:** static deterministic call-sequence failure; physics frequency unmeasured.

**Reproduce:** Have distinct balls cross the same hoop during one frame/physics sequence. After the first `shotMade(runtime)`, a second distinct-runtime call before `Update` returns immediately. Shared upper/lower trigger flags can also be consumed by the first ball.

**Impact and root cause:** Legitimate human/CPU makes can be lost. The collision latch and crossing flags belong to the hoop rather than a ball/attempt.

**Evidence:** [frame latch](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/basketball/BasketBallShotMade.cs#L100), [global make suppression](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/basketball/BasketBallShotMade.cs#L122), [lower trigger](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/basketball/BasketBallShotMadeCollision.cs#L9), [multiple spawned balls](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/SpawnCoordinator.cs#L312).

**Smallest safe correction:** Scope crossing/deduplication state to the ball/attempt in the existing hoop component. Preserve duplicate suppression for the same make; no event framework or CPU rewrite is needed.

### F5 Campaign ends at a scene disabled in build settings

**Affected:** 26. **Evidence level:** static route/build/transition inconsistency; no player build executed.

**Reproduce:** Build with the checked-in enabled scene list, reach the penultimate Cocaine Island round, win, and advance. The next authored entry resolves to `level_23_dev`.

**Impact and root cause:** The final round cannot load in that build configuration. The route includes Dev, its scene is disabled in `EditorBuildSettings`, and continuation directly calls `SceneManager.LoadScene`.

**Evidence:** [campaign entries](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/critical/player_data.prefab#L52), [disabled Dev scene](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/ProjectSettings/EditorBuildSettings.asset#L101), [direct transition](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/menu_end_round/EndRoundMenuManager.cs#L290), [enabled-scene build helper](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Level5/Editor/LocalVersusCertificationBuild.cs#L46).

**Smallest safe correction:** Reconcile the route with an approved shipping endpoint and validate route/build membership. Do not automatically enable a developer scene. Boneyard's hidden status is a separate content-policy question.

### F6 Cage Match lacks an ordinary end summary

**Affected:** 22. **Evidence level:** static presentation branch omission.

**Reproduce:** Finish Cage Match by ordinary player death rather than the special idle-death override.

**Impact and root cause:** The end summary remains empty. Live Cage scoring exists, but `GetDisplayText` has a Battle Royal branch and no Cage branch.

**Evidence:** [live Cage HUD](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchHudPresenter.cs#L954), [summary dispatch](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchHudPresenter.cs#L1018).

**Smallest safe correction:** Add a Cage summary using existing kill/survival stats and presentation infrastructure, without changing scoring or persistence.

### F7 Descriptions advertise different timer lengths

**Affected:** 14, 24. **Evidence level:** static description/configuration mismatch.

**Reproduce:** Compare selection copy with resolved timers. Consecutive shots advertises two minutes but runs 60 seconds; 7 point contest advertises 100 seconds but runs 90.

**Impact and root cause:** Players receive misleading instructions because copy and authored timer values diverge.

**Evidence:** [mode 14 description and timer](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_14.prefab#L50), [mode 24 description and timer](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_24.prefab#L50).

**Smallest safe correction:** Correct copy to current execution unless a timing change is separately approved.

### F8 Contest descriptions promise an inactive time bonus

**Affected:** 16, 17, 18, 24. **Evidence level:** static descriptions and inactive implementation.

**Reproduce:** Clear a contest with time remaining and inspect resulting points/bonus.

**Impact and root cause:** The advertised remaining-time bonus is absent. The implementation is commented out, and no active equivalent was found.

**Evidence:** [mode 16](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_16.prefab#L50), [17](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_17.prefab#L50), [18](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_18.prefab#L50), [24](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_24.prefab#L50), [inactive bonus](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L863).

**Smallest safe correction:** Correct descriptions, or approve a formula before implementing it narrowly. Do not reactivate an obsolete formula by assumption.

### F9 Pocket activation differs from its description

**Affected:** 15. **Evidence level:** static description/rule discrepancy plus shared scoring/stats checks.

**Reproduce:** Make the first qualifying three/four/seven-pointer and compare awarded points against “after 3 consecutive” activation copy.

**Impact and root cause:** Bonus activation occurs earlier than advertised. Production scoring explicitly preserves a zero threshold, whose source comment identifies nonzero behavior as a separate gameplay decision.

**Evidence:** [mode 15 description](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_15.prefab#L50), [preserved zero threshold](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/basketball/BasketBallShotMade.cs#L21).

**Smallest safe correction:** Decide whether copy or threshold is intended. A zero-to-three change is not an outcome-preserving cleanup.

### F10 Arcade uses the Free Play record projection

**Affected:** 98. **Evidence level:** static HUD/projection/persistence trace.

**Reproduce:** Establish different Arcade and Free Play longest-shot records, then launch Arcade. Its HUD reads the Free Play record. A qualifying Arcade make invokes the Free Play projection callback.

**Impact and root cause:** Arcade displays the wrong record and mutates shared longest-shot state through the Free Play projection path.

**Evidence:** [separate record queries](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/PlayerData.cs#L137), [shared HUD branch](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/Level5Match/MatchHudPresenter.cs#L990), [projection write](https://github.com/sweat-this/level5/blob/c4353a905d845f65e83b0a1c253168e7264d4b8b/Assets/Scripts/game%20manager/GameRules.cs#L1018).

**Smallest safe correction:** Make the existing projection/read path mode-aware while preserving the approved all-time-stat policy. This does not establish that Arcade inserts mode-99 `HighScores` rows.

## Risks and requirements that remain unresolved

| Topic | Supported conclusion and next evidence |
| --- | --- |
| Battle Royal victory | Objective is LastPlayerStanding, while execution replenishes contestants and displays survival/kills. A finite winner requirement is missing. Clarify intent before proposing elimination architecture. |
| Campaign hidden arenas | Boneyard inclusion is a content-policy question. Dev's disabled build membership is separately confirmed as F5. |
| CPU contest participation | Mode 16 permits CPU shooters. Execute AI interaction with authored contest markers before certifying it. |
| Expiry fairness | Grace observes primary-player ball state. Whether CPU shots deserve equivalent grace requires policy and a boundary reproduction. |
| Seven-point capability | Mode 24 derives an explicit requirement; modes 4/25 do not. All current basketball arenas author the line, so no current arena failure is established from that omission alone. |
| Missing components | Hoop binding failures can suppress scoring; marker binding failures disable markers; malformed/zero courses and unchecked component accesses can prevent completion. Inspect/execute concrete scenes before calling them broken shipping content. |
| Modifier compatibility | Requested enemies are not equivalent to enemies-only arena filtering. Basketball-only arena 22 plus enemies requires execution before claiming failure. |
| Arcade exit saving | Arcade does not enter Free Play's explicit exit-save condition. Persistence of unfinished Arcade sessions is unspecified. |
| Persistence faults | Existing idempotency is source-backed, but disk exhaustion, process death, and live Backend concurrency were not certified. |
| Level progression | Completion/unlock semantics and durable integration remain separately owned work. Do not add a new progress authority during mode repair. |

## Existing issue ownership and dependencies

The inspected issue inventory and relevant bodies do not directly own F1–F10. The following existing issues own adjacent capabilities and must not be duplicated. No new issue was opened.

| Existing work | Relationship |
| --- | --- |
| [#278](https://github.com/sweat-this/level5/issues/278) | Arena correction/drift detection landed in #280. Mode capability output still depends on #234/#235. |
| [#232](https://github.com/sweat-this/level5/issues/232), [#233](https://github.com/sweat-this/level5/issues/233) | Hosted CI recovery and validation authority |
| [#270](https://github.com/sweat-this/level5/issues/270) → [#271](https://github.com/sweat-this/level5/issues/271) → [#272](https://github.com/sweat-this/level5/issues/272) | Completion/unlock decisions, durable level progress, selection integration; coordinate campaign decisions |
| [#273](https://github.com/sweat-this/level5/issues/273)–[#276](https://github.com/sweat-this/level5/issues/276) | Residual runtime-state retirement; not a prerequisite to all mode fixes |
| [#238](https://github.com/sweat-this/level5/issues/238)–[#245](https://github.com/sweat-this/level5/issues/245) | Local simultaneous mode certifications; different launch/result paths from these ordinary single-player defects |
| Closed [#54](https://github.com/sweat-this/level5/issues/54), [#69](https://github.com/sweat-this/level5/issues/69), [#70](https://github.com/sweat-this/level5/issues/70), [#71](https://github.com/sweat-this/level5/issues/71) | Historical CPU shot choice/data/Hardcore fixes; no unsupported reopening |
| Closed [#185](https://github.com/sweat-this/level5/issues/185) | Competitive-result exclusion; preserve it |
| Closed [#39](https://github.com/sweat-this/level5/issues/39) | Existing progression boundary; no replacement authority |
| Closed [#41](https://github.com/sweat-this/level5/issues/41), [#43](https://github.com/sweat-this/level5/issues/43) | Existing authored export and provenance infrastructure |
| Closed [#63](https://github.com/sweat-this/level5/issues/63) | Historical CI issue; current recovery belongs to #232/#233 |

## Prioritized issue-sized remediation

| Priority | Proposed scope | Findings and regression focus |
| --- | --- | --- |
| P1 | Reject incomplete Spot-Up completion records | F3; success versus death/timeout; local record and Backend enqueue eligibility |
| P1 | Restore authored Spot-Up 7s course | F1; seven-point markers, make quotas, early completion |
| P1 | Preserve independent makes at a shared hoop | F4; distinct balls versus duplicate callbacks for one make |
| P1 | Reconcile campaign endpoint with shipping scenes | F5; approved route, transition, terminal aggregate save |
| P1 | Correct range-specific local rankings | F2; wrong-range makes cannot improve records |
| P2 | Isolate Arcade record presentation | F10; distinct mode-98/mode-99 records and approved all-time policy |
| P2 | Restore Cage end summary | F6; ordinary death and idle override |
| P2 | Reconcile mode instructions with approved execution | F7–F9; copy-only correction unless rule changes are approved |

Each slice should use existing owners and focused regression seams, followed by execution of an affected real mode. Full-mode certification remains separate from static repair. No catalog, scoring-system, CPU-architecture, or compatibility rewrite is needed.

## Review passes and plan refinements

### Correctness and edge cases

The first review rechecked menu provenance, factory-derived objectives, combined flags, qualifying versus aggregate stats, death/timeout result eligibility, concurrent CPU/human ball detection, and campaign continuation.

Refinements:

- Availability comes from loaded authored prefabs, not constants.
- Conversion parity is distinct from gameplay correctness.
- Mode 18 and mode 22 combined flags are load-bearing.
- The campaign concern separates Boneyard content intent from a concrete Dev route/build inconsistency.
- Existing CPU selection fixes are current and covered; the historical four-point collapse is not a new finding.
- Mode 15's zero threshold is explicitly preserved behavior; only the description/rule discrepancy is certain.

### Scope and ownership

The second review checked duplicated work, unsupported conclusions, and avoidable architectural changes.

Refinements:

- Do not certify complete modes from component tests.
- Do not infer a finite Battle Royal winner requirement or introduce an elimination manager.
- Do not retune timers, bonuses, or Pocket threshold as cleanup.
- Do not enable developer scenes, multiplayer modes, or arenas.
- Do not replace progression, SQLite, result, CPU, or legacy-bridge authorities.
- Keep CI, progression, and generated capability work with existing issues.
- Keep old-record repair and Backend ranking semantics separate from local query fixes.

## Proposed next implementation issue

**Title:** Single-player — restore Spot-Up 7s authored marker course.

This is a bounded authored configuration repair that can precede broader certification without deciding new clock or scoring rules.

### Scope

Repair the mode-25 marker requirement through its existing authored source and regenerate the existing characterization. Preserve stable mode identity, public contracts, current clock, and Spot-Up make-quota behavior.

### Acceptance criteria

- Stable ID 25 and the existing countdown remain unchanged.
- The resolved definition requests seven-point make-quota markers.
- SevenPointLine compatibility derives from that requirement.
- Missed attempts do not advance the make quota.
- Completing the final required marker requests ending once.
- Other Spot-Up modes and point-contest attempt semantics retain their behavior.
- Characterization is regenerated through the existing exporter; the drift check passes.
- F3 remains a separate result-eligibility repair. This slice does not claim failed-attempt ranking correctness.
- Gameplay remains Implemented–Not Certified until the repaired authored path is actually executed and its completion/result behavior is verified.

### Regression strategy

Extend existing authored-data/configuration assertions for mode 25. Exercise seven-point marker make quotas and final-marker completion through existing marker/session seams. Execute a focused real-mode PlayMode course on an existing compatible arena, including timeout/death behavior. Run repository validation and affected parity/characterization fixtures. Do not add a generic characterization framework.
