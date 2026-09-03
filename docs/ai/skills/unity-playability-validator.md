# Unity Playability Validator

Use this workflow when a Level 5 acceptance criterion or domain guardrail requires runtime observation that existing focused automated validation cannot reasonably establish, or when explicit playability/regression validation is requested.

Do not invoke it merely because a change is player-facing. Deterministic logic already established by focused tests does not require an additional manual matrix solely for reassurance.

## Goal

Determine whether the smallest affected gameplay path that still requires runtime evidence works as a player experiences it.

## Establish the Evidence Gap

Identify:

- the exact runtime behavior that remains unproven;
- the affected gameplay mode(s), player path(s), and input path(s);
- automated evidence already current for this implementation state;
- domain-specific requirements from `AGENTS.md` that mandate runtime verification;
- the minimum manual matrix needed to close the gap.

Do not expand a focused feature check into an exhaustive game QA pass unless explicitly requested.

## Automated Evidence Reuse

Reuse current repository-validation, compilation, focused EditMode/PlayMode, or CI results already produced for the same implementation state.

Do not rerun broad automated validation simply because this workflow is invoked. Run an additional automated check only when:

- the relevant result does not exist;
- implementation changed after the result;
- the result is untrusted or contradictory;
- a domain rule specifically requires it;
- or the check is needed to isolate a runtime failure.

When another check is needed, use the narrowest one capable of establishing the affected contract.

`./scripts/validate-repository.ps1` is normally owned by PR Repository Validation CI. Run it locally here only when its invariant/script is directly implicated, CI evidence is unavailable and needed before completion, or local certification is explicitly requested.

## Runtime Smoke Test

Build the smallest manual matrix from the affected feature. Potential dimensions include:

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

Include a dimension only when the changed contract, acceptance criteria, a known regression risk, or a Level 5 domain guardrail makes it relevant. Do not mechanically test every dimension.

## Basketball/Match Checklist

When shooting, scoring, stats, timers, or rules are affected, preserve the stronger requirements in `AGENTS.md`, including testing at least one affected real gameplay mode in Play Mode. Within that required path, verify only applicable items:

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

Do not expand to unrelated modes or input paths without evidence that they share the changed contract.

## Scene/Prefab Checklist

When scene composition changes, verify only affected composition and lifecycle contracts, such as:

- required objects/components exist once;
- serialized references are intact;
- no missing scripts;
- no duplicate managers/event systems/input owners introduced by the change;
- initialization for the changed flow does not depend on accidental hierarchy/order;
- disable/destroy cleanup when subscriptions/reservations are affected;
- scene reload only when persistent/re-entry behavior is implicated.

## Failure Handling

For each failed check:

1. state the exact step;
2. state expected versus observed behavior;
3. capture only the relevant error/log evidence;
4. classify it as caused by the change, pre-existing, or environment-limited;
5. diagnose before retrying;
6. rerun only the smallest check needed after a fix.

Do not reread successful Unity logs in full or repeatedly retry environment failures.

## Result Classification

Report each validation item as:

- `PASS` — directly verified;
- `FAIL` — reproduced incorrect behavior;
- `BLOCKED` — could not run, with reason;
- `NOT APPLICABLE` — outside changed behavior.

Never convert `BLOCKED` into `PASS` based on static reasoning.

## Completion Output

Return:

1. remaining runtime evidence gap;
2. existing automated evidence reused;
3. minimal manual matrix and results;
4. regressions found;
5. remaining unverified behavior;
6. whether the affected runtime path is safe to merge based on available evidence.

A successful result from this workflow means the runtime-dependent acceptance path was verified. It does not mean every repository check, Unity suite, gameplay mode, device input path, or legacy flow was independently rerun.
