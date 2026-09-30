# Player Input Architecture

Last updated: 2026-09-29

2026-09-29 (two-human local input foundation): gameplay controls are now paired to physical devices
through a match-local `LocalGameplayDevicePlan` instead of `PlayerControlsProvider` resolving
`Gamepad.all[localInputSlot]` on every acquisition. See "Match-Local Gameplay Device Plan" below. This is
the input/roster prerequisite for a future `VersusMode.LocalSimultaneous`; it does not implement
simultaneous versus, split-screen, a join lobby, remapping or hot-plug reassignment, and it does not
adopt `PlayerInput`/`PlayerInputManager` (migration plan step 7 is unchanged). Single-player assignment
is unchanged. Physical-device certification has **not** been run (see "Physical certification").

2026-09-29 (shared camera and truthful arena multiplayer capability): a match whose roster seats more than one local
human is now framed by one shared camera, and `ArenaCapability.Multiplayer` is authored per arena instead of
granted to all of them. See "Shared camera for two local humans" below. Physical-device certification of the
camera is still outstanding.

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

2026-09-17 (AUD-012 Phase 5 Slice 80): fixed the gap Slice 79 surfaced.
`GameLevelManager.Update`'s `toggle_run_keyboard`/`toggle_stats_keyboard` reads (and the
`Controls.Other.change.enabled`/`_locked`/`PlayerController1.ToggleRun()`/
`BasketBall.instance.toggleUiStats()` logic guarded by them) are now wrapped in
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, the same gate already used by
`PlayerInputReader.DebugChangeHeld`/`DebugLightningPressed`, `DevFunctions.cs`, and
`CheerleaderSwapAnimation.cs`. Both toggles remain reachable in Editor/Development builds and are now
compiled out of shipped release builds. No additional held-check was added, and `Other`'s shared
lifecycle (`PlayerControlsProvider.EnableOther`/`DisableOther`, called unconditionally from
`GameLevelManager.OnEnable`/`OnDisable`) is unchanged - that lifecycle governs whether the `Other` map is
active at all, not whether these two release-build-only reads compile in. No action map, binding,
generated wrapper, scene, or prefab changed. `Level5GameLevelManagerDebugToggleGatingTests` guards the
gate by source inspection; the smoke-validation checklist's corresponding row is updated to `Passing`.

2026-09-17 (AUD-012 Phase 5 Slice 81): piloted - not completed - the first half of migration plan step 3
below. Added one Unity Input System `OnScreenStick` (`touch_joystick.prefab` ->
`Canvas/OnScreenStickMovement`) bound to `<Gamepad>/leftStick`, the same control path `Player/movement`'s
existing gamepad composite already binds in `PlayerControls.inputactions` - no action map, action, or
binding change was needed or made. The pilot was added as a new sibling of the existing "Floating
Joystick" prefab instance inside the same Canvas, purely additive (only the Canvas's child list grew);
nothing was removed, renamed, or reparented. The legacy `FloatingJoystick` fallback remains fully intact,
and `PlayerInputReader.ReadMove`/`ReadLegacyTouchMove`/`ScaleByTouchDistance` and
`GameLevelManager.ReadLegacyTouchMovement` are unchanged - `PlayerInputReader.ReadMove` still tries the
Input System `movement` action first and only falls back to the legacy touch reader when that reads zero,
so the new `OnScreenStick` and the legacy joystick both feed the same one action rather than competing
paths. The `touch_joystick` root keeps its `joystick` tag, and `activeInputHandler` remains `2` (Both).
Removing the legacy joystick fallback (the second half of step 3) remains blocked on device playtesting -
no mobile device or Editor touch simulation was exercised this slice; see
[`player-input-smoke-validation.md`](player-input-smoke-validation.md)'s "Mobile movement" section, which
now also records the new pilot and its automated coverage.

This document tracks the player input modernization plan. The project already uses Unity's Input System through `PlayerControls.inputactions` and `PlayerControlsProvider`, but mobile/touch gameplay and menu input still contain legacy `Input.touchCount`, `Input.touches`, direct `Input.GetKeyDown`, third-party joystick reads, and per-screen touch controllers.

## Current Ownership

| Area | Current Owner | Notes |
| --- | --- | --- |
| Input actions | `PlayerControls.inputactions`, generated `PlayerControls.cs` | Source for keyboard/gamepad gameplay, UI navigation, and debug actions (`Player`, `UINavigation`, `Other`). The unused `PlayerTouch` action map was retired in AUD-012 Phase 5 Slice 76. |
| Action lifecycle | `PlayerControlsProvider` | Reference-counted static provider for gameplay, menu, and debug maps. Kept as the compatibility bridge. Per-slot gameplay controls are paired to devices from the match-local `LocalGameplayDevicePlan` (see below). |
| Local input slot -> physical devices | `LocalGameplayDevicePlan` (`Level5.Input`), configured by `GameLevelManager.Awake` | Match-local, immutable, device-exclusive. `PlayerSlot.LocalInputSlot` (Core) only names the slot; it never names a device. |
| Player gameplay input | `PlayerInputReader`, `PlayerTouchInputState`, `PlayerController` | `PlayerInputReader` owns the player's movement/action reads and lives in the `Level5.Input` assembly (AUD-012 Phase 2b Slice 26). `TouchInputController` queues touch gameplay intents through `PlayerTouchInputState`, and `PlayerController` consumes them in the normal gameplay path. `TouchBlockHeld` reads `PlayerTouchInputState.BlockHeld` alone; it no longer also consults `TouchInputController.instance.HoldDetected`, which was written in lockstep with it. Gameplay reads (`movement`, `run`, `jump`, `shoot`, `callball`, `attack`, `block`, `special`) come from the per-player `Player` map on the controls instance `PlayerController` was constructed with. `DebugChangeHeld`/`DebugLightningPressed` instead read the shared `Other` owner through `PlayerControlsProvider.DevChangeHeld`/`DevChangeControlEnabled` (AUD-012 Phase 5 Slice 77) - the per-player instance never has `Other` enabled. Both remain Editor/Development-only (`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`). `Other/change`'s keyboard binding previously collided with `Player/run`'s (`shift`) via `leftShift`/`rightShift`; Slice 78 moved it to `<Keyboard>/backquote`, so the two actions no longer share a keyboard binding, though the gate itself was left in place. |
| Mobile movement | `PlayerInputReader` with Input System movement first and legacy `FloatingJoystick` fallback | Unchanged in behaviour, but the fallback's axes now arrive by composition rather than by the reader reaching for `GameLevelManager.instance.Joystick` - see "Legacy Joystick Composition" below. AUD-012 Phase 5 Slice 81 added a pilot Unity Input System `OnScreenStick` (`touch_joystick.prefab` -> `Canvas/OnScreenStickMovement`, bound to `<Gamepad>/leftStick`) alongside the old joystick, which remains as fallback until it is playtested on device and removed. |
| Mobile gestures/actions | `TouchInputController`, `PlayerTouchInputState` | Gameplay gestures now queue input intents instead of directly calling player combat/basketball methods. Target is still `OnScreenButton` bindings where the UI/UX allows it. |
| Menu touch input | `TouchInput*Controller` scripts, `UiSelectionAdapter` | Duplicated per-screen touch scripts still exist. `UiSelectionAdapter` is the shared bridge for screens as they move to standard Unity UI events. |
| UI input modules | `UiSelectionAdapter`, `PlatformCheck` | `UiSelectionAdapter` can bootstrap/configure `InputSystemUIInputModule` for migrated UI screens. `PlatformCheck` uses the same path when present. Scene assets still need a permanent EventSystem migration. |

## Match-Local Gameplay Device Plan

`PlayerSlot.LocalInputSlot` (`Level5.Core`) says which participant uses local input slot N. It says
nothing about hardware, and must not: `PlayerRoster`, `PlayerSlot`, `MatchConfiguration`,
`MatchConfigurationBuilder` and `GameModeCompatibility` stay framework-independent, and a guard test
fails if `Level5.Core` ever names an Input System type. Which `InputDevice` instances belong to slot N is
decided in the Unity input layer, once per match, by `LocalGameplayDevicePlan`
(`Assets/Scripts/input/Level5Input/LocalGameplayDevicePlan.cs`).

| Piece | Owner | Notes |
| --- | --- | --- |
| Roster -> "N local humans" | `PlayerRoster.LocalHumanCount` | Core, no devices. |
| Device snapshot | `LocalGameplayDeviceAvailability.Capture()` | One consistent read of `Keyboard.current`, `Mouse.current`, `Touchscreen.current`, `Gamepad.all`. |
| Slot -> devices | `LocalGameplayDevicePlan.TryCreate` | Plain C# object. Immutable, exclusive, never persisted, not a ScriptableObject, no service container. |
| Active plan for this match | `PlayerControlsProvider.GameplayDevicePlan` | Static, reset at `SubsystemRegistration` with the rest of the provider. |
| Composition point | `GameLevelManager.Awake` -> `PlayerControlsProvider.TryConfigureGameplayDevicePlan(_roster.LocalHumanCount, ...)` | After the roster exists, before `SpawnPlayers`, so no `PlayerController.Start()` order is involved. Fails closed (logs, disables the manager) like a missing spawn point. `OnDestroy` clears it. |
| Launch preflight | `PlayerControlsProvider.TryPreflightGameplayDevices(localHumanCount, out reason)` | Side-effect free. `StartManager.loadGame` asks it before `ActiveMatch.Begin`, and `EndRoundMenuManager.pressNext` asks it before a campaign round reuses the roster (a pad may have been unplugged since; the player stays on the end-round screen and can retry). Any future launch source that can build a multi-human roster must too. Physical availability is deliberately not in `GameModeCompatibility` (pure Core). |
| Consumers | `AcquireGameplayControls(localInputSlot)` / `ReleaseGameplayControls(localInputSlot)` | API unchanged. Controls are still cached per slot; a slot's `PlayerControls.devices` comes from the plan. |

Configuring a plan while gameplay controls from a previous match are still cached (a scene reload can
compose the next match before the old owners release) disposes those controls and logs a warning, rather than
handing the new match controls paired by the old plan. An acquisition with no configured plan (a directly
constructed player, or a test) captures a single-human plan once and keeps it.

### Layouts

| Local humans | Layout | Slot 0 | Slot 1 |
| --- | --- | --- | --- |
| 0 | `None` | - | - |
| 1 | `SingleHuman` | keyboard, mouse, touchscreen, first gamepad (each where present) | - |
| 2, two or more gamepads | `TwoGamepads` | gamepad 0 | gamepad 1 |
| 2, one gamepad and a keyboard | `KeyboardMouseAndGamepad` | keyboard (+ mouse if present) | gamepad 0 |
| 2, anything else | refused | - | - |
| 3+ | refused | - | - |

- **Single human is unchanged.** One local human always gets a plan, even with no device attached, exactly
  as before.
- **Two gamepads means no keyboard/mouse for anyone.** Keyboard and mouse are not given to slot 0 in that
  layout.
- **The keyboard layout requires a keyboard.** A mouse alone plus a gamepad is not a layout. Touchscreen
  never counts as a second player's device and is never assigned in a two-human layout; mobile two-player is
  out of scope.
- **Refusals carry a deterministic reason**, for example `Two local players need two gamepads, or a
  keyboard plus one gamepad. Detected: keyboard, 0 gamepad(s).` A second human is never launched with an
  empty device list.
- **Exclusivity.** No `InputDevice` instance appears under more than one slot. This holds by construction
  and is asserted for every layout.
- **Capture, not re-resolution.** The devices are held by the plan, so re-ordering of `Gamepad.all`, or a
  gamepad being unplugged and another plugged in, cannot silently hand one player's device to the other
  mid-match. Re-acquiring controls (pause, `OnDisable`/`OnEnable`) reuses the captured devices.

### Limitations

- **No hot-plug handling.** A paired device that disappears simply stops producing input for its player.
  A reconnected or replacement device is not adopted, and there is no reassignment, pairing screen or
  remapping UI. That is a future issue.
- **Generic HID joysticks are not included.** `Player/movement` also binds `<Joystick>/stick`, but the
  plan only pairs `Gamepad`-derived devices, as `PlayerControlsProvider` always did.
- **Shared controls are unrestricted.** `PlayerControlsProvider.Controls` (menu navigation, pause/start
  compatibility, the `Other` debug map) is not device-scoped, so pause and menu input work from any device
  regardless of the plan.
- **Three or four local humans are refused** until a layout for them is designed.

### On-screen gamepads are not gamepads

`OnScreenStick` (the AUD-012 Slice 81 pilot in `touch_joystick.prefab`, bound to `<Gamepad>/leftStick`)
makes `OnScreenControl` create a real `Gamepad`-layout device tagged with the `OnScreen` usage.
`touch_joystick.prefab` is nested into every menu and gameplay scene, so `Gamepad.all` contains this
phantom pad on desktop too whenever such a scene is loaded (the two-human PlayMode fixture found exactly one
in a real gameplay scene). Consequences:

- `LocalGameplayDeviceAvailability.Capture()` leaves it out of `Gamepads`, so it can never satisfy "two
  gamepads" or "keyboard plus one gamepad". Without this, a keyboard-only desktop passed the two-player
  preflight and P2 would have been paired to the touch stick.
- A **single human** still owns it: `PlayerControlsProvider` adds the on-screen gamepad(s) to slot 0 when
  the controls are acquired (not when the plan is configured, because the scene's `OnScreenControl` may
  enable after `GameLevelManager.Awake`). This preserves the mobile pilot, whose movement rides that device.
- In a two-human match no player receives it.

### Relationship to `PlayerInput`/`PlayerInputManager`

Not adopted. The device plan is the smallest explicit ownership boundary at the existing provider; migration
plan step 7 (evaluate `PlayerInput`/`PlayerInputManager`) remains a later evaluation, and would replace this
plan rather than sit on top of it.

### First production launch source for two humans

`VersusLauncher.LaunchSimultaneous` (Most Points / The Scrapyard; `versus-architecture.md` section 15) is
the first production launch source that builds a two-`LocalHuman` roster. It runs
`PlayerControlsProvider.TryPreflightGameplayDevices(2, ...)` before issuing anything, as `StartManager`
does, and leaves device assignment to `GameLevelManager` and the match-local plan exactly as above; it
spawns nothing and assigns no device itself. Its EditMode tests override the preflight
(`VersusLauncher.OverrideDevicePreflight`) because a test machine cannot be assumed to have two gamepads;
`LocalSimultaneousVersusProductionPlayModeTests` exercises the real preflight and plan on two virtual
gamepads. Physical two-gamepad and keyboard + gamepad certification is still outstanding.

### Two-human runtime findings

Certifying a two-human match (`Level5TwoHumanLocalInputPlayModeTests`) surfaced runtime gaps that the input
plan alone does not cover. Two were fixed because a two-human match cannot run without them; the rest are
recorded for the `LocalSimultaneous` work.

- **Fixed: a second human was never spawned in most modes.** `SpawnCoordinator.SpawnPlayers` and
  `SpawnBasketballs` returned early when `rules.AllowsCpuShooters` was false, which also dropped every human
  past the first. In authored data only ThreePointContest, VersusCpu, BeatThaComputahs and Lockdown allow CPU
  shooters, while every mode but Lockdown declares `MaxPlayers` 4 - so `GameModeCompatibility` accepted a
  two-human Total Points roster and the runtime silently spawned one player. The gate now applies to CPU
  slots only; one-human-plus-CPU rosters behave exactly as before.
- **Fixed: `BasketBall.Start` threw on the second human's ball.** The debug stats overlay binding assumed a
  single human ball; the first ball deactivates `textBackground`, so the second ball's `GameObject.Find`
  returned null. The overlay is now bound by the primary ball only, the rule `displayUiStats` already used.
- **Open: mode data does not say which modes are meaningful for two humans.** Compatibility accepts two
  humans in any mode with `GameModeDefinition.MaxPlayers >= 2` on any arena with the multiplayer capability;
  authored `MaxPlayers` is 4 for nearly every mode.
- **Fixed: `ArenaCapability.Multiplayer` was granted to every arena.** It is now authored per arena
  (`LevelSelected.LevelSupportsMultiplayer`) and defaults to unsupported; see "Arena multiplayer capability"
  below. The audit that found the problem: a scene
  audit of the authored YAML found: every basketball arena that instances `basketball_goal.prefab`,
  `basketball_goal_circlek`, `_slab`, `_snow` or `_sudan` gets `player_spawn_location1..4` from that prefab
  (this is the arena family the Total Points certification ran on); `level_21_shore` authors all four spawns
  directly in the scene; `level_17_rumble_pit` and `level_18_aveb2` author only `player_spawn_location1`
  directly. `SpawnLocations.Validate` fails a two-human roster on an arena missing `player_spawn_location2`
  with a named error at scene load, which is why the flag was left permissive - but that failure happens
  after the launch, not in compatibility. Only The Scrapyard was exercised at runtime; other arenas were
  audited from authored data, not played.
- **Fixed: the shared camera followed slot 0 only** (see "Shared camera for two local humans").

### Camera and HUD characterization (before the shared camera)

Measured in the PlayMode fixture on Total Points at The Scrapyard (`Camera.main`, both actors driven by
virtual gamepads), on `dev` b0af8f7c7, before the shared camera existed:

- At spawn both humans are inside the frame (viewport P1 = (0.47, 0.52), P2 = (0.32, 0.40)). The initial
  shared-camera presentation is usable for a local multiplayer match.
- `cameraUpdater` follows the pid-0 participant. With P1 standing still, holding P2's stick for 3 seconds
  walked P2 to viewport x = 1.86 - fully off screen - while the camera barely moved. P2 is playable only
  while it stays near P1.
- The match HUD, health bar, stats overlay and `BasketBall.instance` are primary-player-centric by design;
  they were not redesigned.

That measurement is what the shared camera below replaces.

### Arena multiplayer capability

`ArenaCapability.Multiplayer` means "this arena has the player spawn points a local multiplayer roster needs
and has been verified to play with more than one local human". The authored source is unchanged in kind:
`LevelSelected` prefabs (no `Resources/Match` definition assets exist on `dev`, so they remain the source of
truth) carry a new `levelSupportsMultiplayer` field, `LevelPreset` carries it, and
`LevelDefinitionFactory.ResolveCapabilities` turns it into the capability. Nothing else grants it, and
`LevelDefinitionData.Default` no longer includes it.

- Authored `true` only for The Scrapyard (level id 1), the arena the two-human PlayMode certification ran on.
  Every other arena is unsupported until someone certifies it. A missing field deserializes to `false`, so no
  other prefab needed editing.
- `Level5AuthoredMatchDataTests.OnlyArenasThatHaveBeenCertifiedForTwoLocalHumansAuthorMultiplayer` holds the
  certified id list; adding an arena means adding its two-human certification and its id there.
- `GameModeCompatibility` is unchanged and remains the launch authority
  (`roster.LocalHumanCount > 1 && !level.Supports(ArenaCapability.Multiplayer)` -> `ArenaLacksMultiplayer`).
  Nothing in the camera, controllers or spawning re-checks it.
- The per-arena spawn audit above is a starting point for certifying more arenas, not a certification.

### Shared camera for two local humans

`cameraUpdater` chooses its behavior from roster topology, not from a versus flag: when
`MatchRuntime.Roster.LocalHumanCount > 1` it takes an explicit second branch and never enters the
single-target code, which is untouched for every one-human match.

**Targets.** Each tick the branch reads `GameLevelManager.players`, keeps the non-CPU participants and drops
any whose actor is null (destroyed) or inactive. CPUs and basketballs are never targets (remote and replay
participants cannot be seated at all: compatibility rejects them). Two live humans -> shared framing; one -> the same framing over one point
(centred on that human, no zoom); none -> the camera holds where it is. The arithmetic lives in
`SharedCameraFraming` (plain static, tested in EditMode); `cameraUpdater` only smooths and applies it.

**Framing.** The authored gameplay camera is a perspective camera pitched down 13.6 degrees. The camera's base
position follows the midpoint of the humans (x from the midpoint, y by the same `addToCameraPosY` rule as the
single path, z at its authored rest depth), and it zooms by dollying back along its own optical axis, which
keeps the ground point at the middle of the screen fixed. The distance is solved per human in camera space:
`(|lateral| + padding) <= (depth + extra) * tan(horizontal half-FOV)`, largest wins, clamped to
`[0, sharedFramingMaxExtraDistance]`. Measuring in camera space is what makes a player standing close to the
lens (small depth) correctly need more room than one far away. It is measured from where the camera actually is,
not where it is heading, so a fast walker is never left out while smoothing catches up: zooming out is
immediate, zooming back in eases with the camera's existing `smoothSpeed`. The far plane grows by the same
distance. Tunables (serialized on `cameraUpdater`, so prefab-overridable): `sharedFramingPadding` 1.5,
`sharedFramingMaxExtraDistance` 7.

**Characterization of the certified camera** (Total Points at The Scrapyard, `camera_perspective_1`):
perspective, vertical FOV 50, near 0.3, far 40, pitch 13.6 degrees, yaw 0, rest position (-0.9, 1.9, -12.8);
normal movement is world X only (y follows the player at `addToCameraPosY` 1.835, z fixed); `xMin/xMax` are
only applied on the orthographic path, so the perspective camera has no horizontal bounds; `customCamera` is
false; there was no zoom. Spawns: P1 (-1.23, 0, -4.31), P2 (-2.17, 0, -7.16); the playable ground band is
roughly z -10.6 .. -2.6 (depth 2.2 .. 10.2 from the camera); walking speed is about 3.3 units/second; the
court's three-point arc reaches roughly 8 units either side of the rim (x 0.9), estimated from a rendered frame.

**Capacity.** The bound exists because the ground plane ends a few units behind the authored camera: dollying
back much further shows the void under the arena at the bottom of the frame. At the clamp (7) the void is a
strip of roughly the bottom 8% of the frame, and it appears only when the humans are already past what
the framing can hold. The separation the framing keeps both humans in frame for:

| Aspect | Both at ordinary play depth (~6-9 from the lens) | One hugging the near sideline (depth ~2) |
| --- | --- | --- |
| 4:3 (measured; batch mode) | about 12-13 units (test asserts >= 10) | about 8 units |
| 16:9 | about 18 units (computed from the same geometry, not run) | about 12 units |

Beyond that the humans can separate past the frame: the zoom is bounded on purpose, and there is no tether or
push-back. Player 2 can no longer walk off-screen while player 1 stands still *inside that certified
separation*, which covers the court's three-point range at 16:9 (computed, not run).

**Special cameras.**

- *Goal inset* (`camera_perspective_on_goal`, a picture-in-picture toggled by player 0's distance from the
  rim): held closed for the whole two-human match, because the trigger says nothing about the second human.
- *Follow-ball, orthographic, sniper*: single-target (they read the `BasketBall.instance` singleton or one
  player). Not extended. None is reachable from a normal two-human match: `CameraManager` starts on the
  perspective camera and only deactivates the others, `switchCamera()` is not called from any live code path
  and now refuses to cycle when more than one local human is seated, and `cameraUpdater.Start` forces
  `sniperCamera` false. If one of them is ever the active camera in a two-human match it holds still and logs
  a warning once.
- *Custom cameras* (`customCamera`): the same base rule as the single path (height held) is used, plus the
  zoom; no arena authoring `customCamera` is certified for two humans.
- Weather is untouched.

**Known limits.** The HUD, health bar, stats overlay and `BasketBall.instance` remain primary-player-centric.
`GameRules` persists only the primary human's (slot 0) score and all-time stats at match end; slot 1's stats
live and finish correctly in memory but are not saved anywhere. That is result semantics, deliberately left to
the `LocalSimultaneous`/versus work. Three or four local humans are not supported by this branch.

### Automated evidence

| Fixture | Covers |
| --- | --- |
| `Level5LocalGameplayDevicePlanPlayModeTests` | Every layout, refusals and their reasons, exclusivity, immutability; virtual devices under `InputTestFixture`. |
| `Level5TwoHumanLocalRosterTests` (EditMode) | Two-human `PlayerRoster.Build` shape; `ArenaLacksMultiplayer` rejection and acceptance in `GameModeCompatibility`; `MaxPlayers == 1` roster rejection; one human + CPU is not local multiplayer. |
| `Level5ArenaMultiplayerCapabilityTests` (EditMode) | The capability comes only from the authored flag through `LevelSelected` -> `LevelPreset` -> `LevelDefinitionFactory`; nothing grants it by default; compatibility refuses/accepts accordingly. |
| `Level5AuthoredMatchDataTests` (EditMode) | Parity of the authored flag with the resolved capability on every shipped arena; only certified arenas author multiplayer. |
| `Level5SharedCameraFramingTests` (EditMode) | The pure framing math: midpoint/bounds, separation growth, min/max clamp, depth handling, degenerate input, destroyed/disabled/missing targets. |
| `Level5GameplayDevicePlanCompositionGuardTests` (EditMode) | Plan configured before spawn, preflight before `ActiveMatch.Begin` (start menu) and before the campaign round advance, no `Gamepad.all[` in the provider, no Input System in `Level5.Core`. |
| `Level5GameplayDeviceProviderPlayModeTests` | Per-slot pairing, capture across gamepad re-ordering, provider semantics, release, reset, on-screen handling. Runs under `InputTestFixture`. |
| `Level5TwoHumanLocalInputPlayModeTests` | A real two-human match on virtual gamepads and on keyboard + gamepad: two actors, two `GameStats`, distinct balls, input isolation, keyboard-only refusal, unchanged CPU gating. Shared camera on Total Points at The Scrapyard: both humans stay in frame as P2 walks away, crosses P1 and reconverges (per frame), P1 walking away is framed too, camera centred on the pair rather than slot 0, goal inset held closed, zoom bounded under pathological separation, degradation two -> one -> none -> two, a CPU participant never a target, and a two-human match played to its normal `TimeExpired` end and `MatchPhase.Completed` against a throwaway database. |

### Physical certification

**Not run - outstanding manual certification.** No physical gamepad was available where the shared camera
work was done (2026-09-29), so none of the rows below has been exercised on hardware; the shared camera was
checked with virtual devices only, which say nothing about physical-device ownership, pause/resume or scene-load
slot stability. The automated evidence above uses virtual `InputTestFixture` devices only and does not certify
simultaneous-versus readiness. Before this foundation is called production-ready, run on desktop, with a
two-human match composed through the test/dev seam, and record the result here:

| Setup | Each player controls only their own actor | No device controls both | Both actors spawn | Shared camera playable enough | Pause/menu input stable | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Keyboard/mouse + 1 real gamepad | not run | not run | not run | not run | not run | not run |
| 2 real gamepads | not run | not run | not run | not run | not run | not run |

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
distance scaling, the Input System-first movement priority and every action name are unchanged. AUD-012
Phase 5 Slice 81 added a pilot `OnScreenStick` alongside the fallback (see the Slice 81 note above); the
second half of step 3 below - removing the fallback after device playtesting - is still outstanding.

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
3. Add Input System `OnScreenStick` components in Unity scenes/prefabs and bind them to `Player/movement`; then remove the legacy joystick fallback after device playtesting. Piloted (AUD-012 Phase 5 Slice 81): one `OnScreenStick` was added to `touch_joystick.prefab`, bound to `<Gamepad>/leftStick`. The legacy `FloatingJoystick` fallback remains, and removing it is still blocked on device playtesting - not performed this slice.
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
