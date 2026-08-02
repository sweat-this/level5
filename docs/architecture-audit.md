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
| AUD-001 | Combat state ownership | High | Open | Combat state is split across player controller, collision handlers, health scripts, animation events, UI, and the attack queue. | Changes in one script can leave another script with stale state, causing death, attack, UI, or collision bugs. | Define shared combat contracts such as `IDamageable`, `DamageInfo`, health/death events, and attack definitions. Move ownership of health/death to health components and let UI/presentation subscribe. |
| AUD-002 | Player controller scope | High | Open | `PlayerController` owns too many responsibilities, including movement, input handling, basketball actions, combat decisions, animation coordination, and state changes. | The player system becomes hard to test, risky to change, and difficult to reuse across modes. | Split player behavior into focused components: movement motor, combat driver, basketball driver, input adapter, animation bridge, and state facade. |
| AUD-003 | Global/static dependencies | High | Open | Broad use of global managers and static option/state holders creates implicit dependencies. | Systems depend on scene load order and hidden global state, making tests, prefab reuse, and mode-specific behavior more fragile. | Introduce an explicit scene-owned runtime context for gameplay services. Keep globals temporarily as compatibility bridges while new systems migrate to explicit references. |
| AUD-004 | Duplicated actor health | High | Open | Player, enemy, and bodyguard health/death behavior is implemented separately. | Damage, invulnerability, UI updates, death cleanup, and scoring reactions can diverge between actor types. | Create a reusable health component or common health service, then use actor-specific adapters only where behavior truly differs. |
| AUD-005 | Attack queue coupling | Medium | Mitigated | The attack queue now handles reservations more safely, but it still knows about concrete enemy/bodyguard behavior patterns. | Adding new melee actor types will require queue edits and increases the chance of leaked reservations. | Evolve `PlayerAttackQueue` into an actor-agnostic `CombatSlotReservationSystem` using an `ICombatAgent` contract. |
| AUD-006 | Animation event authority | Medium | Mitigated | Animation events still participate in important gameplay timing and cleanup. | Renamed, missing, or mistimed animation events can break attacks, projectile firing, recovery, or state reset. | Treat animation events as presentation/timing hints. Gameplay code should validate state, provide fallbacks, and own cleanup on disable/death/interrupt. |
| AUD-007 | AI state complexity | Medium | Open | Enemy, bodyguard, CPU player, and special NPC behavior rely on local booleans and bespoke checks. | Behavior changes become fragile because state transitions are implicit and scattered. | Move toward explicit finite state machines with states such as idle, patrol, pursue, reserve slot, attack, recover, stunned, and dead. |
| AUD-008 | UI/gameplay coupling | Medium | Open | Several UI flows share ownership of gameplay or progression state instead of only rendering it. | UI changes can accidentally change gameplay behavior, and gameplay changes can break menu/result presentation. | Use presenters and events. Gameplay emits state/result events; UI subscribes, formats, and renders. |
| AUD-009 | Spawn/instantiate lifecycle | Medium | Open | Projectiles have pooling, but enemies, vehicles, pickups, effects, and other high-churn objects do not appear to share a standard lifecycle. | Runtime allocation spikes, stale references, and reset bugs become more likely as scenes get busier. | Expand pooling patterns and document reset contracts for each pooled prefab category. |
| AUD-010 | Basketball flow ownership | Medium | Open | Basketball shot state, scoring, range/shot meters, stats, game rules, and player actions are spread across multiple systems. | Makes/misses, score updates, UI feedback, and progression stats can drift if the flow changes. | Document the shot lifecycle and introduce a single shot result event consumed by UI, stats, audio, progression, and game rules. |
| AUD-011 | Persistence boundaries | Medium | Open | Account, local save, server messages, database/API calls, progression, and migration systems need clearer source-of-truth documentation. | Offline behavior, retry handling, migration safety, and conflict resolution can become unclear. | Document local-vs-remote ownership, failure handling, retry behavior, migration rules, and user/account identity flow. |
| AUD-012 | Legacy/dev/test separation | Low | Open | Production scripts, test scripts, diagnostics, original managers, and utility helpers are mixed in the runtime tree. | Old or diagnostic code may be accidentally referenced by production systems. | Tag or move code into clear `Runtime`, `Editor`, `Dev`, `Legacy`, and `Tests` ownership areas over time. |

## Recent Mitigations

These issues were reduced by the recent player interaction work, but they should remain visible until durable architecture follow-up is complete.

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
