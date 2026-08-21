# Implementation Plan Red Team

Use this workflow for nontrivial Level 5 changes after the current behavior and requirements are understood.

Choose a mode before starting:

- **Standard** — default for ordinary scoped changes. One integrated review, one revision.
- **Deep** — full independent two-pass adversarial review. Use when risk warrants it (see "When to Use Deep Planning" below) or when explicitly requested.

## Step 1 — Build the Initial Plan

Create an ordered implementation plan that identifies:

- files/systems likely to change;
- current and target state ownership;
- preserved behavior;
- migration/compatibility steps;
- tests;
- scene/prefab/serialized-reference impact;
- manual Play Mode checks;
- explicit non-goals.

The plan must be implementable in small coherent slices. Avoid vague steps such as "refactor architecture" or "clean up manager."

## Standard Planning

Use:

```text
targeted repository evidence
→ initial bounded plan
→ one integrated review
→ revise once
→ final plan
```

### Integrated Review

Challenge the plan for applicable risks:

- current versus target state ownership;
- duplicated state;
- Unity lifecycle/order assumptions;
- hidden scene/global dependencies;
- scene/prefab serialization breakage;
- stale references after disable/destroy/scene changes;
- ScriptableObject runtime-state misuse;
- input ownership conflicts;
- UI accidentally owning gameplay state;
- animation-event dependency;
- legacy compatibility;
- unnecessary abstraction;
- speculative extensibility;
- staged migration opportunities;
- mode-specific behavior;
- human/CPU parity;
- persistence/network compatibility;
- regression coverage;
- validation that proves only compilation rather than gameplay.

For basketball-related changes, also review:

```text
attempt → launch → make/miss → stats → rules → UI
```

Revise the plan once to address material findings, then produce the final plan.

## Deep Planning

Preserve the existing independent two-pass structure.

### Review Pass 1 — Architecture and Unity Failure Modes

Challenge the plan for:

- wrong or duplicated state ownership;
- hidden scene/global dependencies;
- MonoBehaviour lifecycle/order assumptions;
- prefab/scene serialization breakage;
- stale references after disable/destroy/scene changes;
- animation event dependency;
- ScriptableObject runtime-state misuse;
- input ownership conflicts;
- UI becoming an owner of gameplay state;
- insufficient test seams.

For Level 5, also inspect shot/scoring, player identity, match mode, CPU parity, persistence, and versus boundaries when touched.

Revise the plan to address material findings.

### Review Pass 2 — Scope, Compatibility, and Regression Risk

Challenge the revised plan for:

- unnecessary abstraction;
- speculative extensibility;
- broad rewrites where staged migration is safer;
- unrelated cleanup;
- missing legacy compatibility;
- data/save/network contract changes;
- missing mode-specific cases;
- missing failure paths;
- inadequate regression coverage;
- validation that proves only compilation rather than gameplay.

Revise again.

### When to Use Deep Planning

Use deep/two-pass planning when:

- explicitly requested;
- state ownership changes materially;
- several systems/modes are involved;
- shot/scoring architecture changes;
- persistence contracts change;
- versus/network contracts change;
- stable player identity changes;
- assembly boundaries change;
- significant scene/prefab migration occurs;
- architecture remediation crosses major ownership boundaries;
- repository evidence conflicts materially;
- comparable risk warrants it.

## Final Output

Return:

1. final ordered implementation plan;
2. acceptance criteria mapped to steps;
3. automated validation;
4. manual Play Mode matrix;
5. preserved behavior/compatibility notes;
6. explicit deferred work;
7. concise record of important issues found in review (the integrated review for standard mode, or pass 1 and pass 2 for deep mode).

Do not implement while running this workflow unless implementation was explicitly requested as part of the same approved task.
