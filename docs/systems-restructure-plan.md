# Systems Restructure Plan

Last updated: 2026-08-20 (Phase 2 detailed)

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
asmdef-based and which are precompiled. The 2a canary (below) proved this table rather than
inferring it, and corrected one entry: `Analytics` does not need to be listed. The runtime code that
looked like it needed it (`AnaylticsManager.cs`) uses `UnityEngine.Analytics.*`, which is the
`UnityAnalyticsModule` engine module and is referenced automatically; the asmdef-based `Analytics`
assembly is a separate, Editor-only assembly (`com.unity.analytics`'s Editor window code,
`includePlatforms: ["Editor"]`) that no runtime code actually touches.

| Reference | Kind |
| --- | --- |
| `Unity.InputSystem`, `Unity.Mathematics` | asmdef assemblies — must be listed |
| `Newtonsoft.Json`, `Mono.Data.Sqlite`, `UnityEngine.Analytics` (`UnityAnalyticsModule`) | precompiled or engine — automatic |
| `Unity.IO.LowLevel.Unsafe` | unused — a dead `using` in `AutoPlayerDefense.cs` with no API calls; no production type currently needs it |

**2a result (2026-08-20):** `Level5.AssemblyFeasibility` compiled clean headless on 6000.5.7f1
referencing only `Level5.Core`, `Unity.InputSystem` and `Unity.Mathematics`, while a probe method
also called `Newtonsoft.Json.JsonConvert`, `Mono.Data.Sqlite.SqliteConnection` and
`UnityEngine.Analytics.Analytics.CustomEvent` with no extra reference declared. Full EditMode
(463/463) and PlayMode (9/9) suites passed with the canary present; both passed again after it was
deleted. No production runtime source has moved assemblies yet.

Phase 2 establishes enforceable compile-time boundaries around Level5 runtime code without changing
gameplay, scenes, prefabs, rendering, input behaviour, or public runtime behaviour. It is
structurally invisible: URP assets, renderer data/features, volume profiles, post-processing,
shaders, materials, lighting, cameras, scenes, prefab behaviour and import settings do not change. A
visual difference during verification is a regression.

Phase 2 begins only after the Phase 1d ratchet is in place as the current game-manager boundary
guard. Remaining player↔basketball coupling does not block the 2a spike, but cyclic dependencies may
block specific 2b production assembly boundaries.

#### 2a — Runtime assembly feasibility canary

Do not migrate a production gameplay folder as the spike. Create a temporary, disposable runtime
assembly (e.g. `Level5.AssemblyFeasibility`) whose only purpose is to prove the exact
assembly-reference behaviour the real migration needs:

- runtime assembly, not Editor-only; `autoReferenced: true`; `overrideReferences: false` initially
- references `Level5.Core` where useful, and explicitly references every asmdef-based package
  assembly being proven
- exercises representative public types from those assemblies in compile-only probe code
- exercises representative precompiled/plugin dependencies without explicitly adding them, where
  Unity should auto-reference them

At minimum, verify the package assumptions already recorded above: `Unity.InputSystem`,
`Unity.Mathematics`, the Analytics assembly this project uses, Newtonsoft.Json and
Mono.Data.Sqlite automatic/precompiled reference behaviour, and `Unity.IO.LowLevel.Unsafe` if a
production runtime candidate needs it. Do not add URP, Cinemachine, TMP, Addressables or other
package references merely because the packages exist — only if relevant to a planned production
assembly or needed to verify an unresolved assembly name.

With the canary active: launch the pinned Unity version in batch mode, force script compilation and
fail on any compiler error, run repository validation, and run the full EditMode and PlayMode
suites. Record the exact successful assembly names, whether each dependency is asmdef-based or
precompiled, and any plugin/platform constraints discovered. Then delete the canary and confirm the
project returns to a green baseline.

**2a exit:** headless compilation succeeds with the canary active; package assembly names and
precompiled-reference assumptions are proven rather than inferred; full EditMode and PlayMode suites
are green; the canary is removed; no production runtime source has moved assemblies yet.

#### 2b0 — Production assembly boundary gate

Before adding an asmdef around any production folder, calculate its actual dependency closure.
Build a source-to-assembly dependency graph covering the Level5 runtime code intended for migration.
For every proposed production assembly, determine: source files it owns; other Level5 assemblies it
requires; remaining dependencies on `Assembly-CSharp`; package asmdef references; precompiled/plugin
references; Editor-only dependencies; platform-specific constraints; unsafe-code requirements;
generated-code ownership; serialization/reflection risks. Compute strongly connected components.

A proposed production assembly may be created only when: it does not require a type remaining in
`Assembly-CSharp`; its dependency graph is acyclic; all direct custom-assembly references can be
declared explicitly; runtime source does not depend on an Editor assembly; its recursive folder
ownership is understood.

Do not treat the Phase 1d ratchet as proof the entire original three-way cycle is gone. Remeasure
player→basketball, basketball→player, player→game manager, game manager→player, basketball→game
manager and game manager→basketball specifically. If player and basketball still form a strongly
connected component, do not create separate player and basketball asmdefs yet — prefer the smallest
behaviour-preserving dependency inversion that removes the remaining cycle, and do not create a
permanent coarse `Level5.Gameplay` assembly solely to hide a cyclic design unless that architecture
is chosen for independent reasons.

**Remeasured (2026-08-20), against `dev` at `3c15cd47c` (Phase 1d landed):** still a strongly
connected component. The `game manager → player`/`game manager → basketball` direction is the one
Phase 1d actually addressed, and `Level5GameManagerEdgeTests` (the ratchet) is authoritative for it
rather than a fresh grep: it allowlists exactly 5 files —
`GameLevelManager.cs`, `GameRules.cs`, `MatchHudPresenter.cs`, `SpawnCoordinator.cs`, `Pause.cs` —
each with a documented reason (roster/spawn-identity bookkeeping, a `GameStats` read that is
load-bearing for `getExperienceGainedFromSession()` and the persistence layer, or the deferred
HUD-polling design pass). Reduced from the plan's raw ~68/~46 count, but not zero, and not eliminated
— narrowed and pinned. The reverse direction (`player → game manager`, `basketball → game manager`)
was never in Phase 1d's scope and remains extensive: a rough re-run of the coupling method (declared
types per folder, whole-word matches, comment lines excluded) found non-trivial mentions in both
directions for all three pairs (`player ↔ basketball`, `player ↔ game manager`,
`basketball ↔ game manager`). **Conclusion: player, basketball and game manager remain one strongly
connected component. None of the three gets its own production asmdef in this pass of 2b** — 2b picks
a leaf candidate outside this triangle instead.

Before changing the assembly identity of production types, search for `[SerializeReference]`,
`Type.GetType`, `Assembly.Load`/`Assembly.LoadFrom`, assembly-qualified type-name strings, Newtonsoft
`TypeNameHandling`, custom reflection serializers, and persistence code that records managed type
names. If none exist for the candidate types, record that result; if they do, add migration/
regression coverage before moving those types.

**2b0 exit:** every production asmdef candidate has an explicit, acyclic dependency manifest.

#### 2b — Incremental runtime assembly migration

Migrate runtime code leaf-first. The current top-level folder layout is not assumed to equal the
desired assembly architecture — boundaries follow dependencies and ownership. For each assembly:
choose one dependency-closed candidate; add its asmdef without unrelated refactoring; keep source
paths and `.meta` files stable wherever practical; preserve namespaces, serialized fields and
component contracts; set `autoReferenced: true` and start with `overrideReferences: false`; add only
required asmdef-to-asmdef/package references; compile immediately; run relevant focused tests, then
the full EditMode suite, then the full PlayMode suite; do not begin the next assembly until green.
One assembly boundary should be independently revertible.

**Slice 1 — `Level5.Input` (2026-08-20).** `Assets/Scripts/input` is not a clean leaf as a whole (10
of its 13 files reach into `game manager`/menu/player types); a 3-file subset was
(`PlayerControls.cs` — the generated Input Actions wrapper, `PlayerControlsProvider.cs`,
`PlayerTouchInputState.cs`), referencing only `Unity.InputSystem` and `UnityEngine`. Moved with their
`.meta` files (GUIDs preserved) into a new `Assets/Scripts/input/Level5Input/` sub-folder — the
disqualified 10 files stay put in `Assets/Scripts/input`, still in `Assembly-CSharp`. None of the
three declares a `MonoBehaviour`, so no scene/prefab component reference was at risk; all three are in
the global namespace already, so no consumer call site changed. `Level5.Input.asmdef`:
`autoReferenced: true`, `overrideReferences: false`, references `["Unity.InputSystem"]`. Headless
compile clean; full EditMode 463/463 and PlayMode 9/9 both passed with the new asmdef present;
`validate-repository.ps1` passed. Consumers (`Pause.cs`, `GameLevelManager.cs`,
`PlayerController.cs`, `SniperCameraController.cs`, `RacingGameManager.cs`,
`UserAccountManager.cs`, `ProgressionManager.cs`, and the two `StartScreen*` menu files) needed no
changes — `Assembly-CSharp` auto-references it the same way it already does `Level5.Core`.

**Slice 2 — `Level5.Combat` (2026-08-20).** Same pattern: 7 of `combat`'s 9 files
(`ICombatAgent.cs`, `ICombatDetection.cs`, `IDamageable.cs`, `CombatReservation.cs`,
`CombatTacticalState.cs`, `CombatTargetSelector.cs`, `DamageInfo.cs`) reference only `System`/
`UnityEngine`; the other two (`ActorHealth.cs` — `MatchRuntime.Rules.Hardcore`; `CombatCredit.cs` —
`GameLevelManager`, `PlayerIdentifier`, `GameStats`, `BasketBall`) stay in `Assembly-CSharp`. Moved
into `Assets/Scripts/combat/Level5Combat/` with `.meta` files intact. None of the seven is a
`MonoBehaviour` (two interfaces, two structs, one enum, two static classes) — no scene/prefab
component reference at risk. `Level5.Combat.asmdef`: no references needed at all — pure
`System`/`UnityEngine`. Headless compile clean; full EditMode 463/463 and PlayMode 9/9 passed;
`validate-repository.ps1` passed. Consumed by `enemy`, `bodyguard` and `player`, none of which
needed changes.

**Slices 3-9 (2026-08-20), one verification pass covering seven independent single-domain leaves.**
Each was hand-verified file-by-file (full contents read, not just `using` statements — this project
mostly doesn't use namespaces, so a grep for `using` misses same-namespace/global references) before
moving, after a candidate the automated sweep called clean turned out not to be: `versus/VersusRuntime.cs`
instantiates `FileVersusSeriesRepository`, which calls `AtomicFile.WriteAllText`, and `AtomicFile` is
declared in `Assets/Scripts/player/CharacterProgressStore.cs` — inside the blocked triangle. Dropped
that candidate; kept the following seven, each moved into a new `Level5<Name>/` sub-folder with
`.meta` files intact, `autoReferenced: true`:

| Assembly | Files (leaf subset only) | References |
| --- | --- | --- |
| `Level5.Enemy` | `EnemyAttackBox.cs` (MonoBehaviour, primitive fields only) | none |
| `Level5.PlayerRacing` | `RacingVehicleProfile.cs` (MonoBehaviour, primitive fields only) | none |
| `Level5.Vehicle` | `VehicleMove.cs` (MonoBehaviour, `UnityEngine` only) | none |
| `Level5.MenuProgression` | `MatchProgressionResult.cs` (`[Serializable]`, `System` only, no Inspector exposure found) | none |
| `Level5.Utility` | `LegacyAchievementRecord.cs`, `LegacyNextSceneMarker.cs`, `SceneObjects.cs`, `UtilityFunctions.cs` (uses `PercentChance`) | `Level5.Core` |
| `Level5.Misc` | `ConfirmDialogue.cs`, `FPSDisplay.cs`, `PlayerTips.cs`, `SunglassesCollision.cs`, `TheyLiveManager.cs` (mutually self-contained: `SunglassesCollision` reads `TheyLiveManager.instance`) | none |
| `Level5.Models` | `ServerMessageModel.cs`, `UserReportModel.cs` (`System` only) — `HighScoreModel.cs` in the same folder stays put, still reaching for `GameStats`/`MatchRuntime`/`PlayerIdentifier`/`GameOptions` | none |

None of the MonoBehaviour types here (`EnemyAttackBox`, `RacingVehicleProfile`, `VehicleMove`,
`LegacyAchievementRecord`, `LegacyNextSceneMarker`, `ConfirmDialogue`, `FPSDisplay`,
`SunglassesCollision`, `TheyLiveManager`) is referenced by a `[SerializeField]` on another
still-in-`Assembly-CSharp` type — checked by grep before moving — so no prefab/scene component
reference was put at risk beyond the GUID-preservation the `.meta` move already guarantees. Headless
compile clean; full EditMode 463/463 and PlayMode 9/9 both passed with all seven present;
`validate-repository.ps1` passed. No consumer file needed a change.

**Slice 10 — `Level5.MenuStart` (2026-08-20), and a correction to the automated sweep.** The sweep
that found slices 3-9 also named an 8-file `menu_start` subset as clean
(`CheerleaderProfile.cs`, `EndRoundData.cs`, `LevelCatalog.cs`, `LevelPreset.cs`, two
`player_select/*` files, `StartMenuSelectionState.cs`, `StartMenuUiObjects.cs`). Hand-verifying it
file-by-file (full contents, not a `using`-statement scan — this project mostly doesn't namespace
its code) found half of it wrong: `EndRoundData.cs` holds a `List<LevelSelected>` field;
`LevelPreset.cs` and `LevelCatalog.cs` both take a `LevelSelected` parameter; and `LevelSelected.cs`
itself (`Assets/Scripts/menu_start/LevelSelected.cs`, not in the proposed set) reaches
`cpuPlayer.GetComponent<CharacterProfile>()` — `CharacterProfile` is declared in the blocked `player`
folder, so all three are transitively blocked. `StartMenuSelectionState.cs` reaches `GameOptions`
(`Assets/Scripts/menu_start/GameOptions.cs`, the legacy global config class
`Level5MatchArchitectureTests` is already migrating call sites off) directly. The four
`player_select/*` files reach `CharacterProfile`, `EndRoundData`, `PlayerData` and `LoadedData` —
all blocked. Only `CheerleaderProfile.cs` and `StartMenuUiObjects.cs` survive: both `MonoBehaviour`s
with `UnityEngine`/`UnityEngine.UI`-typed fields only, no other type referenced. Moved into
`Assets/Scripts/menu_start/Level5MenuStart/`; `Level5.MenuStart.asmdef` needs no references. Headless
compile clean; full EditMode 463/463 and PlayMode 9/9 passed; `validate-repository.ps1` passed.

The lesson for any further sweep: trust an automated "ruled out" finding (it cites a concrete
disqualifying reference, easy to verify) more than an automated "this is clean" finding (an absence
claim, and the one place it was checked by hand instead of trusted, it missed a two-hop chain through
a getter property). Read full file contents before moving anything, every time.

**Running total after slices 1-10:** 34 files across 10 leaf assemblies (`Level5.Input`,
`Level5.Combat`, `Level5.Enemy`, `Level5.PlayerRacing`, `Level5.Vehicle`, `Level5.MenuProgression`,
`Level5.Utility`, `Level5.Misc`, `Level5.Models`, `Level5.MenuStart`) plus the 4 pre-existing ones
(`Level5.Core`, `Level5.Constants`, `Level5.Pooling`, `Level5.Audio`) — 14 production runtime
assemblies total, out of roughly 218 `.cs` files in `Assets/Scripts` before this phase started. The
remainder is either inside the blocked player/basketball/game-manager triangle, or reaches into it
(directly or
transitively) and so is blocked the same way `versus`/`analytics`/`Models/HighScoreModel` were.

Prohibited in Phase 2: controller convergence, player/CPU behaviour cleanup, locomotion changes,
input ownership changes, scene-search removal, namespace restructuring, API redesign, new service
layers, DI/service locators, shader/material changes, URP configuration changes, and scene or
environment polishing. If a dependency must be inverted solely to make an intended boundary legal,
make the smallest possible behaviour-preserving change and protect it with a dependency regression
test.

#### 2c — Normalize gameplay PlayMode tests

The asmdef-free `Assets/Tests/PlayModeGameplay` workaround remains until the runtime code it tests is
available through proper asmdef references. Only once the required gameplay runtime assemblies
exist: give gameplay PlayMode tests explicit references to those assemblies; move/consolidate them
under the normal PlayMode test assembly structure as appropriate; remove the asmdef-free
`Level5GameplayPlayModeTests` workaround; run the entire PlayMode suite. The workaround disappearing
is an exit consequence of 2b, not an early migration step.

**Checked (2026-08-20): still blocked, correctly.** All four files in the workaround folder
(`Level5GameplayPlayModeTests.cs`, `BasketballVisibilityTests.cs`, `GameplayLevelUnpauseTests.cs`,
`PlayerMovementPhysicsTests.cs`) instantiate or look up `GameStats`, `MatchController`,
`PlayerController`, or `BasketBall` directly - all inside the blocked player/basketball/game-manager
triangle. None of slices 1-10 touched that triangle (2b0's gate forbids it), so none of this phase's
migrated assemblies are what these tests need. 2c cannot complete until the triangle itself is cut,
which is not this phase's work - see 2b0's remeasurement above.

#### 2d — Architecture guards and exit verification

Add or extend tests asserting: intended runtime source no longer falls back into `Assembly-CSharp`;
no forbidden custom-assembly dependency cycle exists; runtime assemblies do not reference
Editor-only assemblies; new assemblies declare their required custom/package dependencies; assembly
allowlists cannot silently grow; the asmdef-free gameplay PlayMode workaround no longer exists.

**Landed (2026-08-20):** `Assets/Tests/Editor/Level5ProductionAssemblyBoundaryTests.cs`, asmdef-free
like `Level5GameManagerEdgeTests` so it can read every folder as text without joining the dependency
graph it checks. Two guards, covering every current and future production asmdef automatically
(discovered at run time from every non-test `.asmdef` under `Assets/Scripts` and `Assets/Level5`, not
a hand-maintained list):

- `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` - collects every top-level `public` type
  declared outside a production asmdef folder, then fails if any migrated file's identifiers hit that
  set.
- `NoProductionAssemblyReferencesAKnownEditorOnlyPackageAssembly` - fails if a production `.asmdef`
  lists `"Analytics"` (the 2a-canary-confirmed Editor-only package assembly).

Getting the first guard right took two real fixes, not just tuning: restricting the "Assembly-CSharp
declared types" collection to unindented `public` declarations only (a private nested
`StatsManager.mode` class was colliding with every unrelated local variable named `mode` across the
codebase - nested/non-public types can't be reached by a bare identifier from another assembly at
all, so they were never real hits); and stripping string/char literals **before** comments, not after
- `Constants.cs`'s `"https://localhost:44362/..."` API-address constants contain `//`, and stripping
comments first misread that as a comment start, truncating the string literal and desyncing every
quote-pairing for the rest of the file, which was *why* a `"CharacterProfile"` table-name string
survived stripping and read as a real reference. Both fixes are recorded in the test file's own
comments. Full EditMode 465/465 (463 + these 2) and PlayMode 9/9 both passed with the guards active;
`validate-repository.ps1` passed.

**2d exit, checked against current state:** intended runtime source (the 10 slices) does not fall
back into `Assembly-CSharp` - guarded, green. No forbidden cycle among production assemblies -
trivially true today (none of the 10 new leaves reference each other, only `Level5.Core`/packages).
Runtime assemblies don't reference a known Editor-only assembly - guarded, green. New assemblies
declare required references explicitly - true by construction (`Level5.Utility` → `Level5.Core`,
`Level5.Input` → `Unity.InputSystem`, the rest need none). **Not reachable this phase:** the asmdef-
free gameplay PlayMode workaround is still present (2c, above) - this is the one 2d exit item that
depends on cutting the player/basketball/game-manager cycle, which 2b0 correctly keeps out of scope
here.

**Full Phase 2 exit is therefore not reached in this pass**, and that is the expected outcome given
2b0's gate, not a shortfall: 14 production runtime assemblies now exist (10 new + 4 pre-existing),
the migrated portion of the graph is acyclic and guarded against regrowth, and everything not moved
is either inside the blocked triangle or reaches into it. Finishing Phase 2 - removing the
`Level5GameplayPlayModeTests` workaround and migrating `player`/`basketball`/`game manager` themselves
- requires first inverting or cutting that remaining cycle, which is a Phase-1-scale slice of its own
(see Phase 1's own slicing for the shape that work took), not a continuation of leaf-picking.

**Exit:** production gameplay code targeted by this phase lives in proper referenced assemblies; the
runtime assembly graph is acyclic; no migrated assembly depends on `Assembly-CSharp`; package
references are explicit where Unity requires them; full repository validation, Unity batch
compilation, and the full EditMode/PlayMode suites pass; the asmdef-free gameplay PlayMode workaround
is gone; one representative gameplay mode and one representative menu flow pass manual Play Mode
verification; gameplay and visuals are observably unchanged.

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
