# Level 5 AI Engineering Workflows

These documents are reusable task workflows for coding agents working in the Level 5 Unity repository.

They complement `AGENTS.md`; they do not replace repository documentation or issue acceptance criteria.

## Normal Flow

```text
AGENTS.md
→ docs/ai/PRESETS.md
→ targeted evidence
→ detailed specialist workflow only if necessary
→ work
→ affected validation
```

Most routine tasks are resolved by [`PRESETS.md`](PRESETS.md) alone. Read only the specialist workflows below when the preset, risk level, or task genuinely requires deeper procedure — avoid loading every workflow by default.

## Specialist Workflows

- `unity-repo-architect.md` — substantial ownership/lifecycle/system architecture work.
- `implementation-plan-red-team.md` — planning review; deep mode retains the full two-pass adversarial review.
- `unity-implementation-agent.md` — detailed implementation discipline when useful.
- `unity-debug-investigator.md` — non-obvious failures and root-cause investigation.
- `unity-playability-validator.md` — detailed player-facing regression/playability verification.
- `level5-scope-guardian.md` — actual scope/modernization decisions.

## Example Compositions (Complex/Deep Changes)

These sequences remain valid for architecture-level or high-risk work. They are not the default for routine engineering — start from `PRESETS.md` first.

### Architecture or major refactor

1. `unity-repo-architect.md`
2. `implementation-plan-red-team.md`
3. `unity-implementation-agent.md` after the plan is approved
4. `unity-playability-validator.md`

### Bug or unexplained Unity behavior

1. `unity-debug-investigator.md`
2. `unity-implementation-agent.md` after root cause is established
3. `unity-playability-validator.md`

### New feature or modernization proposal

1. `level5-scope-guardian.md`
2. `unity-repo-architect.md` when architecture is materially affected
3. `implementation-plan-red-team.md`
4. `unity-implementation-agent.md`
5. `unity-playability-validator.md`

### Small, well-defined implementation

Use `unity-implementation-agent.md` directly when behavior, scope, and architecture are already clear. Add `unity-playability-validator.md` for runtime/player-facing behavior.

## Evidence Rule

A workflow is not permission to invent missing requirements. Repository state, current docs, issue acceptance criteria, scenes/prefabs, tests, and observed runtime behavior remain the evidence base.

## Level 5 Specific References

Use these documents whenever the touched area overlaps them:

- `docs/systems-architecture-baseline.md`
- `docs/architecture-audit.md`
- `docs/architecture-remediation-plan.md`
- `docs/shot-lifecycle.md`
- `docs/persistence-boundaries.md`
- `docs/player-input-architecture.md`
- `docs/ui-input-architecture.md`
- `docs/versus-architecture.md`
- `docs/player-select-architecture-overhaul-plan.md`

Do not copy old audit recommendations blindly. Confirm whether each finding is still open on current `dev`.
