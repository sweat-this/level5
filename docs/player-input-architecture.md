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

2026-09-17 (AUD-012 Phase 5 Slice 77): corrected the first live-map ownership mismatch.
`PlayerInputReader.DebugChangeHeld`/`DebugLightningPressed` used to read the per-player `PlayerControls`
instance's `Other` map, but `PlayerController` builds that reader from
`PlayerControlsProvider.AcquireGameplayControls(playerId)`, which enables only that instance's `Player`
map - `Other` is never enabled on it. Both reads were therefore always reading a disabled action: the
debug/change modifier could never suppress call-ball via that path, and debug lightning could never fire.
`PlayerInputReader` now reads debug state through two new `PlayerControlsProvider` accessors,
`DevChangeHeld` and (pre-existing) `DevChangeControlEnabled`, which read the one shared `Other` owner
(`PlayerControlsProvider.Controls`, the same instance `GameLevelManager.OnEnable`/`OnDisable` ref-counts
via `EnableOther()`/`DisableOther()`). `Other` remains a shared runtime/debug map, not per-player gameplay
input - it is not enabled on every per-player controls instance. Normal gameplay input is unchanged and
still comes from the per-player `Player` map. `activeInputHandler` remains `2` (Both); touch compatibility
is unchanged. This modifier is one shared value across every local player, not scoped per player.

Code review (2026-09-17) caught that this fix, as first written, made `DebugChangeHeld` reachable in
every build, not just Editor/Development like its sibling `DebugLightningPressed`. `PlayerControls.inputactions`
binds the `Other` map's `change` action to `<Keyboard>/leftShift` and `<Keyboard>/rightShift` - the same
physical keys as the `Player` map's `run` action's `<Keyboard>/shift` binding (Unity's synthetic "either
shift key" control). The ownership bug had been silently masking that pre-existing binding collision:
with `DebugChangeHeld` fixed to read live shared state, every keyboard player holding Shift to run would
also read as holding the debug/change modifier, suppressing call-ball via `PlayerController`'s
`!reader.DebugChangeHeld` guard - in shipped release builds, not only Editor/Development. `DebugChangeHeld`
is now gated `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (returning `false` otherwise), the same as
`DebugLightningPressed`, so the collision is confined to internal testing rather than reaching players.
The keyboard binding overlap itself is unchanged and still present in `PlayerControls.inputactions` -
resolving it (or confirming it is intentional) is follow-up work, not part of this slice; see
`Level5PlayerInputOtherMapOwnershipTests.OtherChangeAndPlayerRun_ShareTheKeyboardShiftKeys` for a canary
test that fails if the overlap is ever removed, prompting a re-check of this gating decision.

2026-09-17 (AUD-012 Phase 5 Slice 78): resolved the keyboard collision Slice 77 exposed. `Other/change`'s
keyboard binding moved off Shift entirely - `<Keyboard>/leftShift` and `<Keyboard>/rightShift` were
replaced with a single `<Keyboard>/backquote` binding in `PlayerControls.inputactions`, verified unused
elsewhere in the asset before the change. `Player/run` still binds `<Keyboard>/shift`, unchanged. `Other`
remains the shared runtime/debug map (`PlayerControlsProvider.EnableOther`/`DisableOther`); no map was
renamed and `Other` was not enabled on any per-player controls instance. `PlayerInputReader.DebugChangeHeld`
and `DebugLightningPressed` remain `UNITY_EDITOR || DEVELOPMENT_BUILD`-gated - resolving the collision is
not by itself a reason to ship the debug read in release builds. No touch, menu, or input-backend migration
happened in this slice. The Slice 77 canary test that asserted the Shift overlap existed
(`OtherChangeAndPlayerRun_ShareTheKeyboardShiftKeys`) was replaced with `PlayerRun_KeepsItsShiftKeyboardBinding`,
`OtherChange_NoLongerBindsEitherShiftKey`, and `OtherChangeAndPlayerRun_ShareNoKeyboardBinding` in
`Level5PlayerInputOtherMapOwnershipTests`.

2026-09-17 (AUD-012 Phase 5 Slice 79): added
[`player-input-smoke-validation.md`](player-input-smoke-validation.md), the checklist migration plan
step 2 below calls for - a per-behavior matrix covering keyboard, gamepad, and mobile touch controls,
Editor/Development debug input, menu/pause, and the `activeInputHandler` gate, each row marked with a
validation type (automated / manual desktop / manual gamepad / manual mobile-device / blocked) and a
status (passing / failing / not run / blocked / not applicable). This is a validation-gate slice: no
action map, binding, generated wrapper, scene, prefab, or runtime input behavior changed, and
`activeInputHandler` remains `2` (Both). Writing the checklist surfaced one pre-existing defect, left
unfixed here because fixing it changes shipped runtime behavior: `GameLevelManager.Update`'s
`toggle_run_keyboard`/`toggle_stats_keyboard` reads (`Other` map) check only
`Controls.Other.change.enabled` - which reflects whether the shared `Other` map is currently active
(always true), not whether `change` is held, the same coarse pattern `DevFunctions.cs` and
`CheerleaderSwapAnimation.cs` use for their own `Other.change.enabled` reads. Unlike those two, and
unlike `PlayerInputReader.DebugChangeHeld`/`DebugLightningPressed`, this call site has no
`#if UNITY_EDITOR || DEVELOPMENT_BUILD` wrapper at all, so both toggles are reachable in shipped
release builds via plain `1`/`2` keypresses - see the checklist's "Editor/Development debug input"
section for detail.
Actual manual/device validation against the new checklist remains outstanding; `OnScreenStick`/
`OnScreenButton` migration (step 3 below) is still future work, and the touch scripts referenced by the
checklist must not be deleted until their device checklist rows pass.

This document tracks the player input modernization plan. The project already uses Unity's Input System through `PlayerControls.inputactions` and `PlayerControlsProvider`, but mobile/touch gameplay and menu input still contain legacy `Input.touchCount`, `Input.touches`, direct `Input.GetKeyDown`, third-party joystick reads, and per-screen touch controllers.

## Current Ownership

| Area | Current Owner | Notes |
| --- | --- | --- |
| Input actions | `PlayerControls.inputactions`, generated `PlayerControls.cs` | Source for keyboard/gamepad gameplay, UI navigation, and debug actions (`Player`, `UINavigation`, `Other`). The unused `PlayerTouch` action map was retired in AUD-012 Phase 5 Slice 76. |
| Action lifecycle | `PlayerControlsProvider` | Reference-counted static provider for gameplay, menu, and debug maps. Kept as the compatibility bridge. |
| Player gameplay input | `PlayerInputReader`, `PlayerTouchInputState`, `PlayerController` | `PlayerInputReader` owns the player's movement/action reads and lives in the `Level5.Input` assembly (AUD-012 Phase 2b Slice 26). `TouchInputController` queues touch gameplay intents through `PlayerTouchInputState`, and `PlayerController` consumes them in the normal gameplay path. `TouchBlockHeld` reads `PlayerTouchInputState.BlockHeld` alone; it no longer also consults `TouchInputController.instance.HoldDetected`, which was written in lockstep with it. Gameplay reads (`movement`, `run`, `jump`, `shoot`, `callball`, `attack`, `block`, `special`) come from the per-player `Player` map on the controls instance `PlayerController` was constructed with. `DebugChangeHeld`/`DebugLightningPressed` instead read the shared `Other` owner through `PlayerControlsProvider.DevChangeHeld`/`DevChangeControlEnabled` (AUD-012 Phase 5 Slice 77) - the per-player instance never has `Other` enabled. Both remain Editor/Development-only (`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`). `Other/change`'s keyboard binding previously collided with `Player/run`'s (`shift`) via `leftShift`/`rightShift`; Slice 78 moved it to `<Keyboard>/backquote`, so the two actions no longer share a keyboard binding, though the gate itself was left in place. |
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
2. Add focused smoke tests/manual checklist for keyboard, gamepad, and mobile touch controls. Done
   (AUD-012 Phase 5 Slice 79): see
   [`player-input-smoke-validation.md`](player-input-smoke-validation.md). The checklist exists;
   running it against a real desktop/gamepad/device session is separate outstanding work, tracked in
   that document's "Outstanding / not run" section.
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
