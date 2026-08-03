# Player Input Architecture

Last updated: 2026-08-02

This document tracks the player input modernization plan. The project already uses Unity's Input System through `PlayerControls.inputactions` and `PlayerControlsProvider`, but mobile/touch gameplay and menu input still contain legacy `Input.touchCount`, `Input.touches`, direct `Input.GetKeyDown`, third-party joystick reads, and per-screen touch controllers.

## Current Ownership

| Area | Current Owner | Notes |
| --- | --- | --- |
| Input actions | `PlayerControls.inputactions`, generated `PlayerControls.cs` | Source for keyboard/gamepad gameplay, UI navigation, debug actions, and a partially-defined touch action map. |
| Action lifecycle | `PlayerControlsProvider` | Reference-counted static provider for gameplay, menu, debug, and touch maps. Kept as the compatibility bridge. |
| Player gameplay input | `PlayerInputReader`, `PlayerTouchInputState`, `PlayerController` | `PlayerInputReader` owns the player's movement/action reads. `TouchInputController` queues touch gameplay intents through `PlayerTouchInputState`, and `PlayerController` consumes them in the normal gameplay path. |
| Mobile movement | `PlayerInputReader` with Input System movement first and legacy `FloatingJoystick` fallback | Ready for Unity Input System `OnScreenStick` mapped to `Player/movement`; the old joystick remains as fallback until scenes/prefabs are migrated and playtested. |
| Mobile gestures/actions | `TouchInputController`, `PlayerTouchInputState` | Gameplay gestures now queue input intents instead of directly calling player combat/basketball methods. Target is still `OnScreenButton` bindings where the UI/UX allows it. |
| Racing input | `RacingInputReader`, `RacingVehicleController` | Racing movement, run, and jump reads are routed through a reader with Input System movement first and legacy touch joystick fallback. |
| Menu touch input | `TouchInput*Controller` scripts | Duplicated per screen. Target is standard Unity UI through `InputSystemUIInputModule`. |
| UI input modules | `PlatformCheck` prefers `InputSystemUIInputModule` and falls back to `StandaloneInputModule` only when needed | Menu touch scripts still exist, but the scene input module preference is now aligned with the Input System. |

## Implemented First Slice

- Added `PlayerInputReader` as the first player input intent layer.
- Added `PlayerTouchInputState` so touch gameplay can queue input intents instead of directly calling player gameplay methods.
- Added `RacingInputReader` for racing movement/run/jump input ownership.
- Routed `PlayerController` movement, run, jump, shoot, call-ball, attack, block, special, and debug-lightning reads through `PlayerInputReader`.
- Routed racing movement/run/jump reads through `RacingInputReader`.
- Kept `PlayerControlsProvider` and `PlayerControls.inputactions` intact.
- Kept legacy touch movement behavior intact as a fallback, but prefer `Player/movement` first so `OnScreenStick` can drive movement once added to scenes.
- Updated UI module selection to prefer `InputSystemUIInputModule` with fallback to `StandaloneInputModule` if the scene has no Input System module.

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
3. Add Input System `OnScreenStick` components in Unity scenes/prefabs and bind them to `Player/movement`; then remove the legacy joystick fallback after device playtesting.
4. Replace touch combat quadrants with `OnScreenButton` bindings for jump, shoot, attack, block, special, and pause where the UI/UX allows it.
5. Move retained gestures into one `GestureInputAdapter`.
6. Route racing vehicle input through a racing input reader using the same movement action. Done for movement/run/jump reads.
7. Replace menu-specific `TouchInput*Controller` scripts with `InputSystemUIInputModule` plus UI submit/cancel/pointer events.
8. Evaluate switching from `PlayerControlsProvider` to scene-owned `PlayerInput` components or `PlayerInputManager`.

## Risks And Guardrails

- Do not delete touch scripts until mobile controls have been playtested on device.
- Do not replace the joystick and touch gesture model in the same commit as player gameplay routing.
- Preserve action names while migrating so generated `PlayerControls.cs` stays compatible.
- Keep `PlayerControlsProvider` until all major gameplay/menu callers have migrated.
- Avoid direct `Input.*` reads in new gameplay code. Add them only inside input adapters when there is no Input System equivalent yet.
