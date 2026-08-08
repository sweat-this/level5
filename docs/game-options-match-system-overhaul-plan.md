# Game Options and Match System Overhaul Plan

Status: Phases 0-9 implemented; phases 10-11 (deleting legacy fields) outstanding  
Project: Level 5  
Branch target: `dev`  
Primary scope: `GameOptions`, `StartManager`, `StartScreenModeSelected`, `Modes`, `GameLevelManager`, `GameRules`, mode/level compatibility, roster configuration, and gameplay scene bootstrap  
Last reviewed: 2026-08-08

## Implementation status

| Phase | State | Notes |
| --- | --- | --- |
| 0 - baseline and freeze | done | [`docs/game-options-inventory.md`](game-options-inventory.md); characterization matrix exported by `Level 5 > Match > Export Mode Characterization Matrix` |
| 1 - typed identity and catalogs | done | `GameModeId`, `GameModeCatalog`, `LevelDefinitionCatalog`; `Modes` unchanged and asserted equal |
| 2 - authored `GameModeDefinition` | done | ScriptableObject + `GameModeDefinitionFactory` seam + parity validator over the shipping prefab |
| 3 - capabilities and compatibility | done | `ArenaCapability`, `GameModeCompatibility`; the recursive menu cycling is gone |
| 4 - request and immutable configuration | done | `MatchRequest`, `MatchConfigurationBuilder`, `ResolvedMatchRules`, `MatchConfiguration` |
| 5 - legacy bridge | done | `LegacyGameOptionsBridge`, one-way, with parity tests |
| 6 - player roster | done | `PlayerRoster` / `PlayerSlot` / `PlayerControlType` |
| 7 - `LevelRuntimeContext` | done | plus `MatchRuntime` for scenes entered without a launch |
| 8 - decompose `GameLevelManager` | done | `PlayerRegistry`, `SpawnCoordinator`, `ArenaBootstrap`; the manager is a facade |
| 9 - refocus `GameRules` | done | `MatchController` owns the lifecycle, `MatchHudPresenter` owns presentation, `MatchEndConditions` owns the "is it over?" rules, and `GameRules` reads the configuration rather than copying it |
| 10 - remove legacy match fields | in progress | 36 gameplay scripts moved onto `MatchRuntime` and off the allowlist; 20 fields deleted (85 → 65). Four gameplay files remain, all on the boundary itself |
| 11 - split remaining global state | not started | account, navigation and application metadata are separate work |

Three behaviour changes were made deliberately rather than preserved:

- an invalid combination refuses to launch with a stated reason instead of loading a scene (§6.3);
- a mode needing seven point markers now requires an arena with a seven point line (§6.1). Every
  authored arena currently has one, so this excludes nothing today - see the characterization
  matrix;
- **a scene with an incomplete HUD now still finishes and saves its match** (§11.3). `GameRules`
  used one flag both to switch off the score display and to gate the durable end-of-match work,
  while logging "Match results are still saved." They were not: a renamed HUD object meant the
  match never ended, no score was written and no experience was applied. Presentation moved to
  `MatchHudPresenter` and cannot gate any of that.

One oddity was preserved rather than fixed, and is marked in the code: the basketball goal is hidden
for a battle royal only in a scene entered without a launch, because the old condition also required
that no mode had been selected.

Two of the plan's proposed rule dimensions had to be flag sets rather than single values, because
the parity validator found the shipping data using them that way and gameplay reading each flag
separately:

- `ShotRule` - the authored all point contest is also marked a three point and a four point contest;
- `CombatMode` - the authored Cage Match is also marked a battle royal, which is why a cage match
  needs a battle-royal arena today.

A single value would have dropped those flags silently. Sections 5.2 and 4.2 assumed exclusivity
that the data does not have.

## Executive Summary

Level 5 currently represents a match through a broad collection of static mutable fields in `GameOptions`, serialized mode booleans in `StartScreenModeSelected`, menu-owned compatibility checks in `StartManager`, and copied runtime flags inside `GameRules` and `GameLevelManager`. The system works, but the ownership model no longer scales safely.

The central architectural problem is not any one bad flag or one oversized manager. It is that authored configuration, menu selection state, account state, runtime match state, level capability data, player roster state, and gameplay rules all share overlapping mutable representations. The same fact can exist in multiple places at once, and several systems can change it after a match has begun.

This overhaul replaces that structure incrementally with:

- typed game-mode identity;
- ScriptableObject-backed authored mode and level definitions;
- a validated `MatchRequest` produced by menu/UI flows;
- an immutable `MatchConfiguration` used as the authoritative description of the match;
- explicit rule dimensions instead of mutually exclusive boolean identity flags;
- arena capabilities and a single compatibility validator;
- an explicit `PlayerRoster` model that can represent local human, CPU, remote, and asynchronous participants;
- a scene-owned `LevelRuntimeContext` as the gameplay composition root;
- a focused `MatchController` for match lifecycle;
- specialized systems for clock, scoring, win conditions, spawning, and presentation;
- a temporary one-way compatibility bridge into `GameOptions` while legacy consumers are migrated.

This is intentionally a strangler migration, not a rewrite. The old system remains operational while the new system becomes authoritative one vertical slice at a time. Existing numeric mode IDs, scene names, serialized prefab data, save compatibility, and current gameplay behavior are preserved unless a specific behavior change is separately approved.

---

## 1. Why the Current System Needs an Overhaul

### 1.1 `GameOptions` has exceeded the responsibility of an options object

`GameOptions` currently contains several unrelated categories of state:

- application/platform information;
- current account and local user information;
- bearer/session-adjacent values;
- selected character and cheerleader data;
- selected level data;
- selected game mode identity;
- per-mode rule booleans;
- per-level capability booleans;
- combat/environment toggles;
- difficulty state;
- player roster and CPU ownership;
- menu selection indices;
- scene-navigation state;
- match result/session data;
- campaign-adjacent data.

Because these values are public static mutable fields, every consumer can both read and mutate process-wide state. Dependencies are implicit, scene-order assumptions are difficult to detect, tests require global reset discipline, and invalid combinations can exist without any type or validation barrier.

### 1.2 Mode identity is duplicated as both an ID and boolean flags

The project already has stable numeric mode identity in `Modes.cs`, but several modes are also represented by independent booleans such as Battle Royal, Cage Match, contest variants, survival requirements, enemies-only behavior, basketball requirements, and CPU-shooter rules.

A mode can therefore be represented simultaneously by:

- `gameModeSelectedId`;
- `battleRoyalEnabled`;
- `cageMatchEnabled`;
- `gameModeThreePointContest`;
- `gameModeFourPointContest`;
- `gameModeSevenPointContest`;
- `gameModeAllPointContest`;
- `EnemiesOnlyEnabled`;
- `gameModeRequiresPlayerSurvive`;
- `gameModeRequiresBasketball`;
- other supporting booleans.

The system does not make contradictory combinations structurally impossible.

### 1.3 The same configuration is copied through multiple layers

The current flow effectively duplicates state through this chain:

`StartScreenModeSelected` -> `StartManager` -> `GameOptions` -> `GameRules` / `GameLevelManager` / gameplay consumers.

The menu copies serialized mode properties into static globals. `GameRules` then copies many globals into private runtime fields. Other consumers continue to read directly from the globals.

This creates configuration shadow state: after initialization, there may be multiple representations of the same rule, and it is not obvious which one is authoritative.

### 1.4 Runtime systems mutate configuration

`GameLevelManager` currently performs mode-driven corrections such as enabling enemies when basketball is not required. This means the runtime can modify the configuration after the menu has selected it.

A resolved match should instead be validated before gameplay begins and then treated as immutable input to the runtime.

### 1.5 Level/mode compatibility belongs to domain logic, not recursive menu navigation

`StartManager` currently recursively changes selected levels to skip incompatible combinations based on shooting, fighting, Battle Royal, Cage Match, and level flags.

This has several risks:

- compatibility policy is embedded in presentation/navigation code;
- recursive cycling can become hard to reason about as combinations increase;
- future multiplayer and asynchronous variants will multiply conditions;
- different menus could accidentally implement different compatibility rules;
- validation is difficult to test independently.

Compatibility should have one explicit owner.

### 1.6 `GameLevelManager` and `GameRules` are becoming broad coordinators

`GameLevelManager` currently participates in player spawning, basketball spawning, CPU/human ownership, scene searches, spawn-point validation, character/vehicle/NPC replacement, analytics, terrain exceptions, runtime registries, input, mode handling, and score ordering.

`GameRules` currently participates in clocks, counters, markers, contest variants, money ball, consecutive shots, HUD rendering, score presentation, persistence, progression, campaign handling, death, match termination, and obstacle setup.

Both classes contain useful behavior, but their responsibilities should be separated gradually before additional game modes make them harder to change safely.

---

## 2. Goals

The overhaul must produce a system where one authoritative object describes the selected match, invalid combinations are rejected before scene load, gameplay systems depend on typed concepts rather than global booleans, and new modes can be added without modifying a long chain of unrelated managers.

Specific goals:

1. Give each category of state one owner.
2. Preserve current gameplay behavior during migration.
3. Preserve existing numeric mode IDs and any persistence/backend contracts that depend on them.
4. Make authored mode and level data inspectable and editable in Unity without duplicating runtime state.
5. Make match configuration immutable after validation.
6. Make level/mode compatibility testable without loading UI scenes.
7. Model participants in a way that supports local human, CPU, remote human, replay/ghost, and asynchronous challenge use cases.
8. Reduce direct reads of `GameOptions` and broad manager singletons in new code.
9. Separate match lifecycle from specialized game rules.
10. Permit one mode to compose legitimate independent rule dimensions without boolean identity explosions.
11. Create migration checkpoints where the project remains playable and reversible.
12. Add architecture validation so new global mode flags do not creep back into the system.

---

## 3. Non-Goals

This plan does not authorize a full rewrite of gameplay. It does not redesign scoring behavior, player physics, AI, combat feel, persistence semantics, UI appearance, scene layout, or backend contracts unless required to preserve compatibility with the new configuration boundary.

It also does not require a third-party dependency-injection framework, an ECS conversion, addressables migration, scene-system rewrite, or a general event-bus architecture.

The plan should remain understandable with ordinary C# classes, Unity MonoBehaviours, ScriptableObjects, explicit serialized references, and small interfaces only where a real system boundary benefits from one.

---

## 4. Architecture Principles for This Overhaul

### 4.1 Authored data, user selection, resolved configuration, and runtime state are different things

They must not share one mutable object.

Authored data is what designers define in Unity. User selection is what a menu or service requests. Resolved configuration is the validated immutable match specification. Runtime state is what changes while the match is being played.

### 4.2 Identity should use a single typed value

Mutually exclusive identity should not be represented as several booleans.

Examples:

- one `GameModeId` identifies the mode;
- one `CombatMode` identifies the combat rule family;
- one `MatchClockMode` identifies the clock behavior;
- one `MatchObjective` identifies the primary completion objective.

### 4.3 Independent capabilities may use flags

Flags are appropriate when several capabilities may legitimately coexist, such as an arena supporting basketball, combat, multiplayer, a seven-point line, and weather.

### 4.4 A resolved match is immutable

Once the menu/configuration service validates a match and begins a scene transition, gameplay systems may read configuration but may not mutate it.

Runtime changes belong in runtime state objects.

### 4.5 The scene has an explicit composition root

A `LevelRuntimeContext` owns or references the scene-level services needed for gameplay and makes scene requirements explicit.

### 4.6 Migration is one-way

During transition, new configuration may populate legacy `GameOptions` fields for old consumers. Old consumers must not write back and become authoritative again.

### 4.7 Tests precede destructive migration

Each current mode needs characterization coverage sufficient to prove its existing configuration before old flags are removed.

---

## 5. Target Domain Model

### 5.1 `GameModeId`

Replace raw mode integers at new API boundaries with a typed enum while preserving the existing numeric values exactly.

Example:

```csharp
public enum GameModeId
{
    TotalPoints = 1,
    Total3Pointers = 2,
    Total4Pointers = 3,
    Total7Pointers = 4,
    TotalDistance = 6,
    SpotUp3s = 7,
    SpotUp4s = 8,
    SpotUpAll = 9,
    ConsecutiveShots = 14,
    InThePocket = 15,
    ThreePointContest = 16,
    FourPointContest = 17,
    AllPointContest = 18,
    PointsByDistance = 19,
    BashUpSomeNerds = 20,
    BattleRoyal = 21,
    CageMatch = 22,
    VersusCpu = 23,
    SevenPointContest = 24,
    SpotUp7s = 25,
    BeatThaComputahs = 26,
    Lockdown = 27,
    Arcade = 98,
    FreePlay = 99
}
```

A compatibility layer may temporarily preserve `Modes` constants so callers can migrate gradually.

### 5.2 Rule dimensions

Do not create a giant enum containing every possible combination. Instead model the small number of dimensions that are actually mutually exclusive.

Proposed initial dimensions:

```csharp
public enum MatchObjective
{
    Score,
    MakeCount,
    Distance,
    ConsecutiveShots,
    ContestCompletion,
    Survival,
    LastPlayerStanding,
    CampaignProgression
}

public enum MatchClockMode
{
    None,
    Countdown,
    CountUp
}

public enum CombatMode
{
    None,
    Standard,
    Cage,
    BattleRoyal
}

public enum ShotRule
{
    Any,
    ThreePointOnly,
    FourPointOnly,
    SevenPointOnly,
    AllContestRanges
}
```

Only introduce a new dimension when existing behavior proves it is necessary. Do not pre-model hypothetical features.

### 5.3 `GameModeDefinition`

Create a ScriptableObject as the authored source of truth for a mode.

Recommended responsibilities:

- stable `GameModeId`;
- display name and description;
- primary objective;
- clock definition;
- scoring definition/reference;
- combat rule family;
- shot rule;
- basketball requirement;
- survival requirement where not already implied by objective;
- CPU-shooter policy;
- roster constraints;
- arena capability requirements;
- optional mode-specific tuning references.

The definition should describe rules, not hold runtime score/time/player state.

### 5.4 `LevelDefinition`

Converge current level metadata toward a ScriptableObject-backed definition rather than parallel level data models.

Recommended contents:

- stable level ID;
- scene identifier;
- display name and description;
- arena capabilities;
- time-of-day support/requirement;
- weather support/requirement;
- spawn profile/reference;
- optional mode-specific arena metadata;
- seven-point-line capability rather than a free-floating boolean in global state.

Do not migrate unrelated visual/environment tuning in the first slice.

### 5.5 `ArenaCapability`

Use flags for legitimate combinable capabilities.

Example:

```csharp
[Flags]
public enum ArenaCapability
{
    None = 0,
    Basketball = 1 << 0,
    Combat = 1 << 1,
    Cage = 1 << 2,
    BattleRoyal = 1 << 3,
    ThreePointLine = 1 << 4,
    FourPointLine = 1 << 5,
    SevenPointLine = 1 << 6,
    Multiplayer = 1 << 7
}
```

The exact capability list should be derived from real existing scenes during implementation, not guessed beyond current needs.

### 5.6 `PlayerRoster`

Replace the combination of `numPlayers`, `numCpuPlayers`, `player1IsCpu` through `player4IsCpu`, parallel selected-character arrays, and inferred input slots with an explicit roster.

Proposed model:

```csharp
public enum PlayerControlType
{
    LocalHuman,
    Cpu,
    RemoteHuman,
    ReplayGhost
}

public sealed class PlayerSlot
{
    public int SlotId { get; }
    public PlayerControlType ControlType { get; }
    public int? LocalInputSlot { get; }
    public CharacterSelection Character { get; }
    public string ParticipantId { get; }
}

public sealed class PlayerRoster
{
    public IReadOnlyList<PlayerSlot> Players { get; }
}
```

`ParticipantId` must not automatically be assumed to equal a save-data account ID. Online/asynchronous identity should remain a separate boundary.

This model is deliberately capable of future local-versus and asynchronous challenge participants without requiring those networking systems to be implemented now.

### 5.7 `MatchModifiers`

Represent legitimate player-selected orthogonal options in one narrow object, for example:

- difficulty;
- traffic;
- optional enemies where the mode permits it;
- obstacle toggle;
- sniper variant;
- hardcore modifier.

The builder/validator determines whether a modifier is legal for a mode/level. Gameplay does not infer legality by silently correcting configuration.

### 5.8 `MatchRequest`

The start menu should create a request, not mutate runtime globals.

Example concept:

```csharp
public sealed class MatchRequest
{
    public GameModeId ModeId { get; init; }
    public int LevelId { get; init; }
    public PlayerRoster Roster { get; init; }
    public MatchModifiers Modifiers { get; init; }
    public CheerleaderSelection Cheerleader { get; init; }
}
```

### 5.9 `MatchConfiguration`

`MatchConfigurationBuilder` resolves a valid request against authored catalogs and produces the immutable authoritative configuration.

Example concept:

```csharp
public sealed class MatchConfiguration
{
    public GameModeDefinition Mode { get; }
    public LevelDefinition Level { get; }
    public PlayerRoster Roster { get; }
    public MatchModifiers Modifiers { get; }
    public ResolvedMatchRules Rules { get; }
}
```

All constructor inputs should be validated. Runtime code gets references/read-only properties, not public setters.

### 5.10 `ResolvedMatchRules`

Some authored rules may need resolution based on level, difficulty, roster, or modifiers. That resolved result belongs in a compact immutable object.

Possible contents:

- final clock duration/mode;
- final objective;
- final combat mode;
- final shot rule;
- basketball count/policy;
- marker requirements;
- survival requirement;
- CPU shooter policy;
- scoring strategy/reference.

Do not expose dozens of legacy booleans simply to mirror `GameOptions`. Prefer semantic types and derived read-only helper properties when necessary.

---

## 6. Compatibility and Validation System

### 6.1 `GameModeCompatibility`

Create one pure/testable service responsible for determining whether a mode, level, roster, and modifier combination is valid.

Suggested API shape:

```csharp
public ValidationResult Validate(MatchRequest request);
public bool CanPlay(GameModeDefinition mode, LevelDefinition level);
```

A validation result should identify specific reasons such as:

- arena lacks required basketball capability;
- Battle Royal arena support missing;
- Cage capability missing;
- seven-point line missing;
- roster size outside mode bounds;
- CPU participants forbidden;
- local multiplayer unsupported by arena/mode;
- modifier not permitted.

### 6.2 Menu behavior after compatibility extraction

The UI may ask the compatibility service for valid levels/modes and skip or disable invalid choices, but the UI must not own the rules.

Preferred behavior is to construct a filtered list or disable unavailable entries rather than recursive self-calls such as `changeSelectedLevelUp()` calling itself repeatedly.

### 6.3 Validation must run again at match launch

UI filtering is convenience, not authority. `MatchConfigurationBuilder` must validate again before scene load so non-menu launch paths, tests, campaign flows, deep links, future online invites, and asynchronous challenge launches cannot bypass compatibility.

---

## 7. Runtime Architecture

### 7.1 `LevelRuntimeContext`

Create a scene-owned MonoBehaviour that acts as the explicit gameplay composition root.

Initial responsibilities should be small:

- expose the immutable `MatchConfiguration`;
- expose player registry;
- expose basketball registry if present;
- expose `MatchController`;
- expose spawn coordinator/bootstrap services;
- validate required scene references early.

Do not turn it into another manager containing gameplay logic.

### 7.2 `PlayerRegistry`

Own runtime participant/actor lookup and replace broad dependence on `GameLevelManager.Player1`, `Player2`, etc. over time.

Recommended API concepts:

- read-only collection of participant runtimes;
- lookup by slot ID;
- lookup of primary local human when a legacy flow specifically requires it;
- no direct mutation of underlying list by arbitrary callers.

### 7.3 `SpawnCoordinator`

Extract player, CPU player, basketball, and optional companion spawning from `GameLevelManager` incrementally.

The coordinator consumes `MatchConfiguration` and explicit spawn references. It should not inspect mutable global flags.

Subcomponents may emerge only where useful, e.g. `PlayerSpawner` and `BasketballSpawner`. Do not prematurely create many tiny classes before tests prove the boundary.

### 7.4 `ArenaBootstrap`

Own level-specific setup such as enabling/disabling basketball goal elements or combat-specific arena objects based on resolved configuration.

This replaces scattered scene-start checks and prevents general managers from silently mutating match rules.

### 7.5 `MatchController`

Focus `GameRules` toward an explicit lifecycle owner.

Proposed state machine:

```text
Preparing -> Countdown -> Playing -> Ending -> Completed
```

`MatchController` owns:

- current match phase;
- accepting a match-end request;
- enforcing idempotent transition to Ending/Completed;
- coordinating durable match-end work through explicit services;
- publishing lifecycle events.

It should not itself contain every scoring rule, marker implementation, or UI formatting function.

### 7.6 Win-condition policies

Extract mode-specific completion behavior behind small policies only after characterization tests exist.

Likely real policies include:

- time expiration;
- marker/contest completion;
- survival/death;
- last-player-standing;
- campaign round completion.

They all request match termination through one method such as:

```csharp
matchController.RequestEnd(new MatchEndReason(...));
```

Existing `GameRules.RequestGameOver()` is a useful compatibility seam and should be retained as a wrapper during migration.

### 7.7 Match clock

`GameRules` should remain the owner of legacy timer behavior until tests pin it. Then move clock semantics to a small `MatchClock`/`MatchClockController` that consumes resolved clock configuration.

Do not let `Timer` and `GameRules` become competing authorities again.

### 7.8 Scoring

Do not redesign the basketball scoring pipeline as part of the initial overhaul. Instead expose the resolved mode/rules to current scoring systems through the new configuration boundary.

A later slice may use a shared scoring policy/result event, aligned with the existing basketball architecture plan, but that should remain separately testable.

---

## 8. `GameOptions` Replacement Strategy

`GameOptions` should not be deleted first. It should be reduced category by category.

### 8.1 One-way `LegacyGameOptionsBridge`

During migration, after a `MatchConfiguration` is successfully built, a bridge writes the required values into legacy `GameOptions` fields exactly once for old consumers.

Flow:

```text
StartManager / launch source
        |
        v
    MatchRequest
        |
        v
MatchConfigurationBuilder
        |
        v
immutable MatchConfiguration  <-- authoritative
        |
        +--> MatchSession / scene bootstrap
        |
        +--> LegacyGameOptionsBridge --> old consumers
```

The bridge is temporary and should be clearly marked obsolete/deprecated.

### 8.2 Add a rule against reverse synchronization

Legacy runtime systems may not update the new configuration from `GameOptions`.

If an old system needs changing runtime state, that state must move to a runtime owner instead.

### 8.3 Split remaining non-match global state separately

After match fields migrate, the remaining `GameOptions` categories should move to appropriate owners:

| Current category | Target owner |
| --- | --- |
| menu indices | `StartMenuState` or persisted menu preference object |
| account/user identity | account/session service |
| bearer token | API/authentication service only |
| application version/platform | application/platform service or direct read where appropriate |
| previous scene | navigation/session flow service |
| match result ID | `MatchSession` |
| campaign data | campaign session/state owner |

These moves can be independent follow-up slices. The match-system overhaul should not expand into a general account rewrite.

---

## 9. `StartManager` Overhaul

### 9.1 Keep it as a UI controller initially

Do not rewrite the entire menu while changing the domain model.

First separate the concerns:

- `StartManager` manages interaction and presentation;
- a selection model stores current selections;
- compatibility service answers allowed combinations;
- builder creates the final match configuration;
- navigation service/scene loader loads the target scene.

### 9.2 Replace immediate writes to `GameOptions`

Methods such as player/mode/level display initialization should not have side effects that mutate gameplay configuration simply because an item is highlighted or rendered.

The menu should mutate only its local selection model until Start is chosen.

### 9.3 Replace recursive compatibility cycling

Build valid candidate lists or use a bounded loop over the mode/level collection. Never recursively call selection methods to find a compatible entry.

### 9.4 Launch path

At `loadGame()`:

1. gather current menu selection into `MatchRequest`;
2. validate/build configuration;
3. display a clear launch error if invalid;
4. begin a new match/session ID;
5. make configuration available to scene bootstrap;
6. populate legacy bridge values;
7. load scene.

Scene selection special cases such as the campaign scrapyard should move to a launch policy or mode definition only when safely characterized.

---

## 10. `GameLevelManager` Overhaul

### 10.1 Freeze feature additions to the manager

No new unrelated responsibilities should be added while the extraction is underway.

### 10.2 Preserve it temporarily as a facade

Existing callers can continue using `GameLevelManager.instance` while internal responsibility moves into focused collaborators.

Example:

```csharp
public PlayerIdentifier Player1 => playerRegistry.GetBySlot(0)?.Identifier;
```

This allows old code to remain stable while ownership changes underneath.

### 10.3 Extraction order

Recommended order because it minimizes behavioral risk:

1. runtime player registry/list ownership;
2. spawn-point reference validation;
3. player spawning;
4. basketball spawning;
5. cheerleader spawning;
6. arena bootstrap/mode setup;
7. NPC/vehicle duplicate-character disabling;
8. analytics calls;
9. remaining input/UI conveniences.

Each extraction should leave compatibility forwarding methods/properties where current callers need them.

### 10.4 Remove configuration mutation

Any code that currently changes `GameOptions` based on scene/mode state must become either:

- configuration validation/resolution before launch; or
- runtime state in a dedicated owner.

`GameLevelManager` must not repair invalid match definitions by mutating global configuration.

---

## 11. `GameRules` Overhaul

### 11.1 Preserve current end-match hardening

The existing idempotent `RequestGameOver()`/`HandleMatchEnded()` work should be treated as a migration asset, not discarded.

### 11.2 Replace flag copying with configuration reference

Instead of copying a dozen `GameOptions` flags in `Start()`, inject or resolve the current `MatchConfiguration` and derive the few required properties from `ResolvedMatchRules`.

### 11.3 Extract presentation from lifecycle

Current HUD formatting and required scene-name lookups should migrate to a `MatchHudPresenter` or equivalent after lifecycle behavior is characterized.

Gameplay ending should not depend on all HUD objects existing.

### 11.4 Extract durable result processing

Persistence/progression/campaign work should eventually be coordinated by a match result service invoked once from match completion. The controller provides the result; services decide how to persist/apply it.

Do not redesign persistence semantics in the first migration phase.

### 11.5 Consolidate mode dispatch after rule definitions exist

The existing duplicated score display/end-display mode dispatch should be replaced only after mode characterization coverage exists. Avoid combining this with the first configuration migration because it changes too many behavior surfaces at once.

---

## 12. Serialization and Unity Asset Safety

This overhaul touches serialized data and must respect Unity's asset model.

### 12.1 Do not delete serialized fields immediately

When `StartScreenModeSelected` begins using `GameModeDefinition`, keep old serialized fields long enough to migrate and validate existing scene/prefab data.

Possible migration paths:

- editor migration utility that creates ScriptableObjects from existing components;
- temporary adapter that reads legacy serialized fields and compares against the new definition;
- validation test that reports any mismatch.

### 12.2 Stable asset identity

`GameModeDefinition` and `LevelDefinition` assets must use stable IDs independent of asset file names. Existing numeric IDs remain canonical for persistence/backend interoperability.

### 12.3 Scene references

When extracting `GameLevelManager` scene searches, prefer serialized references or a single scene bootstrap object. Keep validation errors descriptive so missing legacy object names do not become silent null behavior.

### 12.4 ScriptableObjects are definitions, not runtime state

Never write match score, time, selected player, current winner, or mutable session values into ScriptableObject assets at runtime.

---

## 13. Testing Strategy

The overhaul needs a behavior safety net before destructive cleanup.

### 13.1 Mode characterization matrix

For every current `GameModeId`, record and test at least:

- objective;
- clock mode and duration;
- counter/countdown semantics;
- basketball requirement;
- shot marker requirements;
- allowed shot ranges;
- money-ball requirement;
- consecutive-shot behavior;
- contest variant;
- CPU-shooter behavior;
- enemy/combat mode;
- player survival requirement;
- roster constraints;
- permitted arena capabilities;
- score persistence behavior where mode-specific.

The initial characterization should describe current behavior, including weird behavior, rather than silently fixing it.

### 13.2 Configuration builder tests

Cover:

- valid mode/level pair;
- missing required arena capability;
- invalid roster size;
- forbidden CPU/player type;
- incompatible modifier;
- stable numeric mode ID mapping;
- resolved timer defaults/custom timers;
- null/missing catalog entries;
- deterministic errors.

### 13.3 Legacy bridge parity tests

For every mode, compare the new resolved configuration against the values the existing menu would previously write into `GameOptions`.

The bridge should reproduce legacy values until each consumer migrates.

### 13.4 Scene bootstrap tests

Add edit-mode validation and targeted play-mode smoke tests for:

- required scene context references;
- spawn locations required by roster;
- basketball/non-basketball scenes;
- Cage/Battle Royal arena setup;
- one-, two-, three-, and four-slot rosters;
- human/CPU mixtures.

### 13.5 Match lifecycle tests

Pin:

- only one transition to ending;
- repeated end requests are harmless;
- timer/marker/death triggers converge on the same end API;
- persistence/progression result work is not duplicated;
- missing HUD does not prevent durable match completion.

### 13.6 Architecture guard tests

Add validation rules when the migration is mature enough:

- prevent new public mutable static match fields;
- prevent new `GameOptions.*` usages outside approved legacy/bridge files;
- prevent new mode identity booleans such as `isXMode` when `GameModeId`/rule dimensions already represent the concept;
- ensure each mode definition has a unique ID;
- ensure every level definition has a unique ID;
- ensure compatibility requirements reference known capabilities.

---

## 14. Migration Phases

### Phase 0 — Baseline and Freeze

Goal: make the current system measurable before changing ownership.

Work:

- inventory every `GameOptions` field by category and consumer count;
- inventory mode/level serialized fields;
- generate current mode characterization table;
- identify backend/database use of numeric mode IDs;
- identify scene/prefab references to `StartScreenModeSelected`;
- add a policy: no new game-mode identity booleans in `GameOptions`;
- document approved migration boundaries.

Exit criteria:

- all current modes mapped;
- all current global fields categorized;
- numeric IDs confirmed stable;
- enough tests exist to compare old/new configuration.

### Phase 1 — Typed Identity and Catalogs

Goal: introduce new domain vocabulary without changing behavior.

Work:

- add `GameModeId` enum with exact existing values;
- preserve `Modes` as compatibility constants/wrappers;
- create `GameModeCatalog` abstraction;
- create `LevelCatalog` abstraction if the existing one cannot serve the needed boundary cleanly;
- add unit tests for mappings and uniqueness.

Exit criteria:

- new code no longer requires raw integer mode values;
- old code still compiles unchanged.

### Phase 2 — Authored `GameModeDefinition`

Goal: establish one authored source of truth for mode rules.

Work:

- define minimal rule dimensions based only on current modes;
- create ScriptableObject definitions;
- migrate existing mode data into assets;
- build a parity validator comparing legacy `StartScreenModeSelected` values to new assets;
- keep legacy component fields intact.

Exit criteria:

- every current mode has a definition;
- parity validator reports no unexplained differences;
- menu still uses legacy path if necessary.

### Phase 3 — Level Capabilities and Compatibility Service

Goal: remove domain compatibility logic from menu recursion.

Work:

- define arena capabilities based on actual existing level flags;
- create/extend `LevelDefinition`;
- implement `GameModeCompatibility`;
- add compatibility tests;
- change menu selection to ask compatibility service;
- add final validation at launch.

Exit criteria:

- `StartManager` no longer contains Battle Royal/Cage/shooting/fighting compatibility condition chains;
- invalid match combinations cannot launch.

### Phase 4 — `MatchRequest` and Immutable `MatchConfiguration`

Goal: create the new authoritative configuration boundary.

Work:

- create `MatchRequest`;
- create `MatchConfigurationBuilder`;
- create `ResolvedMatchRules`;
- create immutable `MatchConfiguration`;
- update launch flow to build configuration;
- store the active configuration in a match/session bootstrap owner;
- write parity tests.

Exit criteria:

- every menu-launched match produces exactly one validated configuration;
- runtime cannot mutate configuration.

### Phase 5 — Legacy Bridge

Goal: keep all old consumers operational while changing the authority.

Work:

- implement `LegacyGameOptionsBridge`;
- populate old match-related fields from `MatchConfiguration` exactly once;
- add diagnostic comparison in development/editor builds;
- mark direct legacy match fields obsolete where safe;
- stop `StartManager` from individually assigning legacy mode booleans.

Exit criteria:

- new configuration is authoritative;
- current scenes play through legacy consumers with parity;
- no reverse synchronization exists.

### Phase 6 — Player Roster Model

Goal: replace slot booleans and prepare for robust versus architecture.

Work:

- create `PlayerRoster`/`PlayerSlot`/`PlayerControlType`;
- migrate menu CPU selection into roster construction;
- derive legacy `numPlayers`/CPU flags in the bridge;
- migrate spawn logic to consume roster;
- preserve current local input mapping behavior;
- add 1-4 player combination tests.

Exit criteria:

- new runtime code never asks `player2IsCpu` etc.;
- roster can represent local human and CPU combinations explicitly;
- model can later represent remote/asynchronous participants without changing the existing local semantics.

### Phase 7 — `LevelRuntimeContext`

Goal: make gameplay scene dependencies explicit.

Work:

- create context component;
- attach/validate configuration at scene bootstrap;
- create runtime player registry;
- expose only necessary services;
- begin migrating new code away from static global access.

Exit criteria:

- new scene-owned systems receive configuration/runtime references through context or serialized dependencies;
- no new broad singleton dependencies are added.

### Phase 8 — Decompose `GameLevelManager`

Goal: reduce manager responsibility without breaking callers.

Work in vertical slices:

- extract registry;
- extract spawn-point validation;
- extract player spawn;
- extract basketball spawn;
- extract arena setup;
- migrate character/NPC/vehicle replacement behavior;
- retain facade properties/methods until callers move.

Exit criteria:

- `GameLevelManager` is a facade or can be retired;
- it does not own domain configuration;
- it does not mutate match configuration.

### Phase 9 — Refocus `GameRules` into `MatchController`

Goal: separate lifecycle from rule-specific and presentation responsibilities.

Work:

- consume `MatchConfiguration` directly;
- preserve existing `RequestGameOver` compatibility seam;
- introduce explicit match phases;
- extract clock controller when covered;
- extract win-condition policies one at a time;
- extract HUD presentation;
- route durable result work through a match-result boundary.

Exit criteria:

- match lifecycle has one owner;
- individual game modes plug in through resolved rules/policies rather than large flag chains;
- UI failure does not block game completion.

### Phase 10 — Remove Legacy Match Fields

Goal: delete the duplicated mode/match portion of `GameOptions`.

Work:

- inventory remaining reads/writes;
- migrate consumers category by category;
- enforce no-new-usage validation;
- delete obsolete mode rule fields after zero consumers;
- remove bridge assignments as fields disappear;
- retain unrelated `GameOptions` fields temporarily if their owners are separate work.

Exit criteria:

- `GameOptions` no longer represents match configuration;
- no gameplay system requires legacy mode booleans.

### Phase 11 — Split Remaining Global State

Goal: retire `GameOptions` entirely if practical.

Work:

- menu indices -> menu preference/state owner;
- account/session state -> account/auth service;
- platform/version -> appropriate service/direct Unity API;
- navigation state -> navigation/session service;
- remaining campaign/session data -> explicit owner.

Exit criteria:

- `GameOptions` is deleted or reduced to a narrowly justified compatibility object with a documented retirement path.

---

## 15. Three Review Passes

The initial plan was deliberately challenged three times before this version was finalized.

### Review Pass 1 — Behavior, Persistence, and Backward Compatibility

Questions asked:

- Does the plan accidentally change existing mode behavior while restructuring it?
- Could converting integer constants to enums break database/API/save compatibility?
- Could removing booleans early make legacy scenes impossible to run?
- Could ScriptableObject migration lose inspector-authored values?
- Does the builder become a second source of truth instead of replacing the first?

Problems found:

1. A naive replacement of `Modes` with an enum could break consumers that serialize or transmit raw numeric values if numbers changed.
2. Deleting `GameOptions` early would create a high-risk all-at-once migration across many gameplay scripts.
3. Recreating mode configuration manually in new ScriptableObjects could introduce silent parity errors.
4. If both `StartManager` and the new builder continued writing configuration independently, shadow state would remain.
5. A mode-rule refactor combined with scoring behavior cleanup would make regressions hard to attribute.

Updates made:

- preserve every existing numeric mode ID exactly;
- keep `Modes` compatibility surface during migration;
- introduce a one-way legacy bridge rather than immediate deletion;
- require characterization/parity tests before deleting legacy fields;
- require migration/validation of existing serialized mode data;
- make `MatchConfiguration` authoritative once the builder lands;
- explicitly exclude scoring/AI/combat feel changes from the initial system overhaul.

Conclusion: the overhaul is viable if it behaves as a strangler migration rather than a rewrite.

### Review Pass 2 — Unity Lifecycle, Serialization, and Scene Safety

Questions asked:

- Will ScriptableObjects accidentally become mutable runtime state?
- What happens to prefab/scene references when legacy components are replaced?
- Is `LevelRuntimeContext` at risk of becoming another singleton God object?
- Are scene searches being replaced too aggressively before inspector references exist?
- Could initialization order between menu/session/scene bootstrap create missing configuration?

Problems found:

1. ScriptableObjects are easy to misuse as session state if runtime fields are added to them.
2. Immediate removal of serialized fields/components risks prefab/scene data loss.
3. A broad `LevelRuntimeContext` could reproduce `GameLevelManager` under a new name.
4. Replacing all `GameObject.Find` calls at once creates a large scene-edit migration and reference risk.
5. Runtime configuration needs a deterministic handoff across scene loading.

Updates made:

- definitions are explicitly immutable authored data; runtime state is prohibited in them;
- legacy serialized fields remain through a verified migration period;
- `LevelRuntimeContext` is constrained to references/composition, not gameplay logic;
- scene search cleanup is phased behind validation/facade boundaries;
- launch flow explicitly stores/attaches the active configuration before scene load;
- bootstrap validation is an acceptance criterion for each migrated scene.

Conclusion: the Unity-specific risks are manageable if serialized migration and scene bootstrap are treated as first-class work rather than cleanup.

### Review Pass 3 — Scalability, Versus, Networking, and Long-Term Maintainability

Questions asked:

- Does the design actually support local versus without special-case booleans?
- Will asynchronous challenges require another rewrite?
- Are rule dimensions too abstract or too rigid?
- Could a giant `MatchConfiguration` simply become a new `GameOptions`?
- Does compatibility scale when new modes and arenas are added?
- Does the design introduce speculative networking abstractions before they are needed?

Problems found:

1. A roster modeled only as human/CPU booleans would fail as soon as remote/asynchronous participants arrive.
2. A giant enum of every possible game-mode combination would recreate combinatorial growth.
3. A giant mutable `MatchConfiguration` would simply replace one global bag with another.
4. Prebuilding networking interfaces now would be speculative and likely wrong.
5. Capability flags alone are insufficient for constraints such as roster-size limits or mutually exclusive participant policies.

Updates made:

- roster slots use a typed `PlayerControlType` extensible to remote/ghost without implementing networking now;
- mode identity and independent rule dimensions are separate;
- `MatchConfiguration` is immutable and composed of narrow objects;
- networking transport/session concepts remain outside this overhaul;
- compatibility combines capability checks with explicit constraints/validation;
- async challenge support is treated as a future launch source that can build a `MatchRequest`, not a responsibility of the match core.

Conclusion: the final model can support the planned versus direction without embedding networking complexity prematurely.

---

## 16. Final Updated Plan After Reviews

The final recommended architecture is:

```text
                 Launch Source
       (Start Menu / Campaign / Future Invite)
                         |
                         v
                    MatchRequest
                         |
                         v
              MatchConfigurationBuilder
                         |
          +--------------+--------------+
          |                             |
          v                             v
 GameModeDefinition              LevelDefinition
          |                             |
          +--------------+--------------+
                         |
                         v
              GameModeCompatibility
                         |
                         v
             immutable MatchConfiguration
                         |
             +-----------+-----------+
             |                       |
             v                       v
       MatchSession          LegacyGameOptionsBridge
             |                       |
             v                       v
       Scene Bootstrap            Old Consumers
             |
             v
       LevelRuntimeContext
             |
     +-------+--------+----------------+
     |                |                |
     v                v                v
PlayerRegistry  SpawnCoordinator  MatchController
                                      |
                           +----------+----------+
                           |          |          |
                           v          v          v
                         Clock     Scoring   Win Conditions
```

The most important sequencing rule is that the new configuration authority lands before old globals are removed. The second most important rule is that old behavior must be characterized before mode rules are consolidated. The third is that new versus work should be built on `PlayerRoster` and `MatchConfiguration`, not by adding more slot/mode booleans to `GameOptions`.

---

## 17. Concrete File/Type Proposal

Exact folder placement should follow the project's existing Core/runtime conventions, but a reasonable target structure is:

```text
Assets/Level5/Core/Match/
    GameModeId.cs
    MatchObjective.cs
    MatchClockMode.cs
    CombatMode.cs
    ShotRule.cs
    ArenaCapability.cs
    MatchRequest.cs
    MatchConfiguration.cs
    ResolvedMatchRules.cs
    MatchModifiers.cs
    PlayerRoster.cs
    PlayerSlot.cs
    PlayerControlType.cs
    MatchConfigurationBuilder.cs
    GameModeCompatibility.cs

Assets/Level5/Definitions/Match/
    GameModeDefinition.cs
    GameModeCatalog.cs
    LevelDefinition.cs

Assets/Scripts/game manager/
    LevelRuntimeContext.cs
    MatchController.cs
    PlayerRegistry.cs
    SpawnCoordinator.cs
    ArenaBootstrap.cs

Assets/Scripts/menu_start/
    StartMenuSelectionState.cs
    LegacyGameOptionsBridge.cs   // temporary
```

Do not create every proposed class in one issue. The file layout is a destination map, not a mandate for one large PR.

---

## 18. Suggested Epic and Issue Breakdown

### Epic: Match Configuration and Game Manager Overhaul

#### Issue A — Characterize current mode configuration

Deliverables:

- test/table for every mode;
- map legacy booleans to semantic concepts;
- document anomalies without fixing them.

#### Issue B — Add typed `GameModeId`

Deliverables:

- enum with stable existing values;
- compatibility with `Modes`;
- mapping tests.

#### Issue C — Add authored `GameModeDefinition`

Deliverables:

- minimal definition fields;
- assets for current modes;
- parity validator.

#### Issue D — Add arena capabilities and compatibility service

Deliverables:

- level capability mapping;
- pure validator;
- tests;
- remove recursive mode/level compatibility logic from menu.

#### Issue E — Add `MatchRequest` and `MatchConfigurationBuilder`

Deliverables:

- immutable configuration;
- validation result/error model;
- builder tests.

#### Issue F — Add legacy bridge and switch authority

Deliverables:

- menu builds new configuration;
- bridge populates old fields;
- parity diagnostics;
- no independent legacy assignment chain.

#### Issue G — Introduce `PlayerRoster`

Deliverables:

- typed participant slots;
- CPU/human migration;
- input-slot parity;
- spawn compatibility.

#### Issue H — Introduce `LevelRuntimeContext` and player registry

Deliverables:

- explicit configuration handoff;
- runtime registry;
- scene validation;
- no new singleton dependency.

#### Issue I — Extract spawning from `GameLevelManager`

Deliverables:

- player spawn coordinator;
- basketball spawn coordinator/slice;
- facade compatibility;
- tests/smoke validation.

#### Issue J — Refocus `GameRules` into match lifecycle

Deliverables:

- configuration consumption;
- explicit phases;
- end-request compatibility;
- first extracted win-condition/clock slice.

#### Issue K — Remove legacy match fields from `GameOptions`

Deliverables:

- zero consumers for targeted fields;
- architecture guard;
- bridge shrink/removal.

#### Issue L — Retire remaining `GameOptions` categories

Separate, lower-risk follow-up work for menu preferences, navigation, account/session boundaries, and application metadata.

---

## 19. Acceptance Criteria for the Overall Overhaul

The overhaul is complete when:

- every gameplay launch path produces or consumes a validated immutable `MatchConfiguration`;
- no runtime system mutates match configuration;
- no game mode requires an independent identity boolean such as `battleRoyalEnabled` or `cageMatchEnabled`;
- level/mode compatibility has one testable owner;
- `StartManager` renders/selects configuration but does not own gameplay domain rules;
- roster ownership no longer depends on `player1IsCpu` through `player4IsCpu`;
- new local-versus modes can be expressed by mode definition + roster + rules without adding global flags;
- `GameLevelManager` no longer owns configuration and has been reduced to a facade or retired;
- match lifecycle has one owner and all completion triggers converge on it;
- old numeric mode IDs remain compatible;
- legacy serialized mode data has been migrated and verified;
- automated tests detect duplicate mode IDs, invalid definitions, and new prohibited legacy usages;
- the project remains playable at each migration checkpoint.

---

## 20. Risks and Mitigations

| Risk | Severity | Mitigation |
| --- | --- | --- |
| behavior changes hidden inside architecture refactor | High | characterization and parity tests first; separate behavior fixes into distinct issues |
| serialized mode data loss | High | keep legacy fields; migration utility/parity validator; do not delete fields until verified |
| save/API mode ID break | High | preserve exact numeric values; test mappings |
| huge PR with scene conflicts | High | vertical slices; facade/bridge approach; one responsibility migration per PR |
| new configuration becomes another global bag | High | immutable composed model; narrow types; no runtime state inside definitions |
| `LevelRuntimeContext` becomes another God object | Medium | context exposes references only; gameplay logic remains in owned services |
| invalid configuration corrected silently at runtime | Medium | builder validation before launch; runtime fails clearly rather than mutating configuration |
| menu and future online launch disagree on validity | Medium | shared compatibility/builder used by all launch sources |
| old/new state divergence | High during migration | one-way legacy bridge; development parity diagnostics; prohibit reverse sync |
| overengineering future networking | Medium | represent participant type only; networking/session transport remains separate future work |

---

## 21. Decisions Locked by This Plan

Unless deliberately revisited in a later architecture decision:

- `GameOptions` is a legacy compatibility surface, not the future match architecture.
- Existing numeric game-mode IDs remain stable.
- New game-mode identity booleans should not be added.
- New match systems consume typed immutable configuration.
- ScriptableObjects define authored mode/level data but do not store mutable session state.
- Compatibility belongs outside menu presentation.
- Runtime systems do not silently repair configuration.
- `GameLevelManager` is decomposed incrementally rather than replaced with a new giant `GameManager`.
- `GameRules` evolves toward lifecycle coordination while specialized rules move outward incrementally.
- `PlayerRoster` is the foundation for future local/CPU/remote/asynchronous participant representation.
- The migration preserves behavior first; cleanup and rule simplification follow after parity is demonstrated.

---

## 22. Recommended Immediate Next Work

The first implementation work should not create the whole architecture. Begin with the safety slice:

1. inventory all `GameOptions` fields/usages and classify them;
2. build the current game-mode characterization matrix from `StartScreenModeSelected` and `StartManager` behavior;
3. add tests around that matrix;
4. introduce `GameModeId` with exact legacy values;
5. design the minimal `GameModeDefinition` fields strictly from the characterization matrix;
6. create a parity validator before changing the menu launch path.

Only after that safety slice is green should the project introduce `MatchConfigurationBuilder` and switch configuration authority.

This sequence gives the overhaul a stable save point: if work pauses after any phase, the existing game still runs and the completed architecture slice remains useful rather than leaving the project between two incompatible systems.
