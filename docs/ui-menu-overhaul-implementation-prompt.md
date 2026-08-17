# Implementation Prompt — UI Menu System Overhaul

Hand this to an implementation agent, one phase at a time. Phases are ordered by dependency; do not
start a phase before the previous one is merged and verified.

---

## Context you must read first

- `AGENTS.md` — repository engineering requirements, authoritative.
- `docs/ai/skills/unity-implementation-agent.md` — the workflow for approved implementation work.
- `docs/ui-input-architecture.md` — the plan. Phases below map 1:1 onto its phases.
- `docs/ui-menu-audit-2026-08-17.md` — the findings register (AUD-088 to AUD-112) with file and line
  evidence for every item referenced here.

Branch from current `dev`; target `dev` through a pull request. One phase per PR.

## Standing rules for every phase

- Implement the narrowest complete solution for the phase. Do not fold in a later phase's work.
- Preserve Unity GUIDs and serialized references. Commit `.meta` files for any new asset or folder.
- Do not change gameplay, match configuration, scoring, progression or persistence behaviour. This is
  a UI/input/presentation overhaul; those systems are read-only to you here.
- Compilation is not verification. Run, per phase:
  1. `./scripts/validate-repository.ps1` from the repository root.
  2. Compile with the Unity version in `ProjectSettings/ProjectVersion.txt` (6000.5.7f1).
  3. The EditMode suite, plus any test the phase adds.
  4. The PlayMode suite.
  5. A manual Play Mode pass over every screen the phase touched, on desktop **and** on a device for
     Phases 1, 2 and 6.
- Report any validation you could not run and why. Never claim a check passed that you did not run.
- Surface unrelated defects separately with evidence; do not fix them in the same PR.

---

## Phase 0 — Make the menu UI reviewable

**Why first.** Six of the ten menu screens are authored in binary-serialized prefabs. Until they are
text, no reviewer — human or automated — can see what a change to them did.

**Do.**

1. Reserialize these ten assets to text (Assets → Reserialize Assets on the selection; the project is
   already `m_SerializationMode: 2`). Verify each file now starts with `%YAML` and that its GUID in
   the `.meta` is unchanged:
   ```
   Assets/Resources/Prefabs/critical/OptionManager.prefab
   Assets/Resources/Prefabs/critical/start.prefab
   Assets/Resources/Prefabs/critical/GameManager.prefab
   Assets/Resources/Prefabs/critical/touch_joystick.prefab
   Assets/Resources/Prefabs/menu_start/StartManager.prefab
   Assets/Resources/Prefabs/menu_start/main_camera_start.prefab
   Assets/Resources/Prefabs/menu_stats/StatsManager.prefab
   Assets/Resources/Prefabs/menu_progression/progressionScreen.prefab
   Assets/Resources/Prefabs/menu_credits/creditsManager.prefab
   Assets/Resources/Prefabs/menu_account/LoginManager.prefab
   ```
   The other eight binary prefabs found by the audit are non-UI; leave them for a separate PR.
2. Add `Level5ProjectValidator.CollectBinarySerializedAssetErrors()` — fails when any `.prefab`,
   `.unity` or `.asset` under `Assets/` (excluding `Assets/Plugins`, `Assets/Standard Assets`,
   `Assets/OmniSARTechnologies` and `Library`) does not begin with `%YAML`. Assert it from a new test
   in `Assets/Tests/Editor/Level5SceneContractTests.cs`.
3. Resolve the duplicate start menu (AUD-089). `Constants.SCENE_NAME_level_00_start` currently
   resolves to `"level_00_start_test"`. Delete the orphaned `Assets/Scenes/level_00_start.unity`,
   rename `level_00_start_test.unity` to `level_00_start.unity`, and update `Constants.cs` and
   `ProjectSettings/EditorBuildSettings.asset` in the same commit. Grep for the literal
   `"level_00_start_test"` before you finish.

**Acceptance.** No behavioural change of any kind. Every menu scene opens and plays as before. The new
validator test fails if a binary asset is reintroduced. Diff of the ten prefabs is large but purely
mechanical.

---

## Phase 1 — One input route (AUD-095)

**Why.** `UiSelectionAdapter.EnsureInputSystemUiModule()` calls `AssignDefaultActions()`
(`Assets/Scripts/input/UiSelectionAdapter.cs:139`), so standard UI navigation runs Unity's built-in
`DefaultInputActions` rather than `PlayerControls.UINavigation`. WASD navigation, `Gamepad/start` and
every `HID::Sony PLAYSTATION(R)3 Controller` binding therefore do not reach the UI on the screens
already migrated to `onClick` (Options, EndRound).

**Do.**

1. In `Assets/Scripts/input/PlayerControls.inputactions`, add a `Navigate` action of type `Value` /
   control type `Vector2` to the `UINavigation` map, with 2D-vector composites mirroring the existing
   `Up`/`Down`/`Left`/`Right` bindings (WASD, arrows, d-pad, left stick, PS3 HID stick and
   buttons 13-16). Keep the four existing button actions for now — Phase 2 removes their consumers.
   Regenerate `PlayerControls.cs`.
2. Rewrite `UiSelectionAdapter.EnsureInputSystemUiModule()` to assign
   `PlayerControlsProvider.Controls.asset` to `inputSystemUIInputModule.actionsAsset` and set
   `move`, `submit`, `cancel`, `point` and `leftClick` to the corresponding `InputActionReference`s.
   Delete the `AssignDefaultActions()` call. Keep the try/catch fallback to `StandaloneInputModule`.
3. Serialize a configured `InputSystemUIInputModule` on the `EventSystem` in each menu scene and menu
   prefab (now editable after Phase 0), so the runtime `AddComponent` path becomes a fallback rather
   than the normal route. `PlatformCheck` keeps working unchanged.
4. `point`/`leftClick` must come from a map that is enabled on menu screens. If `UINavigation` is the
   wrong home for pointer actions, add them there rather than enabling a second map.

**Acceptance.** On every menu screen, all of these navigate and submit exactly once per press:
arrow keys, WASD, mouse click, gamepad d-pad, gamepad left stick, `Gamepad/buttonSouth`,
`Gamepad/start`, and a PS3 HID controller if one is available. `Application.isPlaying` allocations
from `AssignDefaultActions` are gone. Verify on device.

---

## Phase 2 — One behaviour route (AUD-096, 097, 098, 099, 102, 105, 109-partial)

**Why.** Directional and submit input is polled in menu `Update` methods while the UI module consumes
the same press, so one press both moves selection and changes a value. Two screens have controls a
mouse cannot operate at all.

**Do.**

1. **`StatsManager`** — delete `HandleSelectedStatsControl`'s polled input entirely
   (`Assets/Scripts/menu_stats/StatsManager.cs:376-448` and the `HandleLeftInput`/`HandleRightInput`/
   `HandleVerticalOptionInput` methods). Register the mode, page and filter controls through
   `onClick`. This screen has **no** `Time.frameCount` guard, so it is the one currently
   double-actuating; it is also the one issuing a synchronous SQLite query per press.
2. **`ProgressionManager`** — delete the polled `Submit`/`Cancel`/`Up`/`Down` handling in
   `HandleProgressionInput`. Register the progression stat buttons through `onClick` so points can be
   spent with a mouse or touch; keep `RunProgressionAction`'s frame guard.
3. **`StartManager`** — delete `ShouldUseManualMenuInput` and the manual navigation block it gates
   (`Assets/Scripts/menu_start/StartManager.cs:403-622`), and the `MenuSubmitTriggered` invoke at
   lines 351-358. Option cycling stays reachable through the `onClick` route that `RunOptionAction`
   already serves. Once the block is gone, remove the
   `#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_EDITOR_OSX` guard at line 333 so desktop and device
   run the same code. Also delete the duplicated level-select/mode-select `if` blocks (lines 390-397
   repeat 377-384).
4. **`Pause`** — register `loadSceneButton`, `loadStartScreenButton`, `cancelMenuButton` and
   `quitGameButton` through `onClick` and delete the name-comparison dispatch at
   `Assets/Scripts/game manager/Pause.cs:233-258`. Delete the per-frame
   `OnSelect(null)` / `.Select()` pair at lines 228-229; set selection once when the menu opens.
   **Do not touch** time scale, audio, save calls, scene loading, game-over or `startOnPause` logic.
5. **Registration symmetry and policy** — add `RegisterButtonCallbacks()` to `OnEnable` (guarded on
   `initialized`) in `OptionsManager`, `CreditsManager` and `EndRoundMenuManager`, matching what
   `StartManager`/`StatsManager`/`ProgressionManager` already do. Settle on one policy: code owns
   behaviour. Delete `UiSelectionAdapter.RegisterButton`'s persistent-listener skip
   (`UiSelectionAdapter.cs:97-102`) and the five verbatim copies of `RegisterRequiredButtonCallback`,
   replacing both with one helper on `UiSelectionAdapter`. Add an edit-mode assertion that no menu
   button has a persistent `onClick` listener.

**Acceptance.** Per screen, on desktop and device: one press does one thing. Navigation moves
selection and nothing else. Submit invokes the selected control exactly once. Mouse click works on
every control including pause and progression stat buttons. Stats paging and filtering still return
the same rows. Progression point add/subtract still balances against `PointsAvailable`. Pause still
reloads, returns to start, cancels and quits correctly, and still restores time scale.

---

## Phase 3 — One wiring route (AUD-103, 104, 110, 109-remainder, 112)

**Do.**

1. Add a serialized `*UiObjects` view component per screen, following `StartMenuUiObjects` and
   `EndRoundUIObjects`: `OptionsUiObjects`, `StatsUiObjects`, `ProgressionUiObjects`,
   `CreditsUiObjects`, `AccountUiObjects`, `PauseUiObjects`. Wire them in the (now text) prefabs and
   scenes. Delete the `ResolveButton`/`ResolveInputField` `GameObject.Find` fallbacks from
   `OptionsManager`, `CreditsManager`, `StatsManager`, `ProgressionManager` and `AccountManager`.
2. Delete `Resources.FindObjectsOfTypeAll<Button>()` from `StartManager.ResolveButton`
   (`StartManager.cs:721`) and take the seven footer buttons from the view object instead.
3. Extract `MenuFooterNav` — one component owning `press_start`, `stats_menu`, `options_menu`,
   `credits_menu`, `update_menu`, `account_menu`, `quit_game` and their scene loads. Remove the five
   duplicate name-constant sets and handler copies from `StartManager`, `OptionsManager`,
   `CreditsManager`, `StatsManager` and `ProgressionManager`. Keep the public `*ButtonName`
   properties only if something outside the menus still reads them; grep before deleting.
4. Fix the two unguarded chains in `StartManager`: line 1565
   (`GameObject.Find(FriendSelectOptionButtonName).GetComponent<Text>()`) takes the reference from
   `StartMenuUiObjects`; line 1857 (`GameObject.Find("messageDisplay")`) goes through
   `messageLog.instance` with a null check.
5. Move display refresh off the per-frame name comparison. Each screen's `Update` should no longer
   read `currentSelectedGameObject.name`; drive refresh from an `ISelectHandler` on the control or a
   single selection-changed watcher. `OptionsManager.Update`'s four-`SetActive` `DisplayControls` call
   must stop running every frame.
6. Extend `Level5ProjectValidator.CollectGameplaySceneObjectErrors` to cover the ten menu scenes, and
   add a play-mode smoke test per screen: scene loads without error, `EventSystem` exists with a
   configured `InputSystemUIInputModule`, first selected object is non-null, each footer button loads
   the expected scene.

**Acceptance.** No `GameObject.Find` remains in `Assets/Scripts/menu_*`. Every screen fails loudly and
by name if its view object is unwired. Menu `Update` methods do no per-frame string work.

---

## Phase 4 — Stats table correctness (AUD-106, 107, 108)

**Do.**

1. `StatsManager.Start` (`Assets/Scripts/menu_stats/StatsManager.cs:263-267`) currently writes row
   data into the shared `Resources` prefab and then instantiates it — the same defect class as the
   closed AUD-020. Invert it: `Instantiate(highScoreRowPrefab, highScoresRowsObject.transform, false)`
   first, then write the data into the returned instance.
2. Collect the instantiated rows into `highScoreRowsObjectsList` in creation order. Delete
   `GameObject.FindGameObjectsWithTag(highScoreRowTag)` at line 270 — it has no defined order and
   returns only active objects, so displayed rank order is currently undefined.
3. `StatsTableHighScoreRow`: serialize the six `Text` references instead of binding by
   `transform.GetChild(0..5)`; replace `Update` with a `Bind(...)` method called once per data change;
   either populate or delete the dead `TrafficEnabled`/`EnemiesEnabled`/`Platform` fields.
4. Confirm the `highScoreRow` prefab asset is not dirtied by a play session after the change.

**Acceptance.** Local and online high-score tables display in the same order the query returned, at
every page and filter combination, and the row prefab asset shows no diff after playing the stats
screen.

---

## Phase 5 — Scaling and presentation (AUD-090, 091, 093, 094)

**Do.**

1. Apply one canvas contract to every menu canvas in every menu scene and menu prefab:
   `m_UiScaleMode: 1` (ScaleWithScreenSize), `m_ReferenceResolution: {x: 1920, y: 1080}`,
   `m_ScreenMatchMode: 0`, `m_MatchWidthOrHeight: 0.5`, `m_ScaleFactor: 1`. The start menu currently
   has three canvases at 800×400/0.9, 800×600/1 and 1920×1080/1, all width-only — expect the layout to
   need re-tuning after this, which is the point.
2. Add an edit-mode test asserting that contract across every menu scene and menu prefab.
3. Reconcile the per-scene anchor overrides (59 in `level_00_options.unity`, 42 in the start scene)
   back into the prefabs, and prefer the existing `VerticalLayoutGroup`/`HorizontalLayoutGroup`
   components over absolute anchored positions where a group already exists.
4. Author `UniversalAdditionalCameraData` on every menu camera; set `m_HDR: 0` and `m_AllowMSAA: 0`
   there, since the camera renders nothing behind a Screen-Space-Overlay canvas.
5. Reconcile AUD-094: `QualitySettings.asset` has one level with `vSyncCount: 0` while
   `PlatformCheck.Awake` sets it to 1 at runtime. Pick one owner.

**Acceptance.** Every menu screen holds its layout at 16:9, 21:9 and 20:9, and at 1080p and 4K.
Screenshot each screen at those combinations before and after.

---

## Phase 6 — Retire the legacy paths (AUD-092, 100, 101)

Only after Phases 1-3 have been verified on a real device.

**Do.**

1. Delete the stray touch controllers from `level_00_start_test`: two
   `TouchInputAccountScreenController`, one `TouchInputStatsScreenController`, one
   `TouchInputProgressionScreenController`, one `TouchInputController`. Each currently calls
   `gameObject.SetActive(false)` on its host when its manager is absent, deactivating shared objects
   depending on which lookup fails first.
2. Delete the seven `TouchInput*Controller` scripts and `TouchInputEndRoundMenuController` once every
   screen passes device verification without them.
3. Set `ProjectSettings/ProjectSettings.asset` `activeInputHandler` to Input System only. Grep for
   remaining `Input.` (legacy) usage across `Assets/Scripts` first — this will break anything still
   on the old backend.
4. Begin the TMP migration (94 legacy `Text` components in the start menu alone), one screen per PR,
   behind the view-object contract from Phase 3.

**Acceptance.** Full touch pass on device for every screen with no touch controllers present. No
legacy `Input` usage. Project builds and runs with a single input backend.
