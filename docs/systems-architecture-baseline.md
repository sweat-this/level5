# Systems and Architecture Baseline

Last updated: 2026-08-02

This baseline documents the current runtime systems in the Unity project and the architectural direction we should use when making future changes. It is intentionally practical: enough detail to orient engineers, avoid duplicate systems, and make refactors safer without turning the docs into stale ceremony.

## Architecture Snapshot

Level5 is organized around Unity `MonoBehaviour` scripts grouped by feature folders under `Assets/Scripts`. The current architecture is scene-driven, with global managers and serialized prefab references coordinating gameplay, UI, progression, input, and persistence.

The largest gameplay surfaces are:

- Player control, combat, health, animation, and basketball actions.
- Enemy/bodyguard AI, detection, attack slots, damage, and death.
- Projectile lifecycle and sniper interactions.
- Match state, scoring, timers, game rules, and end-round flow.
- Character progression, unlocks, runtime stats, and persistence.
- Menu/input/UI screens around start, options, stats, progression, account, loading, and end-round states.

Recent combat work improved safety around health/death, attack queue release, projectile pooling, and player Rigidbody movement. The next architecture step should be gradual system extraction: stable contracts first, then one vertical gameplay slice at a time.

## Guiding Principles

- Prefer one owner for each piece of gameplay state.
- Keep model state independent from UI; UI should observe and render state changes.
- Prefer serialized references or explicit runtime context over scene-wide searches and broad singletons.
- Separate detection, decision, action execution, damage resolution, and presentation.
- Use ScriptableObjects for authored tuning data such as attacks, stats, levels, modes, and character presets.
- Pool high-churn runtime objects such as projectiles, enemies, vehicles, impact effects, and temporary attack volumes.
- Keep animation events as presentation triggers when possible; gameplay should have code-owned fallbacks and validation.
- Add tests around system contracts before large refactors.

## Systems Inventory

| System | Key Scripts | Responsibility | Current Notes |
| --- | --- | --- | --- |
| Game/session management | `GameLevelManager`, `GameRules`, `Timer`, `Pause`, `messageLog`, `LoadGame` | Owns match runtime, rules, timer, pause state, scene/game loading, and shared level flow. | Broad manager responsibilities make feature ownership harder to reason about. |
| Mode/level constants | `Modes`, `Levels`, `Constants`, `GameOptions`, `LevelCatalog`, `LevelPreset` | Stores selected mode, level configuration, and game constants. | Move toward ScriptableObject config as systems become more data-driven. Mode dispatch in `GameRules` is currently a duplicated if-chain with inconsistent constant usage (AUD-018), and `LevelSelected`/`LevelPreset` are two hand-synced level data models (see Audit Backlog). |
| Player core | `PlayerController`, `PlayerIdentifier`, `groundcheck`, `PlayerDunk`, `PlayerSwapAttack`, `PlayerAnimationEvents`, `CheerleaderSwapAnimation` | Handles player movement, basketball actions, animation callbacks, player identity, and player-specific actions. | `PlayerController` is a major orchestration point and should be split by responsibility over time. `PlayerIdentifier`'s identity/role flags are independently duplicated on `PlayerController`/`AutoPlayerController` with no single owner (AUD-013), and its `pid` is implicitly reused as a save-data array index (AUD-014). |
| Player combat and health | `PlayerHealth`, `PlayerHealthBar`, `PlayerCollisions`, `PlayerAttackBox`, `PlayerAttackQueue`, `PlayerAttackPosition` | Applies player damage/death, renders health, resolves player collisions, and coordinates enemy/bodyguard melee attack positioning. | Needs shared combat contracts so health, death, attack queues, and damage are not duplicated per actor type. |
| CPU player/defense | `AutoPlayerController`, `AutoPlayerCollisions`, `AutoPlayerDefense`, `GroundCheckDefense`, `CollisionCheckDefense` | Runs non-human player movement, defensive behavior, ground/collision checks, and automated interactions. | Should align with future AI state-machine contracts rather than owning bespoke decision rules. |
| Enemy AI and combat | `EnemySpawner`, `EnemyController`, `EnemyDetection`, `EnemyCollisions`, `EnemyHealth`, `EnemyHealthBar`, `EnemyAttackBox`, `EnemyAnimationEvents` | Spawns enemies, detects targets, drives enemy behavior, applies health/death, and executes attacks. | Enemy behavior should move toward explicit states and shared damage/attack interfaces. |
| Bodyguard AI and combat | `BodyGuardController`, `BodyGuardDetection`, `BodyGuardCollisions`, `BodyGuardHealth`, `BodyGuardHealthBar`, `BodyGuardAnimationEvents` | Runs companion/guard AI, detection, collisions, health, and animation callbacks. | Similar to enemies; duplicated health and behavior patterns should converge through shared components. |
| Attack queue/reservation | `PlayerAttackQueue`, `PlayerAttackPosition`, enemy/bodyguard detection scripts | Controls how attackers reserve positions around the player and avoids attacker pileups. | Current queue works but remains coupled to concrete enemy/bodyguard scripts. Target is a generic combat slot reservation service. |
| Projectile/sniper | `PlayerProjectile`, `EnemyProjectile`, `ProjectilePool`, `PooledProjectile`, `SniperManager`, `SniperCameraController` | Fires, reuses, and releases projectile objects; coordinates sniper-specific camera/attack behavior. | Pooling exists for projectiles and should become the standard pattern for other high-churn objects. |
| Basketball gameplay | `BasketBall`, `BasketBallAuto`, `BasketBallState`, `BasketBallShotMade`, `BasketBallShotMadeCollision`, `ShotMeter`, `RangeMeter`, `GameStats`, shot marker/test stat scripts | Owns ball state, shot attempt feedback, scoring collisions, shot meters, range meters, and game stats. | Basketball actions currently intersect with player controller, UI, and game state. Shot flow is now documented (see Basketball Shot and Scoring below); it surfaced a real scoring-integrity bug (AUD-015), a stats-display bug (AUD-016), and full human/CPU logic duplication (AUD-017). |
| Character data/progression | `CharacterProfile`, `CharacterStats`, `RuntimeCharacterStats`, `CharacterPreset`, `CharacterPresetCatalog`, `CharacterRuntimeProvider`, `SelectedLoadout`, progression services/stores/migration scripts | Stores authored character definitions, runtime stat resolution, loadout selection, upgrade progress, and migration/parity helpers. | This is already moving toward service/data separation and should be the model for future gameplay data work. |
| Input | `PlayerControls`, `PlayerControlsProvider`, `TouchInputController`, screen-specific touch controllers | Provides generated input bindings and screen-specific touch/menu input routing. | Input should route intent to owners rather than mutate unrelated systems directly. |
| Camera/presentation | `CameraManager`, `cameraUpdater`, `cameraUpdaterOrthographic`, `SniperCameraController`, visual effect scripts | Owns camera mode/follow behavior and scene presentation helpers. | Camera transitions should be explicit runtime states, especially for sniper and mode-specific views. |
| Menu/UI flow | Start menu, loading, options, credits, stats, progression, account, and end-round manager scripts | Owns screen state, user selections, menus, stats display, account UI, and round results. | UI should become a subscriber to gameplay/progression events instead of sharing ownership of state. |
| Persistence/accounts/network | `DBHelper`, `DBConnector`, `LocalAccount`, `UserAccountManager`, `ServerMessagesManager`, REST API helpers/connectors, model classes | Handles local account identity, server messages, database/API calls, and serialized data models. | Needs documented failure/retry/offline behavior and clear boundaries between local and remote truth. |
| Analytics/email | `AnaylticsManager`, `SendEmail`, report models | Sends analytics and email/report data. | Should be isolated behind service interfaces so gameplay can run when reporting fails. |
| Audio | `SFXBB` | Plays basketball/game sound effects. | Audio should respond to gameplay events rather than being called from many gameplay scripts directly. |
| Vehicles/traffic | `TrafficManager`, `VehicleController`, `VehicleMove`, `BehaviorVehicleLawnmower` | Spawns and moves traffic/vehicles and handles special vehicle behavior. | Candidate for pooling and state-based behavior similar to enemy spawning. Fixed 2026-08-02: `TrafficManager` no longer mutates shared prefab-sourced `VehicleController` state before `Instantiate()` (AUD-020) - still not pooled, just no longer racy. |
| Racing mode | `RacingGameManager`, `RacingVehicleController`, `RacingVehicleCollisions`, `RacingGroundCheck`, `RacingAnimationEvents`, `RacingCinderBlock`, `RacingVehicleProfile` | Owns racing-specific player vehicle movement, collisions, hazards, animation events, and mode state. | Treat as a separate vertical mode with shared input/progression/presentation contracts where practical. |
| NPC/special behaviors | `BehaviorPrimo`, `BehaviorNpcRob`, `BehaviorNpcCritical`, `BehaviorNpcAutonomous`, `TheyLiveManager`, `RandomEvents`, pickups/sunglasses/misc scripts | Implements special NPC logic, random events, pickups, and one-off character behaviors. | These scripts need ownership tags: core feature, mode-specific feature, or legacy/experimental. |
| Utility/dev/test | `UtilityFunctions`, `DevFunctions`, `PlatformCheck`, FPS displays, test scripts, original/legacy managers | Provides helpers, diagnostics, platform checks, test scenes/scripts, and legacy references. | Separate production helpers from dev-only and legacy scripts to reduce accidental dependencies. |

## Core Runtime Flows

### Scene Bootstrap and Match Start

`GameLevelManager` and related game manager scripts coordinate the scene-level runtime. Menu selections and loading scripts provide selected mode, level, player, and character data. Gameplay scripts then discover or receive references to required actors, UI, timers, and rules.

Target direction: define a `LevelRuntimeContext` or similar scene-owned context that exposes only the services needed by gameplay systems. This makes scene requirements explicit and reduces hidden dependency chains.

### Player Attack, Damage, and Death

Player attack behavior currently spans the player controller, attack box, collision handlers, health, animation events, and enemy/bodyguard interactions. Player death should be owned by `PlayerHealth`, while presentation and UI respond to health/death state.

Target direction: introduce shared contracts:

- `IDamageable` for anything that receives damage.
- `DamageInfo` for source, amount, impulse, type, and context.
- `HealthChanged` and `Died` events for UI, audio, scoring, and AI reactions.
- `AttackDefinition` for authored attack timing, range, damage, cooldown, and animation metadata.

### Enemy/Bodyguard Attack Queue

`PlayerAttackQueue` coordinates which attackers can occupy attack positions around the player. This prevents attackers from stacking into the same combat space and gives melee behavior a central reservation point.

Target direction: evolve the queue into a generic `CombatSlotReservationSystem` that knows about slots and reservations, not concrete enemy/bodyguard classes. Detection components should request a reservation; controllers should execute behavior based on reservation state; release must happen on death, disable, target loss, and attack completion.

### Projectile Lifecycle

Projectile scripts use pooling to reduce runtime allocation and instantiate/destroy churn. `PooledProjectile` and `ProjectilePool` provide the reusable object lifecycle, while player/enemy projectile scripts own projectile behavior.

Target direction: standardize projectile reset rules and apply the same pooling discipline to enemies, vehicles, pickups, and temporary effects.

### Basketball Shot and Scoring

Basketball state, shot meters, shot collision, range meter, markers, stats, and game manager rules collectively resolve attempts, makes/misses, and match progress.

Traced lifecycle: `PlayerController.PlayerShoot()` calls `BasketBall.shootBasketBall(...)` using range flags `BasketBallState` computes continuously from rim distance. `shootBasketBall` sets shot-type attempt flags and marker/moneyball state, then `LaunchBasketBall()` waits on the shot meter and calls `Launch()`, which computes projectile velocity, rolls crit/accuracy/range/release modifiers, writes shot-meter UI text, and fires an analytics call - all inline in one method. Make detection is physics-based: `BasketBallShotMade` sets a first-trigger flag, `BasketBallShotMadeCollision` checks it and calls `BasketBallShotMade.shotMade()`, which updates `GameStats` totals/points. UI (`BasketBall.updateScoreText`, `GameRules.GetStatsTotals`, `BasketBallShotMarker`) all read `GameStats`/`BasketBallState` directly and reformat text independently. `BasketBallAuto` duplicates nearly this entire pipeline for CPU shooters, and has already diverged from it in places.

This tracing surfaced concrete bugs, not just structural risk: shot-type attempt flags were not reset on a miss, so a miss-then-make across two shot categories could double-score (AUD-015, fixed); `GameRules.GetStatsTotals` could pair one player's made/attempt counts with a different player's accuracy percentage in local-multiplayer modes because `BasketBall.instance.gameStats` is a reassignable shared reference (AUD-016, fixed); and the full shot pipeline is copy-pasted between `BasketBall`/`BasketBallAuto` (AUD-017, still open).

Target direction: move toward a single shot result event that UI, stats, audio, progression, and game rules can consume, replacing direct cross-system reads. Add regression coverage for AUD-014/AUD-015/AUD-016 before deeper basketball refactors.

### Progression and Persistence

Character presets, runtime stats, progression services, result stores, migration helpers, account identity, and server/local persistence scripts form the progression data pipeline.

Target direction: keep progression service boundaries explicit. Gameplay should emit match results; progression services should transform results into durable character/account state; UI should render results from service output.

## Known Problems and Solutions

| Problem | Risk | Recommended Solution |
| --- | --- | --- |
| Combat state is split across controller, collision, health, animation, UI, and queue scripts. | Bugs appear when one script changes state without the others knowing. | Add shared combat contracts and event-driven health/death notifications. |
| `PlayerController` owns too many responsibilities. | Movement, combat, basketball, input, animation, and state changes become difficult to test or modify independently. | Split by vertical behavior: motor, combat driver, basketball driver, animation/presentation bridge, and state facade. |
| Global managers and static options are broad dependency paths. | Systems become hard to run in isolation and order-of-initialization issues are harder to diagnose. | Introduce explicit scene context/service references and keep global access as a compatibility bridge during migration. |
| Enemy and bodyguard behavior use similar but separate patterns. | Fixes and tuning need to be duplicated across actor types. | Define shared AI/combat interfaces and move common detection, reservation, attack, health, and death behavior into reusable components. |
| Attack queue is improved but still actor-specific. | Adding new melee actor types will require queue edits. | Make attack reservations actor-agnostic through `ICombatAgent` or equivalent. |
| Animation events can drive critical gameplay. | Missed or renamed events can break attacks, projectile firing, or state cleanup. | Keep animation events as timing hints with code-owned validation, fallbacks, and state cleanup. |
| UI and gameplay state are interwoven in several flows. | UI changes can accidentally alter gameplay behavior and vice versa. | Use events and presenters: gameplay emits state/result events, UI subscribes and renders. |
| Spawn systems instantiate high-churn actors directly. | Runtime allocation and cleanup complexity can cause spikes and stale references. | Expand pooling beyond projectiles and document reset contracts per pooled prefab. |
| Health is implemented separately for player, enemy, and bodyguard. | Death, invulnerability, UI updates, and damage behavior can diverge. | Introduce a reusable `HealthComponent` or common health service with actor-specific adapters where needed. |
| AI behavior relies on many booleans and local checks. | State transitions become fragile as behavior complexity grows. | Move toward explicit finite state machines: idle, patrol, pursue, reserve slot, attack, recover, stunned, dead. |
| Legacy, test, and production scripts are mixed together. | Accidental references to old or diagnostic code become more likely. | Tag or move scripts into `Runtime`, `Editor`, `Dev`, `Legacy`, and `Tests` conventions over time. |
| Player identity/role flags are duplicated across `PlayerIdentifier`, `PlayerController`, and `AutoPlayerController` with no single owner. | A new spawn/mode path can set one copy and forget the other, silently splitting CPU-detection or defensive behavior between subsystems. | Make `PlayerIdentifier` the single source of truth; have the controllers read from it instead of holding independent copies. |
| Basketball shot-attempt flags and result percentages had concrete integrity bugs. | Fixed now, but the lack of regression coverage means future shot-pipeline edits could reintroduce miss-then-make double scoring or wrong-player percentage displays. | Add scoring/stat regression tests before extracting the shared shot pipeline. |
| Game mode dispatch was duplicated across two `GameRules` methods with inconsistent constant usage (fixed 2026-08-02: both now use `Modes` constants consistently, though still two separate if-chains). End-of-match side effects previously ran inside a per-frame `Update()` poll rather than a fire-once flow. | Per-frame polling of end-of-match side effects risked re-entrancy/duplicate execution. | Mitigated: `GameRules` now routes timer, marker, and death triggers through `RequestGameOver()` and gates side effects behind `matchEndHandled`/`HandleMatchEnded()`. Unity verification pending. (Note: `GameRules.IsGameOver()` is live marker-based win-condition logic, not dead code as an earlier pass of this doc claimed - see AUD-019.) |

## Near-Term Architecture Plan

1. Document combat in detail: current attack queue flow, health/death flow, attack boxes, projectiles, and animation event contracts.
2. Add shared combat interfaces without moving all implementations at once.
3. Refactor one actor vertical slice first, preferably enemy melee combat, to prove the contracts.
4. Move player UI health updates behind health events.
5. Convert attack reservations to actor-agnostic slots.
6. Extract player movement from `PlayerController` only after combat contracts are stable.
7. Add edit-mode tests for health, damage, queue reservation/release, and projectile pool reset.
8. Add play-mode smoke tests for scene bootstrap, player death, enemy attack, and projectile reuse.

## Suggested Folder/Ownership Direction

This is a target shape, not an immediate migration requirement:

```text
Assets/Scripts/
  Core/
    Runtime/
    Services/
    Events/
  Gameplay/
    Combat/
    Player/
    AI/
    Basketball/
    Projectiles/
    Vehicles/
    Racing/
  Data/
    Characters/
    Levels/
    Modes/
  UI/
    Screens/
    Presenters/
  Persistence/
    Account/
    Progression/
    Network/
  Dev/
  Tests/
```

## Documentation Standards

- Every major runtime system should have a short owner doc with responsibilities, key scripts, data inputs, events, scene/prefab requirements, and known failure modes.
- Every cross-system flow should have a sequence outline before major refactors.
- Every ScriptableObject config type should document who authors it and who consumes it.
- Every pooled prefab should document reset requirements.
- Every global/static access path should be documented as intentional, compatibility-only, or targeted for removal.

## Glossary

- Actor: A gameplay entity that can take actions, such as the player, enemy, bodyguard, CPU player, or vehicle.
- Damageable: Anything that can receive damage and report health/death state.
- Attack definition: Authored attack data such as damage, range, timing, cooldown, animation, and hit rules.
- Attack reservation: A claim on a combat position or slot that lets an actor attack without overlapping other attackers.
- Runtime context: A scene-owned object that provides explicit references to systems needed during play.
- Presentation bridge: Code that translates gameplay state into animation, UI, audio, camera, or visual effects.
