# Architecture Audit

Last updated: 2026-08-02

This document is the running audit register for Level5 architecture and gameplay systems. It starts with the main problems identified during the player interaction and systems audit, then gives us a durable place to track severity, impact, recommendations, and remediation status.

Use this document for problems and decisions. Use [Systems and Architecture Baseline](systems-architecture-baseline.md) for the current system map.

## Audit Status Legend

- Open: Known issue with no durable fix yet.
- In Progress: Fix has started but follow-up work remains.
- Mitigated: Immediate risk has been reduced, but the architecture should still improve.
- Closed: Durable fix is complete and verified.

## Main Problems

| ID | Area | Severity | Status | Problem | Impact | Recommended Solution |
| --- | --- | --- | --- | --- | --- | --- |
| AUD-001 | Combat state ownership | High | In Progress | Combat state is split across player controller, collision handlers, health scripts, animation events, UI, and the attack queue. | Changes in one script can leave another script with stale state, causing death, attack, UI, or collision bugs. | Define shared combat contracts such as `IDamageable`, `DamageInfo`, health/death events, and attack definitions. Move ownership of health/death to health components and let UI/presentation subscribe. |
| AUD-002 | Player controller scope | High | Open | `PlayerController` owns too many responsibilities, including movement, input handling, basketball actions, combat decisions, animation coordination, and state changes. | The player system becomes hard to test, risky to change, and difficult to reuse across modes. | Split player behavior into focused components: movement motor, combat driver, basketball driver, input adapter, animation bridge, and state facade. |
| AUD-003 | Global/static dependencies | High | Open | Broad use of global managers and static option/state holders creates implicit dependencies. | Systems depend on scene load order and hidden global state, making tests, prefab reuse, and mode-specific behavior more fragile. | Introduce an explicit scene-owned runtime context for gameplay services. Keep globals temporarily as compatibility bridges while new systems migrate to explicit references. |
| AUD-004 | Duplicated actor health | High | In Progress | Player, enemy, and bodyguard health/death behavior is implemented separately. | Damage, invulnerability, UI updates, death cleanup, and scoring reactions can diverge between actor types. | Create a reusable health component or common health service, then use actor-specific adapters only where behavior truly differs. |
| AUD-005 | Attack queue coupling | Medium | Mitigated | The attack queue now handles reservations more safely, but it still knows about concrete enemy/bodyguard behavior patterns. | Adding new melee actor types will require queue edits and increases the chance of leaked reservations. | Evolve `PlayerAttackQueue` into an actor-agnostic `CombatSlotReservationSystem` using an `ICombatAgent` contract. |
| AUD-006 | Animation event authority | Medium | Mitigated | Animation events still participate in important gameplay timing and cleanup. | Renamed, missing, or mistimed animation events can break attacks, projectile firing, recovery, or state reset. | Treat animation events as presentation/timing hints. Gameplay code should validate state, provide fallbacks, and own cleanup on disable/death/interrupt. |
| AUD-007 | AI state complexity | Medium | Open | Enemy, bodyguard, CPU player, and special NPC behavior rely on local booleans and bespoke checks. | Behavior changes become fragile because state transitions are implicit and scattered. | Move toward explicit finite state machines with states such as idle, patrol, pursue, reserve slot, attack, recover, stunned, and dead. |
| AUD-008 | UI/gameplay coupling | Medium | Open | Several UI flows share ownership of gameplay or progression state instead of only rendering it. | UI changes can accidentally change gameplay behavior, and gameplay changes can break menu/result presentation. | Use presenters and events. Gameplay emits state/result events; UI subscribes, formats, and renders. |
| AUD-009 | Spawn/instantiate lifecycle | Medium | Open | Projectiles have pooling, but enemies, vehicles, pickups, effects, and other high-churn objects do not appear to share a standard lifecycle. | Runtime allocation spikes, stale references, and reset bugs become more likely as scenes get busier. | Expand pooling patterns and document reset contracts for each pooled prefab category. |
| AUD-010 | Basketball flow ownership | Medium | Open | Basketball shot state, scoring, range/shot meters, stats, game rules, and player actions are spread across multiple systems. | Makes/misses, score updates, UI feedback, and progression stats can drift if the flow changes. | Document the shot lifecycle and introduce a single shot result event consumed by UI, stats, audio, progression, and game rules. |
| AUD-011 | Persistence boundaries | Medium | Open | Account, local save, server messages, database/API calls, progression, and migration systems need clearer source-of-truth documentation. | Offline behavior, retry handling, migration safety, and conflict resolution can become unclear. | Document local-vs-remote ownership, failure handling, retry behavior, migration rules, and user/account identity flow. |
| AUD-012 | Legacy/dev/test separation | Low | Open | Production scripts, test scripts, diagnostics, original managers, and utility helpers are mixed in the runtime tree. | Old or diagnostic code may be accidentally referenced by production systems. | Tag or move code into clear `Runtime`, `Editor`, `Dev`, `Legacy`, and `Tests` ownership areas over time. |

## Finding Details

### AUD-001: Combat State Ownership Is Split

Evidence:

- `PlayerCollisions` applies damage, toggles player controller damage state, displays health UI messages, and triggers audio.
- `PlayerHealth` owns health values and death events.
- `PlayerController` watches `TakeDamage` in `Update` and starts `PlayerTakeDamage`.
- `PlayerAnimationEvents`, `EnemyAnimationEvents`, and `BodyGuardAnimationEvents` enable/disable attack boxes from animation callbacks.
- `PlayerAttackQueue` coordinates enemy/bodyguard reservations and also writes back into detection components.

Why it matters: there is no single combat transaction. A hit can touch collision logic, health, animation, UI, audio, queue state, stats, and AI behavior. That makes it hard to guarantee idempotent death, clean attack interruption, or reliable UI updates.

Recommended next move: introduce a small combat contract layer before further behavior changes: `DamageInfo`, `IDamageable`, `ICombatAgent`, `HealthChanged`, `Died`, and `AttackDefinition`.

### AUD-002: PlayerController Is Over-Scoped

Evidence:

- `PlayerController` resolves UI with `GameObject.Find` at `Assets/Scripts/player/PlayerController.cs:171`.
- It pulls health and rim state from `GameLevelManager.instance` at `Assets/Scripts/player/PlayerController.cs:178`.
- It reads mobile joystick input from `GameLevelManager.instance` in movement at `Assets/Scripts/player/PlayerController.cs:243`.
- It owns attack entry with `PlayerAttack` at `Assets/Scripts/player/PlayerController.cs:658`.
- It owns damage reaction coroutine state with `PlayerTakeDamage` at `Assets/Scripts/player/PlayerController.cs:779`.
- It triggers sniper behavior through `SniperManager.instance` around `Assets/Scripts/player/PlayerController.cs:573`.
- It mutates camera FOV through `CameraManager.instance` around `Assets/Scripts/player/PlayerController.cs:881`.

Why it matters: this class is acting as movement controller, input adapter, combat state machine, basketball action owner, animation bridge, UI message writer, sniper trigger, and camera modifier. Any refactor risks side effects across unrelated gameplay features.

Recommended next move: do not split it all at once. Start by extracting the lowest-risk seam first: input intent collection or damage reaction state. Keep a facade on `PlayerController` while callers migrate.

### AUD-003: Actor Health Is Inconsistent

Evidence:

- `PlayerHealth` clamps values, exposes `OnHealthChanged`, `OnBlockChanged`, `OnSpecialChanged`, and `OnDied`, and rejects damage after death.
- `EnemyHealth` and `BodyGuardHealth` expose settable `Health`/`IsDead` properties without the same event or clamping model.
- Enemy/bodyguard damage and death cleanup are driven from collision/controller scripts instead of a reusable health lifecycle.

Why it matters: every actor type can drift in how death is detected, whether repeat damage is ignored, how UI updates, and who performs cleanup.

Recommended next move: create a reusable `HealthComponent` behavior or base service with optional actor adapters. Migrate enemy or bodyguard first because player health has the most existing UI dependencies.

### AUD-004: Attack Queue Is Still Actor-Specific

Evidence:

- `PlayerAttackQueue` stores attackers and bodyguards as `GameObject` lists.
- It discovers slots and bodyguards through tags at `Assets/Scripts/player/PlayerAttackQueue.cs:81` and `Assets/Scripts/player/PlayerAttackQueue.cs:97`.
- It writes directly into `EnemyDetection` and `BodyGuardDetection` state when assigning or clearing reservations.
- `EnemyDetection` gets the queue through `GameLevelManager.instance.PlayerController1.PlayerAttackQueue`.
- `EnemyController` and `BodyGuardController` read queue internals like `BodyGuards`, `EnemiesQueued`, and attack positions directly.

Why it matters: the reservation concept is good, but the queue currently knows too much about the actor implementations. New melee actors will require queue changes, and queue invariants can be bypassed through public lists.

Recommended next move: introduce `CombatReservation` and `ICombatAgent`. The queue should return a reservation object and expose read-only slot state. Detection/controller scripts should not mutate queue internals directly.

### AUD-005: Global Managers and Scene Searches Are Load-Bearing Dependencies

Evidence:

- Common runtime scripts use `GameLevelManager.instance`, `BasketBall.instance`, `GameRules.instance`, `Pause.instance`, `SFXBB.instance`, and other singletons.
- Scene object lookup appears in gameplay paths such as `PlayerController`, `EnemySpawner`, `TrafficManager`, `Timer`, `BasketBall`, `DBHelper`, and `DevFunctions`.
- Some systems search by object name or tag for required objects like `messageDisplay`, `shot_clock`, `steelCageRootObject`, spawn markers, and queue positions.

Why it matters: scene setup becomes an implicit API. Renames, prefab reuse, additive scenes, tests, and mode-specific scenes are fragile because required references are not visible at compile time or in prefab contracts.

Recommended next move: document a scene contract for each game mode, then introduce a `LevelRuntimeContext` that exposes typed references. Keep old globals as compatibility accessors during migration.

### AUD-006: Spawn Lifecycle Does Not Have a Shared Pooling Contract

Evidence:

- `EnemySpawner` directly instantiates minions, bosses, and battle royal contestants.
- `TrafficManager` directly instantiates vehicles and mutates prefab/controller state before spawn.
- Enemy/bodyguard controllers destroy themselves on death/knockdown paths.
- Projectiles already have `ProjectilePool` and `PooledProjectile`, so a local pooling pattern exists.

Why it matters: mixed instantiate/destroy behavior can cause allocation spikes, stale references, and inconsistent reset behavior. Mutating prefab/component state before instantiate is especially risky because authored prefab defaults can accidentally become runtime state.

Recommended next move: document reset requirements for projectiles first, then create a generic pooled actor contract for enemies or vehicles as the first non-projectile pool.

### AUD-007: Animation Events Still Own Gameplay Timing

Evidence:

- `PlayerAnimationEvents` enables/disables hitboxes and attack boxes, spawns projectiles, applies force, changes Rigidbody kinematic state, and plays audio.
- `EnemyAnimationEvents` and `BodyGuardAnimationEvents` duplicate many of the attack box, projectile, force, and audio responsibilities.

Why it matters: animation clips become hidden gameplay scripts. Missing events, renamed methods, or animation timing edits can break attacks, projectile firing, or cleanup.

Recommended next move: define animation event methods as presentation/timing adapters only. Combat drivers should own attack windows, validate that an attack is still legal, and forcibly close windows on disable, death, interruption, and state exit.

### AUD-008: Basketball Flow Mixes Gameplay, UI, Stats, and Analytics

Evidence:

- `BasketBall` holds `GameStats`, `PlayerController`, UI `Text`, shot distance, shot state, collision handling, score text formatting, shot math, and analytics calls.
- `GameRules` also formats basketball score output and calls `BasketBall.instance` for percentages.
- Shot attempts update state, stats, marker rules, player animation, shot meter messaging, and analytics in one flow.

Why it matters: shot behavior is a core game loop, but it is hard to test because one shot can affect physics, UI, scoring, progression stats, analytics, and mode rules directly.

Recommended next move: document the shot lifecycle and introduce a `ShotResult` event. UI, stats, analytics, progression, and rules should consume the result instead of being embedded in the shot execution path.

### AUD-009: Persistence and Progression Boundaries Need Ownership Rules

Evidence:

- `Pause` handles game pausing, high score persistence, all-time stat persistence, and match progression application.
- `DBConnector`, `DBHelper`, `PlayerData`, local account scripts, server messages, and progression services all participate in durable state.
- Progression has a stronger service shape than older persistence paths, but the source-of-truth rules are not documented yet.

Why it matters: save timing, offline behavior, retries, migration, and duplicate result prevention are product-critical. They should not depend on UI/pause flow details.

Recommended next move: document the persistence lifecycle from match result to local save/server sync. Promote progression result application behind a single service boundary and make pause/end-round UI call that boundary.

## Recent Mitigations

These issues were reduced by the recent player interaction work, but they should remain visible until durable architecture follow-up is complete.

- Shared combat contracts were introduced with `DamageInfo`, `IDamageable`, `ICombatAgent`, and `CombatReservation`.
- Player, enemy, and bodyguard health now expose a common damageable contract.
- Enemy and bodyguard health now clamp health, guard duplicate death transitions, and publish health/death events.
- Enemy and bodyguard health bars now subscribe to health change events instead of relying only on manual refresh calls.
- Player health and death were hardened so dead players ignore additional damage and publish clearer state.
- Player attack queue release paths were improved to reduce stuck attacker reservations.
- Projectile pooling and reset behavior were improved.
- Player movement was adjusted toward Rigidbody-friendly Unity patterns.
- Animation event entry points were made safer, but animation events still need stronger system-level contracts.

## Recommended Remediation Order

1. Define common combat contracts first: `IDamageable`, `DamageInfo`, health/death events, attack definitions, and combat agent/reservation interfaces.
2. Refactor one enemy melee path through those contracts to prove the shape.
3. Convert the player attack queue into a generic combat slot reservation system.
4. Move player health UI updates behind health/death events.
5. Document and stabilize projectile reset rules, then apply pooling to the next highest-churn runtime object type.
6. Write focused edit-mode tests for health/death, damage application, reservation release, and projectile reset.
7. Split `PlayerController` only after the combat contracts are stable enough to reduce risk.

## Audit Backlog

- Build a combat sequence diagram from input/AI decision through hit resolution and death cleanup.
- Audit all `FindObjectOfType`, `GameObject.Find`, static singleton, and global option usage.
- Inventory all `Instantiate`/`Destroy` hot paths and classify which need pooling.
- Identify every animation event method and document which ones are critical gameplay triggers.
- Trace basketball scoring from shot start through stats/progression/end-round output.
- Trace account/progression persistence from menu selection through local/server save.
- Classify scripts as core runtime, mode-specific runtime, UI, persistence, dev-only, test, or legacy.
