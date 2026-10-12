# GP-001 F3: Spot-Up completion record eligibility

Baseline: freshly fetched `dev` **4670f299065eb2788a7cb3b053b0f9da65260f8e** (PR #282). Unity **6000.5.7f1**, revision **017862109af0**. The starting worktree was clean, open PR inventory was empty, and the open/closed issue inventory and recent merged PRs contained no F3 implementation. PR #282 restored mode 25's markers and explicitly deferred F3. Issue #244 concerns simultaneous versus certification; #270-272 own level progression, and #232-233 own hosted validation recovery.

## Root cause and ownership

`GameRules.setTimePlayed` records elapsed time for unsuccessful attempts. `HandleMatchEnded` converts stats into a `HighScoreModel`; `SaveMatchResults` previously admitted every ordinary mode except Free Play and campaign. `DBConnector` delegates to `DBHelper.InsertGameScore`, whose existing score-id check makes local replay idempotent. A failed SQLite save falls back to `PendingMatchPersistenceStore.QueueScore`; once either is durable, `BackendV2MatchResultSubmission.TryQueue` can enqueue under the current authenticated player's identity. Ascending Spot-Up rankings therefore admitted short failed attempts as completions.

`GameRules.SetPositionMarkers` owns required-course filtering and initializes `markersRemaining`. `BasketBallShotMarker` owns each quota and its completion latch; `IShotMarkerSession.RecordMarkerCompleted` decrements the match count. The final marker calls `RequestMatchEnd` -> `RequestGameOver` -> `RequestEnd(Unknown)`. `MatchController` delegates to the first-wins `MatchLifecycle`, which stores the accepted reason, enters Ending, and synchronously publishes Ending. `GameRules.HandleMatchEnded` later requests ObjectiveComplete, which cannot replace Unknown.

## Correction and tradeoffs

The only production file changed is `Assets/Scripts/game manager/GameRules.cs`:

- Subscribe to the existing controller's Ending event in Awake and release the subscription in OnDestroy. Current production source has no other scene-facing Ending subscribers that mutate course state.
- Remember whether required-marker initialization finished with at least one marker. A default zero remaining count alone cannot establish success.
- Freeze one nullable ordinary-score eligibility result at the first accepted ending. Modes 7, 8, 9 and 25 require initialized nonempty course state, zero remaining markers, and an accepted Unknown or ObjectiveComplete cause. Other causes fail closed. The existing controller-absent compatibility branch captures through the same method; the latch prevents later changes.
- Mark the ordinary score stage complete when ineligible, before SQLite/pending/Backend ordinary-result code. All-time stats still flow through their existing independent save/fallback stage, and progression still uses the existing service and result id.

No public termination/model contracts, schemas, queries, mode definitions, clocks, quotas, awards, Backend or Platform implementation changed. Successful courses keep the same SQLite, pending durability, authenticated submission, and retry behavior. Free Play/campaign and competition-only exclusions remain in place.

## Regressions

`Level5SpotUpRecordEligibilityTests` uses actual authored mode prefabs, the actual GameRules Awake subscription and controller lifecycle, real required-marker setup and session callbacks, the production SaveMatchResults method, and isolated SQLite. It covers successful Unknown completion for all four modes; death and mode-25 timeout; incomplete Unknown; missing/empty course initialization; ObjectiveComplete; unsuccessful causes even with zero remaining; first-wins requests in both orders; late marker changes; repeated saves; authenticated/no-session queue behavior; SQLite refusal and durable repair; all-time totals; unchanged XP and progression idempotency; and neighboring ordinary, point-contest, Free Play and campaign exclusions. Its `.meta` preserves the new test asset identity.

`Level5SpotUp7sPlayModeTests` extends the existing authored Scrapyard fixture with actual match-end SQLite, pending, Backend queue/request, all-time stage and progression assertions. Successful completion must persist one ordinary record; timer expiry and death before completion must persist none. Death followed by late marker callbacks before the next frame and repeated match-end handling must remain excluded. SQLite opens before scene bootstrap; the course is built with an explicit authored shooter through the existing validated builder rather than depending on a remembered menu selection. A fake transport returns retryable server errors and retains the successful result in the durable Backend queue without network I/O.

## Validation

Logs and NUnit XML are local under `Logs/gp001-f3/` and are not committed. The product name is temporarily isolated to `level5-gp001-f3-test` during Unity execution; original ProjectSettings bytes are restored afterward. SQLite fixtures use unique temporary files, and no real player database is tested.

Repository validation passed (exit 0). Unity compilation and the focused EditMode run passed: **125 passed, zero failed/skipped**, including 29 new Spot-Up tests, lifecycle 19, timer context 11, Backend submission 10, Backend wiring 3, local simultaneous versus 47, and progression 6. Three initial compile attempts found new-code errors (Object ambiguity, the existing GameModeId property shadowing the enum, and two assumed arena enum names); these were corrected before tests executed.

The first graphics-enabled PlayMode selection was stopped after fresh isolated menu state repeatedly refused a non-shooting Doctor Blood launch. A subsequent setup correction was stopped before completion, and another run still depended on remembered menu state and was stopped. These interrupted attempts produced no complete NUnit report and are not passes. The final fixture launches its explicit authored shooter directly through the existing builder.

The completed graphics-enabled PlayMode selection reports **13 tests: 12 passed, one failed, zero skipped**. All three Scrapyard cases pass, including 24 makes/168 points and exactly one successful ordinary score, expiry with six unfinished markers and no score, and death followed by late callbacks with no score. Both success/failure retain stats/progression completion and the expected Backend queue/request counts. The campaign decision seam, three gameplay lifecycle/reporting checks, four real simultaneous-versus checks (including competition-only atomic persistence), and correspondence general-endpoint exclusion also pass.

The failing unchanged `Level5ShotMarkerSessionCompositionPlayModeTests.EveryRealSceneMarkerIsBoundToTheRealGameRulesInstanceBeforeGameplay` cannot launch the remembered Doctor Blood character in Total Points; NUnit reports the unhandled launch-refusal log. It does not reach marker-binding assertions. A separate fresh-process run using **dev's unmodified GameRules and the unchanged fixture** reproduced **one failed / zero passed / zero skipped**, with the identical launch-refusal message. Other edited tests were present but were not selected in that baseline run; this is not a claim of a completely pristine checkout. The fixed GameRules source was then restored byte-for-byte. This failure is not counted as a pass or silently excluded.

Exact retained commands (Unity was started using `Start-Process -WindowStyle Hidden -PassThru` with these arguments; no `-quit` is supplied to the test runner):

```powershell
$repo = 'C:/Users/Patrick/Documents/github/level5'
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe'
./scripts/validate-repository.ps1

$edit = 'Level5SpotUpRecordEligibilityTests;Level5MatchLifecycleTests;Level5TimerMatchEndContextTests;Level5BackendV2MatchResultSubmissionTests;Level5BackendV2MatchResultWiringTests;Level5LocalSimultaneousVersusTests;ProgressionServiceApplyMatchResultTests'
& $editor -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode `
  -testFilter $edit -testResults "$repo/Logs/gp001-f3/edit-results.xml" `
  -logFile "$repo/Logs/gp001-f3/edit-results.log"

$play = 'Level5SpotUp7sPlayModeTests;Level5ShotMarkerSessionCompositionPlayModeTests;CampaignRoundPlayModeTests;Level5GameplayPlayModeTests;LocalSimultaneousVersusProductionPlayModeTests;BackendV2CompetitiveAttemptExclusionPlayModeTests'
& $editor -batchmode -projectPath $repo -runTests -testPlatform PlayMode `
  -testFilter $play -testResults "$repo/Logs/gp001-f3/play-isolated.xml" `
  -logFile "$repo/Logs/gp001-f3/play-isolated.log"

# Only for baseline reproduction: dev's GameRules was temporarily substituted, then restored.
& $editor -batchmode -projectPath $repo -runTests -testPlatform PlayMode `
  -testFilter Level5ShotMarkerSessionCompositionPlayModeTests `
  -testResults "$repo/Logs/gp001-f3/play-binding-baseline.xml" `
  -logFile "$repo/Logs/gp001-f3/play-binding-baseline.log"
```

The earlier compile commands used the same EditMode filter with output stems `edit`, `edit-fixed`, and `edit-compiled`. Interrupted PlayMode stems were `play`, `play-fixed`, and `play-reviewed`, using the final PlayMode filter minus `BackendV2CompetitiveAttemptExclusionPlayModeTests`.

Hosted `repository-quality.yml` was verified through `gh api repos/sweat-this/level5/actions/workflows/repository-quality.yml --jq .state`: **disabled_manually**. No hosted pass is claimed.

Final repository validation passed again (exit 0) after restoring original ProjectSettings, excluding Unity solution ordering, and removing generated runner scenes. `git diff --check` passed. The final remote dev SHA remained the audited baseline. There was no Unity licensing/IPC blocker for the retained runs; existing obsolete-API warnings and deliberately suppressed unrelated scene log errors remain, so these checks do not establish an error-free scene.

## Reviews

Correctness review checks the accepted transition, Unknown compatibility, initialization and empty-course handling, unsuccessful cause precedence, frozen late-event behavior, and ordinary/pending/Backend exclusion. It also checks independent stats and progression writes and duplicate-save safety.

Result: no remaining in-scope correctness finding. The controller-absent compatibility path was routed through the same guarded capture method, and unsuccessful causes with a zero remaining count are explicitly covered. All 29 new EditMode tests and all three actual scene-result cases pass.

Architecture/regression review checks one match-local eligibility owner, existing controller event ordering and teardown, unchanged public contracts and persistence dependencies, successful durability, neighboring modes and competitive exclusions. It caught an overly restrictive test product-name guard, which would have failed ordinary CI; that guard was removed and SQLite isolation moved before bootstrap. It also caught the PlayMode queue's automatic drain and added a fake transport. Tests call the production lifecycle/save paths; the real-scene fixture checks end results, not only a predicate.

Result: production remains confined to GameRules, with no new dependencies, public contracts or serialized-content changes. The test's hidden dependence on remembered menu selection was removed using the existing validated builder and authored shooter metadata. Incidental Unity solution ordering and temporary validation settings are excluded from publication.

## Limits and separate ownership

Historical failed records may still exist in SQLite and Backend V2. This change does not delete, rewrite or migrate them. The existing Unknown marker-completion end cause is preserved. Scrapyard's four-make quotas versus the five-make description remain a separate authored-content decision documented in the F1 restoration report. Other GP-001 findings, progression policy and hosted validation recovery retain their existing ownership.

The marker-binding fixture's dependence on a launchable remembered/default character is a separate test-bootstrap follow-up. Its failed baseline reproduction leaves that neighboring check unverified in this isolated environment; the successful Spot-Up scenes independently execute real Awake/session binding and course completion, but do not turn the failed fixture into a pass.

Injected PlayMode shot callbacks and accelerated elapsed time provide automated scene integration evidence; they do not certify player builds, physical input, aiming or real shot flight. No live Backend submission is performed by these queue tests.
