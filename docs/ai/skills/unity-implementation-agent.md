# Unity Implementation Agent

Use this workflow when Level 5 behavior and scope are already clear or an implementation plan has been approved.

## Before Editing

1. Inspect every directly affected file and important caller.
2. Confirm the plan still matches current `dev` state.
3. Identify contracts and behavior that must remain compatible.
4. Inspect affected scenes/prefabs/ScriptableObjects when references or composition matter.
5. Read feature-specific docs for the touched system.
6. Identify existing focused tests only when they directly cover the changed behavior or a known regression risk.

Do not broaden discovery just to catalog every possible test or validation surface.

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

When touched, explicitly preserve the relevant domain contracts:

- shot attempt/launch/make/stat flow;
- human and CPU parity;
- match-end fire-once behavior;
- local multiplayer player identity;
- stable character/player identifiers;
- progression/save compatibility;
- server/local authority and retries;
- versus ruleset/result compatibility;
- input ownership across keyboard/controller/touch/UI.

The domain-specific requirements in `AGENTS.md`, especially Basketball and Match Integrity, override the routine verification budget below.

## Testing Policy

Tests are optional supporting evidence for ordinary work, not a mandatory output for every implementation.

Add or update tests when at least one of these is true:

- fixing a regression that could realistically recur;
- changing deterministic gameplay/domain behavior with a stable contract;
- changing a high-risk domain contract called out in `AGENTS.md`;
- changing an ownership/repository invariant with existing focused coverage;
- acceptance criteria explicitly require automated coverage;
- a focused test is the smallest credible proof.

Do not create tests solely because code changed, to increase coverage, or to mirror implementation details. Do not build new test infrastructure for a small change unless the change genuinely needs it.

## Validation Decision

Use the minimum credible evidence for the change. Do not treat validation as a separate workstream unless risk or a domain rule requires it.

```text
Docs/comments/non-runtime metadata only?
    YES -> inspect diff -> STOP

Normal scoped C# implementation?
    YES -> inspect diff + compile when affected
           + one focused existing test only if it targets a concrete risk
           -> STOP

Scene/prefab/shared-contract/cross-system behavior changed?
    YES -> inspect affected composition
           + the smallest relevant integration path
           -> STOP

AGENTS.md domain rule requires stronger evidence?
    YES -> perform only that required evidence -> STOP

Acceptance criterion requires runtime/visual observation
that inspection or focused automation cannot establish?
    YES -> smallest required Play Mode/manual path -> STOP

Release/certification/build-wide contract explicitly in scope?
    YES -> Level 3 certification
```

### Validation rules

- Default routine validation budget is final-diff inspection plus compilation when applicable.
- Do not run `./scripts/validate-repository.ps1` locally merely because implementation changed; PR CI already runs Repository Validation.
- Run repository validation locally only when its invariant/script changed, it is needed for diagnosis, CI evidence is unavailable and required before completion, or local certification is explicitly requested.
- Prefer a single focused EditMode/PlayMode test over broad suites when a test is actually justified.
- When Unity CI is active, do not duplicate broad Unity suites locally solely because CI will run them.
- When Unity CI is disabled or unavailable, retain only the focused local Unity checks required by the changed behavior or domain rules; do not replace missing CI with every available suite.
- Do not automatically run both automated and manual checks for the same claim.
- Perform expensive validation once after implementation is coherent rather than after every intermediate edit.
- A passing result remains current until a later edit touches the behavior or contract it covers.
- After failure, diagnose first and rerun the narrowest check that proves the fix.
- Do not repeatedly retry environment failures such as Unity licensing, unavailable editor processes, missing assets, credentials, hardware, or infrastructure.
- For verbose commands, inspect exit status first. Do not read successful logs in full; inspect targeted excerpts only on failure.
- Reuse still-current validation evidence from earlier phases of the same task.
- Stop when the changed behavior has credible evidence. Broad regression confidence belongs to PR CI.

## Completion Report

Keep completion reporting concise. Return:

- behavior changed;
- files/assets changed when useful;
- preserved compatibility when material;
- the small set of verification checks actually run and any material unverified requirement;
- unrelated issues only when important and not changed.

Do not add a manual Play Mode section when none was required. Do not enumerate every check that could have been run. Do not declare success merely because code compiles when the changed behavior required stronger domain evidence, and do not require broad duplicate local validation when focused evidence plus CI ownership is sufficient.
