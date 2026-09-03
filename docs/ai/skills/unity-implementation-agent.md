# Unity Implementation Agent

Use this workflow when Level 5 behavior and scope are already clear or an implementation plan has been approved.

## Before Editing

1. Inspect every directly affected file and important caller.
2. Confirm the plan still matches current `dev` state.
3. Identify contracts and behavior that must remain compatible.
4. Inspect affected scenes/prefabs/ScriptableObjects when references or composition matter.
5. Read feature-specific docs for the touched system.
6. Identify relevant existing tests before adding new test infrastructure.

If repository evidence materially invalidates the approved plan, do not improvise a redesign. Report the conflict and use the narrowest safe adjustment consistent with the task.

## Implementation Rules

- Keep the change scoped to the requested behavior.
- Preserve unrelated behavior.
- Maintain one clear owner for new/changed state.
- Prefer explicit references and existing service/context seams.
- Preserve serialized field compatibility where practical during migrations.
- Preserve Unity asset GUIDs and required `.meta` files.
- Add compatibility adapters only when real existing callers need them.
- Keep UI as a consumer/presenter of gameplay state rather than a new owner.
- Avoid runtime scene-wide searches unless the existing architecture explicitly requires a temporary compatibility bridge.
- Do not add packages or architectural frameworks without an approved need.
- Do not clean up unrelated legacy code while touching a file.

## High-Risk Level 5 Areas

When touched, explicitly preserve and validate:

- shot attempt/launch/make/stat flow;
- human and CPU parity;
- match-end fire-once behavior;
- local multiplayer player identity;
- stable character/player identifiers;
- progression/save compatibility;
- server/local authority and retries;
- versus ruleset/result compatibility;
- input ownership across keyboard/controller/touch/UI.

## Validation Decision

Determine the local validation level before running expensive checks. Default to **Level 1 — focused** as defined in `AGENTS.md`. Domain-specific guardrails in `AGENTS.md`, especially basketball/match integrity, override this default when they require stronger evidence.

Use this decision path:

```text
Docs/comments/non-runtime metadata only?
    YES -> inspect/lightweight checks -> STOP

Normal C# or deterministic system implementation?
    YES -> compile when affected + focused applicable tests -> STOP

Scene/prefab/shared-contract/cross-system behavior changed?
    YES -> focused checks + only the relevant integration paths -> STOP

Acceptance criterion or domain rule requires runtime observation?
    YES -> smallest required Play Mode path/mode matrix -> STOP

Release/certification/build-wide contract explicitly in scope?
    YES -> Level 3 certification
```

### Validation rules

- Do not run `./scripts/validate-repository.ps1` locally merely because implementation changed; PR CI already runs Repository Validation. Run it when its invariant/script changed, it is needed for diagnosis, CI evidence is unavailable and required before completion, or local certification is explicitly requested.
- Compile with the pinned editor when C# or Unity compilation can be affected.
- Prefer the smallest relevant EditMode/PlayMode test selection over broad suites.
- When Unity CI is confirmed active for the current PR, do not duplicate broad Unity suites locally solely because CI will run them.
- When Unity CI is disabled or unavailable, retain the focused local Unity checks required by the changed behavior; do not automatically replace missing CI with every available suite.
- Perform expensive validation after the implementation is coherent rather than after every intermediate edit.
- A passing result remains current until a later edit touches the behavior or contract it covers.
- After a failure, diagnose first and rerun the narrowest check capable of proving the fix.
- Do not repeatedly retry environment failures such as Unity licensing, unavailable editor processes, missing assets, credentials, hardware, or infrastructure.
- For verbose commands, inspect exit status first. Do not read successful logs in full; inspect targeted excerpts only on failure.
- Reuse still-current validation evidence from earlier phases of the same task.
- Verify each acceptance criterion with the smallest credible evidence source; not every criterion requires a separate command.
- Report exactly what was run, what passed, what was blocked, and what was intentionally left to CI or manual verification.

## Completion Report

Return:

- files changed;
- behavior changed;
- preserved compatibility;
- focused tests/validation run and results;
- manual Play Mode checks performed only when required;
- checks deferred to Repository Validation or active Unity CI;
- remaining risks/unverified behavior;
- unrelated issues discovered but not changed.

Do not declare success merely because the code compiles, and do not require broad duplicate local validation when focused evidence plus CI ownership is sufficient.
