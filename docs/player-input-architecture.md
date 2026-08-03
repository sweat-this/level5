# Player Input Architecture

Last updated: 2026-08-02

This document tracks the player input modernization plan. The project already uses Unity's Input System through `PlayerControls.inputactions` and `PlayerControlsProvider`, but mobile/touch gameplay and menu input still contain legacy `Input.touchCount`, `Input.touches`, direct `Input.GetKeyDown`, third-party joystick reads, and per-screen touch controllers.

## Current Ownership

| Area | Current Owner | Notes |
| --- | --- | --- |
| Input actions | `PlayerControls.inputactions`, generated `PlayerControls.cs` | Source for keyboard/gamepad gameplay, UI navigation, debug actions, and a partially-defined touch action map. |
| Action lifecycle | `PlayerControlsProvider` | Reference-counted static provider for gameplay, menu, debug, and touch maps. Kept as the compatibility bridge. |
| Player gameplay input | `PlayerInputReader`, `PlayerController` | `PlayerInputReader` now owns the player's movement/action reads. `PlayerController` consumes gameplay intent and keeps behavior logic. |
| Mobile movement | `PlayerInputReader` wrapping current `FloatingJoystick`/legacy touch behavior | Preserved for this first slice. Target is Unity Input System `OnScreenStick` mapped to `Player/movement`. |
| Mobile gestures/actions | `TouchInputController` | Still directly calls gameplay methods for tap/hold/quadrant gestures. Target is an input adapter or on-screen buttons that produce actions. |
| Menu touch input | `TouchInput*Controller` scripts | Duplicated per screen. Target is standard Unity UI through `InputSystemUIInputModule`. |
| UI input modules | `PlatformCheck` toggles `StandaloneInputModule`/`InputSystemUIInputModule` | Target is consistent `InputSystemUIInputModule` once menu touch scripts are migrated. |

## Implemented First Slice

- Added `PlayerInputReader` as the first player input intent layer.
- Routed `PlayerController` movement, run, jump, shoot, call-ball, attack, block, special, and debug-lightning reads through `PlayerInputReader`.
- Kept `PlayerControlsProvider` and `PlayerControls.inputactions` intact.
- Kept legacy touch movement behavior intact, but contained it inside `PlayerInputReader` instead of `PlayerController`.
- Left racing and menu touch input unchanged for a separate slice.

## Target Direction

- Gameplay scripts should consume intent, not raw devices.
- `PlayerControls.inputactions` should become the source of truth for keyboard, gamepad, touch buttons, and virtual sticks.
- On-screen mobile controls should use Unity Input System `OnScreenStick` and `OnScreenButton` where possible.
- True gestures, such as swipe pause if retained, should live in one gesture adapter using EnhancedTouch.
- Menus should use `InputSystemUIInputModule` and normal UI events instead of duplicated per-screen touch polling.
- `PlayerInput`/`PlayerInputManager` should be evaluated after gameplay is behind an input reader, especially if local multiplayer device pairing becomes important.

## Migration Plan

1. Finish routing `PlayerController` input through `PlayerInputReader`. Done for the first gameplay reads.
2. Add focused smoke tests/manual checklist for keyboard, gamepad, and mobile touch controls.
3. Replace mobile movement with an Input System `OnScreenStick` bound to `Player/movement`.
4. Replace touch combat quadrants with `OnScreenButton` bindings for jump, shoot, attack, block, special, and pause where the UI/UX allows it.
5. Move retained gestures into one `GestureInputAdapter`.
6. Route racing vehicle input through a racing input reader using the same movement action.
7. Replace menu-specific `TouchInput*Controller` scripts with `InputSystemUIInputModule` plus UI submit/cancel/pointer events.
8. Evaluate switching from `PlayerControlsProvider` to scene-owned `PlayerInput` components or `PlayerInputManager`.

## Risks And Guardrails

- Do not delete touch scripts until mobile controls have been playtested on device.
- Do not replace the joystick and touch gesture model in the same commit as player gameplay routing.
- Preserve action names while migrating so generated `PlayerControls.cs` stays compatible.
- Keep `PlayerControlsProvider` until all major gameplay/menu callers have migrated.
- Avoid direct `Input.*` reads in new gameplay code. Add them only inside input adapters when there is no Input System equivalent yet.
