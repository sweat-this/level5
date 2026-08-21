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
impact-based validation
```

## Routing: Route + Mode + Risk

Pick a **route** describing the task shape, a **mode** describing how much investigation it needs, then let **risk** (below) upgrade the mode if warranted.

**Routes**

- `status` — is this done, merged, stale, or what's next.
- `plan` — produce or revise an implementation plan.
- `implement` — make an approved change.
- `bug` — diagnose unexplained/incorrect behavior.
- `review` — check a diff or plan for correctness/risk.

**Modes**

- `lean` — minimal evidence, no broad investigation.
- `standard` — targeted evidence, normal engineering rigor. Default.
- `deep` — full investigation/review depth. A mode, not a route.

### Default behavior

```text
Mode: standard
Read strategy: targeted
Evidence reuse: enabled while relevant state remains current
Output: concise
```

Use `lean` for:

- is-this-done checks;
- merged/stale verification;
- next-action questions;
- continuation where current state is already known;
- small documentation/process work.

Use `deep` only when risk warrants it (see below) or the user explicitly requests deep analysis.

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
- Do not read every architecture document by default.
- Do not read every AI workflow by default.
- Do not perform whole-repository audits unless explicitly requested.
- File-count limits must not override correctness.
- Cross-system Level 5 behavior may legitimately require following the entire affected state flow — do not truncate a trace for token savings.

## Critical-Flow Escalation to `deep`

Standard mode is appropriate for ordinary scoped work. Automatically consider `deep` when materially touching:

**Basketball integrity** — shot attempt/launch/make lifecycle; scoring; player statistics; point-value determination; match completion; CPU/human behavior parity; mode-specific shot rules. Use [`docs/shot-lifecycle.md`](../shot-lifecycle.md).

**Identity and multiplayer** — local player ownership; player index versus stable identity; controller/input ownership; local multiplayer; CPU versus human resolution; player-select architecture.

**Persistence** — save format; progression; durable identity; migrations; persisted match/result state; retry/idempotency behavior. Use [`docs/persistence-boundaries.md`](../persistence-boundaries.md).

**Versus/correspondence** — network payloads; durable match results; ruleset/version compatibility; correspondence state; authority between local and server state. Use [`docs/versus-architecture.md`](../versus-architecture.md).

**Architecture** — broad manager ownership migration; state ownership moving between systems; assembly/asmdef restructuring; scene/bootstrap architecture; public/shared contracts; multiple gameplay modes; significant serialized-data migration; conflicting repository evidence.

`deep` means deeper *targeted* investigation, not reading the whole repository.

## Evidence Reuse

Reuse prior evidence across:

```text
audit → plan → revised plan → implementation prompt → implementation continuation
```

when:

- the issue/task remains the same;
- the relevant branch/head has not materially changed;
- the affected implementation has not materially changed;
- no overlapping PR invalidated the conclusions.

Refresh only affected evidence when:

- the branch/head changed materially;
- overlapping work merged;
- affected files changed;
- acceptance criteria changed;
- repository evidence conflicts.

Do not reread files solely to restate known scope in an implementation prompt.

## Detailed Workflows

Load a workflow under [`docs/ai/skills/`](skills/) only when the route, mode, or risk requires it. See [`docs/ai/README.md`](README.md) for the full catalog and composition guidance.

## Impact-Based Validation

Token reduction must not weaken verification. Match validation to the actual change:

```text
pure/system logic          → focused EditMode tests
runtime gameplay behavior  → focused PlayMode tests + smoke test
shot/scoring change        → affected shot lifecycle + relevant gameplay mode
CPU/human shared behavior  → verify both applicable paths
mode-sensitive behavior    → exercise affected modes
scene/prefab change        → composition + serialized-reference validation
persistence/network change → compatibility/migration/idempotency tests as applicable
```

Repository validation remains:

```powershell
./scripts/validate-repository.ps1
```

Run validation appropriate to the actual change; do not turn every focused change into exhaustive project QA, and do not skip validation that the change actually requires.
