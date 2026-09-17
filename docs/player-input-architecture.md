# Player Input Architecture

Last updated: 2026-09-17

2026-09-17: the racing minigame and `RacingInputReader` have been retired and removed. References to
racing input ownership below have been removed accordingly; this was a subsystem deletion, not an input
redesign.

2026-09-17 (AUD-012 Phase 5 Slice 76): the `PlayerTouch` action map in `PlayerControls.inputactions` has
been retired - it had no live consumer (`PlayerTouchInputState`/`TouchInputController` never read it, and
`PlayerControlsProvider.EnablePlayerTouch()`/`DisablePlayerTouch()` had no production caller). `Player`,
`UINavigation`, and `Other` are unchanged. `PlayerTouchInputState` and `TouchInputController` remain live
compatibility paths - gameplay touch still flows `TouchInputController -> PlayerTouchInputState ->
PlayerInputReader`, unchanged by this slice. The legacy joystick fallback is unaffected.
`activeInputHandler` remains `2` (Both); switching to Input System-only input is out of scope for this
slice.

This document tracks the player input modernization plan. The project already uses Unity's Input System through `PlayerControls.inputactions` and `PlayerControlsProvider`, but mobile/touch gameplay and menu input still contain legacy `Input.touchCount`, `Input.touches`, direct `Input.GetKeyDown`, third-party joystick reads, and per-screen touch controllers.

## Current Ownership

| Area | Current Owner | Notes |
| --- | --- | --- |
| Input actions | `PlayerControls.inputactions`, generated `PlayerControls.cs` | Source for keyboard/gamepad gameplay, UI navigation, and debug actions (`Player`, `UINavigation`, `Other`). The unused `PlayerTouch` action map was retired in AUD-012 Phase 5 Slice 76. |
| Action lifecycle | `PlayerControlsProvider` | Reference-counted static provider for gameplay, menu, and debug maps. Kept as the compatibility bridge. |
| Player gameplay input | `PlayerInputReader`, `PlayerTouchInputState`, `PlayerController` | `PlayerInputReader` owns the player's movement/action reads and lives in the `Level5.Input` assembly (AUD-012 Phase 2b Slice 26). `TouchInputController` queues touch gameplay intents through `PlayerTouchInputState`, and `PlayerController` consumes them in the normal gameplay path. `TouchBlockHeld` reads `PlayerTouchInputState.BlockHeld` alone; it no longer also consults `TouchInputController.instance.HoldDetected`, which was written in lockstep with it. |
| Mobile movement | `PlayerInputReader` with Input System movement first and legacy `FloatingJoystick` fallback | Unchanged in behaviour, but the fallback's axes now arrive by composition rather than by the reader reaching for `GameLevelManager.instance.Joystick` - see "Legacy Joystick Composition" below. Ready for Unity Input System `OnScreenStick` mapped to `Player/movement`; the old joystick remains as fallback until scenes/prefabs are migrated and playtested. |
| Mobile gestures/actions | `TouchInputController`, `PlayerTouchInputState` | Gameplay gestures now queue input intents instead of directly calling player combat/basketball methods. Target is still `OnScreenButton` bindings where the UI/UX allows it. |
| Menu touch input | `TouchInput*Controller` scripts, `UiSelectionAdapter` | Duplicated per-screen touch scripts still exist. `UiSelectionAdapter` is the shared bridge for screens as they move to standard Unity UI events. |
| UI input modules | `UiSelectionAdapter`, `PlatformCheck` | `UiSelectionAdapter` can bootstrap/configure `InputSystemUIInputModule` for migrated UI screens. `PlatformCheck` uses the same path when present. Scene assets still need a permanent EventSystem migration. |

## Implemented First Slice

- Added `PlayerInputReader` as the first player input intent layer.
- Added `PlayerTouchInputState` so touch gameplay can queue input intents instead of directly calling player gameplay methods.
- Added `UiSelectionAdapter` and migrated the EndRound and Options menu pilots to `Button.onClick` callbacks.
- Routed `PlayerController` movement, run, jump, shoot, call-ball, attack, block, special, and debug-lightning reads through `PlayerInputReader`.
- Kept `PlayerControlsProvider` and `PlayerControls.inputactions` intact.
- Kept legacy touch movement behavior intact as a fallback, but prefer `Player/movement` first so `OnScreenStick` can drive movement once added to scenes.
- Updated UI module setup to add/configure `InputSystemUIInputModule` at runtime with fallback to `StandaloneInputModule`.

## Legacy Joystick Composition (AUD-012 Phase 2b Slice 26)

`PlayerInputReader` moved into the `Level5.Input` runtime assembly. That required removing its only two
references to types still compiled into `Assembly-CSharp`, without changing what the reader does:

- `TouchInputController` - `TouchBlockHeld` dropped its second read of
  `TouchInputController.instance.HoldDetected`. That flag was never independent of
  `PlayerTouchInputState.BlockHeld`: every write to `hold1Detected` (hold begin, hold end, special
  release, and the disable-time `PlayerTouchInputState.Clear()`) sets the same value on `BlockHeld`, and
  nothing else in production reads or writes it. `HoldDetected` still exists and `TouchInputController`
  still uses it as its own gesture-state flag.
- `GameLevelManager` - the reader no longer calls `GameLevelManager.instance.Joystick`. It takes an
  optional `Func<Vector2>` and asks it for the current axes inside the existing mobile fallback, so the
  value stays synchronous rather than becoming a frame-delayed cache.

The fallback is still *invoked* only under `(UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`, but
`ReadLegacyTouchMove` is now compiled on every target rather than being preprocessed away, and its
touch-distance scaling lives in a pure static `ScaleByTouchDistance`. Both changes exist so the mobile
fallback's arithmetic is type-checked and unit-tested on machines with no mobile module installed;
neither changes what runs on device.

That callback reaches the reader through the composition path that already spawns humans:

```text
GameLevelManager.ReadLegacyTouchMovement()          // joystick.Horizontal / joystick.Vertical, or zero
  -> SpawnCoordinator.BindHumanLegacyTouchMovement  // human participants only; CPUs are skipped
    -> PlayerController.BindLegacyTouchMovementReader
      -> new PlayerInputReader(controls, ReadLegacyTouchMovement)
```

`PlayerController` stores the callback independently of its current `PlayerInputReader`, because that
reader is dropped and rebuilt whenever gameplay controls are released and reacquired (`OnDisable` /
`OnEnable`, `TryEnsureInputReader`, the `Controls` setter). All three construction sites hand the reader
a controller-owned indirection, so a reader built before or after binding resolves the same live source.
The `FloatingJoystick` component itself is never handed across an assembly boundary - only its current
values - so `Level5.Input` gains no dependency on it or on the Joystick Pack.

This is a dependency inversion, not an input redesign. The legacy mobile joystick fallback, the touch
distance scaling, the Input System-first movement priority and every action name are unchanged, and the
`OnScreenStick` migration in step 3 below is still outstanding.

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
6. Replace menu-specific `TouchInput*Controller` scripts with `InputSystemUIInputModule` plus UI submit/cancel/pointer events.
7. Evaluate switching from `PlayerControlsProvider` to scene-owned `PlayerInput` components or `PlayerInputManager`.

See `ui-input-architecture.md` for the menu-specific baseline and migration checklist.

## Risks And Guardrails

- Do not delete touch scripts until mobile controls have been playtested on device.
- Do not replace the joystick and touch gesture model in the same commit as player gameplay routing.
- Preserve action names while migrating so generated `PlayerControls.cs` stays compatible.
- Keep `PlayerControlsProvider` until all major gameplay/menu callers have migrated.
- Avoid direct `Input.*` reads in new gameplay code. Add them only inside input adapters when there is no Input System equivalent yet.
