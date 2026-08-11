# Implementation Plan Red Team

Use this workflow for nontrivial Level 5 changes after the current behavior and requirements are understood.

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

## Review Pass 1 — Architecture and Unity Failure Modes

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

## Review Pass 2 — Scope, Compatibility, and Regression Risk

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

## Final Output

Return:

1. final ordered implementation plan;
2. acceptance criteria mapped to steps;
3. automated validation;
4. manual Play Mode matrix;
5. preserved behavior/compatibility notes;
6. explicit deferred work;
7. concise record of important issues found in review pass 1 and pass 2.

Do not implement while running this workflow unless implementation was explicitly requested as part of the same approved task.
