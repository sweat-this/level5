# Unity Repository Architect

Use this workflow before major Level 5 systems, architectural refactors, ownership changes, or migrations.

## Goal

Design the smallest durable architecture that fits the repository's current state and approved requirements.

## Required Inspection

Before proposing architecture:

1. Inspect all directly affected scripts and their important call sites.
2. Inspect relevant scenes, prefabs, ScriptableObjects, serialized references, tests, and settings.
3. Read `docs/systems-architecture-baseline.md` and relevant audit/remediation sections.
4. Read feature-specific architecture docs for the touched area.
5. Trace the current runtime flow end to end.
6. Identify current state owners, compatibility bridges, globals/singletons, and duplicated state.
7. Check current `dev` before treating an audit item or issue as unresolved.

## Architecture Principles

Prefer:

- one owner for each piece of gameplay state;
- explicit dependencies;
- incremental extraction from legacy managers;
- event/result boundaries between gameplay and UI/presentation;
- plain C# logic where it provides meaningful testability;
- ScriptableObjects for authored data rather than runtime mutable truth;
- composition over inheritance;
- compatibility adapters during staged migration;
- stable IDs/contracts across persistence and versus boundaries.

Do not:

- rewrite working systems solely for architectural purity;
- introduce a DI framework, event bus, ECS, service locator, or other framework without demonstrated need;
- replace concrete duplication with abstraction before the duplicated behavior is understood;
- expand a current refactor to every similar system automatically;
- make unrelated scene/prefab changes;
- assume documented target architecture has already been implemented.

## Level 5 Risk Review

Explicitly check whether the proposal affects:

- basketball shot/scoring lifecycle;
- human versus CPU behavior parity;
- match start/end and game-mode dispatch;
- player identity and local multiplayer;
- persistence/account authority;
- versus/correspondence ruleset compatibility;
- input ownership;
- animation-event-driven gameplay;
- scene initialization/order;
- serialized references and prefab compatibility.

## Output

Return:

1. **Current architecture** — evidence-based ownership and flow.
2. **Problems** — concrete defects or constraints, not generic smells.
3. **Preserved behavior** — behavior that must not change.
4. **Target architecture** — smallest complete design.
5. **Migration strategy** — ordered, reversible slices.
6. **Compatibility strategy** — old call sites/data/serialized references that need bridges.
7. **Risks and mitigations**.
8. **Tests and Play Mode validation**.
9. **Deferred work** — explicitly outside scope.

If a material requirement is unresolved, stop before implementation and identify the decision needed.
