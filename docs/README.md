# Level5 Documentation

This folder is the baseline documentation home for the Level5 Unity project. It is meant to capture how the game is currently organized, where systems connect, and what architectural direction should guide future changes.

## Current Docs

- [Systems and Architecture Baseline](systems-architecture-baseline.md) - current system inventory, runtime flows, architectural gaps, and recommended target direction.
- [Architecture Audit](architecture-audit.md) - running audit register for main problems, risks, recommended solutions, and remediation status.
- [Architecture Remediation Plan](architecture-remediation-plan.md) - staged implementation plan for clean architecture, Unity best practices, performance, and risk control.
- [Player Input Architecture](player-input-architecture.md) - current input ownership, modernization target, migration plan, and first input-reader slice.

## Documentation Backlog

- Combat system design: damage contracts, attack definitions, hit validation, death flow, and attack queue ownership.
- Player architecture: controller split plan for movement, basketball actions, combat, animation, and presentation.
- Input architecture: finish touch/mobile migration to Input System actions, on-screen controls, and standard UI input.
- AI architecture: enemy/bodyguard state machines, detection, target selection, and action execution.
- Spawn and pooling: enemy, projectile, vehicle, and temporary effect lifecycle rules.
- Scene composition: required managers, prefab contracts, serialized references, and scene bootstrap order.
- Progression and persistence: account identity, local saves, server sync, migration behavior, and failure handling.
- UI architecture: screen ownership, presenter/view boundaries, and gameplay event subscriptions.
- Test strategy: edit-mode coverage, play-mode smoke tests, prefab validation, and combat regression scenarios.
