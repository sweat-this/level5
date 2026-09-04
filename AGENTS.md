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

## AI Workflow Entry Point

[`docs/ai/PRESETS.md`](docs/ai/PRESETS.md) is the default entry point for routine AI-assisted work in this repository.

- Begin with the smallest directly relevant implementation surface (issue/PR/branch/current implementation state) before broader investigation.
- Inspect scenes, prefabs, ScriptableObjects, tests, project settings, architecture docs, and related systems when plausibly affected, rather than automatically for every task.
- Trace behavior end-to-end when the requested change genuinely crosses systems.
- Expand investigation only as needed to establish ownership, behavior, compatibility, or validation.
- Reuse evidence that remains current instead of rereading unchanged files.
- Do not perform whole-repository audits unless explicitly requested.
- Detailed workflows under `docs/ai/skills/` are specialist/reference procedures, loaded only when the task genuinely requires them.
- Keep completion reporting concise.

This does not relax any rule elsewhere in this document, including the basketball/scoring, persistence, identity, versus, compatibility, legacy-behavior, and validation requirements below.

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
2. Inspect relevant scenes, prefabs, ScriptableObjects, tests, project settings, and documentation when plausibly affected.
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

## Efficiency and Verification Budget

Implementation and root-cause work are the primary tasks. Testing and validation are supporting evidence, not a default deliverable to maximize.

For ordinary scoped work outside the high-risk domain rules above:

- inspect the completed diff;
- compile when the change can affect compilation;
- run a focused automated test only when it targets a concrete regression risk, changed deterministic contract, or acceptance criterion;
- do not create new tests solely because code changed or to increase coverage numbers;
- do not add a manual Play Mode pass merely for reassurance when code/asset inspection or focused automation already establishes the claim;
- keep validation plans and completion reports short—normally the one to three highest-value checks, not an exhaustive matrix;
- stop once the changed behavior has credible evidence and let PR CI own broad regression coverage.

Escalate beyond this budget when a domain rule above requires it or when the change crosses scoring/match lifecycle, persistence, identity, versus/networking, input ownership, serialized composition, project/build configuration, or another known high-risk boundary.

`unity-playability-validator.md` is a specialist workflow for explicit playability/regression verification or a material runtime evidence gap. It is not a standard final phase of ordinary implementation work.

## Validation

Validation is risk-based. Local agent validation establishes confidence in the changed behavior and directly affected contracts; it does not recertify every Level 5 mode or repository invariant before each pull request.

Domain-specific requirements above, especially the Basketball and Match Integrity rules, override the default validation level when they require stronger evidence.

### Ownership

- **Local implementation:** use the minimum credible evidence for the current change. For ordinary code changes this is often final-diff inspection plus compilation; add focused tests only when they materially improve confidence or a domain rule requires them.
- **Repository Validation CI:** `./scripts/validate-repository.ps1` runs on every pull request and push to `dev`; do not duplicate it locally solely because CI will run it.
- **Unity CI:** when `UNITY_CI_ENABLED` is `true` and credentials are configured, PR CI owns broad EditMode/PlayMode regression suites. When that capability is disabled or unavailable, do not assume Unity CI coverage; retain only the focused local Unity checks required by the changed behavior or domain rules.
- **Manual Unity validation:** use when an acceptance criterion or domain guardrail requires runtime/visual evidence that automation cannot reasonably establish.

### Local validation levels

- **Level 0 — inspection only:** documentation, comments, non-executable metadata, and similarly non-runtime changes. Do not start Unity solely for these changes.
- **Level 1 — focused (default):** inspect the completed diff; compile when compilation can be affected; run at most the smallest directly relevant automated test when a concrete failure risk justifies it; otherwise stop after compile.
- **Level 2 — integration:** use when changing scene/prefab composition, serialized references, shared gameplay contracts, scoring/match lifecycle, persistence/identity, input ownership, versus/network boundaries, or cross-system runtime ownership. Run Level 1 plus only the affected integration path or domain-required manual check. Do not automatically add both automated and manual coverage.
- **Level 3 — certification:** use only for explicit release/certification work, major build/project configuration changes, or tasks that specifically require broad regression evidence before proceeding.

Run `./scripts/validate-repository.ps1` locally when its repository invariant changed, the script itself changed, a focused failure needs it for diagnosis, CI evidence is unavailable and the task requires it before completion, or local certification is explicitly requested. Do not run it automatically for every implementation.

### Stop and reuse rules

- Run expensive validation after the implementation is coherent, not after every intermediate edit.
- A successful check remains valid until a later edit touches the behavior or contract it covers.
- Do not rerun successful checks merely for additional reassurance.
- After a failure, diagnose before retrying and rerun the narrowest check that can confirm the fix.
- Do not repeatedly retry environment failures such as Unity licensing, unavailable editor processes, missing external dependencies, or infrastructure failures.
- Reuse still-current validation evidence across audit, plan, implementation, continuation, and review.
- For verbose commands, check exit status first. Do not read successful logs in full; inspect only relevant failure excerpts.
- Report checks not run only when the omission matters to confidence or acceptance criteria; never convert `not run` or `blocked` into `passed`.

CI capability remains defined by `.github/workflows/repository-quality.yml`; do not assume the optional Unity job is active without current evidence.

## Scope Control

For proposed work, classify additional work as:

- `REQUIRED` — necessary to satisfy the current issue, fix a regression, or keep the touched flow correct.
- `HIGH-VALUE` — small adjacent work that materially improves reliability or player-facing quality and is justified now.
- `LATER` — useful modernization or future feature work that should remain possible but is not needed now.
- `REJECT` — unrelated, duplicative, speculative, or not justified by current requirements.

For `LATER` work, preserve only the seam current work genuinely requires. Do not partially implement future systems.

## Specialized Workflows

Start from [`docs/ai/PRESETS.md`](docs/ai/PRESETS.md) to pick a route and mode. Task-specific workflows live under [`docs/ai/skills/`](docs/ai/skills/) and are loaded only when the preset, risk, or task requires them.

Use only workflows relevant to the current task:

- Architecture/system refactors: `unity-repo-architect.md`
- Implementation planning and adversarial review: `implementation-plan-red-team.md`
- Approved implementation work: `unity-implementation-agent.md`
- Bugs and unexplained Unity behavior: `unity-debug-investigator.md`
- Playability/regression verification: `unity-playability-validator.md` — explicit verification work only; do not append it automatically to normal implementation
- Scope and modernization triage: `level5-scope-guardian.md`

Read [`docs/ai/README.md`](docs/ai/README.md) for composition guidance.
