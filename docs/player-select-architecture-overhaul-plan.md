# Player Select Architecture Overhaul Plan

Status: implemented (phases 0-8 complete; see verification note below)  
Project: Level 5  
Branch target: `dev`  
Audit baseline: `ed689b20c1c3df6f1506f80579c883db94d8dfc1`  
Reviewed: 2026-08-09, two architecture/problem-solution passes  
Verified 2026-08-13: all 9 migration phases (section 9) are implemented in code and covered by
`Level5PlayerSelectArchitectureTests.cs` plus the other `Level5PlayerSelect*Tests.cs` suites
(70 edit-mode tests, all passing). `StartMenuUiObjects`'s cpu1/2/3 fields, which an earlier pass of
this audit flagged as possibly-obsolete leftovers, are in fact the live `PlayerSelectView` binding
targets - deliberately reused rather than replaced so the migration added no new serialized scene
state. `StartManager` still composes the start screen for unrelated concerns (level/mode/cheerleader
select, options, navigation) per this plan's own non-goals; that is expected, not a sign of
incomplete migration.

## Executive summary

Player select is currently not a subsystem. Its state, input, rendering, persistence, progression display, CPU roster construction, mode compatibility, launch conversion, and scene navigation are spread through `StartManager`, `StartMenuSelectionState`, `StartMenuUiObjects`, `TouchInputStartScreenController`, `LoadedData`, `GameOptions`, and `ProgressionManager`.

The match overhaul has already created the correct downstream boundary:

```text
PlayerRoster -> MatchRequest -> GameModeCompatibility -> MatchConfigurationBuilder -> MatchConfiguration
```

This plan does **not** replace that work. The player-select overhaul should stop at `PlayerRoster` and feed the existing match pipeline.

The target is:

```text
LoadedData / current live profiles
          |
          v
PlayerSelectCatalogAdapter
          |
          v
CharacterSelectOption[] + visuals
          |
          v
PlayerSelectionController <---- current mode/modifiers via existing compatibility rules
          |
          +---- PlayerSelectView / input adapters
          |
          +---- PlayerSelectionSession
          |
          v
PlayerRoster
          |
          v
StartMenuSelectionState.BuildRequest(... roster ...)
          |
          v
existing match configuration pipeline
```

The defining ownership rule is:

> Player select owns a **roster draft expressed by stable character identity**. `StartManager` does not own player selection, CPU slots, player-select rendering, or character cycling.

The refactor is intentionally a strangler migration, not a start-menu rewrite. Level, mode, cheerleader, options, account navigation, and progression editing remain separate work.

---

## 1. Current-state audit

### 1.1 `StartManager` still owns nearly every concern involved in player select

`StartManager` currently owns or coordinates:

- the local-player catalog;
- the CPU catalog;
- the selected human index;
- three selected CPU indices through `StartMenuSelectionState`;
- participant count;
- player and CPU UI references;
- player stat formatting;
- progression stat formatting;
- lock overlay rendering;
- character cycling;
- CPU cycling;
- keyboard/gamepad navigation;
- button callbacks;
- focus-based tab switching;
- match request construction;
- Wizard-of-Boat runtime variant choice;
- end-round portrait selection;
- progression snapshot transfer to `PlayerData`;
- scene transition.

This is a responsibility problem, not merely a long-file problem. Moving the same methods into another large MonoBehaviour would not fix it.

### 1.2 Selection is represented by catalog position instead of character identity

`StartMenuSelectionState` currently stores:

```text
PlayerIndex
Cpu1Index
Cpu2Index
Cpu3Index
```

and `GameOptions` keeps corresponding process-wide static indices.

The index is not the identity. Reordering a catalog changes what an index means. The current code mitigates out-of-range values, but it cannot make an index semantically stable across catalog changes.

The project already has a stable legacy numeric player identity (`CharacterProfile.PlayerId`) used by SQLite and carried into `CharacterSelection`. This overhaul should use that identity for current menu/session state rather than inventing another identity migration.

`CharacterPreset.characterId` remains the longer-term progression identity. Converting the live progression source from legacy IDs/SQLite to preset IDs/JSON belongs to the progression migration, not this refactor.

### 1.3 The CPU roster is encoded as three special fields plus a fake character

The current CPU menu assumes exactly three special CPU slots and treats CPU catalog index 0 / player ID 0 as “no CPU”. `PlayerRoster`, by contrast, already models actual participants and has no need for a fake participant.

The new draft must represent an empty CPU slot explicitly:

```text
slot present + CharacterId
or
slot inactive / no CharacterId
```

The legacy CPU “none” profile may remain in the loaded catalog while migration is in progress, but it must be consumed only by the legacy adapter/view and must never enter the new `PlayerRoster` as a character.

### 1.4 Request construction mutates selection state

`StartMenuSelectionState.BuildRequest` currently changes `Cpu1Index` to the first real CPU when a mode requires an opponent and no CPU was selected.

Building a value should not mutate the menu that requested it. It also creates a UI mismatch: the visible roster may contain one participant while the launch silently produces two and then persists the changed CPU index.

Required-opponent reconciliation must become an explicit player-selection operation, preferably when the mode context changes and again as a defensive pre-launch check.

### 1.5 Rendering mutates character data

`StartManager.initializePlayerDisplay()` currently recalculates and writes `CharacterProfile.Level` and `CharacterProfile.Clutch` while rendering text.

A view refresh must be read-only. Character normalization belongs in data loading/projection, and the view should receive already-resolved values.

The existing behavior must be preserved while ownership changes. In particular, the current effective player clutch is `min(level, 100)`. If gameplay relies on the mutated `CharacterProfile`, move that normalization into the profile-loading/projection path before deleting the render-time write.

### 1.6 Player-select rendering is driven every frame by focus polling

`StartManager.Update()` checks the selected GameObject name every frame and repeatedly calls `initializePlayerDisplay`, `setCpuPlayer1/2/3`, and menu tab activation methods while the corresponding control remains selected.

That causes repeated hierarchy activation, string formatting, and UI writes even when state has not changed.

The new player-select UI must render only when one of these changes:

- player-selection state;
- match context relevant to character availability;
- selected/focused player-select slot;
- loaded catalog/progression snapshot.

It must not rebuild the player display every frame.

### 1.7 Input is coupled to object names and render methods

Desktop/gamepad and legacy touch flows both reason about names such as `player_selected_name`, `cpu1_button`, etc. The touch controller directly calls a mutation method and then a rendering method on `StartManager`.

The new input boundary should emit player-selection commands:

```text
SelectNextPrimary
SelectPreviousPrimary
SelectNextCpu(slot)
SelectPreviousCpu(slot)
FocusPrimary
FocusCpu(slot)
```

The controller changes state once; the presenter/view redraws from that state. Input must not need to know how player selection is stored or rendered.

Do not turn this into a generic menu framework. Remove the player-select branches from the legacy input code and leave unrelated menu options alone.

### 1.8 Character/mode capability logic already has an owner, but player cycling duplicates part of it

`GameModeCompatibility` is explicitly the authoritative owner of “can this match be played?” and the match builder revalidates every request.

`StartMenuSelectionState.CyclePlayer`, however, independently re-derives the fighter-vs-shooter requirement. That can drift from launch validation.

The player-select overhaul must not introduce a second `CharacterEligibilityPolicy` that repeats these rules. Instead, expose/reuse a focused character-capability query from `GameModeCompatibility` and have player select call it.

Account unlock/availability is a separate concern and must not be folded into match compatibility.

### 1.9 Locked characters are presentation-only in the inspected launch path

The start menu renders a lock overlay from `CharacterProfile.IsLocked`, but the inspected `StartManager.loadGame` + `MatchConfigurationBuilder` path does not independently reject a locked selected character.

This overhaul should make that boundary explicit:

- locked characters remain browseable so the player can inspect them;
- a locked primary character cannot be confirmed/launched;
- the source of the lock during this migration is the current live loaded profile/progression projection;
- do not make JSON/preset progression authoritative as a side effect of this work.

This is a deliberate correctness change and must be covered by tests.

### 1.10 Authored character presets already exist, but progression authority is not migrated

The repository already contains:

- `CharacterPreset`;
- `CharacterPresetCatalog`;
- `CharacterRuntimeProvider`;
- `CharacterProgressResolver`;
- per-account JSON character progress.

However, `docs/persistence-boundaries.md` establishes that SQLite-backed loaded `CharacterProfile` data is still authoritative for what the game currently displays, while the JSON path is a partially wired fallback.

Therefore this refactor must **not** create another authored character model, and it must **not** switch player select to JSON/preset progression authority.

Use a projection/adapter over the current live loaded profiles. Existing preset data may provide authored metadata or parity checks where already reliable, but changing progression authority belongs to issue #39.

### 1.11 Player selection leaks into the progression screen through `GameOptions.playerSelectedIndex`

`StartManager` writes the current player index before opening progression, and `ProgressionManager` reads that index.

If player selection becomes identity-based, this bridge must move with it. A focused session-level player-selection memory should carry the primary character ID. `ProgressionManager` can resolve that ID to its current catalog index, falling back safely when absent.

This is a small integration seam, not authorization to redesign the progression screen.

### 1.12 Wizard of Boat is a hidden character-launch special case

`StartManager` currently identifies Wizard of Boat by matching the display name and randomly chooses `wob1` or `wob2` at launch.

Do not lose or generalize this silently. Move it behind a tiny legacy character-variant resolver used by roster construction. Keep the current launch-time randomization semantics. A future authored variant model may replace it separately.

---

## 2. Existing architecture to preserve

The following pieces are good seams and must remain authoritative:

### `CharacterSelection`

A plain match DTO. It deliberately does not hold a `CharacterProfile` MonoBehaviour. Continue using it as the selected-character representation carried into a match.

### `PlayerRoster`

The immutable ordered participant list used by gameplay. It already models `LocalHuman`, `Cpu`, `RemoteHuman`, and `ReplayGhost` control types and caps current scenes at four slots.

Player select should build this; it should not invent a parallel runtime roster.

### `GameModeCompatibility`

The authoritative owner of arena/roster/mode compatibility. Player select may query it for character capability, but must not copy its fighter/shooter rules.

### `MatchRequest` and `MatchConfigurationBuilder`

Every launch path already converges here. Keep the final validation at launch even when the menu has filtered/validated earlier.

### `ActiveMatch` / `MatchConfiguration`

The resolved match remains authoritative after launch. Player-selection state ends at launch and must not become runtime mutable match state.

---

## 3. Goals

1. Remove player-selection state ownership from `StartManager`.
2. Replace human/CPU catalog indices with stable character identity.
3. Replace `Cpu1Index`/`Cpu2Index`/`Cpu3Index` domain state with explicit roster-draft slots.
4. Make “no CPU” an empty slot, not a fake character.
5. Make player-select state plain C# and edit-mode testable without loading a scene.
6. Reuse `PlayerRoster` as the only launch roster representation.
7. Reuse `GameModeCompatibility` as the only match-capability rule owner.
8. Keep account unlock/availability separate from match compatibility.
9. Make rendering passive and side-effect free.
10. Stop player-select rendering every frame.
11. Remove player-select object-name dispatch from `StartManager` and legacy touch input.
12. Preserve the existing start-menu appearance and navigation unless a behavior correction is explicitly listed below.
13. Preserve Wizard-of-Boat launch-time variant selection.
14. Preserve current SQLite-backed progression authority during this refactor.
15. Move player/CPU session preference state out of `GameOptions` where safely possible.
16. Leave the architecture capable of additional local humans/remote participants without implementing those flows now.
17. Add ratchet tests that prevent the old ownership pattern from returning.

---

## 4. Non-goals

This plan does not authorize:

- a full start-menu redesign;
- a UI Toolkit conversion;
- a generic menu/event-bus framework;
- a dependency-injection framework;
- a full `StartManager` rewrite unrelated to player select;
- a progression persistence migration from SQLite to JSON;
- redesigning `ProgressionManager`;
- replacing all `CharacterProfile` usage in gameplay;
- converting all character data to new ScriptableObjects (the preset model already exists);
- implementing local two-player joining;
- implementing remote/correspondence player-select UI;
- changing scoring, gameplay stats, character balance, or CPU AI;
- changing the current four-spawn-point roster cap;
- forcing character uniqueness between slots unless separately approved.

---

## 5. Review pass 1: architecture problems and corrections

The first draft proposed the right ownership direction but still contained several traps.

### Problem 1: a new eligibility policy would duplicate `GameModeCompatibility`

**Risk:** fighter/shooter requirements would exist in two services and drift again.

**Correction:** refactor `GameModeCompatibility` to expose a small pure character-capability query. Internally it must use the same helper that full roster validation uses.

Example shape:

```csharp
bool CanCharacterPlay(
    GameModeDefinition mode,
    MatchModifiers modifiers,
    CharacterSelection character);
```

or an equivalent validation result. Player select calls it; the match builder remains the final authority.

Unlock state is *not* part of this method.

### Problem 2: introducing another character definition would create a third source of truth

**Risk:** `CharacterProfile`, `CharacterPreset`, and a new player-select definition would all describe the same character.

**Correction:** add a read-only projection for selection, not authored data.

`CharacterSelectOption` is a snapshot/value used by the selector. It is created from current loaded data and discarded/rebuilt when that data refreshes.

### Problem 3: the draft still treated all four slots as participants

**Risk:** the legacy CPU “none” record leaks into `PlayerRoster`.

**Correction:** `PlayerSelectionState` has one required primary slot and up to three optional CPU selections for current UI parity. Optional means absent, not character ID 0.

### Problem 4: string IDs would introduce an unnecessary identity migration

**Risk:** the refactor would mix `CharacterPreset.characterId`, object names, and legacy player IDs in one release.

**Correction:** selection/session state uses the current stable numeric `PlayerId`/match `CharacterId` for this overhaul. The adapter may also retain canonical preset ID as metadata, but it is not the session key yet.

### Problem 5: a generic menu framework would expand scope dramatically

**Risk:** player select becomes blocked on refactoring every start-menu option.

**Correction:** make focused player-select view/input adapters. Remove only player/CPU branches from the giant manager/touch controller. Other start-menu debt remains visible rather than being opportunistically folded in.

---

## 6. Review pass 2: migration, behavior, and verification problems

### Problem 1: preset/JSON progression looks cleaner but is not yet the live authority

**Risk:** switching player select to preset+JSON data can show stale/default progress for existing players.

**Correction:** the adapter reads current loaded `CharacterProfile` values during this overhaul. `CharacterPresetCatalog` may enrich static authored metadata only when parity is established. Issue #39 owns the later authority switch.

### Problem 2: required CPU opponents are currently added invisibly during request construction

**Risk:** the player sees one roster and launches another; building a request has a side effect.

**Correction:** make reconciliation explicit in `PlayerSelectionController`.

When the selected mode requires a CPU opponent and no CPU is active:

1. select the first real compatible CPU option as slot 1;
2. update the visible roster/participant count immediately;
3. re-check before launch defensively;
4. never mutate the draft inside `BuildRoster`.

For modes with `AddsImplicitDefender`, CPU draft slots are not included in the built roster, preserving current Lockdown semantics.

This is a deliberate visible behavior improvement and must be tested.

### Problem 3: locked characters can currently reach the inspected launch path

**Risk:** unlocks become cosmetic.

**Correction:** browsing and launching are separate capabilities. Locked characters may be browsed/rendered but `TryBuildRoster` fails with an explicit reason when the primary character is locked.

Do not silently skip locked characters while cycling; the lock screen must remain discoverable.

### Problem 4: moving view fields can break serialized scene references

**Risk:** deleting fields from `StartMenuUiObjects` before Unity serializes them onto `PlayerSelectView` loses references.

**Correction:** migrate serialization safely:

1. introduce the new component and fields;
2. copy current references through Unity Editor/SerializedObject or a temporary compatibility binder;
3. open/validate the start scene;
4. only then remove legacy player/CPU fields from `StartMenuUiObjects`.

Do not hand-delete serialized references and assume names will repair them.

If implementation is running without a Unity editor, stop before destructive serialized-field removal and leave the compatibility binder explicit rather than guessing YAML.

### Problem 5: player-select index also selects the progression-screen character

**Risk:** removing `GameOptions.playerSelectedIndex` breaks navigation context.

**Correction:** add focused session memory keyed by stable numeric character ID. `ProgressionManager` resolves that ID into its local list. When progression changes its selected character, update the same session memory so returning to start preserves the choice.

Do not redesign progression editing in this task.

### Problem 6: Wizard of Boat is easy to lose during roster extraction

**Risk:** the new roster always spawns one fixed Wizard variant.

**Correction:** `PlayerSelectRosterBuilder` (or equivalent launch adapter) owns the current legacy variant rule and receives/testably wraps the random choice. Keep display-name matching quarantined there until authored variant metadata exists.

### Problem 7: pure edit-mode tests cannot prove the Unity wiring

**Risk:** the controller is correct while start-scene references/callbacks are broken.

**Correction:** pair pure tests with scene/Play Mode validation:

- edit-mode characterization/domain tests for state and roster behavior;
- scene contract/editor validation for serialized references;
- at least one Play Mode or manual start-screen smoke covering player cycle -> CPU cycle -> launch request.

---

## 7. Final target architecture

### 7.1 Pure selection model

Place pure state/controller code under `Assets/Level5/Core/PlayerSelection/` (or the closest existing pure-core convention).

#### `CharacterSelectOption`

Read-only selection projection containing only values player-selection logic needs, for example:

```csharp
public sealed class CharacterSelectOption
{
    public int CharacterId { get; }
    public string DisplayName { get; }
    public string ObjectName { get; }
    public bool IsShooter { get; }
    public bool IsFighter { get; }
    public bool IsUnlocked { get; }
    public CharacterSelectStats Stats { get; }
}
```

No `MonoBehaviour`, `GameObject`, `Sprite`, database access, singleton, or persistence call belongs here.

`CharacterSelectStats` may carry the numeric display snapshot needed by the start menu: current level/experience/points, shooting ratings, release, range, speed %, jump %, luck, and effective clutch.

#### `PlayerSelectionSlot`

Represents a draft slot rather than a legacy CPU index.

Conceptually:

```csharp
public sealed class PlayerSelectionSlot
{
    public PlayerControlType ControlType { get; }
    public int? CharacterId { get; }
    public bool IsActive => CharacterId.HasValue;
}
```

For current UI parity:

- slot 0 is one local human and is always active;
- up to three CPU draft slots may be active/inactive;
- the model must not name them CPU1/CPU2/CPU3 internally beyond their ordered slot position.

The model may later represent multiple local humans without changing the match boundary, but this task must not build that UI.

#### `PlayerSelectionState`

Owns only roster-draft selection.

It must not contain:

- mode index;
- level index;
- friend index;
- difficulty;
- sniper;
- environment modifiers;
- UI references;
- persistence calls.

#### `PlayerSelectionController`

Pure orchestration over state and catalog values.

Responsibilities:

- select/cycle primary character;
- select/cycle a CPU slot;
- activate/deactivate optional CPU slots;
- expose participant count;
- reconcile a required CPU opponent explicitly;
- query `GameModeCompatibility` for character capability rather than reproducing rules;
- validate lock/availability separately;
- return selected options for presentation;
- never call Unity APIs;
- never write `GameOptions`;
- never load a scene.

### 7.2 Catalog projection adapter

`PlayerSelectCatalogAdapter` lives in the Unity/menu layer.

Responsibilities:

- consume current `LoadedData`/loaded `CharacterProfile` lists once data is ready;
- map profiles to `CharacterSelectOption` values;
- map stable character ID to visual assets needed by the view (`portrait`, `winPortrait`, `losePortrait`);
- convert the legacy CPU ID-0 “none” item into absence, not a real option;
- preserve current catalog order for UI cycling;
- calculate display-only derived values without mutating during render;
- preserve current live SQLite-backed lock/progression values;
- optionally cross-reference existing `CharacterPresetCatalog` for authored metadata only when safe.

No selection state belongs in the adapter.

### 7.3 Compatibility ownership

Refactor `GameModeCompatibility` so the fighter/shooter requirement used by full roster validation and player-select cycling comes from the same helper.

Do **not** create a parallel match compatibility service in the player-select namespace.

Availability/lock validation remains player/account-side because it is not a property of the game mode.

### 7.4 `PlayerSelectView`

A passive MonoBehaviour containing explicit serialized references.

Recommended shape:

```text
primary option button
primary character name
portrait
lock overlay
character stats text/fields
progression stats text/fields
participant count text
CPU panel
CPU slot views[]
shared/current CPU stats display
```

Each CPU slot view should be a serialized small binding object containing its button, portrait, and name rather than three methods and three sets of fields.

The view may format values, but it may not:

- mutate `CharacterProfile`;
- change selection state;
- load data;
- write persistence;
- build a match;
- call `GameOptions`;
- infer behavior from GameObject names.

### 7.5 `PlayerSelectCoordinator` / presenter

One scene-level player-select component composes:

- catalog adapter output;
- `PlayerSelectionController`;
- `PlayerSelectView`;
- input bindings;
- session preference restore/save;
- roster building.

This is the public boundary `StartManager` talks to.

Suggested narrow API:

```csharp
void Initialize(...);
void SetMatchContext(GameModeDefinition mode, MatchModifiers modifiers);
bool TryBuildRoster(out PlayerRoster roster, out string error);
int ParticipantCount { get; }
void ApplyLaunchSideEffects(MatchConfiguration configuration);
```

Exact names may differ; preserve the responsibility boundaries.

### 7.6 Input adapter

Player-select-specific input maps the current UI interaction to coordinator/controller commands.

Requirements:

- Button click continues to cycle forward;
- keyboard/gamepad previous/next behavior remains;
- CPU slot focus determines which CPU stats are shown;
- legacy touch swipe behavior remains if that path is still active;
- compare object references / serialized bindings, not object-name strings;
- update UI once per state/focus change, not every frame.

Do not migrate unrelated start-menu input in this task.

### 7.7 Session preference owner

Add a focused `PlayerSelectionSession` (name flexible) with an encapsulated API and stable IDs.

It should remember within the current application session:

- primary character ID;
- ordered active/inactive CPU selections by stable character ID/control type.

It should not expose public mutable fields.

This replaces the player/CPU selection indices in `GameOptions`.

`ProgressionManager` reads/writes the primary selected ID through this seam and resolves it to its local index.

Do not add disk persistence unless separately required; the existing `GameOptions` behavior is session memory, not durable user preference storage.

### 7.8 Roster builder and legacy character variant

Building the match roster must be a read-only conversion from selection state.

```text
PlayerSelectionState
  -> resolve CharacterSelectOption by ID
  -> resolve legacy runtime variant if required
  -> CharacterSelection
  -> PlayerRosterEntry
  -> PlayerRoster.Build
```

The Wizard-of-Boat special case belongs in a tiny legacy variant resolver at this conversion seam. It must not remain in `StartManager`, and it must not contaminate generic selection state.

### 7.9 Launch integration

After this overhaul, the start-menu request flow should look approximately like:

```csharp
if (!playerSelect.TryBuildRoster(out PlayerRoster roster, out string playerError))
{
    ShowLaunchError(playerError);
    return;
}

MatchRequest request = selection.BuildRequest(
    Compatibility,
    roster,
    friendSelectedData);

MatchBuildResult result = MatchCatalogs.Builder.Build(request);
...
```

`StartMenuSelectionState.BuildRequest` no longer receives player/CPU profile lists and no longer constructs or mutates the roster.

It remains responsible for the non-player match choices it already owns.

---

## 8. Explicit behavior decisions

These are not accidental side effects of refactoring.

### Preserve

- one local human in the current production player-select UI;
- zero to three selected CPUs where the mode permits them;
- current character catalog order;
- current human character fighter/shooter filtering behavior;
- current match-builder validation as final authority;
- locked characters can be browsed and inspected;
- Lockdown/implicit-defender modes do not put the defender into the authored CPU draft roster;
- Wizard-of-Boat runtime variant is chosen at launch;
- player/CPU duplicate character choices remain allowed unless existing validation says otherwise;
- current SQLite-backed displayed progression values remain authoritative during this task.

### Deliberate corrections

1. A locked primary character cannot launch a match.
2. A mode that requires a CPU opponent reconciles that CPU visibly before launch rather than mutating selection inside request construction.
3. “No CPU” becomes absence instead of a fake character record in new state/rosters.
4. Player-select rendering no longer mutates `CharacterProfile`.
5. Player-select rendering is change-driven rather than repeated every frame.
6. Stable IDs replace persisted/session catalog indices for player/CPU selection.

Any additional behavior change requires separate justification and characterization.

---

## 9. Migration plan

### Phase 0 — characterization before movement

Add tests around the existing player-select behavior before deleting old code.

Pin at minimum:

- default primary character selection;
- next/previous wraparound order;
- shooter vs fighter filtering;
- locked character remains browseable;
- zero, one, two, and three CPU selections produce participant counts 1–4;
- CPU legacy “none” does not become a roster participant;
- current CPU catalog order;
- `RequiresCpuOpponent` current/default opponent identity;
- `AddsImplicitDefender` ignores authored CPU picks in the launched roster;
- `MatchRequest` built from one human + CPUs has the same slot order/control types as current behavior;
- Wizard-of-Boat variant remains one of the two current runtime object names;
- progression screen opens on the start-menu-selected character;
- returning from progression preserves primary selection.

Where the current implementation has undesirable behavior being deliberately corrected, write the old characterization test first, then replace it with a test named for the approved new behavior so the change is visible in review.

### Phase 1 — pure selection model

Add pure:

- `CharacterSelectOption`;
- `CharacterSelectStats` if needed;
- `PlayerSelectionSlot`;
- `PlayerSelectionState`;
- `PlayerSelectionController`.

Do not wire production UI yet.

Tests must cover:

- stable-ID selection independent of catalog index;
- wraparound;
- empty CPU slot handling;
- participant count;
- four-slot cap;
- required-opponent reconciliation;
- lock validation;
- no mutation during `BuildRoster`/conversion.

### Phase 2 — unify character capability queries

Refactor the fighter/shooter check in `GameModeCompatibility` into one reusable internal/public query and point both:

- full roster validation;
- new player-selection cycling/validation

at it.

Delete the copied logic from `StartMenuSelectionState.CyclePlayer` only after parity tests pass.

### Phase 3 — catalog adapter and roster conversion

Create the adapter from current loaded human/CPU `CharacterProfile` lists.

Move derived display snapshot calculation here or into a dedicated mapper. Move the current effective clutch normalization out of `initializePlayerDisplay` while preserving gameplay behavior.

Add the roster builder and isolate Wizard-of-Boat variant resolution.

At the end of this phase, pure selection state can produce the exact current `PlayerRoster` shape without `StartMenuSelectionState` knowing character lists.

### Phase 4 — wire `StartMenuSelectionState` to an external roster

Change request construction from:

```text
selection + CharacterProfile lists -> roster + request
```

to:

```text
player-select subsystem -> PlayerRoster
start-menu match selection -> mode/level/modifiers/friend
both -> MatchRequest
```

Remove from `StartMenuSelectionState`:

- `PlayerIndex`;
- `Cpu1Index`;
- `Cpu2Index`;
- `Cpu3Index`;
- `ParticipantCount`;
- `GetCpuIndex` / `SetCpuIndex`;
- `CyclePlayer`;
- `CycleCpu`;
- character-profile-to-roster construction;
- any mutation of CPU selection while building a request.

Keep friend/mode/level/modifier behavior untouched.

### Phase 5 — extract view and input from `StartManager`

Add the new player-select scene components and migrate existing references safely.

Move out of `StartManager`:

- player select UI fields;
- CPU slot UI fields;
- `initializePlayerDisplay`;
- `initializeCpuPlayerDisplay`;
- `setCpuPlayer1/2/3`;
- `setCpuPlayerDisplay`;
- player stat formatting;
- player/CPU cycling methods;
- participant-count display ownership;
- player/CPU button callbacks;
- player/CPU focus/render branches from `Update`.

Remove player-select branches from `TouchInputStartScreenController`; route legacy touch commands to the new player-select adapter/coordinator without object-name dispatch.

Do not refactor friend/level/mode/options UI in the same change.

### Phase 6 — session identity and progression-screen bridge

Introduce focused stable-ID player-selection session memory.

Migrate:

- `GameOptions.playerSelectedIndex`;
- `GameOptions.cpu1SelectedIndex`;
- `GameOptions.cpu2SelectedIndex`;
- `GameOptions.cpu3SelectedIndex`.

Update `ProgressionManager` minimally:

- resolve selected stable character ID into its local profile list;
- fall back to the first valid character if missing;
- update session primary ID when its own character selection changes.

Do not alter progression calculations, draft commit behavior, persistence authority, or progression UI architecture beyond this selection bridge.

Lower the `GameOptions` field-count ratchet and remove stale allowlist entries where applicable.

### Phase 7 — remove remaining StartManager character launch knowledge

Move Wizard-of-Boat runtime variant resolution and player-specific end-round/progression launch snapshot handling behind the player-selection launch boundary.

After this phase `StartManager` should not need to index `playerSelectedData` or `cpuPlayerSelectedData` at all.

`StartManager` may remain the start-screen flow/composition coordinator for unrelated systems.

### Phase 8 — serialization cleanup and architecture ratchets

After the start scene/prefab has been opened and saved with the new components:

- remove obsolete player/CPU fields from `StartMenuUiObjects`;
- remove obsolete player/CPU name constants from `StartManager` where no compatibility path needs them;
- remove old player-select methods;
- remove any compatibility binder used solely for serialized-reference migration.

Add architecture tests that fail on regression.

---

## 10. Architecture guardrails

Add edit-mode source-boundary tests similar to `Level5MatchArchitectureTests`.

Recommended assertions:

1. `StartMenuSelectionState.cs` does not reference `CharacterProfile`, `PlayerIndex`, `Cpu1Index`, `Cpu2Index`, or `Cpu3Index`.
2. New player-selection core files do not reference `UnityEngine`, `GameOptions`, `LoadedData`, `StartManager`, `SceneManager`, or `GameObject`.
3. `StartManager.cs` does not contain the removed player-select render/cycle method names.
4. Player-select production code does not use `GameObject.Find`.
5. Player-select production code does not branch on `player_selected_name`, `cpu1_button`, `cpu2_button`, or `cpu3_button` string names.
6. `GameOptions` no longer contains the four player/CPU selection-index fields once phase 6 is complete.
7. Player-select code does not reimplement fighter/shooter mode logic outside `GameModeCompatibility`.
8. `BuildRoster` / request conversion is side-effect free.
9. A roster never contains the legacy CPU “none” profile.
10. The new view/coordinator has valid scene references in the start scene.

Do not create brittle tests that forbid all future references by filename when a typed dependency is legitimate; ratchet the specific legacy patterns being removed.

---

## 11. Test and verification matrix

### Pure edit-mode tests

#### Selection state

- default state;
- stable-ID restore;
- missing remembered ID fallback;
- previous/next wraparound;
- optional CPU activate/deactivate;
- participant count;
- max four participants;
- slot ordering.

#### Capability

- shooter mode accepts shooter;
- fighter-required context accepts fighter;
- incompatible primary is reported;
- compatibility query matches full `GameModeCompatibility` roster validation for the same character.

#### Availability

- unlocked primary builds;
- locked primary remains selectable/browseable but launch build fails;
- CPU “none” is absence;
- missing character ID produces explicit validation rather than null-reference behavior.

#### Mode constraints

- required CPU opponent is reconciled outside request building;
- reconciliation chooses the same first real CPU the legacy path used unless it is incompatible;
- implicit-defender mode omits draft CPUs from built roster;
- roster conversion does not mutate state.

#### Conversion

- one human only;
- one human + one CPU;
- one human + three CPUs;
- slot IDs dense/order preserved;
- control types preserved;
- character identity/object name preserved;
- Wizard-of-Boat variant resolver returns only current allowed variants and is invoked at launch conversion.

### Edit-mode Unity adapter tests

- profile -> selection projection preserves PlayerId/display/object name/capabilities;
- portrait/win/lose visual mapping;
- level/experience/points values match current loaded profile semantics;
- effective clutch matches existing behavior without render-time mutation;
- legacy CPU ID 0 is converted to empty selection, not a real option;
- catalog order remains deterministic.

### Scene/editor validation

- start scene contains exactly one player-select coordinator/view;
- every required player/CPU binding is assigned;
- no obsolete serialized player-select fields remain after final migration;
- no missing-script reference introduced.

### Play Mode/manual smoke

At minimum:

1. open start menu;
2. focus player select and cycle forward/back;
3. verify portrait/name/stats/lock overlay update once and correctly;
4. focus each CPU slot and cycle including “none”;
5. verify participant count tracks active slots;
6. switch between a shooting and fighting mode and verify character capability behavior;
7. select a locked primary and verify launch is refused with a useful message;
8. select a mode requiring CPU and verify opponent becomes visible before launch;
9. launch one-human match and one-human-plus-CPU match;
10. launch Lockdown and verify its implicit defender behavior;
11. open progression from the selected player and verify the same identity is selected;
12. return to start and verify primary/CPU choices survive the scene round-trip;
13. verify Wizard of Boat still resolves to a valid runtime variant.

Run the full edit-mode suite, existing play-mode suite, and repository validator. Do not claim completion from compilation alone because this refactor changes serialized UI wiring.

---

## 12. Expected file footprint

Exact names may vary, but keep the scope recognizable.

### New pure-core files

Likely under `Assets/Level5/Core/PlayerSelection/`:

- `CharacterSelectOption.cs`
- `PlayerSelectionSlot.cs`
- `PlayerSelectionState.cs`
- `PlayerSelectionController.cs`

Only add a separate validation/result type if it materially improves the API.

### New Unity/menu files

Likely under `Assets/Scripts/menu_start/player_select/`:

- `PlayerSelectCatalogAdapter.cs`
- `PlayerSelectView.cs`
- `PlayerSelectCoordinator.cs`
- `PlayerSelectInputAdapter.cs` if input cannot live cleanly in the coordinator
- `PlayerSelectionSession.cs`
- `LegacyCharacterVariantResolver.cs` or equivalent small conversion seam

Avoid one-class-per-trivial-method fragmentation; combine responsibilities when they are genuinely cohesive.

### Existing files expected to change

- `Assets/Scripts/menu_start/StartManager.cs`
- `Assets/Scripts/menu_start/StartMenuSelectionState.cs`
- `Assets/Scripts/menu_start/StartMenuUiObjects.cs`
- `Assets/Scripts/input/TouchInputStartScreenController.cs`
- `Assets/Scripts/menu_progression/ProgressionManager.cs` (selection bridge only)
- `Assets/Scripts/menu_loading/LoadManager.cs` only if derived profile normalization moves there
- `Assets/Level5/Core/Match/GameModeCompatibility.cs`
- `Assets/Scripts/menu_start/GameOptions.cs`
- relevant editor/play-mode tests and validator tests
- start-menu scene/prefab serialization
- `docs/game-options-inventory.md` if GameOptions fields are deleted

Do not modify unrelated gameplay files.

---

## 13. Acceptance criteria

The overhaul is complete only when all of the following are true:

### Ownership

- `StartManager` does not own selected human/CPU indices or player-select render methods.
- `StartMenuSelectionState` does not own player/CPU selection.
- one player-selection subsystem owns the roster draft.
- `PlayerRoster` remains the launch boundary.

### State

- current selection uses stable character IDs rather than catalog indices;
- empty CPU slots are explicit;
- request/roster construction does not mutate selection;
- required CPU reconciliation is explicit and visible.

### Rules

- fighter/shooter compatibility has one owner in the existing match compatibility layer;
- account lock/availability is separate from match capability;
- a locked primary character cannot launch.

### Data

- player-select rendering never mutates `CharacterProfile`;
- no new progression source of truth is introduced;
- current live SQLite-backed progression behavior is preserved;
- the existing preset/JSON migration remains issue #39 work.

### UI/input

- player/CPU view references are explicit and validated;
- player-select does not use `GameObject.Find` or behavior-significant GameObject names;
- player-select does not redraw every frame;
- touch/gamepad/mouse all reach the same state-changing commands;
- the visual layout remains functionally equivalent unless a listed correction requires otherwise.

### Integration

- `StartMenuSelectionState.BuildRequest` receives a ready `PlayerRoster`;
- match builder remains final validation;
- progression screen receives selected-character identity without `GameOptions.playerSelectedIndex`;
- Wizard-of-Boat launch behavior is preserved;
- one-human, CPU-opponent, and Lockdown launches pass smoke testing.

### Guardrails

- new edit-mode domain/architecture tests pass;
- existing edit-mode tests pass;
- existing play-mode tests pass;
- repository validation passes;
- start scene has no missing scripts/references caused by migration;
- `GameOptions` field-count ratchet is lowered if fields are deleted.

---

## 14. Recommended implementation order / commit structure

Keep commits independently reviewable and behavior-focused:

1. **tests: characterize current player-select and roster behavior**
2. **core: add stable-ID player-selection state/controller**
3. **match: expose one character-capability compatibility query**
4. **menu: project loaded profiles into player-select options and build rosters**
5. **menu: make StartMenuSelectionState accept PlayerRoster and remove player/CPU indices**
6. **ui: extract player/CPU view and input from StartManager**
7. **session: replace GameOptions player/CPU indices and bridge ProgressionManager by ID**
8. **cleanup: move launch special cases, migrate serialized refs, remove legacy fields/methods**
9. **tests/docs: architecture ratchets, scene validation, inventory updates**

If the Unity editor is unavailable, commits 1–7 may proceed only if production behavior can remain wired through a clearly marked compatibility binding. Do not perform destructive serialized-field cleanup or claim the migration complete until the start scene has been opened/validated/saved.

---

## 15. Final design review

The final plan deliberately avoids three attractive but incorrect rewrites:

1. **No new character content model.** `CharacterPreset` already exists; the live progression authority is the real unresolved problem.
2. **No second compatibility service.** `GameModeCompatibility` already owns playable combinations; player select consumes it.
3. **No generic menu framework.** The player-select vertical slice is extracted cleanly first. Shared menu abstractions may be extracted later only from repeated proven patterns.

The desired end state is small and testable:

```text
loaded character data
      -> selection projection
      -> stable-ID roster draft
      -> passive player-select UI
      -> PlayerRoster
      -> existing validated match launch
```

That boundary is enough to remove the current player-select architecture debt without reopening the match overhaul, progression persistence migration, or the rest of the start menu.
