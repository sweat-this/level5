# Player Input Smoke Validation

Last updated: 2026-09-17 (AUD-012 Phase 5 Slice 80)

This is the validation-gate checklist called out by
[`player-input-architecture.md`](player-input-architecture.md)'s migration plan step 2. It exists so
that future Phase 5 work (`OnScreenStick`/`OnScreenButton` adoption, retained-gesture consolidation,
menu touch migration, and any eventual `activeInputHandler` change) has a concrete, per-behavior
checklist to run against, instead of relying on ad hoc manual passes.

**This slice creates the checklist. It does not perform device or manual validation.** Every item
below is marked with its current status; unless a status explicitly says otherwise, treat the
underlying behavior as unverified by this document. No runtime input behavior, action map, binding,
scene, or prefab changed to produce this checklist.

## How to read this document

Every checklist item states two things:

**Validation type** - one of:

- **Automated** - covered by an existing or addable EditMode/PlayMode test that runs without a human
  or a physical device.
- **Manual desktop** - requires a human running the game with a keyboard/mouse in the Editor or a
  desktop build.
- **Manual gamepad** - requires a human running the game with a physical or virtual gamepad connected.
- **Manual mobile/device** - requires a human running the game on a real Android/iOS device (or the
  Editor's simulated touch, which does not exercise `Input.touchCount`/`Input.touches` the same way
  hardware does).

Every row below states the most specific manual type it needs (desktop, gamepad, or mobile/device) even
when that type's session was unavailable to this agent - the unavailability itself is recorded in the
**Status** column as `Blocked`, not folded into a generic "blocked" validation type.

**Status** - one of:

- **Passing** - verified, with the evidence (test name or session note) cited.
- **Failing** - verified to be broken; see the note for what breaks and why it was not fixed here.
- **Not run** - not yet attempted by any session.
- **Blocked** - attempted or attemptable in principle, but no environment/device was available.
- **Not applicable** - the behavior does not currently exist as a live path (e.g., a commented-out or
  dead consumer), so there is nothing to validate yet.

Real device/OS input (`Input.touchCount`, `Input.touches`, actual gamepad hardware, actual on-screen
character motion resulting from a keypress) cannot be established by static analysis or a headless
test runner - those rows are Manual/Blocked by construction. Pure arithmetic (e.g., touch-distance
scaling) and structural facts (action map ownership, binding lists, gating `#if`s) can be, and where
an automated test already exists it is cited by name.

## Automated validation

Existing automated coverage this checklist relies on, so it is not re-described per row below:

| Test | What it establishes |
| --- | --- |
| `Level5PlayerTouchRetirementTests.SurvivingActionMapsStillExist` | `Player`, `UINavigation`, and `Other` action maps exist; `PlayerTouch` does not. |
| `Level5PlayerTouchRetirementTests.PlayerControlsCompilesIntoLevel5Input` | The generated wrapper compiles into `Level5.Input`. |
| `Level5PlayerInputOtherMapOwnershipTests` (`PlayerRun_KeepsItsShiftKeyboardBinding`, `OtherChange_NoLongerBindsEitherShiftKey`, `OtherChangeAndPlayerRun_ShareNoKeyboardBinding`) | `Player/run` keeps `<Keyboard>/shift`; `Other/change` no longer shares a keyboard binding with it. |
| `Level5PlayerInputReaderCompositionTests` | `PlayerInputReader`'s legacy-joystick composition and touch-distance scaling arithmetic (`ScaleByTouchDistance`), independent of live device input. |
| `Level5ProductionAssemblyBoundaryTests` | Assembly/dependency boundaries the input reader relies on. |
| `Level5PlayerInputSmokeValidationDocTests` | This document exists and still contains every required section heading. |
| `Level5GameLevelManagerDebugToggleGatingTests` (new, Slice 80) | `GameLevelManager.Update`'s `toggle_run_keyboard`/`toggle_stats_keyboard` reads are wrapped in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`. |

None of the tests above exercise a live keypress, a live gamepad, or live touch hardware end to end -
they check ownership, structure, and pure arithmetic. The rows below call out, item by item, what
remains manual and why.

## Keyboard gameplay

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| Movement (WASD/arrows) moves the player | Manual desktop | Not run | `Player/movement` binding existence is automated (table above); that the character actually moves on screen requires a live Play Mode session. |
| Run (Shift) increases move speed / triggers run state | Manual desktop | Not run | Depends on `PlayerController` locomotion/animation state, not exercised by any automated test. |
| Jump | Manual desktop | Not run | `Player/jump` is also read by `PlayerInputReader.BlockHeld` (jump held counts as block-held); both effects need a live check. |
| Shoot | Manual desktop | Not run | Crosses the shot lifecycle - see `docs/shot-lifecycle.md`. This checklist only covers the input read, not shot outcome/make-detection. |
| Call ball | Manual desktop | Not run | Gated by `!reader.DebugChangeHeld`, which now reads `Other/change` on `<Keyboard>/backquote` (Slice 78) rather than Shift; verify pressing Shift-to-run no longer suppresses call-ball, and that backquote (Editor/Development builds only) does. |
| Attack | Manual desktop | Not run | |
| Block | Manual desktop | Not run | |
| Special | Manual desktop | Not run | |
| Pause/start flow (Cancel/Submit) | Manual desktop | Not run | `Pause` reads `Controls.Player.cancel` (toggle pause) and `Controls.Player.submit` (dismiss start-on-pause) via injected `Func<bool>` readers, not directly - verify both a live pause toggle and a start-on-pause dismissal. |

## Gamepad gameplay

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| Movement | Manual gamepad | Blocked | No physical/virtual gamepad was available to this session. `PlayerControls.inputactions` defines a `gamepad` composite binding under `Player/movement`. |
| Run | Manual gamepad | Blocked | |
| Jump | Manual gamepad | Blocked | |
| Shoot | Manual gamepad | Blocked | |
| Call ball | Manual gamepad | Blocked | |
| Attack | Manual gamepad | Blocked | |
| Block | Manual gamepad | Blocked | |
| Special | Manual gamepad | Blocked | |
| Pause/start flow | Manual gamepad | Blocked | `ui-input-architecture.md` also lists gamepad d-pad/stick and `Gamepad/start` navigation as an existing manual smoke item for menus (Phase 1); this row is the gameplay-side equivalent. |

## Editor/Development debug input

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| `Other/change` no longer collides with `Player/run` on the keyboard | Automated | Passing | `Level5PlayerInputOtherMapOwnershipTests` (Slice 78). |
| Holding `Other/change` (backquote) actually suppresses call-ball in an Editor/Development session | Manual desktop | Not run | The binding-collision fix is automated; the live behavioral effect is not. |
| `DebugChangeHeld`/`DebugLightningPressed` return `false` outside Editor/Development builds | Automated (inspection) | Passing | Both are `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`-gated in `PlayerInputReader.cs`, confirmed by source inspection this slice; no test currently asserts this at the compiled-behavior level (see "Outstanding" below), so a future edit to the guard could regress silently. |
| Debug lightning (`Input.GetKeyDown(KeyCode.Alpha8)`, gated by `PlayerControlsProvider.DevChangeControlEnabled`) fires in an Editor/Development session | Manual desktop | Not run | |
| `toggle_run_keyboard` / `toggle_stats_keyboard` (`Other` map) remain reachable outside Editor/Development builds | Automated (inspection) | Passing | Fixed in AUD-012 Phase 5 Slice 80: `GameLevelManager.Update` now wraps both reads (and the `Controls.Other.change.enabled`/`_locked`/`PlayerController1.ToggleRun()`/`BasketBall.instance.toggleUiStats()` logic around them) in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, matching the same pattern already used by `PlayerInputReader.DebugChangeHeld`/`DebugLightningPressed`, `DevFunctions.cs`, and `CheerleaderSwapAnimation.cs`. Guarded by `Level5GameLevelManagerDebugToggleGatingTests`, which fails if either read moves outside that `#if`/`#endif` region. No held-check, action-map, or binding change was made - `Other/change` and `Player/run` are unchanged. |
| `toggle_camera_keyboard` (`Other` map) | Automated (inspection) | Not applicable | Its only reader, in `CameraManager.cs`, is fully commented out - no live consumer exists today, so there is no reachable behavior to validate. |
| `toggle_character_max_stats` (`Other` map) | Automated (inspection) | Not applicable | Its only reader, in `DevFunctions.cs`, is commented out - no live consumer exists today, so there is no reachable behavior to validate. |

## Mobile movement

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| Input System movement path (`Player/movement`) drives movement when present | Manual mobile/device | Blocked | No device was available to this session. Requires a real touch/controller source feeding the Input System action on Android/iOS; the Editor does not reproduce this. |
| Legacy joystick fallback (`FloatingJoystick`) drives movement when the Input System path reads zero | Manual mobile/device | Blocked | Gated to `(UNITY_ANDROID \|\| UNITY_IOS) && !UNITY_EDITOR` in `PlayerInputReader.ReadMove`; cannot be exercised in the Editor or on desktop. The scaling arithmetic it uses is covered separately (see automated table above). |
| No movement occurs when neither an Input System stick nor a legacy touch is present | Manual mobile/device | Blocked | `Input.touchCount == 0` short-circuits to `Vector2.zero` in `ReadLegacyTouchMove`; real device idle-state confirmation is what's missing, not the code path. |

## Mobile gestures/actions

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| Tap (touch 1) queues jump/shoot | Manual mobile/device | Blocked | Requires `!MatchRuntime.Rules.EnemiesOnly`. `TouchInputController.Update` reads `Input.touchCount`/`Input.touches` directly; no automated or Editor-simulated path reaches this. |
| Hold (touch 1, top-left, `PlayerCanBlock` true) sets block-held | Manual mobile/device | Blocked | `x < Screen.safeArea.center.x && y > Screen.safeArea.center.y` - Unity screen space has `y` increasing upward from a bottom-left origin, so this is the top-left quadrant, not bottom-left. Also depends on `MatchRuntime.Rules.EnemiesEnabled`. |
| Second touch (bottom-right) queues attack | Manual mobile/device | Blocked | Requires `!hasBasketball && CanAttack`. |
| Second touch (top-right) queues special | Manual mobile/device | Blocked | Requires `!InAir && Grounded && !KnockedDown` and `Special == MaxSpecial`. |
| Swipe-down (touch 1, top-right, not paused) toggles pause | Manual mobile/device | Blocked | |
| Touch state clears on disable / scene transition | Manual mobile/device | Blocked | `TouchInputController.OnDisable` resets `hold1Detected` and calls `PlayerTouchInputState.Clear()`; a real scene-transition pass is needed to confirm nothing survives across scenes when it shouldn't (see the AUD-012 Phase 2b Slice 26 note in `player-input-architecture.md` about tap-on-teardown being intentionally preserved). |

## Menu and pause

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| Keyboard navigation moves menu selection | Manual desktop | Not run | Also tracked as a Phase 1 manual smoke check in `ui-input-architecture.md`. |
| Gamepad navigation (d-pad/stick, `Gamepad/start`, PS3 HID) moves menu selection | Manual gamepad | Blocked | Same source; no physical gamepad was available to this session. |
| Submit/cancel activates the selected control (keyboard) | Manual desktop | Not run | |
| Submit/cancel activates the selected control (gamepad) | Manual gamepad | Blocked | |
| Touch/click activation invokes the selected control | Manual mobile/device | Blocked | Exercised through `TouchInputController.selectPressedButton` (paused-state raycast) and the seven `TouchInput*Controller` screens; see `ui-input-architecture.md`'s per-screen smoke checks for the authoritative list. |
| No double invocation, keyboard submit fires exactly once | Manual desktop | Not run | Existing per-screen automated coverage checks structural wiring (`Level5ProjectValidator`, canvas-contract tests per `ui-input-architecture.md`), not double-fire behavior itself. |
| No double invocation, gamepad submit fires exactly once | Manual gamepad | Blocked | Same automated-coverage caveat as above. |
| No double invocation, mouse tap fires exactly once | Manual desktop | Not run | Same automated-coverage caveat as above. |
| No double invocation, touch tap fires exactly once | Manual mobile/device | Blocked | Same automated-coverage caveat as above. |
| Selection never becomes null (keyboard/desktop session) | Manual desktop | Not run | `Pause.Update`'s comment notes it re-selects "if paused, keep a selection so navigation and submit always have a target" - confirming that holds across every screen is manual. |
| Selection never becomes null (gamepad/mobile session) | Manual gamepad, Manual mobile/device | Blocked | Same behavior, remaining input sources. |

## Input-backend gate

| Behavior | Validation type | Status | Notes |
| --- | --- | --- | --- |
| `activeInputHandler` is `2` (Both) | Automated (inspection) | Passing | `ProjectSettings/ProjectSettings.asset` line `activeInputHandler: 2`, unchanged by Slices 76-79. |
| Confirmed: legacy `Input.*` reads still block a switch to Input System-only | Automated (inspection) | Passing | Legacy `Input.touchCount`/`Input.touches` remain live in `TouchInputController.Update` and `PlayerInputReader.ReadLegacyTouchMove`; legacy `Input.GetKeyDown` remains live in `PlayerInputReader.DebugLightningPressed`. None of these have an Input System replacement wired up yet (`OnScreenStick`/`OnScreenButton`/`GestureInputAdapter` are all still future work per the migration plan), so `activeInputHandler` correctly stays at `2` (Both) for now. This row exists so any future PR that flips `activeInputHandler` to Input System-only is checked against this list first - see `ui-input-architecture.md`'s 2026-08-31 update for why this is not scaffolding that goes away on its own. Re-check this row (and flip it to a real gap) once any of the three legacy reads above is removed without its replacement landing yet. |

## Outstanding / not run

- No manual desktop, gamepad, or mobile/device pass has been performed for this slice. Every "Not run"
  and "Blocked" row above is exactly that - unverified, not passing.
- The `toggle_run_keyboard`/`toggle_stats_keyboard` Editor/Development gating gap (Editor/Development
  debug input section), surfaced by Slice 79, is fixed as of Slice 80: both reads are now
  `#if UNITY_EDITOR || DEVELOPMENT_BUILD`-gated in `GameLevelManager.Update`, guarded by
  `Level5GameLevelManagerDebugToggleGatingTests`.
- No test currently asserts `DebugChangeHeld`/`DebugLightningPressed` return `false` outside
  Editor/Development builds at the C# level (today this is established by reading the `#if` guard, not
  by a test that compiles both configurations). Adding that assertion is future work, not required for
  this slice.
- `OnScreenStick`/`OnScreenButton` migration, retained-gesture consolidation into one
  `GestureInputAdapter`, and menu `TouchInput*Controller` retirement are unaffected and remain future
  work (migration plan steps 3-6). Do not delete any touch script until its device checklist rows above
  pass.
