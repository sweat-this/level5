# UI Input Architecture

Last updated: 2026-08-03

## Current Baseline

Menu input is split between Unity UI selection, generated `PlayerControls.UINavigation`, and duplicated `TouchInput*Controller` scripts. The long-term target is standard Unity UI behavior: `Button.onClick`, `Selectable` navigation, and `InputSystemUIInputModule` for mouse, keyboard, gamepad, and touch pointer input.

## Main Problems

- Per-screen touch controllers duplicate touch polling, selected-object restore logic, swipe thresholds, and double-tap submit behavior.
- Several managers dispatch menu commands by selected object name strings instead of button events.
- Some screens manually move selection through `FindSelectableOnUp/Down/Left/Right`, which can throw if scene navigation is incomplete.
- Scene assets do not currently appear to serialize `InputSystemUIInputModule`, so runtime setup must preserve a fallback path.
- `Pause` is a separate high-risk migration because it owns time scale, audio pause/resume, save calls, scene loading, game-over state, and start-on-pause behavior.

## Implemented Pilot

- Added `UiSelectionAdapter` as a shared wrapper for `EventSystem` selection, button invocation, and runtime `InputSystemUIInputModule` setup.
- Updated `PlatformCheck` to use the same runtime UI module setup when it is present in a scene.
- Migrated `EndRoundMenuManager` and `OptionsManager` to normal `Button.onClick` callbacks while keeping legacy touch controllers available only as fallback when the Input System UI path is unavailable.

## Migration Plan

1. Keep `TouchInput*Controller` scripts until each screen passes keyboard, gamepad, mouse, and mobile touch checks.
2. Convert simple screens to `Button.onClick` and `Selectable` navigation first. EndRound and Options are the first migrated pilots.
3. Extract reusable option-stepper behavior for start and progression screens before migrating them.
4. Migrate pause last with explicit guards for duplicate save, duplicate scene load, time-scale re-entry, and game-over/start-on-pause state.
5. Remove direct `Input.touches` menu polling after every menu has a standard UI route and mobile device verification.

## Manual Smoke Checks

- Mouse/touch tap invokes the selected button once.
- Keyboard/gamepad submit invokes the selected button once.
- Keyboard/gamepad navigation never leaves selection null.
- Start menu, progression, stats, options, credits, account, end-round, and pause each define a valid first selected object.
- Options control preview updates when keyboard, keyboard+mouse, gamepad, or touch controls are selected or clicked.
- Mobile touch no longer requires double tap once a screen is migrated to standard UI.
