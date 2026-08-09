# Level5 Documentation

This folder is the baseline documentation home for the Level5 Unity project. It is meant to capture how the game is currently organized, where systems connect, and what architectural direction should guide future changes.

## Current Docs

- [Systems and Architecture Baseline](systems-architecture-baseline.md) - current system inventory, runtime flows, architectural gaps, and recommended target direction.
- [Architecture Audit](architecture-audit.md) - running audit register for main problems, risks, recommended solutions, and remediation status.
- [Deep Audit 2026-08-06](deep-audit-2026-08-06.md) - five runtime passes covering progression/XP math, stat ownership, AI instance state, persistence locking, and lifecycle hygiene. AUD-022 to AUD-045 fixed, pending Unity compile/playtest verification.
- [Deep Audit 2026-08-09](deep-audit-2026-08-09.md) - credential security, Unity null semantics, singleton teardown, per-frame allocation, device assumptions, build contents, and assembly structure. AUD-057 to AUD-063, all fixed. Includes what was checked and found clean, and the first play-mode coverage of runtime gameplay code.
- [Architecture Remediation Plan](architecture-remediation-plan.md) - staged implementation plan for clean architecture, Unity best practices, performance, and risk control.
- [Persistence and Account Identity](persistence-boundaries.md) - the three stores (SQLite, per-account JSON, server), which is authoritative, how identity differs from a session, retry/queue behaviour, and the client/server trust boundary.
- [Versus and Correspondence Multiplayer](versus-architecture.md) - competitive rulesets, attempt lifecycle, game resolution, best-of-N series, sealed attempts and open targets, ruleset versioning, persistence, the future networking boundary, and how to add a versus-compatible mode.
- [Versus Implementation Plan](versus-correspondence-plan.md) - the decision record behind the above: audit findings, target architecture, and the two review passes.
- [Player Input Architecture](player-input-architecture.md) - current input ownership, modernization target, migration plan, and first input-reader slice.
- [UI Input Architecture](ui-input-architecture.md) - menu input baseline, UI module target, EndRound pilot, and mobile/desktop smoke checks.

## Documentation Backlog

- Combat system design: damage contracts, attack definitions, hit validation, death flow, and attack queue ownership.
- Player architecture: controller split plan for movement, basketball actions, combat, animation, and presentation.
- Input architecture: finish touch/mobile migration to Input System actions, on-screen controls, and standard UI input.
- AI architecture: enemy/bodyguard state machines, detection, target selection, and action execution.
- Spawn and pooling: enemy, projectile, vehicle, and temporary effect lifecycle rules.
- Scene composition: required managers, prefab contracts, serialized references, and scene bootstrap order.
- UI architecture: screen ownership, presenter/view boundaries, and gameplay event subscriptions.
- Test strategy: edit-mode coverage, play-mode smoke tests, prefab validation, and combat regression scenarios.
