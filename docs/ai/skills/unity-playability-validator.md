# Unity Playability Validator

Use this workflow after player-facing Level 5 changes, system refactors, scene/prefab work, input changes, scoring changes, or bug fixes whose correctness depends on runtime behavior.

## Goal

Determine whether the affected gameplay still works as a player experiences it. Passing compilation or unit tests is not enough.

## Automated Baseline

Run applicable checks:

1. `./scripts/validate-repository.ps1`.
2. Unity compilation with the pinned editor.
3. focused EditMode tests;
4. focused PlayMode tests;
5. broader suites when shared contracts or common gameplay code changed.

Record exact failures instead of summarizing them as "tests failed."

## Runtime Smoke Test

Build a minimal manual matrix from the affected feature. Check only relevant dimensions, but do not omit a dimension merely because it is inconvenient.

Potential dimensions include:

- human player;
- CPU player/defender;
- local multiplayer;
- keyboard/mouse;
- controller;
- touch/mobile input;
- affected basketball modes;
- combat-enabled modes;
- pause/resume;
- end-of-round flow;
- scene restart/re-entry;
- persistence reload;
- offline/network failure paths;
- versus/correspondence flow.

## Basketball/Match Checklist

When shooting, scoring, stats, timers, or rules are affected, verify as applicable:

- attempt begins from clean state;
- release/launch occurs once;
- miss clears attempt state correctly;
- make scores exactly once;
- shot category/range/moneyball/marker state does not leak to the next attempt;
- stats attach to the correct player;
- UI reflects authoritative stats without changing them;
- CPU and human shooters behave consistently where intended;
- win/end condition fires once;
- restart/new round begins cleanly.

## Scene/Prefab Checklist

When scene composition changes, verify:

- required objects/components exist once;
- serialized references are intact;
- no missing scripts;
- no duplicate managers/event systems/input owners;
- initialization does not depend on accidental hierarchy/order;
- disabling/destroying objects releases subscriptions/reservations;
- scene reload does not leave persistent stale state.

## Result Classification

Report each validation item as:

- `PASS` — directly verified;
- `FAIL` — reproduced incorrect behavior;
- `BLOCKED` — could not run, with reason;
- `NOT APPLICABLE` — outside changed behavior.

Never convert `BLOCKED` into `PASS` based on static reasoning.

## Completion Output

Return:

1. automated checks and results;
2. manual matrix and results;
3. regressions found;
4. remaining unverified behavior;
5. whether the change is safe to merge based on available evidence.
