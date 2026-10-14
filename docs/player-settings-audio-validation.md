# Player settings and audio validation evidence

Baseline: e594eaa07190cb766adce9ec47a28b886aad6523. Unity 6000.5.7f1. Local Windows graphics uses Direct3D 11 / NVIDIA RTX 4060. Final dev refresh still matches baseline; no open PR overlap.

| Run | Cases | Passed | Failed | Skipped | Inconclusive | Process exit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| editmode-complete | 84 | 84 | 0 | 0 | 0 | 0 |
| playmode-complete | 4 | 3 | 0 | 0 | 1 | 2 |

Repository validator: PASS, process exit 0. Final diff whitespace check: PASS. Independent baseline comparison confirms all 397 original covered AudioSources preserve every authored property except output group; the two added effects sources retain the original acoustic properties. Existing metadata/GUIDs unchanged.

## Commands

PowerShell, from repository root (each Unity process awaited and ExitCode captured):

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe'
$project = 'C:/Users/Patrick/Documents/github/level5'
& $unity -batchmode -projectPath $project -runTests -testPlatform EditMode -testFilter 'PlayerSettingsPersistenceTests;PlayerAudioCompositionTests;OptionsTextMeshProMigrationTests;Level5ProductionAssemblyBoundaryTests' -testResults TestResults/audio-settings/editmode-complete.xml -logFile TestResults/audio-settings/editmode-complete.log
& $unity -batchmode -force-d3d11 -projectPath $project -runTests -testPlatform PlayMode -testFilter PlayerAudioSettingsPlayModeTests -testResults TestResults/audio-settings/playmode-complete.xml -logFile TestResults/audio-settings/playmode-complete.log
./scripts/validate-repository.ps1
git diff --check
```

No -nographics flag was used for PlayMode. Audio diagnostic was also run without -batchmode in a hidden editor. The same unrouted baseline was zero. Exact raw XML/log/exit artifacts are in ignored local TestResults/audio-settings; the case results below are the committed evidence summary.

## Earlier runs and findings

- First authoring compile: exit 1, temporary helper was under an unrelated installer asmdef without TMPro. Moved it into the existing editor assembly; authoring then exited 0. Temporary helper removed after serialization.
- Initial EditMode: 83 cases, 81 passed, 2 failed, 0 skipped, process exit 2. Missing legacy SFX references repaired; CRLF-only mixer assertion repaired. Affected composition/migration rerun: 17/17, exit 0. Final set above supersedes both.
- First two graphics launches aborted without XML because test teardown unloaded the NUnit runner scene. Terminated owned processes; both exits -1. No passing evidence claimed.
- Corrected graphics fixture: 4 cases, 2 passed, 2 failed, exit 2. Playlist/pause and real Scrapyard passed. Inactive audio rows and unavailable audio output were the failures.
- Activated-row rerun: 2 cases, Options passed, audio probe failed (zero capture-frame samples), exit 2. Explicit render-buffer probe then failed with zero DSP output, exit 2. Unmuted, unrouted diagnostic and hidden non-batch diagnostic both failed with zero unrouted DSP output, exit 2. This establishes an unavailable acceptance measurement rather than a successful attenuation result.
- After preventing layout/footer overlap: Options case 1/1, exit 0. Final fixture above reports the unavailable unrouted baseline as Inconclusive, with process exit 2 retained. No failing attenuation comparison was waived or converted to PASS.

## Final exact NUnit cases

| Case | Result |
| --- | --- |
| `Level5ProductionAssemblyBoundaryTests.ActiveMatchCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.AtomicFileCompilesIntoLevel5Utility` | Passed |
| `Level5ProductionAssemblyBoundaryTests.AttackBoxHitInfoCompilesIntoLevel5Combat` | Passed |
| `Level5ProductionAssemblyBoundaryTests.BasketballProductionTypesCompileIntoLevel5Basketball` | Passed |
| `Level5ProductionAssemblyBoundaryTests.CallBallToPlayerCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.CharacterPresetProgressionTypesCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.CharacterProfileOwnershipTypesCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.EnemyPopulationRulesCompilesIntoLevel5Core` | Passed |
| `Level5ProductionAssemblyBoundaryTests.FinalPlayerRootTypesCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.GameStatsAttemptResultsCompilesIntoLevel5Versus` | Passed |
| `Level5ProductionAssemblyBoundaryTests.IntentionalGameManagerShellCompilesIntoAssemblyCSharp` | Passed |
| `Level5ProductionAssemblyBoundaryTests.MatchCatalogsCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.MatchControllerCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.MatchHudPresenterCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.MatchRuntimeCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.MatchSessionCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` | Passed |
| `Level5ProductionAssemblyBoundaryTests.NoProductionAssemblyReferencesAKnownEditorOnlyPackageAssembly` | Passed |
| `Level5ProductionAssemblyBoundaryTests.NoProductionSourceExistsDirectlyUnderThePlayerRootOutsideLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.NoSourceFileReturnsToTheAsmdefFreePlayModeGameplayWorkaround` | Passed |
| `Level5ProductionAssemblyBoundaryTests.OnlyIntentionalAssemblyCSharpGameManagerShellRemainsLoose` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PauseCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerAttackBoxCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerAttackQueueTypesCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerControllerCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerControllerParticipantStateCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerDamageReactionTypesCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerDunkCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerDunkHostCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerHealthCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerIdleSniperRuntimeCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerInputReaderCompilesIntoLevel5Input` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerMatchRuntimeCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerRegistryCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.PlayerSwapAttackCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.RemainingPlayerDomainTargetsCompileIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.RigidbodyFreezeHelperCompilesIntoLevel5Utility` | Passed |
| `Level5ProductionAssemblyBoundaryTests.SceneRuntimeBridgeTypesCompileIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.SceneTransitionCompilesIntoLevel5Utility` | Passed |
| `Level5ProductionAssemblyBoundaryTests.SerializedManagerUiLeavesCompileIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.ShooterAttributesMapperCompilesIntoLevel5Player` | Passed |
| `Level5ProductionAssemblyBoundaryTests.SpawnCoordinatorCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.TimerCompilesIntoLevel5Match` | Passed |
| `Level5ProductionAssemblyBoundaryTests.UiSelectionAdapterCompilesIntoLevel5Input` | Passed |
| `Level5ProductionAssemblyBoundaryTests.VersusCatalogTypesCompileIntoLevel5Versus` | Passed |
| `Level5ProductionAssemblyBoundaryTests.VersusMatchStateTypesCompileIntoLevel5Versus` | Passed |
| `Level5ProductionAssemblyBoundaryTests.VersusRuntimeTypesCompileIntoLevel5Versus` | Passed |
| `OptionsTextMeshProMigrationTests.ConvertSingleTextCompensatesOutlineWithUnderlayAndRemovesIt` | Passed |
| `OptionsTextMeshProMigrationTests.ConvertSingleTextPreservesVisualPropertiesAndRewiresTargetGraphic` | Passed |
| `OptionsTextMeshProMigrationTests.ConvertSingleTextSkipsUnderlayCompensationForADisabledOutline` | Passed |
| `OptionsTextMeshProMigrationTests.OptionsPrefabUsesTextMeshProOnly` | Passed |
| `OptionsTextMeshProMigrationTests.OptionsSelectablesHaveValidTargetGraphics` | Passed |
| `OptionsTextMeshProMigrationTests.OptionsTextMeshProComponentsHaveFontAssets` | Passed |
| `PlayerAudioCompositionTests.EveryInventoriedSourceHasItsClassifiedRouteAndUnchangedAuthoredGain` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(1.0f,0.0f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(0.5f,-6.0206f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(0.0f,-80.0f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(1E-06.0f,-80.0f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(-1.0f,-80.0f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(2.0f,0.0f)` | Passed |
| `PlayerAudioCompositionTests.LinearGainConversionIsFiniteAndBounded(float.NaN,0.0f)` | Passed |
| `PlayerAudioCompositionTests.MixerHasExactlyThreeExposedGainsAndMasterHierarchyAtFullGain` | Passed |
| `PlayerAudioCompositionTests.OptionsPrefabReferencesAndSliderNavigationAreComplete` | Passed |
| `PlayerAudioCompositionTests.SfxPrefabSourcesAreExplicitDistinctAndPreserveRelativeGain` | Passed |
| `PlayerSettingsPersistenceTests.FailedWriteReportsFailureAndKeepsExistingValidFamily` | Passed |
| `PlayerSettingsPersistenceTests.FreshStoreRestoresSavedValues` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("broken")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("null")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("[]")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":2,\"master\":0,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":0,\"music\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":\"0.5\",\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":NaN,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":Infinity,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":1e100,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":-0.1,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MalformedOrUnsupportedDocumentReturnsDeterministicDefaults("{\"version\":1,\"master\":0,\"master\":1,\"music\":0,\"sfx\":0}")` | Passed |
| `PlayerSettingsPersistenceTests.MissingFileReturnsFullGainWithoutWriting` | Passed |
| `PlayerSettingsPersistenceTests.MissingOrCorruptPrimaryRestoresValidBackup(False)` | Passed |
| `PlayerSettingsPersistenceTests.MissingOrCorruptPrimaryRestoresValidBackup(True)` | Passed |
| `PlayerSettingsPersistenceTests.RuntimeUpdatesNormalizeNonfiniteAndOutOfRangeValues` | Passed |
| `PlayerSettingsPersistenceTests.SaveAfterBothFilesAreCorruptCreatesAValidPrimaryAndBackup` | Passed |
| `PlayerSettingsPersistenceTests.SaveAfterRecoveryDoesNotRotateCorruptionIntoBackup` | Passed |
| `PlayerAudioSettingsPlayModeTests.MixerMuteFloorAttenuatesActualAudioEngineOutput` | Inconclusive |
| `PlayerAudioSettingsPlayModeTests.OptionsRestoresWithoutWritesAndNavigatesWithKeyboardAndGamepad` | Passed |
| `PlayerAudioSettingsPlayModeTests.PlaylistAndEffectsRemainIndependentAcrossGainChangesAndPause` | Passed |
| `PlayerAudioSettingsPlayModeTests.RealScrapyardSourcesAndSpawnedBallRetainRoutingAcrossOptionsTransition` | Passed |

## Acceptance by issue

| Issue | Status | Evidence / remaining gate |
| --- | --- | --- |
| #249 | PASS, implementation ready locally | One authority; full-gain defaults; finite normalization; durable fresh-store/runtime restoration; AtomicFile recovery including both-invalid save and failed writes. No profile/match/progression duplication. Actual standalone Player process is not certified. |
| #279 | ACCEPTED WITH OWNER WAIVER | Central hierarchy, 399 covered routes, separate playlist/effects, authored gain preservation, scene transition and Pause assertions pass. Full-gain audible equivalence and practical -80 dB attenuation remain unmeasured; owner explicitly waived that acceptance check on 2026-10-14. |
| #250 | PASS, upstream accepted with waiver | Sliders, persisted initial values, immediate parameter updates, durable change saves, virtual keyboard/gamepad navigation, callback lifecycle, real-scene layout bounds and Options reload pass. #279's remaining practical/listening gate was waived by the owner. |

Manual listening: WAIVED by explicit owner instruction, "skip listening check and pass". The automated DSP probe remains Inconclusive, not PASS. Physical input, actual Player restart, Player/IL2CPP, full project suites and hosted CI: NOT RUN locally. Publication is authorized with the recorded waiver.
