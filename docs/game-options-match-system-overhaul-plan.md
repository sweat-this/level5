# Game Options and Match System Overhaul Plan

Status: Phases 0-9 implemented; #274 A/B/C/D1/D3/E implemented, D2/F blocked; #275 outstanding
Project: Level 5
Branch target: `dev`
Primary scope: `GameOptions`, `StartManager`, `StartScreenModeSelected`, `Modes`, `GameLevelManager`, `GameRules`, mode/level compatibility, roster configuration, and gameplay scene bootstrap
Last reviewed: 2026-10-14
Audited dev: `8a93cb2a7d3b979c759196f0430d3aa4895b23a8` (Unity `6000.5.7f1`); #274 incremental implementation and validation recorded in the existing inventory.

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
| 10 - remove legacy match fields | partial (#274); D2/F blocked | Current inventory: 49 stored fields + 2 forwarding properties; 41 bridge-written fallback inputs; 48 non-bridge release accesses on 45 lines in 13 files. Captured persistence and campaign ownership migrated; all supported fallback inputs retained |
| 11 - split remaining global state | partly migrated; outstanding (#275) | LocalAccountIdentity already owns local identity; MatchSession owns result ID; V1 bearer/session state is retired. Residual menu/account/navigation/metadata dependencies remain |

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

The configured match migration is already implemented: `MatchRequest → MatchConfigurationBuilder → MatchConfiguration → ActiveMatch → MatchRuntime` supplies validated immutable rules, roster and arena data. LegacyGameOptionsBridge is a one-way compatibility copy. Gameplay systems have moved to resolved configuration or explicit composition; this work does not restart phases 0–9.

The [current inventory](game-options-inventory.md) verifies 49 stored fields, two forwarding identity properties, 48 non-bridge release accesses and 41 bridge assignments, down from 55/68/45 respectively. All 41 bridge-written fields still feed the live fallback, including configured missing-character and out-of-roster cases. Six safe fields retired in #274; captured persistence and campaign route/cursor now use existing explicit owners.

LocalAccountIdentity already owns local profile identity, BackendV2SessionStore owns authenticated online identity, and MatchSession owns result identity. No new generic state manager, account system, persistence schema or navigation framework is needed. The guard now uses the existing literal-aware scanner; LoadManager's formerly hidden identity access is accounted for until #275 C. Six migrated files leave the allowlist and retired members cannot return.

Issue #273 supplied the documentation-only baseline. Phase 10/#274 implements the independent slices below; phase 11/#275 retains its own responsibility boundaries. #276 remains blocked until all production consumers, direct-scene needs and bridge-dependent launch paths are resolved. Existing numeric IDs, authored route/GUIDs, save contracts, scoring behavior and input ownership remain unchanged.

## 1. Current Retirement Constraints

### 1.1 Ownership is already split, compatibility storage is not

MatchConfiguration owns match facts; GameOptions still mirrors fallback inputs. Persistence uses captured models and explicit result identities. userName/userid are forwarding properties, not duplicate storage. LevelSelected and mode components remain authored compatibility inputs. Existing EndRoundData owns campaign position; GameOptions retains process-lifetime menu preference memory pending #275. Neither belongs in immutable configuration.

### 1.2 Fallback is a supported consumer

GameOptions.CaptureMatchRuntimeSnapshot supplies 41 fields through the existing MatchRuntime reader seam. It reconstructs live rules/roster on demand for direct scene entry and supplies primary-character/out-of-roster edge answers even when configured. Removing every normally unread flag would break this boundary. Fallback is read-only with respect to ActiveMatch, not dead code.

### 1.3 Persistence needs captured values

DBHelper.InsertGameScore now binds the captured HighScoreModel.numPlayers. Unused implicit-character progression overloads are removed, and a null result returns an unapplied diagnostic before identity lookup or pending repair. Existing explicit-ID/idempotent progression boundaries remain. Querying a newer ActiveMatch during delayed persistence would regress this ownership repair.

### 1.4 Campaign and menu positions are different identities

PlayerData.LevelsList is the authored route. EndRoundData.currentLevelIndex is now its active/completed route position; nextLevelIndex remains the proposed outcome handoff. GameLevelManager, GameRules and EndRoundMenuManager use these existing owners, while GameOptions.levelSelectedIndex is only a menu preference. CampaignRoundDecision remains pure policy. Fresh launch resets cursor zero and resolves the authored first arena by stable ID; hidden Boneyard/Dev continuation remains permitted. #275 can now retire menu preference fields independently of the campaign cursor.

### 1.5 Residual values do not justify speculative services

previousSceneName, applicationVersion, operatingSystemVersion and numOfLocalUsers remain write-only #275 candidates. #274 removed producer-less levelSelectedName and its dormant analytics branch without emitting replacement events. Tip-dialogue state and remaining menu preferences have real cross-scene behavior and need narrow lifetime decisions.

### 1.6 Historical design versus current work

Sections 2–13 retain the implemented architecture's design rationale; phases 0–9 below describe completed migration steps, not new work. Section 15 retains the original three design reviews; the two fresh #273 review passes are recorded in the inventory. Sections 16–18 retain historical target/file/issue proposals and must not be used to create duplicate existing types or services. Phase 10/11 and section 22 define current next work.

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
- `CombatMode` is a flag set: authored Cage and BattleRoyal can coexist;
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

### 8.3 Residual ownership (after independent #274 slices)

| Responsibility | Existing owner / remaining boundary |
| --- | --- |
| Configured rules/roster/arena | MatchConfiguration through ActiveMatch and explicit scene composition |
| Direct scene entry / configured fallback edges | MatchRuntime reader seam; GameOptions still supplies inputs. Independent scene-owned inputs required before deletion |
| Live menu selection | StartMenuSelectionState; cross-scene mode/friend/level/modifier memory still needs narrowly scoped retention, coordinated with campaign separation |
| Local profile identity | LocalAccountIdentity; GameOptions.userName/userid only forward |
| Online authentication | BackendV2SessionStore; no GameOptions bearer token remains |
| Application metadata | Direct Application/SystemInfo APIs; current GameOptions copies have no reader |
| Navigation | SceneTransition and specialized LocalVersusNavigationState where used; previousSceneName copy has no reader |
| Match result ID | MatchSession; old GameOptions.matchResultId already removed |
| Campaign | PlayerData authored route; existing EndRoundData active/completed cursor, proposed next position, outcomes and continues; no GameOptions route/cursor dependency |
| Startup presentation | Existing tip/CPU-select/start-launch boundary; flag still live and needs narrow start-flow retention |

See phase 10/11 for exact files and exits. These are dependency retirements, not a general account, navigation or earned-progression rewrite.

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

These guards already exist; preserve them as consumers retire (see the current inventory for limitations):

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

### Phase 10 — Remaining match-state retirement (#274)

#### 2026-10-14 implementation review (before source changes)

Baseline: `origin/dev 8a93cb2a7d3b979c759196f0430d3aa4895b23a8`, Unity `6000.5.7f1`.
Fetched #273 (closed, PR #309 merged), #274/#275/#276 (open); no open PR overlap.
Source confirms 55 stored fields, two identity forwarding properties and 45 bridge assignments;
41 assignments remain inputs to the installed fallback reader. Use the current inventory's
release-expression accounting (68 expressions, not 63 expressions).

**Review pass 1: gameplay and data integrity.** Deleting bridge writes just because configured
gameplay ignores them would break live direct-scene rules, combined shot/combat flags, timer-zero,
CPU/input ordering, Lockdown's out-of-roster defender and incomplete configured characters. Keep
all 41 fallback inputs. A's four candidates have only writes, declarations and test snapshots;
asset/reflection searches reveal no consumer. B must bind SQLite `@numPlayers` to the captured
score model, including retry after a later launch. C's one-argument progression overloads have
no consumers; null results must fail without attributing XP to the current selection, applying
anything, or repairing unrelated pending results. D1 needs separate startup preference and
bound-match filters; use the existing menu preference boundary at startup, resolved configuration
at launch, and the manager's captured rules on pause refresh. D2's object-name write still
supplies configured-incomplete fallback and cannot yet retire. D3 has no arena-name producer;
remove the dormant branch without emitting replacement events. E must migrate route and cursor
together: use the serialized PlayerData route and existing EndRoundData.currentLevelIndex for
the active/completed round. Keep nextLevelIndex as the outcome handoff; do not convert level IDs
to route positions. Preserve retry/continue decisions, hidden destinations, adapter null/index
semantics and the menu preference's last-launch behavior on return.

**Review pass 2: architecture and scope.** No new service, static state bag, route collection,
asmdef edge or persistence schema is needed for A-E. MatchConfiguration and MatchRuntime remain
authoritative and the bridge stays one-way. PlayerSelectionSession remains the menu character
owner, with D2's explicitly retained compatibility publication. Reuse EndRoundData's existing
cursor instead of adding a new global, and remove its unused route copy alongside GameOptions'
copy. Campaign launch selects the authored route's first arena through its stable ID in the
ordinary catalog; cursor zero is a route position, independent of that catalog's menu index.
Keep #275 preference storage until its own migration; expose only the existing hardcore
preference read at StartMenuSelectionState, with no second stored value. Tighten architecture
guards to count declarations (excluding methods/properties), prohibit retired members, and
remove migrated consumers; account for the pre-existing missing LoadManager allowlist entry.
F1 remains blocked: ArenaBootstrap consumes resolved rules, LevelRuntimeContext consumes
MatchRuntime, and neither authors independent rules/roster/identity inputs before early Awake.
Catalogs and PlayerSelectionSession cannot reconstruct arbitrary live direct-scene flags or
configured-incomplete character fallback. Keep the installed reader/reset lifecycle unchanged;
F2 and bridge deletion (#276) depend on a separately established scene-input contract.

Implementation order: A (dead compatibility), B/C (captured persistence identities), D1/D3
(explicit records and dormant telemetry), E1/E2 (one campaign responsibility change), then
document D2/F blockers. Use separate reviewable commits. Validate compilation and focused
bridge/fallback/architecture, SQLite/progression/record tests; validate E through the existing
authored CampaignBoundaryPlayModeTests with isolated persistence. Do not claim issue completion
while D2/F still require GameOptions.

Current acceptance and validation are in the [updated inventory](game-options-inventory.md#per-slice-acceptance). A/B/C/D1/D3/E are implemented; D2/F1/F2 remain blocked. The following sequence retains the pre-implementation source locations from #273 for traceability; use the current inventory for live locations. Each row is a separate responsibility, not permission to delete all fields together. Re-scan before further retirement and retain bridge writes until normal, fallback and edge readers are gone.

| Slice | Concrete source boundary and solution | Exit / focused evidence when implemented |
| --- | --- | --- |
| A — write-only compatibility values | GameOptions, LegacyGameOptionsBridge, StartManager:309, EndRoundMenuManager:321: retire gameModeHasBeenSelected, levelSelected, numCpuPlayers and sniperEnabled after fresh reflection/asset/caller check. Remove stale CharacterProfile comment. Retain CPU flags and sniper variants. Treat ConfigureSingleHumanRoster separately if removing its test-only compatibility surface | Zero reads confirmed for each deleted field, paired assignment/test snapshot cleanup, ratchet lowered; smallest affected Level5MatchBridgeParityTests / Level5CoreTests and architecture guards |
| B — captured score participant count | DBHelper.InsertGameScore:410 consumes HighScoreModel.numPlayers, already captured by both factories at 163/328; no SQLite column or serialization change | Queue/capture model with one roster, change globals/current match before insert, verify persisted numPlayers still matches model; focused SQLite record test and compile |
| C — explicit progression character | DBConnector:517–524 and DBHelper:480–485 already offer explicit characterId. Audit/remove unused one-argument adapters or pass explicit IDs from actual callers. ProgressionService:30 null-result diagnostic must have a defined no-result ID behavior, without silently choosing a newer active match | No GameOptions.characterId read outside fallback; focused ProgressionServiceApplyMatchResultTests plus null-result/adapter case; preserve account scope, result idempotency, XP and SQL schema |
| D1 — explicit record-query filter | PlayerData.loadStatsFromDatabase:123 takes an explicit hardcore filter; startup/menu callers use the existing preference meaning, configured launch passes configuration.Rules.Hardcore, and GameLevelManager.ReloadPlayerDataForPause:556 passes the appropriate bound match filter without changing Pause callback ownership | Startup without ActiveMatch, launched and pause-refreshed hardcore/non-hardcore queries preserved; focused Level5RangeShotRecordTests; preference handoff coordinated with #275 B |
| D2 — progression selection publication | ProgressionManager:969 currently writes characterObjectName for highlighted selection. Reuse existing PlayerSelectionSession stable ID and the screen's selected CharacterProfile; remove unused compatibility publication only after fallback edge review | Menu character selection remains independent of old active roster; smallest player-selection/session composition test; no new global character owner |
| D3 — dormant arena telemetry | GameLevelManager:635,637 reads levelSelectedName, which has no producer. Smallest behavior-preserving slice removes dormant branch/field after fresh scan. Adding a SceneName/ObjectName/DisplayName producer needs a separate explicit telemetry decision | No accidental new analytics event or renamed event payload; source guard/compile, focused event assertion only if telemetry behavior is approved |
| E1 — authored route handoff | Keep PlayerData.LevelsList as the serialized route owner; replace GameOptions route reads in GameLevelManager campaign-prefab adapter, GameRules outcome handoff and EndRoundMenuManager with explicit route references at their existing composition boundaries; replace StartManager/LoadGame copies only after all readers move | Same route order, CPU prefabs, null/invalid-index adapter behavior and hidden Boneyard/Dev finale; Level5CampaignRouteTests + affected campaign integration path |
| E2 — campaign cursor separation | Split current menu LevelIndex from active route position; choose the narrow owner by extending the existing campaign/end-round boundary only after lifecycle/reset responsibilities are recorded. Preserve EndRoundData completed/proposed indices and CampaignRoundDecision tie/retry/advance/final behavior; update GameLevelManager adapter and EndRoundMenuManager commit together | New run starts at Scrapyard, ties retry, losses consume continues as before, victories advance authored route, return menu remains coherent; Level5SpawnCoordinatorCampaignCpuPrefabResolverTests and configured CampaignBoundaryPlayModeTests / affected round tests |
| F1 — establish independent direct-scene inputs | Existing MatchRuntime.InstallLegacyFallbackReader and LegacyMatchRuntimeSnapshot are sufficient seams. Characterize the 41 fallback inputs and configured missing-character/out-of-roster cases before choosing scene-owned initialization. No approved independent fallback input owner exists today; resolve this design at the scene composition boundary before field deletion | No global compatibility replacement, no reverse sync, no reader installation gap; Level5MatchRuntimeLegacyFallbackTests and Level5ProductionAssemblyBoundaryTests; preserve missing-reader error and live-read semantics |
| F2 — retire fallback fields in groups | After F1 and earlier readers migrate: rules/clock/shot/combat/sniper; roster/input/primary-character; arena identity/capabilities; cheerleader identity. Each group is a separate change, deleting paired bridge assignments only after an explicit replacement supplies its answers | Focused fallback + affected bridge parity tests and compile for each group; required affected scene integration for ownership changes; keep IDs, flags, Lockdown defender, timer-zero signal and direct-scene limitations |

F1 is a **deletion blocker**, not a request to invent a fallback global. A configured-only replacement cannot satisfy #274 while direct scene entry still uses GameOptions. Preserve configured primary-character and out-of-roster fallback as well as unconfigured scenes. Existing MatchRuntime itself has no direct GameOptions dependency, but its installed reader does.

Phase 10 exit: no production gameplay/persistence path reads or writes legacy match state; no compatibility helper or direct-scene reader requires those fields. Reduce the allowlist and ratchet alongside actual retirement. Do not claim completion just because normal configured gameplay has zero direct rule reads.

### Phase 11 — Residual non-match ownership (#275)

Local identity storage has already moved. Authenticated online identity remains BackendV2SessionStore; MatchSession already owns result ID. This phase removes residual dependencies without creating an application-wide state bag.

| Slice | Current owner and bounded solution | Exit / focused evidence when implemented |
| --- | --- | --- |
| A1 — unused application/navigation copies | Remove GameOptions.applicationVersion/operatingSystemVersion writes in StartManager:1794–1795 and previousSceneName writes in StartManager:747, ProgressionManager:843, GameLevelManager:629 after fresh reader/reflection scan. Metadata comes from Application/SystemInfo where actually consumed; SceneTransition already loads scenes; separate LocalVersusNavigationState owns versus return | Zero remaining references to deleted copies, ratchet lowered; source inspection/compile. Do not invent a return-state service for a value nobody reads |
| A2 — unused local-profile count mirror | UserAccountManager.RefreshLocalProfiles:235 already has profiles.Count; retire numOfLocalUsers mirror, update count assertions to observable loaded-profile/row state | Level5UserAccountManagerProfileLoadingTests preserve empty/populated/reload list behavior, no new list/count authority |
| B1 — mode/friend preference memory | StartMenuSelectionState is current live selection owner; move modeSelectedIndex/friendSelectedIndex process-lifetime retention to minimal menu-owned memory at Load/SavePersistedPreferences seam. Reuse PlayerSelectionSession's stable-ID approach where appropriate, without mixing player and match runtime state | Return-to-menu retains the same choice; preserve index validation/catalog semantics and difficulty reset to Normal; no new disk persistence |
| B2 — level and modifier preference memory | Only after #274 E2 separates campaign position, retire menu levelSelectedIndex. Coordinate traffic/hardcore/obstacles preference values with #274 D1/F so UI still reflects the last resolved launch values in the same launch/save order | Menu browsing never changes active configuration; campaign cursor cannot be mistaken for menu index; explicit hardcore record filter survives startup/re-entry |
| C1 — account selection dependencies | AccountManager, UserAccountManager and LocalAccount use LocalAccountIdentity directly, preserving guest selection and current-profile deletion cleanup | Focused local-profile selection/deletion/allocation tests; unchanged SQLite/file account scopes and independent restored Backend V2 session |
| C2 — identity consumers | StartManager profile label, HighScoreModel factories, APIHelper user report and **LoadManager:321** migrate to LocalAccountIdentity directly. Preserve HighScoreModel's captured local identity, report fallback 999/not logged in, and SQLite profile lookup | Existing local-profile/score tests; no online identity substitution. Remove each allowlist entry only when its last reference disappears, including the now-visible LoadManager entry |
| C3 — remaining guard hardening | #274 corrected comment/literal scanning with the existing helper and a nested-comment regression; existing LoadManager identity debt is explicit instead of hidden. Consider repository-relative paths separately if a real basename collision appears | Do not allow new production accesses; remove existing allowances as consumers migrate. Relative-path hardening remains a separate bounded follow-up |
| D — start-flow presentation lifetime | Current flag spans TipDialogueManager, CpuSelectManager and StartManager launch reset. Establish minimal menu/start-flow memory at that existing boundary; preserve true-on-Start and reset-at-launch timing | Tip/CPU-select menu wiring works on initial flow and post-match re-entry; smallest affected TipDialogueTextMeshProMigrationTests plus focused runtime re-entry if lifecycle owner changes |

Phase 11 exit: GameOptions is no longer an account, navigation, application metadata or menu/presentation authority. No auth/account rewrite, persistence schema migration, durable earned-level state or broad navigation replacement belongs here. The authored route stays in PlayerData; campaign run state migration is #274 E, coordinated with preference retirement.

### Bridge deletion gate (#276)

Keep #276 blocked until current dev establishes all of the following. Bridge call sites can remain until the deletion patch, but must be proven unnecessary and removed in that patch.

- #274 and #275 responsibility slices are complete; the fresh release-production direct GameOptions consumer ledger reaches **zero**, including SQLite, menu, account and campaign adapters.
- The 41 fallback inputs and configured missing-character/out-of-roster cases have independent supported answers. No installed reader or direct-scene bootstrap still needs GameOptions or values populated by the bridge.
- All five current release Apply sites (StartManager, EndRoundMenuManager, two VersusLauncher sites, RemoteAttemptLauncher) and development LoadGame can launch/continue correctly without the copy. No replacement global compatibility object is introduced.
- Remove bridge, paired dead fields/methods and remaining calls together; inspect static initialization hooks, reflection, MonoScript GUID/serialized references, UnityEvents and development launch tooling at the deletion SHA.
- Update bridge-specific parity/snapshot/architecture tests deliberately, keeping configuration, typed numeric-ID, resolved-rule, roster and scene-bootstrap coverage. Do not delete useful behavioral assertions just because their old fixture named the bridge.
- Compile and run the smallest affected architecture/fallback/configuration tests and required configured/direct-scene/campaign/versus launch integration paths. Preserve local account/result contracts and report actual process/XML evidence; follow repository domain rules without rerunning unrelated certification suites.
- Re-fetch dev, inspect open-PR/worktree overlap, review final diff and document any unresolved ownership/lifetime question. A remaining real consumer keeps #276 open.

Deleting the bridge and deleting all of GameOptions are related gates, not synonyms. Today both are blocked; leaving a narrow legacy fallback bag under another name would not complete retirement.

### Target flow after approved retirement

```mermaid
flowchart LR
    U[Menu / campaign / versus / remote launch] --> Q[MatchRequest / configured round continuation]
    Q --> B[Existing builder / resolver]
    B --> A[ActiveMatch immutable configuration]
    A --> R[MatchRuntime / explicit scene composition]
    R --> S[Captured HighScoreModel / MatchProgressionResult]
    S --> D[Existing SQLite and retry boundary]
    I[LocalAccountIdentity] --> P[Local profile UI and captured result identity]
    O[BackendV2SessionStore] --> V[Authenticated online requests]
    M[Menu-owned preference memory] --> U
    C[PlayerData authored route + explicit campaign cursor] --> U
    F[Scene-owned direct-entry inputs: design required] -. existing reader seam .-> R
```

Solid edges describe the target use of existing authorities; menu-memory lifetime, campaign cursor placement and independent direct-entry inputs require bounded decisions above. This diagram is not a claim those missing owners are implemented.

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

Issue #273 supplied the documentation-only baseline; independent #274 slices are now implemented and validated in [the existing inventory](game-options-inventory.md). Do not repeat completed retirements or recreate GameModeId, definitions, builder, roster, runtime context or bridge.

1. Re-fetch dev and inspect overlapping PRs/worktrees at each implementation slice.
2. Continue #275 A1/A2 (verified unused non-match copies), with fresh removal evidence.
3. #274 A/B/C/D1/D3/E are implemented. Preserve their captured result, explicit filter and campaign cursor contracts while migrating #275 menu memory. D2 is blocked by incomplete-character fallback, not an invitation to replace menu selection from ActiveMatch.
4. Move remaining local identity callers in #275 C, including the guard-hidden LoadManager read, and resolve the small startup presentation lifetime in D.
5. Establish independent supported direct-scene inputs through the existing seam in #274 F before deleting any of the 41 fallback fields.
6. Evaluate #276 only after all deletion gates in phase 11 are satisfied; a real remaining consumer keeps it open.

Remaining design questions are minimal start-flow memory ownership and independent fallback initialization/character identity. Campaign cursor lifetime is resolved using EndRoundData; dormant analytics is retired without replacement. Do not guess the remaining choices or introduce replacement global state.

### Historical #273 validation and review record

Fetched origin/dev and issues #273–#276; inspected open PRs/worktrees; reconciled every current field/property/method and source expression; traced fallback, bridge callers, campaign, local identity and SQLite paths. Static source checks reproduce the 59-token ratchet and expose the LoadManager false negative. Both review passes and final source/diff evidence are recorded in the inventory/PR.

That historical patch changed only the two existing documents and used Level 0 inspection, with no Unity execution. Current #274 compilation, focused NUnit counts, real-scene evidence, corrected failures and remaining limits are recorded in the [updated inventory](game-options-inventory.md#focused-validation).

### #275 pre-implementation audit and two review passes (2026-10-19)

Baseline: freshly fetched origin/dev `99c3024b7f9fee4ea492423a6b3bb9eb534ea62a`, Unity 6000.5.7f1. #273 closed/PR #309 merged; #274 closed/PR #310 merged but D2/F remain blocked in source; #275 and #276 open. No open PRs targeting dev. This refresh supersedes issue-state prose from the #274 audit.

Initial plan: A1 remove write-only application/navigation copies; A2 derive profile count from the loaded list; C replace identity forwarding accesses with LocalAccountIdentity; B retain live StartMenuSelectionState and add six menu-specific process-memory values; D move one presentation bit to start-flow memory. Preserve assets, persistence, input callbacks and the 41 fallback bridge assignments.

Review pass 1 - behavioral correctness (before production edits):

- Preference timing: indices save only after a validated launch in StartManager; modifier memory currently comes from resolved bridge writes, including campaign, local-versus and remote launches. Saving highlighted requested modifiers would change forced/unsupported options. Revised plan: remember the three resolved modifier preferences at the existing ApplyModifiers publication boundary; retain index save and Awake restore ordering. Browsing and rejected launches do not save.
- Menu re-entry/catalogs: keep existing integer wrapping/eligibility and friend-list indexing; no demonstrated reorder defect justifies catalog redesign. Campaign cursor already belongs to EndRoundData, and authored route to PlayerData; never restore either from menu memory. Difficulty still resets to Normal.
- Startup records: PlayerData.Start must read menu hardcore memory without ActiveMatch. Configured launch/pause continue passing explicit rules filters.
- Identity: all forwarding properties share LocalAccountIdentity today. Replace direct callers in place, at identical capture points; keep guest 74, deletion cleanup, CharacterProgressAccountId scoping, report 999 / "not logged in", and Backend V2 identity independent.
- Presentation: tip Awake and OnEnable gate eligibility, Start marks shown; CPU select is unconditionally enabled and only marks shown in Start. Preserve the different ordering, callbacks, maps and focus. Reset only at successful ordinary launch as today.
- Deletion safety: fresh Assets/ProjectSettings/Packages scan finds no production reader, reflection string or serialized reference for metadata/navigation/count fields; ignored Legacy~ contains historical metadata writes only. Static fields are not Unity instance serialization.
- Domain reload disabled: new menu and presentation memories reset with SubsystemRegistration to emulate a new process, while scene loads retain them. Do not reset unrelated identity/player-session owners.

Review pass 2 - architecture and regression (before production edits):

- Reuse LocalAccountIdentity, StartMenuSelectionState, PlayerData and EndRoundData. PlayerSelectionSession establishes the narrow process-memory pattern; its character state remains separate. No settings/disk/cloud owner or generic global bag.
- Keep new owners beside menu code in Assembly-CSharp: Core stays Unity-free and asmdef dependencies unchanged. No scene/prefab/GUID changes are necessary.
- The menu modifier copy is preference history only; it never feeds MatchRuntime or replaces configuration. The bridge continues its one-way gameplay writes and additionally records the same resolved values for menu re-entry. Future #276 removal must keep that menu notification at supported launch boundaries.
- Preserve all 41 fallback fields/assignments, fresh snapshots, missing configured character/out-of-roster fallback, and ProgressionManager's retained character-name publication. These remain #274 D2/F and #276 blockers.
- Lower the public field ratchet from 49 to 41; remove migrated allowlist entries and prohibit returning fields/properties. Migrate test setup to actual owners and observable profile-list assertions.
- Focused evidence: affected identity/SQLite/record/campaign/menu/bridge/architecture EditMode fixtures, plus actual start-scene lifecycle/focus/menu-retention and disposable-profile PlayMode paths. Record actual exits and omissions; no unrelated full suite.

Refined implementation sequence: focused commits A1, A2, C, B, D, then integrated regression/docs. Acceptance requires no non-match GameOptions fields/properties and unchanged gameplay fallback. Any unresolved compatibility requirement stays documented rather than deleted.
