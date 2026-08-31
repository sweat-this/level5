# UI Menu System Audit — 2026-08-17

Scope: the ten menu scenes in build settings (`level_00_start_test`, `level_00_options`,
`level_00_stats`, `level_00_progression`, `level_00_credits`, `level_00_account`,
`level_00_account_createNew`, `level_00_account_loginExisting`, `level_00_account_loginLocal`,
`level_00_end_round_screen`), the pause menu, `Assets/Scripts/menu_*`, `Assets/Scripts/input`,
and the canvases, cameras and prefabs those scenes compose.

Two review passes were run. Pass 1 collects findings. Pass 2 re-tests each against the code before
publishing; three Pass 1 candidates were withdrawn there and are recorded below so they are not
re-raised.

New IDs continue the register in [Architecture Audit](architecture-audit.md), which ended at AUD-087.

## Remediation status

Twenty of the twenty-five were fixed in the same session as the audit, on explicit instruction to fix
all findings. See [Status by finding](#status-by-finding) at the end for the per-ID result, including
what was deliberately left and why.

Verified after the changes: `Assembly-CSharp` and `Assembly-CSharp-Editor` compile with 0 errors,
**410/410 EditMode** and **4/4 PlayMode** tests pass under Unity 6000.5.7f1, and
`scripts/validate-repository.ps1` passes. No manual Play Mode pass and no on-device pass has been run
— that gap is what keeps AUD-100 and the remaining items open.

Two findings below were corrected by the fix work itself and are marked inline: AUD-102 was wrong
about `CreditsManager`, and AUD-101 was more serious on paper than in the scene.

---

## Pass 1 — Findings

### A. Asset authoring

**AUD-088 — High — Every menu screen except the start menu is authored in a binary prefab.**
`ProjectSettings/EditorSettings.asset` sets `m_SerializationMode: 2` (Force Text) and `.gitattributes`
declares `*.prefab text eol=lf merge=unityyamlmerge`, but 18 prefabs never got reserialized and are
still Unity 2021.1 binary. Ten of them are UI or menu-critical:

```
Resources/Prefabs/critical/OptionManager.prefab       Resources/Prefabs/menu_stats/StatsManager.prefab
Resources/Prefabs/critical/start.prefab               Resources/Prefabs/menu_progression/progressionScreen.prefab
Resources/Prefabs/critical/GameManager.prefab         Resources/Prefabs/menu_credits/creditsManager.prefab
Resources/Prefabs/critical/touch_joystick.prefab      Resources/Prefabs/menu_account/LoginManager.prefab
Resources/Prefabs/menu_start/StartManager.prefab      Resources/Prefabs/menu_start/main_camera_start.prefab
```

Every one of them contains the screen's `Canvas`, `EventSystem` and buttons (verified by string
extraction). Impact: no menu UI change outside the start menu is reviewable in a diff, merge conflicts
in them are unresolvable, `unityyamlmerge` is being handed binary content, and `git check-attr` confirms
git is told `text: set, eol: lf` on binary blobs. It also means this audit could not read the canvas,
scaler or button wiring for six of the ten screens.

**Solution.** Assets → Reserialize Assets on those ten, committed as one mechanical change with no
behavioural edits, GUIDs preserved. Then add a `Level5ProjectValidator` check that fails when any
`.prefab`/`.unity`/`.asset` under `Assets/` does not begin with `%YAML`.

**AUD-089 — Medium — Two divergent start menu scenes.**
`Constants.SCENE_NAME_level_00_start = "level_00_start_test"`. The shipped start menu is
`Assets/Scenes/level_00_start_test.unity` (15,621 lines, UI authored inline). `level_00_start.unity`
(1,818 lines) is still in the repo, is not in `EditorBuildSettings`, and composes a different prefab
set (`main_camera_start.prefab`, no `StartMenuUiObjects`). Any edit made to the wrong file is silently
lost.

**Solution.** Delete the orphan, rename the shipped scene off `_test`, update `Constants` and
`EditorBuildSettings` in the same commit.

**AUD-090 — Medium — Menu layout is authored as per-scene prefab overrides.**
`level_00_options.unity` carries 59 `m_AnchoredPosition.x`/`.y` plus 59 `m_AnchorMin.y`/`m_AnchorMax.y`
overrides on its `OptionManager` instance; `level_00_start.unity` carries 42. The prefab and its scene
instance disagree about where roughly sixty elements sit, so a layout fix in the prefab does not reach
the scene.

**Solution.** After AUD-088 makes the prefabs diffable, apply the overrides back to the prefabs and
drive layout from `VerticalLayoutGroup`/`HorizontalLayoutGroup` (22 and 16 already exist in the start
menu) rather than absolute anchored positions.

### B. Scaling and rendering

**AUD-091 — High — Three canvases, three different scaling contracts, in one screen.**
`level_00_start_test.unity` serializes three `CanvasScaler` components:

| Canvas | Reference resolution | Scale factor | Screen match |
| --- | --- | --- | --- |
| 9406 | 800 × 400 | 0.9 | width only (`matchWidthOrHeight: 0`) |
| 11287 | 800 × 600 | 1 | width only |
| 14966 | 1920 × 1080 | 1 | width only |

Three co-displayed layers scale at three different rates, so they hold together only at the aspect
they were authored on and drift apart everywhere else. Width-only matching against a 2:1 reference
overflows or floats vertically on 16:9 and 20:9. This is the concrete reason menu layout does not
survive resolution changes.

**Solution.** One scaling contract for every menu canvas: `ScaleWithScreenSize`, reference
1920 × 1080, `matchWidthOrHeight: 0.5`, scale factor 1 — enforced by an edit-mode test over every
menu scene and menu prefab.

**AUD-092 — Medium — Legacy `Text` upscaled from an 800-wide design.**
94 `UnityEngine.UI.Text` components in the start menu; zero TextMeshPro anywhere in
`Assets/Scripts/menu_*`. Text authored against an 800 × 400 / 800 × 600 reference is displayed at
roughly 2.4–2.7× on a 1080p screen.

**Solution.** Fix AUD-091 first (it changes every effective font size), then migrate menu text to TMP
one screen at a time behind the same view-object contract.

**AUD-093 — Medium — Menu cameras are unauthored for URP.**
The `main_camera` in `level_00_options.unity` has only `Transform`, `Camera` and `AudioListener` — no
`UniversalAdditionalCameraData`. URP adds one at load with defaults, so renderer index, post-processing
and anti-aliasing are never authored. The same camera is `m_HDR: 1`, `m_AllowMSAA: 1`, full viewport,
clearing to solid colour behind a Screen-Space-Overlay canvas: a full-resolution HDR clear every frame
that renders nothing.

**Solution.** Author `UniversalAdditionalCameraData` on every menu camera; disable HDR and MSAA on
them; consider a reduced render scale for menu scenes on mobile.

**AUD-094 — Low — Quality settings are authored in one place and overwritten in another.**
`ProjectSettings/QualitySettings.asset` has exactly one level ("Fastest", `antiAliasing: 0`,
`vSyncCount: 0`), and `PlatformCheck.Awake` sets `QualitySettings.vSyncCount = 1` at runtime on every
platform.

**Solution.** Decide which is authoritative and delete the other.

### C. Input and interaction

**AUD-095 — High — Two independent UI input action sets are live at once.**
`UiSelectionAdapter.EnsureInputSystemUiModule()` calls `inputSystemUIInputModule.AssignDefaultActions()`
(`input/UiSelectionAdapter.cs:139`), so the module runs Unity's built-in `DefaultInputActions`, **not**
`PlayerControls.UINavigation`. The project's own menu bindings are therefore not what drives standard UI
navigation. From `PlayerControls.inputactions`, the bindings that do not exist in Unity's defaults and
so no longer reach the UI:

- `Up`/`Down`/`Left`/`Right` ← `<Keyboard>/w`,`/s`,`/a`,`/d`
- `Submit` ← `<Gamepad>/start`
- every `<HID::Sony PLAYSTATION(R)3 Controller>` binding (nav, submit, cancel)

On the two screens already migrated to `Button.onClick` (Options, EndRound) those inputs silently stopped
working. `AssignDefaultActions()` also allocates a fresh `InputActionAsset` per module setup per scene load.

**Solution.** Assign `PlayerControls.UINavigation` to the module's `actionsAsset` and map
`move`/`submit`/`cancel`/`point`/`leftClick` explicitly; delete the `AssignDefaultActions()` path; serialize
a configured `InputSystemUIInputModule` on each menu's `EventSystem` instead of `AddComponent`-ing it at
runtime. `PlayerControls.UINavigation` needs a `Vector2` `Navigate` action (it currently has four separate
button actions, which the module cannot consume).

**AUD-096 — High — `StatsManager` responds twice to one directional press.**
`StatsManager.HandleSelectedStatsControl` polls `controls.UINavigation.Left/Right/Up/Down.triggered`
(`menu_stats/StatsManager.cs:383-396`) with no `InputSystemUiActive` gate and no per-frame guard, while the
UI module's Navigate consumes the same press. One Left press both moves EventSystem selection and runs
`DecreaseLocalPage()`/`ChangeLocalModeLeft()` — each of which issues a **synchronous SQLite query on the main
thread** (`changeHighScoreDataDisplay`, `menu_stats/StatsManager.cs:853-883`). `ProgressionManager.HandleProgressionInput`
has the same ungated shape for `Submit`→`addPoint` and `Cancel`→`subtractPoint`.

Note: `StartManager.RunCommand`/`RunOptionAction` and `ProgressionManager.RunProgressionAction` do carry
`Time.frameCount` guards, so the *double-actuation* half is contained on those two screens. `StatsManager` has
no such guard at all, and the *selection-moves-and-value-changes* half is unguarded everywhere.

**Solution.** Route value changes through `Button.onClick` only; delete the polled directional handling from
menu `Update`. Where a control genuinely needs left/right stepping, give it explicit stepper buttons rather
than overloading the navigation axis.

**AUD-097 — Medium — Start-menu option cycling is behind an invisible latch.**
`StartManager.ShouldUseManualMenuInput` (`menu_start/StartManager.cs:689-694`) returns false while the UI
module is active and `selectedObject != lastSelectedObject`, and `lastSelectedObject` is assigned only after a
Submit (line 354). So keyboard/gamepad option cycling on the start menu is unreachable until the player presses
Submit on that control, then stays on until they move away — and while on, Up/Down both cycles the value and
moves selection. Nothing renders which mode the control is in.

**Solution.** Remove the latch with the polled navigation in AUD-096; make stepping explicit and always
available through onClick.

**AUD-098 — High — Controls that mouse and touch cannot operate.**
Two screens dispatch only from the polled Submit action and register no `onClick`:

- `Pause` (`game manager/Pause.cs:233-258`) compares `currentHighlightedButton.name` against
  `loadSceneButton.name`, `loadStartScreenButton.name`, `cancelMenuButton.name`, `quitGameButton.name` under
  `PlayerControlsProvider.MenuSubmitTriggered`. Clicking a pause button does nothing.
- `ProgressionManager.RegisterButtonCallbacks` wires start/stats/quit/save/reset/confirm/cancel but **not** the
  progression stat buttons, so points can only be spent with the Submit key.

**Solution.** Wire both through `onClick`, keeping the name comparison only until the click route is verified
on device.

**AUD-099 — Medium — The pause menu re-selects its button every frame.**
`Pause.Update` calls `currentHighlightedButton.GetComponent<Button>().OnSelect(null)` and then `.Select()` on
every frame while paused (`game manager/Pause.cs:228-229`). That restarts the Selectable state transition each
frame and takes selection ownership away from the EventSystem.

**Solution.** Set selection once when the pause menu opens; delete the per-frame re-select.

**AUD-100 — Medium — Seven near-identical touch controllers.**
`TouchInputController`, `TouchInputStartScreenController`, `TouchInputStatsScreenController`,
`TouchInputProgressionScreenController`, `TouchInputOptionsScreenController`, `TouchInputLinksScreenController`,
`TouchInputAccountScreenController` and `TouchInputEndRoundMenuController` each re-implement the same ~150 lines:
touch polling, swipe tolerances derived from `Screen.height`, previous-selection restore, double-tap submit, and a
per-screen name→scene dispatch table that duplicates the manager's own button actions. They are the only reason
`ProjectSettings/ProjectSettings.asset` still needs `activeInputHandler: 2` (both input backends) for `Input.touches`.

**Solution.** They are already self-disabling when the Input System UI module is available
(`if (UiSelectionAdapter.InputSystemUiActive) { enabled = false; return; }`). Once AUD-095 makes that path correct on
device, delete them and drop `activeInputHandler` to Input System only.

**AUD-101 — Low (was Medium) - Dead touch controllers for other screens sat in the start menu.**

> **Corrected during remediation.** Each stray controller sits on its own dedicated holder object
> (`touchInputAccountScreen`, `touchInputStatsScreen`, `touchInput`, ...), not on shared UI, so its
> `SetActive(false)` only switched off its own holder. Dead weight, not a shared-object shutdown.
`level_00_start_test.unity` contains `TouchInputAccountScreenController` ×2, `TouchInputStatsScreenController`,
`TouchInputProgressionScreenController` and `TouchInputController` — none of whose managers exist in that scene. Each
one's initializer ends `else { gameObject.SetActive(false); }`, so a failed manager lookup deactivates its **host**
GameObject. Which object goes away depends on which controller happens to fail first.

**Solution.** Remove the stray components from the scene. Subsumed entirely by AUD-100 if the controllers are deleted.

**AUD-102 — Medium - `OnEnable`/`OnDisable` asymmetry leaves buttons inert.**

> **Corrected during remediation.** `CreditsManager` already re-registered in `OnEnable`; this
> finding was wrong about it. Only `OptionsManager` and `EndRoundMenuManager` had the asymmetry.

`OptionsManager` and `EndRoundMenuManager` call `UnregisterButtonCallbacks()` in `OnDisable` but do
not re-register in `OnEnable`. `StartManager`, `StatsManager` and `ProgressionManager` do
(`if (initialized) RegisterButtonCallbacks();`). Disabling and re-enabling one of the first three leaves every button
on that screen dead.

**Solution.** One shared base or helper that owns the register/unregister pair symmetrically.

### D. Wiring and ownership

**AUD-103 — High — Three competing wiring mechanisms.**
Menu screens resolve their UI in three different ways at once:

1. Serialized view objects — `StartMenuUiObjects` (~60 fields), `EndRoundUIObjects`.
2. `SceneObjects.Find` with a collected-missing-names report — used only by `Pause`.
3. Raw `GameObject.Find(nameConstant)` — `OptionsManager`, `CreditsManager`, `StatsManager`,
   `ProgressionManager`, `AccountManager`, and `StartManager` for its seven footer buttons.

`StartManager` uses (1) for option controls and (3) for command buttons in the same class. `GameObject.Find` cannot
see inactive objects, so any panel authored disabled resolves null. `StartManager.ResolveButton`
(`menu_start/StartManager.cs:707-733`) then falls back to `Resources.FindObjectsOfTypeAll<Button>()`, which walks every
loaded `Button` in the process including prefab assets and hidden objects.

**Solution.** `SceneObjects` and the `*UiObjects` pattern already exist and already have validator support
(`ProgressionManager.RequiredProgressionObjectNames` is asserted by `Level5CombatMathTests`). Standardise every screen
on a serialized `*UiObjects` view component; keep `SceneObjects.Find` only as the reporting fallback; delete
`Resources.FindObjectsOfTypeAll`.

**AUD-104 — Medium — The footer nav bar exists five times.**
`press_start`, `stats_menu`, `options_menu`, `credits_menu`, `update_menu`, `account_menu` and `quit_game` are
re-declared as name constants and re-wired to identical scene-load handlers in `StartManager`, `OptionsManager`,
`CreditsManager`, `StatsManager` and `ProgressionManager`.

**Solution.** One `MenuFooterNav` component owning the seven buttons and their scene loads, referenced by each screen.

**AUD-105 — Medium — Two contradictory button-registration policies.**
`UiSelectionAdapter.RegisterButton` skips the code listener when the button already has inspector-authored persistent
listeners (`input/UiSelectionAdapter.cs:97-102`). `RegisterRequiredButtonCallback` — duplicated verbatim in
`StartManager`, `StatsManager`, `ProgressionManager`, `CreditsManager` and `EndRoundMenuManager` — always adds. Whether a
button's behaviour comes from code or from the scene depends on which helper the screen happens to use, and for the
AUD-088 prefabs the scene half is unreadable.

**Solution.** Pick one policy — code owns behaviour, scene authors nothing — assert zero persistent `onClick` listeners
on menu buttons in the validator, and delete the duplicated helper.

### E. Per-frame cost and correctness

**AUD-106 — High — The high-score row prefab is mutated before it is instantiated.**
`StatsManager.Start` (`menu_stats/StatsManager.cs:263-267`):

```csharp
StatsTableHighScoreRow row = highScoreRowPrefab.GetComponent<StatsTableHighScoreRow>();
StatsTableHighScoreRow source = i < highScoreRowsDataList.Count ? highScoreRowsDataList[i] : null;
CopyHighScoreRow(row, source);
Instantiate(highScoreRowPrefab, highScoresRowsObject.transform.position, Quaternion.identity, highScoresRowsObject.transform);
```

Row data is written into the shared `Resources`-loaded prefab and then copied out by instantiation. This is the same
defect class as the already-closed AUD-020 (traffic vehicles). In the Editor it dirties the prefab asset on disk, which
is why that asset picks up spurious diffs.

**Solution.** Instantiate first, then write the data into the instance. Use `Instantiate(prefab, parent, false)` so the
row inherits the layout group's local space instead of a world position.

**AUD-107 — High — High-score row order is undefined.**
`highScoreRowsObjectsList = GameObject.FindGameObjectsWithTag(highScoreRowTag).ToList()`
(`menu_stats/StatsManager.cs:270`) is then indexed `[i]` against the ordered query result
(`SetHighScoreRow(i, highScoreRowsDataList[i])`). `FindGameObjectsWithTag` guarantees no order and returns only active
objects, so the displayed leaderboard order does not have to match the ranked order it was queried in.

**Solution.** Keep the instantiated rows in the list returned by the instantiation loop, in creation order. Never
re-derive row identity from a tag search.

**AUD-108 — Medium — Rows write six `Text` fields every frame and bind by child index.**
`StatsTableHighScoreRow.Update` assigns `scoreText/characterText/levelText/dateText/hardcoreText/userNameText` every
frame, and `Start` binds them by `transform.GetChild(0..5)`. Reordering the row prefab's children silently reassigns
every column. `setRowValues` also never assigns `TrafficEnabled`, `EnemiesEnabled` or `Platform`, which are serialized
but dead.

**Solution.** Serialize the six `Text` references; assign them once in a `Bind(rowData)` method; delete `Update`.

**AUD-109 — Medium — Every menu manager dispatches from a per-frame selected-object name.**
`StartManager`, `StatsManager`, `ProgressionManager`, `OptionsManager`, `CreditsManager`, `EndRoundMenuManager` and
`AccountManager` each read `EventSystem.current.currentSelectedGameObject.name` into a string field every frame and
run a chain of `string.Equals` against it. Two specific costs:

- `OptionsManager.Update` (`menu_options/OptionsManager.cs:79-104`) calls `DisplayControls(...)` — four `SetActive`
  calls plus four `string.Contains` — on every frame the selection rests on a controls button.
- `StartManager.Update` contains the level-select and mode-select blocks **twice** (lines 377-384 and again at
  390-397), refreshing both displays twice per frame.

Additionally, `StartManager.Update`'s entire body is inside `#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_EDITOR_OSX`
(lines 333-623), so the start menu runs a materially different interaction model on device than in the editor.

**Solution.** Drive display refresh from selection-changed events (`ISelectHandler` on the control, or a single
`EventSystem` selection watcher) rather than a per-frame name comparison, and delete the platform conditional once the
onClick route is the only route.

**AUD-110 — Medium — Two unguarded `GameObject.Find(...).GetComponent<Text>()` chains in `StartManager`.**

- Line 1565: `friendSelectOptionText = GameObject.Find(FriendSelectOptionButtonName).GetComponent<Text>();` sits inside
  a `catch` that only `Debug.Log`s, so a rename or an inactive object makes the friend display fail silently.
- Line 1857: `GameObject.Find("messageDisplay").GetComponent<Text>()` in `turnOffMessageLogDisplayAfterSeconds` has no
  guard at all, and `messageDisplay` does not appear in `level_00_start_test.unity`.

**Solution.** Route both through the screen's `*UiObjects` contract; if `messageDisplay` is genuinely cross-scene, take
it from `messageLog.instance` with a null check.

**AUD-111 — Low — Menu defects are logged, not surfaced.**
23 `catch (Exception)` blocks across `Assets/Scripts/menu_*`, 16 of which do nothing but
`Debug.Log("ERROR : " + e)`. Display failures become log noise rather than test failures.

**Solution.** Narrow the catches to the operations that can actually throw (database and network calls); let
programming errors surface.

### F. Coverage

**AUD-112 — Medium — No test drives any menu screen.**
`Level5ProjectValidator.CollectGameplaySceneObjectErrors()` enforces the name contract for **gameplay** scenes only;
the ten menu scenes — which name-resolve considerably more objects — are outside it. The only menu references in
`Assets/Tests` are source-text architecture assertions (`Level5PlayerSelectArchitectureTests`,
`Level5MatchArchitectureTests`) and one name-constant contract (`ProgressionManager.RequiredProgressionObjectNames`
via `Level5CombatMathTests`). Nothing loads a menu scene or exercises a menu interaction.

**Solution.** Extend the scene-object contract to menu scenes, add the canvas-contract test from AUD-091, and add a
play-mode smoke test per screen: scene loads, `EventSystem` exists with a configured `InputSystemUIInputModule`, the
first selected object is non-null, and invoking each footer button loads the expected scene.

---

## Pass 2 — Re-review

Each Pass 1 candidate was re-tested against the code. Three were withdrawn, and two were re-scoped.

**Withdrawn — double `onClick` invocation on the start menu.** The initial read of
`StartManager.Update` (lines 350-358) showed `PlayerControlsProvider.MenuSubmitTriggered` calling
`UiSelectionAdapter.TryInvokeSelectedButton` without consulting `useManualMenuInput`, which would fire `onClick` twice
per Submit alongside the UI module. It does not: `RunCommand` and `RunOptionAction` (lines 886-937) both drop the
second call in the same frame via `lastCommandFrame`/`lastOptionFrame == Time.frameCount`, and
`ProgressionManager.RunProgressionAction` does the same with `lastActionFrame`. The double-actuation is real but already
guarded on those two screens. What survives is AUD-096 (`StatsManager` has no equivalent guard) and AUD-097 (the latch
those guards do not address).

**Withdrawn — `StandaloneInputModule` would throw under the new Input System.**
`ProjectSettings/ProjectSettings.asset` has `activeInputHandler: 2` (both backends), so the fallback module and the
`Input.touches` polling in the touch controllers are functional. The cost is that the legacy backend cannot be turned
off, which is recorded as part of AUD-100 rather than as a defect of its own.

**Withdrawn — git is corrupting the binary prefabs.** `git ls-files --eol` reports
`i/-text w/-text attr/text eol=lf` and the working tree is clean, so no normalization is happening today. The attribute
mismatch is a latent hazard, not active damage; it is folded into AUD-088's solution rather than raised separately.

**Re-scoped — canvas scaling.** The first pass recorded "no `CanvasScaler` in the start menu", from a grep for
`CanvasScaler:` in the scene YAML. uGUI components are `MonoBehaviour`s serialized by script GUID, so that grep could
not match. Resolving GUID `0cd44c1031e13a943bb63640046fad76` found three scalers — and the real finding, AUD-091, is
worse than the original: they are present but mutually inconsistent.

**Re-scoped — `StartManager` still uses `GameObject.Find` for everything.** It does not; `GetUiObjectReferences`
resolves roughly forty references through `StartMenuUiObjects`. The finding narrowed to AUD-103: the same class uses two
mechanisms, with `GameObject.Find` retained only for the seven footer buttons — which is also what makes AUD-104
(one footer, five copies) the cheap fix.

**Confirmed under re-test.** AUD-088 (binary format verified by header bytes; component names recovered by string
extraction), AUD-091 (three scaler blocks read directly), AUD-095 (`AssignDefaultActions` at
`UiSelectionAdapter.cs:139` against the `PlayerControls.inputactions` binding list), AUD-098 (`Pause` and
`ProgressionManager` registration lists read in full), AUD-106 and AUD-107 (`StatsManager.cs:263-270`),
AUD-112 (`Assets/Tests` grepped in full).

---

## Severity Summary

| Severity | IDs |
| --- | --- |
| High | AUD-088, AUD-091, AUD-095, AUD-096, AUD-098, AUD-103, AUD-106, AUD-107 |
| Medium | AUD-089, AUD-090, AUD-092, AUD-093, AUD-097, AUD-099, AUD-100, AUD-101, AUD-102, AUD-104, AUD-105, AUD-108, AUD-109, AUD-110, AUD-112 |
| Low | AUD-094, AUD-111 |

## Scope Classification

- `REQUIRED` — AUD-088, AUD-091, AUD-095, AUD-096, AUD-098, AUD-106, AUD-107, AUD-112. These are correctness or
  reviewability blockers; the rest of the work cannot be verified without them.
- `HIGH-VALUE` — AUD-089, AUD-097, AUD-099, AUD-101, AUD-102, AUD-103, AUD-104, AUD-105, AUD-108, AUD-109, AUD-110.
  Small, adjacent, and each removes a whole class of the above.
- `LATER` — AUD-090, AUD-092, AUD-093, AUD-100. Layout re-authoring, the TMP migration, URP camera authoring and
  deleting the touch controllers all depend on the earlier phases landing and on device verification.
- `REJECT` — none. AUD-094 and AUD-111 are Low but are one-line changes inside work already being done.

---

## Status by finding

Fixed and verified (compile + 410/410 EditMode + 4/4 PlayMode + `validate-repository.ps1`):

| ID | What changed |
| --- | --- |
| AUD-088 | 91 assets reserialized to text over two passes via a new `Level5/Reserialize Binary Assets` menu item. Nine of the ten menu prefabs converted; every scene is now text. A `CollectBinarySerializedAssetErrors` validator plus a `UnityAssetsAreTextSerialized` test guards the regression. |
| AUD-089 | Orphaned `level_00_start.unity` deleted; the shipped `level_00_start_test.unity` renamed onto it with its GUID preserved; `Constants.cs` and `EditorBuildSettings.asset` updated. This also repointed `PlayerSelectSceneValidation`, which had been validating the dead scene. |
| AUD-091 | One contract - ScaleWithScreenSize, 1920x1080, match 0.5, scale 1 - applied to all 19 menu canvases across 6 prefabs and 7 scenes, enforced by `MenuCanvasesShareOneScalingContract`. |
| AUD-093 | `UniversalAdditionalCameraData` authored on all 11 menu cameras, with shadows, post-processing and HDR output off. |
| AUD-094 | `QualitySettings.asset` aligned to `vSyncCount: 1`; `PlatformCheck` documented as the owner. |
| AUD-095 | `AssignDefaultActions()` removed. A `Vector2` `Navigate` action plus `Point`/`Click`/`ScrollWheel` added to `PlayerControls.UINavigation`, mirroring the existing WASD, arrow, d-pad, stick and PS3 HID bindings; the module's move/submit/cancel/point/leftClick/scrollWheel bind to them, references cached. |
| AUD-096 | Polled directional and submit handling deleted from `StatsManager` and `ProgressionManager`; `StatsManager.RunStatsAction` gained the `Time.frameCount` guard it never had. |
| AUD-097 | `ShouldUseManualMenuInput` and the latched navigation/cycling block deleted; the `#if UNITY_EDITOR \|\| UNITY_STANDALONE` guard around `StartManager.Update` removed, so device and editor run one model. |
| AUD-098 | `Pause`'s four actions, the three progression stat buttons and the progression character stepper wired to `Button.onClick`. |
| AUD-099 | Per-frame `OnSelect(null)`/`Select()` removed from `Pause.Update`. |
| AUD-101 | The five foreign touch-controller holder objects removed from the start scene through a `Level5/Remove Foreign Touch Controllers From Start Scene` editor utility. |
| AUD-102 | `OnEnable` re-registration added to `OptionsManager` and `EndRoundMenuManager`, and to `Pause`, which gained the same pair. |
| AUD-103 (partial) | `Resources.FindObjectsOfTypeAll<Button>()` removed from `StartManager.ResolveButton`, which now reports an unresolved button by name. |
| AUD-105 | One policy - code owns menu button behaviour. `UiSelectionAdapter.RegisterButton` no longer defers to inspector-authored listeners, and the five verbatim `RegisterRequiredButtonCallback` copies were deleted. |
| AUD-106 | Rows are instantiated first and written into the instance; the shared `Resources` prefab is no longer mutated. |
| AUD-107 | `highScoreRowsObjectsList` is built in creation order; `GameObject.FindGameObjectsWithTag` and the now-unused tag constant removed. |
| AUD-108 | The six `Text` references are serialized on `highScoreRow.prefab` and bound once through a new `Bind()`; `Update` deleted. The child-index binding survives as a logged fallback. |
| AUD-109 | Display refresh moved to selection-change in `StartManager` and `OptionsManager`; the duplicated level-select and mode-select blocks removed. |
| AUD-110 | Both unguarded `GameObject.Find(...).GetComponent<Text>()` chains replaced - the friend label uses the reference `StartMenuUiObjects` already holds, the message line goes through `SceneObjects.Find`. |
| AUD-111 (partial) | The 11 `Debug.Log("ERROR : ")` sites promoted to `Debug.LogError` so menu failures surface. Catch scopes were not narrowed. |
| AUD-112 | Menu scenes brought inside `CollectGameplaySceneObjectErrors` via new `RequiredSceneObjectNames` contracts on `StartManager`, `OptionsManager` and `StatsManager`, plus two new tests. The per-screen play-mode smoke test was not written. |

Deliberately left open:

| ID | Why |
| --- | --- |
| AUD-092 | The TMP migration is 94 `Text` components in the start menu alone. The plan stages it one screen per PR behind the view-object contract, gated on AUD-091 being visually settled. |
| AUD-100 | Deleting the seven `TouchInput*Controller` scripts and dropping `activeInputHandler` to Input System only removes the fallback mobile input path. AUD-095 changed what drives UI input and that has not been verified on a device, so the fallback stays until it has. |
| AUD-103 (rest) | A serialized `*UiObjects` view component per screen means wiring each one in its prefab by hand. Now unblocked, since the prefabs are text, but it is a per-screen job with no automated coverage to catch a mis-wire. |
| AUD-104 | `MenuFooterNav` touches footer wiring in five managers at once, and nothing in the suite exercises menu button behaviour, so a silent break would not be caught. Best done alongside the AUD-103 view objects, one screen at a time. |
| AUD-111 (rest) | Narrowing 23 catch scopes needs per-site judgement about which call can actually throw. Raising the log level was the part that could be made safe without that analysis. |

### AUD-090 remediation — 2026-08-27

A current-source audit at `e7dcce673c11f437e9bf78e6f9a5f7abb3ede9a3` revised the original AUD-090
diagnosis: most of the ~100 per-scene overrides this finding described were not divergent layout at
all, just stale overrides whose serialized value already equalled the prefab's. `PrefabUtility.
GetPropertyModifications` was compared against each modification's own source-prefab
`SerializedProperty` (never runtime RectTransform state, which the prefabs' `LayoutGroup`s can alter
independently of authoring), classified into child-layout/root-composition/semantic/unknown, and
child-layout entries split into redundant vs. genuinely divergent - all before anything was changed.
Full characterization, root-cause evidence and per-property matrices live in the tool that did this,
`Assets/Level5/Editor/MenuLayoutOwnershipMigration.cs`.

| Screen | Total mods | Redundant child-layout (removed) | Divergent child-layout (resolved) | Root composition (kept) | Semantic (kept) | Unknown |
| --- | --- | --- | --- | --- | --- | --- |
| Options / `OptionManager.prefab` | 269 | 182 | 60 | 22 | 5 | 0 |
| Stats / `StatsManager.prefab` | 270 | 244 | 0 | 22 | 0 | 4 (pre-existing dangling references to a deleted RectTransform; incidentally dropped as a side effect of `SetPropertyModifications`, not something this migration set out to touch) |
| Progression / `progressionScreen.prefab` | 136 | 114 | 0 | 22 | 0 | 0 |
| Credits / `creditsManager.prefab` | 118 | 83 | 9 | 22 | 4 | 0 |

Redundant overrides (623 total) were stripped by `Level5/Normalize Menu Layout Overrides`, which never
touches root or semantic properties and only reverts a child RectTransform override whose scene value
already equals the prefab's.

The two divergent clusters were resolved deliberately, not by a blanket apply/revert, via
`Level5/Resolve Menu Layout Divergences`:

- **Options `keyboardMouse_keys` (60 properties, accidental drift).** Every child under this panel was
  zeroed (anchor 0/0, position 0,0) in the scene, while the prefab held a structured per-row vertical
  list matching the sibling `keyboardOnly_keys`/`gamepad_keys`/`touch_keys` panels. `OptionsManager`
  shows the panel purely via `SetActiveIfNotNull` (`OptionsManager.cs:268`, panel is inactive by
  default - `Start()` selects `keyboardOnly` first) with no repositioning, so the scene's zeroed state
  would have stacked every control row on top of itself the moment "keyboard+mouse" was selected. The
  prefab was correct; the scene overrides were reverted. No change to the screen's default appearance.
- **Credits `nft_airdrop` × 2 (9 properties, prefab should own the scene's value).** Both objects are
  active and wired to `CreditsManager.OpenNftAirdrop()`, but the prefab collapsed each to a zero-size
  point at its anchor origin - a real, clickable element with no authored layout. The scene already
  held the sized, positioned values rendering today (unchanged effective visual result); they were
  pushed into the prefab and the now-redundant scene overrides removed in the same pass.

`Level5ProjectValidator.CollectMenuLayoutOverrideContractErrors()` (exercised by
`Level5SceneContractTests.PrefabDrivenMenuScreensDoNotOverridePrefabOwnedChildLayout`) now fails if a
child-layout override reappears on any of the four instances; it does not forbid root or semantic
overrides. Verified idempotent: a second `Normalize`/`Resolve` pass changes nothing. Compile +
576/576 EditMode + 13/13 PlayMode + `validate-repository.ps1` all pass after the change. No manual
multi-resolution Play Mode/Game View pass was run in this session (no interactive GUI available to this
agent) - see the AUD-090 completion report for what that leaves open.

Found while fixing, not part of this audit:

- **Seven prefabs cannot be reserialized because they contain missing MonoBehaviour scripts** -
  `menu_start/StartManager.prefab`, `camera/Cameras.prefab`, `camera/Cameras _Mobile.prefab`,
  `critical/NavMesh.prefab`, `enemy_misc/enemyShotMarker.prefab`,
  `auto_players/enemy_executioner_auto.prefab` and `they_live/Sunglasses.prefab`. Unity refuses to
  save them: "You are trying to save a Prefab with a missing script." Being binary is why nothing
  caught this earlier - `UnityAssetsDoNotHaveMissingScriptReferences` parses YAML, so it could not
  see inside them. Three are live (`Cameras _Mobile` referenced by 22 assets, `Sunglasses` by 26,
  `NavMesh` by one); four are referenced by nothing, `menu_start/StartManager.prefab` among them - a
  Unity 2020.2 orphan superseded by `start_manager_test.prefab`. They are allowlisted in
  `Level5ProjectValidator.BinaryAssetsBlockedByMissingScripts`; repairing the scripts is
  gameplay-asset work, and shrinking that list to empty is the follow-up.

### AUD-111 remediation — 2026-08-31

A current-source audit at `18b8f06eaf806e5c41d66b4bd3d4097c85e50cab` inventoried every remaining
`catch (Exception)` in `Assets/Scripts/menu_*` (20 sites: 5 in `StartManager`, 2 in
`ProgressionManager`, 4 in `StatsManager`, 1 in `StatsTableAllTime`, 2 in `LoadManager`, plus 6 in
the progression persistence trio) and classified each against the actual APIs it wraps rather than
against what the site's own comments assumed.

The load-bearing finding: every `DBHelper`/`DBConnector` method these screens call (SQLite reads/
writes, table-existence checks, profile lookups) already catches internally and reports failure
through its return value - an empty list/`0`/`false`, or `null` specifically for
`getUnsubmittedHighScoreFromDatabase()` - and never lets a database exception reach its caller.
None of the 15 non-persistence catches were therefore guarding a real database-exception boundary;
each was either dead code around an already-safe call, or (twice, in `StatsManager`) silently
converting an unchecked `null` return into a caught `NullReferenceException`. Both were rewritten
as explicit `null` guards instead of `catch`.

- `StartManager` - all 5 catches removed. `FocusPlayersTab` and `initializeOptionsDisplay` call
  only already-null-safe helpers (`SetTabObjectActive`, `PlayerSelectCoordinator.FocusPrimary`, per
  AUD-110). `initializefriendDisplay` gained an explicit guard mirroring the class's existing
  `HasLoadedGameSetup`/`DescribeMissingGameSetup` precondition pattern instead of catching the index/
  null failure it was disguising. `RunCommand`/`RunOptionAction` kept their `buttonPressed` guard and
  `finally` cleanup, dropped only the `catch`.
- `ProgressionManager` - both catches removed the same way: `RunProgressionAction` kept its
  `finally`; `initializePlayerDisplay` now uses the class's own pre-existing `HasSelectedCharacterData()`
  guard (already used at every other call site in the file) instead of a `catch`.
- `StatsManager` - `Awake()`'s catch removed (dead code around a call that cannot throw or return
  null). The `getUnsubmittedHighscores()`/`SubmitUnsubmittedScoresCoroutine` catches were replaced
  with an explicit `null` check on `getUnsubmittedHighScoreFromDatabase()`'s return value - the
  method's actual failure signal - preserving the existing "scores unavailable" UI state.
  `changeHighScoreDataDisplay()` was restructured per the required flow: query-parameter lookup
  (which can legitimately throw on a bad `currentModeSelectedIndex` - a programming defect, not a
  database failure) now runs before the database calls, and `DatabaseLocked` is released via
  `finally` instead of duplicated in both the success and catch paths.
- `StatsTableAllTime` - `Start()`'s catch removed; the eleven `DBHelper` reads inside
  `loadAllTimeStats()` all follow the same "catch internally, return a safe default" pattern, so no
  snapshot/result type was needed to separate acquisition from rendering.
- `LoadManager` (the highest-risk change) - both catches removed. `LoadedData.cs` already
  implements the product's real database-unavailable/default-data path: it polls
  `LoadManager`'s readiness flags, explicitly validates the result (`HasAllRequiredData()`), and
  only then calls `TryLoadFallbackData()` - independent of whether `LoadAllDataCoroutine` throws.
  `LoadAllDataCoroutine`'s own catch was therefore the thing actively working against AUD-111: since
  every database call inside it is already exception-safe, its only observable effect was to catch a
  genuine catalog/authoring defect (e.g. from `LevelCatalog.FromLevelSelected`) and silently
  reclassify it as a database fallback. Removing it lets such a defect surface through Unity's own
  exception logging while `LoadedData`'s independent retry/fallback remains the actual degraded-start
  behavior, unchanged. `TryLoadFallbackData()` itself lost its catch for the same reason: its one
  expected failure mode (an empty default catalog) was already reported explicitly via the
  `xDataLoaded` flags it already computes.
- Progression persistence (`ProgressionResultStore`, `PendingProgressionStore`, `ProgressionService`)
  - left as-is. Each catch is already scoped to exactly one file-read/write/JSON-parse call, which is
  a genuine recoverable boundary with no single verifiable exception type (`IOException`,
  `UnauthorizedAccessException`, `ArgumentException`, ... from `File`/`JsonUtility`), so these three
  files are the explicit allowlist entries in both the new regression guard and
  `scripts/validate-repository.ps1`.

Two review passes over this diff - `/code-review high`, then a senior-reviewer pass - caught three
defects in the first version of the `LoadManager`/`StartManager` changes above, all fixed before
landing.

`LoadAllDataCoroutine` originally set `persistenceReady = true` only at its very end, so a genuine
catalog exception now correctly aborted the coroutine early but left `persistenceReady` permanently
`false` - `LoadedData`'s independent fallback would still populate the catalogs, but
`HasAllRequiredData()` also requires `PersistenceReady`, so recovery stalled for up to two full
12-second timeout cycles instead of the same frame. The fix wraps catalog construction in a
`try`/`finally` (no `catch`, since nothing in the block needs one) that always sets
`persistenceReady`/`loadRoutine` while the exception itself still propagates unchanged. The second
pass's review is what caught that this guarantee does not extend to `SeedCharacterTable`/
`SeedCheerleaderTable`: those run via `yield return anotherCoroutine()`, and Unity's coroutine
driver pumps a yielded `IEnumerator` outside this method's own resumed call frame, so an exception
thrown *inside* a nested coroutine does not reliably run the outer method's `finally` - verified
directly against Unity 6000.5.7f1 with a throwaway repro (an outer `try`/`finally` around
`yield return innerCoroutine()`, where the inner throws) before trusting the assumption in
production code. Moot in practice today, since `DBConnector.CreateTableCharacterProfile` and
`DBHelper.InsertCharacterProfile`/`InsertCheerleaderProfile` already catch internally like every
other method in that file - but the seed calls were moved back outside the `try`/`finally` so the
code does not imply a guarantee it cannot keep. Separately,
`StartManager.initializefriendDisplay()`'s guard-and-dedent left a duplicate
`friendSelectOptionText.text = ...` assignment (a leftover of two previously-separate blocks); the
redundant second assignment was removed.

`scripts/validate-repository.ps1` gained a check that fails on any `catch (Exception)` /
`catch (System.Exception)` under `Assets/Scripts/menu_*` (excluding `Legacy~`, `Tests`, `Editor`)
not on that three-file allowlist; `MenuExceptionBoundaryPolicyTests` duplicates the same check as an
EditMode test so it also runs wherever the Unity suite runs. `MenuActionWrapperExceptionTests`
exercises `StartManager.RunCommand` and `ProgressionManager.RunProgressionAction` through reflection
(against a GameObject created inactive, so `Awake()` never runs) to confirm an action's exception
now propagates and `buttonPressed` is still restored by `finally`. `ProgressionPersistenceRecoveryTests`
confirms the retained persistence catches still recover from a corrupted on-disk ledger without
throwing. `LoadManagerFallbackDataTests` exercises `TryLoadFallbackData()` against this project's
real `Resources` catalogs.

Compile + **704/704 EditMode** + **13/13 PlayMode** + `scripts/validate-repository.ps1` all pass
under Unity 6000.5.7f1. No manual interactive Play Mode pass was run in this session (no interactive
GUI available to this agent).
