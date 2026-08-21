# Systems Restructure Plan

Last updated: 2026-08-17 (revised after first review pass)

The brief: no ship date, no feature deadline — the game is done when the systems are correct,
structured and maintainable. That removes the usual reason to defer structural work, so this plan
sequences by **leverage** rather than by visible progress.

This complements [Architecture Remediation Plan](architecture-remediation-plan.md), which is a
combat-first, nine-phase plan describing *what* to build. This one describes *what order* to unlock
it in, because several of those phases are currently unverifiable for a structural reason.

## What "enterprise grade" should mean here

One owner per piece of state, one implementation per behaviour, contracts that fail at build time
rather than in a play session, and changes that can be tested before they ship.

It should **not** mean a DI container, an ECS migration, a service-locator layer, or interfaces
introduced ahead of a second implementation. `AGENTS.md` rejects those, and no defect found so far
was caused by missing infrastructure. Each was caused by *two implementations of one thing*, or by
state with no clear owner.

## The measured problem

See [Appendix: how these were measured](#appendix-how-these-were-measured) for exact commands.

| From | To | Refs | Back |
| --- | --- | --- | --- |
| `basketball` | `player` | 19 | 15 |
| `player` | `game manager` | 19 | 15 |
| `basketball` | `game manager` | 16 | 9 |

A three-way cycle: `player` <-> `basketball` <-> `game manager` <-> `player`.
[The shot lifecycle](shot-lifecycle.md) records the `player`/`basketball` half; the measurement shows
`game manager` is the third side, so cutting one edge frees nothing.

Consequences, observed rather than theorised:

- **218 `.cs` files, 4 assembly definitions.** Gameplay lives in the predefined `Assembly-CSharp`,
  which an `.asmdef` cannot reference. Play-mode tests for gameplay therefore live in a folder with
  *no* asmdef — a workaround documented in `Level5GameplayPlayModeTests` itself.
- **~3,700 lines of human/CPU duplicate pairs**, which diverge silently. AUD-001 found one kill
  healing 5/2 down one path and 7/3 down the other; a single ball-visibility bug had to be fixed
  twice, in two files, in one commit.
- **Five controllers move a dynamic Rigidbody with `MovePosition`.** One fixed, four outstanding.
- **124 live scene searches.** A rename fails during play, not during build.

## Sequence

Every phase lands with its invariant asserted in the validator or a test. A phase that cannot be
verified is not done. Every phase leaves the game playable and the suite green.

### Phase 0 — Scene contract inventory

Every later phase depends on scene and prefab wiring, so contract work starts here rather than
waiting for Phase 6. This phase does not remove scene searches. It records what each system a later
phase touches resolves by name, and adds the validator check as that system migrates.

**Exit:** every system scheduled in Phases 1-5 has its required scene and prefab objects declared, in
the `RequiredSceneObjectNames` style the menus now use.

### Phase 1 — Cut the player/basketball/game-manager cycle

The keystone, split into slices because it is the highest-risk system in the repository. Each slice
ships and is verified on its own.

- **1a — Attempt/result contract.** Introduce the shot result travelling one way. No consumer moves.
- **1b — Match-stats owner behind a `GameStats` facade.** `GameStats` and the marker counters
  deliberately remain inline today because they feed scoring and win conditions rather than
  presentation (AUD-010, In Progress). The new owner goes in behind a compatibility facade, and no
  call site changes behaviour in this slice.
- **1c — Migrate consumers one at a time**, each with its own verification pass, including prefab and
  component migration.
- **1d — Invert the `game manager` edge**, then assert direction.

Preserved oddities from [the shot lifecycle](shot-lifecycle.md) are carried explicitly and named in
whichever slice touches them. They are behaviour, not accidents.

**Exit:** the three edges are one-directional, asserted by a dependency test that fails on a new
back-reference so the cycle cannot reform.

### Phase 2 — Assembly split

Blocked on two things, not one. The cycle is the first. The second is that an asmdef must name every
package assembly it uses, and a wrong name is a project-wide compile failure (AUD-012 note,
2026-08-07) — which is why this was never attempted blind.

That second blocker is now resolved. `Library/ScriptAssemblies` shows which references are
asmdef-based and which are precompiled:

| Reference | Kind |
| --- | --- |
| `Unity.InputSystem`, `Unity.Mathematics`, `Analytics` | asmdef assemblies — must be listed |
| `Newtonsoft.Json`, `Mono.Data.Sqlite`, `Unity.IO.LowLevel.Unsafe` | precompiled or engine — automatic |

- **2a — Feasibility spike.** Smallest candidate asmdef, compiled headless in Unity batch mode before
  anything migrates. The asmdef-free PlayMode workaround stays until this succeeds.
- **2b — Migrate runtime assemblies** once the spike compiles clean.

**Exit:** gameplay code lives in referenced assemblies; the asmdef-free test folder is deleted and
play-mode tests reference gameplay code normally.

### Phase 3 — Converge the human/CPU pairs

Not "one type". The pairs carry real, intended differences: the human path has an analytics call and
an `isCpu` swish gate the CPU path does not, and the CPU path has its own trigger resets, dunk
decisions and shot-meter wait behaviour.

**Exit:** one shared implementation for common behaviour, with documented role-specific adapters for
intentional differences, and a parity matrix covering human, CPU shooter, CPU defense and local
multiplayer.

### Phase 4 — One locomotion motor

Scoped to **actor locomotion**, not to velocity writes in general. Direct velocity stays correct for
jumps, dunks, projectiles and ball launch. The defect is driving a dynamic body's locomotion with
`MovePosition`.

**Exit:** no actor drives locomotion via `MovePosition`; impulse and launch remain available as
explicit APIs. Movement tested separately for human, CPU shooter, CPU defense, enemy, bodyguard,
racing vehicle and cinder block.

### Phase 5 — Input ownership

One owner per action map. Today a shared `PlayerControls` instance and per-player instances coexist,
consumers read maps nothing enables — which made every level unstartable — and the `PlayerTouch` map
has zero readers *and* zero enablers.

**Exit:** dead maps deleted, `activeInputHandler` dropped to Input System only, and a test asserting
every map a consumer reads is enabled by someone.

### Phase 6 — Remove the scene searches

Full removal of the 124 scene searches, on the contracts Phase 0 established.

**Exit:** the validator fails the build on a missing scene contract, not the play session.

## Play Mode verification matrix

Automation has not yet caught a feel regression and will not. Each phase is played, not only tested.

| Phase | Must be played |
| --- | --- |
| 0 | Nothing changes behaviour; smoke test one gameplay mode |
| 1 | Free play, marker contests, points by distance, In The Pocket, CPU shooter, local multiplayer |
| 2 | One gameplay mode and one menu flow — the split should change nothing |
| 3 | Human, CPU shooter, CPU defense, local multiplayer |
| 4 | Human, CPU shooter, CPU defense, enemy, bodyguard, racing |
| 5 | Desktop keyboard, controller, touch/mobile, every menu screen |
| 6 | Every mode touched by a migrated contract |

## Review pass

- **Why not the assembly split first?** It is blocked by the cycle. Attempting it first means drawing
  module boundaries around the tangle and cementing it.
- **Why not collapse the duplicates first?** Most visible, most tempting. But merging two 1,000-line
  controllers with no ability to test either is how a silent behaviour change ships. Phase 2 makes
  Phase 3 survivable.
- **Why is input late when it caused the worst bug?** The blocking bug is fixed; what remains is
  cleanup. Pull it forward if it bites again.
- **What could still make this wrong?** Phase 1b assumes a `GameStats` facade can hold the line while
  consumers migrate. If scoring or win conditions read through it in ways the facade cannot preserve,
  stop and re-plan rather than push through.

## Rejected

- ECS, a DI container, a service locator, a wholesale input rewrite.
- Interfaces or abstractions introduced before a second implementation exists.
- Any big-bang restructure.

## Appendix: how these were measured

Taken 2026-08-17 against `dev`, excluding `Legacy~` throughout. Third-party folders, `Assets/Tests`
and editor-only assemblies are excluded from the coupling table.

    # .cs files and asmdefs under Assets/Scripts
    find Assets/Scripts -name "*.cs" -not -path "*Legacy~*" | wc -l    # 218
    find Assets/Scripts -name "*.asmdef" | wc -l                       # 3, plus Level5.Core = 4

    # live scene searches, comment lines excluded
    grep -rn "GameObject.Find\|FindWithTag\|FindGameObjectsWithTag" \
      --include=*.cs Assets/Scripts | grep -v "Legacy~" | grep -vE ":\s*//" | wc -l   # 124

    # which package references are asmdef-based
    ls Library/ScriptAssemblies/*.dll     # asmdef-based; anything absent is precompiled or engine

The coupling table maps every declared type to its owning top-level folder under `Assets/Scripts`,
then counts foreign type mentions per file with comments stripped. It counts mentions rather than
unique symbols, so it indicates coupling weight rather than an exact dependency count.
