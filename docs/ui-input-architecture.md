# UI and Menu Architecture

Last updated: 2026-08-17

Supersedes the 2026-08-03 revision, which described the EndRound/Options pilot as the state of the
work. That pilot landed, and auditing what it produced showed the target it was migrating toward was
only half specified: it moved two screens onto `Button.onClick` without ever giving the
`InputSystemUIInputModule` the project's own bindings, and without a scaling, wiring or coverage
contract for the screens that followed.

Findings and evidence: [UI Menu System Audit — 2026-08-17](ui-menu-audit-2026-08-17.md) (AUD-088 to
AUD-112). This document is the plan; that document is the register.

## Current Baseline

Ten menu scenes are in build settings. Their UI is authored two incompatible ways: the start menu
(`level_00_start_test.unity`) is inline in the scene and diffable; the other six screens live in
binary-serialized prefabs (AUD-088) that no diff, merge or review can read.

Interaction is split three ways and all three are live at once:

- `InputSystemUIInputModule`, added at runtime and configured with Unity's `DefaultInputActions` —
  not `PlayerControls.UINavigation` (AUD-095).
- Per-frame polling of `PlayerControls.UINavigation` inside each manager's `Update`, dispatching on
  `EventSystem.current.currentSelectedGameObject.name` (AUD-096, AUD-109).
- Seven `TouchInput*Controller` scripts duplicating touch polling and a name→scene dispatch table
  (AUD-100), which are the only reason `activeInputHandler` is still set to both backends.

Wiring is also split three ways — serialized `*UiObjects` view components, `SceneObjects.Find`, and
raw `GameObject.Find` by name constant (AUD-103) — and no test loads a menu scene (AUD-112).

## Target

One route per interaction, one owner per reference, one scaling contract.

- **Input.** `InputSystemUIInputModule`, serialized on each menu `EventSystem`, driven by
  `PlayerControls.UINavigation`. Menu `Update` methods poll nothing.
- **Behaviour.** `Button.onClick`, registered from code, symmetric across `OnEnable`/`OnDisable`.
  Scene assets author no persistent listeners.
- **Display.** Refreshed on selection change, not per frame.
- **References.** A serialized `*UiObjects` view component per screen, following `StartMenuUiObjects`
  and `EndRoundUIObjects`. `SceneObjects.Find` stays only as the reporting fallback.
- **Layout.** Every menu canvas: `ScaleWithScreenSize`, reference 1920 × 1080,
  `matchWidthOrHeight: 0.5`, scale factor 1.
- **Shared chrome.** One `MenuFooterNav` owning the seven footer buttons, not five copies.
- **Coverage.** Menu scenes inside the scene-object contract, a canvas-contract edit-mode test, and a
  per-screen play-mode smoke test.

## Migration Plan

Ordered so each phase is verifiable before the next begins. Phase 0 exists because without it, no
change to six of the ten screens can be reviewed at all.

**Status, 2026-08-17.** Phases 0-5 landed in one pass on explicit instruction to fix every finding,
verified by compile + 410/410 EditMode + 4/4 PlayMode + `validate-repository.ps1`. What did not land:
the `*UiObjects` view components and `MenuFooterNav` from Phase 3, the layout and TMP work in Phase 5,
and all of Phase 6. Per-finding detail is in the
[audit's status table](ui-menu-audit-2026-08-17.md#status-by-finding). Nothing here has had a manual
Play Mode pass or a device pass yet; that is the gate on Phase 6.

**Phase 0 — Make the UI reviewable.** Reserialize the ten binary prefabs to text (AUD-088); add a
validator check that fails on any non-`%YAML` asset. Resolve the duplicate start menu (AUD-089). No
behavioural change; this phase is mechanical and lands on its own.

**Phase 1 — One input route.** Give `PlayerControls.UINavigation` a `Vector2` `Navigate` action; bind
the module's `move`/`submit`/`cancel`/`point`/`leftClick` to it; delete the `AssignDefaultActions()`
path from `UiSelectionAdapter`; serialize a configured module on each menu `EventSystem`
(AUD-095). Verify WASD, arrows, gamepad d-pad/stick, `Gamepad/start` and the PS3 HID bindings all
navigate and submit again.

**Phase 2 — One behaviour route.** Delete polled directional and submit handling from
`StatsManager` (AUD-096), `ProgressionManager` and `StartManager`; remove the
`ShouldUseManualMenuInput` latch (AUD-097); wire the pause menu and the progression stat buttons to
`onClick` (AUD-098); remove `Pause`'s per-frame re-select (AUD-099); make register/unregister
symmetric (AUD-102); settle on one registration policy and delete the duplicated helper (AUD-105).
Remove the `#if UNITY_EDITOR || UNITY_STANDALONE` guard around `StartManager.Update` once nothing in
it is load-bearing.

**Phase 3 — One wiring route.** A `*UiObjects` view component per screen (AUD-103); one
`MenuFooterNav` (AUD-104); delete `Resources.FindObjectsOfTypeAll<Button>` and the two unguarded
`GameObject.Find(...).GetComponent<Text>()` chains (AUD-110); move display refresh onto selection
change (AUD-109).

**Phase 4 — Stats table correctness.** Instantiate before writing row data (AUD-106); keep row
identity in creation order rather than a tag search (AUD-107); serialize the row's `Text` references
and delete its `Update` (AUD-108).

**Phase 5 — Scaling and presentation.** One canvas contract across every menu canvas and prefab
(AUD-091); reconcile the per-scene anchor overrides back into the prefabs and onto layout groups
(AUD-090); author `UniversalAdditionalCameraData` on menu cameras and drop HDR/MSAA there (AUD-093);
reconcile the vSync conflict (AUD-094).

**Phase 6 — Retire the legacy paths.** After device verification of Phases 1-3, delete the seven
`TouchInput*Controller` scripts and the stray instances in the start scene (AUD-100, AUD-101), then
set `activeInputHandler` to Input System only. Begin the TMP migration (AUD-092) screen by screen.

## Guardrails

- Preserve GUIDs and serialized references; the reserialization in Phase 0 must not renumber anything.
- Keep each phase compiling and playable on its own; do not stage a screen half-migrated across a
  commit boundary.
- Phase 2 changes what a keypress does. Verify each screen against the smoke checks below before
  moving on, on both desktop and a real device.
- `Pause` owns time scale, audio, save calls, scene loading and game-over state. It is migrated in
  Phase 2 for its click route only; its pause/resume logic is out of scope.
- Do not delete a `TouchInput*Controller` before the screen it covers has passed device verification.

## Verification

Automated, added as the phases land:

- Non-`%YAML` asset check (Phase 0).
- Menu scenes inside `Level5ProjectValidator.CollectGameplaySceneObjectErrors` (Phase 3).
- Canvas-contract edit-mode test over every menu scene and menu prefab (Phase 5).
- Zero persistent `onClick` listeners on menu buttons (Phase 2).
- Per-screen play-mode smoke test: scene loads, `EventSystem` exists with a configured
  `InputSystemUIInputModule`, first selected object is non-null, each footer button loads the
  expected scene (Phase 3).

Manual smoke checks, per screen, on desktop and device:

- Mouse/touch tap invokes the selected control exactly once.
- Keyboard, gamepad and PS3 HID submit invokes it exactly once.
- Navigation never leaves selection null, and never both moves selection and changes a value.
- Start, progression, stats, options, credits, account, end-round and pause each define a valid first
  selected object.
- Options control preview updates when keyboard, keyboard+mouse, gamepad or touch is selected.
- Mobile touch requires no double tap.
- Menu layout holds at 16:9, 21:9 and 20:9, and at both 1080p and 4K.
