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

**Correction (code review, 2026-08-21):** `CheerleaderProfile` *is* referenced by `[SerializeField]`
elsewhere - `StartManager.cs`, `LoadedData.cs` and `LoadManager.cs` each hold a
`[SerializeField] private List<CheerleaderProfile> ...` field, split across two lines. The single-
line grep pattern used for slices 3-9 (`SerializeField.*TypeName` on one line) missed this shape.
Still safe: Unity serializes a `List<T>` of `UnityEngine.Object`-derived elements (which
`CheerleaderProfile`, a `MonoBehaviour`, is) by each element's GUID/fileID, not by an
assembly-qualified type name, so moving which assembly the type compiles into doesn't touch the
reference - confirmed independently by a full GUID diff across all 27 moved `.meta` files (unchanged)
and a repo-wide `asm: Assembly-CSharp` grep across every `.unity`/`.prefab`/`.asset` (zero hits). The
finding is a documentation-accuracy correction, not a functional regression: nothing needs to move
back, but "checked by grep before moving" for the earlier slices should be read as "checked with a
same-line pattern," not as a proof of absence for multi-line attribute/field pairs.

The lesson for any further sweep: trust an automated "ruled out" finding (it cites a concrete
disqualifying reference, easy to verify) more than an automated "this is clean" finding (an absence
claim, and the one place it was checked by hand instead of trusted, it missed a two-hop chain through
a getter property). Read full file contents before moving anything, every time.

**Slice 11 — `Level5.Basketball` (2026-09-04), the first leaf out of the former player/basketball/game-manager
triangle.** The 2026-08-20 remeasurement above found player, basketball and game manager still one strongly
connected component. Between then and this slice, a separate run of `AUD-010` Phase 2b0 (PRs #94-#99: binding
`BasketBallShotMarker` to `ResolvedMatchRules`, `BasketBall`/`BasketBallAuto` shot telemetry to a callback,
moving `PickupObject` into `Level5.Misc`, and removing basketball's last live `BehaviorNpcCritical` reference)
cut basketball's outbound edges into `Assembly-CSharp` to zero without moving basketball itself into an asmdef
yet — leaving basketball a clean leaf even though `player` and `game manager` remain coupled to each other and
to basketball's own former call sites. This slice is `AUD-012` Phase 2b: give `Assets/Scripts/basketball`
(12 production `.cs` files plus the ignored `Legacy~/` folder, fully commented out) its own asmdef, source files
left in place.

Re-verified against `dev` at `248648d0b` (PR #99 landed) rather than trusting the 2026-08-20 measurement: every
live (non-comment, non-XML-doc) identifier in `Assets/Scripts/basketball/*.cs` resolves to `Level5.Core`
(including the `Level5.Core.Match` sub-namespace — one asmdef, not a separate assembly), `Level5.Utility`
(`UtilityFunctions.FindDeepChild`/`RollPercent`, the copy under `Utility/Level5Utility/`, not the same-named
loose files still in `Assembly-CSharp`), `Level5.Audio` (`SFXBB`), `Level5.Constants` (`Constants.DISTANCE_*`)
or `Level5.Misc` (`PickupObject`, moved there by PR #98). Every remaining mention of `GameLevelManager`,
`SpawnCoordinator`, `GameRules`, `MatchRuntime`, `AnaylticsManager`, `BehaviorNpcCritical` or `PlayerController`
inside the basketball folder is inside an XML-doc `<see cref>`/`<c>` tag or a commented-out line — grepped
individually, none is live code. Reverse edge also checked (new for this slice, since this is the first
migrated assembly with external `Assembly-CSharp` consumers reaching into it): 32 files outside
`Assets/Scripts/basketball` reference a basketball type (`SpawnCoordinator`, `GameRules`, `GameLevelManager`,
`PlayerController`, `AutoPlayerController`, `versus/*`, `database/*`, etc.) — counted by excluding
basketball's own 13 self-referencing files (12 production files plus the ignored `Legacy~` one) from the
raw 45-file grep match. Basketball declares no `internal`
member (one stale comment mentions a former `internal set`, already gone), no `protected` member, and no
external type inherits from a basketball type — every top-level basketball type is `public` — so none of those
32 external call sites relies on same-assembly visibility the new boundary would break. Assembly-sensitive type
identity checked and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`,
`AssemblyQualifiedName` or `TypeNameHandling` touches a basketball type anywhere in the repo.

`Level5.Basketball.asmdef`: `autoReferenced: true`, `overrideReferences: false`, references
`["Level5.Core", "Level5.Utility", "Level5.Audio", "Level5.Constants", "Level5.Misc"]` — no `Unity.ugui`
reference needed despite basketball's `UnityEngine.UI` usage, consistent with every other Phase 2 slice's
`autoReferenced` cascade. Headless Unity 6000.5.7f1 compile clean (`Level5.Basketball.dll`, 144 defines, 300
references, zero new `CS` errors; the only warnings anywhere in the log are pre-existing `CS0618
FindObjectsSortMode` obsolete-API notices in unrelated PlayMode/EditMode test files). Added
`BasketballProductionTypesCompileIntoLevel5Basketball` to `Level5ProductionAssemblyBoundaryTests.cs`
(`typeof(BasketBall)`/`typeof(GameStats)`/`typeof(BasketBallShotMarker)`'s assembly name now asserted equal to
`"Level5.Basketball"`); that fixture's two pre-existing text-scan guards already cover `Level5.Basketball`
automatically, since they discover every non-test `.asmdef` under `Assets/Scripts`/`Assets/Level5` at run time
rather than from a hand-maintained list. Focused EditMode run (this fixture only): 3/3 passed. Ran the existing
`Level5BasketBallShotTelemetryCompositionPlayModeTests` fixture — which drives the real
`SpawnCoordinator.GiveBall` → basketball composition path, not a hand-built substitute — as the one real
composition-path check: 1/1 passed. Per this repository's risk-based validation policy, the full EditMode/
PlayMode suites were not re-run for an assembly-boundary-only change with no behavior modification; PR CI owns
that broader regression coverage.

This does **not** close `AUD-012`/Phase 2 as a whole, and does not by itself unblock 2c: `player` and
`game manager` still form a cycle with each other (and still hold the call sites basketball used to reach into,
now removed from basketball's side only), so the asmdef-free `Level5GameplayPlayModeTests` workaround — whose
four files also instantiate `GameStats`/`MatchController`/`PlayerController` directly, not just `BasketBall` —
remains blocked on migrating `player`/`game manager` themselves, unchanged from the 2026-08-20 finding.

**Slice 12 — `Level5.Match` (2026-09-05), one file out of `game manager`.** `MatchController.cs`
(`Assets/Scripts/game manager/MatchController.cs`) is the scene-facing wrapper around `MatchLifecycle`
(`Level5.Core.Match`, already in `Level5.Core`): it owns the `MatchController.instance` singleton, the
`Ending`/`Completed` events, and the first-request-wins `RequestEnd` door, with no lifecycle logic of its
own. Its only live identifiers are `System`, `UnityEngine` and `Level5.Core.Match` types
(`MatchLifecycle`, `MatchPhase`, `MatchEndCause`, `MatchEndReason`); every historical mention of
`GameLevelManager`, `GameRules`, `MatchRuntime`, `LevelRuntimeContext`, a player type or a basketball
type lives only in this file's own XML-doc comments, not in code. That made it a second dependency-closed
leaf out of the game-manager folder even though `player`/`game manager` remain a mutually coupled pair
overall — same shape as Slice 11 pulling `Level5.Basketball` out of the triangle without the triangle
itself being cut.

Consumer accessibility checked against the two production call sites: `GameRules.cs` only holds a
private `MatchController matchController` field and calls `FindAnyObjectByType<MatchController>()` /
`AddComponent<MatchController>()`; `LevelRuntimeContext.cs` only exposes its own field through a public
`MatchController MatchController` property and calls `GetComponent`/`FindAnyObjectByType`. Both use
already-public cross-assembly members, so no visibility change was needed. Assembly-sensitive type
identity checked and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`,
`AssemblyQualifiedName` or `TypeNameHandling` touches `MatchController` anywhere in the repo; the
Editor-test files that mention it (`Level5BasketballShotMarkerSessionTests.cs`,
`Level5BasketballMoneyBallStateTests.cs`, `Level5BasketballMarkerOwnershipTests.cs`,
`Level5BasketballShotPipelineTests.cs`) do so only in comments. `MatchController.cs.meta`'s GUID
(`07d257b14f484874ab1d0e5ff085d777`) was confirmed before the move and is unchanged after it — verified
by reading the `.meta` post-move rather than assuming `git mv` preserved it.

Moved `MatchController.cs`/`.cs.meta` together into a new `Assets/Scripts/game manager/Level5Match/`
sub-folder (not the `game manager` root, which would recursively pull in every still-coupled file in that
folder). `Level5.Match.asmdef`: `autoReferenced: true`, `overrideReferences: false`, references
`["Level5.Core"]` only. Headless Unity 6000.5.7f1 batch compile clean (`Level5.Match.dll`, 144 defines,
296 references, zero new `CS` errors). Added `MatchControllerCompilesIntoLevel5Match` to
`Level5ProductionAssemblyBoundaryTests.cs` (mirrors `BasketballProductionTypesCompileIntoLevel5Basketball`
for the new leaf); focused run of that fixture: 4/4 passed, including the two pre-existing text-scan
guards which cover `Level5.Match` automatically since they discover every non-test `.asmdef` at run
time. Ran the existing PlayMode singleton coverage
(`Level5GameplayPlayModeTests.ASceneScopedSingletonReleasesItsStaticWhenDestroyed`, which claims and then
destroys a real `MatchController` and asserts the static claims/clears correctly) as the one real
lifecycle check: 1/1 passed. Per this repository's risk-based validation policy, the full EditMode/
PlayMode suites were not re-run for an assembly-boundary-only change with no behavior modification; PR CI
owns that broader regression coverage.

This does **not** close `AUD-012`/Phase 2, and does not unblock 2c: `player` and `game manager` still
form a cycle with each other, and three of the workaround folder's files still instantiate or look up a
type that remains `Assembly-CSharp` directly — `Level5GameplayPlayModeTests.cs` (`VersusRuntime`/
`VersusMatchReporter`/`VersusCatalogs`/`ActiveVersusAttempt` in `Assets/Scripts/versus`, and
`ActiveMatch`/`MatchCatalogs` in `Assets/Scripts/menu_start` — not `GameStats`, which Slice 11 already
moved into `Level5.Basketball`), `BasketballVisibilityTests.cs` and `PlayerMovementPhysicsTests.cs`
(`PlayerController`) — so the asmdef-free workaround is still needed. See the updated 2c status below.

**Slice 13 — `Level5.Versus` (2026-09-06), two files out of `versus`.** `VersusCatalogs.cs` and
`DefaultCompetitiveRulesets.cs` (`Assets/Scripts/versus/`) are the versus folder's only
dependency-closed pair: `VersusCatalogs` loads authored `CompetitiveRulesetDefinition` assets from
`Resources/Versus/Rulesets` and falls back to `DefaultCompetitiveRulesets.CreateAll()` for any id an
asset doesn't cover; `DefaultCompetitiveRulesets` is the code-authored ruleset registry itself. Their
only live identifiers are `System.Collections.Generic`, `UnityEngine`, `Level5.Core.Versus`
(`CompetitiveRulesetCatalog`, `CompetitiveRuleset`, `CompetitiveRulesetDefinition`,
`VersusDomainException`, `RulesetId`, `ComparisonKey`, `AttemptMetric`, `VersusCapability`) and
`Level5.Core.Match` (`GameModeId`) — all already in `Level5.Core` — plus each other
(`VersusCatalogs.Build()` calls `DefaultCompetitiveRulesets.CreateAll()`). Every other file in the
folder (`ActiveVersusAttempt`, `FileVersusSeriesRepository`, `VersusRuntime`, `VersusLauncher`,
`VersusMatchReporter`, `GameStatsAttemptResults`) still reaches `ActiveMatch`, `AtomicFile`, or each
other and stayed in `Assembly-CSharp`, unchanged from the Phase 2b0 finding — consistent with Slice
11 and 12 pulling a single dependency-closed leaf out of a still-coupled folder rather than cutting
the folder itself.

Consumer accessibility checked against the three production call sites: `VersusRuntime.cs` reads
`VersusCatalogs.Rulesets` (public static property); `Assets/Scripts/Dev/VersusDevConsole.cs` calls
`VersusCatalogs.Rulesets.Supporting(...)`; `Assets/Tests/PlayModeGameplay/Level5GameplayPlayModeTests.cs`
(the asmdef-free 2c workaround) calls `VersusCatalogs.Reset()`. All three already used public
cross-assembly members, so no visibility change was needed. Assembly-sensitive type identity checked
and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`, `AssemblyQualifiedName`
or `TypeNameHandling` touches either type anywhere in the repo, and neither ruleset ids, versions, nor
any other persisted versus data are keyed on a type's assembly. `VersusCatalogs.cs.meta`'s GUID
(`f623cb04469457b4c832cbf29a2430d2`) and `DefaultCompetitiveRulesets.cs.meta`'s GUID
(`10289957d0f3a8143bfc06db00ac7753`) were confirmed before the move and unchanged after it, verified by
reading both `.meta` files post-move.

Moved both files/`.meta`s together into a new `Assets/Scripts/versus/Level5Versus/` sub-folder (not the
`versus` root, which is not dependency-closed). `Level5.Versus.asmdef`: `autoReferenced: true`,
`overrideReferences: false`, references `["Level5.Core"]` only — no reference to any other Phase 2b
leaf. Headless Unity 6000.5.7f1 batch compile clean (`Level5.Versus.dll`, 144 defines, 296 references,
zero new `CS` errors; the only warnings in the log are the same pre-existing `CS0618`
`FindObjectsSortMode`/`FindFirstObjectByType` obsolete-API notices and one pre-existing `CS0414` in
`GameRules.cs` seen in prior slices' logs). Added `VersusCatalogTypesCompileIntoLevel5Versus` to
`Level5ProductionAssemblyBoundaryTests.cs` (mirrors `MatchControllerCompilesIntoLevel5Match`); focused
run of that fixture plus the existing `Level5VersusRulesetTests` fixture (ruleset ids, versions,
capabilities, comparison ordering, and `DefaultCompetitiveRulesets`/catalog fallback behavior) passed
with no changes needed to either file — `Level5VersusRulesetTests` already exercised
`DefaultCompetitiveRulesets.CreateAll()` and the shipped registry directly, so this slice is proof the
same code now compiles into a different assembly with identical behavior. Per this repository's
risk-based validation policy, the full EditMode/PlayMode suites were not re-run for an
assembly-boundary-only change with no behavior modification; PR CI owns that broader regression
coverage.

This does **not** close `AUD-012`/Phase 2, and does not unblock 2c on its own: `Level5GameplayPlayModeTests.cs`
now resolves `VersusCatalogs.Reset()` through `Level5.Versus` (`autoReferenced`) instead of
`Assembly-CSharp`, but the same file still reaches `VersusRuntime`, `VersusMatchReporter` and
`ActiveVersusAttempt` directly, all three still `Assembly-CSharp` — so it stays on the blocked list.
See the updated 2c status below.

**Slice 14 — `AtomicFile` into the existing `Level5.Utility` (2026-09-06), one type out of
`CharacterProgressStore.cs`.** `AtomicFile` was a second, unrelated top-level `public static class`
declared at the bottom of `Assets/Scripts/player/CharacterProgressStore.cs` — a dependency-closed
filesystem primitive (temp-file write, `File.Replace`, `.bak` fallback on read) with no player or
gameplay dependency, used by `CharacterProgressStore` itself and, directly, by
`PendingMatchPersistenceStore`, `ProgressionResultStore`, `PendingProgressionStore` and
`FileVersusSeriesRepository`. Its own source used only `System`/`System.IO`. Type-identity checked
and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`, or
`AssemblyQualifiedName` touches it anywhere in the repo, and no persisted data is keyed on its
assembly.

Moved the class body verbatim into a new `Assets/Scripts/Utility/Level5Utility/AtomicFile.cs` (new
`.meta`, fresh GUID — there was no prior GUID to preserve since it was never its own asset) and
deleted it from the bottom of `CharacterProgressStore.cs`, which otherwise keeps its own file, `.meta`,
and behavior untouched. Global type name and public API (`TryReadAllText` both overloads,
`WriteAllText`) are unchanged, so every consumer — inside and outside `Assembly-CSharp` — kept
compiling with no call-site edits; `Level5.Utility` is already `autoReferenced: true`, so this needed
no manifest change. Headless Unity 6000.5.7f1 batch compile clean (`Level5.Utility.dll`, 144 defines,
296 references, zero new `CS` errors; same pre-existing `CS0618`/`CS0414` warnings as prior slices'
logs). Added `AtomicFileCompilesIntoLevel5Utility` to `Level5ProductionAssemblyBoundaryTests.cs`
(mirrors `MatchControllerCompilesIntoLevel5Match`); focused run of that fixture (6/6) plus the existing
`Level5CoreTests` `AtomicFile` backup/recovery/malformed-primary coverage (2/2) passed unchanged. Per
this repository's risk-based validation policy, the full EditMode/PlayMode suites were not re-run for
an assembly-boundary-only change with no behavior modification; PR CI owns that broader regression
coverage.

Post-extraction remeasurement of `FileVersusSeriesRepository` and `VersusRuntime` (`Assets/Scripts/versus/`),
named in Slice 13's finding as blocked on `AtomicFile`: with the `AtomicFile` edge gone,
`FileVersusSeriesRepository`'s only remaining live identifiers are `System`/`System.IO`, `UnityEngine`,
and `Level5.Core.Versus`/`Level5.Core.Versus.Persistence` (`IVersusSeriesRepository`,
`VersusSeriesSerializer`, `VersusSeriesDocument`, `VersusLog`) — all already outside `Assembly-CSharp` —
making it dependency-closed on its own for the first time. `VersusRuntime` still reaches
`FileVersusSeriesRepository` itself (`Assembly-CSharp`) plus `VersusCatalogs` (`Level5.Versus`) and
`VersusMatchCoordinator`/`IVersusSeriesRepository` (`Level5.Core.Versus`, both already migrated), so it
remains blocked only on `FileVersusSeriesRepository`, not on anything this slice didn't already clear —
the two are now a closed pair that could migrate together in a future slice. This is recorded as a
finding only; neither file is moved in this slice.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

**Slice 15 — `Level5.Versus` (2026-09-06), two more files out of `versus`.** `FileVersusSeriesRepository.cs`
and `VersusRuntime.cs` (`Assets/Scripts/versus/`) are the pair Slice 14's remeasurement identified as
newly dependency-closed once `AtomicFile` moved to `Level5.Utility`. Re-read both in full before moving
them: `FileVersusSeriesRepository`'s only live identifiers are `System`, `System.Collections.Generic`,
`System.IO`, `UnityEngine`, `Level5.Core.Versus`/`Level5.Core.Versus.Persistence`
(`IVersusSeriesRepository`, `VersusSeries`, `SeriesId`, `SeriesSummary`, `VersusSeriesDocument`,
`VersusSeriesSerializer`, `VersusLog`) and `AtomicFile` — all already outside `Assembly-CSharp`.
`VersusRuntime` reaches only `Level5.Core.Versus` (`IVersusSeriesRepository`, `VersusMatchCoordinator`,
`CompetitiveRulesetCatalog`), `FileVersusSeriesRepository` itself, and `VersusCatalogs` — the last two
already confirmed in `Level5.Versus`/moving into it together. Both closed with no remaining
`Assembly-CSharp` edge; the expected graph (`Level5.Versus` -> `Level5.Core`, `Level5.Utility` -> `Level5.Core`,
no reverse edge) held, confirmed by re-reading `Level5.Utility.asmdef` showing only a `Level5.Core`
reference.

Consumer accessibility checked against every current production and test call site (`Assets/Scripts/versus/VersusLauncher.cs`,
`Assets/Scripts/versus/VersusMatchReporter.cs`, `Assets/Scripts/Dev/VersusDevConsole.cs`,
`Level5VersusArchitectureTests.cs`, `Level5VersusPersistenceTests.cs`, `Level5VersusIntegrationTests.cs`,
`Level5GameplayPlayModeTests.cs`): every reference is `VersusRuntime.Coordinator`/`.Override`/`.Reset`
or `new FileVersusSeriesRepository(...)`, all already public (`VersusRuntime.Repository` has no current
call site). No visibility change was needed.
Assembly-sensitive type identity checked and clean: no `[SerializeReference]`, `Type.GetType`,
`Assembly.Load`/`LoadFrom`, `AssemblyQualifiedName`, or `TypeNameHandling` touches either type anywhere
in the repo; stored versus JSON holds `VersusSeriesDocument` domain data only, never a repository or
runtime type name. `FileVersusSeriesRepository.cs.meta`'s GUID (`bea14fe6bcc6a6348a74a42ab8bffd44`) and
`VersusRuntime.cs.meta`'s GUID (`be5c8fefbb1960c4c922eae7c2c93a96`) were confirmed before the move and
unchanged after it, verified by reading both `.meta` files post-move.

Moved both files/`.meta`s together into the existing `Assets/Scripts/versus/Level5Versus/` sub-folder
(the same one Slice 13 created), source bodies unchanged. Added `Level5.Utility` to
`Level5.Versus.asmdef`'s `references` (now `["Level5.Core", "Level5.Utility"]`) since
`FileVersusSeriesRepository` calls `AtomicFile.WriteAllText`; confirmed no reverse reference from
`Level5.Utility.asmdef` back to `Level5.Versus`. Headless Unity 6000.5.7f1 batch compile clean
(`Level5.Utility.dll` unchanged at 144 defines/296 references; `Level5.Versus.dll` now 144 defines/297
references — the one new edge to `Level5.Utility` — zero new `CS` errors, same pre-existing
`CS0618`/`CS0414` warnings as prior slices' logs). Added `VersusRuntimeTypesCompileIntoLevel5Versus` to
`Level5ProductionAssemblyBoundaryTests.cs` (mirrors `VersusCatalogTypesCompileIntoLevel5Versus`); focused
EditMode runs passed unchanged: the boundary fixture (7/7, including the new test), `Level5VersusPersistenceTests`
(21/21, including `FileRepositoryWritesAndReloadsSerializedSeries` and
`FileRepositoryPreservesArchiveFlagAcrossLaterSave`), and `Level5VersusIntegrationTests` (8/8, including
`TwoRealMatchesResolveAGameThroughTheWholeStack`, which exercises `VersusRuntime.Override` with an
in-memory repository across the new assembly boundary). Per this repository's risk-based validation
policy, the full EditMode/PlayMode suites were not re-run for an assembly-boundary-only change with no
behavior modification; PR CI owns that broader regression coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

**Slice 16 — `Level5.Versus` (2026-09-06), one more file out of `versus`.** `GameStatsAttemptResults.cs`
(`Assets/Scripts/versus/`) is the gameplay-to-versus result mapper: the one place that reads a finished
match's `GameStats` component and turns it into an `AttemptResult`. Re-read it in full before moving it:
its only live identifiers are `Level5.Core.Match` (`GameModeId`), `Level5.Core.Versus` (`AttemptResult`,
`AttemptMetric`, `RulesetId`) and `GameStats` itself — the last one still `Level5.Basketball` since Slice
11, not `Assembly-CSharp`. `Level5.Basketball.asmdef` was re-read and confirmed to hold no reference back
to `Level5.Versus`, so adding the new edge keeps the custom-assembly graph acyclic (`Level5.Versus ->
Level5.Basketball`, no reverse edge).

Consumer accessibility checked against the file's one production call site, `VersusMatchReporter.cs`
(`Assets/Scripts/versus/`, still `Assembly-CSharp`): it calls the public static `GameStatsAttemptResults.Build(...)`,
already public, so no visibility change was needed — `VersusMatchReporter` keeps compiling against the
moved mapper through the assembly boundary, `Level5.Versus` being `autoReferenced`. Assembly-sensitive
type identity checked and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`,
`AssemblyQualifiedName`, or `TypeNameHandling` touches the type anywhere in the repo, and no ruleset id,
ruleset version, or persisted versus data is keyed on its assembly. `GameStatsAttemptResults.cs.meta`'s
GUID (`7178539eef0d6f748b2d85607518dc34`) was confirmed before the move and unchanged after it, verified
by reading the `.meta` file post-move.

Moved the file/`.meta` together into the existing `Assets/Scripts/versus/Level5Versus/` sub-folder,
source body unchanged — no mapping, metric, shot-counter, completion-time-fallback, or null-stats logic
was touched. Added `Level5.Basketball` to `Level5.Versus.asmdef`'s `references` (now `["Level5.Core",
"Level5.Utility", "Level5.Basketball"]`) since the mapper's method signature takes a `GameStats`
parameter directly. Headless Unity 6000.5.7f1 batch compile clean, zero new `CS` errors. Added
`GameStatsAttemptResultsCompilesIntoLevel5Versus` to `Level5ProductionAssemblyBoundaryTests.cs` (mirrors
`VersusRuntimeTypesCompileIntoLevel5Versus`); focused EditMode runs passed unchanged: the boundary
fixture (8/8, including the new test) and `Level5VersusIntegrationTests` (10/10, including
`AMatchsStatsBecomeAComparableResult`, `AMakeCountModeIsMeasuredOnItsOwnShotRatherThanOnEveryShot`,
`AMatchWithNoStatsStillProducesAResultSoTheSeriesCanMoveOn`, and
`TwoRealMatchesResolveAGameThroughTheWholeStack`, which exercises the moved mapper end to end through
`VersusMatchReporter` across the new assembly boundary) — 18/18 combined in one filtered run confirming
both fixtures compile and pass across the new assembly boundary. Per this repository's risk-based
validation policy, the full EditMode/PlayMode suites were not re-run for an assembly-boundary-only
change with no
behavior modification; PR CI owns that broader regression coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below. The earlier Phase 2 statement (Slice 13) describing `GameStatsAttemptResults` as staying in
`Assembly-CSharp` "unchanged from the Phase 2b0 finding" is now superseded by this slice; no other
current Phase 2 text describes it as structurally blocked.

Post-migration remeasurement of `VersusMatchReporter.cs` (`Assets/Scripts/versus/`), the mapper's one
consumer: with the `GameStatsAttemptResults` edge now resolving through `Level5.Versus`, its remaining
live identifiers are `System`, `Level5.Core.Match` (`GameModeId`), `Level5.Core.Versus` (`AttemptResult`,
`SeriesId`, `AttemptId`, `ParticipantId`, `SubmissionOperation`, `VersusValidationCode`), `UnityEngine`,
`VersusRuntime`/`GameStatsAttemptResults` (both already `Level5.Versus`), and `GameStats` (already
`Level5.Basketball`) — all outside `Assembly-CSharp` except one: `ActiveVersusAttempt`
(`Assets/Scripts/versus/`), read for `IsActive`/`SeriesId`/`AttemptId`/`ParticipantId` and cleared on
every exit path. `ActiveVersusAttempt` is `VersusMatchReporter`'s only remaining `Assembly-CSharp`
blocker, confirmed by re-reading the file rather than assumed. This is recorded as a finding only;
`VersusMatchReporter` and `ActiveVersusAttempt` are not moved in this slice.

**Slice 17 — `Level5.Match` (2026-09-07), `MatchSession` becomes the actual owner of the current match
result id and moves into `Level5.Match`.** `GameOptions.matchResultId` (`Assets/Scripts/menu_start/GameOptions.cs`)
was a documented-but-unmigrated ownership entry (`docs/game-options-inventory.md`: `matchResultId ->
MatchSession`) — `MatchSession` already mediated every read/write of the field, but the field itself
was still the backing store. Re-checked every production reference to `GameOptions.matchResultId`
before touching it: only `MatchSession.cs` read or wrote it (`Assets/Scripts/menu_progression/MatchSession.cs`,
before the move); the one other hit was a test (`Level5CoreTests.cs`) saving/restoring it around
`MatchSession.BeginNewMatch()` calls. No serialized asset, save payload, reflection path, or
assembly-qualified type string depends on `MatchSession` living in `Assembly-CSharp`.

`MatchSession` now holds a private `static string currentResultId` and generates ids itself via a new
`MatchSession.CreateResultId(string prefix)`, moved out of `ProgressionService.CreateResultId` — the
same `<prefix>-<Guid.NewGuid():N>` construction, same null/empty-prefix-becomes-`"match"` rule, unchanged.
`ProgressionService.CreateResultId` stays as a one-line compatibility delegate onto
`MatchSession.CreateResultId` rather than being deleted: it still has a live caller inside
`ProgressionService.ApplyMatchResult` itself (the null-result fallback path) and a direct test
(`Level5CoreTests.ResultIdsAreUniqueAndKeepTheirPrefix`). There is exactly one id-generation
implementation after this slice, on `MatchSession`; `ProgressionService`'s progression-application,
idempotency, pending-queue, repair, database-access, and JSON-projection behavior were not touched.

`BeginNewMatch()`/`EnsureCurrentMatch()` keep their exact prior contracts (always-fresh id / return-if-present-else-create),
now reading and writing `currentResultId` directly instead of `GameOptions.matchResultId`. No reset
method, setter, or other test-only hook was added to `MatchSession` — `Level5CoreTests.MatchSessionRotatesResultIdForEachGameplayLoad`
was simplified to drop its now-unnecessary save/restore of the deleted field rather than gaining a
replacement hook; static state persisting across test runs was already true of the pre-migration
field and no other test depends on a reset value (confirmed by re-checking every `MatchSession`/`EnsureCurrentMatch`/`BeginNewMatch`
reference under `Assets/Tests`).

`GameOptions.matchResultId` is deleted outright — it had exactly one production consumer and that
consumer no longer needs it. `MatchSession.cs` came off `Level5MatchArchitectureTests`'s
`LegacyGameOptionsConsumers` allowlist (it no longer reaches for `GameOptions` at all), and
`GameOptionsGrowsNoNewMatchFields`'s ratchet was lowered from 60 to 59 — measured directly off the
current file (71 raw `static public`/`public static` matches, minus 9 that are commented-out fields
already excluded by `StripComments`, minus 2 methods = 60 before this slice, 59 after removing the
one field), not assumed.

Moved `MatchSession.cs`/`.cs.meta` (`git mv`, preserving history) from `Assets/Scripts/menu_progression/`
into `Assets/Scripts/game manager/Level5Match/`, alongside `MatchController.cs`. GUID
(`3f26ad31a7a14f56a2c5e31f0ee8c5c1`) confirmed unchanged post-move by re-reading the `.meta` file.
Global namespace, type name, and public API (`BeginNewMatch`, `EnsureCurrentMatch`, the new
`CreateResultId`) preserved. `MatchSession` no longer references `GameOptions` or `ProgressionService`
at all — only `System` (`Guid`) — so the existing `Level5.Match.asmdef` (`references: ["Level5.Core"]`)
needed no change; confirmed by the headless compile showing `Level5.Match.dll` still at 144
defines/296 references, unchanged from Slice 12's boundary creation. Headless Unity 6000.5.7f1 batch
compile clean, zero new `CS` errors. Added `MatchSessionCompilesIntoLevel5Match` to
`Level5ProductionAssemblyBoundaryTests.cs` (mirrors `MatchControllerCompilesIntoLevel5Match`); focused
EditMode runs passed unchanged: the boundary fixture, `Level5MatchArchitectureTests` (including
`GameOptionsGrowsNoNewMatchFields`, `TheAllowlistHasNoStaleEntries`, `NoNewFileReachesForGameOptions`),
and the result-id coverage in `Level5CoreTests` (`ResultIdsAreUniqueAndKeepTheirPrefix`,
`MatchSessionRotatesResultIdForEachGameplayLoad`) — 37/37 assertions across the three fixtures, zero
failures. Per this repository's risk-based validation policy, the full EditMode/PlayMode suites were
not re-run for an ownership-migration-plus-assembly-boundary change with no externally observable
lifecycle behavior change; PR CI owns that broader regression coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

Post-migration remeasurement of `ActiveMatch.cs` (`Assets/Scripts/menu_start/`), `MatchSession`'s
main production caller: re-read in full, its only live identifiers are `UnityEngine`,
`Level5.Core.Match` (`MatchConfiguration`, `LevelDefinition`, `MatchConfigurationBuilder`), and
`MatchSession` — all now outside `Assembly-CSharp`. `ActiveMatch` has zero remaining `Assembly-CSharp`
dependency after this slice and is eligible for the next Phase 2b slice. This is a diagnostic finding
only; `ActiveMatch` is not moved in this PR, and neither are `ActiveVersusAttempt`,
`VersusMatchReporter`, `MatchCatalogs`, or `PlayerController`.

**Slice 18 — `Level5.Match` (2026-09-07), `ActiveMatch` moves into the assembly whose eligibility
Slice 17's remeasurement found.** `ActiveMatch.cs` (`Assets/Scripts/menu_start/`) is the authoritative
current-match configuration: a launch source calls `Begin` immediately before the scene load, and every
gameplay/versus/menu consumer downstream reads it. Re-read it in full before moving it: its only live
identifiers are `UnityEngine`, `Level5.Core.Match` (`MatchConfiguration`, `LevelDefinition`,
`MatchConfigurationBuilder`), and `MatchSession` — the last one already `Level5.Match` since Slice 17.
No live dependency on a type remaining in `Assembly-CSharp` was found, confirming Slice 17's
remeasurement.

Consumer accessibility checked against every production call site (`VersusLauncher.cs`, `EndRoundMenuManager.cs`,
`StartManager.cs`, `Utility/LoadGame.cs`, `game manager/MatchRuntime.cs`, `versus/ActiveVersusAttempt.cs`):
all of them call only the existing public members (`Begin`, `Configuration`, `IsActive`, `ContinueInLevel`,
`Clear`), so no visibility change was needed — every consumer keeps compiling against the moved type
through the assembly boundary, `Level5.Match` being `autoReferenced`. Assembly-sensitive type identity
checked and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`,
`AssemblyQualifiedName`, or `TypeNameHandling` touches `ActiveMatch` anywhere in the repo, and — since
`ActiveMatch` is a static non-component type — no scene/prefab component serialization applies either.
`ActiveMatch.cs.meta`'s GUID (`153b6c6eb3113bf4080936a0c383776a`) was confirmed before the move and
unchanged after it, verified by reading the `.meta` file post-move.

Moved `ActiveMatch.cs`/`.cs.meta` (`git mv`, preserving history) from `Assets/Scripts/menu_start/` into
`Assets/Scripts/game manager/Level5Match/`, alongside `MatchController.cs`/`MatchSession.cs` — a pure
assembly-ownership move, source body byte-identical. `Configuration`'s exact stored-reference return,
`IsActive`'s `configuration != null` check, `Begin`'s null-logs/assignment-then-`MatchSession.BeginNewMatch()`
ordering, `ContinueInLevel`'s fresh-`MatchConfigurationBuilder.Resolve` reconstruction (mode/roster/modifiers/
cheerleader/source carried over, new level substituted in), and `Clear`'s configuration-only reset were all
left untouched. In particular, the reference-equality contract `ActiveVersusAttempt` depends on
(`ReferenceEquals(ActiveMatch.Configuration, launchedFor)`, protecting an abandoned competitive attempt from
capturing the next ordinary match) is unaffected: `Configuration` still returns the exact stored object, no
clone or rebuild was introduced. `Level5.Match.asmdef`'s `references` needed no change (`ActiveMatch`'s only
custom-assembly dependency, `Level5.Core.Match`, was already declared) — confirmed by the headless compile
showing `Level5.Match.dll` still at 144 defines/296 references, unchanged from Slice 17. Headless Unity
6000.5.7f1 batch compile clean, zero new `CS` errors. Added `ActiveMatchCompilesIntoLevel5Match` to
`Level5ProductionAssemblyBoundaryTests.cs` (mirrors `MatchSessionCompilesIntoLevel5Match`); focused EditMode
runs passed unchanged: the boundary fixture (10/10, including the new test) and
`Level5VersusIntegrationTests` (10/10, including `AnAbandonedTurnDoesNotCaptureTheNextOrdinaryMatch`, which
exercises the reference-identity protection directly across the new assembly boundary). Per this
repository's risk-based validation policy, the full EditMode/PlayMode suites were not re-run for an
assembly-boundary-only change with no behavior modification; PR CI owns that broader regression coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

Post-migration remeasurement of `ActiveVersusAttempt.cs` (`Assets/Scripts/versus/`), the counterpart type
that reads `ActiveMatch.Configuration` for its reference-identity check: re-read in full, its only live
identifiers are `UnityEngine`, `Level5.Core.Match` (`MatchConfiguration`), `Level5.Core.Versus`
(`SeriesId`, `AttemptId`, `ParticipantId`, `RulesetId`, `Attempt`), and `ActiveMatch` — now `Level5.Match`
instead of `Assembly-CSharp` after this slice. `ActiveVersusAttempt` has zero remaining `Assembly-CSharp`
dependency and is eligible for a future Phase 2b slice into `Level5.Versus`, which would need
`Level5.Versus.asmdef` to add a `Level5.Match` reference (`Level5.Versus -> Level5.Match`, no reverse edge,
so the custom-assembly graph would stay acyclic). This is a diagnostic finding only; `ActiveVersusAttempt`
is not moved in this PR, and neither are `VersusMatchReporter`, `MatchCatalogs`, or `PlayerController`.

**Slice 19 — `Level5.Versus` (2026-09-07), the pair Slice 18's remeasurement found dependency-closed.**
`ActiveVersusAttempt.cs` and `VersusMatchReporter.cs` (`Assets/Scripts/versus/`) are the versus-side
counterpart to `ActiveMatch`: the former tracks which competitive attempt, if any, the loaded match is
playing (and the `ReferenceEquals(ActiveMatch.Configuration, launchedFor)` check protecting an
abandoned attempt from capturing the next ordinary match); the latter is the entire footprint versus
has inside gameplay, the one call `GameRules` makes at match end. Re-read both in full before moving
them: `ActiveVersusAttempt`'s only live identifiers are `UnityEngine`, `Level5.Core.Match`
(`MatchConfiguration`), `Level5.Core.Versus` (`SeriesId`, `AttemptId`, `ParticipantId`, `RulesetId`,
`Attempt`), and `ActiveMatch` — `Level5.Match` since Slice 18. `VersusMatchReporter`'s only live
identifiers are `System`, `UnityEngine`, `Level5.Core.Match`, `Level5.Core.Versus`, `GameStats`
(`Level5.Basketball`), `GameStatsAttemptResults`/`VersusRuntime` (`Level5.Versus`), and
`ActiveVersusAttempt`, moved in this same slice. Neither has a live dependency on a type remaining in
`Assembly-CSharp`, confirming Slice 18's finding.

Consumer accessibility checked against every production call site (`VersusLauncher.cs`'s
`ActiveVersusAttempt.Begin(...)`, `game manager/GameRules.cs`'s `VersusMatchReporter.TryReport(...)`):
both call only existing public members, so no visibility change was needed. Assembly-sensitive type
identity checked and clean: no `[SerializeReference]`, `Type.GetType`, `Assembly.Load`/`LoadFrom`,
`AssemblyQualifiedName`, or `TypeNameHandling` touches either type anywhere in the repo, and — since
both are static non-component types — no scene/prefab component serialization applies either.
`ActiveVersusAttempt.cs.meta`'s GUID (`33c21ba9a178bae4f8b12309f22a8f3c`) and
`VersusMatchReporter.cs.meta`'s GUID (`b14974f9aa1601140bc1b18fe10e35a3`) were confirmed before the
move and unchanged after it, verified by reading both `.meta` files post-move.

Moved both `.cs`/`.cs.meta` pairs (`git mv`, preserving history) from `Assets/Scripts/versus/` into
`Assets/Scripts/versus/Level5Versus/`, alongside the versus files already there — a pure
assembly-ownership move, source bodies byte-identical. `ActiveVersusAttempt`'s `SeriesId`/`AttemptId`/
`ParticipantId`/`RulesetId`/`RulesetVersion` properties, private `launchedFor`, `Begin`'s
invalid-input logging/no-op and valid-input capture, `IsActive`'s reference-equality check against
`ActiveMatch.Configuration`, and `Clear`'s full reset were all left untouched. `VersusMatchReporter`'s
no-active-attempt no-op, successful-submission clear-and-return-true, `PersistenceFailed`
leave-in-place-and-return-false retry path, other-refusal log-clear-and-return-true path, and
exception log-clear-and-return-true path were all left untouched — control flow, logging, and retry
policy unchanged. `Level5.Versus.asmdef` gained one new reference, `Level5.Match` (`ActiveVersusAttempt`'s
only custom-assembly dependency not already declared); `Level5.Match.asmdef` was re-checked and still
does not reference `Level5.Versus`, so the new edge (`Level5.Versus -> Level5.Match`) keeps the
custom-assembly graph acyclic. Headless Unity 6000.5.7f1 batch compile clean, zero new `CS` errors.
Added `VersusMatchStateTypesCompileIntoLevel5Versus` to `Level5ProductionAssemblyBoundaryTests.cs`
(mirrors `ActiveMatchCompilesIntoLevel5Match`); focused EditMode runs passed unchanged: the boundary
fixture plus `Level5VersusIntegrationTests` plus `Level5VersusArchitectureTests` together (31/31,
including the new test, and `TheReporterDoesNothingAtAllWhenTheMatchIsNotPartOfASeries`/
`AFailedSaveLeavesTheAttemptInPlaceSoTheMatchEndLoopRetriesIt`/
`ARefusedSubmissionReleasesTheAttemptRatherThanRetryingForever`/
`AnAbandonedTurnDoesNotCaptureTheNextOrdinaryMatch`, which exercise the no-op, retry, refusal-release,
and reference-identity contracts directly across the new assembly boundary). Per this repository's
risk-based validation policy, the full EditMode/PlayMode suites and
`TwoRealMatchesResolveAGameThroughTheWholeStack` were not re-run for an assembly-boundary-only change
with no behavior modification and no focused-test uncertainty; PR CI owns that broader regression
coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

**Slice 20 — `Level5.Match` (2026-09-07), `MatchCatalogs` splits into a clean runtime owner and a
legacy composition seam, and the runtime owner moves.** Unlike slices 12-19, `MatchCatalogs.cs`
(`Assets/Scripts/menu_start/`) was not dependency-closed as it stood: its public `EnsureBuilt`
accepted `IReadOnlyList<StartScreenModeSelected>`/`IReadOnlyList<LevelSelected>` directly and called
`GameModeDefinitionFactory`/`LevelDefinitionFactory` itself, and an unused `EnsureBuiltFromLoadedData()`
depended on `LoadedData` — three `Assembly-CSharp` types a `Level5.Match` asmdef cannot legally
reference. Freshly searched every caller of `EnsureBuilt`, `EnsureBuiltFromLoadedData`, `Override`,
`Reset`, `Builder`, `Compatibility`, `IsReady`, `Modes`, and `Levels` repository-wide before touching
anything: `StartManager.cs`'s `getLoadedData()` was confirmed the only production caller of
`EnsureBuilt(modeSources, levelSources)`; `EnsureBuiltFromLoadedData()` had zero callers anywhere in
the repository (dead since it was written); `Override`/`Reset` are called only by
`Level5VersusIntegrationTests.cs` and `Level5GameplayPlayModeTests.cs`; `Builder`/`Compatibility`/
`IsReady` are called only by `StartManager.cs` and `VersusLauncher.cs`, all through types already in
`Level5.Core.Match`.

The split: `MatchCatalogs` keeps every runtime concern (authored-Resources precedence, the mode/level
catalogs, `GameModeCompatibility`, `MatchConfigurationBuilder`, the independent per-catalog reference
cache, `ConversionAnomalies`, problem reporting, `IsReady`/`Modes`/`Levels`/`Compatibility`/`Builder`/
`Override`/`Reset`) but its `EnsureBuilt` signature changes to
`EnsureBuilt(IReadOnlyList<GameModeDefinition> fallbackModes, IReadOnlyList<LevelDefinition>
fallbackLevels, IReadOnlyList<string> fallbackModeAnomalies = null)` — every parameter a
`Level5.Core.Match` type. `EnsureBuiltFromLoadedData()` was deleted rather than carried forward: no
repository evidence of a caller, so retaining an `Assembly-CSharp` `LoadedData` dependency for it would
have been dead weight, not a preserved contract. A new type, `LegacyMatchCatalogBootstrap`
(`Assembly-CSharp`, `Assets/Scripts/menu_start/`), is the only thing that still converts
`StartScreenModeSelected`/`LevelSelected` through the existing `GameModeDefinitionFactory`/
`LevelDefinitionFactory` — it duplicates none of that conversion logic, it just calls it — and hands
`MatchCatalogs` the resulting typed definitions. `StartManager.cs`'s single call site now reads
`LegacyMatchCatalogBootstrap.EnsureBuilt(modeSelectedData, levelSelectedData)`; every other
`MatchCatalogs` call site (`StartManager.cs`'s `Compatibility`/`IsReady`/`Builder`,
`VersusLauncher.cs`'s `Builder`, both test files' `Override`/`Reset`) was left untouched since none of
those members' signatures changed.

The source-reference cache that used to live inside `MatchCatalogs.EnsureModes`/`EnsureLevels` (skip
rebuilding when the exact same source-list object comes back) had to move with the conversion it was
guarding: `LegacyMatchCatalogBootstrap` now holds `lastModeSources`/`cachedFallbackModes`/
`cachedModeAnomalies` and `lastLevelSources`/`cachedFallbackLevels`, keyed by `ReferenceEquals` against
the legacy source lists exactly as `MatchCatalogs` used to key them — same identity semantics, in-place
mutation of the same list still does not force a reconversion, mode and level caches still invalidate
independently. `MatchCatalogs.EnsureModes`/`EnsureLevels` keep their own independent reference cache
underneath, now keyed by the fallback definition lists `LegacyMatchCatalogBootstrap` hands them; because
that bootstrap hands back the same cached list objects when its own legacy sources have not changed,
`MatchCatalogs`'s cache still sees "unchanged" the same way it used to. Critically, the bootstrap calls
`MatchCatalogs.EnsureBuilt` on every request regardless of its own cache hit/miss, so
`MatchCatalogs.Reset()` followed by the same legacy sources still rebuilds — `MatchCatalogs`'s own
`modeSourceKey`/`levelSourceKey` are cleared by `Reset()`/`Override()`, independent of whatever the
bootstrap cached. Anomaly clearing/reporting behavior is unchanged: `MatchCatalogs` only clears and
repopulates `conversionAnomalies` when the authored-Resources catalog is empty and the fallback path is
actually taken, exactly as before the split, and still reports it through `Debug.LogError` via
`ReportProblems`.

Authored-Resources precedence, the legacy prefab fallback, and every conversion rule in
`GameModeDefinitionFactory`/`LevelDefinitionFactory` are untouched — `MatchCatalogs` still decides
authored vs. fallback, `LegacyMatchCatalogBootstrap` still just converts, and `Assets/Resources/Match/
Modes`/`Levels` still do not exist on `dev`, so the fallback path remains the one actually live in
production. `MatchCatalogs.cs.meta`'s GUID (`d9db91f6a5878674f9d6d1d008150297`) was confirmed before the
move and unchanged after it (`git mv`, preserving history), verified by reading the `.meta` file
post-move. Moved `MatchCatalogs.cs`/`.cs.meta` from `Assets/Scripts/menu_start/` into `Assets/Scripts/
game manager/Level5Match/`, alongside `MatchController.cs`/`MatchSession.cs`/`ActiveMatch.cs`.
`Level5.Match.asmdef` needed no new reference — `MatchCatalogs`'s only custom-assembly dependency,
`Level5.Core.Match`, was already declared, and it references no menu/loading/player/versus type.
Assembly-sensitive type identity checked and clean: no `[SerializeReference]`, `Type.GetType`,
`Assembly.Load`/`LoadFrom`, `AssemblyQualifiedName`, or `TypeNameHandling` touches `MatchCatalogs`
anywhere in the repo, and — since it is a static non-component type — no scene/prefab component
serialization applies either.

A post-implementation review flagged one consistency gap: unlike every other static owner introduced
across this migration (`MatchCatalogs`, `ActiveMatch`, `MatchSession`, `VersusRuntime`, `VersusCatalogs`,
`ActiveVersusAttempt`), `LegacyMatchCatalogBootstrap` initially exposed no way to clear its own
conversion cache. Not a live bug — every production and test path that needed a hard reset already went
through fresh source-list references or `MatchCatalogs.Reset()` — but a testability/consistency gap
against the established pattern. Fixed by adding `LegacyMatchCatalogBootstrap.Reset()`, which clears its
five cache fields and leaves `MatchCatalogs` untouched (callers that also want the runtime catalogs
cleared still call `MatchCatalogs.Reset()` separately, as they already did).

Headless Unity 6000.5.7f1 batch compile clean, zero `CS` errors. Added
`MatchCatalogsCompilesIntoLevel5Match` to `Level5ProductionAssemblyBoundaryTests.cs` (mirrors
`ActiveMatchCompilesIntoLevel5Match`), and a new focused fixture,
`LegacyMatchCatalogBootstrapTests.cs`, proving the four behaviors this split most risked changing:
repeated bootstrap calls with the same legacy source-list reference reuse the built catalog rather than
rebuilding it, `MatchCatalogs.Reset()` followed by the same legacy input still repopulates the catalogs,
`LegacyMatchCatalogBootstrap.Reset()` clears its own conversion cache independently of `MatchCatalogs`,
and a fallback conversion anomaly is still visible through `MatchCatalogs.ConversionAnomalies`. Focused
EditMode runs passed unchanged: `Level5ProductionAssemblyBoundaryTests` (12/12, including the new test),
`Level5AuthoredMatchDataTests` (7/7 — the conversion-parity/anomaly/id/compatibility suite that
exercises `GameModeDefinitionFactory`/`LevelDefinitionFactory` directly and is therefore unaffected by
where `MatchCatalogs` itself lives), `Level5VersusIntegrationTests` (10/10, including
`TheLauncherBuildsAnOrdinaryMatchForTheSeriesFrozenMode`, which calls `MatchCatalogs.Override`/`Reset`
directly), and the new `LegacyMatchCatalogBootstrapTests` (4/4). Per this repository's risk-based
validation policy, the full EditMode/PlayMode suites were not re-run for a behavior-preserving
ownership/boundary split with focused parity coverage already in place; PR CI owns that broader
regression coverage.

This does not close `AUD-012`/Phase 2, and does not unblock 2c on its own — see the updated 2c status
below.

**Slice 21 — dependency cut only, no move (2026-09-07): `PlayerController`'s direct `GameLevelManager`
arena-state dependency is removed through explicit composition.** Unlike slices 1-20, this slice moves
no file — `PlayerController` was confirmed still not dependency-closed (it retains direct references to
`PlayerInputReader`, `MatchRuntime`, `SniperManager`, `PlayerIdentifier`, `CharacterProfile`,
`PlayerHealth`, `PlayerDunk`, `PlayerAttackQueue`, `PlayerSwapAttack`, `PlayerDamageReactions`,
`CallBallToPlayer`, `ShooterAttributesMapper` and `RigidbodyFreezeHelper` — all still `Assembly-CSharp`),
so it cannot move yet. Its `BasketBall`/`ShotMeter`/`IShooterActor`-style dependencies are *not* part of
that blocker set; they are already owned by custom assemblies and would be legal references from a future
player asmdef. As first written this sentence listed them as blockers — corrected 2026-09-08, see the
remeasured closure scan below. This slice narrows one edge in place: the two direct
`GameLevelManager` reads audited beforehand -
`bballRimVector = GameLevelManager.instance.BasketballRimVector` (in `Start()`) and
`GameLevelManager.instance.TerrainHeight` (the no-active-Terrain drop-shadow fallback in `Update()`) -
were the only live references found, matching the expected set exactly.

The replacement mirrors the shape `AUD-010` Phase 1c already established for `BasketBall`'s identical
no-Terrain fallback (`IGroundHeightProvider`, bound by composition instead of read from
`GameLevelManager.instance` directly). `PlayerController` gained `BindArenaContext(Vector3
basketballRimVector, IGroundHeightProvider groundHeightProvider)`, a narrow method that only retains
both references (the rim as a value snapshot, matching how this controller already treated it; the
ground-height provider as a live reference, since `GameLevelManager` keeps updating its own value after
spawning). The no-Terrain fallback was extracted into a private `ResolveDropShadowHeight()` — a pure,
behavior-preserving extraction mirroring `BasketBall.ResolveDropShadowHeight()` in shape — so it could
be driven directly in focused tests the same way. Unlike `BasketBall` (bound synchronously
before its own `Start()` ever runs, letting it dereference the provider unconditionally),
`PlayerController`'s context cannot be bound before its own `Start()` — see the timing constraint below
— so `ResolveDropShadowHeight()` guards the unbound case explicitly: `Debug.LogError` and return the
last-known `terrainYHeight` rather than throwing or inventing a fallback global. This narrow guard
exists only because the timing genuinely differs from `BasketBall`'s; it is not a general pattern this
slice introduces elsewhere. A same-day code review flagged that an unbound provider is re-entered every
`Update()` frame for an entire airborne arc, so an unbound-provider window would otherwise flood the
console once per frame rather than once; fixed with a single `groundHeightProviderMissingLogged` bool so
the error logs exactly once per controller, covered by
`ResolveDropShadowHeight_NoBoundProvider_LogsOnlyOnceAcrossRepeatedCalls`. The same review also noted
that `BindArenaContext` binds the rim vector with no equivalent unbound-state diagnostic — an
intentional asymmetry: the task's own composition contract for this method is "retain the supplied
dependencies" only, and the failure shape it flagged (silently defaulting to `Vector3.zero` if a
composition path never calls `BindArenaContext` at all) already existed identically before this slice
(the direct `GameLevelManager.instance.BasketballRimVector` read it replaced was never diagnosed either)
— left as a pre-existing, unchanged failure mode rather than expanded scope.

A second review round found two more, both fixed. First, the new `Start()` call was the only statement
in that method that could not survive the duplicate-manager path (`Awake`'s `instance != this` guard
returns before `_spawnCoordinator` is assigned): every other statement already tolerates it, since
`ArenaBootstrap.Apply` early-returns on the null `_rules` such an instance carries and `FindRimVector` is
a null-safe scene search. Made null-conditional to restore that parity; on the normal path the
coordinator is always assigned in `Awake` before spawning, so it can only skip where no level was built.
Second, the new guard test is scoped to `PlayerController.cs` alone, but sibling components on the same
player hierarchy still read the same singleton for the same arena values (`PlayerDunk` for the rim
vector; `AutoPlayerDefense`/`AutoPlayerController` for the rim vector and `TerrainHeight`) — all out of
scope here. The guard's own doc comment now says so explicitly, so a pass is not misread as "the player
prefab no longer depends on `GameLevelManager`", mirroring the caveat
`Level5BasketballGameManagerEdgeTests` already records for the unresolved basketball -> `GameRules` edge.

`SpawnCoordinator` gained `BindHumanArenaContext(Vector3 basketballRimVector, IGroundHeightProvider
groundHeightProvider)`, which forwards both already-resolved values to every registered non-CPU
participant's already-wired `PlayerController` (`PlayerIdentifier.playerController`, populated by the
existing `setPlayer()` — no new `GetComponent`/scene search). A registered human unexpectedly missing a
`PlayerController` logs and continues, the same composition-error shape `GiveBall` already uses for a
missing `BasketBallState`/`GameStats`, rather than throwing and aborting every other participant's
binding. CPU participants are skipped entirely — `AutoPlayerController` takes no arena context.

Timing was the hard constraint driving where the call lives: `GameLevelManager.Awake()` spawns players
(via `SpawnCoordinator.SpawnPlayers()`) before the rim is resolved — `ArenaBootstrap.Apply` and
`_basketballRimVector = ArenaBootstrap.FindRimVector()` only run in `GameLevelManager.Start()`. Binding
during `RegisterHuman()` (Awake-time) would therefore hand every human a stale/default rim, so
`GameLevelManager.Start()` calls `_spawnCoordinator.BindHumanArenaContext(_basketballRimVector, this)`
as the last step, immediately after `_basketballRimVector` is assigned. This is a direct method call, not
reliant on `PlayerController.Start()`/`GameLevelManager.Start()` running in a particular order relative
to each other (unlike the value this replaces, which depended on that ordering implicitly) — it sets
`PlayerController`'s fields synchronously regardless of whether that controller's own `Start()` has run
yet this frame, and it always runs before the first `Update()` of any object in the scene.

No serialized field, prefab contract, public player API, movement/jump/dunk/shoot/input/combat/health/
damage algorithm, or CPU behavior changed. `AutoPlayerController` is untouched.

Headless Unity 6000.5.7f1 batch compile clean, zero `CS` errors. Added
`Level5PlayerControllerDependencyGuardTests.PlayerControllerHasNoGameLevelManagerReference` (mirrors
`Level5BasketBallDependencyGuardTests`), and a new focused fixture,
`Level5PlayerControllerArenaContextTests.cs` (8 tests): `BindArenaContext` stores both values;
`ResolveDropShadowHeight` reads the bound provider live, not a value snapshotted at bind time (mirrors
`Level5BasketballGroundHeightProviderTests`'s identical proof for `BasketBall`); an unbound provider
logs and returns the last-known height without throwing, and logs only once across repeated calls;
`SpawnCoordinator.BindHumanArenaContext` binds a single human, binds multiple humans identically, leaves
a CPU participant untouched, and logs (without throwing) for a human missing `PlayerController`. Focused
EditMode run: 47/47, including the two new
fixtures and the existing `Level5BasketBallDependencyGuardTests`/`Level5BasketballGameManagerEdgeTests`/
`Level5RangeMeterOwnershipTests`/`Level5BasketballGroundHeightProviderTests` fixtures this slice's shape
borrows from. `PlayerMovementPhysicsTests.JumpingDoesNotCompoundHorizontalVelocity` (the menu -> gameplay
-> player bootstrap real-scene smoke test) passed unchanged, confirming the new
`GameLevelManager.Start()` -> `SpawnCoordinator.BindHumanArenaContext()` -> `PlayerController.BindArenaContext()`
composition chain wires correctly end to end in a real scene. Per this repository's risk-based validation
policy, the full EditMode/PlayMode suites were not re-run for a behavior-preserving, narrowly-scoped edge
cut with focused parity coverage already in place; PR CI owns that broader regression coverage.

**`PlayerController` dependency closure scan, remeasured from current declarations (2026-09-08).**
Re-read the file completely; `GameLevelManager` no longer appears in live code anywhere (the five
remaining occurrences are `///` doc comments, and the new guard strips comments before asserting).

The scan first published with this slice (2026-09-07) grouped the closure by *concern* only and labelled
every entry `Assembly-CSharp`. That was wrong for eight of them. Ownership was re-derived here by locating
each type's current declaration and walking up to its nearest enclosing `.asmdef`, which is authoritative
— not by folder name, subsystem name, or historical location. That distinction matters because four of
these subsystem folders carry a *nested* asmdef covering only part of the folder
(`Assets/Scripts/input/Level5Input/`, `Assets/Scripts/misc/Level5Misc/`,
`Assets/Scripts/Utility/Level5Utility/`, `Assets/Scripts/game manager/Level5Match/`), so inferring
ownership from the top-level folder gets it wrong in both directions.

**A. Already owned by custom assemblies — legal references, not blockers.** A future `Level5.Player`
asmdef could reference these directly:

- **`Level5.Input`** (`Assets/Scripts/input/Level5Input/`): `PlayerControls`, `PlayerControlsProvider`
- **`Level5.Core`** (`Assets/Level5/Core/`): `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`** (`Assets/Scripts/basketball/`): `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`** (`Assets/Scripts/Utility/Level5Utility/`): `SceneObjects`, `UtilityFunctions`

Referencing these creates no cycle *today*, and the reason is structural rather than incidental: an
asmdef cannot reference `Assembly-CSharp`, so none of these four assemblies is able to depend back on
`PlayerController` while it still lives there. Verified rather than assumed — the only mentions of
`PlayerController` anywhere in those four assemblies are comments, in `Assets/Level5/Core/IShooterActor.cs`
and `Assets/Scripts/basketball/BasketBall.cs` (a third hit, `Assets/Scripts/basketball/Legacy~/`, is
excluded from compilation entirely by Unity's trailing-`~` convention). That guarantee expires the moment
`PlayerController` moves: once `Level5.Player` exists, a back-reference becomes expressible, so acyclicity
must be re-checked at that point rather than inherited from this scan.

**B. Still compiled into `Assembly-CSharp` — the actual remaining blockers (13).** Only these prevent
`PlayerController` from moving into a production asmdef:

- **input:** `PlayerInputReader` (`Assets/Scripts/input/`, *outside* `Level5Input/`)
- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerHealth`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerSwapAttack`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `CallBallToPlayer` (`Assets/Scripts/misc/callBallToPlayer.cs`, *outside*
  `Level5Misc/`), `ShooterAttributesMapper` (`Assets/Scripts/player/`), `RigidbodyFreezeHelper`
  (`Assets/Scripts/Utility/`, *outside* `Level5Utility/`)

Three corrections to closure *membership*, beyond ownership: `BasketBallState` and
`IGroundHeightProvider` are live dependencies the first scan omitted (both legal — `Level5.Basketball`
and `Level5.Core`), and `AutoPlayerController` is **not** a dependency at all — its only two appearances
in the file are a `Debug.LogError` string literal and a comment, so it never belonged in the graph.

This remeasurement is diagnostic only — none of these are addressed in this PR, `PlayerController` is
not moved, and Phase 2c's PlayMode test assemblies are not normalized here. `PlayerController` itself
remains in `Assembly-CSharp` (`Assets/Scripts/player/`); Slice 21 cut one outbound edge and moved no
file, so Phase 2c stays blocked on it.

**Slice 22 — `Level5.Utility` (2026-09-08), `RigidbodyFreezeHelper` moves into the assembly that
already owns the shared utility leaf.** A source-identical assembly-ownership move: the file and its
`.meta` were relocated from loose `Assets/Scripts/Utility/` (where they compiled into
`Assembly-CSharp`) into `Assets/Scripts/Utility/Level5Utility/`, alongside `AtomicFile`,
`SceneObjects` and `UtilityFunctions`. Nothing else changed: same global namespace, same type name,
same `public static class RigidbodyFreezeHelper`, same two methods (`FreezePosition`,
`UnfreezeRotationOnly`), same `RigidbodyConstraints` masks, same null behaviour, and not a single
caller edited. `git` recorded both files as pure renames and the moved bytes hash identically to the
originals, so "behaviour-preserving" here is byte-level, not an assessment.

The helper was confirmed dependency-closed against current source before moving: it references only
`UnityEngine.Rigidbody` and `RigidbodyConstraints`, and no type remaining in `Assembly-CSharp`. It is
also assembly-identity-safe - a plain static class, not a `MonoBehaviour`, `ScriptableObject`, or
serialized managed object, so no scene or prefab carries a component entry for it. A focused search
for assembly-sensitive resolution (`[SerializeReference]`, `Type.GetType`, `AssemblyQualifiedName`,
`TypeNameHandling`, hard-coded assembly-qualified names) found exactly one hit across project-authored
code under `Assets/`, in `Assets/Editor/MobileDependencyResolverInstallerLp.cs` for an unrelated
Google package (the remaining hits repository-wide are all inside `Library/PackageCache`, Unity's own
untracked package sources) - nothing in this project resolves this helper by assembly-qualified name. Its `.meta` GUID
`ea19b4ee40100a34b8a4e029df148059` is preserved unchanged (the audited value matched current `dev`),
so any future serialized reference stays intact. No compatibility wrapper or forwarding type was
introduced.

**`Level5.Utility.asmdef` was not modified, and that was verified rather than assumed.** The helper
needs only Unity engine APIs, which need no asmdef reference; the assembly already existed and is
`autoReferenced`, so its three callers - `PlayerController`, `AutoPlayerController` (both
`Assets/Scripts/player/`) and `RacingVehicleController` (`Assets/Scripts/player_racing/`, *outside*
`Level5PlayerRacing/`) - all still in `Assembly-CSharp`, resolve it with no call-site or reference
change. No reference was added merely because this one file has a narrower dependency set than the
rest of the assembly. The resulting direction is `Assembly-CSharp callers -> Level5.Utility ->
UnityEngine`; the forbidden `Level5.Utility -> Assembly-CSharp` edge is not created, and
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` continues to enforce that outbound boundary
now that this file is inside a scanned production folder.

Headless Unity 6000.5.7f1 batch compile clean, zero `CS` errors, with every existing caller compiling
unchanged - no visibility change, no new assembly reference, no runtime source change. Extended the
existing `Level5ProductionAssemblyBoundaryTests` with one focused identity assertion,
`RigidbodyFreezeHelperCompilesIntoLevel5Utility` (mirroring
`AtomicFileCompilesIntoLevel5Utility`); no second dependency scanner was added. Focused EditMode run:
13/13 in that fixture, including both pre-existing boundary guards. Per this repository's risk-based
validation policy the player-movement, combat and racing suites were not re-run - this slice changes
assembly ownership, not physics behaviour, and the compile plus identity/boundary guards already
establish that claim; PR CI owns broader regression coverage.

**`PlayerController` dependency closure scan, freshly remeasured after this slice (2026-09-08).**
Re-derived from current declarations rather than by subtracting one from Slice 21's count: every
identifier `PlayerController.cs` actually references (comments and string literals stripped) was
matched against every type declaration under `Assets/`, and each match resolved to its nearest
enclosing `.asmdef`, which is authoritative. Group A is unchanged from Slice 21 except that
`RigidbodyFreezeHelper` has joined it:

**A. Already owned by custom assemblies - legal references, not blockers (11).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, **`RigidbodyFreezeHelper`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (12, down from 13).**

- **input:** `PlayerInputReader` (`Assets/Scripts/input/`, *outside* `Level5Input/`)
- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerHealth`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerSwapAttack`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `CallBallToPlayer` (`Assets/Scripts/misc/callBallToPlayer.cs`,
  *outside* `Level5Misc/`), `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`RigidbodyFreezeHelper` is the only entry that left group B; nothing else moved between groups, and no
new dependency appeared. `PlayerController` itself remains in `Assembly-CSharp`
(`Assets/Scripts/player/`) - this slice moved a type it depends on, not the controller.

**`PlayerSwapAttack` eligibility, freshly rechecked (2026-09-08) - diagnostic only.** Re-read in full:
it references only Unity engine types (`MonoBehaviour`, `Animator`, `AnimatorOverrideController`,
`AnimationClip`, `Random`, `SerializeField`) and no type declared anywhere else in this repository, so
it is dependency-closed and is a strong candidate for the slice that establishes or first enters
`Level5.Player`. Its four live consumers - `PlayerController`, `AutoPlayerController`,
`BodyGuardController` and `EnemyController` (the latter in `Assets/Scripts/enemy/`, *outside*
`Level5Enemy/`) - are all in `Assembly-CSharp` today, so no existing custom assembly would need a new
reference to follow it. Unlike this slice's helper it *is* a `MonoBehaviour` with serialized fields
placed on player/enemy prefabs, so a future move must preserve its `.meta` GUID
(`138cd9572af08c54ba58bbfabe64f328`) for those component entries to keep resolving. It is **not**
created, moved, or otherwise addressed here. When `Level5.Player` is eventually created, group A's
acyclicity must be freshly rechecked rather than inherited: the structural guarantee recorded for
Slice 21 (#112) - that those assemblies *cannot* depend back on `PlayerController` because an asmdef
cannot reference `Assembly-CSharp` - expires the moment a back-reference becomes expressible.

**Slice 23 — `Level5.Player` (2026-09-08), the assembly's first type: `PlayerSwapAttack`.** This slice
creates the `Level5.Player` production runtime assembly and moves exactly one type into it, the
dependency-closed component Slice 22's diagnostic recheck had already nominated. `PlayerSwapAttack.cs`
and its `.meta` were relocated from `Assets/Scripts/player/` (where they compiled into
`Assembly-CSharp`) into a new `Assets/Scripts/player/Level5Player/` leaf folder. Nothing else changed:
same global namespace, same type name, same `public class PlayerSwapAttack : MonoBehaviour`, same
`public AnimationClip[] closeAttacks`, same `[SerializeField] protected Animator anim` and
`[SerializeField] AnimatorOverrideController animatorOverrideController`, same
`public AnimationClip longRangeAttack`, same `AnimatorOverrideController` getter, same `Start()`, same
`setCloseAttack()`/`setLongRangeAttack()` names, bodies, random close-attack selection and null-guard
behaviour, and not a single caller edited. `git` recorded both files as pure renames and the moved
`.cs` hashes identically to the original blob (`5f15c30588d8...`), so "behaviour-preserving" here is
byte-level, not an assessment.

**The asmdef deliberately does *not* live at `Assets/Scripts/player/`.** Asmdef ownership is recursive,
so a player-root asmdef would have swallowed `PlayerController`, `AutoPlayerController`,
`PlayerHealth`, `CharacterProfile` and the roughly 30 other files in that folder — none of which are
dependency-closed yet. `Assets/Scripts/player/Level5Player/` is the same leaf-subfolder shape
`Level5Enemy/`, `Level5Combat/`, `Level5Input/` and `Level5Utility/` already use, and it keeps the new
assembly's contents to exactly what this slice migrated.

The component was confirmed dependency-closed against current source before moving: it references only
Unity engine types (`MonoBehaviour`, `Animator`, `AnimatorOverrideController`, `AnimationClip`,
`Random`, `SerializeField`) and no type declared anywhere else in this repository. Unlike Slice 22's
static helper it *is* a serialized `MonoBehaviour`: 80 authored assets (79 prefabs and one scene)
carry a component entry for it, so its `.meta` GUID `138cd9572af08c54ba58bbfabe64f328` was moved with
the file rather than regenerated, and was verified unchanged after the move. No compatibility
wrapper or forwarding type was introduced, and no prefab, scene, animation asset or animator
controller was edited or resaved. The two ways a cross-assembly move can break a serialized
reference *despite* a preserved GUID were both checked and are both clear: no authored asset stores
an assembly-qualified `m_TargetAssemblyTypeName` naming this type (a `UnityEvent` persistent call
would record `PlayerSwapAttack, Assembly-CSharp` and silently break), and no `.anim`/`.controller`
names `setCloseAttack`/`setLongRangeAttack` as an animation event - both methods are called from
C# only. The single `Type.GetType` anywhere in project-authored code is in
`Assets/Editor/MobileDependencyResolverInstallerLp.cs` for an unrelated Google package, and nothing
in this project uses `[SerializeReference]`.

**`Level5.Player.asmdef` declares `"references": []`, and that emptiness is the point.** The type needs
only Unity engine APIs, which need no asmdef reference, and the assembly is `autoReferenced`, so its
four live consumers — `PlayerController`, `AutoPlayerController` (both `Assets/Scripts/player/`),
`BodyGuardController` (`Assets/Scripts/bodyguard/`) and `EnemyController` (`Assets/Scripts/enemy/`,
*outside* `Level5Enemy/`) — all still in `Assembly-CSharp`, resolve it with no call-site or reference
change. `RacingVehicleController`'s two mentions are commented out and were left alone. No reference
was pre-added for player types that may migrate later. The resulting direction is
`Assembly-CSharp callers -> Level5.Player -> UnityEngine`; the forbidden
`Level5.Player -> Assembly-CSharp` edge is not created, and
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` now enforces that outbound boundary
automatically, the new folder being a discovered production asmdef folder.

**Slice 21's acyclicity warning was honoured, not inherited.** That warning — that group A's
cycle-freedom rested on `Assembly-CSharp` being unreferenceable by any asmdef, and expires once
`Level5.Player` exists and a back-reference becomes expressible — was rechecked against current
`.asmdef` files rather than assumed. `Level5.Player` declares no outbound reference at all, so it
cannot be the source of a cycle; and no other `.asmdef` in the project names `Level5.Player`, so it is
not yet the target of one either. The first assembly reference either into or out of `Level5.Player`
is the point at which this recheck has to happen again.

Headless Unity 6000.5.7f1 batch compile clean, zero `CS` errors, with every existing caller compiling
unchanged — no visibility change, no new assembly reference, no runtime source change; Unity built
`Library/ScriptAssemblies/Level5.Player.dll`. Extended the existing
`Level5ProductionAssemblyBoundaryTests` with one focused identity assertion,
`PlayerSwapAttackCompilesIntoLevel5Player` (mirroring `MatchControllerCompilesIntoLevel5Match`); no
second dependency scanner was added. Focused EditMode run: 14/14 in that fixture, including both
pre-existing boundary guards. Serialized-component resolution was verified directly rather than
inferred, via a throwaway editor pass (created, run, deleted — not committed) that loaded one
representative player prefab and one representative enemy prefab: `player_ak47.prefab` and
`enemy_drblood.prefab` both resolve `PlayerSwapAttack`, report zero missing-script components, and the
enemy's authored clips survive intact (`closeAttacks` = `enemy_attack_drblood1..3`, `longRangeAttack` =
`enemy_attack_drblood4`); the same pass confirmed `typeof(PlayerSwapAttack).Assembly` is
`Level5.Player` while `AssetDatabase` still maps the script to `138cd9572af08c54ba58bbfabe64f328`.
`player_ak47`'s empty `closeAttacks` is authored state, not migration loss — the unmodified prefab YAML
records `closeAttacks: []`. Per this repository's risk-based validation policy the combat,
player-movement and CPU suites were not re-run: this slice changes assembly ownership, not attack
behaviour, and the compile plus identity/boundary/prefab-resolution evidence already establishes that
claim; PR CI owns broader regression coverage.

**`PlayerController` dependency closure scan, freshly remeasured after this slice (2026-09-08).**
Re-derived from current declarations rather than by subtracting one from Slice 22's count: every
identifier `PlayerController.cs` actually references (comments and string literals stripped) was
matched against every type declaration under `Assets/`, and each match resolved to its nearest
enclosing `.asmdef`, which is authoritative. Group A is unchanged from Slice 22 except that
`PlayerSwapAttack` has joined it:

**A. Already owned by custom assemblies — legal references, not blockers (12).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: **`PlayerSwapAttack`** (this slice)

**B. Still compiled into `Assembly-CSharp` — the actual remaining blockers (11, down from 12).**

- **input:** `PlayerInputReader` (`Assets/Scripts/input/`, *outside* `Level5Input/`)
- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerHealth`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `CallBallToPlayer` (`Assets/Scripts/misc/callBallToPlayer.cs`,
  *outside* `Level5Misc/`), `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`PlayerSwapAttack` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared. `PlayerController` itself remains in `Assembly-CSharp` (`Assets/Scripts/player/`)
— this slice moved a type it depends on, not the controller, and moving the controller into
`Level5.Player` stays blocked on all eleven group B entries. Six of those eleven are its own sibling
components under `Assets/Scripts/player/`, so that folder cannot simply be absorbed wholesale; each
still needs its own dependency-closure check before it can follow `PlayerSwapAttack` into the new leaf.

**Running total after slices 1-23:** `Assets/Scripts/basketball`'s 12 production files (source left in
place, no `.meta` moved) plus `MatchController.cs`/`MatchSession.cs`/`ActiveMatch.cs`/`MatchCatalogs.cs`
(all four `Level5.Match`) plus
`VersusCatalogs.cs`/`DefaultCompetitiveRulesets.cs`/
`FileVersusSeriesRepository.cs`/`VersusRuntime.cs`/`GameStatsAttemptResults.cs`/`ActiveVersusAttempt.cs`/
`VersusMatchReporter.cs` (all seven moved, `.meta`s intact) plus `AtomicFile` (new file/`.meta` under
`Level5.Utility`, `CharacterProgressStore.cs` itself left in place) plus `PlayerSwapAttack.cs`/`.meta`
(moved into the new `Level5.Player`, GUID intact) plus the 27 files across 10 leaf
assemblies from slices 1-10 (`Level5.Input`,
`Level5.Combat`, `Level5.Enemy`, `Level5.PlayerRacing`, `Level5.Vehicle`, `Level5.MenuProgression`,
`Level5.Utility`, `Level5.Misc`, `Level5.Models`, `Level5.MenuStart`) plus the 4 pre-existing ones
(`Level5.Core`, `Level5.Constants`, `Level5.Pooling`, `Level5.Audio`) plus `Level5.Player` — 18
production runtime assemblies total (Slice 23 is the first new assembly since Slice 18: Slice 20 added
a file to an existing assembly, not a new one; Slice 21 moved no file at all — it narrowed an edge in
place; and Slice 22 moved `RigidbodyFreezeHelper.cs`/`.meta` into the existing `Level5.Utility`), out
of roughly 218 `.cs` files in
`Assets/Scripts` before this phase started. `LegacyMatchCatalogBootstrap.cs`, the composition seam Slice
20 added, stays in `Assembly-CSharp` (`Assets/Scripts/menu_start/`) and is not counted here. The
remainder is either `player`/`game manager` themselves (still mutually coupled, and still most of what
the asmdef-free gameplay PlayMode workaround needs), or reaches into that pair (directly or transitively)
and so is blocked the same way the rest of `versus`/`analytics`/`Models/HighScoreModel` were.

**Slice 24 — `Level5.Player` (2026-09-08), its second type: `CallBallToPlayer`, after inverting that
type's `MatchRuntime` dependency.** Unlike Slice 23's dependency-closed move, this slice had to remove
a dependency before the move became legal. `CallBallToPlayer.Start()` read `MatchRuntime.Rules`
directly — `MatchRuntime` is `Assets/Scripts/game manager/`, *outside* `Level5Match/`, so it is still
`Assembly-CSharp` and was the single edge keeping this component out of any custom assembly. The read
is now an explicitly bound `ResolvedMatchRules` (`Level5.Core`), supplied by the existing participant
composition, and `Assets/Scripts/misc/callBallToPlayer.cs`/`.meta` moved to
`Assets/Scripts/player/Level5Player/` alongside `PlayerSwapAttack`.

**The call-enabled policy did not move and did not change.** `CallBallToPlayer` still owns it, still in
its own `Start()`, still in exactly the same shape: enabled by default; `CallEnabled = false` when
`Hardcore && EnemiesOnly`; back to `true` inside that branch for a three-, four-, seven-point or
all-point contest. Only the source of the rules changed. `SpawnCoordinator` supplies facts — the
already-resolved rules object it has held since AUD-010 Phase 2b0 — and does not decide whether
calling the ball is enabled; no match-context service, provider interface, registry, global fallback
or generalized player-binding framework was introduced. Everything else about the component is
byte-identical: same global namespace, same type name, same `public bool CallEnabled`, same
`public bool Locked`, same live `[SerializeField]` set (`pullSpeed`, `pullDirection`, `locked`,
`CallEnabled`) in the same order and with the same types, same `pullSpeed = 2.3f` at startup, same
`pullBallToPlayer`/`pullBallToPlayerAuto` names and Rigidbody math, same commented-out legacy blocks.
Not one caller was edited: `PlayerController`, `AutoPlayerController`, `groundcheck` and `PlayerDunk`
all stay in `Assembly-CSharp` and reach the type through `autoReferenced`.

**One dead serialized field was removed, and it is the reason this assembly needs no basketball
reference.** `[SerializeField] private BasketBallState _basketBallState` was declared and never used:
the identifier appears exactly once in the whole repository - its own declaration - and all 71 authored
prefabs serialize it as `{fileID: 0}`, so it held no authored data anywhere. It was nonetheless a real
compiled dependency, because a field declaration must resolve, and it alone would have forced
`Level5.Player` to reference `Level5.Basketball`. Removing an unused, universally-null field is not the
"remove serialized fields as cleanup" this phase prohibits: keeping it would have recorded an assembly
edge that no behaviour justifies. Unity ignores the now-unknown `_basketBallState` key when
deserializing the existing prefabs, and no prefab was resaved to strip it - the key simply disappears
the next time each asset is written by the editor for unrelated reasons. Prefab resolution was
re-verified after the removal (below).

**`BindMatchRules` follows the repository's existing bind-once shape**, the one `BasketBall` and
`BasketBallState` already use, including its guard ordering: the already-bound branch is checked
*before* the null-argument branch, so a null second call after a real bind reports "already bound"
rather than "remaining unbound" and cannot obscure the original valid reference. (`ShotMeter` and
`GameStats` still carry the older null-first ordering; they were not touched.)

**An unbound `Start()` fails closed rather than reaching back into `MatchRuntime`.** A production
participant always has rules by then — both binding sites run inside `GameLevelManager.Awake`'s spawn
pass, and Unity runs every `Awake` before any `Start`, so the ordering is structural rather than a
script-execution-order assumption. Reaching `Start()` unbound is therefore a composition defect: it
logs an actionable error and sets `CallEnabled = false` for that one instance. The player object is
not disabled and the component is not disabled — only calling the ball. Note this fails closed for the
paths that consult `CallEnabled` (`PlayerController`'s keyboard call-ball and `AutoPlayerController`'s
CPU pull); `PlayerController`'s touch call-ball branch has never consulted `CallEnabled` and still does
not. That asymmetry is pre-existing and was deliberately left alone here.

**Composition binds from both registration paths, through one shared helper.**
`SpawnCoordinator.BindCallBallMatchRules(GameObject)` is called from `RegisterHuman` and from
`RegisterCpu`, next to the existing `BindRangeMeters`/`BindShotMeters` calls, so every route that
produces a participant is covered: the primary human, additional roster humans, roster CPUs, a
scene-supplied `autoPlayer`, and Lockdown's defender. It uses `GetComponent`, not
`GetComponentsInChildren` — the component is authored on the participant root, which is where both
`PlayerController` and `AutoPlayerController` resolve it from — and it silently skips a participant
that has none, because that is authored composition rather than a defect:
`cpu_player_defense_oldreal.prefab` carries no `CallBallToPlayer` at all (verified: 71 authored
prefabs reference the script GUID, and the Lockdown defender is not one of them). It never adds a
missing component and never re-reads `MatchRuntime.Rules`.

**`Level5.Player.asmdef` gains exactly one reference: `"Level5.Core"`.** That is for
`ResolvedMatchRules`, which the bound field and `BindMatchRules` name. The audit expected
`"Level5.Basketball"` as well, and the instruction to verify rather than assume that expectation is
what caught it: the only thing that would have required it was the dead `_basketBallState` field
described above, so removing the field removed the reference with it. An assembly reference that
exists only to compile a field nothing reads is not a dependency worth recording. Nothing else was
added; no reference was pre-added for player types that may migrate later — `PlayerController` will
need `Level5.Basketball` when it eventually moves, and that is when the edge should appear.

**The graph was rechecked from current `.asmdef` files before and after the edit**, as Slice 21's
warning and Slice 23 both require, because this slice creates the first outbound edges from
`Level5.Player`. Before: `Level5.Player` declared nothing and nothing named it. After:
`Level5.Player -> {Level5.Core}`, and `Level5.Core` declares no references at all, so the outbound
side is one edge deep and terminal. No `.asmdef` in the project names `Level5.Player`, so it is still
not the target of any edge and cannot be part of a cycle. The resulting direction is
`Assembly-CSharp composition/controllers -> Level5.Player -> Level5.Core`. Worth recording for the
next remeasurement: the reverse edge is not merely absent but structurally unavailable — basketball
reaches back to the player only through `IShooterActor.LockCallBallToPlayer`, which lives in
`Level5.Core` (`BasketBall.cs`, `BasketBallAuto.cs`), so `Level5.Basketball` has no reason to name
`Level5.Player` even once more player types migrate.

**Serialized identity.** `CallBallToPlayer` is a `MonoBehaviour` carried by 71 authored prefabs (no
scene stores the script GUID directly), so its `.meta` was moved with `git mv` rather than
regenerated; the moved `.meta` blob hashes identically to the original
(`c7688e4cfa11361ef8bd586b1eef1bc655faf301`) and the GUID is unchanged at
`567ca935929c9cd4ea9d04d51ef75440`. The same two cross-assembly hazards Slice 23 checked were checked
again and are both clear: no authored asset stores an assembly-qualified `m_TargetAssemblyTypeName`
naming this type, and no `.anim`/`.controller` names `pullBallToPlayer`/`pullBallToPlayerAuto` as an
animation event — both are called from C# only. Nothing in this project uses `[SerializeReference]`.
No prefab, scene, animation asset or animator controller was edited or resaved.

**One visibility consequence, deliberate and behaviour-neutral.** `internal float pullSpeed` is now
internal to `Level5.Player` rather than to `Assembly-CSharp`. No production caller ever named it (its
only reads and writes are inside the component itself), so nothing broke; the new EditMode fixture
reads it by reflection rather than widening the field, since this migration changes no visibility.

Headless Unity `6000.5.7f1` batch compile clean, zero `CS` errors, with every existing caller compiling
unchanged; Unity rebuilt `Library/ScriptAssemblies/Level5.Player.dll` containing the type. Extended
`Level5ProductionAssemblyBoundaryTests` with one focused identity assertion,
`CallBallToPlayerCompilesIntoLevel5Player`, mirroring `PlayerSwapAttackCompilesIntoLevel5Player`; no
second dependency scanner, source parser, assembly registry or architecture-test framework was added.
Added `Assets/Tests/Editor/Level5CallBallToPlayerMatchRulesTests.cs`, which asserts the policy as
behaviour rather than as a bound reference — ordinary rules leave calling the ball enabled, each half
of the gate alone leaves it enabled, `Hardcore + EnemiesOnly` disables it, and all four point-contest
shot rules re-enable it (parameterized) — plus the bind-once/null/fail-closed contract, and drives the
real private `RegisterHuman`/`RegisterCpu` composition path (the same technique
`Level5ShotMeterOwnershipTests` already uses) to prove both participant routes receive *this* match's
rules: the coordinator is built with `Hardcore + EnemiesOnly`, so a participant that bound nothing
would log a composition error and a participant that bound some other default rules object would leave
`CallEnabled` true — both fail the assertion. Focused EditMode run: 30/30 across both fixtures,
including `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` and every pre-existing identity
guard. Serialized-component resolution was verified directly rather than inferred, via a throwaway
editor pass (created, run, deleted — not committed) that loaded one human prefab
(`player_ak47.prefab`), one CPU prefab (`cpu_player_ak47.prefab`) and the scene auto-player prefab
(`auto_player_drblood.prefab`): all three resolve `CallBallToPlayer`, report zero missing-script
components, and keep their authored serialized values (`pullSpeed` 2, `CallEnabled` true, `locked`
false); the same pass confirmed `typeof(CallBallToPlayer).Assembly` is `Level5.Player` while
`AssetDatabase` still maps the script to `567ca935929c9cd4ea9d04d51ef75440`, and confirmed
`cpu_player_defense_oldreal.prefab` legitimately has no such component. That pass was re-run after
`_basketBallState` was removed, with identical results — the surviving serialized values are
unaffected by dropping a field that was null in every prefab. Per this repository's
risk-based validation policy the combat, player-movement, CPU and basketball suites were not re-run:
this slice changes where the rules come from, not what the policy decides, and the compile plus
policy/composition/identity/boundary/prefab-resolution evidence already establishes that claim; PR CI
owns broader regression coverage.

**`PlayerController` dependency closure scan, freshly remeasured after this slice (2026-09-08).**
Re-derived from current declarations rather than by subtracting one from Slice 23's count: every
identifier `PlayerController.cs` actually references (comments and string literals stripped) was
matched against every type declaration under `Assets/`, and each match resolved to its nearest
enclosing `.asmdef`, which is authoritative. Group A is unchanged from Slice 23 except that
`CallBallToPlayer` has joined `PlayerSwapAttack` in `Level5.Player`:

**A. Already owned by custom assemblies — legal references, not blockers (13).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, **`CallBallToPlayer`** (this slice)

**B. Still compiled into `Assembly-CSharp` — the actual remaining blockers (10, down from 11).**

- **input:** `PlayerInputReader` (`Assets/Scripts/input/`, *outside* `Level5Input/`)
- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerHealth`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`CallBallToPlayer` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared. Note that `PlayerController` still reaches `MatchRuntime` on its own account —
this slice cut `CallBallToPlayer`'s edge to it, not the controller's — so `MatchRuntime` remains a
group B blocker. `PlayerController` itself remains in `Assembly-CSharp` (`Assets/Scripts/player/`), and
moving it into `Level5.Player` stays blocked on all ten group B entries; six of those are its own
sibling components under `Assets/Scripts/player/`, so that folder still cannot be absorbed wholesale.
The "gameplay adapters/helpers" line is now down to a single entry, `ShooterAttributesMapper`.

**Running total after slices 1-24:** unchanged in assembly count from Slice 23 — 18 production runtime
assemblies — since this slice moved one file into the existing `Level5.Player` rather than creating a
new assembly. `Level5.Player` now holds `PlayerSwapAttack.cs`/`.meta` and
`callBallToPlayer.cs`/`.meta`, both moved with their GUIDs intact. `Assets/Scripts/misc/` outside
`Level5Misc/` is one file smaller. Everything else in the Slice 23 running total is unchanged.

**Slice 25 - `Level5.Player` (2026-09-08), its third type: `PlayerHealth`, after inverting that
type's `MatchRuntime` dependency.** The same shape as Slice 24: a dependency had to be removed before
the move became legal. `PlayerHealth.Update()` read `MatchRuntime.Rules` five times to decide whether
regeneration runs, and `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`) is
still `Assembly-CSharp`, so it was the single edge keeping this component out of any custom assembly.
Everything else it names was already owned: `IDamageable`/`DamageInfo` by `Level5.Combat`,
`ResolvedMatchRules`/`SniperMode` by `Level5.Core`. The read is now an explicitly bound
`ResolvedMatchRules` supplied by the existing participant composition, and
`Assets/Scripts/player/PlayerHealth.cs`/`.meta` moved to `Assets/Scripts/player/Level5Player/`
alongside `PlayerSwapAttack` and `CallBallToPlayer`.

**`PlayerHealth` was not merged into `ActorHealth` and its contract did not change.** The two stay
separate deliberately - `ActorHealth.cs`'s own summary already records why - because `PlayerHealth`
owns player-only block, special and regeneration state while sharing the `IDamageable` contract.
Preserved unchanged: the global namespace, the type name, all eleven `[SerializeField]`s (`health`,
`maxHealth`, `block`, `maxBlock`, `special`, `maxSpecial`, `regenerateBlockRate`,
`regenerateHealthRate`, `regenerateSpecialRate`, `regenerateTimeDelay`, `isDead`) in the same order
and with the same types, the four public events, every public property including the
`Health`/`Block`/`Special` clamping setters and the `IsDead` death latch, `TakeDamage`, `ApplyDamage`,
`Heal`, `SpendBlock`, `SpendSpecial`, the `Awake` maxima, the three startup regeneration rates set in
`Start`, and all three regeneration coroutines with their intervals. The `regenerateTimeDelay` field is
authored-but-unread and was deliberately left in place: unlike Slice 24's `_basketBallState` it forces
no assembly reference, so removing it would be exactly the unrelated cleanup this phase prohibits.

**The regeneration predicate was preserved as-is, including its redundancy.** It still reads
`EnemiesEnabled || SniperEnabled || Sniper == Bullet || Sniper == Laser || ObstaclesEnabled`. The
middle three terms are reducible - `SniperEnabled` is `Sniper != None`, so it already subsumes both
explicit comparisons - but simplifying a live gameplay gate is not this slice's business and nothing in
the repository establishes that the redundancy is unintentional. Only the source of the rules changed.
`SpawnCoordinator` supplies facts and does not decide when regeneration runs; no match-context service,
provider interface, registry, global fallback or generalized player-binding framework was introduced.

**`BindMatchRules` follows the repository's bind-once shape**, copied from `CallBallToPlayer` (and
`BasketBall`/`BasketBallState` before it) including the guard ordering: the already-bound branch is
checked *before* the null-argument branch, so a null second call after a real bind reports "already
bound" rather than "remaining unbound" and cannot obscure the original valid reference.

**An unbound component keeps every non-regeneration behaviour.** Missing composition is reported once
from `Start()` - not per frame from `Update()`, which is what a naive null check would have produced -
and `Update()` then returns before the gate. Damage, healing, block/special spending, clamping, the
death latch and all four events keep working; the component is not disabled, the player object is not
disabled, no default `ResolvedMatchRules` is invented, and nothing reaches back into `MatchRuntime`.
Only regeneration is skipped. This differs from `CallBallToPlayer`'s fail-closed handling because the
two failures are not alike: there, an unbound instance had a policy flag to fall back to; here, health
processing is the component's primary job and has to survive a composition defect.

**Composition binds from both registration paths, through one shared helper.**
`SpawnCoordinator.BindPlayerHealthMatchRules(GameObject)` is called from `RegisterHuman` and from
`RegisterCpu`, next to the existing `BindRangeMeters`/`BindShotMeters`/`BindCallBallMatchRules` calls,
so every route that produces a participant is covered: the primary human, additional roster humans,
roster CPUs, a scene-supplied `autoPlayer`, and Lockdown's defender. It runs inside
`GameLevelManager.Awake`'s spawn pass, so it always precedes the component's own `Start()`.

Unlike `BindCallBallMatchRules`, it uses `GetComponentInChildren<PlayerHealth>(true)`, not
`GetComponent`, and the prefab probe below is what settled that rather than an assumption: in all four
representative prefabs the component is authored on a child named `hitbox`, never on the participant
root. Every live consumer already resolves it that way (`PlayerController`, `AutoPlayerController`,
`PlayerCollisions`, `AutoPlayerCollisions`, `GameLevelManager`), so a root-only lookup would have bound
nothing at all and left every participant logging a composition error. `(true)` so an inactive authored
copy is reached as well; binding has no side effects. A participant without one is silently skipped, and
nothing is ever added. Also unlike Slice 24: Lockdown's defender prefab (`cpu_player_defense_oldreal`)
*does* carry `PlayerHealth`, so it is bound through its existing `RegisterCpu` route rather than being a
legitimate skip.

**`Level5.Player.asmdef` gains exactly one reference: `"Level5.Combat"`,** joining the `"Level5.Core"`
Slice 24 added. `Level5.Core` covers `ResolvedMatchRules` and `SniperMode`; `Level5.Combat` covers the
`IDamageable` interface this component implements and the `DamageInfo` its `TakeDamage`/`ApplyDamage`
name. Both were verified against the final compiled source rather than copied from the audit's
expectation. Nothing was pre-added for player types that may migrate later.

**The graph was rechecked from current `.asmdef` files after the edit.** `Level5.Player ->
{Level5.Core, Level5.Combat}`; `Level5.Core` declares no references at all and `Level5.Combat` declares
an empty list, so both outbound edges are one deep and terminal. No `.asmdef` in the project names
`Level5.Player`, so it is still the target of no edge and cannot be part of a cycle. The direction is
unchanged: `Assembly-CSharp composition/controllers -> Level5.Player -> {Level5.Core, Level5.Combat}`.

**Serialized identity.** `PlayerHealth` is a `MonoBehaviour` carried by 72 authored assets (71 prefabs
plus `Assets/Scenes/level_01_scrapyard_cpu_defense_test.unity`, which stores the script GUID directly),
so its `.meta` was moved with `git mv` rather than regenerated and the GUID is unchanged at
`773787b24fb812a4b818381a01e71101`. The same two cross-assembly hazards Slices 23 and 24 checked were
checked again and are both clear: no authored asset stores an assembly-qualified
`m_TargetAssemblyTypeName` naming this type, and no `.anim`/`.controller` names any of its methods as an
animation event - the regeneration coroutines and the spend/heal methods are called from C# only.
Nothing in this project uses `[SerializeReference]`. No prefab, scene, animation asset or animator
controller was edited or resaved.

Headless Unity `6000.5.7f1` batch compile clean, zero `CS` errors, with every consumer compiling
unchanged. Extended `Level5ProductionAssemblyBoundaryTests` with one focused identity assertion,
`PlayerHealthCompilesIntoLevel5Player`, mirroring `CallBallToPlayerCompilesIntoLevel5Player`; no second
dependency scanner, source parser, assembly registry or architecture-test framework was added. Added
`Assets/Tests/Editor/Level5PlayerHealthMatchRulesTests.cs`, which asserts the gate as behaviour rather
than as a bound reference - no enabling rule leaves regeneration shut, and enemies, obstacles and each
of the three sniper modes each open it (parameterized) - plus the bind-once/null contract, the
report-once-in-`Start()` behaviour, and a test that an unbound component still takes damage, clamps a
heal past max, latches death and raises `OnDied`. The composition tests drive the real private
`RegisterHuman`/`RegisterCpu` path (the technique `Level5ShotMeterOwnershipTests` and Slice 24's fixture
already use) with the component on a *child*, as the prefabs author it, and with coordinator rules that
enable enemies: a participant that bound nothing would log a composition error and one that bound some
default rules object would leave the gate shut, so both failure modes fail the assertion. The gate is
observed through the coroutine's own `regenerateBlock` flag, whose first segment runs synchronously
inside `StartCoroutine` - deterministic, no timing, and no widening of the component's contract;
existing coroutine intervals were not retested. Focused EditMode run: 45/45 across the three fixtures,
including `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` and every pre-existing identity guard.
Serialized-component resolution was verified directly rather than inferred, via a throwaway editor pass
(created, run, deleted - not committed) over one human prefab (`player_ak47.prefab`), one regular CPU
prefab (`cpu_player_ak47.prefab`), the Lockdown defender (`cpu_player_defense_oldreal.prefab`) and the
scene auto-player (`auto_player_drblood.prefab`): all four resolve `PlayerHealth` on their `hitbox`
child, report zero missing-script components, and keep their authored serialized values (`maxHealth`
100, `maxSpecial` 100, `isDead` false throughout; `maxBlock` 20 on the human and 25 on the three CPU
variants; `regenerateBlockRate` 0 on the human and 0.5 on the three CPU variants - all authored values,
and all overwritten at runtime by `Start()` exactly as before). The same pass confirmed
`typeof(PlayerHealth).Assembly` is `Level5.Player` while `AssetDatabase` still maps GUID
`773787b24fb812a4b818381a01e71101` to the new path. Per this repository's risk-based validation policy
the combat, player-movement and CPU suites were not re-run: this slice changes where the rules come
from, not what the gate decides, and the compile plus gate/composition/identity/boundary/prefab-
resolution evidence already establishes that claim; PR CI owns broader regression coverage.
`validate-repository.ps1` passed.

**`PlayerController` dependency closure scan, freshly remeasured after this slice (2026-09-08).**
Re-derived from current declarations rather than by subtracting one from Slice 24's count: every
identifier `PlayerController.cs` actually references (comments and string literals stripped) was matched
against every top-level type declaration under `Assets/`, and each match resolved to its nearest
enclosing `.asmdef`, which is authoritative. Group A is unchanged from Slice 24 except that
`PlayerHealth` has joined `PlayerSwapAttack` and `CallBallToPlayer` in `Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (14).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, **`PlayerHealth`** (this slice)

(Scan note for whoever remeasures next: `PlayerControls` is declared as the verbatim identifier
`public partial class @PlayerControls` in the Input System's generated file, so an identifier-based scan
that does not account for the `@` prefix reports 13 here instead of 14. It is `Level5.Input`-owned
either way and is not a blocker.)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (9, down from 10).**

- **input:** `PlayerInputReader` (`Assets/Scripts/input/`, *outside* `Level5Input/`)
- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`PlayerHealth` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared. `PlayerController` still reaches `MatchRuntime` on its own account - this slice cut
`PlayerHealth`'s edge to it, not the controller's - so `MatchRuntime` remains a group B blocker.
`PlayerController` itself remains in `Assembly-CSharp` (`Assets/Scripts/player/`), and moving it into
`Level5.Player` stays blocked on all nine group B entries; five of those are its own sibling components
under `Assets/Scripts/player/`, down from six, so that folder still cannot be absorbed wholesale.

**Running total after slices 1-25:** unchanged in assembly count from Slice 24 - 18 production runtime
assemblies - since this slice moved one file into the existing `Level5.Player` rather than creating a new
assembly. `Level5.Player` now holds `PlayerSwapAttack.cs`/`.meta`, `callBallToPlayer.cs`/`.meta` and
`PlayerHealth.cs`/`.meta`, all moved with their GUIDs intact, and is the first player assembly to declare
`Level5.Combat`. `Assets/Scripts/player/` outside `Level5Player/` is one file smaller. Everything else in
the Slice 24 running total is unchanged.

**Slice 26 - `Level5.Input` (2026-09-08), its fourth type: `PlayerInputReader`, after cutting that
type's `GameLevelManager` and `TouchInputController` reads.** The same shape as Slices 24 and 25:
dependencies had to be removed before the move became legal. `PlayerInputReader` named exactly two types
still compiled into `Assembly-CSharp` - `GameLevelManager` (`Assets/Scripts/game manager/`) inside its
mobile movement fallback, and `TouchInputController` (`Assets/Scripts/input/`, *outside*
`Level5Input/`) inside `TouchBlockHeld`. Everything else it names was already owned: `PlayerControls` and
`PlayerTouchInputState` by `Level5.Input` itself, the rest by `System`/`UnityEngine`.
`Assets/Scripts/input/PlayerInputReader.cs`/`.meta` moved to `Assets/Scripts/input/Level5Input/`
alongside them.

**The `TouchInputController` read was removed as a duplicate, not replaced.** `TouchBlockHeld` returned
`PlayerTouchInputState.BlockHeld || TouchInputController.instance.HoldDetected`. Those were never
semantically independent states: every write to `TouchInputController.hold1Detected` sets the identical
value on `PlayerTouchInputState.BlockHeld` in the same statement pair - hold begin, hold end, the special
release, and the disable-time `PlayerTouchInputState.Clear()` that covers `BlockHeld` too - and the
`HoldDetected` property has no other production reader or writer anywhere in the repository (the only
other mention is a commented-out line in `RacingVehicleController`). Dropping the second read therefore
cannot change what the property returns. `HoldDetected` was left in place, `TouchInputController` was not
migrated or redesigned, and the touch gesture/queueing model is untouched.

**The `GameLevelManager` read was inverted, and deliberately kept synchronous.** The reader took
`GameLevelManager.instance.Joystick.Horizontal`/`.Vertical` inside `ReadLegacyTouchMove`, on the frame it
ran. It now takes an optional trailing `Func<Vector2>` constructor parameter and invokes it at that same
point, so the axes are still read at the moment of use rather than becoming a value cached a frame
earlier - the same distinction Slice 21 drew for `IGroundHeightProvider`. A null source reads as
`Vector2.zero`, which is exactly what the old `GameLevelManager.instance == null ||
...Joystick == null` guard produced. Nothing else in `ReadLegacyTouchMove` changed: the Input System
`Player/movement` read still wins whenever it has any magnitude, the touch-start position tracking and
the `screenXRange`/`screenYRange` distance scaling are arithmetically identical, and the fallback is
still *invoked* only under `(UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`. **This is a dependency
inversion, not an input redesign: the legacy `FloatingJoystick` fallback stays, `OnScreenStick` was not
added, no `.inputactions` asset or action name was touched, and no `PlayerInput`/`PlayerInputManager`
was introduced.** See `docs/player-input-architecture.md`.

**The callback reaches the reader through the existing human-participant composition path.**

```text
GameLevelManager.ReadLegacyTouchMovement()          // joystick.Horizontal / joystick.Vertical, or zero
  -> SpawnCoordinator.BindHumanLegacyTouchMovement  // human participants only; CPUs are skipped
    -> PlayerController.BindLegacyTouchMovementReader
      -> new PlayerInputReader(controls, ReadLegacyTouchMovement)
```

`SpawnCoordinator`'s constructor was not widened: the new bind is a sibling method of Slice 21's
`BindHumanArenaContext`, and the human-participant iteration both need is now one shared private
`BindEveryHumanController` helper rather than two copies of the same loop (the existing fail-closed
"has no PlayerController to bind ... to" error text and behaviour are preserved, which is what
`Level5PlayerControllerArenaContextTests` already asserts). `GameLevelManager` hands over a private
method returning the joystick's current axes, never the `FloatingJoystick` object, so `Level5.Input`
gains no dependency on it or on the vendored Joystick Pack. The bind runs inside
`GameLevelManager.Awake`'s spawn pass, where the joystick has already been resolved (it is resolved
before the coordinator is even constructed) and before any spawned `PlayerController.Start()` runs -
unlike `BindHumanArenaContext`, which has to wait for `Start()` because the rim is not final until
`ArenaBootstrap` has run. No new service, locator, provider registry, static analog state or generic
joystick abstraction was introduced, and CPU participants are not bound.

**Reader lifetime was the real risk in the inversion, and is where the coverage went.** A
`PlayerController` does not own one `PlayerInputReader` for its lifetime: `OnDisable` drops it along with
the acquired gameplay controls, `OnEnable`/`TryEnsureInputReader` rebuild it after reacquiring them, and
the `Controls` setter replaces it outright. A source captured only on the reader would therefore be lost
on the first disable/re-enable cycle. `PlayerController` stores the callback in its own field and hands
every one of those three construction sites the same controller-owned indirection, so binding order does
not matter and every rebuilt reader resolves the same live source.

**`Level5.Input.asmdef` gained no reference, verified from the final source.** The migrated file names
only `System` (`Func<Vector2>`), `UnityEngine`, `PlayerControls` and `PlayerTouchInputState`; the last two
already live in this assembly and the assembly already declares `Unity.InputSystem` for them. Its one
consumer, `PlayerController`, stays in `Assembly-CSharp` and reaches it through `autoReferenced`. Nothing
was pre-added for `TouchInputController`, `RacingInputReader` or any other input type that may migrate
later. The graph is unchanged in direction and depth: `Level5.Input -> Unity.InputSystem` only, and no
`.asmdef` in the project names `Level5.Input`, so it remains the target of no edge and cannot be part of
a cycle.

**Serialized identity.** `PlayerInputReader` is a plain C# class - not a `MonoBehaviour` or
`ScriptableObject` - so no prefab, scene or animation asset references it and none was edited. Its
`.meta` was moved with `git mv` rather than regenerated and the GUID is unchanged at
`85712732dbcee3f4f810900730093386`.

Headless Unity `6000.5.7f1` batch compile clean, zero `CS` errors and no new warnings in any touched
file. Extended `Level5ProductionAssemblyBoundaryTests` with one focused identity assertion,
`PlayerInputReaderCompilesIntoLevel5Input`, mirroring `PlayerHealthCompilesIntoLevel5Player`; no second
dependency scanner, source parser, assembly registry or architecture-test framework was added. Added
`Assets/Tests/Editor/Level5PlayerInputReaderCompositionTests.cs`, which covers only the two behaviours
this slice made load-bearing: `TouchBlockHeld` following `PlayerTouchInputState.BlockHeld` in both
directions and through `Clear()`, and the composition/lifetime contract - a human participant's reader
resolving the bound source, multiple humans sharing it, CPUs skipped, a controller-less human logging and
continuing, an unbound controller reading `Vector2.zero` rather than throwing, the source being read live
rather than snapshotted at reader construction, and the source surviving a reader rebuilt through each of
the three construction sites, including the real `OnDisable`/`OnEnable` release-and-reacquire pair. The
bound source is asserted through the reader's stored delegate because `ReadLegacyTouchMove` itself is
compiled out of an Editor run; that limitation is recorded in the fixture. Focused EditMode run: 38/38
across the four fixtures, including `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` (which is what
would catch a surviving `GameLevelManager`/`TouchInputController` reference), every pre-existing identity
guard, `Level5PlayerControllerArenaContextTests` for the refactored bind loop, and
`Level5PlayerControllerDependencyGuardTests`. Per this repository's risk-based validation policy the
broader input, player-movement and PlayMode suites were not re-run: this slice changes where the legacy
axes come from, not what any input read decides, and no `.inputactions`, prefab or scene asset was
touched. `validate-repository.ps1` passed.

**Mobile-only compilation: not run - target support unavailable. The gap it left was closed a
different way instead.** This machine's `6000.5.7f1` install has only `windowsstandalonesupport` in
`PlaybackEngines`; no Android or iOS module is present, and none was installed for this slice. As
originally written that left this slice's only behavioural edit - the three lines that turn the composed
`Func<Vector2>` into `movementHorizontal`/`movementVertical` - checked by nothing at all: excluded from
every Editor compile by `!UNITY_EDITOR`, unreachable from any test, and unbuildable on a runner without a
mobile module. A transposition of the two axes would have shipped silently. Code review caught that, and
two changes fix it without installing a platform module:

- **The `#if` moved off the method and stayed on the call.** `ReadLegacyTouchMove` is now compiled on
  every target while `ReadMove` still only calls it under the mobile conditions, so the platform
  behaviour is unchanged and the body is type-checked by every compile this project runs. This is safe
  because the project sets `activeInputHandler: 2` (Both), so legacy `UnityEngine.Input` -
  `Input.touchCount`, `Input.touches`, `Touch`, `TouchPhase` - resolves on every target, which the same
  file already depends on for `Input.GetKeyDown` in `DebugLightningPressed`. Verified by a full headless
  compile: zero errors, zero new warnings, and the cost is one unreachable private method in a non-mobile
  player.
- **The scaling arithmetic was split into a pure static, `ScaleByTouchDistance`.**
  `ReadLegacyTouchMove` returns early whenever `Input.touchCount` is zero, which it always is on a CI
  runner, so the method as a whole still cannot be driven from a test. The part worth protecting - the
  axis mapping and the per-axis attenuation - now can be, and is: four focused tests cover pass-through
  with no range, each axis scaling by its own displacement (the transposition guard), full magnitude at
  or past the range, and `Mathf.Abs` on a backwards drag. No behaviour changed; the statements moved
  verbatim.

A mobile build is still the only thing that proves `Input.touches` behaves as expected on device, and the
`OnScreenStick` migration remains the real fix - but the slice's own edit is no longer unverifiable.

**`PlayerController` dependency closure scan, freshly remeasured after this slice (2026-09-08).**
Re-derived from current declarations rather than by subtracting one from Slice 25's count, using the same
method: every identifier `PlayerController.cs` actually references (comments and string literals
stripped) matched against every top-level type declaration under `Assets/`, each match resolved to its
nearest enclosing `.asmdef`. Group A is unchanged from Slice 25 except that `PlayerInputReader` has
joined `PlayerControls` and `PlayerControlsProvider` in `Level5.Input`:

**A. Already owned by custom assemblies - legal references, not blockers (15).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, **`PlayerInputReader`** (this slice)
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`

(Scan notes for whoever remeasures next, both affecting group A's count only, never the blocker set:
`PlayerControls` is declared as the verbatim identifier `public partial class @PlayerControls`, so a scan
that does not strip the `@` misses it; `ShooterAttributes` is a `public readonly struct`, so a scan whose
declaration pattern lacks `readonly` misses that one. Both are custom-assembly-owned either way.)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (8, down from 9).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`, *outside* `Level5Match/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`, no asmdef)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`PlayerInputReader` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared - `PlayerController` names no new type, since the callback it now stores is a
`System.Func<UnityEngine.Vector2>`. `PlayerController` itself remains in `Assembly-CSharp`
(`Assets/Scripts/player/`), and moving it into `Level5.Player` stays blocked on all eight group B
entries; five of those are still its own sibling components under `Assets/Scripts/player/`, so that
folder still cannot be absorbed wholesale. `Assets/Scripts/input/` outside `Level5Input/` still holds
`TouchInputController`, `RacingInputReader`, `UiSelectionAdapter`, the six menu `TouchInput*Controller`
scripts and `PlayerControls.inputactions`, none of which this slice touched.

**Running total after slices 1-26:** unchanged in assembly count from Slice 25 - 18 production runtime
assemblies - since this slice moved one file into the existing `Level5.Input` rather than creating a new
assembly. `Level5.Input` now holds `PlayerControls.cs`/`.meta`, `PlayerControlsProvider.cs`/`.meta`,
`PlayerTouchInputState.cs`/`.meta` and `PlayerInputReader.cs`/`.meta`, the last moved with its GUID
intact, and still declares only `Unity.InputSystem`. `Assets/Scripts/input/` outside `Level5Input/` is one
file smaller. Everything else in the Slice 25 running total is unchanged.

**Slice 27 - `CharacterProfile` (2026-09-08): dependency cut only - no ownership move.**
`CharacterProfile.cs` stays exactly where it is, at `Assets/Scripts/player/CharacterProfile.cs`, in
`Assembly-CSharp`. Its `.meta` is untouched, no `.asmdef` changed, and no prefab or scene asset was
edited. The point of the slice is to make the type dependency-closed *first*, so the later serialized
ownership move is a file move and nothing else - the two risks (inverting live gameplay state
discovery, and changing which assembly a `MonoBehaviour` serializes from) are deliberately not taken in
the same PR.

**The two cuts.** `CharacterProfile` named exactly two types still compiled into `Assembly-CSharp`:

- **`LoadedData`** - `intializeShooterStatsFromProfile(int)` opened with
  `LoadedData.instance.getSelectedCharacterProfile(characterId)`, i.e. the profile reached into the
  menu-boot persistence singleton for the saved data it rebuilds a human from.
- **`MatchRuntime`** - `MatchRuntime.Cheerleader`'s seven bonus properties inside the same rebuild,
  `MatchRuntime.Rules`'s four point-contest flags for the Luck/Clutch suppression, and
  `MatchRuntime.Rules.ArcadeMode`/`.Difficulty` in `Start()`.

Both now arrive through one runtime-only preparation method,
`PrepareHumanMatchContext(Func<int, CharacterProfile>, CheerleaderSelection, ResolvedMatchRules)`,
mirroring the `PrepareCpuMatchContext(int, ResolvedMatchRules)` #71 already added. Nothing is
serialized: no resolver, cheerleader or rules field is `[SerializeField]`, so no prefab or scene
carries any of it. **No service, repository interface, service locator, DI container or participant-
context object was introduced** - the resolver is a plain `System.Func<int, CharacterProfile>` and the
other two are the value objects composition already holds. `CharacterProfile` still owns every stat and
bonus policy; composition only supplies the inputs.

**One prepared-rules field, written by both paths.** `Start()` needs resolved rules for a human *and*
for a CPU, so the human and CPU preparation methods write the same `preparedRules` field rather than two
that could disagree about the same match - and that reference is itself the record that rules arrived,
rather than a parallel bool a later edit could forget to keep in step. The CPU-specific context
(`preparedBaseCpuLevel`, `preparedPrimaryHumanLevel`, `hasPreparedMatchContext`) is untouched, and
`ApplyPreparedCpuMatchInitialization`'s Hardcore bump, contest suppression and no-context safe baseline
are unchanged - `Level5CpuBaselineInitializationTests` still passes on them. `hasPreparedMatchContext`
survives because it records that the CPU path specifically ran, which the rules reference alone cannot
say (a human is prepared with rules and no CPU context), and it is derived from the same `rules != null`
condition: a prepare with no rules now takes the documented safe-baseline path rather than claiming a
context whose rules `ApplyPreparedCpuMatchInitialization` would dereference. Production never passes null
there, so this only decides how an already-broken caller fails.

**Composition prepares every human, before the configured-match gate.** This ordering is the slice's
one genuinely load-bearing decision. `SpawnCoordinator.InitializeHumanProfile` used to return
immediately when `MatchRuntime.HasConfiguration` was false; it now resolves the profile, prepares its
context, and only then decides whether to run the saved-profile rebuild:

```text
RegisterHuman -> InitializeHumanProfile
    -> PrepareHumanMatchContext(ResolveLoadedCharacterProfile, MatchRuntime.Cheerleader, rules)
    -> MatchRuntime.HasConfiguration ?
         yes -> intializeShooterStatsFromProfile(ResolveHumanCharacterId(slot))
         no  -> (direct scene) no saved-profile rebuild, rules context still held
```

Preparing inside the gate would have silently dropped the Arcade/easy maximum-stat override for a
directly entered gameplay scene, which previously got it from `MatchRuntime.Rules`'s legacy-globals
fallback. The distinction the direct-scene path has always drawn - legacy-derived match rules yes,
configured-match saved profile no - is therefore preserved exactly. `InitializeHumanProfile` became an
instance method to reach the coordinator's already-resolved `rules`; the constructor was not widened. The
gate itself now reads the coordinator's captured `hasActiveMatchConfiguration` instead of re-reading
`MatchRuntime.HasConfiguration` - that live read existed only because the method used to be `static`, and
this class already captures configuration presence once on purpose (AUD-010 Phase 2b0). Both are sampled
inside `GameLevelManager.Awake`, after `ActiveMatch` is established for the scene load and before any
participant spawns, so the value is the same one; it is now read from the field the class owns.
The `identifier.characterProfile == null` error now precedes the configuration gate rather than
following it, so a human prefab missing that component is reported in a directly entered scene too -
the only behavioural difference outside the dependency cut, and it reports a real defect.

**The composition side keeps the global knowledge.** A four-line private static adapter,
`SpawnCoordinator.ResolveLoadedCharacterProfile(int)`, wraps `LoadedData.instance` (carrying over the
profile's own null guard) into the `Func`; the cheerleader is still read from `MatchRuntime.Cheerleader`
here, which is fine - the requirement was to remove that dependency from `CharacterProfile`, not to
finish removing `MatchRuntime` from `SpawnCoordinator`. `LoadedData` and persistence architecture are
otherwise untouched. `SpawnCoordinator.cs` is already on both allowlists in
`Level5GameManagerEdgeTests` (spelled types, and `.characterProfile.` reach-through), and `LoadedData`
is not a restricted name, so no allowlist grew.

**Missing context fails loudly and never falls back.** No hidden `LoadedData`/`MatchRuntime` path was
kept. An `intializeShooterStatsFromProfile` call with no prepared resolver, cheerleader or rules logs an
actionable composition error naming the method that should have run, and returns before mutating
anything. An instance reaching `Start()` with no prepared rules keeps its context-free initialization
(`fadeaway`/`InAirSpeed`, and for a CPU the safe baseline), skips only the match-derived Arcade/easy
override, disables nothing, invents no rules, and emits at most one Editor/Development warning - the CPU
branch's existing "no match context prepared" warning suppresses the second one, so an unprepared shooting
CPU still reports once, not twice, while a defensive CPU (which never runs that method) is still reported
by `Start` itself. That skip is written as an `else if` around the override rather than an early `return`,
so "continue context-free initialization" stays true for whatever `Start` grows later instead of holding
only because the override happens to be its last statement today.

**`CharacterProfile` is now fully dependency-closed, measured from the final source.** Same method as
the `PlayerController` scans: identifiers with comments and string/char literals stripped, matched
against every top-level declaration under `Assets/`, each resolved to its nearest enclosing `.asmdef`.
Every type it names is now custom-assembly-owned - `Level5.Constants` (`CpuBaseStats`, and the nested
`ShooterType`) and `Level5.Core` (`CharacterLevel`, `CheerleaderSelection`, `CpuDifficultyLevelPolicy`,
`MatchDifficulties`, `ResolvedMatchRules`) - with **zero `Assembly-CSharp` blockers**. The physical move
into `Level5.Player` is therefore nominated as a later ownership slice. It will need
`Level5.Player -> Level5.Constants` added to `Level5.Player.asmdef`, which currently declares only
`Level5.Core` and `Level5.Combat`; **that reference was deliberately not added in this slice**, since
nothing in `Level5.Player` needs it yet. That move is also the slice that carries the serialized risk
this one avoids: `CharacterProfile` is a `MonoBehaviour` on every character prefab, so it needs the
prefab/scene and Missing Script verification explicitly excluded here. `ShooterAttributesMapper` was
not moved and is not touched: it remains blocked on `CharacterProfile`'s ownership and becomes a
simpler follow-on once that changes.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-08).** Re-derived
from current declarations by the same scan rather than carried over: group A is **15**, unchanged from
Slice 26 (`Level5.Input`: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`;
`Level5.Core`: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`; `Level5.Basketball`:
`BasketBall`, `ShotMeter`, `BasketBallState`; `Level5.Utility`: `SceneObjects`, `UtilityFunctions`,
`RigidbodyFreezeHelper`; `Level5.Player`: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`), and
group B is **8, unchanged from Slice 26**:

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `CharacterProfile`, `PlayerDunk`,
  `PlayerAttackQueue`, `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `ShooterAttributesMapper` (`Assets/Scripts/player/`)

An unchanged count is the expected result and was not forced: no type changed assembly ownership in
this slice, `CharacterProfile` still compiles into `Assembly-CSharp`, and `PlayerController.cs` itself
was not edited. What changed is that one of those eight is now ready to move.

**Validation.** Headless Unity `6000.5.7f1` batch compile clean - zero `CS` errors, and zero new
warnings in either touched file. Focused EditMode run: **158/158 passed** across eleven fixtures -
the two new ones, `Level5CharacterProfileDependencyGuardTests` (the source-level guard: with comments
*and* string literals stripped via the existing `Level5TestSourceText.StripCommentsAndLiterals`,
`CharacterProfile.cs` names neither `MatchRuntime` nor `LoadedData`) and
`Level5CharacterProfileMatchContextTests` (21 tests: resolver receives the requested id, the saved-data
copy contract, all seven cheerleader bonus categories, each of the four point contests suppressing
Luck/Clutch with ordinary rules keeping both, Arcade and easy applying the maximum-stat values with
ordinary rules not, both missing-context error paths mutating nothing, the one-diagnostic-per-participant
contract for an unprepared human and for both kinds of unprepared CPU, and four composition tests: a
configured human prepared *before* the rebuild - proven by which error a resolverless configured run
produces - two humans each rebuilding from their own roster slot's character id *and* from the match's
own cheerleader, a direct-scene human holding rules but loading no saved profile, and a human without a
`CharacterProfile` reported rather than crashing) - plus
`Level5CpuBaselineInitializationTests`, `Level5RangeMeterOwnershipTests`,
`Level5ShotMeterOwnershipTests`, `Level5CallBallToPlayerMatchRulesTests`,
`Level5PlayerHealthMatchRulesTests` (the four other fixtures that drive
`SpawnCoordinator.RegisterHuman`), `Level5GameManagerEdgeTests`,
`Level5ProductionAssemblyBoundaryTests`, `Level5PlayerControllerDependencyGuardTests` and
`Level5TestSourceTextTests`. The three "exactly one diagnostic" assertions count messages through a scoped
`Application.logMessageReceived` handler rather than `LogAssert`: Unity fails a test on unexpected
*errors*, not on unexpected *warnings*, so `LogAssert.Expect` can prove a warning happened but not that
it was the only one - and "one report per participant" is the contract. The configured-match fixture also
carries a cheerleader with real bonuses rather than `CheerleaderSelection.None`, because with a zero-bonus
cheerleader every assertion would still pass if composition handed the profile the wrong one. No second
source parser, scanner framework or architecture-test system was added - the guard reuses the existing
utility, in the same shape as `Level5BasketBallStateDependencyGuardTests`. One existing test was adjusted rather than deleted:
`Level5CpuBaselineInitializationTests.ArcadeEasyOverrideStillWinsAfterCpuMatchInitialization` set easy
difficulty through `GameOptions.difficultySelected`, which reached `Start()` via
`MatchRuntime.Rules`'s legacy fallback; it now carries `MatchDifficulty.Easy` on the prepared rules,
which is where production's value comes from too (`GameLevelManager.Awake` reads `MatchRuntime.Rules`
once and hands that exact instance to the coordinator). Per the risk-based policy, full EditMode/
PlayMode, player builds, prefab Missing Script probes and serialized-migration certification were **not**
run: no asset, `.meta` or `.asmdef` changed here, and those belong to the ownership move.
`validate-repository.ps1` passed.

Prohibited in Phase 2: controller convergence, player/CPU behaviour cleanup, locomotion changes,
input ownership changes, scene-search removal, namespace restructuring, API redesign, new service
layers, DI/service locators, shader/material changes, URP configuration changes, and scene or
environment polishing. If a dependency must be inverted solely to make an intended boundary legal,
make the smallest possible behaviour-preserving change and protect it with a dependency regression
test.

**Slice 28 - `CharacterProfile` (2026-09-09): the ownership move Slice 27 set up, plus a
same-assembly closure Slice 27's audit did not anticipate.** `CharacterProfile.cs` moves from
`Assets/Scripts/player/` into `Assets/Scripts/player/Level5Player/`, compiling into `Level5.Player`
for the first time. Three more files move with it in the same PR, not left behind:
`CharacterProfileStatMapper.cs`, `RuntimeCharacterStats.cs`, `CharacterStats.cs`. All four `.meta`
files moved with their source (`git mv`), preserving every GUID:

- `CharacterProfile` - `11a4e02d59c2d6841b3084444663e0f6`
- `CharacterProfileStatMapper` - `8fe900f6fc824be7bc6f9c7697a5eb11`
- `RuntimeCharacterStats` - `0ba110d6dc674971be77ec42413463c8`
- `CharacterStats` - `55a696f94614408cbaa18c08bc9c037b`

**Why four files, not one.** `CharacterProfile.IsLocked` is `public bool IsLocked { get; internal
set; }` - internal because it was never meant to be set from just anywhere. Once `CharacterProfile`
enters `Level5.Player`, only same-assembly code can use that setter. `CharacterProfileStatMapper.Apply`
writes it (`profile.IsLocked = !runtimeStats.unlocked`), so the mapper has to move too; the mapper
takes a `RuntimeCharacterStats`, whose `stats` field is a `CharacterStats`, so both value types come
with it. None of the three needed a source change - `CharacterProfileStatMapper`/`RuntimeCharacterStats`/
`CharacterStats` named nothing outside this closure or `Level5.Constants`/`Level5.Core` (already
verified dependency-closed for `CharacterProfile` itself, see Slice 27 above).

`Level5.Player.asmdef` gains exactly one new reference, `Level5.Constants`, for `CharacterProfile`'s
`CpuBaseStats` use (`CpuBaseStats.RANGE`, `.LUCK`, `.ShooterType`, etc.) - the reference Slice 27
deliberately did not add. Final references: `Level5.Core`, `Level5.Combat`, `Level5.Constants`. No
reverse edge: `Level5.Constants.asmdef` declares `"references": []`, so the new direction
(`Level5.Player -> Level5.Constants`) cannot cycle back.

**Deviation: `[assembly: InternalsVisibleTo("Assembly-CSharp")]`, added to a new
`Assets/Scripts/player/Level5Player/AssemblyInfo.cs`.** The Slice 27 audit's premise - that
`CharacterProfileStatMapper` is the only live writer of `IsLocked` - was wrong. Headless compilation
after the move failed with `CS0200` at `Assets/Scripts/menu_loading/LoadManager.cs:380`:
`temp.IsLocked = dbStats.IsLocked;`, inside `loadPlayerSelectDataList()` - the live, documented
primary-roster SQLite unlock path (`docs/persistence-boundaries.md`, "Unlock authority": `SQLite is
the unlock authority`, and `UnlockSnapshotBuilder` reads `profile.IsLocked` straight off the profiles
this method populates). This line existed unchanged at the audited baseline
(`f451df735db630dd87ec859bf8390b033a553ffb`) - the audit missed a real caller, this was not a race
with concurrent work. `CharacterProfileStatMapper.Apply` itself, by contrast, currently has no live
caller at all: its only caller, `CharacterRuntimeProvider.TryApplyRuntimeStats`, is not attached to
any prefab or scene and is not invoked from any other script.

`LoadManager` is not dependency-closed (tightly coupled to `DBHelper`/`DBConnector`/`LoadedData`) and
is far outside this slice's scope to move. Every technique this slice's instructions called out as
forbidden - a public setter, `InternalsVisibleTo`, reflection, a wrapper API - was ruled out for the
*known* mapper write; discovering a *second*, unaccounted-for live writer in an out-of-scope legacy
file was outside that list's premise. Presented to the user as a blocking choice between halting the
slice, a public setter, or `InternalsVisibleTo`; `InternalsVisibleTo` was chosen and approved because
it keeps `IsLocked`'s public contract exactly as documented for every consumer except the one
assembly that already has unrestricted access to everything else on `CharacterProfile`, and is a
one-line, trivially reversible attribute once `LoadManager`'s write is eventually migrated onto the
mapper flow - unlike a public setter, which would be a permanent, harder-to-undo widening of the
public API. `IsLocked` itself is untouched: still `public bool IsLocked { get; internal set; }`.

**Representative serialized validation.** GUID `11a4e02d59c2d6841b3084444663e0f6` is authored onto
185 prefabs (183 at the Slice 27 audit, two added since - not investigated, out of scope). Five
representative prefabs were loaded and checked for a Missing Script, that `CharacterProfile` resolves,
that `typeof(CharacterProfile).Assembly.GetName().Name` reports `Level5.Player`, and that authored
values survive: `Assets/Resources/Prefabs/characters/players/player_ak47.prefab` (gameplay human),
`Assets/Resources/Prefabs/characters/cpu_players/cpu_player_ak47.prefab` (gameplay CPU),
`Assets/Resources/Prefabs/menu_start/default_shooter_profiles/player_selected_ak47.prefab` (menu
default human profile), `Assets/Resources/Prefabs/menu_start/cpu_players_selected_objects/cpu_player_ak47.prefab`
(menu CPU-selected profile), and `Assets/Resources/Prefabs/basketball.prefab`. All five passed; none
were resaved. `AssetDatabase.GUIDToAssetPath` for that GUID resolves to the new
`Assets/Scripts/player/Level5Player/CharacterProfile.cs` path. No authored `CharacterPreset` asset
exists in the repository (`AssetDatabase.FindAssets("t:CharacterPreset")` returned zero) - `CharacterStats`'
serialization inside `CharacterPreset` is therefore unverified against real authored data; no synthetic
asset was created to force the check.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero `CS` errors (after the
`InternalsVisibleTo` deviation above; the pre-deviation attempt reproduced the `CS0200` finding).
Focused EditMode run: **44/44 passed** across four fixtures -
`Level5CharacterProfileDependencyGuardTests` (2, retargeted at the new path, still zero live
`MatchRuntime`/`LoadedData` references), `Level5CharacterProfileMatchContextTests` (21, unchanged,
rerun because its target type changed assembly), `Level5ProductionAssemblyBoundaryTests` (18,
including the new `CharacterProfileOwnershipTypesCompileIntoLevel5Player` identity assertion and the
still-green `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`), and the new
`Level5CharacterProfileStatMapperTests` (3: `unlocked` inverts into `IsLocked` in both directions, and
a representative set of ordinary numeric fields - `Accuracy2Pt`, `Range`, `Level`, `PlayerId` - copy
in the same `Apply` call). Per the risk-based validation policy, full EditMode/PlayMode were not
re-run; PR CI owns that broader regression coverage.

*(Corrected during Slice 29.)* This slice recorded `validate-repository.ps1` as "not re-run for this
source/meta/asmdef-only change (no repository invariant it checks was touched)". That justification
was wrong: the script's `.meta`-pairing check walks every file under `Assets/` and fails any asset
lacking a sibling `.meta`, so moving four `.cs` files plus their four `.meta` files sits squarely
inside the one invariant it enforces. The script was not run at the time. It was run during Slice 29
against a tree that already contains all four of this slice's moved files, and passed - so the
invariant is confirmed green for this slice's result, just not by this slice's own reporting. See
Slice 29's validation notes below for the corrected standard: a source-plus-`.meta` move runs it.

Not moved in this slice: `ShooterAttributesMapper`, `PlayerController`, or any other Group B blocker.
`ShooterAttributesMapper` remains the strongest next pure-move candidate now that `CharacterProfile`
has left `Assembly-CSharp`.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
Re-derived from current declarations, same method as every prior scan: identifiers with comments and
string/char literals stripped, matched against every top-level declaration under `Assets/`, each
resolved to its nearest enclosing `.asmdef`. Group A is **16, up from 15** - `CharacterProfile` joins
`Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (16).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, **`CharacterProfile`**
  (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (7, down from 8).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk`, `PlayerAttackQueue`,
  `PlayerDamageReactions` (all `Assets/Scripts/player/`)
- **gameplay adapters/helpers:** `ShooterAttributesMapper` (`Assets/Scripts/player/`)

`CharacterProfile` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared - `PlayerController.cs` itself was not edited. `PlayerController` remains in
`Assembly-CSharp` and stays blocked on all seven group B entries; four of those are still its own
sibling components under `Assets/Scripts/player/`. `ShooterAttributesMapper` was not moved here, but
this slice makes it dependency-closed for the first time: it names only `CharacterProfile` (now
`Level5.Player`) and `Level5.Core`'s `ShooterAttributes` - both already legal references. It stays in
group B only because it is itself still physically in `Assembly-CSharp`, not because anything it needs
is still blocked; it is the strongest next pure-move candidate.

**Slice 29 - `ShooterAttributesMapper` (2026-09-09): a pure assembly-ownership move, dependency-closed
since Slice 28.** `ShooterAttributesMapper.cs` moves from `Assets/Scripts/player/` into
`Assets/Scripts/player/Level5Player/`, compiling into `Level5.Player` for the first time. `.meta` moved
with it (`git mv`), preserving GUID `779f424a2b464bbcbb9a1f0baaf40bdd`. Production source is
byte-identical - path only, confirmed by `git diff -M --summary` reporting both as `rename ... (100%)`
with zero insertions or deletions. No dependency inversion was needed: the mapper's only project-type
references, `CharacterProfile` and `ShooterAttributes`, were already legal (`Level5.Player` and
`Level5.Core` respectively) once Slice 28 moved `CharacterProfile`.

**No serialized Unity asset surface**, unlike Slice 28's `CharacterProfile`: `ShooterAttributesMapper`
is a plain static class, never a `MonoBehaviour`/`ScriptableObject`, so no prefab, scene or asset
carries a `MonoScript` reference to it. Verified rather than assumed - a repository-wide search for
GUID `779f424a2b464bbcbb9a1f0baaf40bdd` returns only the script's own `.meta` and this document. No
prefab/scene Missing Script sweep was needed or run, and no authored asset was opened or resaved.

`Level5.Player.asmdef` is unchanged - still `Level5.Core`, `Level5.Combat`, `Level5.Constants`. The
mapper needs only `Level5.Core`, already present. Callers `PlayerController` and `AutoPlayerController`
are unchanged; both stay in `Assembly-CSharp` and reach the mapper through `autoReferenced`, same
pattern as every prior 2b leaf. Slice 28's `[assembly: InternalsVisibleTo("Assembly-CSharp")]`
(`Assets/Scripts/player/Level5Player/AssemblyInfo.cs`) is untouched - it exists for the unrelated
`CharacterProfile.IsLocked`/`LoadManager` boundary.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors. Focused EditMode run:
**38/38 passed** across two fixtures - `Level5ProductionAssemblyBoundaryTests` (19, including the new
`ShooterAttributesMapperCompilesIntoLevel5Player` identity assertion and the still-green
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`) and `Level5BasketballShotPipelineTests` (19,
unchanged - it already exercises `ShooterAttributesMapper.From` directly for both a populated profile,
through `ComputeLaunch`, and a null profile via `MissingCharacterProfileLogsAndFallsBackToAnInertShooter`,
which asserts the warning log and the zeroed fallback). No new mapper-behavior test was needed; existing
coverage already proved both paths. Per the risk-based validation policy, full EditMode/PlayMode were
not re-run; PR CI owns that broader regression coverage.

`validate-repository.ps1` **was** run for this slice and passed - a departure from Slice 28's
"source/meta-only, no invariant touched" reasoning, which was wrong on its own terms. The script's
`.meta`-pairing check walks every file under `Assets/` and fails any asset lacking a sibling `.meta`,
so a source-plus-`.meta` move is precisely the change class that invariant guards: moving a `.cs`
without its `.meta` (or the reverse) is the exact failure it exists to catch. Cheap to run, and it is
the one repository invariant this slice could plausibly have broken.

Not moved in this slice: `PlayerController` or any other Group B blocker.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
Re-derived from current declarations by an actual scan, not by subtracting this slice's type from the
Slice 28 list: `PlayerController.cs` stripped of comments and string/char literals, its remaining
identifiers matched against every top-level `public` type declared under `Assets/`, each hit resolved
to its nearest enclosing `.asmdef`. The scan is also a second, independent confirmation of the move
itself - it resolves `ShooterAttributesMapper` to `Level5.Player` by folder ownership, which is a
different mechanism from the runtime `typeof(...).Assembly` assertion added to
`Level5ProductionAssemblyBoundaryTests`. Group A is **17, up from 16** - `ShooterAttributesMapper`
joins `Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (17).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  **`ShooterAttributesMapper`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (6, down from 7).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk`, `PlayerAttackQueue`,
  `PlayerDamageReactions` (all `Assets/Scripts/player/`)

`ShooterAttributesMapper` is the only entry that left group B; nothing else moved between groups, and
no new dependency appeared - `PlayerController.cs` itself was not edited. The "gameplay
adapters/helpers" line that tracked `ShooterAttributesMapper` since Slice 24 is now empty; all six
remaining blockers are `PlayerController`'s own sibling components or cross-folder runtime/sniper
dependencies. `PlayerController` remains in `Assembly-CSharp` and stays blocked on all six group B
entries.

**Slice 30 - `PlayerAttackQueue`'s external dependencies removed; `EnemyPopulationRules` moved
(2026-09-09): dependency-preparation, not an ownership migration.** Two changes, not one move:
`EnemyPopulationRules.cs`/`.meta` moves unchanged from `Assets/Scripts/enemy/` into
`Assets/Level5/Core/Match/`, preserving GUID `bc63e88e998c49c9b04cf8a32daf111b` and compiling into
`Level5.Core` for the first time (`git diff -M --summary` reports `rename ... (97%)`, and the new
`EnemyPopulationRulesCompilesIntoLevel5Core` identity assertion confirms the assembly). Every
executable line is byte-identical; the only edit is two XML doc comments. `<see cref="EnemySpawner"/>`
became unresolvable once the file left `Assembly-CSharp` - `Level5.Core` cannot reference that
assembly - so both were rewritten as `<c>EnemySpawner</c>`, the form this same file already uses for
`<c>PlayerAttackQueue.GetMaxEnemiesQueued</c>` and `<c>MatchRuntime.HasConfiguration</c>`. Caught in
review, not by the compiler: Unity does not pass `/doc`, so no `CS1574` is emitted, and the
architecture guard strips comments before scanning, so a stale cross-assembly cref is invisible to
both. Separately, `PlayerAttackQueue` itself stays in `Assembly-CSharp` - it is not moved this
slice - but its last two `Assembly-CSharp` edges are cut: the direct `MatchRuntime.Rules` read (queue
capacity and the battle-royal shared-slot decision) and its own `GetComponent<PlayerIdentifier>()`
lookup (the participant anchor position) are both replaced by one explicit composition seam,
`PlayerAttackQueue.BindMatchContext(ResolvedMatchRules rules, Transform anchor)`, bound once by
`SpawnCoordinator` from both `RegisterHuman` and `RegisterCpu` (mirroring
`BindCallBallMatchRules`/`BindPlayerHealthMatchRules`). Queue policy is unchanged and still lives in
`PlayerAttackQueue` - composition supplies the rules and the anchor, it does not decide capacity,
sharing, or slot selection. An unbound queue (a composition defect, not a normal path) logs one
`Debug.LogError` in `Start()` and runs on `EnemyPopulationRules.MaxQueued(null)`'s standard capacity
with its own transform as the anchor, rather than reaching back into `MatchRuntime` or disabling
anything - the same fail-safe shape `CallBallToPlayer`/`PlayerHealth` already use.

**No serialized field changed.** `PlayerAttackQueue`'s `[SerializeField]` list, its public API,
reservation lifecycle, stale-entry cleanup, bodyguard registration, slot occupancy/sharing rules, slot
selection ordering, queue capacity policy, battle-royal behavior and attack-position offsets are all
unchanged; only the removed `playerIdentifier` field (and the now-empty `Awake()` that resolved it)
and the two new runtime-only fields (`matchRules`, `participantAnchor`) touch the class.
`PlayerAttackPosition` is untouched, and no queue/slot prefab or scene asset was opened or resaved.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors. Focused EditMode run:
**44/44 passed** across four fixtures - the existing `Level5AutonomousActorTests`
(`EnemyPopulationRules`' own capacity matrix, unchanged and still green after the move),
`Level5ProductionAssemblyBoundaryTests` (now including `EnemyPopulationRulesCompilesIntoLevel5Core`),
the new `Level5PlayerAttackQueueDependencyGuardTests` (source-guard: zero live
`MatchRuntime`/`PlayerIdentifier` references after comments/literals are stripped, the same shape as
`Level5CharacterProfileDependencyGuardTests`), and the new
`Level5PlayerAttackQueueMatchContextTests` (bind-once contract; queue capacity and the battle-royal
shared-slot decision both follow the bound rules over the unbound default; attack-slot positioning
follows the bound anchor rather than the queue's own transform; `SpawnCoordinator.RegisterHuman`/
`RegisterCpu` bind this match's rules and the participant root as anchor; a participant without a
`PlayerAttackQueue` is left untouched; an unbound queue reports the composition error exactly once and
runs on safe defaults). Per the risk-based validation policy, full EditMode/PlayMode were not re-run;
PR CI owns that broader regression coverage.

`validate-repository.ps1` **was** run for this slice and passed - `EnemyPopulationRules.cs`/`.meta`
moving together is exactly the class of change the script's `.meta`-pairing invariant guards.

Not moved in this slice: `PlayerAttackQueue` itself, `PlayerAttackPosition`, or any other Group B
blocker.

**Two pre-existing findings surfaced by this slice's review, deliberately not actioned here.** Both
predate the change and both would breach this slice's non-goals (no queue-policy change, no unrelated
cleanup); recorded for whoever moves the queue into `Level5.Player`:

1. *The `IsBattleRoyal` arm of `SelectAttackSlot`'s `allowSharedSlots` is unreachable in production.*
   `TryReserve` gates on `attackSlotOpen` (`currentEnemiesQueued < maxEnemiesQueued &&
   attackPositions.Length > 0`), so reaching `SelectAttackSlot` with every slot occupied requires
   `entries.Count >= attackPositions.Length` while `entries.Count < maxEnemiesQueued` - which already
   implies `maxEnemiesQueued > attackPositions.Length`, the first arm of the same `||`. The
   battle-royal term can therefore never be the deciding factor; the slice preserved it verbatim
   rather than "simplifying" a branch whose original intent is not recorded. The focused tests reach
   it only by forcing `maxEnemiesQueued` down through reflection, which is called out in the fixture.
2. *`Assets/Resources/Prefabs/auto_players/auto_player_drblood.prefab` is orphaned.* It is the only
   one of the 70 queue-carrying prefabs that has no `PlayerIdentifier`, and the only one whose
   behaviour this slice would change (its queue previously read live `MatchRuntime.Rules`; unbound, it
   would now log the composition error and use standard capacity). It is unreachable: its GUID
   `c0d61ad742506be41bd9ed1cbdc24788` is referenced by no scene, prefab or asset, no code loads
   `Prefabs/auto_players/`, and its root is `Untagged` so the scene auto-player lookup cannot find it.
   A content-cleanup question, not a code defect.

**Composition coverage, verified against the prefabs rather than inferred.** All 70 prefabs carrying
`PlayerAttackQueue` live in `Prefabs/characters/players` (47), `Prefabs/characters/cpu_players` (22)
and the orphan above; in every live one the queue sits on the same GameObject as `PlayerIdentifier`,
which is the participant root `SpawnCoordinator` instantiates and resolves the identifier from. Those
two folders are loaded only by `SpawnCoordinator` (`Constants.PREFAB_PATH_CHARACTER_*` has no other
consumer), and `SpawnPlayers()` runs inside `GameLevelManager.Awake()`, so every live participant is
bound before its `Start()`. The Lockdown defender prefab carries no queue and is silently skipped. A
consequence worth stating: because the queue is authored on the root, the bound anchor is always the
same `Transform` as the queue's own, so the anchor is a structural seam - it removes the queue's
dependency on `PlayerIdentifier` and on being authored at the root - not a change in where slots sit.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
`PlayerController.cs` was not edited, and `EnemyPopulationRules` was never one of its dependencies, so
its blocker set is unchanged by construction; re-derived anyway by the same scan as prior slices for
confirmation. Group A is unchanged at 17; Group B is **unchanged at 6** - `PlayerAttackQueue` stays in
group B because it stays in `Assembly-CSharp` itself, exactly as this slice's plan expected:

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (6, unchanged).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk`, `PlayerAttackQueue`,
  `PlayerDamageReactions` (all `Assets/Scripts/player/`)

**`PlayerAttackQueue`'s own dependency closure, freshly measured (2026-09-09).**
`PlayerAttackQueue.cs` stripped of comments and string/char literals, its remaining project-type
identifiers resolved to their nearest enclosing `.asmdef`: `ResolvedMatchRules` and
`EnemyPopulationRules` resolve to `Level5.Core` (the latter for the first time, this slice);
`ICombatAgent`, `ICombatReservationState`, `IDamageable` and `CombatReservation` resolve to
`Level5.Combat` (all already legal, unchanged by this slice); `PlayerAttackPosition` is the only
remaining project dependency still compiled into `Assembly-CSharp` - its same-assembly sibling,
explicitly out of scope for this slice. That makes `PlayerAttackQueue` the strongest next pure-move
candidate:

```text
PlayerAttackQueue
PlayerAttackPosition
        ↓
Level5.Player
```

**Slice 31 - `PlayerAttackQueue` and `PlayerAttackPosition` (2026-09-09): a pure assembly-ownership
move, dependency-closed since Slice 30.** Both files move from `Assets/Scripts/player/` into
`Assets/Scripts/player/Level5Player/`, compiling into `Level5.Player` for the first time. `.meta`
moved with each (`git mv`), preserving GUID `7121a35c5d4fa6e409483d2bb06cedd6`
(`PlayerAttackQueue`) and `ef24fa3df874e374fadd7438f107da25` (`PlayerAttackPosition`). Production
source is byte-identical - path only, confirmed by `git diff -M --summary` reporting both pairs as
`rename ... (100%)` with zero insertions or deletions. The two move together, not separately:
`PlayerAttackPosition.Initialize(PlayerAttackQueue owner, int slotId)` only compiles from the same
assembly as the queue, the same same-assembly-sibling shape Slice 28 hit with `CharacterProfile`'s
stat mapper. No dependency inversion was needed - Slice 30 already cut the queue's last two
`Assembly-CSharp` edges (`MatchRuntime`, `PlayerIdentifier`), and its remaining project-type
references (`ResolvedMatchRules`/`EnemyPopulationRules` → `Level5.Core`;
`ICombatAgent`/`ICombatReservationState`/`IDamageable`/`CombatReservation` → `Level5.Combat`) were
already legal. `Level5.Player.asmdef` is unchanged - still `Level5.Core`, `Level5.Combat`,
`Level5.Constants`; the pair needs only the two already present. `PlayerAttackPosition`'s unused
`owner` parameter on `Initialize` was left in place, as directed - not this slice's concern.

**Preflight re-verification, not re-audit.** The baseline SHA (`b4859fcb6`) matched current `dev`
exactly, so Slice 30's facts were spot-checked rather than re-derived: `PlayerAttackQueue.cs` still
has zero live `MatchRuntime`/`PlayerIdentifier` references; `PlayerAttackPosition` remains the
queue's only `Assembly-CSharp` project dependency and has none beyond the queue itself; both GUIDs
matched the audit exactly. One discrepancy did surface: the audit's "no direct `.unity` scene
serialization for either GUID" claim does not hold for `PlayerAttackPosition` -
`minigame_racing.unity` and `level_01_scrapyard_cpu_defense_test.unity` each carry several direct
`m_Script: {fileID: 11500000, guid: ef24fa3df874e374fadd7438f107da25, type: 3}` MonoBehaviour
components, not merely prefab instances. This does not block or complicate the move - Unity resolves
a `MonoScript` reference by GUID via the `.meta` file, independent of the script's assembly or
folder - and both scenes still resolve their `PlayerAttackPosition` components correctly after the
move (see validation below). Recorded here so the discrepancy isn't silently absorbed into "no scene
surface" for a future slice.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors (only pre-existing,
unrelated obsolete-API warnings). Focused EditMode run: **33/33 passed** across three fixtures -
`Level5ProductionAssemblyBoundaryTests` (21, including the new
`PlayerAttackQueueTypesCompileIntoLevel5Player` identity assertion and the still-green
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`), the relocated
`Level5PlayerAttackQueueDependencyGuardTests` (2 - now reading
`Assets/Scripts/player/Level5Player/PlayerAttackQueue.cs`; both permanent
no-`MatchRuntime`/no-`PlayerIdentifier` invariants still hold), and the unmodified
`Level5PlayerAttackQueueMatchContextTests` (10 - bind-once contract, queue capacity/battle-royal
sharing, participant-anchor positioning, human/CPU composition, and the unbound-safe-default path all
still green under the new assembly). Per the risk-based validation policy, full EditMode/PlayMode
were not re-run; PR CI owns that broader regression coverage.

**Representative serialized-asset validation, run rather than inferred.** A scratch
`-executeMethod` pass (not committed) confirmed, for one ordinary human participant
(`player_ashley.prefab`), one ordinary CPU participant with queue-plus-position children
(`cpu_player_ak47.prefab`), the required `Assets/Resources/Prefabs/critical/attackPositions.prefab`,
and the Slice-30-flagged legacy carrier `auto_player_drblood.prefab`: no Missing Script component on
any GameObject; `PlayerAttackQueue`/`PlayerAttackPosition` components resolve and report runtime
assembly identity `Level5.Player`; and `AssetDatabase.GUIDToAssetPath` maps both preserved GUIDs to
their moved script paths. No prefab or scene was opened or resaved.

`validate-repository.ps1` **was** run for this slice and passed - the same `.meta`-pairing rationale
as Slice 29: a source-plus-`.meta` move is exactly the change class that invariant guards.

Not moved in this slice: `PlayerController` or any other Group B blocker.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
`PlayerController.cs` stripped of comments and string/char literals, its remaining identifiers
matched against every top-level `public` type declared under `Assets/`, each hit resolved to its
nearest enclosing `.asmdef` - not subtracted mechanically from Slice 30's list. Group A is **18, up
from 17** - `PlayerAttackQueue` joins `Level5.Player` (`PlayerAttackPosition` is not itself a
`PlayerController` dependency):

**A. Already owned by custom assemblies - legal references, not blockers (18).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, **`PlayerAttackQueue`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (5, down from 6).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk`, `PlayerDamageReactions` (all
  `Assets/Scripts/player/`)

`PlayerAttackQueue` is the only entry that left group B; nothing else moved between groups, and no
new dependency appeared - `PlayerController.cs` itself was not edited. `PlayerController` remains in
`Assembly-CSharp` and stays blocked on all five group B entries.

**Slice 32 - `PlayerDamageReactions`'s remaining `Assembly-CSharp` dependencies removed; moved into
`Level5.Player`** (2026-09-09): the human damage/knockdown/lightning/shrink reaction helper,
dependency-closed by inverting both of its last two edges. `PlayerDamageReactions` used to hold a
`PlayerController controller` field and reach `CameraManager.instance.Cameras[0]` directly for the
shrink reaction's camera. Both are now indirections:

- a new narrow contract, `IPlayerDamageReactionHost`
  (`Assets/Scripts/player/Level5Player/IPlayerDamageReactionHost.cs`), replaces the concrete
  `PlayerController` field. `PlayerController` implements it - eight members (`Anim`, `RigidBody`,
  `CurrentState`, `TakeDamage`, `KnockedDown`, `Locked`, `AvoidedKnockDown`, `FacingRight`) map onto
  its existing public properties implicitly, with no new code; the other eight
  (`ActorTransform`; the four state hashes `TakeDamageStateHash`/`KnockedDownStateHash`/
  `DisintegratedStateHash`/`LightningStateHash`; `IsShrunk`; `MarkDead()`; `GetShrinkCamera()`) have
  no ordinary public equivalent and are implemented explicitly, off `PlayerController`'s ordinary
  public surface.
- the shrink camera lookup moves the other direction: `CameraManager` stays on the
  `Assembly-CSharp` side. `GameLevelManager.ReadPlayerDamageReactionCamera()` is the exact former
  null-safe `Cameras[0]` lookup, composed as a live `Func<Camera>` through
  `SpawnCoordinator.BindHumanDamageReactionCamera` -> `PlayerController.BindDamageReactionCameraReader`
  -> `IPlayerDamageReactionHost.GetShrinkCamera()`. Deliberately not folded into `PlayerController`
  itself - that would only swap one `Assembly-CSharp` blocker (`PlayerDamageReactions`) for another
  (`CameraManager`), leaving the net blocker count unchanged; `Level5PlayerControllerDependencyGuardTests.PlayerControllerHasNoCameraManagerReference`
  (new this slice) guards against that swap happening silently in the future.

`PlayerDamageReactions.cs`/`.meta` move from `Assets/Scripts/player/` into
`Assets/Scripts/player/Level5Player/`, compiling into `Level5.Player` for the first time. The `.meta`
moved with the file (not regenerated), preserving GUID `15316dc803bcd77458d8cedae6d682ad`. Unlike
Slice 31's byte-identical move, this file's body changed - every `controller.X` read/write becomes
`host.X` and the constructor parameter is now `IPlayerDamageReactionHost` rather than the concrete
class - so git recorded the move as a delete-plus-add rather than a detected rename; the GUID is what
Unity actually resolves the script by, and it is unchanged. No reaction *logic* changed - the
freeze/animate/wait/restore shape of every coroutine, the shrink scale/FOV math, and the AUD-054
exact-FOV-restore fix are all preserved, only how each line reaches its state.

`Level5.Player.asmdef` is unchanged - still `Level5.Core`, `Level5.Combat`, `Level5.Constants`;
neither new type needs any of them (both compile against only `System`/`System.Collections`/
`UnityEngine`).

**Preflight re-verification.** Baseline SHA (`3137c93d7`) matched current `dev` exactly.
`PlayerDamageReactions` still held exactly the two documented edges (`PlayerController`,
`CameraManager`) plus ordinary `System`/`UnityEngine` types; `PlayerController`'s constructor
(`damageReactions = new PlayerDamageReactions(this);`) remained its only production construction
site; the shrink reaction still resolved `CameraManager.instance.Cameras[0]`; and the helper's GUID
and its lack of any authored/reflection/assembly-qualified dependency were unchanged from the audited
baseline.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors (only pre-existing,
unrelated obsolete-API warnings). Full EditMode run (broader than the routine focused subset, run here
because this slice adds three new fixtures whose Animator/coroutine-stepping assumptions were worth
confirming against the whole suite rather than in isolation): **1145/1145 passed**, including
`Level5ProductionAssemblyBoundaryTests.PlayerDamageReactionTypesCompileIntoLevel5Player` (new) and the
still-green `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`;
`Level5PlayerControllerDependencyGuardTests.PlayerControllerHasNoCameraManagerReference` (new,
alongside the still-green `PlayerControllerHasNoGameLevelManagerReference`); and three new fixtures -
`Level5PlayerDamageReactionHostTests` (7: interface implementation, representative read/write
round-trips, `MarkDead` updating the existing `PlayerHealth.IsDead` rather than duplicate death state,
and the shrink-camera resolver's null-safe/live-not-snapshotted behavior),
`Level5PlayerDamageReactionCameraCompositionTests` (5: human participants receive the resolver, CPU
participants are skipped, the resolver stays live rather than snapshotted, an unbound/null-returning
resolver is safe, and the existing missing-`PlayerController` fail-closed-and-continue convention
holds), and `Level5PlayerDamageReactionsTests` (3: `PlayerDamageReactions` driven directly against a
fake host by manually stepping its coroutines via `IEnumerator.MoveNext()` - no `PlayerController`,
scene, or Play Mode required - covering one representative ordinary reaction, `PlayerKnockedDown`, and
the changed camera seam, `ShrinkPlayer`, including its null-camera path). `validate-repository.ps1`
**was** run and passed - the same `.meta`-pairing rationale as Slices 29/31, since this slice's own
move is exactly that change class, now also covering the two new production `.meta` files and the
three new test-file `.meta`s.

Not moved in this slice: `PlayerController` itself, `CameraManager`, or any other Group B blocker.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
`PlayerController.cs` stripped of comments and string/char literals, its remaining identifiers matched
against every top-level `public` type declared under `Assets/`, each hit resolved to its nearest
enclosing `.asmdef`. Group A is **20, up from 18** - `PlayerDamageReactions` and its new
`IPlayerDamageReactionHost` contract both join `Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (20).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, `PlayerAttackQueue`, **`PlayerDamageReactions`, `IPlayerDamageReactionHost`**
  (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (4, down from 5).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **sniper/projectile:** `SniperManager` (`Assets/Scripts/projectile/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk` (`Assets/Scripts/player/`)

`PlayerDamageReactions` is the only entry that left group B; `CameraManager` never entered group B at
all - the blocker-swap this slice's plan explicitly warned against did not happen, confirmed by
`PlayerControllerHasNoCameraManagerReference`. `PlayerController` remains in `Assembly-CSharp` and
stays blocked on all four remaining group B entries.

**Slice 33 - `PlayerController`'s `SniperManager` dependency removed; dependency-cut only, no move**
(2026-09-09): the idle-sniper check's last direct edge into `SniperManager` (still `Assembly-CSharp`)
is cut, mirroring Slice 21/26/32's dependency-cut shape - `SniperManager` itself is untouched and
stays exactly where it is.

- A new narrow contract, `IPlayerIdleSniperRuntime`
  (`Assets/Scripts/player/Level5Player/IPlayerIdleSniperRuntime.cs`), names only the two things
  `checkIdleTimeForSniper()` reads/writes on the runtime: `Locked` and
  `GetInstantKillRoutine(float)`. `SniperManager` implements it explicitly (`locked`/
  `StartSniperBulletInstantKill` are unchanged and still the ordinary public surface every other
  caller - `startSniper`, `InstantiateConfiguredProjectile` - uses).
- `PlayerController` no longer reads `SniperManager.instance`. It stores a
  `Func<IPlayerIdleSniperRuntime>` (`BindIdleSniperRuntimeReader`) and resolves it live, at the point
  of use inside `checkIdleTimeForSniper()`, exactly where the old static read was - not cached at
  bind time, not resolved anywhere else.
- The concrete lookup moves to the composition side:
  `GameLevelManager.ReadPlayerIdleSniperRuntime()` (re-read on every call, deliberately not captured;
  null-checked against the concrete `SniperManager` type before the implicit conversion to
  `IPlayerIdleSniperRuntime` - code review, 2026-09-10: a bare `return SniperManager.instance;` would
  let a destroyed-but-not-yet-nulled manager cross the interface boundary as non-null, since
  `UnityEngine.Object`'s overloaded `==` is not selected once the static type is the interface, mirroring
  the explicit `CameraManager.instance == null` guard `ReadPlayerDamageReactionCamera` already uses two
  methods above) -> `SpawnCoordinator.BindHumanIdleSniperRuntime` (new, reusing the
  existing `BindEveryHumanController` human-only iteration, mirroring
  `BindHumanDamageReactionCamera`) -> `PlayerController.BindIdleSniperRuntimeReader`. Bound from
  `GameLevelManager.Awake`'s spawn pass, adjacent to `BindHumanLegacyTouchMovement`/
  `BindHumanDamageReactionCamera`. No sniper runtime needs to exist at binding time.

**Ownership unchanged.** `PlayerController` still owns: the `MatchRuntime.Rules.SniperEnabled` check,
idle timing (including the actual idle condition this codebase uses -
`movementHorizontal == 0 && movementVertical == 0 && Grounded`, not an animator-state check), the
150-second threshold, random-delay generation via `UtilityFunctions.GetRandomFloat`, the lock
transition (`sniper.Locked = true` before the routine is requested), and `StartCoroutine` - it remains
the coroutine host. `checkIdleTimeForSniper()`'s branch structure and ordering are byte-for-byte the
same as before, with `SniperManager.instance`/`.locked`/`.StartSniperBulletInstantKill(...)` replaced
by `sniper`/`sniper.Locked`/`sniper.GetInstantKillRoutine(...)`. Missing-vs-locked semantics are
preserved exactly: no bound reader, or a reader answering `null`, resets idle timing precisely like
the old missing-`SniperManager.instance` path; a locked runtime is read (idle time still accumulates
from `Time.time - idleStartTime`) but never triggers a reset or a routine request merely for being
locked.

**Preflight re-verification.** Baseline SHA (`282c01ff2`) matched current `dev` exactly.
`PlayerController`'s only `SniperManager` reference was still the single `checkIdleTimeForSniper()`
site; `StartSniperBulletInstantKill(float)` had no production caller besides it; `SniperManager` still
exposed public `locked` and `StartSniperBulletInstantKill(float)` unchanged; `SpawnCoordinator` still
had the shared `BindEveryHumanController` human-binding path; `GameLevelManager.Awake` still performed
human runtime bindings (`BindHumanLegacyTouchMovement`, `BindHumanDamageReactionCamera`) after
participant spawning; and `Level5.Player.asmdef` still referenced only `Level5.Core`, `Level5.Combat`,
`Level5.Constants` - a neutral `bool`/`float`/`IEnumerator` interface needed none of them.

**No serialized field changed.** `SniperManager`'s serialized fields, singleton lifecycle, projectile
prefabs/configuration and every coroutine's body are unchanged; only the new explicit interface
implementation was added. No prefab or scene asset was opened or resaved.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors. Focused EditMode run
covering this slice's own new/changed surface: `Level5ProductionAssemblyBoundaryTests`
(`PlayerIdleSniperRuntimeCompilesIntoLevel5Player`, new) and the still-green
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`; the extended
`Level5PlayerControllerDependencyGuardTests` (`PlayerControllerHasNoSniperManagerReference`, new,
alongside the still-green `PlayerControllerHasNoGameLevelManagerReference`/
`PlayerControllerHasNoCameraManagerReference`); the new `Level5PlayerIdleSniperRuntimeTests` (interface
implementation, `Locked` read/write round-tripping the existing public `locked` field,
`GetInstantKillRoutine` delegating to `StartSniperBulletInstantKill` by iterator-type identity rather
than executing the projectile flow); the new `Level5PlayerIdleSniperRuntimeCompositionTests` (human
participants receive the resolver, CPU participants are skipped, the resolver stays live rather than
snapshotted, an unbound resolver leaves the reader `null`, and the existing missing-`PlayerController`
fail-closed-and-continue convention holds); and the new `Level5PlayerControllerIdleSniperTests`
(missing/null-reader and sniper-disabled paths all reset idle timing and request no routine; a locked
runtime neither resets accumulated idle time nor requests a routine; an eligible unlocked runtime locks
before requesting the routine, resets idle timing, and requests exactly one routine) - driven against a
fake `IPlayerIdleSniperRuntime` with `idleStartTime` moved into the past rather than 150 real seconds
of elapsed test time, and an `ActiveMatch`-backed `ResolvedMatchRules(sniper: SniperMode.Bullet)` to
control `MatchRuntime.Rules.SniperEnabled` without touching legacy `GameOptions` globals. Per the
risk-based validation policy, full EditMode/PlayMode were not re-run; PR CI owns that broader
regression coverage. `validate-repository.ps1` was run and passed.

Not moved in this slice: `SniperManager` itself, `PlayerController`, or any other Group B blocker.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-09).**
`PlayerController.cs` stripped of comments and string/char literals, its remaining identifiers matched
against every top-level `public` type declared under `Assets/`, each hit resolved to its nearest
enclosing `.asmdef`. Group A is **21, up from 20** - `IPlayerIdleSniperRuntime` joins `Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (21).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, `PlayerAttackQueue`, `PlayerDamageReactions`,
  `IPlayerDamageReactionHost`, **`IPlayerIdleSniperRuntime`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (3, down from 4).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **player-local components:** `PlayerIdentifier`, `PlayerDunk` (`Assets/Scripts/player/`)

`SniperManager` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared - the composition-side lookup lives in `GameLevelManager`/`SpawnCoordinator`, not
on `PlayerController`. `PlayerController` remains in `Assembly-CSharp` and stays blocked on all three
remaining group B entries.

**Slice 34 - `PlayerController`'s `PlayerIdentifier` dependency removed; dependency-cut only, no move**
(2026-09-10): `PlayerController`'s last two direct `PlayerIdentifier` reads - `InitializeInput()`'s
`pid`/`isCpu`, and `Start()`'s human `basketball` resolution - are cut, mirroring Slices 21/26/32/33's
dependency-cut shape. `PlayerIdentifier` itself is untouched, stays in `Assembly-CSharp`, and remains
the authoritative owner of participant id, CPU status, and the human basketball association.

- A new narrow contract, `IPlayerControllerParticipantState`
  (`Assets/Scripts/player/Level5Player/IPlayerControllerParticipantState.cs`), names only the three
  things `PlayerController` reads off the identifier: `PlayerId`, `IsCpu`, and `BasketballObject`.
  `PlayerIdentifier` implements it explicitly, mapping straight onto its existing `pid`/`isCpu`/
  `basketball` fields - `BasketballObject` returns `basketball`, never `autoBasketball`, matching what
  `PlayerController.Start()` always consumed.
- `PlayerController` no longer calls `GetComponent<PlayerIdentifier>()`. Both call sites now resolve
  `GetComponent<IPlayerControllerParticipantState>()` at the same points, live, with no caching -
  `InitializeInput()`'s missing-provider (`playerId` defaults to 0), CPU-guard (log, disable, return),
  and human (`MatchRuntime.LocalInputSlotFor(playerId)`) branches are otherwise byte-for-byte unchanged,
  and `Start()`'s basketball resolution keeps the same timing and the same requirement that a normal
  composed human already has a `PlayerIdentifier` with `basketball` set before `PlayerController.Start()`
  runs.
- No composition change: `SpawnCoordinator` already populates `PlayerIdentifier` before this point in
  the spawn sequence, so the interface reads that existing state directly - no new binding pass, no
  copied `pid`/`isCpu`/basketball field on `PlayerController`, no `SpawnCoordinator` edit.

**Ownership unchanged.** `PlayerIdentifier` remains the sole owner of `pid`, `isCpu`, and the human
`basketball` reference. `IPlayerControllerParticipantState` is a read-only live view over that state,
not a second owner - the interface's three members simply return the identifier's existing fields on
every call.

**Preflight re-verification.** Baseline SHA (`e32c05f0f`) matched current `dev` exactly. `PlayerController`
still had exactly the two documented `PlayerIdentifier` reads; `PlayerIdentifier` still owned `pid`,
`isCpu`, and human `basketball` with no existing neutral interface already exposing them; normal
production human controllers still receive their identifier and basketball association through
`SpawnCoordinator` before `PlayerController.Start()` runs; `PlayerController` remains prefab-authored,
not scene-serialized; and a contract of only `int`/`bool`/`GameObject` needed no new
`Level5.Player.asmdef` reference.

**No serialized field changed.** `PlayerIdentifier`'s serialized fields, public methods, `Actor`, and
`IBasketballParticipantStateProvider` implementation are unchanged; only the new explicit interface
implementation was added. No prefab or scene asset was opened or resaved; `SpawnCoordinator` and
`PlayerRegistry` were not touched.

**Validation.** Headless Unity `6000.5.7f1` batch compile: zero new `CS` errors. Focused EditMode run
covering this slice's own new/changed surface: `Level5ProductionAssemblyBoundaryTests`
(`PlayerControllerParticipantStateCompilesIntoLevel5Player`, new) and the still-green
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`; the extended
`Level5PlayerControllerDependencyGuardTests` (`PlayerControllerHasNoPlayerIdentifierReference`, new,
alongside the still-green `PlayerControllerHasNoGameLevelManagerReference`/
`PlayerControllerHasNoCameraManagerReference`/`PlayerControllerHasNoSniperManagerReference`); the new
`Level5PlayerControllerParticipantStateTests` (interface implementation; `PlayerId`/`IsCpu`/
`BasketballObject` round-tripping the existing public `pid`/`isCpu`/`basketball` fields;
`BasketballObject` proven distinct from `autoBasketball`; a live-read check proving the interface is not
snapshotted; and a real `InitializeInput()` invocation proving the CPU guard still fires through the
interface path); and the existing `Level5PlayerInputReaderCompositionTests`, rerun unchanged and still
green, since they already drive `InitializeInput()`/`TryEnsureInputReader()` through real
`PlayerIdentifier`-backed participants. Per the risk-based validation policy, full EditMode/PlayMode
were not re-run; PR CI owns that broader regression coverage. `validate-repository.ps1` was run and
passed.

Not moved in this slice: `PlayerIdentifier` itself, `MatchRuntime`, `PlayerDunk`, or any other Group B
blocker.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-10).**
`PlayerController.cs` stripped of comments and string/char literals, its remaining identifiers matched
against every top-level `public` type declared under `Assets/`, each hit resolved to its nearest
enclosing `.asmdef`. Group A is **22, up from 21** - `IPlayerControllerParticipantState` joins
`Level5.Player`:

**A. Already owned by custom assemblies - legal references, not blockers (22).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, `PlayerAttackQueue`, `PlayerDamageReactions`,
  `IPlayerDamageReactionHost`, `IPlayerIdleSniperRuntime`, **`IPlayerControllerParticipantState`** (this
  slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (2, down from 3).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **player-local components:** `PlayerDunk` (`Assets/Scripts/player/`)

`PlayerIdentifier` is the only entry that left group B; nothing else moved between groups, and no new
dependency appeared. `PlayerController` remains in `Assembly-CSharp` and stays blocked on the two
remaining group B entries, `MatchRuntime` and `PlayerDunk`.

**Slice 35 - `PlayerDunk`'s `PlayerController`, `PlayerIdentifier` and `GameLevelManager` dependencies
removed; dependency-preparation only, no move** (2026-09-10): `PlayerDunk` stays in `Assembly-CSharp`
this slice - only its three remaining `Assembly-CSharp` edges are cut, mirroring Slice 30's
`PlayerAttackQueue` shape (dependency-cut now, pure ownership move later).

- A new narrow contract, `IPlayerDunkHost` (`Assets/Scripts/player/Level5Player/IPlayerDunkHost.cs`),
  names only what `PlayerDunk` reads and writes on the controller: `RigidBody`, `BasketballRimVector`,
  `CurrentState`, `DunkStateHash`, `Locked`, `HasBasketball`, and six action forwards
  (`SetCallBallLocked`, `FaceBasketballGoal`, `PlayAnimation`, `SetAnimationBool`, `FreezePosition`,
  `UnfreezePosition`). `PlayerController` implements it - `RigidBody`/`CurrentState`/`Locked` are
  satisfied implicitly by the existing public properties of the same shape; the rest are explicit
  implementations forwarding to `bballRimVector`/`dunkState`/`hasBasketball` and the existing
  `CheckIsPlayerFacingGoal()`/`PlayAnim()`/`SetPlayerAnim()`/`FreezePlayerPosition()`/
  `UnFreezePlayerPosition()`/`callBallToPlayer.Locked` - the same explicit-implementation shape Slice 32
  established for `IPlayerDamageReactionHost`.
- `PlayerDunk` no longer resolves `GetComponent<PlayerController>()` (via the former cached
  `PlayerIdentifier.playerController`), `GetComponent<PlayerIdentifier>()`, or
  `GameLevelManager.instance.BasketballRimVector`. `Start()` now resolves
  `GetComponent<IPlayerDunkHost>()` and `GetComponent<IPlayerControllerParticipantState>()` (the same
  contract Slice 34 added) on the same participant GameObject, and reaches the human basketball through
  `participant.BasketballObject.GetComponent<BasketBall>()`/`<BasketBallState>()` - the identical
  resolution `PlayerController.Start()` itself already uses. `playerDunk()`'s rim-relative left/right
  decision reads `playerHost.BasketballRimVector` in place of the direct `GameLevelManager` read;
  `Launch()` and `TriggerDunkSequence()` read/write the same host members in place of the former
  `playerController.*` calls.
- `PlayerController.PlayerDunk` and `PlayerCollisions`' `TriggerDunkSequence()` call are untouched -
  `PlayerDunk` keeps its class name, namespace, file path, GUID (`f30bfacdf55aac546906c63b39b58411`),
  serialized fields, and public API (`playerDunk()`, `TriggerDunkSequence()`, `DunkRangeFeet`,
  `PlayerCanDunk`) exactly as before.

**Rim-source equivalence.** `PlayerController.bballRimVector` is set once, from `BindArenaContext`,
called only from `SpawnCoordinator.BindHumanArenaContext` -> `GameLevelManager.Start()` after arena
bootstrap resolves the final rim (Slice 21). `GameLevelManager._basketballRimVector` itself is written
once, at `ArenaBootstrap.FindRimVector()`, before that same `BindHumanArenaContext` call - no other
production write site exists. The two values are therefore the same finalized snapshot for the whole
match, not a live-vs-cached divergence; reading `playerHost.BasketballRimVector` instead of
`GameLevelManager.instance.BasketballRimVector` changes nothing observable.

**Ownership unchanged.** `PlayerController` remains the sole owner of `bballRimVector`, `dunkState`,
`hasBasketball`, `currentState`/`CurrentState`, and `Locked`; `IPlayerDunkHost` is a live view over that
state, not a second owner. `PlayerIdentifier` remains the sole owner of the human basketball association,
reached the same way Slice 34 already established. `PlayerDunk` still owns every dunk-decision algorithm
(marker lookup and the missing-marker `PlayerCanDunk` gate, jump angle/dunk range defaults, the left/right
decision, the ballistic velocity equation and its launch gate, `TriggerDunkSequence`'s freeze/animation/
wait/ball-reset ordering) - none of that changed, only how it reaches controller/identifier state.

**Preflight re-verification.** Baseline SHA (`c9517eaf9`) matched current `dev` exactly. `PlayerDunk`
still had exactly the three documented `Assembly-CSharp` edges (`PlayerController` via
`PlayerIdentifier.playerController`, `PlayerIdentifier` itself, `GameLevelManager`); its other project
dependencies (`BasketBall`/`BasketBallState` -> `Level5.Basketball`, `SceneObjects` -> `Level5.Utility`)
were already legal; `IPlayerControllerParticipantState.BasketballObject` still exposed the human
`basketball` field; `PlayerController` still received the finalized arena rim through `BindArenaContext`
with no later production write to a different `GameLevelManager.BasketballRimVector`;
`PlayerController.PlayerDunk` and `PlayerCollisions`' `TriggerDunkSequence()` call were unchanged; the
`PlayerDunk` GUID matched; and no existing interface (`IShooterActor`, `IPlayerDamageReactionHost`,
`IPlayerControllerParticipantState`, `IPlayerIdleSniperRuntime`) already expressed the exact controller
capabilities `PlayerDunk` needed.

**No serialized field changed.** `PlayerDunk`'s `[SerializeField]` list, GUID, public API, and every
dunk algorithm/animation name/ordering are unchanged; only the `playerController`/(implicit
`PlayerIdentifier`) field became `playerHost` (`IPlayerDunkHost`), and the `Start()` basketball
resolution now goes through `IPlayerControllerParticipantState` instead of `PlayerIdentifier` directly.
No prefab or scene asset was opened or resaved.

**Validation.** Headless Unity `6000.5.7f1` batch compile was not available in this environment (no
valid Editor license) - not run; final diff/`.meta` inspection is the load-bearing evidence for this
change, per the risk-based validation policy's environment-failure guidance. Focused EditMode coverage
added, not yet executed against a licensed Editor for the same reason: `Level5ProductionAssemblyBoundaryTests`
(`PlayerDunkHostCompilesIntoLevel5Player`, new, alongside the still-green
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp`); the new
`Level5PlayerDunkDependencyGuardTests` (source-guard: zero live `PlayerController`/`PlayerIdentifier`/
`GameLevelManager` references after comments/literals are stripped, the same shape as
`Level5PlayerAttackQueueDependencyGuardTests`); the new `Level5PlayerDunkHostTests` (`PlayerController`
implements `IPlayerDunkHost`; `RigidBody`/`BasketballRimVector`/`CurrentState`/`DunkStateHash`/`Locked`/
`HasBasketball` round-trip the same public state every other caller uses; `SetCallBallLocked` forwards to
the existing `CallBallToPlayer` component; `FreezePosition`/`UnfreezePosition` forward to the existing
`RigidbodyFreezeHelper` calls as the one representative action-forwarding path); and the new
`Level5PlayerDunkSeamTests` (`Start()` resolves `IPlayerDunkHost`/`IPlayerControllerParticipantState`
rather than the concrete types, against a single fake host component; the same `BasketBall`/
`BasketBallState` the participant exposes are the ones `PlayerDunk` resolves; the rim-relative left/right
decision reads the host's `BasketballRimVector` - proven by driving `playerDunk()` with the rim on each
side and asserting the resulting launch velocity's sign; `Launch()`'s host locking/`RigidBody`/
`inair_dunk` animation calls; and `TriggerDunkSequence()`'s freeze/`dunk`-animation/state-wait/ball-reset/
`hasBasketball`-reset sequence, drained step-by-step the same way `Level5PlayerDamageReactionsTests`
already does). Per the risk-based validation policy, full EditMode/PlayMode were not re-run; PR CI owns
that broader regression coverage. `validate-repository.ps1` **was** run and passed - it caught three new
test files initially missing their `.meta` before they were added, exactly the class of defect that
invariant guards.

Not moved in this slice: `PlayerDunk` itself, `PlayerController`, `PlayerIdentifier`, `GameLevelManager`,
or `MatchRuntime`.

**`PlayerDunk`'s own dependency closure, freshly measured (2026-09-10).** `PlayerDunk.cs` stripped of
comments and string/char literals, its remaining project-type identifiers resolved to their nearest
enclosing `.asmdef`: `IPlayerDunkHost` and `IPlayerControllerParticipantState` resolve to
`Level5.Player` (both for the first time on this file, this slice); `BasketBall`/`BasketBallState`
resolve to `Level5.Basketball`; `SceneObjects` resolves to `Level5.Utility` - all three already legal,
unchanged by this slice. That leaves zero remaining `Assembly-CSharp` dependencies:

```text
PlayerDunk Assembly-CSharp dependencies: 3 -> 0
```

`PlayerDunk` is therefore dependency-closed - the next candidate for a pure ownership move into
`Level5.Player`, the same shape Slice 31 was for `PlayerAttackQueue` after Slice 30. Not implemented
this slice:

```text
PlayerDunk
        ↓
Level5.Player
```

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-10).**
`PlayerController.cs` was not edited beyond adding the `IPlayerDunkHost` implementation, and
`IPlayerDunkHost` compiles into `Level5.Player` - already a legal reference class, and not a new type
`PlayerController` itself names (it satisfies the interface, it does not reference the interface type
by name anywhere in its own body). Group A and Group B are therefore unchanged by construction;
re-verified anyway by the same scan as prior slices:

```text
PlayerController blockers: 2 -> 2
```

**B. Still compiled into `Assembly-CSharp` - the actual remaining blockers (2, unchanged).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)
- **player-local components:** `PlayerDunk` (`Assets/Scripts/player/`)

`PlayerDunk` stays in group B because it stays in `Assembly-CSharp` itself this slice, exactly as
expected - becoming dependency-closed does not change which assembly it currently compiles into.
`PlayerController` remains in `Assembly-CSharp` and stays blocked on the same two group B entries,
`MatchRuntime` and `PlayerDunk`.

**Slice 36 - `PlayerDunk` moved into `Level5.Player`; pure ownership move, no behavior change**
(2026-09-10): Slice 35 already cut `PlayerDunk`'s three `Assembly-CSharp` edges and left it
dependency-closed; this slice is the ownership move that follows, the same shape Slice 31 was for
`PlayerAttackQueue` after Slice 30.

**Baseline certification (hard gate, run before moving anything).** Untouched current `dev` at
`7cbad9bdb` (Slice 35, PR #128) - the first opportunity to run this Slice's Unity compile and focused
tests against a licensed Editor, since Slice 35 itself could not - was exercised with a headless
Unity `6000.5.7f1` EditMode batch run filtered to `Level5PlayerDunkDependencyGuardTests` (3),
`Level5PlayerDunkHostTests` (9), `Level5PlayerDunkSeamTests` (5), and
`Level5ProductionAssemblyBoundaryTests` (25): 42/42 passed, compilation succeeded. The migration
proceeded only after this passed.

- `Assets/Scripts/player/PlayerDunk.cs`/`.cs.meta` moved to
  `Assets/Scripts/player/Level5Player/PlayerDunk.cs`/`.cs.meta` via `git mv` - path only, content
  byte-identical (confirmed by an empty `git diff` across the rename), GUID
  (`f30bfacdf55aac546906c63b39b58411`) preserved unchanged.
- `Level5.Player.asmdef` gained exactly two direct references, `Level5.Basketball` and
  `Level5.Utility` - the two project assemblies `PlayerDunk` itself needs
  (`BasketBall`/`BasketBallState` and `SceneObjects`). Final reference list: `Level5.Core`,
  `Level5.Combat`, `Level5.Constants`, `Level5.Basketball`, `Level5.Utility`. Freshly re-checked
  before and after: neither `Level5.Basketball` nor `Level5.Utility` references `Level5.Player` -
  acyclic.
- `Level5PlayerDunkDependencyGuardTests`'s source path updated to the new location; its permanent
  invariant (zero live `PlayerController`/`PlayerIdentifier`/`GameLevelManager` references) is
  unchanged and still enforced against the moved file.
- `Level5ProductionAssemblyBoundaryTests` gained `PlayerDunkCompilesIntoLevel5Player`, the same
  identity-check shape as `PlayerDunkHostCompilesIntoLevel5Player`, proving the moved type actually
  compiles into `Level5.Player` rather than falling back to `Assembly-CSharp`.
- `PlayerController.PlayerDunk`, its `GetComponent<PlayerDunk>()`/`PlayerCanDunk`/`DunkRangeFeet`/
  `playerDunk()` usages, and `PlayerCollisions`' `PlayerController1.PlayerDunk.TriggerDunkSequence()`
  call are untouched source - all reach the moved type through the auto-referenced `Level5.Player`
  assembly exactly as before.

**Serialized identity.** No prefab or scene asset was opened or resaved. 71 prefabs and one direct
scene reference (`Assets/Scenes/level_01_scrapyard_cpu_defense_test.unity`, a disabled legacy
`cpu_player_defense_oldreal.1` GameObject, tag `autoPlayer` - one more than Slice 35's "0 direct
`.unity` references" audit line counted, evidently missed by that earlier pass rather than new drift)
carry the `PlayerDunk` GUID; since the GUID and every serialized field are unchanged, Unity's
GUID-keyed script resolution is unaffected by the file's new path regardless of which carrier is
checked. Representative carriers spot-checked by direct YAML inspection (`m_Enabled`, then
`dunkPositionLeft`/`dunkPositionRight`/`dunkRangeFeet`/`jumpAngle`/`playerCanDunk`, all default
`{0,0,0}`/`{0,0,0}`/`15`/`45` since these repopulate from scene markers in `Start()`):
`player_ak47.prefab` (ordinary human carrier, `m_Enabled: 1`, `playerCanDunk: 0`),
`cpu_player_ak47.prefab` (ordinary CPU carrier, `m_Enabled: 0`, `playerCanDunk: 0`),
`auto_player_drblood.prefab` (legacy auto-player, `m_Enabled: 0`, `playerCanDunk: 1`), and the scene's
`cpu_player_defense_oldreal.1` (legacy, `m_Enabled: 0`) - all four match the previously-authored
enabled/disabled pattern exactly, none show `Missing Script`.

**Validation.** Headless Unity `6000.5.7f1` EditMode batch run, post-move, filtered to the same four
fixtures plus the new identity test: 43/43 passed (the 42 baseline cases plus
`PlayerDunkCompilesIntoLevel5Player`), compilation succeeded with the new asmdef references in place -
this doubles as the acyclicity proof, since a reference cycle would have failed compilation outright.
Per the risk-based validation policy, full EditMode/PlayMode were not re-run; PR CI owns that broader
regression coverage. Two incidental `Level5.slnx` project-order reorderings written by the Unity runs
were reverted before commit as unrelated churn.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-10).** Same scan
as prior slices; `PlayerDunk` moves from group B to group A:

```text
PlayerController blockers: 2 -> 1
```

**A. Already owned by custom assemblies - legal references, not blockers (23, up from 22).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, `PlayerAttackQueue`, `PlayerDamageReactions`,
  `IPlayerDamageReactionHost`, `IPlayerIdleSniperRuntime`, `IPlayerControllerParticipantState`,
  **`PlayerDunk`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blocker (1, down from 2).**

- **match/session runtime:** `MatchRuntime` (`Assets/Scripts/game manager/`)

`PlayerController` remains in `Assembly-CSharp`, now blocked on only `MatchRuntime`. Not moved this
slice: `PlayerController`, `PlayerIdentifier`, `GameLevelManager`, or `MatchRuntime` itself.

**Slice 37 - `PlayerController`'s `MatchRuntime` dependency removed; dependency-preparation only, no
move** (2026-09-10): `PlayerController` stays in `Assembly-CSharp` this slice - only its last
`Assembly-CSharp` edge, `MatchRuntime`, is cut, mirroring Slice 35's `PlayerDunk` shape
(dependency-cut now, pure ownership move later, held open deliberately since `PlayerController`'s
48-prefab serialized surface needs its own migration audit).

- A new narrow contract, `IPlayerMatchRuntime` (`Assets/Scripts/player/Level5Player/IPlayerMatchRuntime.cs`),
  names only what `PlayerController` reads: `Rules`, `CustomCamera`, and `LocalInputSlotFor(int)`.
  `GameLevelManager` implements it explicitly, forwarding every member straight to the static
  `MatchRuntime` on each call (`ResolvedMatchRules IPlayerMatchRuntime.Rules => MatchRuntime.Rules;`
  and so on) - it does not answer from its own `_rules`/`_roster` snapshot fields, which serve a
  different point-of-use (this class's own facade) than `PlayerController`'s former direct reads
  needed (a directly entered/unconfigured scene's legacy-global fallback, re-evaluated live on every
  call). `MatchRuntime` stays the only owner of that compatibility translation; the interface is only a
  boundary, not a second match-state owner.
- `SpawnCoordinator.BindHumanMatchRuntime(IPlayerMatchRuntime)` forwards the scene's provider to every
  registered human's `PlayerController.BindMatchRuntime`, reusing the existing `BindEveryHumanController`
  human-only iteration every other `BindHuman*` pass already shares - no new iteration, no CPU exposure.
  Called from `GameLevelManager.Awake`'s spawn pass, adjacent to `BindHumanLegacyTouchMovement`/
  `BindHumanDamageReactionCamera`/`BindHumanIdleSniperRuntime`.
- `PlayerController` stores the bound provider in one field (`matchRuntime`), read live at every former
  `MatchRuntime.Rules`/`CustomCamera`/`LocalInputSlotFor` call site - `Start()`'s custom-camera/
  combat-setup/sniper-knockdown reads, `Update()`'s touch-jump/jump/shoot/attack/block/special gating,
  `checkIdleTimeForSniper()`, `PlayerJump()`, and `Flip()` - unconditionally rewritten expression-for-
  expression, no predicate/ordering changes. No `ResolvedMatchRules` snapshot field was introduced; the
  forbidden `private ResolvedMatchRules matchRules;` shape the plan called out explicitly does not exist
  anywhere in this file.
- `InitializeInput()` gained one guard, after the pre-existing CPU-misuse check and before resolving a
  local input slot: a human participant with no bound `matchRuntime` logs an actionable composition
  error, disables the controller, and returns - without falling back to the static `MatchRuntime`. The
  CPU guard still runs first and still fires on its own, unaffected by the new requirement (a CPU never
  receives this binding, but never needs to reach it either). `Start()` gained one narrow check
  immediately after calling `InitializeInput()` (`if (matchRuntimeRequiredButMissing) return;`),
  reading a field only the new guard sets - since disabling a `MonoBehaviour` inside `InitializeInput()`
  does not itself abort the rest of `Start()`, which would otherwise dereference the still-null
  `matchRuntime` a few lines later. Every other `InitializeInput()` exit (CPU misuse, invalid input
  slot) leaves that field `false` and `Start()`'s existing continuation behaviour for those paths is
  unchanged.

**Preflight re-verification.** Baseline SHA (`7b26db52d`) matched current `dev` exactly. `MatchRuntime`
was still `PlayerController`'s only live `Assembly-CSharp` dependency, and its three uses were still
exactly `Rules`, `CustomCamera`, and `LocalInputSlotFor(playerId)`. `MatchRuntime.Rules` still resolved
a validated configuration when present and reconstructed rules from the legacy globals otherwise;
`MatchRuntime.LocalInputSlotFor` still owned the roster/legacy fallback. `GameLevelManager.Awake` still
spawned human participants and could bind them before their `PlayerController.Start()`;
`SpawnCoordinator.BindEveryHumanController` was still the established human-only binding path.
`PlayerController` still had no direct `.unity` scene serialization. No existing contract
(`IShooterActor`, `IPlayerDamageReactionHost`, `IPlayerDunkHost`, `IPlayerControllerParticipantState`,
`IPlayerIdleSniperRuntime`) already exposed these three capabilities.

**No serialized field changed.** `PlayerController`'s `[SerializeField]` list, GUID, and public API are
unchanged except for the one new public `BindMatchRuntime(IPlayerMatchRuntime)` method, the same
binding-method shape every prior Phase 2b `PlayerController` composition seam (`BindArenaContext`,
`BindLegacyTouchMovementReader`, `BindDamageReactionCameraReader`, `BindIdleSniperRuntimeReader`) already
added without a prefab/scene edit. No prefab or scene asset was opened or resaved.

**Validation.** Headless Unity `6000.5.7f1` batch compile: clean, zero `error CS` lines. EditMode batch
run filtered to `Level5PlayerControllerDependencyGuardTests` (5, including the new
`PlayerControllerHasNoMatchRuntimeReference` guard), `Level5ProductionAssemblyBoundaryTests` (27,
including the new `PlayerMatchRuntimeCompilesIntoLevel5Player` identity check), the new
`Level5PlayerMatchRuntimeCompositionTests` (12: `GameLevelManager` implements `IPlayerMatchRuntime`;
`Rules`/`CustomCamera`/`LocalInputSlotFor` forward live rather than from a snapshot;
`BindHumanMatchRuntime` reaches every human with the exact provider instance and skips CPUs; an
unbound human fails closed without a NullReferenceException; the CPU guard still fires before the
match-runtime requirement), `Level5PlayerControllerIdleSniperTests` (7, updated to bind a live-forwarding
`IPlayerMatchRuntime` adapter since `checkIdleTimeForSniper` no longer reads the static),
`Level5PlayerInputReaderCompositionTests` (16, same update to its human-registration helper), and
`Level5PlayerControllerParticipantStateTests` (7, confirming the pre-existing CPU-guard test is
unaffected): 74/74 passed. Real-gameplay PlayMode path
`PlayerMovementPhysicsTests.JumpingDoesNotCompoundHorizontalVelocity` (menu -> gameplay level ->
`GameLevelManager.Awake` -> `SpawnCoordinator.BindHumanMatchRuntime` -> spawned `PlayerController`
receiving the provider before its own `Start()` -> real jump/rules path) also passed, confirming the
composition order holds in a real scene load, not only in isolated EditMode fixtures. Per the
risk-based validation policy, full EditMode/PlayMode were not re-run; PR CI owns that broader regression
coverage.

Not moved in this slice: `PlayerController`, `PlayerIdentifier`, `GameLevelManager`, or `MatchRuntime`
itself.

**`PlayerController` dependency closure, freshly remeasured after this slice (2026-09-10).** Same scan
as prior slices; `MatchRuntime` moves out of group B entirely (it is not a project-owned type, so it
does not join group A either - it simply stops being named by `PlayerController.cs`):

```text
PlayerController blockers: 1 -> 0
```

**A. Already owned by custom assemblies - legal references, not blockers (24, up from 23).**

- **`Level5.Input`**: `PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`
- **`Level5.Core`**: `IShooterActor`, `ShooterAttributes`, `IGroundHeightProvider`
- **`Level5.Basketball`**: `BasketBall`, `ShotMeter`, `BasketBallState`
- **`Level5.Utility`**: `SceneObjects`, `UtilityFunctions`, `RigidbodyFreezeHelper`
- **`Level5.Player`**: `PlayerSwapAttack`, `CallBallToPlayer`, `PlayerHealth`, `CharacterProfile`,
  `ShooterAttributesMapper`, `PlayerAttackQueue`, `PlayerDamageReactions`,
  `IPlayerDamageReactionHost`, `IPlayerIdleSniperRuntime`, `IPlayerControllerParticipantState`,
  `PlayerDunk`, **`IPlayerMatchRuntime`** (this slice)

**B. Still compiled into `Assembly-CSharp` - the actual remaining blocker (0, down from 1).**

- none

`PlayerController` remains in `Assembly-CSharp` but is now fully dependency-closed - every project type
it names resolves to a custom assembly, and it names no `MatchRuntime`/`GameLevelManager`/
`CameraManager`/`SniperManager`/`PlayerIdentifier` reference (all five now permanently guarded by
`Level5PlayerControllerDependencyGuardTests`). It is therefore the next candidate for a pure ownership
move into `Level5.Player`, the same shape Slice 31 was for `PlayerAttackQueue` after Slice 30 and
Slice 36 was for `PlayerDunk` after Slice 35. Not implemented this slice:

```text
PlayerController
        ↓
Level5.Player
```

Its 48-prefab serialized surface needs its own GUID/assembly/prefab/cross-assembly migration audit
before that move - a materially larger verification burden than the single-consumer moves Slices 31 and
36 already completed, and explicitly out of scope for this slice.

**Slice 38 - `PlayerController` moved into `Level5.Player`; pure ownership move, no behavior change**
(2026-09-10): Slice 37 already cut `PlayerController`'s last `Assembly-CSharp` edge (`MatchRuntime`)
and left it fully dependency-closed; this slice is the ownership move that follows, the same shape
Slice 36 was for `PlayerDunk` after Slice 35 - held open deliberately in Slice 37 because this
controller's 48-prefab serialized surface needed its own migration audit before the move, unlike the
single-consumer moves Slices 31 and 36 completed directly.

**Preflight re-verification.** Baseline SHA (`d2e94f8b1`, Slice 37, PR #130) matched current `dev`
exactly. `PlayerController` still had zero live `Assembly-CSharp` type dependencies; its input
dependencies (`PlayerControls`, `PlayerControlsProvider`, `PlayerInputReader`) were still owned by
`Level5.Input`, which still had no dependency, direct or transitive, on `Level5.Player`. The `.meta`
still carried GUID `94042c583f6a9b04d9bd1dbd2ef81d4f` and `executionOrder: -2000`. Serialized usage was
reconfirmed by GUID (not name-substring, which also matches `AutoPlayerController`/
`...ParticipantState` and over-counts): 48 prefab carriers (47 under
`Assets/Resources/Prefabs/characters/players/`, one `Assets/Resources/Prefabs/player_christy.prefab`),
zero `.unity` scene carriers. No `PlayerController, Assembly-CSharp` assembly-qualified string existed
anywhere in the project. `Level5.Player/AssemblyInfo.cs` still granted
`InternalsVisibleTo("Assembly-CSharp")`.

- `Assets/Scripts/player/PlayerController.cs`/`.cs.meta` moved to
  `Assets/Scripts/player/Level5Player/PlayerController.cs`/`.cs.meta` via `git mv` - git recognized both
  as pure renames; GUID preserved unchanged. Executable source is otherwise unchanged: no field,
  method, lifecycle, or behavior edit.
- `Level5.Player.asmdef` gained exactly one direct reference, `Level5.Input` - the project assembly
  owning `PlayerControls`/`PlayerControlsProvider`/`PlayerInputReader`, this controller's only
  dependency not already covered by the asmdef's existing references. Final reference list:
  `Level5.Core`, `Level5.Combat`, `Level5.Constants`, `Level5.Basketball`, `Level5.Utility`,
  `Level5.Input`. Freshly checked: `Level5.Input.asmdef` references only `Unity.InputSystem`, no
  dependency back on `Level5.Player` - acyclic.
- Three real `<see cref>` links into the moved file's XML docs named `SpawnCoordinator` members
  (`BindHumanMatchRuntime`, `BindHumanArenaContext`, `BindHumanLegacyTouchMovement`) - all
  `Assembly-CSharp` types. Converted to `<c>` text tags so they no longer imply a compile-time reverse
  dependency into `Assembly-CSharp`; every other `cref` in the file already pointed at a
  same-assembly-reachable member or type and was left untouched.
- Two comment blocks describing assembly-history reasoning were corrected: the `AUD-002` note on
  `anim`/`rigidBody`/`playerHealth` claimed "nothing outside Assets/Scripts can see these either
  way," no longer true now that these `internal` fields are visible only within `Level5.Player` and to
  `Assembly-CSharp` through the existing `InternalsVisibleTo` bridge; and the `damageReactions` note
  claiming `PlayerDamageReactions` "is no longer a same-assembly helper," which is now backwards since
  the controller joined it in `Level5.Player`. Both were corrected in place without touching executable
  code.
- `Level5PlayerControllerDependencyGuardTests`'s source path updated to the new location; its five
  permanent invariants (zero live `GameLevelManager`/`CameraManager`/`SniperManager`/
  `PlayerIdentifier`/`MatchRuntime` references) are unchanged and still enforced against the moved
  file. Class doc gained a Slice 38 note recording the move, the same shape
  `Level5PlayerDunkDependencyGuardTests` carries for Slice 36.
- `Level5ProductionAssemblyBoundaryTests` gained `PlayerControllerCompilesIntoLevel5Player`, the same
  identity-check shape as `PlayerDunkCompilesIntoLevel5Player`, proving the moved type actually compiles
  into `Level5.Player` rather than falling back to `Assembly-CSharp`.
- No production caller (`GameLevelManager`, `SpawnCoordinator`, `PlayerIdentifier`,
  `PlayerCollisions`, `PlayerAnimationEvents`, `SniperManager`, or any other) needed a source change -
  all reach the moved type through the auto-referenced `Level5.Player` assembly exactly as before.

**Serialized identity.** No prefab or scene asset was opened or resaved. All 48 GUID-carrying prefabs
are unaffected by the file's new path: Unity resolves `m_Script` by GUID, not path. Representative spot
check confirmed the `m_Script` GUID line unchanged, byte-for-byte, in both `player_ian.prefab` (ordinary
`Assets/Resources/Prefabs/characters/players/` carrier) and `player_christy.prefab` (the one root-level
legacy carrier) before and after the move.

**Validation.** Headless Unity `6000.5.7f1` EditMode batch run, post-move, filtered to
`Level5PlayerControllerDependencyGuardTests` (5), `Level5ProductionAssemblyBoundaryTests` (28,
including the new `PlayerControllerCompilesIntoLevel5Player` identity check),
`Level5PlayerMatchRuntimeCompositionTests` (12), `Level5PlayerInputReaderCompositionTests` (16), and
`Level5PlayerControllerParticipantStateTests` (7): 68/68 passed, compilation succeeded with the new
asmdef reference in place - this doubles as the acyclicity proof, since a reference cycle would have
failed compilation outright. Real-gameplay PlayMode path
`PlayerMovementPhysicsTests.JumpingDoesNotCompoundHorizontalVelocity` (menu -> gameplay level ->
`GameLevelManager`/`SpawnCoordinator` composition -> spawned Resources player prefab -> preserved GUID
resolving to the moved `PlayerController` in `Level5.Player` -> real jump/movement path) also passed,
confirming a Resources-loaded player prefab still resolves and initializes correctly post-move.
`scripts/validate-repository.ps1` passed (asmdef and `.meta` ownership changed). Per the risk-based
validation policy, full EditMode/PlayMode were not re-run; PR CI owns that broader regression coverage.

Not moved in this slice: `PlayerIdentifier`, `GameLevelManager`, `MatchRuntime`, or any other remaining
`Assembly-CSharp` player/game-manager type.

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

**Still blocked after Slice 11 (2026-09-04).** `BasketBall` itself now lives in `Level5.Basketball`,
but every file in the workaround folder that references it also references `GameStats`,
`MatchController`, or `PlayerController` - all still `Assembly-CSharp`, since Slice 11 only cut
basketball's own outbound edges and did not touch `player` or `game manager`. 2c's exit condition is
unchanged: it needs `player`/`game manager` migrated too, not just basketball.

**Still blocked after Slice 12 (2026-09-05), re-verified against the current folder rather than the
2026-08-20 four-file count.** `Assets/Tests/PlayModeGameplay` now holds nine files, not four -
`Level5MenuScreenPlayModeTests.cs`, `Level5MoneyBallStateCompositionPlayModeTests.cs`,
`Level5BasketBallShotMadeCompositionPlayModeTests.cs`, `Level5ShotMarkerSessionCompositionPlayModeTests.cs`
and `Level5BasketBallShotTelemetryCompositionPlayModeTests.cs` were added since that count and were never
part of the blocked set (they exercise `BasketBall`/`GameRules` composition only, no `GameStats`/
`PlayerController`). `MatchController` moving to `Level5.Match` in this slice removes it from the
blocker list, but three files still reach a type that remains `Assembly-CSharp` directly:
`Level5GameplayPlayModeTests.cs` reaches `VersusRuntime`, `VersusMatchReporter` and `VersusCatalogs`
(`Assets/Scripts/versus/`) plus `ActiveMatch` and `MatchCatalogs` (`Assets/Scripts/menu_start/`) — not
`GameStats`, which Slice 11 already moved into `Level5.Basketball` and which this file's own
`host.AddComponent<GameStats>()` calls now resolve through, without issue, since `Level5.Basketball` is
`autoReferenced`; `BasketballVisibilityTests.cs` and `PlayerMovementPhysicsTests.cs` reach
`PlayerController` via `FindAnyObjectByType`/`GetComponent`. 2c's exit condition is unchanged: those
three files stay blocked until `versus`/`menu_start` (already known blocked via `AtomicFile`, see
Slices 3-9) and `PlayerController` themselves migrate, which needs the `player`/`game manager` cycle cut
first, not a continuation of leaf-picking.

**Still blocked after Slice 13 (2026-09-06), re-verified against the current folder.** The same nine
files as Slice 12's count; no files were added or removed. Slice 13 moved `VersusCatalogs` out of
`Assembly-CSharp`, and `Level5GameplayPlayModeTests.cs`'s `VersusCatalogs.Reset()` call now resolves
through `Level5.Versus` (`autoReferenced`) instead — but the same file still calls `VersusRuntime.Reset()`
and `ActiveVersusAttempt.Clear()` directly (`Assembly-CSharp`, `Assets/Scripts/versus/`) and
`ActiveMatch.Clear()`/`MatchCatalogs.Reset()` (`Assembly-CSharp`, `Assets/Scripts/menu_start/`), and its
own body still calls `VersusMatchReporter.TryReport(...)`. `BasketballVisibilityTests.cs` and
`PlayerMovementPhysicsTests.cs` are unaffected by this slice — neither references anything in `versus`.
2c's exit condition is unchanged: `VersusRuntime`, `VersusMatchReporter`, `ActiveVersusAttempt`
(blocked on `ActiveMatch`), `FileVersusSeriesRepository` (blocked on `AtomicFile`), `ActiveMatch`,
`MatchCatalogs`, and `PlayerController` all still need to migrate, which needs the `player`/`game
manager` cycle cut first, not a continuation of versus leaf-picking — `VersusCatalogs`/
`DefaultCompetitiveRulesets` were `versus`'s only dependency-closed pair (see Slice 13 above).

**Still blocked after Slice 14 (2026-09-06).** Same nine files as Slices 12-13, unchanged, for the
same reasons Slice 13 gave — `AtomicFile` moving to `Level5.Utility` touches none of the direct
`VersusRuntime`/`VersusMatchReporter`/`ActiveVersusAttempt`/`ActiveMatch`/`MatchCatalogs` calls in
`Level5GameplayPlayModeTests.cs` that keep it blocked. What changed is *why* `FileVersusSeriesRepository`
is blocked: no longer on `AtomicFile` (see Slice 14's remeasurement above — it is now dependency-closed
on its own), just on still living in `Assembly-CSharp` — a future-candidate leaf, not a cleared one.
2c's exit condition is unchanged.

**Still blocked after Slice 15 (2026-09-06), re-verified against the current folder.** Same nine files
as Slices 12-14, unchanged. `Level5GameplayPlayModeTests.cs`'s `VersusRuntime.Override(...)`,
`VersusRuntime.Reset()` and four separate `VersusRuntime.Coordinator` property accesses (`CreateSeries`,
`Load`, `IssueAttempt`, `StartAttempt`) now resolve through `Level5.Versus` (`autoReferenced`) instead
of `Assembly-CSharp`, closing the last `versus`-side gap this slice could close — but the same file
still calls `VersusMatchReporter.TryReport(...)` and
`ActiveVersusAttempt.Clear()` (`Assembly-CSharp`, `Assets/Scripts/versus/`) and `ActiveMatch.Clear()`/
`MatchCatalogs.Reset()` (`Assembly-CSharp`, `Assets/Scripts/menu_start/`) directly, and
`BasketballVisibilityTests.cs`/`PlayerMovementPhysicsTests.cs` are unaffected — neither references
anything in `versus`. 2c's exit condition is unchanged: `VersusMatchReporter`, `ActiveVersusAttempt`,
`ActiveMatch`, `MatchCatalogs`, and `PlayerController` all still need to migrate, which needs the
`player`/`game manager` cycle cut first, not a continuation of versus leaf-picking —
`FileVersusSeriesRepository`/`VersusRuntime` were `versus`'s only remaining dependency-closed pair after
Slice 14's remeasurement (see Slice 15 above).

**Still blocked after Slice 16 (2026-09-06), re-verified against the current folder.** Same nine files
as Slices 12-15, unchanged. Slice 16 moved `GameStatsAttemptResults` out of `Assembly-CSharp`, but
`Level5GameplayPlayModeTests.cs` never referenced that type directly — its `VersusMatchReporter.TryReport(...)`
call already ran on top of the mapper before this slice and still does, unaffected by which assembly the
mapper lives in. The same file still calls `VersusMatchReporter.TryReport(...)` and
`ActiveVersusAttempt.Clear()` directly (`Assembly-CSharp`, `Assets/Scripts/versus/`) and
`ActiveMatch.Clear()`/`MatchCatalogs.Reset()` (`Assembly-CSharp`, `Assets/Scripts/menu_start/`), and
`BasketballVisibilityTests.cs`/`PlayerMovementPhysicsTests.cs` are unaffected — neither references
anything in `versus`. 2c's exit condition is unchanged: `VersusMatchReporter`, `ActiveVersusAttempt`,
`ActiveMatch`, `MatchCatalogs`, and `PlayerController` all still need to migrate, which needs the
`player`/`game manager` cycle cut first, not a continuation of versus leaf-picking —
`GameStatsAttemptResults` was `versus`'s only remaining dependency-closed file after Slice 15 (see Slice
16 above); `VersusMatchReporter`'s one remaining blocker is now `ActiveVersusAttempt` alone (see Slice
16's remeasurement).

**Still blocked after Slice 17 (2026-09-07), re-verified against the current folder.** Same nine files
as Slices 12-16, unchanged. Slice 17 moved `MatchSession` out of `Assembly-CSharp`, but none of the
nine blocked files reference `MatchSession` directly — `Level5MenuScreenPlayModeTests.cs` only
mentions it in a doc comment explaining why its isolated scene load skips `GameLevelManager`/
`MatchSession` initialization, not in code. `Level5GameplayPlayModeTests.cs` still calls
`VersusMatchReporter.TryReport(...)`/`ActiveVersusAttempt.Clear()` (`Assembly-CSharp`,
`Assets/Scripts/versus/`) and `ActiveMatch.Clear()`/`MatchCatalogs.Reset()` (`Assembly-CSharp`,
`Assets/Scripts/menu_start/`) directly, unaffected by this slice. 2c's exit condition is unchanged:
`VersusMatchReporter`, `ActiveVersusAttempt`, `ActiveMatch`, `MatchCatalogs`, and `PlayerController`
all still need to migrate, which needs the `player`/`game manager` cycle cut first — this slice's
remeasurement found `ActiveMatch` itself now dependency-closed (see Slice 17 above), the first of
those five with no remaining `Assembly-CSharp` edge, but it is not moved in this PR.

**Still blocked after Slice 18 (2026-09-07), re-verified against the current folder.** Same nine files
as Slices 12-17, unchanged. Slice 18 moved `ActiveMatch` out of `Assembly-CSharp`, and
`Level5GameplayPlayModeTests.cs`'s `ActiveMatch.Clear()` call now resolves through `Level5.Match`
(`autoReferenced`) instead — but the same file still calls `VersusMatchReporter.TryReport(...)`/
`ActiveVersusAttempt.Clear()` (`Assembly-CSharp`, `Assets/Scripts/versus/`) and `MatchCatalogs.Reset()`
(`Assembly-CSharp`, `Assets/Scripts/menu_start/`) directly, and `BasketballVisibilityTests.cs`/
`PlayerMovementPhysicsTests.cs` are unaffected — neither references anything in `menu_start`. 2c's exit
condition is unchanged: `VersusMatchReporter`, `ActiveVersusAttempt`, `MatchCatalogs`, and
`PlayerController` all still need to migrate, which needs the `player`/`game manager` cycle cut first.
This slice's remeasurement found `ActiveVersusAttempt` itself now dependency-closed (see Slice 18 above),
the next candidate with no remaining `Assembly-CSharp` edge, but it is not moved in this PR.

**Still blocked after Slice 19 (2026-09-07), re-verified against the current folder.** Same nine files
as Slices 12-18, unchanged. Slice 19 moved `VersusMatchReporter` and `ActiveVersusAttempt` out of
`Assembly-CSharp`, and `Level5GameplayPlayModeTests.cs`'s `VersusMatchReporter.TryReport(...)`/
`ActiveVersusAttempt.Clear()` calls now resolve through `Level5.Versus` (`autoReferenced`) instead —
but the same file still calls `MatchCatalogs.Reset()` (`Assembly-CSharp`, `Assets/Scripts/menu_start/`)
directly, and `BasketballVisibilityTests.cs`/`PlayerMovementPhysicsTests.cs` each call
`PlayerController` directly (`Assembly-CSharp`, `Assets/Scripts/player/`), unaffected by this slice.
2c's exit condition is unchanged: `MatchCatalogs` and `PlayerController` are now the only two remaining
direct `Assembly-CSharp` dependencies the workaround needs migrated, which needs the `player`/
`game manager` cycle cut first — this is a diagnostic finding only, neither is moved in this PR.

**Still blocked after Slice 20 (2026-09-07), re-verified against the current folder.** Same nine files
as Slices 12-19, unchanged. Slice 20 moved `MatchCatalogs` out of `Assembly-CSharp`, and
`Level5GameplayPlayModeTests.cs`'s `MatchCatalogs.Reset()` call now resolves through `Level5.Match`
(`autoReferenced`) instead — closing the last direct `Assembly-CSharp` dependency that file had.
`Level5GameplayPlayModeTests.cs` itself now references no type remaining in `Assembly-CSharp` directly.
`PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs` each still call
`PlayerController` directly (`Assembly-CSharp`, `Assets/Scripts/player/`) via `GetComponent`/
`FindAnyObjectByType`, unaffected by this slice, and the remaining six files
(`GameplayLevelUnpauseTests.cs`, `Level5MenuScreenPlayModeTests.cs`, and the four `*Composition
PlayModeTests.cs` files) are unaffected as in every prior remeasurement — none references anything in
`menu_start`, `versus`, or `player`. 2c's exit condition is unchanged in kind but narrower in scope:
`PlayerController` is now the *only* remaining direct `Assembly-CSharp` dependency across all nine
files in the workaround folder, still gated on cutting the `player`/`game manager` cycle first — this
is a diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 21 (2026-09-07), re-verified against the current folder.** Same nine files
as Slices 12-20, unchanged. Slice 21 removed `PlayerController`'s direct `GameLevelManager` dependency
through explicit composition, but moved no file — `PlayerMovementPhysicsTests.cs`/
`BasketballVisibilityTests.cs` still call `PlayerController` directly (`Assembly-CSharp`,
`Assets/Scripts/player/`) via `GetComponent`/`FindAnyObjectByType`, unaffected by this slice: removing
one of `PlayerController`'s own outbound edges does not change that these test files reach the type
itself, which remains `Assembly-CSharp` since it did not move. 2c's exit condition is unchanged in kind:
`PlayerController` is still the only remaining direct `Assembly-CSharp` dependency across all nine
workaround files, still gated on cutting the `player`/`game manager` cycle first, and now additionally
on `PlayerController`'s own remaining `Assembly-CSharp` dependency set (input, match/session runtime,
sniper/projectile, player-local components, gameplay adapters/helpers — the 13 types in group B of this
slice's remeasured closure scan above) before a move becomes possible. Its `Level5.Input`/`Level5.Core`/
`Level5.Basketball`/`Level5.Utility` dependencies are legal references and do not block the move — this
is a diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 22 (2026-09-08), re-verified against the current folder.** Same nine files
as Slices 12-21, unchanged. Slice 22 moved `RigidbodyFreezeHelper` into `Level5.Utility`, but none of
the nine workaround files references that helper at all - its only three callers are production
controllers - so this slice changes nothing for them directly. `PlayerMovementPhysicsTests.cs` and
`BasketballVisibilityTests.cs` still call `PlayerController` directly (`Assembly-CSharp`,
`Assets/Scripts/player/`) via `GetComponent`/`FindAnyObjectByType`. 2c's exit condition is unchanged
in kind and one entry narrower in scope: `PlayerController` is still the only remaining direct
`Assembly-CSharp` dependency across all nine workaround files, still gated on cutting the `player`/
`game manager` cycle first and on `PlayerController`'s own remaining `Assembly-CSharp` dependency set,
now 12 types rather than 13 (group B of this slice's freshly remeasured closure scan above). This is a
diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 23 (2026-09-08), re-verified against the current folder.** Same nine files
as Slices 12-22, unchanged. Slice 23 created `Level5.Player` and moved `PlayerSwapAttack` into it, but
no file in the workaround folder mentions `PlayerSwapAttack` at all - its only four callers are
production controllers - so this slice changes nothing for them directly. `PlayerMovementPhysicsTests.cs`
and `BasketballVisibilityTests.cs` still call `PlayerController` directly (`Assembly-CSharp`,
`Assets/Scripts/player/`) via `GetComponent`/`FindAnyObjectByType`. 2c's exit condition is unchanged in
kind and one entry narrower in scope: `PlayerController` is still the only remaining direct
`Assembly-CSharp` dependency across all nine workaround files, still gated on cutting the `player`/
`game manager` cycle first and on `PlayerController`'s own remaining `Assembly-CSharp` dependency set,
now 11 types rather than 12 (group B of this slice's freshly remeasured closure scan above). The
assembly those tests will eventually need `PlayerController` to live in now exists, which narrows the
remaining work to moving the type rather than also choosing its home. This is a diagnostic finding
only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 24 (2026-09-08), re-verified against the current folder.** Same nine files
as Slices 12-23, unchanged. Slice 24 inverted `CallBallToPlayer`'s `MatchRuntime` dependency and moved
it into `Level5.Player`, but no file in the workaround folder mentions `CallBallToPlayer` at all - its
four callers are production components - so this slice changes nothing for them directly.
`PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs` still call `PlayerController`
directly (`Assembly-CSharp`, `Assets/Scripts/player/`) via `GetComponent`/`FindAnyObjectByType`. 2c's
exit condition is unchanged in kind and one entry narrower in scope: `PlayerController` is still the
only remaining direct `Assembly-CSharp` dependency across all nine workaround files, still gated on
cutting the `player`/`game manager` cycle first and on `PlayerController`'s own remaining
`Assembly-CSharp` dependency set, now 10 types rather than 11 (group B of this slice's freshly
remeasured closure scan above). `MatchRuntime` is still in that set: this slice cut
`CallBallToPlayer`'s edge to it, not `PlayerController`'s own. This is a diagnostic finding only,
`PlayerController` is not moved in this PR.

**Still blocked after Slice 25 (2026-09-08), re-verified against the current folder.** Same nine files
as Slices 12-24, unchanged. No file in the workaround folder mentions `PlayerHealth` at all, so this
slice changes nothing for them directly. `PlayerMovementPhysicsTests.cs` and
`BasketballVisibilityTests.cs` still call `PlayerController` directly (`Assembly-CSharp`,
`Assets/Scripts/player/`) via `GetComponent`/`FindAnyObjectByType`. 2c's exit condition is unchanged in
kind and one entry narrower in scope: `PlayerController` is still the only remaining direct
`Assembly-CSharp` dependency across all nine workaround files, still gated on cutting the `player`/`game
manager` cycle first and on `PlayerController`'s own remaining `Assembly-CSharp` dependency set, now 9
types rather than 10 (group B of this slice's freshly remeasured closure scan above). `MatchRuntime` is
still in that set: this slice cut `PlayerHealth`'s edge to it, not `PlayerController`'s. This is a
diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 26 (2026-09-08), re-verified against the current folder.** Same nine files,
unchanged. No file in the workaround folder mentions `PlayerInputReader`, so this slice changes nothing
for them directly either. `PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs` still reach
`PlayerController` via `GetComponent`/`FindAnyObjectByType`, and `Level5GameplayPlayModeTests.cs` still
reaches `VersusRuntime`, `VersusMatchReporter`, `ActiveVersusAttempt` and `ActiveMatch`'s remaining
`Assembly-CSharp` neighbours. 2c's exit condition is unchanged in kind and one entry narrower in scope
again: `PlayerController`'s own remaining `Assembly-CSharp` dependency set is now 8 types rather than 9
(group B of this slice's freshly remeasured closure scan above). This is a diagnostic finding only,
`PlayerController` is not moved in this PR.

**Still blocked after Slice 27 (2026-09-08), re-verified against the current folder.** Same nine files,
unchanged. Slice 27 moved no type at all - it cut `CharacterProfile`'s `MatchRuntime` and `LoadedData`
dependencies and left the file in `Assembly-CSharp` - so 2c's exit condition is unchanged in both kind
and scope: `PlayerController`'s own remaining `Assembly-CSharp` dependency set is still 8 types (group B
of this slice's freshly remeasured closure scan above), and `PlayerMovementPhysicsTests.cs` and
`BasketballVisibilityTests.cs` still reach `PlayerController` via `GetComponent`/`FindAnyObjectByType`.
What did change is that `CharacterProfile`, one of those eight, is now dependency-closed and ready to
move. This is a diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 28 (2026-09-09), re-verified against the current folder.** Same nine
files, unchanged - Slice 28 moved `CharacterProfile` (and its stat-mapping closure) into
`Level5.Player`, but `PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs` still reach
`PlayerController` via `GetComponent`/`FindAnyObjectByType`, and `PlayerController` itself was not
edited or moved. 2c's exit condition is unchanged in kind and one entry narrower in scope:
`PlayerController`'s own remaining `Assembly-CSharp` dependency set is now 7 types, down from 8 (group
B of this slice's freshly remeasured closure scan above). This is a diagnostic finding only,
`PlayerController` is not moved in this PR.

**Still blocked after Slice 29 (2026-09-09), re-verified against the current folder.** Same nine
files, unchanged - Slice 29 moved `ShooterAttributesMapper` into `Level5.Player`, but
`PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs` still reach `PlayerController` via
`GetComponent`/`FindAnyObjectByType`, and `PlayerController` itself was not edited or moved. 2c's exit
condition is unchanged in kind and one entry narrower in scope: `PlayerController`'s own remaining
`Assembly-CSharp` dependency set is now 6 types, down from 7 (group B of this slice's freshly
remeasured closure scan above). This is a diagnostic finding only, `PlayerController` is not moved in
this PR.

**Still blocked after Slice 30 (2026-09-09), re-verified against the current folder.** Same nine
files, unchanged - Slice 30 moved `EnemyPopulationRules` into `Level5.Core` and cut
`PlayerAttackQueue`'s `MatchRuntime`/`PlayerIdentifier` dependencies, but left `PlayerAttackQueue`
itself in `Assembly-CSharp`, and `PlayerController` itself was not edited or moved. 2c's exit
condition is unchanged in both kind and scope: `PlayerMovementPhysicsTests.cs` and
`BasketballVisibilityTests.cs` still reach `PlayerController` via `GetComponent`/
`FindAnyObjectByType`, and `PlayerController`'s own remaining `Assembly-CSharp` dependency set is
still 6 types (group B of this slice's freshly remeasured closure scan above) - unchanged, because
this slice deliberately did not move `PlayerAttackQueue` itself. What did change is that
`PlayerAttackQueue`, one of those six, is now dependency-closed (aside from its same-assembly sibling
`PlayerAttackPosition`) and ready to move alongside it. This is a diagnostic finding only,
`PlayerController` is not moved in this PR.

**Still blocked after Slice 31 (2026-09-09), re-verified against the current folder.** Same nine
files, unchanged - Slice 31 moved `PlayerAttackQueue` and `PlayerAttackPosition` into
`Level5.Player`, but neither file in the workaround folder names either type directly (they reach
`PlayerController` itself), and `PlayerController` itself was not edited or moved. 2c's exit
condition is unchanged in both kind and scope: `PlayerMovementPhysicsTests.cs` and
`BasketballVisibilityTests.cs` still reach `PlayerController` via `GetComponent`/
`FindAnyObjectByType`, and `PlayerController`'s own remaining `Assembly-CSharp` dependency set is
now 5 types, down from 6 (group B of this slice's freshly remeasured closure scan above). This is a
diagnostic finding only, `PlayerController` is not moved in this PR.

**Still blocked after Slice 32 (2026-09-09), re-verified against the current folder.** Same nine
files, unchanged - Slice 32 moved `PlayerDamageReactions` and added `IPlayerDamageReactionHost` into
`Level5.Player`, but neither file in the workaround folder names either type directly (they reach
`PlayerController` itself), and `PlayerController` itself was not edited or moved. 2c's exit condition
is unchanged in both kind and scope: `PlayerMovementPhysicsTests.cs` and `BasketballVisibilityTests.cs`
still reach `PlayerController` via `GetComponent`/`FindAnyObjectByType`, and `PlayerController`'s own
remaining `Assembly-CSharp` dependency set is now 4 types, down from 5 (group B of this slice's freshly
remeasured closure scan above). This is a diagnostic finding only, `PlayerController` is not moved in
this PR.

**Current checkpoint — after Slices 38/39 (2026-09-10), superseding the "`PlayerController` is the
only remaining blocker" framing threaded through Slices 12-32 above.** Slice 38 (Phase 2b, not a 2c
slice itself) moved `PlayerController` into `Level5.Player`. A fresh file-by-file audit of the
workaround folder done for Slice 39 found that framing was no longer a sufficient description of
current dependencies: several files were already blocked on other `Assembly-CSharp` types
independently of `PlayerController`, and `Level5GameplayPlayModeTests.cs` — the file whose own header
comment had tracked its remaining blockers since Slice 19 — turned out to be fully dependency-clean
once re-audited against current source, with no live reference to `PlayerController` left in it at
all. Its production dependencies now resolve as: `MatchController`/`MatchCatalogs`/`ActiveMatch`
(`Level5.Match`); `VersusRuntime`/`VersusCatalogs`/`ActiveVersusAttempt`/`VersusMatchReporter`
(`Level5.Versus`); `GameStats` (`Level5.Basketball`); and the `Level5.Core.Match`/`Level5.Core.Versus`/
`Level5.Core.Versus.Persistence` model and repository types (`Level5.Core`).

**Slice 39 (2026-09-10) moved `Level5GameplayPlayModeTests.cs`/`.cs.meta`** (GUID
`b9fe884d9d919e143bf50dc3fec652c1` preserved) from `Assets/Tests/PlayModeGameplay` into
`Assets/Tests/PlayMode`, onto the normal `Level5.PlayModeTests` asmdef, whose `references` grew from
just `Level5.Core` to `["Level5.Core", "Level5.Match", "Level5.Versus", "Level5.Basketball"]` — the
four assemblies the fixture's production types directly resolve through per the paragraph above. No
production source or runtime asmdef changed. The asmdef-free workaround folder shrinks from nine files
to eight. This is the first fixture normalized under 2c; 2c is **in progress / partially unblocked**,
not complete — the remaining eight files still have direct, live dependencies on `Assembly-CSharp`
types, freshly re-verified against current source rather than carried forward from earlier slices:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `BasketballVisibilityTests.cs` | `StartManager`, `Pause` (`PlayerController` reference itself now resolves through `Level5.Player`, but is no longer this file's blocker) |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5BasketBallShotMadeCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameLevelManager` (`BasketBallShotMade` itself already lives in `Level5.Basketball`, not a blocker) |
| `Level5BasketBallShotTelemetryCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `AnaylticsManager` |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |
| `Level5MoneyBallStateCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules`, `GameLevelManager` |
| `Level5ShotMarkerSessionCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules` |
| `PlayerMovementPhysicsTests.cs` | `StartManager`, `Pause` (`PlayerController` reference itself now resolves through `Level5.Player`, but is no longer this file's blocker) |

None of the remaining eight are currently dependency-clean, so this checkpoint reveals no immediate
Slice 40 candidate: `StartManager`/`Pause` (the start-menu bootstrap and pause overlay) are the shared
blocker for seven of the eight, and `Level5MenuScreenPlayModeTests.cs` is blocked on four separate
menu-manager types instead. 2c's exit condition is otherwise unchanged from the framing above: the
`menu_start`/`game manager`/`menu_options`/`menu_credits`/`menu_stats`/`menu_login`/`analytics` types
above still need to migrate before any of these eight can normalize.

**Slice 40 (2026-09-11) hardened `Level5GameplayPlayModeTests` against PlayMode assembly execution
order.** Slice 39 normalized the fixture as the first dependency-clean gameplay PlayMode fixture, but
review found its singleton regression (`ASceneScopedSingletonReleasesItsStaticWhenDestroyed`)
implicitly inherited a clean `MatchController.instance` from `GameplayLevelUnpauseTests`'s
`UnityTearDown`, in the asmdef-free `PlayModeGameplay` workaround. The passing full-suite result
therefore depended on Unity's PlayMode runner happening to execute `Assembly-CSharp`'s tests before
`Level5.PlayModeTests`'s, not on anything that enforced that order. Slice 40 adds a
`[UnitySetUp]` (`EnsureMatchControllerIsolation`) to `Level5GameplayPlayModeTests` that destroys any
inherited `MatchController` component and yields a frame for its normal `OnDestroy` to clear the
static, before asserting the static is clear — no direct `MatchController.instance = null` reset. No
production source or asmdef changed, no test moved; the eight-file `PlayModeGameplay` workaround is
unchanged. Neither fixture now documents or requires a specific execution order between test
assemblies. Phase 2c remains in progress: this slice removed a suite-order dependency, not a
dependency-closure blocker, so the remaining-eight table above is unchanged.

**Slice 41 (2026-09-11) moved `BasketballVisibilityTests.cs`/`.cs.meta`** (GUID
`47ba6b7d839b57245899300b333306a2` preserved) **and `PlayerMovementPhysicsTests.cs`/`.cs.meta`** (GUID
`9d0dc6afd01f70d49b16eb3b845fb208` preserved) from `Assets/Tests/PlayModeGameplay` into
`Assets/Tests/PlayMode`. Both were blocked only on `StartManager`/`Pause` per the Slice 39 table above;
re-audited for this slice, that held: in both fixtures `StartManager`/`Pause` were reached solely to
drive the start menu into gameplay and to stop the start-on-pause screen from leaving `Time.timeScale`
at zero, never to assert either type's own behaviour. Direct migration of `StartManager`/`Pause`
themselves was rejected — both still have the broad dependency closures the Slice 39/40 checkpoints
describe (`menu_start`/`game manager`/`menu_options`/`menu_credits`/`menu_stats`/`menu_login`/
`analytics`), so moving either was out of scope for a two-fixture normalization.

Instead, both fixtures' duplicated bootstrap now goes through one new test-only helper,
`Assets/Tests/PlayMode/GameplayScenePlayModeHarness.cs` (`EnterPlayableGameplayScene`), added to
`Level5.PlayModeTests`. It loads `Constants.SCENE_NAME_level_00_start`, then drives the same public
`press_start` UI event a player would (via `ExecuteEvents`/`EventSystem`) instead of reflecting into
`StartManager.HasLoadedGameSetup`. Because that means the first submission can land before game setup
has actually loaded — production's real response is `StartManager.StartGame` bouncing back to the
loading scene and, once `LoadedData` is ready, back to the start scene — the harness retries: it keeps
submitting `press_start` (at most once every five frames, never every frame) for as long as the active
scene is still the start scene, and stops submitting the moment it is not, which also covers a bounce
back to the start scene starting a fresh retry rather than being mistaken for arrival. Once the active
scene has left the start scene, it waits for a live `PlayerController` (both fixtures need a spawned
human player) as evidence gameplay actually initialized, rather than treating leaving the start scene
alone as success. The whole sequence — UI wait, retries, and the post-transition gameplay wait — shares
one 60-second budget and fails with an actionable `Assert` if either half of it runs out. Pause
suppression stays a narrow compatibility seam: the harness looks for a live `MonoBehaviour` whose
runtime `Type.Name` is exactly `"Pause"` and disables it if found, rather than naming the type or
calling into it, then restores `Time.timeScale`. `PlayerMovementPhysicsTests.EnterGameplayLevel` is kept
as a thin wrapper that calls the harness and extracts the `Rigidbody`, so its three physics assertions'
call sites are unchanged. `BasketballVisibilityTests` calls the harness directly for the
`PlayerController` it forces `hasBasketball` on. Every basketball/player/physics assertion, reflection
lookup, teardown, and log-noise policy in both fixtures is otherwise unchanged — only their bootstrap
moved.

`Level5.PlayModeTests`'s `references` grew from `["Level5.Core", "Level5.Match", "Level5.Versus",
"Level5.Basketball"]` to add `Level5.Player` (`PlayerController`/`CharacterProfile`, the harness's own
dependency and `PlayerMovementPhysicsTests`' `Field<CharacterProfile>` lookup) and `Level5.Constants`
(`Constants.SCENE_NAME_level_00_start`, used by the harness and, already, by
`Level5GameplayPlayModeTests`' transitive types) — both already dependency-clean per the Slice 12-38
history above. `BasketBall`/`BasketBallAuto` were already reachable through the existing
`Level5.Basketball` reference. No production source or runtime asmdef changed.

No production/runtime-asmdef change either fixture. The asmdef-free `PlayModeGameplay` workaround
shrinks from eight files to six. Phase 2c remains in progress; StartManager/Pause block four of the six
remaining files and menu-manager types block the fifth:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5BasketBallShotMadeCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameLevelManager` |
| `Level5BasketBallShotTelemetryCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `AnaylticsManager` |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |
| `Level5MoneyBallStateCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules`, `GameLevelManager` |
| `Level5ShotMarkerSessionCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules` |

This table is carried forward from the Slice 39 audit rather than freshly re-verified line by line for
this slice — none of these six files changed in Slice 41, and Slice 41 did not migrate `GameRules`,
`GameLevelManager`, analytics, or the menu managers. Diagnostic only: the new harness is deliberately
not a generalization these six can reuse as-is, since several of them assert `StartManager`/`Pause`
composition behaviour directly rather than treating either as incidental.

**Slice 42 (2026-09-11) moved `Level5BasketBallShotTelemetryCompositionPlayModeTests.cs`/`.cs.meta`**
(audited against `dev` SHA `3c1c72dfc9f14dbaa28f800cb8cd354c8e74d6fe`, GUID
`99005b7269011ba40ad1d60b1b332171` preserved) from `Assets/Tests/PlayModeGameplay` into
`Assets/Tests/PlayMode`, reusing both Slice 41 helpers unchanged. `StartManager`/`Pause` bootstrap was
replaced with `GameplayScenePlayModeHarness.EnterPlayableGameplayScene`, the same as
`BasketballVisibilityTests`/`PlayerMovementPhysicsTests`; the fixture's own bounded 10-frame settle
after the harness returns was kept, since a live `PlayerController` proves the player spawned but not
that every ball's own `SpawnCoordinator.GiveBall` composition step (the telemetry bind included) has
finished, and current source offered no stronger deterministic signal for that specific to assert on
instead. Scene log-noise suppression, scene teardown, and `Time.timeScale` restoration now go through
`RealScenePlayModeTestSupport` (`IgnoreSceneLogNoise`/`UnloadAllLoadedScenes`) rather than the fixture's
own hand-rolled copies of both.

`AnaylticsManager` was this fixture's third blocker and needed different handling than
`StartManager`/`Pause`: the callback's declaring-type/method identity is still part of what this test
asserts, so it could not simply be dropped like the incidental bootstrap plumbing. It also does not
need a compile-time reference to assert that identity — the migrated fixture now inspects the already
scene-bound delegate through runtime reflection instead: `callback.Method.DeclaringType?.Name ==
"AnaylticsManager"` and `callback.Method.Name == "PlayerShoot"`, in place of
`typeof(AnaylticsManager)`/`nameof(AnaylticsManager.PlayerShoot)`. This is deliberately a weaker,
test-only string check than the EditMode assertion it complements: `Level5BasketBallShotTelemetryTests
.PrimaryHumanBallReceivesAnaylticsManagerPlayerShootAsItsTelemetryCallback` (`Assets/Tests/Editor`)
keeps the compiler-backed `typeof`/`nameof` assertion against a hand-built `PlayerRegistry`, so the
two suites together still prove both halves of the original contract — the exact callback production
composition chooses, and that callback actually reaching real scene-spawned human basketballs. Two
`<see cref>` references in the moved fixture's header (`Level5MoneyBallStateCompositionPlayModeTests`,
`Level5BasketBallShotTelemetryTests`) name types that are no longer visible across the
`Level5.PlayModeTests` assembly boundary (both remain asmdef-free/EditMode-only); both were changed to
`<c>` tags rather than adding a reference for documentation visibility alone.

`Level5.PlayModeTests`'s `references` were unchanged by this slice — `BasketBall`/`BasketBallAuto` were
already reachable through the existing `Level5.Basketball` reference, and no new dependency was
required. No production `.cs`, runtime `.asmdef`, scene, or prefab changed.

Validation: the migrated fixture alone (1/1 passed), `Level5BasketBallShotTelemetryTests` EditMode
(10/10 passed), the full `Level5.PlayModeTests` assembly (9/9 passed, up from the prior 8), the complete
unfiltered PlayMode suite (17/17 passed, unchanged from the prior baseline since this only moved a test
between assemblies), the complete EditMode suite (1206/1206 passed), and `scripts/validate-repository.ps1`
(passed) — all run against the pinned `6000.5.7f1` editor.

The asmdef-free `PlayModeGameplay` workaround shrinks from six files to five. Re-audited against current
source for this slice rather than carried forward:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5BasketBallShotMadeCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameLevelManager` |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |
| `Level5MoneyBallStateCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules`, `GameLevelManager` |
| `Level5ShotMarkerSessionCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules` |

None of these five changed in Slice 42, and Slice 42 did not migrate `GameRules`, `GameLevelManager`,
analytics, or the menu managers, so their blockers are unchanged from the Slice 39/41 audits.

**Slice 43 (2026-09-11) moved `Level5ShotMarkerSessionCompositionPlayModeTests.cs`/`.cs.meta`**
(audited against `dev` SHA `b2832fab58f4c43284fdd6b93c448952e83a647f`, GUID
`e4a5a2456b9becf10dee5f17b84b0f25` preserved) from `Assets/Tests/PlayModeGameplay` into
`Assets/Tests/PlayMode`, reusing the Slice 41/42 `GameplayScenePlayModeHarness`/
`RealScenePlayModeTestSupport` helpers unchanged. `StartManager`/`Pause` bootstrap was replaced with
`GameplayScenePlayModeHarness.EnterPlayableGameplayScene`; the fixture's own bounded 10-frame settle
after the harness returns was kept, for the same reason as Slice 42's telemetry fixture — a live
`PlayerController` proves the player spawned but not that `GameRules.Awake()`'s own marker-binding
composition step has finished, and current source offered no stronger deterministic signal specific to
that to assert on instead.

`GameRules` was this fixture's third blocker (alongside `StartManager`/`Pause`) and, like Slice 42's
`AnaylticsManager`, needed different handling: the assertion's whole point is that every scene marker is
bound to the *real* live `GameRules` singleton, so that identity could not simply be dropped. The
migrated fixture resolves the canonical runtime `GameRules` instance independently of any marker's own
`markerSession` field, so the identity check stays non-circular (`ResolveRuntimeGameRulesInstance`):
it scans every live `MonoBehaviour` (`FindObjectsInactive.Include`, matching the marker scan) for a
runtime type name of exactly `"GameRules"`, fails explicitly if none are found, reads that type's own
`public static GameRules instance` field through reflection, asserts it is non-null and itself reports
runtime type name `"GameRules"`, and asserts it is one of the discovered live components rather than
assuming a single match. Unlike Slice 42's `AnaylticsManager` check (a `string`-vs-`string` method
identity comparison), the marker/session/rules assertions themselves stay strongly typed against the
existing custom-assembly contracts — `IShotMarkerSession` and `ResolvedMatchRules` (both `Level5.Core`,
already reachable) — read via `RealScenePlayModeTestSupport.GetField<T>` instead of untyped `object`,
and compared with `ReferenceEquals` exactly as before. The compiler-backed exact-type `GameRules`
composition proof (bind-once semantics, multi-marker sharing, the resolved-rules reference, the
malformed-tag and duplicate-instance guards) remains unchanged in EditMode's
`Level5BasketballShotMarkerSessionTests`. Two `<see cref>` references in the moved fixture's header
(`Level5MoneyBallStateCompositionPlayModeTests`, `Level5BasketballShotMarkerSessionTests`) name types
no longer visible across the `Level5.PlayModeTests` assembly boundary and were changed to `<c>` tags.

`Level5.PlayModeTests`'s `references` were unchanged by this slice — `BasketBallShotMarker`
(`Level5.Basketball`), `IShotMarkerSession`, and `ResolvedMatchRules` (both `Level5.Core.Match`, under
the existing `Level5.Core` reference) were all already reachable; no new dependency was required. No
production `.cs`, runtime `.asmdef`, scene, or prefab changed.

Validation: the migrated fixture alone (1/1 passed), `Level5BasketballShotMarkerSessionTests` EditMode
(8/8 passed), the full `Level5.PlayModeTests` assembly (10/10 passed, up from the prior 9), the complete
unfiltered PlayMode suite (17/17 passed, unchanged), the complete EditMode suite (1206/1206 passed), and
`scripts/validate-repository.ps1` (passed) — all run against the pinned `6000.5.7f1` editor.

The asmdef-free `PlayModeGameplay` workaround shrinks from five files to four. Re-audited line by line
against current source for this slice rather than carried forward — one stale entry was found and
corrected: `Level5BasketBallShotMadeCompositionPlayModeTests.cs`'s current source asserts the scene's
bound `BasketBallShotMade.gameModeId` against `MatchRuntime.ModeId` directly, an `Assembly-CSharp` type
(`Assets/Scripts/game manager/MatchRuntime.cs`, no covering asmdef) not previously listed as one of its
blockers:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5BasketBallShotMadeCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameLevelManager`, `MatchRuntime` (corrected — previously listed without `MatchRuntime`) |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |
| `Level5MoneyBallStateCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules`, `GameLevelManager` |

`GameplayLevelUnpauseTests.cs`, `Level5MenuScreenPlayModeTests.cs`, and
`Level5MoneyBallStateCompositionPlayModeTests.cs` did not change in Slice 43 and were re-verified
unchanged from the Slice 39/42 audits. Slice 43 did not migrate `GameLevelManager`, `MatchRuntime`,
`GameRules`, or the menu managers.

**Slice 44 (2026-09-11) moved `Level5BasketBallShotMadeCompositionPlayModeTests.cs`/`.cs.meta`**
(audited against `dev` SHA `2be7f2d1fc5a35b00def7b42160aca2522e0331f`, GUID
`3636e7d4e765497482ccc22320a67221` preserved) from `Assets/Tests/PlayModeGameplay` into
`Assets/Tests/PlayMode`, reusing the Slice 41-43 `GameplayScenePlayModeHarness`/
`RealScenePlayModeTestSupport` helpers unchanged. `StartManager`/`Pause` bootstrap was replaced with
`GameplayScenePlayModeHarness.EnterPlayableGameplayScene`; the fixture's own bounded 10-frame settle
after the harness returns was kept, for the same reason as the Slice 42/43 fixtures — a live
`PlayerController` proves the player spawned but not that `GameLevelManager.Awake()`'s own
`BindMatchContext` composition step has finished, and current source offered no stronger deterministic
signal specific to that to assert on instead.

`GameLevelManager` and `MatchRuntime` were this fixture's blockers alongside `StartManager`/`Pause` (the
Slice 43 audit had already flagged `MatchRuntime` for this file). Re-verified from current source before
migrating: `GameLevelManager.Awake()` reads `_rules = MatchRuntime.Rules` and `_modeId =
MatchRuntime.ModeId` (`Assets/Scripts/game manager/GameLevelManager.cs:85-87`), then calls
`shotMade.BindMatchContext(_rules, _modeId)` (line 232); `MatchRuntime.Rules`/`MatchRuntime.ModeId`
answer with `ActiveMatch.Configuration.Rules`/`.ModeId` directly (not rebuilt) whenever
`ActiveMatch.IsActive` (`Assets/Scripts/game manager/MatchRuntime.cs:24-34,46-48`). `ActiveMatch` itself
lives in `Assets/Scripts/game manager/Level5Match/`, which is inside the `Level5.Match.asmdef` boundary
(unlike `GameLevelManager.cs`/`MatchRuntime.cs`, which sit directly under `Assets/Scripts/game manager/`
with no covering asmdef and stay in `Assembly-CSharp`) and already reachable through
`Level5.PlayModeTests`'s existing `Level5.Match` reference. This let the migrated fixture assert against
the typed `ActiveMatch.Configuration.Rules`/`.ModeId` directly instead of `GameLevelManager.instance
.Rules`/`MatchRuntime.ModeId`, dropping both `Assembly-CSharp` blockers with no reflection-by-name
workaround needed for the expected-value side of either assertion. `BasketBallShotMade` itself compiles
into `Level5.Basketball`, already reachable, so it is found and read exactly as before — its private
`hasBoundMatchContext`/`matchRules`/`gameModeId` fields still via
`RealScenePlayModeTestSupport.GetField`/`GetField<T>`, and the rules comparison stays `ReferenceEquals`
rather than value equality. The compiler-backed exact-type `BasketBallShotMade.BindMatchContext`
bind/rebind/fail-closed proof remains unchanged in EditMode's `Level5BasketBallShotMadeTests`, and the
permanent no-`MatchRuntime`-reference guard remains unchanged in
`Level5BasketBallShotMadeDependencyGuardTests` (that guard targets `BasketBallShotMade.cs` itself, which
this slice did not touch).

One `<see cref>` reference in the moved fixture's header (`Level5BasketBallShotMadeTests`) named a type
no longer visible across the `Level5.PlayModeTests` assembly boundary and was changed to a `<c>` tag;
`<see cref="Level5MoneyBallStateCompositionPlayModeTests"/>`, `<see
cref="Level5ShotMarkerSessionCompositionPlayModeTests"/>`, and `<see
cref="GameplayScenePlayModeHarness"/>` stayed `<see cref>` since all three are already in
`Level5.PlayModeTests`.

`Level5.PlayModeTests`'s `references` were unchanged by this slice — `ActiveMatch`/`MatchConfiguration`/
`ResolvedMatchRules`/`GameModeId` (`Level5.Match`/`Level5.Core.Match`, both already referenced) and
`BasketBallShotMade` (`Level5.Basketball`, already referenced) were all already reachable; no new
dependency was required. No production `.cs`, runtime `.asmdef`, scene, or prefab changed.

Validation (pinned `6000.5.7f1` editor): the migrated fixture alone (1/1 passed), `Level5BasketBallShotMadeTests`
+ `Level5BasketBallShotMadeDependencyGuardTests` EditMode (16/16 passed), the full `Level5.PlayModeTests`
assembly (11/11 passed, up from the prior 10), the complete unfiltered PlayMode suite (17/17 passed,
unchanged), the complete EditMode suite (1206/1206 passed, unchanged), and
`scripts/validate-repository.ps1` (passed).

The asmdef-free `PlayModeGameplay` workaround shrinks from four files to three. Re-audited line by line
against current source for this slice rather than carried forward:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |
| `Level5MoneyBallStateCompositionPlayModeTests.cs` | `StartManager`, `Pause`, `GameRules`, `GameLevelManager` |

All three were re-verified unchanged from the Slice 39/42/43 audits. Slice 44 did not migrate
`GameLevelManager`, `MatchRuntime`, `GameRules`, `StartManager`, `Pause`, or the menu managers.

**Slice 45 (2026-09-11) moved `Level5MoneyBallStateCompositionPlayModeTests.cs`/`.cs.meta`** (audited
against `dev` SHA `1cff89a9bd7566df19d541d24a66a74e5d0e01a9`, GUID `d496b7cee9454663935975236fa0f2fc`
preserved) from `Assets/Tests/PlayModeGameplay` into `Assets/Tests/PlayMode`, reusing the Slice 41-44
`GameplayScenePlayModeHarness`/`RealScenePlayModeTestSupport` helpers. `StartManager`/`Pause` bootstrap
was replaced with `GameplayScenePlayModeHarness.EnterPlayableGameplayScene`; the fixture's own bounded
10-frame settle after the harness returns was kept, for the same reason as the Slice 42-44 fixtures — a
live `PlayerController` proves the player spawned but not that `GameRules.Awake()`'s own money-ball-state
composition step has finished, and current source offered no stronger deterministic signal specific to
that to assert on instead.

`GameRules` and `GameLevelManager` were this fixture's remaining blockers alongside `StartManager`/
`Pause` (the Slice 43/44 audits had already flagged `GameRules` for the sibling shot-marker fixture).
Re-verified from current source before migrating: `GameLevelManager` runs at script execution order
`-8000` (`Assets/Scripts/game manager/GameLevelManager.cs.meta`) and its `Awake()` populates the
participant registry before any other object's `Awake()` can run; `GameRules.Awake()` (default order 0)
then calls `BindMoneyBallStateToBasketballs(GameLevelManager.instance != null ?
GameLevelManager.instance.Registry : null)` (`Assets/Scripts/game manager/GameRules.cs:133`), and
`BindMoneyBallStateToBasketballs` returns immediately, logging an error and binding nothing, when handed
a null registry (`GameRules.cs:184-190`). So a missing/unpopulated `GameLevelManager` would already
surface as a null `moneyBallState` on every ball — the per-ball provider assertions the fixture already
made prove the relevant `GameLevelManager` → `GameRules` ordering/composition chain held, making the
fixture's separate explicit `GameLevelManager.instance != null` assertion redundant. It was removed along
with the fixture's last compile-time `GameLevelManager` dependency; no reflection-by-name replacement was
needed since nothing else in the fixture named the type.

`GameRules` itself has no covering asmdef (`Assets/Scripts/game manager/GameRules.cs` sits directly under
`Assets/Scripts/game manager/` with no local `.asmdef`, same as `GameLevelManager.cs`/`MatchRuntime.cs`)
and stays in `Assembly-CSharp`, so the fixture cannot name it at compile time. Slice 43's shot-marker
fixture had already solved exactly this with a private `ResolveRuntimeGameRulesInstance()` helper that
scans live `MonoBehaviour`s for runtime type name `"GameRules"` and reads its `public static instance`
field by reflection. Slice 45 makes this fixture a second real consumer of the identical operation, so
per this slice's own instructions the resolver was moved into `RealScenePlayModeTestSupport` (as
`internal static object ResolveRuntimeGameRulesInstance()`, same fail-closed behavior: explicit failure
if zero `GameRules` components are found or if the resolved static instance is not one of them) and both
fixtures now call the one shared implementation. `Level5ShotMarkerSessionCompositionPlayModeTests.cs`'s
own copy was deleted rather than kept as a second implementation.

`BasketBall`/`BasketBallAuto` (both already in `Level5.Basketball`, already referenced) are found and
read exactly as before — their private `moneyBallState`/`matchRules` fields still via
`RealScenePlayModeTestSupport.GetField<T>`, and the provider-identity comparison stays `ReferenceEquals`
against the shared resolver's result rather than value equality. Both balls' `activeSelf` and bound
`ResolvedMatchRules` assertions were preserved unchanged. The compiler-backed exact-type
`GameRules.BindMoneyBallStateToBasketballs` composition proof — human/CPU/secondary-human composition,
participant-with-no-ball behavior, and the two balls' own `BindMoneyBallState` bind/rebind/null-guard
semantics — remains unchanged in EditMode's `Level5BasketballMoneyBallStateTests`.

Two `<see cref>` references in the moved fixture's header (`Level5BasketballMoneyBallStateTests`,
`BasketballVisibilityTests`) named types no longer visible across the `Level5.PlayModeTests` assembly
boundary and were changed to `<c>` tags; the new header instead links directly to
`Level5ShotMarkerSessionCompositionPlayModeTests` (`<c>`, since that fixture's method-level detail isn't
being cited) and `<see cref="RealScenePlayModeTestSupport"/>`/`<see cref="GameplayScenePlayModeHarness"/>`
(kept as `<see cref>`, both already in `Level5.PlayModeTests`).

`Level5.PlayModeTests`'s `references` were unchanged by this slice — `IMoneyBallState`/`ResolvedMatchRules`
(`Level5.Core.Match`, already referenced) and `BasketBall`/`BasketBallAuto` (`Level5.Basketball`, already
referenced) were all already reachable; no new dependency was required. No production `.cs`, runtime
`.asmdef`, scene, or prefab changed.

Validation (pinned `6000.5.7f1` editor): forced script compilation (0 `error CS` lines); the migrated
fixture together with `Level5ShotMarkerSessionCompositionPlayModeTests` after the resolver extraction
(2/2 passed); `Level5BasketballMoneyBallStateTests` EditMode (all passed); the full `Level5.PlayModeTests`
assembly (12/12 passed, up from the prior 11); the complete unfiltered PlayMode suite (17/17 passed,
unchanged); the complete EditMode suite (1206/1206 passed, unchanged); and
`scripts/validate-repository.ps1` (passed).

The asmdef-free `PlayModeGameplay` workaround shrinks from three files to two. Re-audited line by line
against current source for this slice rather than carried forward:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |
| `Level5MenuScreenPlayModeTests.cs` | `OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager` |

Both were re-verified unchanged from the Slice 39/42/43/44 audits. Slice 45 did not migrate
`GameLevelManager`, `GameRules`, `StartManager`, `Pause`, or the menu managers.

**Code review (2026-09-11) found two Low-severity polish gaps, both fixed:**
1. `RealScenePlayModeTestSupport`'s class summary still described only its original three
   responsibilities (log-noise suppression, scene teardown, private-field reflection) and the
   "extracted because ... all three" framing was now stale once this slice added a fourth
   (`ResolveRuntimeGameRulesInstance`). Fixed by extending the summary to name the resolver and its two
   real consumers, so a future fixture author skimming the class doc doesn't miss it and hand-roll a
   third copy.
2. The migrated fixture dropped the original's `DIAG` scene-name/ball-count log with nothing
   substituted, unlike the still-current Slice 42 telemetry fixture's equivalent log. Restored a single
   summary `Debug.Log` (active scene name, human/CPU ball counts) before the assertions, matching that
   precedent, so a future CI failure's log carries the same triage context the original fixture had.

Neither fix changes test behavior or assertions. Re-validated after the fixes: forced compilation (0
`error CS`), the migrated fixture alone, and `Level5ShotMarkerSessionCompositionPlayModeTests` all
passed.

**Slice 46 (2026-09-11) moved `Level5MenuScreenPlayModeTests.cs`/`.cs.meta`** (audited against `dev` SHA
`2768510b185934057015fe70ee4ac048b9b20365`, GUID `6082a513e79e4f4eb97ee6e2bf7665bb` preserved) from
`Assets/Tests/PlayModeGameplay` into `Assets/Tests/PlayMode`.

Unlike Slice 41-45, this fixture's remaining blocker was not a single runtime singleton but four
concrete menu manager types (`OptionsManager`, `CreditsManager`, `StatsManager`, `AccountManager`), each
declared in an `Assets/Scripts/menu_*` folder with no local `.asmdef` and so compiled into
`Assembly-CSharp`. The four `Object.FindFirstObjectByType<T>()` calls were replaced with one private
`FindManagerInScene(Scene scene, string typeName)` helper that walks `scene.GetRootGameObjects()` and
each root's `GetComponentsInChildren<MonoBehaviour>(includeInactive: false)`, checks
`behaviour.gameObject.scene == scene`, and matches `GetType().Name` exactly. This is stricter than the
original lookup it replaces (which searched every loaded scene, not just the one under test) while
preserving its active/inactive semantics: `includeInactive: false` excludes any component on an inactive
GameObject exactly as default `FindFirstObjectByType` did, but a *disabled* `MonoBehaviour` on an
otherwise active GameObject is still returned, so the existing `manager.enabled == true` assertion (which
exists specifically to catch `ValidateMenuUi` disabling a manager over a missing serialized reference)
can still fail correctly instead of the lookup masking it as "not found". Per this slice's own
instructions the helper stays private to this fixture rather than folding into
`RealScenePlayModeTestSupport` - it has exactly one consumer, unlike the Slice 43/45
`ResolveRuntimeGameRulesInstance` extraction, which had two.

The class doc's stale claim that this fixture "lives in a folder with no asmdef, the same reason
`Level5GameplayPlayModeTests` does" was corrected - that reasoning no longer applies once the fixture
resolves managers by runtime name instead of a compile-time reference. Two `<see cref>` references in the
header (`MenuFooterUiObjects`, `Level5GameplayPlayModeTests`) named types not reachable from
`Level5.PlayModeTests` and were changed to `<c>` tags; a new paragraph documents the runtime-name
resolution strategy and points at `Level5SceneContractTests.
EveryMenuManagerHasItsRequiredUiObjectReferencesWired` as the complementary compiler-backed coverage.

Scene load/teardown (`LoadMenuScene`, the `[UnityTearDown]` unloading only the one scene this fixture
loaded, by name) was kept unchanged rather than switched to
`RealScenePlayModeTestSupport.UnloadAllLoadedScenes` - this slice's instructions were explicit that
consistency with the other fixtures is not by itself a reason to broaden a fixture that only ever loads
one scene at a time.

Re-verified before migrating, per this slice's own instructions, that the compiler-backed side of the
contract is still intact: `Level5ProjectValidator.CollectMenuUiObjectContractErrors`
(`Assets/Level5/Editor/Level5ProjectValidator.cs:883-907`) still declares direct calls against
`OptionsManager`, `CreditsManager`, `StatsManager`, and `AccountManager` and invokes each one's real
`ValidateMenuUi`, and `Level5SceneContractTests.EveryMenuManagerHasItsRequiredUiObjectReferencesWired`
still drives it. Nothing in this slice touched that split - EditMode keeps proving each concrete manager
type's serialized-reference contract; PlayMode keeps proving the real scene load, manager presence,
enabled state, and EventSystem selection.

`Level5.PlayModeTests`'s `references` were unchanged - `Constants` (`Level5.Constants`, already
referenced) was the fixture's only external production symbol, and it was already reachable. No new
dependency was required, and no production `.cs`, runtime `.asmdef`, scene, or prefab changed.

Validation (pinned `6000.5.7f1` editor): forced script compilation via the same batchmode launch that ran
the suites below (0 `error CS` lines); the full `Level5.PlayModeTests` assembly (16/16 passed, up from
the prior 12 - the four migrated tests, `OptionsScreenLoadsAndKeepsItsManagerEnabled`,
`CreditsScreenLoadsAndKeepsItsManagerEnabled`, `StatsScreenLoadsAndKeepsItsManagerEnabled`,
`AccountHubScreenLoadsAndKeepsItsManagerEnabled`, all present and passing); the complete unfiltered
PlayMode suite (17/17 passed, unchanged - `Assembly-CSharp` now contributes only
`GameplayLevelUnpauseTests`); the complete EditMode suite (1206/1206 passed, unchanged, including
`Level5SceneContractTests.EveryMenuManagerHasItsRequiredUiObjectReferencesWired` and all eight
`Level5MenuUiObjectsTests`); and `scripts/validate-repository.ps1` (passed).

**Code review (2026-09-11) found one Low-severity polish gap, fixed:** each of the four test methods
re-resolved the loaded `Scene` with its own `SceneManager.GetSceneByName(loadedSceneName)` call right
after `LoadMenuScene` returned, duplicating the same two-line idiom four times even though
`LoadMenuScene` already knows the scene name it just loaded. `LoadMenuScene` now resolves the `Scene`
once into a `loadedScene` field (right after its `LoadSceneAsync` yield completes, before the settle
frames - the handle itself doesn't change across those frames) and the four tests read `loadedScene`
directly; `TearDown` resets both `loadedSceneName` and `loadedScene` together. No behavior or assertion
changed. Re-validated after the fix: forced compilation (0 `error CS`) and the complete unfiltered
PlayMode suite (17/17 passed, all four migrated tests green).

The asmdef-free `PlayModeGameplay` workaround shrinks from two files to one. Re-audited line by line
against current source for this slice rather than carried forward:

| Remaining file | Direct `Assembly-CSharp` blocker(s) |
| --- | --- |
| `GameplayLevelUnpauseTests.cs` | `StartManager`, `Pause` |

Re-verified unchanged from the Slice 39 audit. Slice 46 did not migrate `StartManager` or `Pause`, and
did not touch `OptionsManager`, `CreditsManager`, `StatsManager`, or `AccountManager` themselves - only
this fixture's lookup of them.

**Slice 47 (audited against `dev` at `d6d4ab7e45d7d3c94e7626cb42683649e3d764a4`, Unity `6000.5.7f1`):**
migrated `GameplayLevelUnpauseTests.cs` (GUID `395c18ff6a76e7145893d6cabddae0d1`, preserved via `git mv`)
from `Assets/Tests/PlayModeGameplay` into `Assets/Tests/PlayMode`, the folder's last remaining file.
Neither `StartManager` nor `Pause` was migrated or otherwise changed - both stay exactly where they
were, in `Assembly-CSharp`:

- `StartManager` is no longer referenced at all. The fixture already reached gameplay through
  `GameplayScenePlayModeHarness`'s real `press_start` UI path rather than `StartManager`'s private
  readiness check.
- `Pause` is resolved by exact runtime type name through a new shared helper,
  `RealScenePlayModeTestSupport.FindActiveBehaviourInScene(Scene, string)`, extracted from Slice 46's
  fixture-local `FindManagerInScene` now that this fixture is a second genuine consumer with identical
  semantics (scene-scoped, inactive GameObjects excluded, disabled `MonoBehaviour`s on active
  GameObjects still found). `Level5MenuScreenPlayModeTests`'s four smoke tests were switched to the same
  helper with no change to their own contracts.
- `startOnPause` is read via `FieldInfo` with an explicit "field exists" assertion (not a helper that
  silently returns `default(bool)`), and `StartGame()` is resolved as an exact parameterless public
  method and asserted non-null before `Invoke` - both fail loudly rather than silently if `Pause`'s
  shape ever changes, exactly as the original reflection-based assertions did before the move.

The harness could not be reused as-is: `EnterPlayableGameplayScene` deliberately disables `Pause` and
restores `Time.timeScale`, which would destroy the exact regression this fixture proves. Both public
entry points now share one private `EnterGameplayScene(bool suppressInitialPause, Action<PlayerController>)`
launch/retry state machine - identical scene load, `press_start` submission/retry/bounce handling, and
bounded realtime deadlines in both cases - with `suppressInitialPause` as the one behavioural fork: true
(`EnterPlayableGameplayScene`) suppresses `Pause` and restores `timeScale` after settling; false
(the new `EnterGameplayScenePreservingInitialPause`) does neither, handing gameplay back exactly as
production left it, paused or not. Every wait in the shared state machine was already realtime/frame-
based (`Time.realtimeSinceStartup`, `yield return null`), not scaled-time, so it needed no change to stay
correct while `Time.timeScale` may be `0`.

Compiler-backed concrete `Pause` coverage is unchanged and still in place:
`Level5ProjectValidator.CollectMenuUiObjectContractErrors` (invoked from EditMode via
`Level5SceneContractTests.EveryMenuManagerHasItsRequiredUiObjectReferencesWired`) references concrete
`Pause` directly and calls its real `ValidateMenuUi`.

`Level5.PlayModeTests.asmdef` gained one reference, `Level5.Input`, for the fixture's direct use of
`PlayerControlsProvider.Controls.Player.enabled` - the only production symbol the migrated source names
outside what the asmdef already referenced. No `Assembly-CSharp` reference was added or needed.

With this fixture gone, `Assets/Tests/PlayModeGameplay` held no more `.cs` files, so the folder and its
`.meta` were removed. A permanent guard,
`Level5ProductionAssemblyBoundaryTests.NoSourceFileReturnsToTheAsmdefFreePlayModeGameplayWorkaround`,
now fails if any `.cs` file exists anywhere under that path - reading the directory directly each run
(not a cached "did it exist at discovery time" flag) so it stays effective even if the folder is
recreated later.

No production `.cs`, runtime `.asmdef`, scene, or prefab changed.

Validation (pinned `6000.5.7f1` editor): forced script compilation (0 `error CS` lines); the full
`Level5.PlayModeTests` assembly (17/17 passed, up from 16 - `GameplayLevelUnpauseTests.GameplayLevelCanBeUnpaused`
now included, plus the four Slice 46 menu tests unchanged after the helper extraction); the complete
unfiltered PlayMode suite (17/17 passed, unchanged in total - `Assembly-CSharp` now contributes zero
PlayMode tests, `Level5.PlayModeTests` absorbed the one that moved); the complete EditMode suite
(1207/1207 passed, up from 1206 - the one new architecture guard - with zero failures); and
`scripts/validate-repository.ps1` (passed).

**Code review found one gap, fixed:** unlike every other fixture that drives a real scene through
`GameplayScenePlayModeHarness`/`RealScenePlayModeTestSupport`, the migrated fixture had no `[SetUp]`
calling `RealScenePlayModeTestSupport.IgnoreSceneLogNoise()`, so an incidental `Debug.LogError` from
production code during the real start-menu-to-gameplay load (unrelated to the pause/unpause behaviour
under test) could fail it - the same protection its six siblings already carry. This gap predated the
migration (the original pre-migration file never called it either), so it was not a regression, but
migrating the file next to its now-shared siblings was the natural point to bring it in line. Fixed by
adding the same `[SetUp]` every sibling fixture uses. Re-validated after the fix: forced compilation
(0 `error CS`), the isolated fixture (1/1 passed - `Time.timeScale=0`/`startOnPause=True` observed
before `StartGame()`, `timeScale=1` after, confirming the pause-preserving harness path end to end),
and the complete unfiltered PlayMode suite (17/17 passed).

**A second review pass found one Low-severity nit, fixed:** the new
`NoSourceFileReturnsToTheAsmdefFreePlayModeGameplayWorkaround` guard re-implemented the same "recurse
for `.cs`, exclude `~`-suffixed paths" filter that `EnumerateFilesUnder` (already in this file) exists
to provide. Switched the guard to call `EnumerateFilesUnder(new[] { workaroundFolder })` instead of
duplicating the filter inline - no behavior change, same directory-exists guard, one filter definition
instead of two. Re-validated: forced compilation (0 `error CS`); `Level5ProductionAssemblyBoundaryTests`
in isolation (29/29 passed); the complete EditMode suite (1207/1207 passed); the complete unfiltered
PlayMode suite (17/17 passed); `scripts/validate-repository.ps1` (passed).

**Phase 2c exit condition met:** the asmdef-free `Assets/Tests/PlayModeGameplay` workaround no longer
exists - zero files, folder and `.meta` removed.

**Phase 2d no-workaround invariant met:** the permanent guard above is green and active.

**Full Phase 2 exit is still not reached.** Removing the last workaround file closes the one 2d exit
item that depended on it, but the rest of Phase 2's stated exit bar was explicitly out of scope for
this slice and remains open: `player`/`basketball`/`game manager` themselves (the blocked triangle) are
still not migrated into proper assemblies - `StartManager` and `Pause` still compile into
`Assembly-CSharp`, reached here only by runtime-name reflection, not by an asmdef move - and this slice
performed no manual Play Mode verification of a representative gameplay mode or menu flow (the acceptance
criteria this task set required `Assert.That(Time.timeScale, Is.EqualTo(1f))` after the real
`StartGame()` call as its runtime evidence for the unpause path, which the passing PlayMode run
establishes; it did not require and did not add a separate manual pass). Finishing Phase 2 remains the
Phase-1-scale slice described in the original 2d exit note: inverting or cutting the
player/basketball/game-manager cycle before those three can move.

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

**Two further code-review rounds (2026-08-21)** found and fixed real gaps in the guard itself, and
one false alarm worth recording so it isn't re-litigated:

- The type-declaration scan required `public` at column zero, silently excluding every type
  declared inside a `namespace { }` block (most of this codebase's model/service types, e.g.
  `HighScoreModel`). Replaced with a brace-depth walk (`CollectTypesNotNestedInAnotherType`): a
  `public` type counts unless an *enclosing brace belongs to another type* - namespace nesting no
  longer excludes it, class/struct nesting still does.
- `EnumerateAssemblyCSharpScripts` only walked `Assets/Scripts` plus loose files directly under
  `Assets/`, missing three vendored third-party folders with no asmdef of their own
  (`Standard Assets`, `Joystick Pack`, `OmniSARTechnologies` - 50 files, genuinely part of
  Assembly-CSharp). Generalized to scan all of `Assets/` for any `.cs` file with no ancestor
  `.asmdef` and no `Editor`/`Tests` path segment, rather than hand-listing folders.
- `StripComments`/`Relative` were re-typed byte-for-byte identical in six test files (the five
  pre-existing architecture-guard tests plus this one) - extracted to a shared
  `Level5TestSourceText` so a future fix to either only has to land once.
- The Editor-only-assembly check switched from a raw substring-in-quotes regex over the whole
  `.asmdef` file to parsing it with `JsonUtility` and checking the `references` array itself -
  more precise, and the same `AsmdefInfo`/`JsonUtility` parse now backs the reference-declaration
  data other guards need.
- A speculative third guard (no production assembly reaches into a *different* production assembly
  it didn't declare) was tried and dropped: bare-identifier matching false-positived
  `GameModeCompatibility.cs`'s `public GameModeCatalog Modes => modes;` property against the
  unrelated `Modes` type in `Level5.Constants` - a property name colliding with a foreign type name.
  Nothing today needs this guard (no production assembly references another yet), so it wasn't
  worth chasing false positives to keep.
- **Rejected as a false alarm:** a review pass flagged `Level5.Misc.asmdef`/`Level5.MenuStart.asmdef`
  as missing an explicit `UnityEngine.UI` reference (for `Text`/`Button`/`Image`) and predicted a
  compile failure. Both packages' asmdefs have `"autoReferenced": true`, which - contrary to the
  review's claim - cascades to *any* consuming asmdef with `overrideReferences: false` (which all of
  this phase's asmdefs use), not only to Unity's predefined assemblies; the compiled
  `Level5.Misc.dll`/`Level5.MenuStart.dll` with zero `CS0246` errors, across every verification run
  in this phase, already proved this empirically before the claim was even checked against the
  package's own `.asmdef`.

Full EditMode 465/465 and PlayMode 9/9 both passed after every round; `validate-repository.ps1`
passed throughout.

**2d exit, checked against current state:** intended runtime source (the 10 slices) does not fall
back into `Assembly-CSharp` - guarded, green. No forbidden cycle among production assemblies -
trivially true today (none of the 10 new leaves reference each other, only `Level5.Core`/packages).
Runtime assemblies don't reference a known Editor-only assembly - guarded, green. New assemblies
declare required references explicitly - true by construction (`Level5.Utility` → `Level5.Core`,
`Level5.Input` → `Unity.InputSystem`, the rest need none). At the time this section was originally
written, the one remaining item was: the asmdef-free gameplay PlayMode workaround is still present
(2c, above) - this is the one 2d exit item that depends on cutting the player/basketball/game-manager
cycle, which 2b0 correctly keeps out of scope here.

**Full Phase 2 exit was therefore not reached in that pass**, and that was the expected outcome given
2b0's gate, not a shortfall: 14 production runtime assemblies existed (10 new + 4 pre-existing),
the migrated portion of the graph was acyclic and guarded against regrowth, and everything not moved
was either inside the blocked triangle or reached into it. Finishing Phase 2 - removing the
`Level5GameplayPlayModeTests` workaround and migrating `player`/`basketball`/`game manager` themselves
- requires first inverting or cutting that remaining cycle, which is a Phase-1-scale slice of its own
(see Phase 1's own slicing for the shape that work took), not a continuation of leaf-picking.

**Update (Slice 47):** the asmdef-free `Assets/Tests/PlayModeGameplay` workaround folder itself is now
gone (0 files, folder and `.meta` removed), and the permanent
`NoSourceFileReturnsToTheAsmdefFreePlayModeGameplayWorkaround` guard prevents its return - this closes
the specific 2d item called out above. It was closed without migrating `StartManager`/`Pause` or
cutting the player/basketball/game-manager cycle: the fixture that needed them now resolves both by
runtime-name reflection through shared PlayMode test helpers instead of a compile-time reference, the
same pattern already used for the four menu managers. **Full Phase 2 exit is still not reached**:
`StartManager`, `Pause`, and the rest of the player/basketball/game-manager triangle still compile into
`Assembly-CSharp`, not into proper referenced assemblies; and no manual Play Mode verification of a
representative gameplay mode or menu flow has been performed as part of this documentation update -
only the automated EditMode/PlayMode evidence recorded under Slice 47, above. Migrating that triangle
remains the Phase-1-scale slice described in the paragraph above.

**Slice 48** (2026-09-11, audited against `dev` SHA `9dd00656a90d423bf6af5117384c46763ec3f81e`, Unity
`6000.5.7f1`): moved the character preset/progression cluster - `CharacterPreset.cs` (GUID
`72fa827508c54a4bbd0c231df454eda0`), `CharacterPresetCatalog.cs` (GUID `4aea7e1d05f5481da1f30f4acadb7699`),
`CharacterUpgradeLevels.cs` (GUID `b39c0091eac646ff9ff11f27d1361fb2`), `PlayerCharacterProgress.cs` (GUID
`5d0b133f61b64954917bc976c485bb26`), `CharacterProgressSave.cs` (GUID `83be264430424e7b8a06f004f363755e`),
`CharacterProgressResolver.cs` (GUID `efac78fcb54e43f3bf99eda8b7a11d8f`), and `CharacterProgressStore.cs`
(GUID `2d85358806364d19b97516f781e7cafa`) - from `Assets/Scripts/player/` into
`Assets/Scripts/player/Level5Player/` via `git mv` (source + `.meta` together), all seven GUIDs
preserved and production source byte-identical (pure `rename ... (100%)`, zero insertions/deletions
outside the added test below).

All seven were already dependency-closed: their only project-type dependencies are `CharacterStats`/
`RuntimeCharacterStats` (`Level5.Player` since Slice 28) and `AtomicFile` (`Level5.Utility`), both
already legal references. `Level5.Player.asmdef` needed no change - `Level5.Utility` was already
referenced. `CharacterRuntimeProvider` and `CharacterProgressAccountId` stay in `Assembly-CSharp`; the
`CharacterRuntimeProvider -> CharacterProgressAccountId -> GameOptions` edge is unchanged and remains
the next boundary. Both reach the moved cluster through `autoReferenced`, the same pattern as every
prior 2b leaf.

No `[SerializeReference]`, `Type.GetType`, `AssemblyQualifiedName`, `Assembly.Load`, or
`TypeNameHandling` usage touches any of the seven types - `CharacterProgressSave`/
`PlayerCharacterProgress`/`CharacterUpgradeLevels` round-trip through plain `JsonUtility` field
serialization, which carries no assembly-qualified type name. `CharacterPreset`/`CharacterPresetCatalog`
are authored `ScriptableObject`s; their script GUIDs are unchanged, so existing asset references
resolve without modification - no prefab, scene, or `ScriptableObject` asset was opened or resaved.

Added one cohesive assembly-boundary test, `CharacterPresetProgressionTypesCompileIntoLevel5Player`
(`Level5ProductionAssemblyBoundaryTests.cs`), asserting all seven types report assembly
`Level5.Player`, the same identity-check pattern as Slice 28's
`CharacterProfileOwnershipTypesCompileIntoLevel5Player`. The existing generic
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` guard covers the new files automatically - it
discovers asmdef-owned folders at run time rather than by an allowlist.

Validation (pinned `6000.5.7f1` editor): forced script compilation (0 `error CS` lines, ~10s compile,
clean domain reload); the complete EditMode suite (1208/1208 passed, up from 1207 - the one new
architecture guard, zero failures); the complete PlayMode suite (17/17 passed, unchanged);
`Level5UnlockSnapshotTests` (10/10 passed - the existing `CharacterProgressStore`/JSON-fallback
precedence regression coverage, unaffected by the pure relocation); and
`scripts/validate-repository.ps1` (passed). No production `.cs` content, runtime `.asmdef`, scene, or
prefab changed other than the file relocation itself.

**Exit:** production gameplay code targeted by this phase lives in proper referenced assemblies; the
runtime assembly graph is acyclic; no migrated assembly depends on `Assembly-CSharp`; package
references are explicit where Unity requires them; full repository validation, Unity batch
compilation, and the full EditMode/PlayMode suites pass; the asmdef-free gameplay PlayMode workaround
is gone; one representative gameplay mode and one representative menu flow pass manual Play Mode
verification; gameplay and visuals are observably unchanged. **Not yet met:** `player`/`basketball`/
`game manager` still compile into `Assembly-CSharp`, and the manual Play Mode verification bullet
above has not been performed.

**Slice 49** (2026-09-12, audited against `dev` SHA `f2ff853d822c80b20c23a5b73c8fbc92c914e607`, Unity
`6000.5.7f1`): moved `PlayerAttackBox.cs` (GUID `74541ade30e1941489c89cbff972346f`) from
`Assets/Scripts/player/` into `Assets/Scripts/player/Level5Player/` via `git mv` (source + `.meta`
together) - pure `rename ... (100%)`, zero insertions/deletions, GUID and all four public fields
(`attackDamage`, `knockDownAttack`, `disintegrateAttack`, `isProjectile`) unchanged.

Already dependency-closed: its only production dependency is `UnityEngine`. Its three live
consumers - `PlayerCollisions`, `AutoPlayerCollisions`, and `EnemyCollisions` - are all loose,
non-asmdef-owned files that stay in `Assembly-CSharp` and reach it through `autoReferenced`, the
same pattern as every prior 2b leaf; `EnemyCollisions` in particular sits outside the
`Level5.Enemy` asmdef's folder (`Assets/Scripts/enemy/Level5Enemy/`), so no custom-assembly
consumer exists and no cycle risk was introduced. `Level5.Player.asmdef` needed no change.

`PlayerAttackBox`'s script GUID is referenced by 84 prefabs, 2 scenes, and 77 animation clips
(dunk/attack triggers across the player roster) - none were opened, resaved, or otherwise touched;
`git status` shows only the two renamed files plus the added test below, so every GUID-based
binding resolves exactly as before the move by construction. Spot-checked one binding
(`ashley_dunk.anim`) directly: it keys on `script: {fileID: 11500000, guid:
74541ade30e1941489c89cbff972346f, type: 3}` plus `path`/`attribute`, i.e. purely GUID-keyed, not
path-keyed - confirming the move cannot disturb it.

Added one assembly-boundary test, `PlayerAttackBoxCompilesIntoLevel5Player`
(`Level5ProductionAssemblyBoundaryTests.cs`), asserting `typeof(PlayerAttackBox).Assembly.GetName().Name
== "Level5.Player"`, the same identity-check pattern as Slice 48's
`CharacterPresetProgressionTypesCompileIntoLevel5Player`. The existing generic
`NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` guard covers the moved file automatically.

Validation (pinned `6000.5.7f1` editor): forced script compilation via the EditMode run below (0
`error CS` lines in the batch log); the complete EditMode suite (1209/1209 passed, up from 1208 -
the one new architecture guard, zero failures); the complete PlayMode suite (17/17 passed,
unchanged); `scripts/validate-repository.ps1` (passed). No production `.cs` content, runtime
`.asmdef`, scene, or prefab changed other than the file relocation and the one added test.

Review pass 1 (correctness/serialization) found nothing to fix: the diff is a pure rename with 0
insertions/0 deletions on both moved files, the GUID is unchanged, no field was renamed or
retyped, and no consumer or asmdef file was touched. Review pass 2 (architecture/scope) likewise
found nothing to fix: the type landed in `Level5.Player` (not a new assembly), no dependency or
reference was added, no consumer was migrated, and no adjacent type was bundled into the move.

**Slice 50** (2026-09-12, audited against `dev` SHA `72e4b0ea8896a00877e522d4beba2f46b3f39cc4`, Unity
`6000.5.7f1`): a single coordinated migration of the remaining 15 named player-domain targets - the
account/character-runtime pair, the CPU actor/defense cluster, the participant/ground/collision
cluster and the presentation/animation cluster - plus one supporting move (`PlayerRegistry`), into
`Level5.Player`. Moved via `git mv` (source + `.meta` together), all GUIDs unchanged:

| Target | GUID |
| --- | --- |
| `CharacterProgressAccountId.cs` | `e5b827b24d2641c19ac1024cc764bfbc` |
| `CharacterRuntimeProvider.cs` | `9dd1fe5ec4484b4f9480093d2c6b3cb4` |
| `AutoPlayerController.cs` | `564c1d9e7ac5c74469072e5500514ac1` |
| `AutoPlayerDamageReactions.cs` | `7fdf0255acb5323448a622abe0669c6a` |
| `AutoPlayerDefense.cs` | `306ca6386a7ffbe4db12499141da79e2` |
| `CollisionCheckDefense.cs` | `114d4a7d46a7d04419a8bbcdf4efaaca` |
| `GroundCheckDefense.cs` | `f564370075e576241a8c48eccf9a0abf` |
| `CpuScoreDeficit.cs` | `2c58b541827f4b358f7ed49ea5405468` |
| `PlayerIdentifier.cs` | `3f595a79238fe4f439aeac86720f9e96` |
| `groundcheck.cs` (`GroundCheck`) | `48af77e4d1c73a94da342786c291ce99` |
| `PlayerCollisions.cs` | `3bd0b015c94f2d249b3d34709a0072f3` |
| `AutoPlayerCollisions.cs` | `6afe4709f3993a5458ba631dde388782` |
| `PlayerAnimationEvents.cs` | `589c6078b9c899444b9a02b93b19a872` |
| `CheerleaderSwapAnimation.cs` | `e77b77da16f56d04899af7a0b8d79368` |
| `PlayerHealthBar.cs` | `ccf286a28b6543140a29fa889eb5382d` |
| `PlayerRegistry.cs` (supporting; from `Assets/Scripts/game manager/`) | `a0603ce0d56569d4eb9f38e4347211f5` |

`CharacterRuntimeProvider.cs`, `CollisionCheckDefense.cs`, `GroundCheckDefense.cs`,
`CpuScoreDeficit.cs`, `PlayerIdentifier.cs`, `groundcheck.cs`, `AutoPlayerDamageReactions.cs` and
`PlayerRegistry.cs` were already dependency-closed (confirmed by re-reading each fully before
moving) and moved source-identically. The other 7 required a dependency cut first:

- **`CharacterProgressAccountId`** read `GameOptions.userid`/`userName` directly. Extracted the
  backing state into `Level5.Core.LocalAccountIdentity` (`UserId`/`UserName`, a plain local-account
  identity holder distinct from authentication - see `APIHelper.HasSession` for that test);
  `GameOptions.userid`/`userName` are now properties forwarding to it, so every existing caller
  (login, account switching, high-score upload) is unaffected. Removed from
  `Level5MatchArchitectureTests`'s `LegacyGameOptionsConsumers` allowlist - one fewer file on that
  ratchet.
- **`AutoPlayerController`** (the central dependency) took: `IPlayerMatchRuntime` via
  `BindMatchRuntime` (the same contract `PlayerController` already uses; extended with one new
  member, `LevelHasSevenPointers`, for `cpuShootSevenpointers`'s former `MatchRuntime` read -
  `LiveMatchRuntimeAdapter` and the `FakeMatchRuntime` test double were updated to implement it);
  arena context (finalized rim vector, `IGroundHeightProvider`) via `BindArenaContext`, mirroring
  `PlayerController.BindArenaContext` exactly, including the drop-shadow "log once if unbound"
  fallback; and the moved `PlayerRegistry` via `BindParticipantRegistry`, replacing
  `GameLevelManager.instance.players` in `SelectShotKind`'s score-deficit read.
- **`AutoPlayerDefense`** took the same arena context and `PlayerRegistry` bindings -
  `ResolveGuardedPlayerIfMissing`'s fallback now reads `participantRegistry.GetBySlot(0)` (the exact
  `Player1` it replaces) instead of `GameLevelManager.instance.Player1`. One dead local
  (`directionOfTravel` in `moveCpuPlayer`, computed and never read) was deleted rather than rebound,
  since it had zero behavioral effect.
- **`PlayerCollisions`/`AutoPlayerCollisions`** took `ResolvedMatchRules` via `BindMatchRules`
  (the same bind-once shape `CallBallToPlayer`/`PlayerHealth` already use); the human spawn point via
  `BindFallRespawnDestination` (from `SpawnCoordinator`'s own `locations.Player1`); and, human-only,
  the `GameRules.killedOnIdle` write via a one-purpose `Action` delegate
  (`BindKilledOnIdleCallback`) rather than an `IGameRules` interface. The fall-respawn teleport and
  `PlayerCollisions`'s dunk-sequence trigger both used to hard-select `GameLevelManager.instance.Player1`
  regardless of which actor's hitbox fired; both now use the component's own resolved
  `playerIdentifier`/`playerController`, per the architecture rules' explicit instruction for this
  exact shape ("already-owned local reference instead of globally selecting player 1"). Attack-box
  metadata (`attackDamage`, `knockDownAttack`, `disintegrateAttack`, `isRake`, `isKilledOnIdle`) is
  now read through a new neutral contract, `IAttackBoxHitInfo` (`Level5.Combat`), implemented
  explicitly by both `PlayerAttackBox` (`Level5.Player`) and `EnemyAttackBox` (`Level5.Enemy`,
  which gained a reference to the `Level5.Combat` leaf to do so) - avoiding a Player↔Enemy assembly
  cycle without an enemy-specific abstraction on the player side.
- **`PlayerHealthBar`** took the primary human's `PlayerHealth`, character display name and
  `ResolvedMatchRules`, plus a live `Func<Text>` damage-display-text reader, via one
  `BindPrimaryHumanContext` call from a new `GameLevelManager.BindPlayerHealthBarContext` (called
  from `GameLevelManager.Awake()` - see the independent review follow-up below for why this moved
  off `Start()` - this HUD is a scene-authored UI singleton, not a per-participant component, so it
  is not spawned or bound through `SpawnCoordinator`). The reader stays a live `Func`, not a
  captured `Text`, because
  `PlayerController.DamageDisplayValueText` is only populated inside that controller's own
  `Start()`, whose ordering relative to this bind is not guaranteed.
- **`PlayerAnimationEvents`** took a projectile-spawn delegate (`BindProjectileSpawner`,
  `Func<GameObject, Vector3, Quaternion, GameObject>`) and a live "does this scene have an auto
  player" reader (`BindHasAutoPlayerReader`, `Func<bool>`), both bound from a new
  `SpawnCoordinator.BindPlayerAnimationEventsContext`, called from `RegisterHuman`, `RegisterCpu`
  *and* `SpawnCheerleader` - every actor this component is authored on. `ProjectilePool` lives in
  loose `Assets/Scripts/projectile/` (`Assembly-CSharp`, not `Level5.Pooling`), so unlike `SFXBB`
  (a genuine `Level5.Audio` leaf reference, kept direct) it is not a legal dependency and needed the
  delegate instead. `enableRigidBodyIsKinematic`/`disableRigidBodyIsKinematic` and the dead
  `CheckAttackBoxActiveStatus` used to hard-select `GameLevelManager.instance.Player1` regardless of
  which actor's animation fired; both now use this component's own resolved `playerController`,
  guarded by the existing `CanApplyForce()` null-safety this file already used for force application.
- **`CheerleaderSwapAnimation`** read `GameLevelManager.instance.Controls.Other.change.enabled` for
  its dev/editor animation-swap toggle - `GameLevelManager.Controls` was only ever a forward of
  `PlayerControlsProvider.Controls` (`Level5.Input`, already an authorized `Level5.Player`
  reference). Added `PlayerControlsProvider.DevChangeControlEnabled`, the same narrow
  bool-over-`InputAction` shape as the existing `MenuSubmitTriggered`, so this file needs no direct
  reference to the `Unity.InputSystem` package assembly.

**Reused contracts:** `IPlayerMatchRuntime`, `IGroundHeightProvider` (both extended, not replaced -
see above), `PlayerRegistry` (moved, not duplicated), `ResolvedMatchRules` bind-once. **New narrow
seams:** `Level5.Core.LocalAccountIdentity`; `IAttackBoxHitInfo` (`Level5.Combat`);
`GameLevelManager.BindPlayerHealthBarContext`; `PlayerControlsProvider.DevChangeControlEnabled`;
five new `SpawnCoordinator` bind passes (`BindCpuMatchRuntime`, `BindCpuArenaContext`,
`BindCpuParticipantRegistry`, `BindPlayerCollisionsContext`/`BindAutoPlayerCollisionsContext`,
`BindPlayerAnimationEventsContext`). No manager mirror, service locator, or reflection was
introduced; `GameLevelManager`/`GameRules`/`GameOptions` were not moved.

`Level5.Player.asmdef` gained one new reference, `Level5.Audio` (a leaf with no references of its
own, for `PlayerAnimationEvents`'s/`PlayerCollisions`'s/`AutoPlayerCollisions`'s existing `SFXBB`
calls - a genuine direct leaf dependency, not a new audio service). `Level5.Enemy.asmdef` gained one
new reference, `Level5.Combat` (also a leaf), for `EnemyAttackBox`'s `IAttackBoxHitInfo`
implementation. No other asmdef changed.

Every target GUID was diffed before/after (table above) and confirmed unchanged; `git status` shows
only the expected renames plus in-place edits, no unrelated scene/prefab/animation touched.

Added `RemainingPlayerDomainTargetsCompileIntoLevel5Player`, `PlayerRegistryCompilesIntoLevel5Player`
and `AttackBoxHitInfoCompilesIntoLevel5Combat` (`Level5ProductionAssemblyBoundaryTests.cs`), the same
identity-check pattern as Slice 49. Added a new fixture,
`Level5PlayerDomainDependencyGuardTests.cs`, with one regression guard per file for the specific
type(s) each cut removed (`GameLevelManager`/`GameRules`/`MatchRuntime`/`ProjectilePool`/
`EnemyAttackBox`), mirroring `Level5PlayerControllerDependencyGuardTests`'s shape. The existing
generic `NoMigratedProductionAssemblyReachesIntoAssemblyCSharp` guard covers all 16 moved files
automatically. `LiveMatchRuntimeAdapter` and `Level5PlayerMatchRuntimeCompositionTests`'s
`FakeMatchRuntime` were updated for the new `IPlayerMatchRuntime.LevelHasSevenPointers` member.

Validation (pinned `6000.5.7f1` editor): forced script compilation via Unity batchmode (0 `error CS`
lines after two small follow-up fixes - a missing `using Level5.Core;` in `AutoPlayerDefense.cs`, a
leftover pre-rename `enemyAttackBox`/`playerAttackBox` local in each collision file's blocking
branch, and the `InputAction` compile error that led to `DevChangeControlEnabled`); the complete
EditMode suite (1219/1219 passed, up from 1208 - the new architecture/dependency guards, after fixing
one pre-existing stale-allowlist failure this slice's own `GameOptions` cut earned
- `CharacterProgressAccountId.cs` removed from `Level5MatchArchitectureTests`'s
`LegacyGameOptionsConsumers`); the complete PlayMode suite (see below).

Review pass 1 (correctness/serialization): confirmed no live behavior changed except the two
deliberate, dependency-cut-required fixes called out above (fall-respawn/dunk-sequence/rigidbody-
kinematic/attack-box-status now resolve through the acting participant's own reference instead of a
hard-coded `Player1`/global lookup) - both are what the architecture rules explicitly prescribe for
this exact "manager reach-through selecting player 1" shape, not incidental scope creep; every other
binding is a byte-for-byte forward of the value the direct read used to return, at an equivalent or
identical point in the composition/frame timeline. No GUID drifted, no serialized field was renamed
or retyped, and no prefab/scene/animation was touched. Review pass 2 (architecture/scope): no manager
mirror or service locator was introduced; `IPlayerMatchRuntime`/`IGroundHeightProvider` were extended
rather than duplicated; `PlayerRegistry`'s move eliminated what would otherwise have been two
separate ad hoc "reach the primary human" seams (`AutoPlayerController` and `AutoPlayerDefense`);
`IAttackBoxHitInfo` exposes only the five fields collision processing actually reads, not a general
attack-box interface; and `Level5.Core.LocalAccountIdentity` holds only the two fields
`CharacterProgressAccountId` needs, not a broader account/session model - `GameOptions.userid`/
`userName` were converted to forwarding properties, not moved wholesale, and `APIHelper`'s session
token was untouched.

**Independent review follow-up (2026-09-12):** a subsequent senior-level code review of this slice
found two defects the self-review above missed, both fixed before merge:

- **`PlayerHealthBar` activation raced `Start()` ordering.** `GameLevelManager.BindPlayerHealthBarContext`
  was originally called from `Start()`, but `PlayerHealthBar.Start()` consumes the bound context
  *synchronously* to decide whether to activate itself - and Unity does not order `Start()` across
  independent components (this project defines no `ScriptExecutionOrder.asset`). If
  `PlayerHealthBar.Start()` happened to run before `GameLevelManager`'s, it saw an unbound context,
  deactivated its own GameObject, and the subsequent `FindAnyObjectByType<PlayerHealthBar>()`
  (active-only) found nothing - silently and permanently disabling the HUD for the match, with no
  error logged. Fixed by moving the bind into `GameLevelManager.Awake()` (guaranteed to precede every
  `Start()` in the scene), reading `registry.GetBySlot(0)` directly instead of the `Start()`-scoped
  `player1` local. Verified safe: `PlayerHealth.Awake()` already sets `Health`/`Block`/`Special`, and
  a human's `CharacterProfile.playerDisplayName` is either the prefab's serialized default or set
  synchronously by `SpawnCoordinator.RegisterHuman` during the same `Awake()` - never by
  `CharacterProfile.Start()` - so nothing this bind reads is Start()-timed.
- **`PlayerCollisions`/`AutoPlayerCollisions.OnTriggerEnter` dereferenced bound `matchRules` with no
  null guard**, unlike the established `PlayerHealth`/`CallBallToPlayer` convention this slice cites
  as its template. The direct `MatchRuntime.Rules` read this replaced could never be null; an unbound
  `matchRules` (a skipped composition step, or a test constructing the component directly) now throws
  in a physics trigger callback instead. Fixed by adding `matchRules != null` to both gated conditions
  in both files, matching the fail-closed shape `PlayerHealth.Update()` already uses.

A third, lower-severity finding - `AutoPlayerDefense.ResolveDropShadowHeight`'s unbound fallback
returning a hardcoded `0f` instead of the last resolved height, unlike its `AutoPlayerController`/
`PlayerController` siblings - was also fixed, caching the last resolved value in
`lastResolvedDropShadowHeight`. All three fixes were re-validated against the full EditMode
(1219/1219) and PlayMode (17/17) suites plus `scripts/validate-repository.ps1`, all passing.

Final source-state certification: all 15 named targets, plus `PlayerRegistry`, are gone from the
loose `Assets/Scripts/player/` folder and report assembly `Level5.Player`; exactly one source
definition exists per target; the final `Level5.Player.asmdef` references
`Level5.Core`/`Level5.Combat`/`Level5.Constants`/`Level5.Basketball`/`Level5.Utility`/
`Level5.Input`/`Level5.Audio` - all leaves or already-legal siblings, no cycle. Remaining loose files
directly under `Assets/Scripts/player/`: `PlayerStats.cs` and `SelectedLoadout.cs` only - the two
non-target trivial leaves this migration was explicitly not required to fold in.

**Production behavior impact:** limited to the two call sites named above (fall-respawn/dunk-sequence
in `PlayerCollisions`/`AutoPlayerCollisions`, and rigidbody-kinematic/attack-box-status in
`PlayerAnimationEvents`), which now act on the participant whose own hitbox/animation actually fired
rather than always `GameLevelManager.instance.Player1` - a fix required by, and inseparable from,
removing the illegal `GameLevelManager` reach-through, and explicitly the shape the architecture
rules prescribe for it. Every other change is compile-time assembly ownership and composition-timing
only; no other runtime logic, serialized data, or public contract changed.

**Remaining AUD-012 blockers:** `player`/`basketball`/`game manager` folders still compile into
`Assembly-CSharp` outside `Level5Player`/`Level5Match`/`Level5Basketball` subfolders (e.g.
`GameLevelManager`, `GameRules`, `MatchRuntime`, `SpawnCoordinator`, `BasketBall`'s non-`Level5.Basketball`
siblings if any remain); the manual Play Mode verification bullet from Slice 2's exit criteria has
still not been performed for this slice.

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
