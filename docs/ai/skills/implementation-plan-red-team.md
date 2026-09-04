# Implementation Plan Red Team

Use this workflow for nontrivial Level 5 changes after the current behavior and requirements are understood.

Choose a mode before starting:

- **Standard** — default for ordinary scoped changes. One integrated review, one revision.
- **Deep** — full independent two-pass adversarial review. Use when risk warrants it or when explicitly requested.

## Step 1 — Build the Initial Plan

Create an ordered implementation plan that identifies:

- files/systems likely to change;
- current and target state ownership;
- preserved behavior;
- migration/compatibility steps;
- scene/prefab/serialized-reference impact when relevant;
- the minimum credible verification for the changed behavior;
- explicit non-goals.

The plan must be implementable in small coherent slices. Avoid vague steps such as "refactor architecture" or "clean up manager."

Do not create a large test matrix for routine changes. Verification belongs with the implementation step it proves where practical.

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
- a material behavior claim with no credible evidence path;
- validation work that is disproportionate to the risk.

For basketball-related changes, also review:

```text
attempt → launch → make/miss → stats → rules → UI
```

Revise the plan once to address material findings, then produce the final plan.

## Verification Budget

For standard planning outside domain-specific requirements in `AGENTS.md`, validation should normally be one to three highest-value checks total.

Prefer in order:

1. final diff/composition inspection;
2. compilation when affected;
3. one focused automated or runtime check for a concrete regression/integration risk.

Do not require new tests by default. Do not require both automated and manual verification for the same claim. Broad regression suites belong to PR CI unless the task explicitly requires local certification.

Basketball/match integrity and other high-risk domain rules in `AGENTS.md` may require stronger evidence; preserve those requirements without expanding beyond them.

## Deep Planning

### Review Pass 1 — Architecture and Unity Failure Modes

Challenge the plan for wrong/duplicated ownership, hidden dependencies, lifecycle/order assumptions, prefab/scene serialization, stale references, animation events, ScriptableObject misuse, input ownership, UI ownership, and any missing evidence for a materially risky behavior claim.

For Level 5, also inspect shot/scoring, player identity, match mode, CPU parity, persistence, and versus boundaries when touched.

Revise the plan to address material findings.

### Review Pass 2 — Scope, Compatibility, and Regression Risk

Challenge unnecessary abstraction, speculative extensibility, broad rewrites, unrelated cleanup, missing legacy compatibility, data/save/network contract changes, mode-specific cases, failure paths, and excessive testing/validation that is not justified by risk or domain rules.

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
3. minimum verification — normally one to three checks, expanded only for domain rules/deep risk;
4. preserved behavior/compatibility notes;
5. explicit deferred work;
6. concise record of important issues found in review.

Do not implement while running this workflow unless implementation was explicitly requested as part of the same approved task. Do not inflate the plan with exhaustive testing scenarios that CI or a dedicated certification task owns.
