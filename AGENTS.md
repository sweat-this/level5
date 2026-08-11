# Level 5 Agent Instructions

## Mission

Level 5 is a Unity basketball/action game with multiple gameplay modes, combat, progression, persistence, local/CPU play, and versus/correspondence systems.

The project is actively being modernized from a scene-driven architecture with broad managers and legacy coupling. Changes should improve correctness and ownership incrementally without destabilizing working gameplay or rewriting the project for architectural purity.

## Source of Truth

Before changing implementation, inspect the current repository state on the branch being modified. Repository evidence overrides assumptions.

Start with the documentation index:

- [Level5 documentation](docs/README.md)
- [Systems and architecture baseline](docs/systems-architecture-baseline.md)
- [Architecture audit](docs/architecture-audit.md)
- [Architecture remediation plan](docs/architecture-remediation-plan.md)

Read feature-specific documents when relevant, especially:

- [Basketball shot lifecycle](docs/shot-lifecycle.md)
- [Persistence boundaries](docs/persistence-boundaries.md)
- [Player input architecture](docs/player-input-architecture.md)
- [UI input architecture](docs/ui-input-architecture.md)
- [Versus architecture](docs/versus-architecture.md)
- [Player select architecture overhaul](docs/player-select-architecture-overhaul-plan.md)

Do not silently replace documented behavior with generic Unity advice. If code and documentation disagree, identify the discrepancy and establish current runtime behavior before changing it.

## Branching

- `dev` is the repository's current default/integration branch.
- Normal implementation work should start from current `dev` and target `dev` through a pull request unless the task explicitly requires another branch.
- Do not assume an issue is unimplemented. Inspect `dev`, relevant commits/PRs, scenes, prefabs, tests, and docs first.
- Do not mix unrelated cleanup into feature or bug-fix work.

## Engineering Priorities

Prefer, in order:

1. Preserve intended gameplay behavior
2. Correctness and data integrity
3. Playability
4. Clear ownership
5. Simplicity
6. Maintainability and testability
7. Incremental modernization
8. Extensibility only when current requirements justify it
9. Optimization when evidence warrants it

## Required Working Method

Before modifying code or Unity assets:

1. Inspect the directly affected scripts and call sites.
2. Inspect relevant scenes, prefabs, ScriptableObjects, tests, project settings, and documentation.
3. Trace the actual runtime flow end to end when behavior crosses systems.
4. Identify the current owner of each piece of state being changed.
5. Establish requested behavior and acceptance criteria.
6. Resolve material ambiguity before changing behavior, architecture, public contracts, serialized data, persistence, scoring, progression, or networking boundaries.
7. Identify preserved legacy behavior explicitly when a refactor touches old systems.

During implementation:

- Implement the narrowest complete solution.
- Preserve behavior outside the requested scope.
- Prefer one owner for each piece of gameplay state.
- Keep migrations compatible until old call sites are intentionally removed.
- Follow existing folders and naming unless the approved plan includes migration.
- Preserve Unity GUIDs and serialized references.
- Commit required `.meta` files for Unity assets and folders.
- Surface unrelated defects separately with evidence and a recommended follow-up.

Do not:

- invent gameplay requirements;
- silently expand scope;
- perform broad rewrites when a staged migration is safer;
- refactor unrelated systems for cleanliness;
- introduce frameworks or packages without demonstrated need;
- create speculative abstractions for hypothetical future modes;
- add new global state as a shortcut around ownership problems;
- use scene-wide searches as ordinary dependency injection;
- treat compilation as proof that a gameplay flow works.

## Level 5 Architecture Guardrails

The current architecture is intentionally being improved one vertical slice at a time. Follow the direction documented in `docs/systems-architecture-baseline.md` and the audit/remediation documents.

Prefer:

- one authoritative owner for gameplay state;
- explicit references and runtime contexts over hidden global discovery;
- plain C# services/models when separation materially improves testability or state ownership;
- ScriptableObjects for authored tuning/configuration where already appropriate;
- event/result boundaries between gameplay and presentation;
- shared contracts only after concrete duplicated behavior is understood;
- staged compatibility bridges during migrations;
- pooling for proven high-churn runtime objects;
- code-owned fallbacks and cleanup for gameplay triggered by animation events.

Avoid:

- new god managers;
- expanding singleton responsibilities;
- duplicate boolean state across controllers/managers;
- UI owning gameplay or match state;
- copy-pasting mode-specific versions of core mechanics without first checking existing contracts;
- replacing working legacy behavior without a regression strategy.

Scenes, prefabs, ScriptableObjects, input assets, animation assets, build settings, and `ProjectSettings` are part of the implementation and must be reviewed when relevant.

## Basketball and Match Integrity

Basketball scoring and match flows are high-risk because shot intent, launch-time state, physics-based make detection, stats, UI, game rules, and CPU/human paths historically cross multiple systems.

When changing shooting, scoring, stats, or match completion:

- read `docs/shot-lifecycle.md` first;
- trace attempt -> launch -> make/miss -> stats -> rules -> UI;
- preserve the distinction between attempt state and make detection;
- verify human and CPU behavior where both paths exist;
- verify local multiplayer/player identity is not resolved through a mutable shared reference;
- add or update regression coverage for bugs the change could reintroduce;
- test at least one affected real gameplay mode in Play Mode.

Do not "simplify" scoring behavior until existing oddities and compatibility requirements have been identified.

## Persistence, Identity, and Versus

Changes involving accounts, player identity, progression, saved results, networking, or versus/correspondence must read the relevant persistence and versus documents first.

Do not assume:

- a session identity is the same as durable account identity;
- local state and server state have the same authority;
- retries are safe without checking idempotency;
- player array positions are stable identities;
- a versus result can be changed without considering ruleset/version compatibility.

Treat save formats, stable IDs, ruleset identifiers, and network payloads as contracts requiring deliberate migration.

## Dependencies and Generated State

- Use the repository's existing package mechanism and lock state.
- Do not add a dependency merely to avoid implementing a small local solution.
- Do not commit machine-local Unity state, caches, credentials, secrets, or generated project files.
- Preserve repository Git LFS behavior for binary assets.

## Validation

Compilation alone is insufficient.

For relevant changes:

1. Run `./scripts/validate-repository.ps1` from the repository root.
2. Compile with the Unity version pinned by `ProjectSettings/ProjectVersion.txt`.
3. Run focused EditMode tests for pure/system behavior where applicable.
4. Run focused PlayMode tests for runtime behavior where applicable.
5. Inspect Unity errors and warnings related to the changed flow.
6. Validate affected scenes, prefabs, serialized references, and build contents.
7. Perform a manual Play Mode smoke test for behavior that automation does not establish.
8. For mode-sensitive work, exercise the affected modes rather than validating only a generic scene.
9. Report validation that could not be run and why; never claim it passed.

CI already runs repository validation plus Unity EditMode and PlayMode suites. Keep local validation aligned with those checks.

## Scope Control

For proposed work, classify additional work as:

- `REQUIRED` — necessary to satisfy the current issue, fix a regression, or keep the touched flow correct.
- `HIGH-VALUE` — small adjacent work that materially improves reliability or player-facing quality and is justified now.
- `LATER` — useful modernization or future feature work that should remain possible but is not needed now.
- `REJECT` — unrelated, duplicative, speculative, or not justified by current requirements.

For `LATER` work, preserve only the seam current work genuinely requires. Do not partially implement future systems.

## Specialized Workflows

Task-specific workflows live under [`docs/ai/skills/`](docs/ai/skills/).

Use only workflows relevant to the current task:

- Architecture/system refactors: `unity-repo-architect.md`
- Implementation planning and adversarial review: `implementation-plan-red-team.md`
- Approved implementation work: `unity-implementation-agent.md`
- Bugs and unexplained Unity behavior: `unity-debug-investigator.md`
- Playability/regression verification: `unity-playability-validator.md`
- Scope and modernization triage: `level5-scope-guardian.md`

Read [`docs/ai/README.md`](docs/ai/README.md) for composition guidance.
