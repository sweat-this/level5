# Unity Debug Investigator

Use this workflow for Level 5 bugs, errors, regressions, broken scenes, incorrect gameplay behavior, or unexplained Unity behavior.

## Principle

Treat debugging as an evidence-gathering investigation, not a code-generation task.

Do not change code until a likely root cause is established unless the diagnostic change is isolated, reversible, and needed to distinguish hypotheses.

## Evidence Collection

Gather the relevant evidence first:

- exact symptom and reproduction path;
- Unity Console/editor/build logs;
- stack traces;
- affected scene and prefab state;
- serialized references;
- recent changes/PRs in the affected area;
- current implementation and call chain;
- MonoBehaviour enable/disable/start/update ordering;
- input/state ownership;
- tests and validation results.

For gameplay bugs, trace the state transition rather than inspecting only the line that logged the error.

## Hypothesis Method

For each plausible cause state:

1. hypothesis;
2. supporting evidence;
3. contradicting evidence;
4. smallest diagnostic action;
5. expected result if true;
6. what the result would rule in/out.

Rank hypotheses by likelihood and diagnostic value.

Prefer one test that distinguishes multiple hypotheses over several speculative fixes.

## Level 5 Checks

When relevant, inspect:

- stale or mutable singleton/global references;
- player identity resolving to the wrong actor;
- human and CPU code-path divergence;
- shot attempt state surviving between attempts;
- match-end logic firing multiple times;
- input being consumed by multiple owners;
- animation events failing to execute cleanup;
- Unity fake-null/destroyed-object semantics;
- disabled objects retaining reservations/subscriptions;
- scene managers assuming required objects by name;
- local/server persistence authority mismatch;
- async request failures or retries mutating gameplay state twice.

## Fix Selection

Once root cause is supported:

- fix the owner/source of the bad state, not only the visible symptom;
- choose the narrowest durable change;
- preserve intended behavior;
- add a regression test when practical;
- avoid opportunistic refactors.

## Output

Report:

1. reproduction/symptom;
2. evidence;
3. hypotheses considered;
4. root cause and confidence;
5. proposed or implemented fix;
6. regression coverage;
7. Play Mode verification;
8. unresolved uncertainty.
