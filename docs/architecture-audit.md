# Architecture Audit

Last updated: 2026-08-02

This document is the running audit register for Level5 architecture and gameplay systems. It starts with the main problems identified during the player interaction and systems audit, then gives us a durable place to track severity, impact, recommendations, and remediation status.

Use this document for problems and decisions. Use [Systems and Architecture Baseline](systems-architecture-baseline.md) for the current system map.

AUD-022 through AUD-033 came out of the 2026-08-06 deep audit and are tracked, with their fixes, in
[Deep Audit 2026-08-06](deep-audit-2026-08-06.md) rather than duplicated into the table below. All
twelve (AUD-022 to AUD-033) are fixed in code and pending Unity compile/playtest verification. A
second pass added AUD-034 to AUD-037 and a third added AUD-038 to AUD-039, also fixed and awaiting
Unity compile/playtest verification. A fourth pass added AUD-040 to AUD-043 (stats-screen paging) and a fifth added AUD-044 to AUD-045
(combat ownership and account identity), also fixed. All twenty-four await Unity compile/playtest
verification.

## Audit Status Legend

- Open: Known issue with no durable fix yet.
- In Progress: Fix has started but follow-up work remains.
- Mitigated: Immediate risk has been reduced, but the architecture should still improve.
- Closed: Durable fix is complete and verified.

## Main Problems

| ID | Area | Severity | Status | Problem | Impact | Recommended Solution |
| --- | --- | --- | --- | --- | --- | --- |
| AUD-001 | Combat state ownership | High | Mitigated | Combat state is split across player controller, collision handlers, health scripts, animation events, UI, and the attack queue. | Confirmed concretely: `EnemyCollisions.enemyIsDead` and `EnemyController.enemyIsDead` were two independent copies of the enemy-death side effects that had **already diverged** - the same kill healed the player 5/2 (boss/minion) through the collision path but 7/3 through the controller path. | Fixed: `EnemyController.HandleDeath(attacker, creditToPlayer)` is the single owner of enemy death - heal, kill credit, critical flourish, and the death coroutine. Both call sites delegate. Heal amounts unified on 5/2 (the melee path, which is what the game has always felt like) and named as constants. `creditToPlayer` is explicit rather than inferred from a null attacker, because the two originals disagreed on whether friendly fire rewards the player - it does not. `ActorHealth` (AUD-004) already owns the death latch, and the health bars already subscribe to `OnHealthChanged`. **Still open**: `PlayerCollisions` retains damage + controller state + UI + audio in one place, and attack definitions are still per-actor-type `PlayerAttackBox`/`EnemyAttackBox` components. |
| AUD-002 | Player controller scope | High | Open | `PlayerController` owns too many responsibilities, including movement, input handling, basketball actions, combat decisions, animation coordination, and state changes. | The player system becomes hard to test, risky to change, and difficult to reuse across modes. | Split player behavior into focused components: movement motor, combat driver, basketball driver, input adapter, animation bridge, and state facade. |
| AUD-003 | Global/static dependencies | High | Open | Broad use of global managers and static option/state holders creates implicit dependencies. | Systems depend on scene load order and hidden global state, making tests, prefab reuse, and mode-specific behavior more fragile. | Introduce an explicit scene-owned runtime context for gameplay services. Keep globals temporarily as compatibility bridges while new systems migrate to explicit references. |
| AUD-004 | Duplicated actor health | High | Mitigated | Player, enemy, and bodyguard health/death behavior is implemented separately. | Damage, invulnerability, UI updates, death cleanup, and scoring reactions can diverge between actor types - and had: `BodyGuardHealth`'s max-health property was still literally named `MaxEnemyHealth`, and the two spelled the hardcore bonus differently. | Fixed: `Assets/Scripts/combat/ActorHealth.cs` owns the clamping `Health` setter, the `MaxHealth` setter, the death latch, `ApplyDamage`/`Heal`/`TakeDamage`, and both events. `EnemyHealth` and `BodyGuardHealth` now derive from it and implement only how max health is chosen (spawn-role scaling vs. a flat default). `PlayerHealth` deliberately stays separate - it carries block/special/respawn state no AI actor has - but already implemented `IDamageable` with the same `Health`/`MaxHealth`/`IsDead` vocabulary, so all three now read alike. `[FormerlySerializedAs]` preserves prefab values across the field rename. Covered by 10 new edit-mode tests. |
| AUD-005 | Attack queue coupling | Medium | Mitigated | The attack queue now handles reservations more safely, but it still knows about concrete enemy/bodyguard behavior patterns. | Adding new melee actor types required editing the queue in three places. The two detection components held the same three fields but named the sighting flag differently (`PlayerSighted` vs `EnemySighted`), so the queue carried four near-identical blocks to set them. | Fixed: new `ICombatDetection` (`Attacking`, `AttackPositionId`, `TargetSighted`) is implemented by `EnemyDetection` and `BodyGuardDetection`, which keep their existing names as the backing properties. `PlayerAttackQueue` now sets reservation state through one `SetAttackerDetection` helper and asks `CanReserve` through the interface; it no longer names either concrete type. Reservation/release itself was already `ICombatAgent`-based. The pass-two note that `RefreshBodyGuards` only ran in `Start` is resolved separately - `BodyGuardController` self-registers on enable and unregisters on disable/death. **Still open**: the class is still named `PlayerAttackQueue` and still lives on the player, rather than being a scene-level `CombatSlotReservationSystem`. |
| AUD-006 | Animation event authority | Medium | Mitigated | Animation events still participate in important gameplay timing and cleanup. | Renamed, missing, or mistimed animation events can break attacks, projectile firing, recovery, or state reset. | Treat animation events as presentation/timing hints. Gameplay code should validate state, provide fallbacks, and own cleanup on disable/death/interrupt. |
| AUD-007 | AI state complexity | Medium | Open | Enemy, bodyguard, CPU player, and special NPC behavior rely on local booleans and bespoke checks. | Behavior changes become fragile because state transitions are implicit and scattered. | Move toward explicit finite state machines with states such as idle, patrol, pursue, reserve slot, attack, recover, stunned, and dead. |
| AUD-008 | UI/gameplay coupling | Medium | Open | Several UI flows share ownership of gameplay or progression state instead of only rendering it. | UI changes can accidentally change gameplay behavior, and gameplay changes can break menu/result presentation. | Use presenters and events. Gameplay emits state/result events; UI subscribes, formats, and renders. |
| AUD-009 | Spawn/instantiate lifecycle | Medium | Mitigated | Projectiles have pooling, but enemies, vehicles, pickups, effects, and other high-churn objects do not appear to share a standard lifecycle. | The premise is now out of date - enemies (`EnemySpawner`) and vehicles (`TrafficManager`) both pool through `RuntimeObjectPool`. The live gap was that reset happened purely by `OnEnable` convention, which nothing declared or enforced, and `RuntimeObjectPool.Spawn` ran the caller's `configure` callback *before* activation, so any type that reset a field its caller configures would silently discard it. | Fixed: new `IPooledSpawnReset` makes the contract explicit, and `Spawn` invokes it on the inactive instance **before** `configure`, so reset clears the previous life, configure applies this one, and activation lets `OnEnable` see both. `EnemyController` implements it (and cascades to `EnemyHealth`); the interface documents that only the prefab's root coordinator should, since implementing it on both would reset health three times per spawn. `Spawn` also gained the null guards `Release` already had. The ordering bug was latent, not live - `VehicleController.ConfigureRoute` does not touch the configured `Direction`/`CurrentTarget`. **Still open**: `ProjectilePool` remains a second, independent pool implementation alongside `RuntimeObjectPool`; the two should merge. |
| AUD-010 | Basketball flow ownership | Medium | Open | Basketball shot state, scoring, range/shot meters, stats, game rules, and player actions are spread across multiple systems. | Makes/misses, score updates, UI feedback, and progression stats can drift if the flow changes. | Document the shot lifecycle and introduce a single shot result event consumed by UI, stats, audio, progression, and game rules. |
| AUD-011 | Persistence boundaries | Medium | Open | Account, local save, server messages, database/API calls, progression, and migration systems need clearer source-of-truth documentation. | Offline behavior, retry handling, migration safety, and conflict resolution can become unclear. | Document local-vs-remote ownership, failure handling, retry behavior, migration rules, and user/account identity flow. |
| AUD-012 | Legacy/dev/test separation | Low | Mitigated | Production scripts, test scripts, diagnostics, original managers, and utility helpers are mixed in the runtime tree. | Old or diagnostic code may be accidentally referenced by production systems - confirmed, not theoretical: three production files referenced dev code, and two more dead files (1683 + 311 lines) sat in the runtime tree. | Fixed: `Assets/Scripts/Dev/` now holds `DevFunctions`, `CharacterProgressParityLogger`, `AutoPlayerControllerTest`, `TestText`, `BasketballTestStats`, `BasketBallTestStatsConclusions`; the two live production call sites are wrapped in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`; the fully-commented `BasketBallShotMarkerAuto.cs` joined `StartManager_original.cs` in a `Legacy~` folder. `Level5ProjectValidator.CollectDevIsolationErrors()` + a new edit-mode test fail the build on any unguarded production→Dev reference. **Still open**: the `.asmdef` split of `Assets/Scripts` - see the note below the table. |
| AUD-013 | Player identity/role ownership | Medium | Mitigated | `PlayerIdentifier.isCpu`/`isDefensivePlayer` are duplicated independently on `PlayerController`/`AutoPlayerController`, and each player slot has two hand-synced `PlayerIdentifier` instances (actor + basketball object). | A new spawn/mode path that sets one flag copy and forgets the other silently splits CPU-detection or defensive behavior between subsystems, with no validation. | Fixed: the known runtime reads in `BasketBall.cs`, `groundcheck.cs`, and `AutoPlayerController.cs` now go through `PlayerIdentifier`. Still open: the controllers' duplicate fields still exist (kept for serialization safety) and the two-`PlayerIdentifier`-instances-per-slot structure is unchanged. |
| AUD-014 | Player slot index used as save-data key | High | Mitigated | `HighScoreModel` derives which player's stats to save by looping over `PlayerIdentifier` entries and overwriting an index variable for every non-CPU entry, then reads back by that index; other code assumes `pid` always equals list position. | With more than one human player, or if `pid`/list-position ever diverge, this silently saves/highscores the wrong player's stats - a save-data correctness bug, not just a smell. | Code fix in place: `convertBasketBallStatsToModel` now uses `GetPrimaryPlayer` and `TryGetPlayer`. Pending: Unity compile/playtest verification. |
| AUD-015 | Basketball shot-attempt flags never reset on miss | High | Mitigated | `TwoAttempt`/`ThreeAttempt`/`FourAttempt`/`SevenAttempt` flags are set true when a shot is launched but only ever cleared inside the make path - nothing resets them on a miss. | A player who misses a 2pt attempt then makes a 3pt attempt gets scored for both categories at once - a reachable scoring-integrity bug, not theoretical. | Code fix in place: `BasketballState.ResetShotAttemptSnapshot()` clears attempt, marker, and moneyball snapshot state and is called at the start of each human/CPU attempt. Pending: Unity compile/playtest verification. |
| AUD-016 | Basketball percentage stats can read the wrong player | Medium | Mitigated | `GameRules.GetStatsTotals` mixed one player's make/attempt counts with `BasketBall.instance`'s accuracy getters, but `BasketBall.instance.gameStats` is reassigned inside `updateScoreText` to whichever player last triggered a score update. | In local-multiplayer modes, a displayed shooting percentage could silently come from a different player's accuracy state than the made/attempt counts sitting next to it. | Code fix in place: percentages now compute from `gameStats1` counts via `UtilityFunctions.getPercentageFloat`, with no `BasketBall.instance` dependency. Pending: Unity compile/playtest verification. |
| AUD-017 | `BasketBall`/`BasketBallAuto` duplicate the shot pipeline | Medium | Open | `shootBasketBall`, `Launch`, modifier math, and score-text formatting are copy-pasted between the human and CPU basketball scripts, and have already diverged (different meter-wait condition, missing money-ball block in the auto path). | Every shot-logic fix, including AUD-015/AUD-016, must be applied twice or the human/CPU paths silently diverge further. | Deferred: assessed as too large/risky to extract safely without test coverage or playtesting in this pass. Needs its own focused slice. |
| AUD-018 | Game mode dispatch duplicated with inconsistent constants | Medium | Mitigated | `GameRules.SetScoreDisplayText` and `GameRules.GetDisplayText` each implement their own long if-chain over the same mode set, one using named `Modes` constants and the other raw integer literals. | Adding, renaming, or reordering a mode means editing two independently-maintained literal representations that can silently drift apart. | Fixed the inconsistency: both methods (plus `GameRules.Update()`) now use `Modes` constants only, no raw literals for named modes. Still two separate if-chains, not one lookup - full consolidation deferred as too risky without test coverage. |
| AUD-019 | Match-end side effects run on a per-frame poll | Low | Mitigated | **Corrected 2026-08-02**: this entry originally claimed `GameRules.IsGameOver()` was dead code competing with a live `Timer` path - that was wrong (see Finding Details). The real, narrower issue: end-of-match side effects ran inside `GameRules.Update()`'s per-frame poll rather than a fire-once flow. | Per-frame polling of end-of-match side effects (persistence, progression, scene load) risked re-entrancy or duplicate execution if the condition held across multiple frames. | Code fix in place: `GameRules` now routes end conditions through `RequestGameOver()`, subscribes to `PlayerHealth.OnDied`, and gates match-end side effects with `matchEndHandled`/`HandleMatchEnded()`. Pending: Unity compile/playtest verification. |
| AUD-020 | Traffic vehicles mutate shared prefab state before instantiating | Medium | Closed | `TrafficManager` set `Direction`/`FacingRight`/`CurrentTarget` directly on the shared, prefab-sourced `VehicleController` list entries before every `Instantiate()` call, in `spawnVehiclePrefabs`, `spawnCustomVehiclePrefabs`, and `spawnVehicleCoRoutine` - the exact pattern already generically flagged in AUD-009, with the original developer's own comment acknowledging it (`this is saving and changing prefabs value`). | `spawnVehicleCoRoutine` mutated the shared reference then waited (`WaitForSeconds`) before instantiating; two respawns of the same vehicle close together could race, with the second's field-writes overwriting the first's pending values on the same shared object. | Fixed: all three call sites now instantiate first and set the per-shot fields on the returned clone instead of the shared list entry. Verified `VehicleController`/`VehiclesList` have no consumers outside `vehicle/TrafficManager.cs` and `vehicle/VehicleController.cs`, so this was safe to change in isolation. |
| AUD-021 | Beat the Computahs continues never reset between campaign attempts | High | Closed | `EndRoundData.numberOfContinues` is a `static` field decremented on a loss-with-continues-remaining (`EndRoundMenuManager.cs`) and zeroed for hardcore mode (`StartManager.setGameOptions`), but nothing ever reset it back to its default when starting a fresh, non-hardcore campaign run. | A player who exhausted their continues once had zero continues on every subsequent "Beat the Computahs" attempt for the rest of the application session, with no indication why - a real, session-persistent progression bug (ties confirmed as an intentional mechanic, not a bug - not touched). | Fixed: `StartManager.setGameOptions()` now explicitly resets `EndRoundData.numberOfContinues` to `EndRoundData.DefaultContinues` for non-hardcore runs (still 0 for hardcore) on every fresh game start, instead of only ever zeroing it. `DefaultContinues` extracted as a named constant (left at 2, the pre-existing default - not a confirmed design number, so not changed) so the default lives in one place. |

### Note on the AUD-012 assembly split (2026-08-07)

The folder separation and its enforcement are done. The remaining half - giving `Assets/Scripts` its
own `.asmdef` - is **blocked on a Unity session**, not on effort, and should not be attempted blind.

What was established:

- **No reverse dependency.** Nothing under `Assets/Joystick Pack`, `Assets/Standard Assets`,
  `Assets/OmniSARTechnologies`, or `Assets/DialogueManager.cs` references any type in
  `Assets/Scripts`. The split is therefore possible without a circular assembly reference.
- **The outbound surface is only two types.** `Joystick`/`FloatingJoystick` (used by
  `GameLevelManager` and `RacingGameManager`) and `DialogueManager` (used by `LocalAccount` and
  `UserAccountManager`). `LiteFPSCounter` looked like a third but is only a `GameObject.Find`
  string, not a type reference. So the split needs one new asmdef for the Joystick Pack and a home
  for `DialogueManager` - not a wholesale reorganisation.
- **What blocks it.** An asmdef must name every package assembly it uses, and a wrong name is a
  project-wide compile failure. `Assets/Scripts` pulls in `UnityEngine.InputSystem`,
  `Unity.Mathematics`, `Newtonsoft.Json`, `UnityEngine.Analytics`, `Mono.Data.Sqlite`, and
  `Unity.IO.LowLevel.Unsafe`. Some of those resolve to precompiled DLLs (referenced automatically);
  others are asmdef-based package assemblies that must be listed explicitly. Which is which cannot
  be determined from the repository - it depends on the resolved package versions in `Library/`,
  which is correctly not tracked in git.

Do this from an open Unity editor, where the console names any assembly that fails to resolve.

One related observation worth recording: `ProjectSettings/EditorSettings.asset` and every `.unity`
scene are currently **binary** on disk, even though `Level5ProjectValidator.ConfigureSourceControlPolicy`
sets `SerializationMode.ForceText` on load. That confirms the project has not been opened in Unity
since that policy was added. The first open will convert them - which also makes AUD-046's
uninspectable serialized value greppable for the first time.

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
- `GameRules` also formats basketball score output; percentage formatting has been decoupled from `BasketBall.instance`, but score-output ownership is still duplicated.
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

Additional evidence (player identifier / game mode audit pass): `Pause.updateFreePlayStats()` (`Assets/Scripts/game manager/Pause.cs:285-308`) independently builds its own `ProgressionService`/`MatchProgressionResult`/result-id (`freePlayProgressionResultId`) with logic nearly identical to `GameRules.ApplyMatchProgressionResult()` (`Assets/Scripts/game manager/GameRules.cs:315-328`, `matchProgressionResultId`) - two independent progression call sites with no shared boundary, confirming this finding rather than introducing a new one.

### AUD-013: Player Identity/Role Flags Have No Single Owner

Evidence:

- `PlayerIdentifier.isCpu`/`isDefensivePlayer` (`Assets/Scripts/player/PlayerIdentifier.cs`) are duplicated as independent fields on `PlayerController.isCPU` and `AutoPlayerController.isCPU`/`isDefensivePlayer`.
- Most gameplay code reads `PlayerIdentifier.isCpu`; the known `BasketBall.cs` and `groundcheck.cs` external reads that used controller-owned duplicate fields have been routed back through `PlayerIdentifier`.
- Each player slot has two separate `PlayerIdentifier` instances (actor and basketball object), manually kept in sync via calls like `_basketball1.setIds(_player1.pid, _player1.pid, _player1.pid, false)` (`Assets/Scripts/game manager/GameLevelManager.cs:413`).
- `bid`/`bsid` fields on `PlayerIdentifier` are always set equal to `pid` at all 17 `setIds` call sites and are never read anywhere - dead fields that look like they should diverge from `pid` but never do.

Why it matters: nothing keeps the duplicated flags in sync. A new spawn/mode path that sets one copy and forgets the other silently splits CPU-detection or defensive behavior between subsystems with no validation, and is a specific case of the broader AUD-003/AUD-004 "no single owner" pattern applied to player identity instead of health.

Recommended next move: make `PlayerIdentifier` the single source of truth; have `PlayerController`/`AutoPlayerController` read from it rather than holding independent copies. Remove or repurpose the dead `bid`/`bsid` fields.

### AUD-014: Player Slot Index Is Implicitly Reused As a Save-Data Key

Evidence:

- Original evidence: `HighScoreModel.convertBasketBallStatsToModel` (`Assets/Scripts/Models/HighScoreModel.cs:172-181`) looped over `PlayerIdentifier` entries and did `if (!p.isCpu) index = p.pid;`, then indexed back into the same list (`pi[index]`, lines 214-274) to build the saved/highscore stat model.
- With more than one non-CPU entry, that loop kept overwriting `index`, so only the last human player's stats were saved.
- `cameraUpdater.cs:101` (`x.pid == 0`) and `GameRules.cs:420-463` (`players[0].pid + 1` for display) make the same implicit assumption that `pid` always equals list position, with nothing enforcing it.

Why it mattered: this was a save/highscore correctness bug, not just an architecture smell - any path that legitimately had more than one human player entry, or any future change that let `pid` and list position diverge, silently recorded the wrong player's stats.

Resolution: `convertBasketBallStatsToModel` now selects a `PlayerIdentifier` object directly through `GetPrimaryPlayer` and uses `TryGetPlayer` for bounded leaderboard slot reads.

### AUD-015: Basketball Shot-Attempt Flags Never Reset on a Miss

Evidence:

- `TwoAttempt`/`ThreeAttempt`/`FourAttempt`/`SevenAttempt` are set `true` in `updateBasketBallStateShotTypeOnShoot` (`Assets/Scripts/basketball/BasketBall.cs:337-360`, duplicated in `BasketBallAuto.cs:371-394`).
- They are only ever reset to `false` inside the make path, `BasketBallShotMade.shotMade`/`updateShotMadeBasketBallStats` (`Assets/Scripts/basketball/BasketBallShotMade.cs:114-117`, `333-336`).
- `BasketBall.OnCollisionEnter` (`BasketBall.cs:184-218`) resets `Locked`/`Thrown`/`CanPullBall` on a miss but never the attempt flags.

Why it matters: a player who misses a 2pt attempt, moves to 3pt range, and makes that shot has both `TwoAttempt` (stale) and `ThreeAttempt` (current) still `true` when `shotMade()` runs, so both categories get scored for the same made shot (`BasketBallShotMade.cs:188-234`). This is a reachable scoring-integrity bug, not a theoretical one.

Recommended next move: reset all shot-type attempt flags at the start of every new attempt (or explicitly on miss detection), not only inside the make path.

### AUD-016: Basketball Shooting Percentages Can Be Computed From the Wrong Player

Evidence:

- Original evidence: `GameRules.GetStatsTotals()` (`Assets/Scripts/game manager/GameRules.cs:955-1003`) took made/attempt counts from `gameStats1` (a specific player's stats, set once at `GameRules.cs:127`) but took percentages from `BasketBall.instance.getXAccuracy()`.
- `BasketBall.instance.gameStats` is reassigned inside `BasketBall.updateScoreText()` (`BasketBall.cs:655`) to `GameLevelManager.instance.players[0].gameStats` - i.e. whichever player's basketball object last ran a score update.
- `GameRules.cs:925-934` shows local-multiplayer branches exist (`numPlayers > 1..3`), so this path is reachable outside 1-player games.

Why it mattered: in local-multiplayer modes, a displayed shooting percentage for one player could silently be computed from a different player's accuracy state while the made/attempt counts next to it were correct - a subtle stats-display bug that would be hard to notice without deliberately comparing numbers.

Resolution: `GetStatsTotals` now computes all percentages from `gameStats1` made/attempt counts via `UtilityFunctions.getPercentageFloat`, not through `BasketBall.instance`.

### AUD-017: `BasketBall` and `BasketBallAuto` Duplicate the Entire Shot Pipeline

Evidence:

- `shootBasketBall`, `Launch`, `updateBasketBallStateShotTypeOnShoot`, `getAccuracyModifier`, `getRangeModifier`, `getReleaseModifier`, `updateScoreText`, and all `getXAccuracy` methods are copy-pasted between `BasketBall.cs` and `BasketBallAuto.cs` (e.g. `BasketBall.cs:532-561` vs `BasketBallAuto.cs:623-652`).
- The two paths have already diverged: `BasketBallAuto.cs:328-348` is missing the money-ball/marker-attempt block present in the human path, and the meter-wait condition differs (`MeterEnded == true` vs `== false`, `BasketBall.cs:493`).
- `Launch()` (`BasketBall.cs:365-483`) itself mixes projectile physics, RNG-based crit/accuracy/range/release rolls, shot-meter UI string building, and an `AnaylticsManager.PlayerShoot` analytics call in one method - the specific coupling point behind the existing AUD-010 finding.

Why it matters: every shot-logic fix, including AUD-015 and AUD-016, has to be applied twice or the human/CPU paths silently diverge further than they already have.

Recommended next move: extract the shared shot pipeline (physics, modifiers, scoring) into one code path parameterized by actor, with only genuine CPU-vs-human differences (input source) kept separate. This deepens AUD-010 rather than replacing it.

### AUD-018: Game Mode Dispatch Is Duplicated With Inconsistent Constants

Evidence:

- `GameRules.SetScoreDisplayText` (`Assets/Scripts/game manager/GameRules.cs:578-839`, ~20 branches) and `GameRules.GetDisplayText` (`GameRules.cs:855-943`, ~20 branches) each implement their own if-chain over the same mode set.
- `SetScoreDisplayText` uses named `Modes.*` constants; `GetDisplayText` uses raw integer literals for the same modes (e.g. `gameModeId == 15 || ... || gameModeId == 24 || gameModeId == 27`, `GameRules.cs:879`) and mixes range checks with literal equality in the same expression.
- Even within `GameRules.Update()`, raw literals `26`/`23` appear at lines 244/248 while the `Modes.FreePlay`/`Modes.BeatThaComputahs` constants are used two lines later at line 266.

Why it matters: adding, renaming, or reordering a mode requires editing two independently-maintained literal representations that can silently drift out of sync, and the mixed literal/constant style makes it easy to miss a spot when they do.

Recommended next move: replace both methods with a single per-mode lookup (data table or strategy) keyed off `Modes` constants only; remove raw integer literals from mode checks entirely.

### AUD-019: Match-End Side Effects Run on a Per-Frame Poll

Evidence:

- **Correction (2026-08-02):** the original version of this finding claimed `GameRules.IsGameOver()` was dead code. That was wrong - it's live, marker-depletion-based win condition logic (`GameRules.cs:1022-1051`, `MarkersRemaining <= 0`) called from `BasketBallShotMarker.cs:125` and `:145` for contest/marker modes. `Timer.Update()`'s time-based path (`Assets/Scripts/game manager/Timer.cs:130-163`) is a separate, legitimate mechanism for timed modes, not a competing/duplicate path for the same modes. There is no evidence these two conflict for any single mode.
- What did still hold: `GameRules.cs` read `GameLevelManager.instance.PlayerHealth.IsDead` directly as a game-over trigger inside a per-frame `Update()` check, which also ran the entire end-of-match flow (pause, persistence, progression application, campaign scene-load) as part of that same per-frame poll rather than firing once.
- `Timer.Update()` also reimplements consecutive-shot-streak logic (`Timer.cs:156-157`) that duplicates flag plumbing already in `GameRules` - worth a look, but not urgent.

Resolution: `GameRules` now has a single match-end entry path. `Timer` and shot-marker depletion call `RequestGameOver()`, player death also raises `RequestGameOver()` through `PlayerHealth.OnDied`, and `HandleMatchEnded()` is protected by re-entry and per-side-effect idempotency guards so pause, persistence, progression, and campaign transition side effects do not duplicate. Progression now applies from primary `GameStats` independently of DB availability. The old polling fallback for `PlayerHealth.IsDead` remains as a compatibility safety net, but it no longer owns the side effects directly.

Recommended next move: add a play-mode smoke test that triggers timer expiry, marker completion, and player death, then asserts the match-end save/progression path runs once per match.

### AUD-020: Traffic Vehicles Mutated Shared Prefab State Before Instantiating

Found and fixed independently of the combat-contracts/match-end work above - confirmed zero references to `GameRules`, `Timer`, `Pause`, `LoadGame`, or `PlayerHealth` anywhere in `Assets/Scripts/vehicle/` before starting.

Evidence:

- `TrafficManager.loadVehiclePrefabs()`/`loadCustomVehiclePrefabs()` (`Assets/Scripts/vehicle/TrafficManager.cs`) populate `VehiclesList` with `VehicleController` components read directly off prefab assets (via `Resources.LoadAll` or the custom prefab list), not runtime clones.
- `spawnVehiclePrefabs()`, `spawnCustomVehiclePrefabs()`, and `spawnVehicleCoRoutine()` all set `Direction`/`FacingRight`/`CurrentTarget` directly on those shared `VehiclesList` entries, then call `Instantiate()` - mutating shared/prefab-sourced state before spawn, the exact pattern AUD-009 already flagged generically. The original developer left a comment acknowledging it: `// need to be spawning from prefabs list. this is saving and changing prefabs value`.
- `spawnVehicleCoRoutine` mutated the shared reference, then `yield return new WaitForSeconds(waitTimeToRespawn)` before instantiating - if the same vehicle ID's coroutine fired twice in quick succession (two vehicles of that type despawning close together), both would mutate the same shared `VehicleController` reference while waiting, and whichever `WaitForSeconds` resolved second would silently overwrite the first's pending `Direction`/`CurrentTarget` before either had instantiated.
- Confirmed via `VehicleController.Start()` that `facingRight` is unconditionally recomputed from `transform.localScale.x` regardless of what's set beforehand - so `FacingRight` assignments in `TrafficManager` were already inert. Left as-is (out of scope, not the bug this pass targets).

Why it matters: a save-data-adjacent-severity bug in spirit to AUD-014/AUD-015 - shared mutable state written by more than one code path with timing that can race, silently producing a vehicle with the wrong direction/target.

Resolution: all three call sites now instantiate first and set `Direction`/`FacingRight`/`CurrentTarget` on the returned clone, never on the shared `VehiclesList`/prefab reference. Grepped the whole codebase first to confirm `VehicleController`/`VehiclesList` have no consumers outside `vehicle/TrafficManager.cs` and `vehicle/VehicleController.cs`, so narrowing this fix to those two files was safe.

### AUD-021: Beat the Computahs Continues Never Reset Between Campaign Attempts

Requested by the user directly: "audit the 'beat the computahs' mode... could probably use better game design." The user confirmed ties are an intentional mechanic, not a bug (not touched). The exact intended continues count is unconfirmed/doesn't matter for this fix - left at 2, the pre-existing default; the bug is the missing reset, not the count.

Evidence:

- `EndRoundData.numberOfContinues` (`Assets/Scripts/menu_start/EndRoundData.cs:11`, was `static public int numberOfContinues = 2;`) is a static field, meaning it persists for the life of the application process, not per campaign attempt.
- The only writes to it: decremented on a loss-with-continues-remaining (`EndRoundMenuManager.cs:240`), and zeroed for hardcore mode (`StartManager.cs:1437`, inside `setGameOptions()` which runs at the start of every game). Nothing reset it back to the default for a fresh non-hardcore run.
- Also traced a related, currently-harmless architecture smell while investigating: `GameRules.LoadNextCampaignLevel()` always optimistically advances `GameOptions.levelSelectedIndex` before the match outcome is known; `EndRoundMenuManager.LoadData()` rolls it back on a tie or a loss-with-continues, but not on a loss with zero continues left. That gap is currently masked only because `StartManager.cs:1273` hard-resets `levelSelectedIndex` to level 1 every time "Beat the Computahs" is freshly started - an incidental safety net, not a designed invariant. Not fixed this pass (no live bug found), but worth documenting as a latent footgun for any future "resume where you left off" feature.

Why it matters: a player who exhausted their continues once had zero continues on every subsequent "Beat the Computahs" attempt for the rest of the session - a real, silent, session-persistent progression bug, not just an architecture smell.

Resolution: extracted `EndRoundData.DefaultContinues = 2` as a named constant (unchanged value - the pre-existing default, not a confirmed design number). `StartManager.setGameOptions()` now does `EndRoundData.numberOfContinues = hardcoreEnabled ? 0 : EndRoundData.DefaultContinues;` on every fresh game start, instead of only ever zeroing it for hardcore mode.

## Recent Mitigations

These issues were reduced by the recent player interaction work, but they should remain visible until durable architecture follow-up is complete.

- Shared combat contracts were introduced with `DamageInfo`, `IDamageable`, `ICombatAgent`, and `CombatReservation`.
- Player, enemy, and bodyguard health now expose a common damageable contract.
- Enemy and bodyguard health now clamp health, guard duplicate death transitions, and publish health/death events.
- Enemy and bodyguard health bars now subscribe to health change events instead of relying only on manual refresh calls.
- Enemy/bodyguard collision damage and enemy lightning damage now route through `ApplyDamage(DamageInfo)` and use the returned death result for cleanup.
- `PlayerAttackQueue` now exposes reservation-oriented APIs, read-only queue state, first-target/slot accessors, and reservation release methods.
- Enemy and bodyguard controllers now implement `ICombatAgent` and release queue reservations on disable/death paths.
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
- Add basketball scoring regression coverage for AUD-014/AUD-015/AUD-016 so these closed fixes stay locked down.
- Reconcile the duplicate progression call sites in `Pause.updateFreePlayStats()` and `GameRules.ApplyMatchProgressionResult()` per AUD-011.
- Decide whether `LevelSelected` (runtime) or `LevelPreset`/`LevelCatalog` (used only by `UnlockService`) should be the single level data model - currently both exist and are kept in sync by hand.
