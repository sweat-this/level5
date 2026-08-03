# Architecture Remediation Plan

Last updated: 2026-08-02

This plan turns the architecture audit into staged implementation work. The goal is not to rewrite the game. The goal is to make the existing Unity project safer to extend by improving ownership, contracts, runtime lifecycle, performance discipline, and testability one vertical slice at a time.

## Goals

- Make gameplay systems easier to reason about by giving each state one owner.
- Reduce hidden scene dependencies and global/static coupling.
- Standardize combat, health, death, attack reservations, and projectile/spawn lifecycle.
- Keep behavior stable while gradually extracting smaller components from large scripts.
- Apply Unity 3D best practices around physics, serialization, pooling, Update usage, allocation, and prefab contracts.
- Add verification gates so every architecture change proves it did not break gameplay.

## Non-Goals

- Do not rewrite the whole game in one pass.
- Do not move large folder trees before behavior is covered by tests or prefab checks.
- Do not replace working gameplay just to satisfy a pattern.
- Do not introduce dependency injection frameworks or heavy abstractions unless the local code proves the need.
- Do not break existing public methods until callers have migrated.

## Architecture Principles

- One owner per state: health owns health/death, queue owns reservations, game rules own match rules, UI owns rendering only.
- Events over polling for state changes: UI, audio, analytics, and scoring should subscribe to gameplay outcomes.
- Serialized references over scene searches: prefer inspector-assigned fields, prefab references, or scene context references.
- Interfaces at system boundaries: use small contracts such as `IDamageable`, `ICombatAgent`, and future `IAttackSource`.
- Composition over large controllers: split behavior into motor, decision, combat, presentation, and data components.
- ScriptableObjects for authored data: attacks, character stats, level rules, mode rules, spawn tables, and tuning values.
- Pool high-churn objects: projectiles first, then enemies, vehicles, pickups, temporary effects, and attack volumes.
- Keep animation events thin: they may signal timing, but gameplay code validates state and owns cleanup.
- Use Unity physics correctly: movement that affects `Rigidbody` should happen through Rigidbody APIs and `FixedUpdate`.
- Optimize after ownership is clear: first remove avoidable allocation/search hot paths, then profile before deeper tuning.

## Problem And Solution Review

| Audit ID | Problem | Current Risk | Plan | Solution Quality Check |
| --- | --- | --- | --- | --- |
| AUD-001 | Combat state ownership is split. | Hits and deaths can leave stale UI, animation, queue, or AI state. | Build shared combat contracts and route one melee path through them. | A damage event has one source, one receiver, one death transition, and observable events. |
| AUD-002 | `PlayerController` is too broad. | Changes to movement, attack, UI, sniper, camera, or basketball can affect each other. | Extract behavior behind the existing facade in small vertical slices. | No external caller should need to know whether behavior still lives in `PlayerController`. |
| AUD-003 | Global/static dependencies hide scene contracts. | Scene load order, prefab reuse, and tests are fragile. | Document scene contracts, then add `LevelRuntimeContext` as a typed reference surface. | New code receives references explicitly and does not add new broad singleton reads. |
| AUD-004 | Actor health is duplicated. | Player, enemy, and bodyguard death rules can diverge. | Continue converging health through `IDamageable`, events, clamps, and death guards. | All actor health changes emit consistent events and reject repeat damage after death. |
| AUD-005 | Attack queue is actor-specific. | New melee actor types require queue edits and can bypass invariants. | Convert to actor-agnostic reservations while keeping legacy wrappers. | Queue internals are not publicly mutable; reservations release on disable/death/target loss. |
| AUD-006 | Animation events own gameplay timing. | Clip edits can break attacks, projectiles, and cleanup. | Move attack window authority into combat drivers; animation events call safe adapters. | Missing animation event should not leave an attack box, projectile, or lock state stuck. |
| AUD-007 | AI state is implicit. | Boolean combinations create fragile behavior transitions. | Add finite state machines after queue contracts are stable. | Each AI state has explicit enter/update/exit and allowed transitions. |
| AUD-008 | UI and gameplay are coupled. | UI changes can alter gameplay behavior. | Add presenters that subscribe to gameplay/progression events. | Gameplay can run without UI objects present where possible. |
| AUD-009 | Spawn lifecycle lacks common pooling. | Instantiate/destroy churn and prefab mutation create performance and reset risks. | Define pooled reset contracts and convert high-churn systems incrementally. | Pooled objects fully reset state on spawn/release and do not mutate prefab defaults. |
| AUD-010 | Basketball flow mixes responsibilities. | Shot scoring, UI, stats, analytics, and progression can drift. | Document shot lifecycle and introduce `ShotResult`. | One result object drives stats, UI, analytics, audio, progression, and rules. |
| AUD-011 | Persistence ownership is unclear. | Save timing, offline behavior, and migration risk are hard to reason about. | Document source-of-truth and route match results through one service boundary. | Pause/end-round UI calls a service; persistence can be tested without UI. |
| AUD-012 | Legacy/dev/test code is mixed with runtime. | Old or diagnostic scripts can become accidental dependencies. | Classify code before moving it; then separate runtime, editor, dev, tests, and legacy. | Each script has a clear owner category and no production scene depends on dev-only scripts. |
| AUD-013 | Player identity/role flags are duplicated with no single owner. | A spawn/mode path can set one copy and forget the other. | Make `PlayerIdentifier` the single source of truth for identity/role flags. | Controllers read identity/role from `PlayerIdentifier` instead of holding independent copies. |
| AUD-014 | Player slot index is implicitly reused as a save-data key. | Wrong player's stats can be silently saved/highscored. | Pass an explicit player reference or deterministic index into highscore conversion. | Code fix in place: `GetPrimaryPlayer`/`TryGetPlayer` helpers replace the overwritable-index loop and add null/bounds checks. Unity verification pending. |
| AUD-015 | Basketball shot-attempt flags never reset on a miss. | Miss-then-make across categories double-scores a single shot. | Reset attempt flags at the start of every new attempt. | Code fix in place: shot snapshots reset on each human/CPU attempt. Unity verification pending. |
| AUD-016 | Basketball percentages can read a different player's accuracy than the counts shown. | Local-multiplayer stat displays can silently mismatch. | Compute percentages from the same per-player `GameStats` instance as the counts. | Code fix in place: percentages use `UtilityFunctions.getPercentageFloat` with `gameStats1` counts. Unity verification pending. |
| AUD-017 | `BasketBall`/`BasketBallAuto` duplicate the entire shot pipeline. | Every shot-logic fix must be applied twice or paths diverge further. | Extract one shared shot pipeline parameterized by actor. | Human and CPU shots share one implementation with only input source differing. |
| AUD-018 | Game mode dispatch is duplicated across two if-chains. Fixed 2026-08-02: both now consistently use `Modes` constants instead of a mix of names and raw literals, so they can no longer silently drift by number. Still two separate if-chains, not one lookup. | Lower now that the constant-usage inconsistency is fixed; a full single-lookup consolidation remains future work, deferred as too risky to do without test coverage across ~20 modes in one pass. | (Optional, not yet done) Replace both dispatch methods with one per-mode lookup keyed off `Modes`. | A new mode requires exactly one dispatch edit, not two independently-styled ones. |
| AUD-019 | **Corrected 2026-08-02**: `GameRules.IsGameOver()` is live marker-based win-condition logic (not dead, as this doc originally claimed), called from `BasketBallShotMarker.cs`. It and `Timer`'s time-based path serve different modes, not competing paths for the same mode. The real, narrower finding: end-of-match side effects (persistence, progression, scene load) ran inside a per-frame `Update()` poll rather than firing once. | Per-frame polling of end-of-match side effects risked re-entrancy/duplicate execution if the condition held across multiple frames. | Code fix in place: route timer, marker, and death triggers through `GameRules.RequestGameOver()` and gate side effects behind `matchEndHandled`/`HandleMatchEnded()`. | Match-end side effects run exactly once per match, even if the trigger condition stays true across frames. Unity verification pending. |

## Recommended Sequence

### Phase 0: Safety Rails

Status: In Progress

Work:

- Keep the audit and remediation plan current.
- Build after each behavior slice with `dotnet build Level5.sln`.
- Avoid large file moves until Unity asset references and prefab contracts are understood.
- Leave compatibility wrappers when migrating old method names.

Done when:

- Every architecture slice updates the audit status.
- Every slice has a build or a clear reason why build was not applicable.

### Phase 1: Combat Contracts And Health

Status: In Progress

Work:

- Finish converging health behavior around `IDamageable` and `DamageInfo`.
- Add an `AttackDefinition` ScriptableObject for authored attack tuning.
- Add a small `IAttackSource` or equivalent if repeated attack code needs a source contract.
- Route enemy/bodyguard collision damage through `ApplyDamage(DamageInfo)` instead of direct `Health -=`. Done for enemy/bodyguard collision damage and enemy lightning damage.
- Make health bars and damage displays event-driven.

Unity practices:

- Keep health components as `MonoBehaviour` components because they are prefab-owned actor state.
- Use serialized max values for prefab defaults, but runtime state should be initialized and clamped in code.
- Avoid direct UI references inside collision logic.

Risks:

- Direct property setters are still used by old callers.
- Death side effects still live in collision/controller scripts.

Mitigation:

- Keep old properties for compatibility, but migrate callers gradually to methods/events.

Acceptance criteria:

- Player, enemy, and bodyguard damage paths use the same damage entry point.
- Death is idempotent across all actor types.
- Health UI updates from events.
- Edit-mode tests cover damage, heal, clamp, death, and duplicate death events.

### Phase 2: Attack Queue And Combat Reservations

Status: In Progress

Problem review:

- `PlayerAttackQueue` has a useful internal reservation model, but callers still interact with concrete `GameObject`, `EnemyDetection`, `BodyGuardDetection`, and mutable lists.
- Enemy/bodyguard controllers read `EnemiesQueued`, `BodyGuards`, and `AttackPositions` directly, which lets queue invariants leak outside the owner.
- Queue release happens in several death paths, but there is not yet a single reservation release API for death, disable, target loss, and cleanup.

Solution plan:

- Add `TryReserve` and `ReleaseReservation` APIs that return or consume `CombatReservation`. Done for the current queue.
- Keep legacy `TryAddToQueue`, `RemoveFromQueue`, and `removeEnemyFromQueue` wrappers while callers migrate.
- Make enemy/bodyguard controllers implement `ICombatAgent`. Done for current controllers.
- Expose read-only queue state plus intention-revealing accessors such as first queued enemy, first bodyguard, and attack slot transform lookup. Done for current queue callers.
- Add cleanup for disabled/dead attackers in the queue and release reservations from actor disable paths.

Work:

- Add reservation-oriented APIs to `PlayerAttackQueue`.
- Make enemy/bodyguard controllers or adapters implement `ICombatAgent`.
- Return `CombatReservation` from successful slot claims.
- Make queue lists read-only from outside the queue.
- Release reservations on death, disable, target loss, queue clear, and attack completion.
- Keep `TryAddToQueue(GameObject)` and `RemoveFromQueue(GameObject, int)` as wrappers during migration.

Unity practices:

- Avoid per-frame scene searches for slots or bodyguards.
- Cache slot components once, validate missing references in `Awake`/`Start`, and fail gracefully.
- Prefer component references over tag-driven behavior for new code.

Risks:

- Enemy/bodyguard AI reads queue internals directly today.
- Changing queue ownership can alter melee pacing.

Mitigation:

- Add compatibility accessors.
- Preserve current slot ordering and max attacker behavior first.
- Add tests for reserve, duplicate reserve, release, disable cleanup, and full queue.

Acceptance criteria:

- New code can reserve without knowing enemy/bodyguard concrete classes.
- Queue internals cannot be mutated from arbitrary scripts.
- No attacker remains queued after disable or death.

### Phase 3: Animation Event Authority

Status: Planned

Work:

- Create combat drivers that own attack windows and projectile firing authority.
- Change animation event methods to call safe driver methods.
- Add forced cleanup when actor state exits, actor dies, object disables, or animation is interrupted.
- Document required animation event names and fallback behavior.

Unity practices:

- Animation events should not be the only cleanup path.
- Never rely on clip timing alone for gameplay validity.
- Keep hitboxes disabled by default on prefab load/spawn.

Risks:

- Existing clips may rely on exact method names.

Mitigation:

- Preserve public animation event methods as wrappers.
- Add logs for missing required references during development builds.

Acceptance criteria:

- Attack boxes cannot remain enabled after death/disable/interruption.
- Projectiles do not spawn if the actor can no longer act.
- Animation event methods are thin adapters.

### Phase 4: PlayerController Decomposition

Status: Planned

Work:

- Extract a `PlayerInputAdapter` that reads input and exposes intent.
- Extract a `PlayerMotor` for Rigidbody movement and facing.
- Extract a `PlayerCombatDriver` for attack requests and combat state.
- Extract a `PlayerBasketballDriver` for ball possession and shot requests.
- Keep `PlayerController` as a facade until callers migrate.

Unity practices:

- Rigidbody movement belongs in `FixedUpdate` through Rigidbody APIs.
- Input should be read in `Update` and cached as intent for physics.
- Avoid mixing UI writes, physics, animation, and gameplay decisions in one method.

Risks:

- `PlayerController` has many external callers and serialized fields.

Mitigation:

- Extract without changing public surface first.
- Move one behavior at a time.
- Use prefab validation and play-mode smoke tests.

Acceptance criteria:

- Movement, combat, and basketball actions can be reasoned about independently.
- `PlayerController` becomes orchestration/facade rather than business logic owner.

### Phase 5: AI State Machines

Status: Planned

Work:

- Define explicit states for enemy/bodyguard behavior.
- Separate detection, target selection, movement, reservation, attack, recovery, and death.
- Reuse combat reservation and damage contracts.
- Add tuning data for ranges, cooldowns, aggression, and movement.

Unity practices:

- Keep state transitions explicit and debuggable.
- Avoid expensive scene searches in per-frame AI updates.
- Use NavMesh/physics APIs intentionally based on actor movement style.

Risks:

- AI behavior is feel-sensitive and may change game difficulty.

Mitigation:

- Start with one enemy type.
- Preserve current timing values.
- Compare before/after behavior in a focused play-mode scene.

Acceptance criteria:

- Each AI state has clear enter/update/exit logic.
- Attack reservations and releases are state-owned.
- Adding a new AI type does not require changing queue internals.

### Phase 6: Runtime Context And Scene Contracts

Status: Planned

Work:

- Document required scene objects for each game mode.
- Add `LevelRuntimeContext` with typed references to managers, player, cameras, UI anchors, spawn roots, and services.
- Migrate new code away from `GameObject.Find` and broad singleton reads.
- Add scene validation helpers for missing required references.

Unity practices:

- Prefer serialized scene references and prefab references over name/tag lookup.
- Validate references early and fail with actionable logs.
- Avoid static mutable state for scene-specific data.

Risks:

- Existing scenes may rely on different object names or optional managers.

Mitigation:

- Start with documentation and validation warnings.
- Migrate one scene/mode at a time.

Acceptance criteria:

- New systems receive dependencies explicitly.
- Scene requirements are documented and validated.
- `GameObject.Find` usage stops increasing.

### Phase 7: Pooling And Object Lifecycle

Status: Planned

Work:

- Document projectile pool reset contract.
- Introduce a generic pooled object interface if needed.
- Convert the next high-churn system: enemies or vehicles.
- Ensure reset handles health, AI state, Rigidbody velocity, colliders, particles, audio, animation state, timers, and queued reservations.

Unity practices:

- Avoid frequent `Instantiate`/`Destroy` during active gameplay.
- Reset pooled objects completely on spawn/release.
- Do not mutate prefab runtime state before instantiation.

Risks:

- Pooled actors can keep stale references or old state.

Mitigation:

- Add explicit `OnSpawnedFromPool` and `OnReleasedToPool` hooks.
- Test repeated spawn/kill/release cycles.

Acceptance criteria:

- Projectiles have documented reset rules.
- One non-projectile actor category uses pooling safely.
- Runtime allocation spikes are reduced in busy gameplay.

### Phase 8: Basketball Result Flow

Status: Planned

Work:

- Document shot lifecycle from input to launch to make/miss to stats/progression.
- Add a `ShotResult` value object.
- Route stats, UI, audio, analytics, progression, and game rules from the result.
- Keep physics and shot math isolated from UI formatting.

Unity practices:

- Keep physics interactions separate from UI rendering.
- Use events for shot outcome consumers.
- Avoid repeated string formatting in hot gameplay paths unless UI is actually visible.

Risks:

- Basketball is a core feel system and scoring must not drift.

Mitigation:

- Snapshot current scoring rules before refactor.
- Add regression tests for scoring categories.

Acceptance criteria:

- One shot result drives all consumers.
- UI score formatting is outside shot execution.
- Stats and progression receive the same authoritative outcome.

### Phase 9: Persistence Boundary

Status: Planned

Work:

- Document account identity, local save, server sync, progression, and migration ownership.
- Move match result save/apply behavior behind a single service boundary.
- Make pause/end-round UI invoke services instead of owning persistence steps.
- Define offline and retry behavior.

Unity practices:

- Keep network/database work away from gameplay frame-critical code.
- Avoid UI managers owning durable data rules.
- Make persistence idempotent where possible.

Risks:

- Save behavior is player-trust-critical.

Mitigation:

- Keep existing persistence paths until the service is proven.
- Use result IDs to prevent duplicate progression application.

Acceptance criteria:

- Persistence can be tested without menu UI.
- Source-of-truth rules are documented.
- Match result application is idempotent.

## Immediate Implementation Backlog

1. Add edit-mode tests for `PlayerHealth`, `EnemyHealth`, and `BodyGuardHealth`.
2. Replace direct enemy/bodyguard `Health -=` collision calls with `ApplyDamage(new DamageInfo(...))`. Done for collision damage and enemy lightning damage.
3. Add `AttackDefinition` as a ScriptableObject for authored attack tuning.
4. Add reservation-oriented methods to `PlayerAttackQueue`. Done for current queue.
5. Add `ICombatAgent` adapters to enemy/bodyguard controllers. Done for current controllers.
6. Make queue internals read-only and preserve legacy wrappers. Done for current direct callers.
7. Add queue cleanup on actor disable/death. Done for enemy/bodyguard controllers; still needs edit/play-mode tests.
8. Document animation event methods and classify critical gameplay callbacks.

## Immediate Implementation Backlog (Player Identity / Basketball / Game Mode / Vehicles)

A separate, independent track from the combat contracts and match-end-flow work - none of these touch `PlayerController`, health, the attack queue, animation events, `GameRules`, `Timer`, `Pause`, or `LoadGame`, so they can proceed in parallel without conflict.

1. **Code fix in place 2026-08-02; verification pending.** Fix AUD-015: added `BasketballState.ResetShotAttemptSnapshot()` (clears `TwoAttempt`/`ThreeAttempt`/`FourAttempt`/`SevenAttempt` plus `MoneyBallEnabledOnShoot`/`PlayerOnMarkerOnShoot`/`OnShootShotMarkerId`), called at the start of every new attempt in both `BasketBall.cs` and `BasketBallAuto.cs`, not only on a make.
2. **Code fix in place 2026-08-02; verification pending.** Fix AUD-014: `HighScoreModel.convertBasketBallStatsToModel` now uses a `GetPrimaryPlayer` helper (first non-CPU player with valid stats, falling back to the first player with any stats) instead of the loop that silently overwrote `index` for every human player, plus a `TryGetPlayer(index)` helper that null/bounds-checks the `p1`-`p4` breakdown reads.
3. **Code fix in place 2026-08-02; verification pending.** Fix AUD-016: `GameRules.GetStatsTotals()` now computes percentages via `UtilityFunctions.getPercentageFloat(gameStats1.Made, gameStats1.Attempts)` - the same `gameStats1` source as the counts - instead of the old `getXPointAccuracy()` methods that read `BasketBall.instance`'s own reassignable `gameStats` field.
4. **Done 2026-08-02 (partial).** `BasketBall.cs`, `groundcheck.cs`, and `AutoPlayerController.cs` now read `.isCpu`/`.isDefensivePlayer` through `PlayerIdentifier` instead of controller duplicate fields where runtime behavior depends on identity/role (AUD-013). The duplicate fields are left in place for serialization safety, but are commented as non-authoritative. The two-`PlayerIdentifier`-instances-per-slot structural issue and the dead `bid`/`bsid` fields are still open.
5. **Deferred.** Extract the shared basketball shot pipeline from `BasketBall`/`BasketBallAuto` into one actor-parameterized implementation (AUD-017). Assessed as too large/risky to do safely in the same pass as the smaller fixes above - this project has no automated test coverage for basketball scoring, and "Basketball is a core feel system and scoring must not drift" (Phase 8 risk note above) applies directly. Needs its own focused pass, ideally with manual playtesting of both human and CPU shooting before/after.
6. **Done 2026-08-02 (partial).** Fix AUD-018: replaced raw integer literals with `Modes` constants in both `GameRules.GetDisplayText()`/`SetScoreDisplayText()`-adjacent dispatch and in `GameRules.Update()` (previously raw `26`/`23`), so the two dispatch methods can no longer silently drift by number. Did **not** collapse them into a single lookup table - same reasoning as item 5, too risky to restructure ~40 branches across two methods without test coverage.
7. **Code fix in place 2026-08-02; verification pending.** AUD-019's original premise (a dead `GameRules.IsGameOver()` competing with a live `Timer` path) was factually wrong - `IsGameOver()` is live, marker-based win-condition logic for contest modes, and doesn't conflict with `Timer`'s time-based path for timed modes. The narrower, still-valid point was fixed by routing timer, marker, and death triggers through `GameRules.RequestGameOver()` and gating pause, persistence, progression, and campaign transition side effects behind `matchEndHandled`/`HandleMatchEnded()`.
8. **Not done.** Reconcile the duplicate progression call sites in `Pause.updateFreePlayStats()` and `GameRules.ApplyMatchProgressionResult()` (AUD-011).
9. **Done 2026-08-02.** Fix AUD-020: `TrafficManager`'s `spawnVehiclePrefabs`, `spawnCustomVehiclePrefabs`, and `spawnVehicleCoRoutine` now instantiate first and set `Direction`/`FacingRight`/`CurrentTarget` on the returned clone, instead of mutating the shared prefab-sourced `VehicleController` list entries before `Instantiate()` - closes a same-vehicle-ID respawn race. Confirmed via full-codebase grep that `VehicleController`/`VehiclesList` have no consumers outside `vehicle/TrafficManager.cs` and `vehicle/VehicleController.cs` before making the change.

## Verification Gates

Every implementation slice should pass the relevant gates:

- Build: `dotnet build Level5.sln`.
- Static check: `git diff --check`.
- Manual smoke check in Unity for touched gameplay scenes when behavior changes.
- Edit-mode tests for pure or component-level contracts.
- Play-mode smoke tests for scene bootstrap and interaction flows.
- Audit update with status, mitigation, and next risk.

## Clean Code Rules For This Project

- Keep public wrappers temporarily when existing scenes/scripts call them.
- Prefer small, intention-revealing methods over boolean-heavy procedural blocks.
- Do not add new static mutable state unless it is truly application-wide.
- Do not expose mutable collections from owner systems.
- Keep UI text formatting out of gameplay decision code.
- Keep comments for why, not what.
- Prefer serialized fields with validation over runtime string lookups.
- Keep refactors vertical: one behavior path should work end-to-end before broad cleanup.

## Performance Rules For Unity 3D

- Do not add per-frame `GameObject.Find`, tag scans, LINQ allocations, or uncached component lookups in gameplay loops.
- Cache required components in `Awake`/`Start` and validate null references.
- Use pooling for repeated runtime objects.
- Avoid allocating strings or temporary collections in hot `Update`, `FixedUpdate`, collision, and trigger paths.
- Use `FixedUpdate` for physics movement and `Update` for input intent capture.
- Disable hitboxes and colliders when not active.
- Prefer layer masks and collision matrices over broad tag checks as systems mature.
- Profile before deep optimization; remove obvious allocation/search hot paths first.

## Decision Record

| Decision | Reason |
| --- | --- |
| Start with combat contracts instead of `PlayerController` split. | Contracts reduce cross-system risk and give later refactors a stable target. |
| Keep compatibility wrappers during migration. | Unity scenes, animation events, and serialized references can call old names. |
| Refactor by vertical slice. | Gameplay feel depends on interactions between input, animation, physics, UI, and rules. |
| Update docs with each slice. | The audit should remain a living system map, not a stale snapshot. |
| Make attack queue ownership explicit before AI refactors. | AI state machines need a stable reservation contract before behavior is reorganized. |
