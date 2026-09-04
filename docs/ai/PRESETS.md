# Level 5 AI Presets

Compact daily entry point for routine AI-assisted work in this repository. `AGENTS.md` remains the authoritative engineering contract; this document is a routing and evidence-scoping layer on top of it, not a replacement.

```text
AGENTS.md
    ↓
docs/ai/PRESETS.md
    ↓
minimum necessary repository evidence
    ↓
detailed Level 5 workflow only when necessary
    ↓
work
    ↓
minimum credible verification
```

## Routing: Route + Mode + Risk

Pick a **route** describing the task shape, a **mode** describing how much investigation it needs, then let **risk** upgrade the mode if warranted.

**Routes**

- `status` — is this done, merged, stale, or what's next.
- `plan` — produce or revise an implementation plan.
- `implement` — make an approved change.
- `bug` — diagnose unexplained/incorrect behavior.
- `review` — check a diff or plan for correctness/risk.

**Modes**

- `lean` — minimal evidence, no broad investigation.
- `standard` — targeted evidence, normal engineering rigor. Default.
- `deep` — deeper targeted investigation. A mode, not a route.

### Default behavior

```text
Mode: standard
Read strategy: targeted
Evidence reuse: enabled while relevant state remains current
Output: concise
```

Use `lean` for state checks, merged/stale verification, next-action questions, continuation where current state is already known, and small documentation/process work.

Use `deep` only when risk warrants it or the user explicitly requests deep analysis.

## Evidence/Token Gate

Before broad repository investigation, answer:

1. What decision must be made?
2. What is the smallest evidence that can support it?
3. Can issue, PR, branch, commit, or current implementation state answer it directly?
4. If not, which exact scripts, call sites, scenes, prefabs, ScriptableObjects, tests, assets, settings, or documentation are plausibly involved?
5. Does the behavior cross system boundaries and therefore require an end-to-end runtime trace?
6. What condition ends investigation?

Rules:

- Start from issue/PR/current branch state when applicable.
- Begin with directly affected implementation and call sites.
- Search/navigate before broad reads where practical.
- Expand one dependency/ownership boundary at a time.
- Stop once sufficient evidence exists.
- Do not read every architecture document or AI workflow by default.
- Do not perform whole-repository audits unless explicitly requested.
- File-count limits must not override correctness.
- Cross-system Level 5 behavior may legitimately require following the entire affected state flow; do not truncate a necessary trace for token savings.

## Critical-Flow Escalation to `deep`

Standard mode is appropriate for ordinary scoped work. Consider `deep` when materially touching:

**Basketball integrity** — shot attempt/launch/make lifecycle; scoring; player statistics; point-value determination; match completion; CPU/human behavior parity; mode-specific shot rules. Use [`docs/shot-lifecycle.md`](../shot-lifecycle.md).

**Identity and multiplayer** — local player ownership; player index versus stable identity; controller/input ownership; local multiplayer; CPU versus human resolution; player-select architecture.

**Persistence** — save format; progression; durable identity; migrations; persisted match/result state; retry/idempotency behavior. Use [`docs/persistence-boundaries.md`](../persistence-boundaries.md).

**Versus/correspondence** — network payloads; durable match results; ruleset/version compatibility; correspondence state; authority between local and server state. Use [`docs/versus-architecture.md`](../versus-architecture.md).

**Architecture** — broad manager ownership migration; state ownership moving between systems; assembly/asmdef restructuring; scene/bootstrap architecture; public/shared contracts; multiple gameplay modes; significant serialized-data migration; conflicting repository evidence.

`deep` means deeper targeted investigation, not reading the whole repository or automatically running Level 3 validation.

## Evidence Reuse

Reuse prior evidence across audit, plan, revised plan, implementation prompt, implementation continuation, and review when the task and affected implementation remain materially unchanged.

Refresh only affected evidence when the branch/head, overlapping work, acceptance criteria, or relevant files changed materially or when evidence conflicts.

Do not reread files solely to restate known scope in an implementation prompt.

Verification evidence follows the same rule. Do not rerun still-current checks in a continuation or review unless implementation changed afterward, the result is untrusted, or reproduction is necessary to resolve a material finding.

## Verification Budget

Routine implementation should not spend a large share of tokens or execution time on testing and validation.

Default `standard` implementation budget outside domain-specific requirements in `AGENTS.md`:

```text
final diff / affected composition inspection
+ compile when compilation can be affected
+ optional single focused test only for a concrete regression/contract risk
→ STOP
```

Rules:

- Tests are not required merely because code changed.
- Do not create new test infrastructure for small scoped work unless acceptance criteria, a known regression, or a deterministic/high-risk contract justifies it.
- Do not automatically run both automated and manual checks for the same claim.
- Manual Play Mode is reserved for runtime/visual evidence gaps and explicit domain rules.
- Do not run `./scripts/validate-repository.ps1` locally merely because implementation changed; PR CI already owns it.
- Broad EditMode/PlayMode suites belong to Unity CI when active. Missing Unity CI does not automatically justify every local suite.
- Validation sections in plans and completion reports should normally contain only the one to three highest-value checks.
- Stop once credible evidence exists for the changed behavior.

The Basketball and Match Integrity rules in `AGENTS.md` override this budget when they require regression coverage and a real-mode Play Mode path.

## Detailed Workflows

Load a workflow under [`docs/ai/skills/`](skills/) only when the route, mode, or risk requires it. See [`docs/ai/README.md`](README.md) for the full catalog and composition guidance.

`unity-playability-validator.md` is intentionally specialized. Use it for explicit playability/regression verification, certification, or a material runtime evidence gap; do not append it automatically to ordinary implementation work.

## Route Guidance

### status

Use the minimum branch/PR/current-state evidence needed. Output concise state, blockers, and next action if obvious.

### plan

Produce a scoped sequence based on current ownership and acceptance criteria. Include minimum verification, not a broad test matrix. Use `skills/implementation-plan-red-team.md` when adversarial review is requested or risk justifies it.

### implement

Implement the narrowest complete change. Default verification is final diff + compile when affected, with one focused test only for concrete risk unless `AGENTS.md` domain rules require more. Use `skills/unity-implementation-agent.md` when implementation needs more guidance.

### bug

Establish root cause before fixing. Use focused reproduction/log/source evidence and `skills/unity-debug-investigator.md` for non-obvious failures.

### review

Assess correctness, scope, architecture, and regressions from the supplied diff/PR plus still-current evidence. Do not rerun validation merely to review. Mention missing validation only when it materially reduces confidence in a changed contract.
