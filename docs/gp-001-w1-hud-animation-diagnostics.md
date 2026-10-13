# GP-001 W1 — HUD fallback and combat animation diagnostics

## Baseline and disposition — 2026-10-13

Audited `sweat-this/level5` **`28c71f324ecf10df6f7585c516c259e3127d93f1`**. Fresh `git fetch origin dev` established identical HEAD, `origin/dev`, and FETCH_HEAD. The initial `dev` checkout was clean; existing worktrees were inventoried and left untouched. GitHub reported no open PR targeting `dev`, and no GP-001 issue or overlapping W1 fix was found. The user subsequently authorized review remediation, commit, push and a PR targeting `dev`; the implementation branch is `fix/gp001-w1-hud-fallback`. No issue closure or merge is included.

| Diagnostic | Evidence-backed disposition |
| --- | --- |
| HUD fallback creation | Intentional compatibility, but **a real fallback-parent defect was reproduced and corrected**: correct Versus CPU score labels could be hidden beneath an inactive damage-popup canvas. Retain fallback creation and its warning. |
| Dracula animation transition | **Benign for the measured combat paths; retain behavior.** An invalid, ignored legacy edge exists alongside a valid conditional jump edge. Production attack events, damage reaction, knockdown and recovery executed successfully. Physical movement/jump and full combat playability remain unverified. |

Negative-scale/negative-size BoxCollider warnings are explicitly accepted and out of scope. No collider, transform, physics setting, scene boundary or basketball ground-check asset was investigated or changed to eliminate them. They did not fail the focused probes. No warning suppression was added.

## HUD root cause and correction

The exact diagnostic is:

```text
The match HUD created fallback text objects for this legacy scene: display_p1_score, display_p2_score, display_p3_score, display_p4_score
```

`MatchHudPresenter.Initialize` resolves assigned text references, then named scene text (including inactive objects), then creates missing text. All six names in `RequiredHudObjectNames` are authored by `critical/GameManager.prefab`: `display_score`, `display_current_score`, `display_high_score`, `money_display`, `money_ball_enabled`, and `other_message`. The validator deliberately checks these six; the four legacy roster panels are supplied at runtime. Their absence alone is not incomplete scene authoring.

The production catalog/builder/configured-scene seam launched Versus CPU (23), Scrapyard (`level_01_scrapyard`), Johnny B and CPU Ian. The original `FindOrCreateHudRoot` returned the first scene Canvas enumerated by `Resources.FindObjectsOfTypeAll<Canvas>()`. In the failing execution that was **`player_damage_display_text`**, whose hierarchy was inactive. Both panels had correct injected values (`Player 1 / Johnny B / points : 9`, `CPU 2 / Ian / points : 3`) but `activeInHierarchy == false`. The other two panels were correctly unused. This establishes a presentation defect independently of the warning itself; enumeration order is not a reliable composition contract.

The correction adds one preference inside the existing `FindOrCreateHudRoot`: find a canvas containing one of the six known match HUD texts before using the existing general fallback. This selects the authored match `Canvas` in the reproduced scene. It also works when `display_score` is absent but another authored match HUD text remains. Existing no-authored-HUD fallback behavior is retained. No replacement HUD architecture or new state owner is introduced.

After the fix, both panels are active on the authored canvas, with built-in fonts, nonzero generated text geometry, correct labels/scores, and disabled raycast targets. The recorded 640×480 Game view coordinates place both panels on screen. Reinitialization reuses the four objects. Production scene reload destroys the prior objects and produces exactly four replacements, with unused slots hidden; forced timeout renders the existing winner summary and persists exactly one result. Scores and timeout are injected presentation/lifecycle inputs, not evidence of physical shots or a played competition.

## Combat diagnostic and actor attribution

The exact diagnostic is:

```text
Asset 'johnny_dracula_animation_controller': Transition 'idle -> inair' in state 'idle' doesn't have an Exit Time or any condition, transition will be ignored
```

The base controller has GUID `70cd97dc950e0b140b37b0b41c80f116`. Its idle state (`1102218538063755998`) references transition **`1101839547668062166`**, whose conditions are empty and `m_HasExitTime` is zero. Its destination is the `inair` sub-state machine (`1107088982956213824`). This is an invalid ignored edge, not a missing state or attack-timing diagnostic. A separate idle transition (`1101635520626248118`) uses `jump == true` and targets the jump state (`1102755987383976072`) directly. Adding exit time or a guessed condition to the ignored edge would create new behavior without evidence that it is needed.

The override controller (`6653c42ac0b1ff1488272401dd57e015`) is referenced by the authored human and CPU Dracula prefabs. The warning also occurs during production start-menu/catalog loading, before gameplay actors are spawned; its presence does not establish that an attacking actor failed. A dedicated probe selected the actual human **`player_johnny_dracula(Clone)`**, using **`johnny_dracula_animation_controller_override`**, in Battle Royal (21), **`level_17_rumble_pit`**. This was a production actor, not a synthetic Animator fixture.

The probe called the existing `PlayerAttack` ability, observed its actual Animator attack state and animation-event attack-box activation, then observed the box disabled after the attack. It applied one point through `PlayerHealth.TakeDamage` and confirmed reduced health; the API's return value denotes death, not successful hit acceptance. Separate explicit reaction flags exercised the ordinary controller/coroutine boundaries: both damage and knockdown states ran, their flags and movement lock cleared, and Rigidbody constraints returned to `FreezeRotation`. No Error/Exception/Assert messages were captured in that combat probe. This does not prove an enemy hit was physically earned, balance, controller input, physical movement/jump, every character or every animation.

**D3 remains separate and unresolved.** PR [#294](https://github.com/sweat-this/level5/pull/294), merged as `b17a916aa9dcfb0229ad72af661f9e20cc953b55`, corrected D1's CPU shared-overlay ownership and explicitly retained D3. CPU/defender `PlayerAnimationEvents` missing-human-controller and defender ShotMeter/null-actor composition are D3 responsibilities. The W1 CPU probes expect only the exact known D3 error and reject other captured errors/exceptions; they do not repair role composition or globally ignore logs. No dedicated D3 issue/owner was found, so no human ownership is invented.

## Completed reviews

**Pass 1 — correctness.** Initially proposed retaining fallback creation unless scene execution demonstrated impact. Rejected warning frequency and absence of four optional authored panels as proof of a defect. The inactive-popup reproduction changed the disposition: repair the root selection, not scene authoring or score formatting. Corrected diagnostic-only assumptions about inactive `Text.canvas`, `TakeDamage`'s return value, and Unity's obsolete instance-ID API. Retained the combat edge because real attack/events/recovery passed and a valid jump route exists. No collider attribution or warning cleanup was included.

**Pass 2 — architecture and regression.** Kept `GameRules`/match models as score/result/persistence owners and `MatchHudPresenter` as presentation owner. The twelve-line production addition changes only startup canvas selection; no public API, serialized field, GUID, prefab, scene, animation asset, package or attack timing changes. Reused existing fixtures, database isolation, snapshot/restore and runner-scene cleanup. Added missing-summary coverage and reinitialization/reload checks. Rejected D3 repair, generic scene discovery infrastructure, new managers, broad log suppression and speculative animation cleanup. Final diff review found no material additional correction.

## Changes

- `Assets/Scripts/game manager/Level5Match/MatchHudPresenter.cs`: prefer an authored match HUD canvas for fallback text.
- `Assets/Tests/Editor/Level5MatchHudPresenterCompositionTests.cs`: two cases for an unrelated inactive popup, an authored HUD with/without summary text, visible fallback objects and repeat initialization without duplicates. A preflight skips these cases before creating objects when any match HUD text already exists in a loaded scene; two additional cases verify that refusal preserves existing text and creates nothing.
- `Assets/Tests/PlayMode/Level5PostRemediationCertificationPlayModeTests.cs`: one production Versus CPU regression for actual canvas binding/generated geometry, labels/values, unused slots, reinitialization, reload cleanup, timeout summary and local result persistence. Reuses the fixture's disposable-company prerequisite and strict D1/D3 log policy.
- This report, documentation index link and a follow-up link in the existing certification report. Temporary combat/hierarchy/screenshot diagnostics remain only in ignored evidence, and were removed from the executed disposable source before final EditMode validation.

## Validation and retained evidence

Unity **6000.5.7f1 (017862109af0)**, pinned by `dev`. Execution reused `TestResults/gp001-post-2026-10-12/project`, refreshed from current tracked source plus the scoped changes. Its disposable company/product directory protects normal player data. Existing fixture isolation and an external wrapper snapshot/restored all twelve root/account queue/session paths (primary, `.bak`, `.tmp`); every invocation verified **12/12** prior contents or absence restored. Working-project settings were untouched.

Artifacts are ignored under `TestResults/d1/W1-*` and `TestResults/gp001-w1/`. Each Unity invocation retains its full argument array (`*-command.json`), log, process exit/duration and preservation JSON. XML is present only where tests executed.

| Check | Actual result |
| --- | --- |
| Repository validation | **PASS**, exit 0. |
| Initial diagnostic compilation, `W1-Production` | **No tests executed**, Unity exit 1; diagnostic-only `GetInstanceID` was an obsolete-error API. Corrected without production changes. |
| First compiled diagnostics, `W1-ProductionFixed.xml` | **0 passed / 2 failed**, exit 2. Unsupported probe assumptions about `TakeDamage` and `Text.canvas`; attack state/events already observed. Not counted as passing validation. |
| Corrected original-behavior diagnostics, `W1-Diagnostic.xml` | **1 passed / 1 failed**, exit 2. Combat passed; HUD reproduced inactive `player_damage_display_text` parenting. |
| Fixed production checks, `W1-FixedHud.xml` | **6 passed / 1 failed**, exit 2. The committed Versus CPU regression and all five existing combat-summary cases passed. Only the temporary capture probe failed because Unity batch mode rejects `WaitForEndOfFrame`. The whole invocation is not called a pass. |
| Corrected temporary HUD probe, `W1-HudCapture.xml` | **1/1 passed**, no skips, Unity exit 0. Correct parent, active/generated text, on-screen geometry, no duplicate/stale panels, reload and winner summary/result checks. `CaptureScreenshot` produced no image in batch mode; no screenshot review is claimed. |
| Pre-review EditMode, `W1-EditMode.xml` | **55/55 passed**, no skips, Unity exit 0. Entire initial HUD composition fixture, including the two new cases, plus `EveryGameplaySceneProvidesTheObjectsItsManagersLookUpByName` over enabled gameplay scenes. Temporary diagnostics were removed before compilation. Review-remediation results below supersede this test-source snapshot. |
| Final diff/scope inspection | **PASS**; no unrelated asset/settings changes or warning suppression. |
| Hosted CI | **Unavailable**: Repository Quality workflow `327878811` remains `disabled_manually`. Existing #232/#233 own recovery/required-check policy. |

Actual local wrapper commands below use **cmd.exe** (semicolons belong to the test-filter argument):

```text
git fetch origin dev
git rev-parse HEAD origin/dev FETCH_HEAD
python TestResults/gp001-w1/audit.py static
python TestResults/gp001-w1/audit.py prepare
python TestResults/gp001-w1/audit.py run Production PlayMode W1HudProductionFallbackRenderingAndReload;W1DraculaProductionAttackDamageAndRecovery
python TestResults/gp001-w1/audit.py run ProductionFixed PlayMode W1HudProductionFallbackRenderingAndReload;W1DraculaProductionAttackDamageAndRecovery
python TestResults/gp001-w1/audit.py run Diagnostic PlayMode W1HudProductionFallbackRenderingAndReload;W1DraculaProductionAttackDamageAndRecovery
python TestResults/d1/tools.py validate
python TestResults/gp001-w1/audit.py prepare
python TestResults/gp001-w1/audit.py run FixedHud PlayMode W1HudProductionFallbackRenderingAndReload;VersusCpuFallbackScoresRenderOnMatchHudAndDoNotSurviveSceneReload;Level5CombatSummaryPlayModeTests
python TestResults/gp001-w1/audit.py run HudCapture PlayMode W1HudProductionFallbackRenderingAndReload
python TestResults/gp001-w1/audit.py clean-diagnostics
python TestResults/gp001-w1/audit.py run EditMode EditMode Level5MatchHudPresenterCompositionTests;Level5SceneContractTests.EveryGameplaySceneProvidesTheObjectsItsManagersLookUpByName
git diff --check
```

The wrapper uses `-batchmode -force-d3d11` for PlayMode and `-batchmode -nographics` for EditMode, with `-projectPath`, `-runTests`, `-testPlatform`, `-testFilter`, `-testResults` and `-logFile` recorded per invocation. Its own process may exit zero after printing a Unity failure: the reported Unity exit JSON and NUnit XML, not wrapper success, determine results.

## Remaining evidence and PR-ready summary

Human review of legibility, overlap and readability at supported resolutions remains **NOT RUN**; generated vertices and on-screen rectangles are narrower evidence. Ordinary menu/device navigation, physical attacks/damage/movement/jump, four-participant layouts, mobile and full matches remain unverified. No fresh player build was run: this startup parent-selection change has no identified stripping/platform-dependent behavior; earlier player results are not relabeled as validation of this change. The all-missing-authored-HUD compatibility route is retained, not newly certified. D3 and disabled CI remain separately owned follow-ups. Known collider warnings remain accepted.

**PR-ready summary:** Versus CPU's runtime score panels could attach to an inactive damage-popup canvas and become invisible despite correct scores. Prefer the existing authored match HUD canvas, preserving legacy fallback creation, score/result/persistence ownership and diagnostics. Add focused composition/isolation cases and a real-scene rendering/reload/summary regression. Dracula's ignored legacy transition remains unchanged after successful production attack/event/damage/knockdown/recovery observations. Original focused EditMode: 55/55; six affected PlayMode cases passed in a mixed run whose separate capture-only failure was subsequently repaired and passed. Review-remediation validation is recorded below. Human visual/device and fresh player-build evidence remain unverified.

## Review finding remediation

Senior review reproduced one defect in the new EditMode coverage: with Scrapyard already open, the missing-summary case resolved that scene's `display_score`, cleared its text, added four fallback panels to the foreign canvas, then threw at its own missing panel. Fixture teardown did not remove those foreign children. `W1-ReviewMissingSummaryIsolation.xml` records a **1/1 passing diagnostic, Unity exit 0**, explicitly confirming those unwanted effects; it is not a passing gameplay or original-fixture result. The initial complete-HUD diagnostic did not reproduce the problem and failed its expected-mutation assertion; `W1-ReviewIsolation.xml` retains that **0/1, Unity exit 2** result.

The correction is test-only: before allocating any fixture object, the scene-sensitive cases check for any of the ten names initialization resolves. If one already exists in a valid scene, `Assert.Ignore` explains the prerequisite and leaves loaded scenes untouched. This avoids discarding unsaved scene state, broadening production lookup behavior or introducing a scene-isolation framework. Tests still execute normally in the clean disposable project. Two additional parameterized cases catch the deliberate skip and verify existing HUD text and fixture allocation counts remain unchanged. The production fix and PlayMode regression are unchanged, so their still-current evidence above is reused.

Remediation commands (cmd.exe):

```text
python TestResults/gp001-w1/audit.py clean-diagnostics
python TestResults/gp001-w1/audit.py run IsolationFixEditMode EditMode Level5MatchHudPresenterCompositionTests;Level5SceneContractTests.EveryGameplaySceneProvidesTheObjectsItsManagersLookUpByName
python TestResults/gp001-w1/audit.py run IsolationFixOpenScene EditMode W1ReviewIsolationDiagnostic
```

`W1-IsolationFixEditMode.xml`: **57/57 passed, no skips, Unity exit 0**, including both original composition cases and both new safety regressions. `W1-IsolationFixOpenScene.xml`: **1/1 passed, no skips, Unity exit 0**; its two nested invocations deliberately skip before mutation while all **41** Scrapyard text objects, their identities and values remain unchanged after fixture teardown. These nested skips are expected control-flow assertions inside a passing diagnostic, not silently omitted coverage. Both invocations restored all twelve external persistence paths. Temporary open-scene diagnostics are retained with ignored evidence and removed from the disposable source after execution; no production scene is saved. Final source/whitespace review and repository validation passed after remediation. The earlier production PlayMode evidence remains valid because production and PlayMode source did not change during remediation.
