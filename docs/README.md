# Level5 Documentation

This folder is the baseline documentation home for the Level5 Unity project. It is meant to capture how the game is currently organized, where systems connect, and what architectural direction should guide future changes.

## Current Docs

- [Systems and Architecture Baseline](systems-architecture-baseline.md) - current system inventory, runtime flows, architectural gaps, and recommended target direction.

## Documentation Backlog

- Combat system design: damage contracts, attack definitions, hit validation, death flow, and attack queue ownership.
- Player architecture: controller split plan for movement, basketball actions, combat, animation, and presentation.
- AI architecture: enemy/bodyguard state machines, detection, target selection, and action execution.
- Spawn and pooling: enemy, projectile, vehicle, and temporary effect lifecycle rules.
- Scene composition: required managers, prefab contracts, serialized references, and scene bootstrap order.
- Progression and persistence: account identity, local saves, server sync, migration behavior, and failure handling.
- UI architecture: screen ownership, presenter/view boundaries, and gameplay event subscriptions.
- Test strategy: edit-mode coverage, play-mode smoke tests, prefab validation, and combat regression scenarios.
