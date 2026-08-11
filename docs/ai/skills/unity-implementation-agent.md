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

## Validation

After implementation:

1. Run `./scripts/validate-repository.ps1`.
2. Compile with the pinned Unity editor.
3. Run focused EditMode tests.
4. Run focused PlayMode tests.
5. Run broader tests when shared contracts changed.
6. Inspect relevant Unity errors/warnings.
7. Execute the affected gameplay flow manually in Play Mode when automated tests cannot prove it.
8. Verify each acceptance criterion directly.

Report exactly what was and was not run.

## Completion Report

Return:

- files changed;
- behavior changed;
- preserved compatibility;
- tests/validation run and results;
- manual Play Mode checks and results;
- remaining risks;
- unrelated issues discovered but not changed.

Do not declare success merely because the code compiles.
