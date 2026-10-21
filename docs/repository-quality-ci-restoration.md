# Repository Quality CI restoration - issue #232

Audit date: 2026-10-21. Scope: [#232](https://github.com/sweat-this/level5/issues/232).
Required-check enforcement belongs to [#233](https://github.com/sweat-this/level5/issues/233).

## Baseline and authenticated audit

- Fresh `git fetch origin dev` succeeded (exit 0). Current `dev` is
  `4ae62a08af4c3b80ca732f6c43b9d5e4facfe7d9`; the historical baseline happens to remain current.
- The checkout was clean. GitHub reported no open PRs targeting `dev` before this work.
- Registered workflow: **Repository quality**, ID **327878811**, path
  `.github/workflows/repository-quality.yml`. Authenticated REST lookup confirmed
  **`disabled_manually`** before restoration.
- Repository Actions permissions: `enabled: true`, `allowed_actions: all`.
  Repository is neither archived nor disabled. The authenticated account has admin access.
- Default workflow token permission is `read`; Actions cannot approve PR reviews.
- `dev` is the default branch and is unprotected (protection endpoint: HTTP 404,
  `Branch not protected`). Rulesets endpoint returned `[]`.
- Paginated branch inventory includes `dev`, `master`, `migration`, and topic branches;
  no `main` branch exists. No release authority is inferred from those branch names.
- Repository variables list returned zero entries; individual `UNITY_CI_ENABLED`
  lookup returned HTTP 404. Repository secrets list returned zero entries. Secret
  metadata only was requested; no secret values were retrieved or printed.
- No checks were attached to the baseline SHA at initial inspection.

The API returned ten historical runs: eight `pull_request` failures, one `push`
failure on `dev`, and one `push` success on `dev`. The latest was
[31196984517](https://github.com/sweat-this/level5/actions/runs/31196984517),
2026-08-07, SHA `52a2b92ad07cbeb5d1c90d3f76f6984d1cb36c87`.
Its `validate` job and actual invariant step succeeded; `unity-editmode` failed
at `Report missing Unity license`, before either suite executed. This is a
licensing/configuration failure, not a repository validation or Unity test failure.
Historical names differ from the current workflow and must not become required checks.
The last observed `dev` push was failed run
[31021751359](https://github.com/sweat-this/level5/actions/runs/31021751359),
SHA `cc69b138ef936ad41212d541c01018d590588670`, on 2026-08-05.
The earlier successful push was
[31015461987](https://github.com/sweat-this/level5/actions/runs/31015461987),
SHA `922c9ac7b1d8730932ec8c5864297917a4dfa287`. Neither proves current-source execution.

## Existing architecture and ownership

The existing workflow accepts `pull_request` and pushes to `dev`, without path
filters. Per-ref concurrency cancels superseded runs; cancellation is not failure
or success. No dispatch trigger is needed for this restoration.

| Owner/job | Dependencies and authority |
| --- | --- |
| `Repository Validation` | Independent `windows-latest` job, 10-minute timeout, checkout v4, PowerShell execution of `./scripts/validate-repository.ps1`, `contents: read` |
| `Unity CI capability` | Independent `ubuntu-latest` job, 5-minute timeout; only same-repository PRs or non-PR events; reports `run=false` unless `UNITY_CI_ENABLED` is exactly `true`; validates credential presence only when enabled |
| `Unity Tests` | Needs capability output `run=true`; Ubuntu, 60-minute timeout; LFS checkout, Library cache, GameCI v4 for separate EditMode/PlayMode steps, artifact uploads; `contents: read` and `checks: write` |
| `Require both Unity suites to pass` | Final step inside `Unity Tests`, not a separate job/check; tests each suite's raw `outcome`, so `continue-on-error` cannot convert test failure into a green job |

Checkout/cache failure fails the Unity job before suites start. Artifact-upload
failure also fails that job, even if it prevents the final step from running.
The two suite steps use `continue-on-error` so both can execute; only successful
outcomes in both permit the final gate to pass. This is source-reviewed behavior;
it is not an executed failure-injection test of unavailable Unity infrastructure.

The unchanged validator owns tracked machine-local/generated file rejection,
missing asset `.meta` detection, synchronous networking API rejection, and the
documented narrow allowlist for broad menu exception catches. Unity gameplay,
authored-data parity, runtime, Player builds, and hardware acceptance are outside
that script's authority.

## Review pass 1 - operational correctness (before changes)

Initial plan: establish live capability, restore the existing registration if
disabled, prove the actual script on a fresh PR, and hand observed checks to #233.

Findings and refinement:

1. Live workflow disablement is proven independently of absent current checks.
   Repository-wide Actions is permitted; enabling this registration is the narrow repair.
2. Existing triggers are correct. Do not edit YAML to manufacture activity.
3. Historical runs reached hosted Windows/Ubuntu runners but used older workflow
   code. Obtain fresh runner, step, SHA, event, and conclusion evidence.
4. The historical overall failure masks a successful validator and missing Unity
   licensing. Record job and step results separately from the run conclusion.
5. Unity is disabled by the effective variable value. Missing credentials do not
   fail the disabled path. Require a fresh truthful disabled notice and skipped
   Unity job; record both suites as NOT EXECUTED, with no passing test count.
6. Current exact names must come from fresh check runs. Review the final Unity
   outcome gate statically; do not claim enabled-suite execution or test artifacts.
7. Fork capability is explicitly skipped before secrets are used. Preserve it.
8. Historical push events establish prior delivery only. A fresh PR proves current
   PR delivery; current `dev` push delivery needs an ordinary subsequent merge/push.

Revised plan: enable workflow 327878811, open a documentation-only PR against
current `dev`, inspect its hosted script and capability results, and leave the
fresh `dev` push gate open unless an ordinary authorized push is observed.

## Review pass 2 - security and minimal scope (before changes)

1. Reuse the registered workflow and validator; introduce no parallel pipeline,
   dispatch automation, gameplay edits, Unity configuration, or licensing architecture.
2. Preserve `pull_request`; do not use `pull_request_target`. Fork PRs run only the
   repository validator with the read-only token; Unity capability/tests are skipped.
3. Preserve workflow/job permissions. Only the gated Unity test job needs checks write.
4. Inspect secret names/counts only. The capability step must not print credentials.
5. A green capability probe is availability reporting, not a green Unity suite.
   Do not require `Unity Tests` or GameCI result checks while capability is inactive.
6. #233 owns branch protection and required checks. Leave all branch rules unchanged.
7. Publish only this evidence record and its documentation-index link. Retain older
   audit documents as historical observations rather than rewriting their evidence.

Final plan: configuration restoration plus reviewable evidence documentation.
No source/workflow correction is justified by the current findings. Hosted
validation failure, if encountered, must be diagnosed from its specific log
before any narrowly scoped repair; no invariant may be weakened for a green run.

## Risk and mitigation

| Risk | Mitigation/evidence requirement |
| --- | --- |
| Confusing disabled with failed | Registered state from authenticated API; inspect individual job/step conclusions |
| Stale or superseded green checks | Record run ID, event, source head, tested merge SHA and check attachment; re-fetch before publication |
| Runner or Actions policy unavailable | Repository permission audit plus actual hosted runner execution |
| Optional Unity represented as tested | Preserve disabled notice; suites NOT EXECUTED; no invented counts/artifacts |
| Unity failure masked by continue-on-error | Keep raw-outcome gate; skipped/failing prerequisites cannot produce successful suite evidence |
| Fork credential exposure | Preserve same-repository condition, read-only default and safe PR trigger |
| Duplicate validation or scope expansion | Reuse script/workflow, no local rerun absent diagnosis, no #233 rule changes |
| Unproved dev-push delivery | Report historical receipt and configured trigger separately from fresh post-restoration proof |

## Restoration and fresh hosted evidence

Pending: enable the existing workflow, record its live active state and fresh PR
execution, and replace this section with observed results before final handoff.

## #233 handoff and remaining gates

Pending fresh evidence. Candidate authority is the exact `Repository Validation`
check from GitHub Actions, not historical `validate`. Unity CI is currently
inactive because `UNITY_CI_ENABLED` is absent; EditMode and PlayMode are NOT
EXECUTED. Do not make optional Unity checks mandatory. No local Unity tests,
Player/IL2CPP build, live-backend certification, manual gameplay, or physical-device
acceptance is performed by this restoration.

Keep #232 open until its hosted acceptance evidence is complete. Current-source
`dev` push delivery must be confirmed after a normal merge; do not force a direct
push or create a branch solely to manufacture that evidence.
