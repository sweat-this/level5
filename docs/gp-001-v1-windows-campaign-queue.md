# GP-001 V1 — Windows campaign result queue

Follow-up to [the post-remediation audit](gp-001-post-remediation-certification-2026-10-12.md). The historical audit and [original characterization](gp-001-single-player-characterization.md) retain their original execution evidence.

**V1's automated Windows boundary is resolved.** The final affected Windows suite passes **13/13**, with no skips, through durable local saving, authenticated fake submission, retryable 503, SQLite reopening and menu return. Production causes and the required HTTP 200/public-contract refinements are recorded below.

## Current-state audit

- Fetched `origin/dev`; audited `55ad3a96fed38afc553b408b191508d4b2934aeb`, matching the clean working HEAD before this change.
- Latest merge: PR [#292](https://github.com/sweat-this/level5/pull/292), which records unresolved V1. Reviewed the recent F1–F10 merge lineage; no subsequent implementation existed. The live open-PR query targeting `dev` returned `[]`; a campaign issue search found existing progression-policy issue #270 and closed architecture issue #39, neither an implementation of V1.
- Traced the authored boundary fixture, `EndRoundMenuManager.saveGame`, campaign score conversion, `DBConnector.savePlayerGameStats`, submission eligibility/adapter, session store/bootstrap, coordinator, durable pending store, runtime transport composition, and existing session/coordinator/persistence/campaign tests.
- Unity Editor and Windows player: **6000.5.7f1 (017862109af0)**, Windows 10 x64, Direct3D11. Standalone uses the existing **IL2CPP** backend and configured managed stripping level `2`; those settings were not changed.

## Reproduction and first divergence

The unchanged victory test was run alone in the retained disposable project at `TestResults/gp001-post-2026-10-12/project`, refreshed from audited `dev`. Only that copy's company/product settings were changed, to company `level5-v1-20261012-55ad3a9` and product `level5-gp001-f4-tests`. The latter retains the existing scene-fixture namespace guard. Actual player saves are outside this namespace.

`OriginalWindowsPermitted.xml`: **0 passed / 1 failed / 0 skipped**, Unity exit **2**, test duration **13.398 seconds**. The failure is unchanged: retryable count **0**, expected **1**, after the exactly-one local aggregate and repeated-save assertions passed. SQLite reopen and return-to-menu assertions were not reached. The first sandboxed launch instead exited **1** during licensing initialization and produced no test XML; that is an environment failure, not a gameplay reproduction. The permitted launch used the available license and reached the test.

For the final combined suite, the three pre-existing runtime probes require the exact company `level5-audit-20261012-14b2cdc`. The first combined invocation in the new V1 namespace passed ten cases but skipped those three; that is not counted as a complete suite. The final invocation reused their required disposable company, retaining product `level5-gp001-f4-tests`, with an **outer** read-all-before-mutation snapshot/restore of all twelve primary/backup/temporary paths in addition to fixture cleanup. `FullWindowsGuarded-preservation.json` verifies every prior present/absent state was restored; original bytes were kept privately in memory and were not logged. Neither the guard nor real player configuration was changed.

Temporary diagnostics were then applied **only to the disposable source copy**, without changing control flow or adding waits. `DiagnosticWindows.xml` again failed the same assertion: **0/1 passed**, exit **2**, test duration **16.113 seconds**. Safe observations:

| Boundary | Direct observation |
| --- | --- |
| Campaign durability | Final action completed; SQLite save succeeded; `isGameSaved=True`; score `82b23d5edbd0453a86fcd0171fabebe4`, mode **26**. Existing duplicate-save checks still found one row. |
| Identity/eligibility | Session installed after initial scene load under owner `ab33416c-9435-4685-878c-689c00879c46`; no intervening session change before failure. At submission, the same owner was present, access was outside the existing 30-second refresh window, and both active competitive-attempt predicates were false. |
| Adaptation | Accepted that exact score ID as `ClientResultId`. |
| Synchronous queue write | Enqueue returned **Added**; the primary file contained one entry for the expected owner and ID, mode **26**, level **23**, `TotalPoints=200`, `definitiveFailure=false`. |
| **First divergence: reopening the queue** | Newtonsoft failed with `Cannot deserialize readonly or fixed size dictionary: System.Collections.Generic.IReadOnlyDictionary` at `entries[0].request.metrics.TotalPoints`. Both primary and backup validation fail; `Load` consequently exposes an empty queue. |
| Delivery | Runtime still referenced the fixture's fake transport, which recorded **zero** requests. No HTTP response/classifier or removal was reached; 503 was not the cause of this failure. |

The stripped `Level5.BackendV2.dll` was copied into ignored evidence and inspected. `SubmitMatchResultDto` retained only its parameterized constructor, including the `IReadOnlyDictionary<string,double>` parameter. Its parameterless constructor existed in source and Editor assemblies but was absent from the stripped player assembly. Newtonsoft therefore selected the remaining constructor rather than populating the concrete `Dictionary<string,double>` property through the intended default-constructor path. `PendingMatchResult` also lost its unused default constructor, but its remaining constructor can deserialize its parameters; preserving that additional constructor was unnecessary.

**Classification: production defect in Windows player managed-stripping/serialization behavior (category 8), not fixture session initialization, ownership mismatch, competitive gating, adaptation, failed writing, or response classification.** A locally successful score alone did not prove a readable authenticated submission queue.

### Required HTTP 200 control and refinement

Preserving the request constructor first made both focused Windows cases pass, including the original victory through SQLite reopening and menu return. The next required control used the existing restart-recovery case in a fresh Windows process. `RestartOwnerWindows.xml` recorded **0/1 passed**, exit **2**: exactly one fake request was sent, but a valid HTTP 200 response was classified `MalformedResponse` and the entry remained pending. This is a second failure of reflection-only construction, not a reason to alter the classifier.

The same stripped assembly had **no constructor** for `MatchResultResponseDto`. A local .NET metadata/deserialization probe loaded that exact stripped DLL and the disposable project's actual Newtonsoft DLL: attempting to deserialize `{}` threw `JsonSerializationException: Unable to find a constructor to use for type Level5.BackendV2.MatchResultResponseDto`. The probe exited **0** because that expected diagnostic was verified; it is supplemental managed evidence, not a Windows gameplay pass. `response-constructor-proof.txt` retains the exception and the probe source is ignored beside it. The correction therefore also roots the existing response type's implicit default constructor. The serializer and HTTP classification remain unchanged.

The architecture review then inspected the corrected stripped response type's members, not just its constructor. It exposed **zero properties** (`final-contract-constructors.txt`): constructor preservation alone permits a successful empty DTO but does not retain its wire fields. The final correction also preserves the response's existing eleven JSON property accessors. A new player regression reads the actual typed HTTP 200 result (IDs, mode/arena/character, version/platform, metrics, modifiers and timestamp), without constructing that DTO or assigning any setters in test code. It therefore checks that the public response contract survives stripping, rather than only checking the success flag.

## Minimal correction and regression changes

- `Assets/Scripts/backendv2/Level5BackendV2/Dtos/MatchResultDtos.cs`: add `[UnityEngine.Scripting.Preserve]` to **only** `SubmitMatchResultDto()` with a short explanation. This keeps the reflection-only deserialization entry point in stripped players, consistent with [Unity's preservation guidance](https://docs.unity3d.com/6000.5/Documentation/Manual/managed-code-stripping-preserving.html). No constructor signature, DTO field, JSON schema, queue policy, or gameplay behavior changed.
- The same file roots `MatchResultResponseDto`, its implicit default constructor, and its existing eleven JSON property accessors for the required successful-response control. This does not preserve a whole assembly or introduce another response model.
- `Assets/Tests/PlayMode/BackendV2MatchResultRestartRecoveryPlayModeTests.cs`: reuse its existing fake transport/JSON response helper for the typed HTTP 200 player regression; allow that helper to return the known test owner/result ID. The existing startup-recovery and wrong-owner test bodies remain unchanged.
- `Assets/Tests/PlayMode/CampaignRoundPlayModeTests.cs`: add a scene-independent durable queue round-trip regression that uses only the parameterized production constructor, preventing the test itself from keeping the missing constructor alive. Verify payload equivalence, owner filtering, and `AlreadyQueued` on a repeated enqueue. Strengthen the victory case to compare pending result ID to SQLite, check mode/metrics/owner/definitive state and session/transport continuity across Dev, and wait for the actual coordinator drain state before asserting delivery and retryability. Queue existence is asserted **before** that wait; no delay can hide a missing enqueue.
- `Assets/Tests/PlayMode/LocalProfileTestDatabase.cs`: add a parameterized single-score ID query for the cross-store identity assertion.
- `Assets/Tests/Editor/Level5BackendV2MatchResultSubmissionTests.cs`: replace a vacuous no-session assertion with evidence that a later login has no entry and no queue file was created.
- The campaign fixture preserves the original runner scene if setup fails before loading gameplay, and restores its prior log policy. Its existing four-family primary/backup/temporary snapshots remain the persistence owner.

The campaign route, Dev finale, local-first ordering, generated `HighScoreModel.Scoreid`, authenticated owner, no-retroactive-login policy, active competitive exclusions, idempotency, 503 retry policy, progress/SQLite/Backend separation, and persistence formats are unchanged. No second authority, queue, framework, or live Backend transport was introduced.

## Validation and commands

Local ignored evidence: `TestResults/gp001-v1-2026-10-12/`. Each run retains `*-command.json`, Unity log, NUnit XML and `*-exit.json` (actual exit plus process duration). Player XML root dates incorrectly show year 0001; use suite dates and logs, not those roots.

| Check / artifact | Result |
| --- | --- |
| Repository validation, `repository-validation.log` | **PASS**, process **0**. |
| Unity compilation | **PASS**, the final-source focused EditMode and campaign PlayMode executions compiled successfully. |
| Final Backend V2 EditMode, `ContractBackendEditMode.xml` | **91/91 passed**, no skips, process **0**. Includes adapter/invalid ID, authenticated and no-session submission, active/stale competitive state, idempotency, queue corruption recovery, coordinator 503 retry/200 removal/wrong-owner behavior, session persistence and bootstrap lifecycle. Earlier `BackendEditMode.xml` / `FinalBackendEditMode.xml` also passed 91/91 before the accessor refinement. |
| Campaign Editor PlayMode, `CampaignEditor.xml` | **4/4 passed**, no skips, process **0**; includes victory through reopen/menu return, tie, exhaustion and the new stripped-player round-trip test. |
| Setup-failure isolation probe, `SetupFault.xml` / `SetupFault-preservation.json` | **PASS verification**: the deliberate setup failure was reported by NUnit (one expected failure, Unity **2**); **12/12** seeded primary/`.bak`/`.tmp` files restored byte for byte. The synthetic seeds and temporary injection were removed/restored in external `finally`. This is not a passing gameplay test. |
| Focused Windows victory and queue regression, `FocusedWindows.xml` | **2/2 passed**, no skips, process **0**, test duration **13.908 seconds**, after the request-constructor repair. Victory reaches authenticated fake POST, retryable 503, exact score ID/owner, duplicate-save protection, SQLite reopen and menu return. The later response-only annotation is covered by final player validation below. |
| Windows restart recovery and typed-response contract, `ContractRecoveryWindows.xml` | **2/2 passed**, no skips, process **0**, on final code. The persisted owner's result is delivered once and removed after HTTP 200; the new typed-response check verifies every response field category. The intermediate constructor-only correction also passed `FinalRestartOwnerWindows.xml` (1/1, process 0). |
| Windows wrong-owner startup recovery, `WrongOwnerWindows.xml` | **1/1 passed**, no skips, process **0**. No fake request is sent, and the original owner's entry remains pending. This ran after the response constructor repair; subsequent response-accessor annotations do not affect its no-delivery path. |
| Initial combined Windows invocation, `FullWindows.xml` | **10 passed / 0 failed / 3 skipped**, process **0**. The existing exact company guard skipped the three prior runtime probes; superseded by the correctly configured run below. |
| Full affected Windows boundary suite, `FullWindowsGuarded.xml` | **13/13 passed**, no skips, process **0**, test duration **55.129 seconds**. Four campaign/queue cases, five persistence-isolation cases, three prior runtime probes (modes 16/21/27), and one typed-response contract case. Victory passes through exact local/Backend ID and owner, one retryable 503 result, no duplicate save/submission, SQLite reopening and menu return. Tie and continue exhaustion pass. |
| Final outer isolation, `FullWindowsGuarded-preservation.json` | **12/12 paths restored**, including each prior absence or original bytes, after the final Windows run. |

Expanded commands are retained as JSON arrays. Equivalent PowerShell commands:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe'
$project = 'C:\Users\Patrick\Documents\github\level5\TestResults\gp001-post-2026-10-12\project'
$evidence = 'C:\Users\Patrick\Documents\github\level5\TestResults\gp001-v1-2026-10-12'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validate-repository.ps1
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testFilter 'Level5BackendV2MatchResult;Level5BackendV2PendingMatchResultStoreTests;Level5BackendV2Session' -testResults "$evidence\ContractBackendEditMode.xml" -logFile "$evidence\ContractBackendEditMode.log"
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform PlayMode -testFilter CampaignBoundaryPlayModeTests -testResults "$evidence\CampaignEditor.xml" -logFile "$evidence\CampaignEditor.log"
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter CampaignBoundaryPlayModeTests.CocaineIslandVictoryLoadsDevAndFinalVictorySavesOnceBeforeBackendAndReturnsToMenu -testResults "$evidence\OriginalWindowsPermitted.xml" -logFile "$evidence\OriginalWindowsPermitted.log"
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter 'BackendV2MatchResultRestartRecoveryPlayModeTests.APendingResultIsRetriedOnceTheSameOwnersSessionIsRestoredAfterRestart;BackendV2MatchResultRestartRecoveryPlayModeTests.SuccessfulMatchResultResponseRetainsItsWireFieldsInAStrippedPlayer' -testResults "$evidence\ContractRecoveryWindows.xml" -logFile "$evidence\ContractRecoveryWindows.log"
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter BackendV2MatchResultRestartRecoveryPlayModeTests.ADifferentAuthenticatedPlayerNeverSubmitsAnotherPlayersPendingResult -testResults "$evidence\WrongOwnerWindows.xml" -logFile "$evidence\WrongOwnerWindows.log"
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter 'CampaignBoundaryPlayModeTests;CampaignBoundaryPersistenceIsolationTests;Level5PostRemediationCertificationPlayModeTests;BackendV2MatchResultRestartRecoveryPlayModeTests.SuccessfulMatchResultResponseRetainsItsWireFieldsInAStrippedPlayer' -testResults "$evidence\FullWindowsGuarded.xml" -logFile "$evidence\FullWindowsGuarded.log"
```

The `OriginalWindowsPermitted` command describes the **pre-fix** reproduction; running it on corrected source passes as recorded by the focused/final Windows suites. The diagnostic run used the same filter with temporary safe instrumentation. Those disposable diagnostics were removed before final validation. `FocusedWindows-command.json` records the original victory and new queue-only filter; `SetupFault-command.json` records the deliberate initialization-failure test.

Before reproducing the final combined command, use the exact disposable company required by the existing probe guard, and snapshot/isolate/restore the twelve documented persistence paths in `finally`. The retained ignored orchestration script's `full-guarded` action performs that setup and verification. Other commands above used the separate V1 company. The working project's company/product settings must remain unchanged.

The existing restart fixtures run in separate fresh player processes, seed durable result/session files, clear session memory, then load the actual startup scene. They verify production restoration and delivery from disk. A process-kill-and-relaunch exercise remains outside this evidence. The final stripped DLL inspection (`final-complete-constructors.txt`) confirms the request constructor, response constructor and all eleven response properties are retained.

## Reviews and certification limits

**Correctness review:** traced session continuity, score conversion and exact ID, local durability, eligibility, queue outcome, file contents, primary/backup read validation, runtime binding and delivery. The first correction addresses the earliest observed divergence. The required Windows restart/200 control then exposed the missing success-response constructor, verified against the stripped assembly before adding its narrow preservation root. Existing exhaustive classifier tests and final player controls distinguish 503 retryability from 200 removal. No failed intermediate run was relabeled a pass.

**Architecture/regression review:** restricted production changes to the request constructor and existing response serialization contract rather than preserving an assembly or replacing the serializer/DTO. Inspection exposed stripped response properties even after successful queue removal; the final refinement preserves those accessors and adds the typed-result check without assigning the DTO's setters in test code. Added queue regression coverage without directly invoking the missing request constructor, kept synchronous enqueue assertions separate from asynchronous delivery completion, and repaired original-runner-scene cleanup on initialization failure. The injected setup failure verified preservation of all 12 seeded files. Reviewed scope, privacy, snapshot restoration and historical evidence preservation. No tokens were logged, no live authenticated Backend requests were made, and no unrelated gameplay findings were implemented.

This development test player is automated boundary evidence. It is not a production-release, physical input, human-played 21-round campaign, Android, controller, live-service or unrestricted gameplay certificate. The historical scene diagnostics and wider certification gaps in the post-remediation audit remain separate.

Hosted Repository Quality remains **`disabled_manually`**, verified live during this follow-up. Local execution does not imply a successful hosted CI run.

Final diff review passed changed-path scope, whitespace, local Markdown links, byte-equivalent executed C# source and unchanged historical characterization. Remote `dev` was rechecked before publication and still pointed to the audited SHA.
