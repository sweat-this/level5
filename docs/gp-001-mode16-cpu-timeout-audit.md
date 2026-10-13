# GP-001: mode 16 CPU participation and countdown grace

Audit date: 2026-10-13. Repository: `sweat-this/level5`. Fetched `dev`, starting HEAD and `origin/dev`: **`bee89a222ea9c1c4507a1b1cf5f2b3885a75121c`**. Re-fetched immediately before implementation; the SHA was unchanged. Unity **6000.5.7f1**, revision **017862109af0**.

Final `git ls-remote origin refs/heads/dev` returned the same SHA. Repository validation was rerun after the final source/docs changes and exited 0.

**Outcome:** CPU participation is real, with separate participant scores and shared marker counters. Baseline countdown expiration could end an airborne CPU shot while the primary human was idle. The owner supplied the missing countdown policy during this audit: **“if it's shot by player or cpu before zero, it gets time out grace.”** The focused correction implements that rule for mode 16. Shared versus independent course progression remains a product decision; the existing shared course is preserved. This is not full gameplay certification.

## Baseline, coordination and requirements

Read `AGENTS.md`, the documentation index/presets, [GP-001 characterization](gp-001-single-player-characterization.md), [post-remediation report](gp-001-post-remediation-certification-2026-10-12.md), [shot lifecycle](shot-lifecycle.md), and [persistence boundaries](persistence-boundaries.md). Applied the repository debug, implementation, runtime-validation and two-pass review workflows. No agent delegation was used.

The starting worktree was clean. Existing worktrees were inventoried and left untouched. Open PR inventory returned **none**. Open issues were inventoried; all-state `contest` and `GP-001` searches found no approved ordinary mode-16 course or timeout contract. Relevant ownership:

| Item | Relevance |
| --- | --- |
| [#245](https://github.com/sweat-this/level5/issues/245), [#244](https://github.com/sweat-this/level5/issues/244), [#222](https://github.com/sweat-this/level5/issues/222) | Future two-human simultaneous contest/Spot-Up certification. #245 says one participant must not complete another's marker state in that competition. It does not authorize independent courses for ordinary human-versus-CPU mode 16. |
| [#277](https://github.com/sweat-this/level5/issues/277) | Broader audit roadmap; not a replacement gameplay contract. |
| [#232](https://github.com/sweat-this/level5/issues/232), [#233](https://github.com/sweat-this/level5/issues/233) | Hosted validation authority. Live Repository Quality workflow query returned `disabled_manually`. |
| [#284](https://github.com/sweat-this/level5/pull/284) | Merged per-runtime/per-attempt hoop crossing and deduplication correction; present and preserved. |
| [#289](https://github.com/sweat-this/level5/pull/289), [#290](https://github.com/sweat-this/level5/pull/290) | Authored timer and inactive-bonus description corrections; present. |
| [#292](https://github.com/sweat-this/level5/pull/292) | Existing characterization/runtime probes; their primary-human timeout limitation was explicit. |
| [#293](https://github.com/sweat-this/level5/pull/293), [#294](https://github.com/sweat-this/level5/pull/294), [#296](https://github.com/sweat-this/level5/pull/296) | Queue serialization, CPU overlay startup and fallback HUD fixes; present. |
| [#295](https://github.com/sweat-this/level5/pull/295), [#297](https://github.com/sweat-this/level5/pull/297), [#298](https://github.com/sweat-this/level5/pull/298) | Recent Spot-Up copy, Arcade exit-policy and Battle Royal work; none resolves this countdown finding. |

The fifteen latest merged PRs, #284–#298, were inspected by title/merge inventory and relevant current implementation. No newer fix superseded the finding. The initial audit did not publish changes. Following a review with no material findings, the owner authorized committing, pushing and opening a PR targeting `dev`; no new issue or merge is part of this delivery. D3 role-composition diagnostics were recorded as existing scene noise and were not investigated or repaired.

## CPU contest data flow

1. [`mode_selected_16.prefab`](../Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_16.prefab) authors ID 16, a 70-second countdown, three-point markers/contest scoring, optional CPU shooters and ordinary `totalPoints` records. `GameModeDefinitionFactory` and `MatchConfigurationBuilder` produce validated rules and roster; the real builder accepted Johnny B + CPU Ian in Scrapyard.
2. [`SpawnCoordinator`](../Assets/Scripts/game%20manager/Level5Match/SpawnCoordinator.cs) gates CPU spawning/balls by `AllowsCpuShooters`. `GiveBall` binds the actual registry participant's ID, role, actor, rules and ball state. Human and CPU have distinct `GameStats`, `BasketBallState` and runtime identity. Slot zero remains the ordinary persistence/HUD owner.
3. [`AutoPlayerController`](../Assets/Scripts/player/Level5Player/AutoPlayerController.cs), `CpuShotSelectionPolicy` and `CpuScoreDeficit` select a shot range. `getClosestPositionMarker()` actually builds a range-relative position; it does not enumerate incomplete contest markers. The existing movement, shooting and meter coroutines run naturally.
4. [`BasketBallShotMarker`](../Assets/Scripts/basketball/BasketBallShotMarker.cs) resolves the exact colliding participant through `IBasketballParticipantStateProvider`. Occupancy and the shot's marker reference belong to that ball's state. Marker-local occupancy sets support presentation; they do not provide separate participant quotas.
5. [`BasketballShotPipeline`](../Assets/Scripts/basketball/BasketballShotPipeline.cs) starts the participant's attempt generation/range counters, captures its marker reference, and registers an attempt on that same marker. The marker's attempts/makes are shared. The first runtime reaching its quota is captured as `finalAttemptRuntime`; subsequent extra attempts do not replace it.
6. [`BasketBallShotMade`](../Assets/Scripts/basketball/BasketBallShotMade.cs) and the lower trigger authorize a make by matching ball runtime and attempt generation. `ShotScoring` applies points to that runtime's stats and updates the captured marker. Duplicate same-attempt crossings are suppressed. A normal uncontested fifth three-point attempt scores six points; controlled runtime proof retained this result.
7. Marker completion checks the captured final owner's actor/ball readiness, disables the marker once, decrements the shared `IShotMarkerSession` owned by `GameRules`, then requests ending if the last marker cleared. The legacy cause is `Unknown`. `MatchLifecycle` accepts the first ending, including when a timer request follows in the same frame.

**Natural evidence, distinguished from controlled callbacks:** baseline CPU Ian displaced **3.66451 units**, took **three shots**, scored **six points**, and contributed **three marker attempts** in 12 seconds. A separate controlled occupancy/make then added three CPU points without changing human points.

A fresh full natural countdown after the correction left the human idle and injected no movement, occupancy, attempts, makes or timer expiry. Ian took **23 shots** and scored **six points**. The common course fell from **five to four** remaining markers; Ian stayed at `left_corner` and continued registering attempts there after that marker completed. The timer naturally ended at **70.00018** seconds with `TimeExpired`; the primary saved duration was **69.95953** seconds. These clocks have different existing initialization owners; this change does not redefine either. This is bounded observation of one authored CPU/arena configuration, not proof of whole-course AI navigation or balance.

**Shared-course answer:** current code deliberately has one session owner and one counter per marker, and current docs explicitly describe that architecture. No approved gameplay requirement establishes whether this is the intended competitive experience. A CPU can consume attempts the human would otherwise take, clear markers, and contribute the last completion that ends the human's session before the human has personally attempted all 25 shots. A controlled CPU-owned completion reproduced the shared ending. The natural single-CPU observation cleared only one marker and did not end the whole course early. Independent courses have not been implemented.

## Countdown and result data flow

Baseline: `Timer.Update` increments elapsed time, calculates remaining countdown time and invokes `ReportTimeExpired` at zero. `GameLevelManager.Start` binds the live GameRules/game-over, consecutive-shot, primary-player and end-request adapters. `MatchEndConditions.TimeExpired` ordinarily returns `!primaryBall.Thrown && primaryHuman.Grounded`; mode 16 therefore ignored the CPU's flight. The separate streak rule is unchanged.

Corrected mode 16: composition additionally binds `registry.Participants` to the timer. Each actual human/CPU `Launch` records scaled `Time.timeAsDouble` on its own `BasketBallState`, after the Rigidbody launch application. Beginning an attempt or setting `Thrown` is insufficient: meter wind-up is not release. The first zero evaluation freezes the release deadline as `Time.timeAsDouble + timeRemaining`. Each subsequent evaluation reads the same deadline and the participants' own pending release times. A release must be **strictly earlier** to retain grace. No CPU-wide/global flag, replacement roster, marker session or end controller was introduced.

Ground/fence collision or retrieval clears the ball's pending flight. Rim contact retains it, because legacy `Thrown` clears on rim contact before a possible later make. A made shot's existing snapshot reset does not end the physical flight, preserving the allowance until it lands/retrieves. Starting a later attempt clears the preceding flight and records a new time only when actually released; a post-zero release cannot renew grace. The primary human's pre-existing landing allowance remains. Only mode 16 receives this binding; other timed modes still use their original predicate.

When expiration is accepted, `GameRules.RequestEnd` routes through `MatchController`/`MatchLifecycle`. `CaptureMatchEndState` freezes elapsed game time. `HandleMatchEnded` computes the existing primary result, persists locally to SQLite or the pending local store, then attempts Backend enqueue under existing session/eligibility rules. Work-completion flags and lifecycle completion retain their existing once-only semantics. CPU points are not added to the human's ordinary result. No save schema, record field, progression authority, backend DTO or result eligibility rule changed.

## Boundary reproduction

All controlled cases loaded the real mode-16 configuration and Scrapyard scene. They fast-forwarded the countdown, not `Time.time`; their saved elapsed durations near 0.03 seconds are expected and are not claimed as 70-second sessions. The natural run above provides separate real countdown evidence.

| Boundary | Baseline observation | Corrected evidence |
| --- | --- | --- |
| Human flight, human grounded | Zero left the match `Playing`; resolving the shot then expiration saved three human points. | Pre-zero human and CPU flights both retained eligibility. Actual human hoop crossings scored three once. |
| CPU flight, human grounded/idle | Frame 30 accepted `TimeExpired` immediately. A later CPU make still added three CPU points; the human result remained zero. | With both flights pending, actual human ground contact left the match playing for the CPU. Actual CPU hoop and ground callbacks scored three CPU points and then allowed `TimeExpired`. |
| Neither shooter released | Ordinary idle expiration accepted once. | Exact-zero solo release was ineligible and saved zero. EditMode also checked no release, exact zero, after zero and before zero. |
| Final marker near expiration | Controlled shared-course completion won with `Unknown`; the later timer could not replace it. | Four markers completed under controlled human readiness. CPU's fifth attempt on the last marker retained countdown grace, scored **six CPU / zero human** points, waited for captured CPU readiness, completed once, and retained `Unknown` against a later timer request. |
| Ending before later hoop callback | A controlled human make after `Ending` but before save raised live/saved points to three. Another attempt/make after `Completed` raised live points to six while persisted points stayed three. Duration and result ID stayed fixed. | Hoop/lifecycle/scoring finalization are unchanged; this remains a separately exposed integrity risk, not a repaired assertion. |

Real hoop/ground evidence uses manually positioned Rigidbody balls with actual `Physics.Simulate` and authored upper/lower triggers. It does not inject `shotMade` for that test. Final-marker and late-callback tests explicitly use controlled callbacks. Neither is physical player/input certification.

Representative exact evidence:

| Run/case | Frame, cause, primary result ID, duration and course |
| --- | --- |
| Baseline CPU flight | Zero frame **30**, `Ending/TimeExpired`; saved frame **31**, ID `37c3677832cc4bb6bc180f566bd6356f`, human points **0**, duration **0.03181538**, remaining **5**. |
| Baseline late human make | Accepted frame **99**; saved frame **100**, ID `1f538fcbdab34a0d87b0533aa0c7bf33`, points **3**, duration **0.0254944**. Live points **6** at frame **101**; same persisted ID/points/duration at frame **102**. |
| Corrected two flights/actual physics | Zero frame **61253**, `Playing`; one ending and completion; ID `c9dc281615ba4c088c747b88248fbfbc`, human points **3**, CPU points **3**, duration **0.02727127**, remaining **5**. |
| Corrected final CPU marker | Frame **61277**: zero `Playing`, then course `Ending/Unknown`; ID `b91e82ac02744eb9ac9867e6de8d712b`, human points **0**, CPU points **6**, duration **0.02797699**, remaining **0**. |
| Natural full countdown | `TimeExpired`; ID `b1b904c021dc491cbdcb2a4cca425351`, match ID `match-8050b6d61a8b4e5d80541f3750e172b1`, human points **0**, CPU points **6**, duration **69.95953**, remaining **4**. |

Each corrected persistence case asserted one score row, the exact same nonempty result ID after later frames, stored primary points, stored/frozen elapsed time equality, and rejection of a competing later cause. These guest runs had no authenticated Backend session; they establish local persistence, not server delivery or successful durable XP application. Existing progression repair warnings were retained.

## Findings, risks and decisions

| Classification | Problem/risk | Resolution or next decision |
| --- | --- | --- |
| Confirmed and corrected | Baseline primary-human-only expiry cut off CPU pre-zero flights. | Owner-approved parity rule implemented at timer composition and per-ball release/flight state. |
| Policy gate B | Shared quotas allow CPU participation to advance the human's course. AI does not naturally choose the next incomplete marker. | Decide whether ordinary mode 16 intentionally shares five attempts per marker and permits any participant's last completion to end the whole session, or requires participant-independent quotas/completion. Define the CPU's course-navigation responsibility before any such work. Current architecture is evidence of behavior, not sufficient gameplay approval. |
| Confirmed, retained follow-up | Hoop callbacks after accepted ending can change live stats and, before finalization, the saved score. First-end semantics freeze cause/time, not score. | Establish the score cutoff for already accepted death/course/timeout endings, then enforce it at the existing scoring/finalization boundary. Do not reopen lifecycle completion or silently migrate other modes. |
| Source risk, not a newly reproduced defect | Final-attempt doubling reads the marker's current attempt count at make time; shared overlapping attempts can change that count. | The controlled uncontested fifth attempt is proven. Concurrent fourth/fifth/extra-attempt scoring is not certified here; any correction needs explicit attempt-specific scoring evidence and scope. The captured completion owner remains intact. |
| Grace recovery limit | A released ball with no subsequent ground/fence/retrieval callback could retain grace. | New attempts cannot refresh grace. No arbitrary maximum-flight duration was invented; trapped/lost-ball recovery is unverified and would need an approved recovery rule if required. |
| Validation limitation | Existing marker-session PlayMode fixture stalled in ordinary menu entry. | Record BLOCKED separately. Existing marker ownership/session EditMode tests pass; configured mode-16 shared completion has fresh runtime proof. No menu harness rewrite. |

## Review pass 1: correctness

Initial proposal: let mode 16's timer inspect every participant's `Thrown` state. Rejected during review: repeated post-zero shots could prolong the match, meter wind-up sets `Thrown` before release, and rim contact clears it too early.

Refined proposal uses an actual release timestamp per ball and one frozen countdown deadline. Ground/fence/retrieval terminate the same flight; a new attempt replaces its eligibility rather than extending the old shot. Primary landing behavior remains. Tests cover both roles, multiple flights, exact-zero exclusion, no flight, new attempts, real crossing deduplication, actual landing, final-marker ownership, once-only completion and persisted frozen duration.

The review also separated course completion from timer grace and exposed the late-score race. Neither can be resolved by changing the timeout predicate. No claim that first-end acceptance makes later callbacks harmless remains in the plan/report.

## Review pass 2: architecture and scope

Reviewed the refined proposal against current state ownership and serialized composition. The existing registry remains the only spawned-participant authority. `BasketBallState` owns its ball's runtime release time; Timer owns only the zero deadline and reads those states. Existing `GameLevelManager.Start` supplies the mode-16-only reference after all Awakes. No new manager, global flag, interface/package, participant course, scene lookup, balance change or framework is required.

The public timer binding is additive; existing `BindMatchEndContext` callers remain compatible. Release time is runtime-only and is not a serialized/save contract. Existing marker-session ownership, final-attempt runtime capture, MatchRuntime/roster resolution, per-attempt hoop deduplication, MatchLifecycle first-ending rules and local-first result processing remain intact. No Unity GUID or serialized reference changed. Other timed-mode behavior has focused regression coverage. The final-marker probe explicitly preserves the legacy `Unknown` cause rather than introducing an unrelated cause migration.

Validation refinement: use a disposable project and the existing persistence isolation instead of changing normal project settings. After a legacy combined PlayMode run stalled, run the configured probes separately; do not count the aborted run or repair the unrelated harness. The two passes were performed sequentially by the primary agent, in the correctness/architecture order requested.

## Final implementation plan and modified files

Completed sequence: refresh/audit baseline → reproduce baseline boundaries → establish countdown policy → complete both reviews → re-fetch dev → add per-ball actual-release state and mode-16-only timer composition → run focused contracts and configured runtime/physics/persistence probes → record the remaining course/scoring policy gates.

| Modified file | Rationale |
| --- | --- |
| `Assets/Scripts/basketball/BasketballState.cs` | Runtime release timestamp and flight termination; reset eligibility for a new attempt. |
| `Assets/Scripts/basketball/BasketBall.cs` | Record actual human launch and clear flight at existing ground/fence/retrieval callbacks. |
| `Assets/Scripts/basketball/BasketBallAuto.cs` | Equivalent CPU lifecycle bookkeeping. |
| `Assets/Scripts/game manager/Level5Match/Timer.cs` | Read bound participant flights against a frozen zero deadline; preserve the existing path when unbound. |
| `Assets/Scripts/game manager/GameLevelManager.cs` | Bind the actual registry only for mode 16. |
| `Assets/Tests/Editor/Level5TimerMatchEndContextTests.cs` | Eight added timer regression cases, including both roles and other-mode preservation. |
| `Assets/Tests/PlayMode/Level5BasketballShotAttemptAndLaunchConvergencePlayModeTests.cs` | Assert both real launch coroutines write actual release timestamps. |
| `Assets/Tests/PlayMode/Level5PostRemediationCertificationPlayModeTests.cs` | Three configured mode-16 boundary tests with physical crossings/landing, controlled final-marker completion and SQLite assertions. |
| `docs/gp-001-mode16-cpu-timeout-audit.md` | This evidence and decision report. |
| `docs/shot-lifecycle.md`, `docs/README.md` | Document the mode-16 exception and index this audit. |

Local diagnostic scripts and baseline/natural probes remain ignored under `TestResults/gp001-mode16`; they are not production assets or permanent test infrastructure. No authored asset, schema or package changed. Publication uses `fix/gp001-mode16-final-shot-grace` with a PR targeting `dev`. The review requested no additional production fixes; the unresolved course/scoring policies remain separate follow-ups.

## Validation and reproducibility

Evidence directory: `TestResults/gp001-mode16/`, containing command argument arrays, logs, NUnit XML, exit-code JSON, baseline probe source and the natural full-countdown probe. The retained disposable project at `TestResults/gp001-post-2026-10-12/project` was refreshed from the tracked current baseline; stale untracked files under its copied source folders were removed before use. Its company/product remain the guarded audit namespace `level5-audit-20261012-14b2cdc` / `level5-gp001-f4-tests`; normal project settings were untouched. Existing SQLite/session/pending-result isolation protects primary, `.bak` and `.tmp` persistence files. SQLite is GUID-temporary. No normal player file was cleared.

| Check | Actual result |
| --- | --- |
| Repository validation | **PASS**, process **0**. |
| Initial baseline probe compilation | **FAIL**, process **1**, no NUnit XML. Diagnostic code initially used concrete `HasBasketball` members instead of the existing explicit `IShooterActor` interface; corrected only the probe. |
| `BaselineCorrected.xml` | **5 passed / 0 failed / 0 skipped**, process **0**. Characterization of existing behavior, including the exposed CPU cutoff and late-score race; not five assertions of conforming gameplay. |
| `FocusedEdit.xml` | **183 passed / 0 failed / 0 skipped**, process **0**. |
| `FocusedPlay.log` combined invocation | **BLOCKED/terminated**, process **1**, no NUnit XML. Existing `Level5ShotMarkerSessionCompositionPlayModeTests` menu entry repeatedly reported no primary character and returned to loading. Not a passing run. |
| `Mode16Play.xml` | **6 passed / 0 failed / 0 skipped**, process **0**. Three new boundary cases, two existing mode-16 probes and the solo overlay/composition regression. |
| `Natural70.xml` | **3 passed / 0 failed / 0 skipped**, process **0**. One temporary full natural mode-16 countdown observation plus the two existing actual-release coroutine tests. |
| Final diff/serialized-scope inspection | `git diff --check` **0**; only listed source/test/docs files modified. |
| Hosted CI | **NOT RUN**, workflow currently `disabled_manually`. |

EditMode breakdown: TimerMatchEndContext **19**, MatchLifecycle **19**, BasketballMarkerOwnership **62**, BasketballShotMarkerSession **8**, ShotScoring **17**, SharedHoop **22**, BasketballShotPipeline **26**, BasketballGameRulesFacadeGuard **2**, BackendV2MatchResultAdapter **8**. Counts are actual NUnit cases, not assertions. Successful checks were reused while their implementation stayed unchanged.

Expanded commands are saved in each `*-command.json`. Reproduction uses the pinned editor with `-batchmode`, `-projectPath` pointing at the disposable project, `-runTests`, and `-testResults`/`-logFile` pointing at the evidence directory. EditMode adds `-nographics`; PlayMode uses `-force-d3d11`. The permanent configured filter is `Level5PostRemediationCertificationPlayModeTests.Mode16;Level5PostRemediationCertificationPlayModeTests.SoloHumanRetainsProductionOverlayToggleBehavior`. The natural probe must be injected into the disposable fixture using the retained script before reproducing its case. Do not apply its audit company/product settings to the normal working project.

Not executed: a full physical human-versus-CPU course, all CPU characters/arenas/modifiers, physical devices, standalone/IL2CPP builds, live authenticated Backend submission, storage-failure retry injection, trapped-ball recovery or complete Unity suites. Existing D3 diagnostics and accepted negative-scale/legacy animation warnings remain separate. Passing controlled/engine-physics probes do not establish physical playability certification.

## Acceptance and PR-ready summary

Latest dev, real mode-16 composition, natural CPU participation, shared progression, baseline timeout asymmetry, final-attempt owner retention, score ownership, once-only completion and stable persisted duration have evidence above. The approved countdown correction and focused regressions are ready for review. Ordinary primary-result eligibility/local-first processing and serialized contracts remain unchanged. Solo composition remains covered; release-at/after-zero exclusion is the intentional owner-approved timing refinement.

Suggested PR title: **Honor pre-zero human and CPU shot grace in mode 16**.

Suggested description: Mode 16 previously ended at zero whenever the primary human was grounded with an unthrown ball, even while a CPU's shot was airborne. Record each ball's actual release/flight lifecycle and bind mode 16's timer to the existing participant registry so all pre-zero shots retain grace without new post-zero attempts renewing it. Preserve shared course progression, first-ending semantics and existing result/persistence contracts. Validation: repository check exit 0; 183 focused EditMode cases; six configured mode-16 PlayMode cases; three natural-countdown/release cases, all passing. The separate legacy marker-session PlayMode harness stalled; shared-course policy and late-callback score finality remain explicitly unresolved.
