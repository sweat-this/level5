# Deep Audit - 2026-08-06

Scope: full runtime pass over `Assets/Scripts` (~42k lines, 245 scripts), concentrating on areas the
existing [Architecture Audit](architecture-audit.md) register left thin: progression/XP math, stat
ownership across multiple players, AI instance state, persistence and networking, and lifecycle
hygiene.

Every finding below was traced to specific lines and cross-checked against its callers. Nothing here
duplicates an existing AUD entry, though several sharpen a previously generic one (noted per finding).
IDs continue the main register's sequence.

**AUD-022 to AUD-033 were fixed on 2026-08-06.** Code changes are described per finding under
"Fix applied". The fixes have not been compiled or playtested in Unity - see
[Verification Status](#verification-status).

**AUD-034 to AUD-037 were fixed the same day.** They came out of a second pass run after the first
batch landed, which re-reviewed the changes and opened the files the first pass never got to.

## Summary

| ID | Area | Severity | Status | Problem |
| --- | --- | --- | --- | --- |
| AUD-022 | Progression/XP | High | Fixed | Sniper evasion XP bonus treats a 0-100 percentage as a 0-1 fraction; every match awards a flat +500 base XP and the intended graded bonus is unreachable. |
| AUD-023 | AI state | High | Fixed | `currentState` is `static` in four behaviour controllers, so all instances of a type share one animator-state variable. |
| AUD-024 | Stat ownership | High | Fixed | `BasketBall.updateScoreText()` rebinds its own `gameStats` field to player 1's stats; combined with an unguarded `instance = this`, enabling the UI-stats overlay redirects a second human player's shot recording to player 1. |
| AUD-025 | Stat ownership | Medium | Fixed | Enemy kill counts are credited to `BasketBall.instance.GameStats` (last basketball to run `Start`), not the killing player. |
| AUD-026 | Randomness | Medium | Fixed | Percentage-roll helpers are off-by-one and mutually inconsistent; `Random.Range` int overloads make some rolls impossible and some certain. |
| AUD-027 | Lifecycle | Medium | Fixed | `Time.timeScale` is never restored when leaving a paused gameplay scene; the only reset helper has zero callers. |
| AUD-028 | Scene contracts | Medium | Fixed | `GameRules.Start` and `Pause.Awake` chain ~15 unchecked `GameObject.Find(...).GetComponent<T>()` calls; one renamed HUD object throws mid-initialization. |
| AUD-029 | Dead code | Low | Fixed | `BodyGuardHealthBar.instance` is declared as type `PlayerHealthBar`, never assigned, never read. |
| AUD-030 | Docs/intent | Low | Fixed | `getReleaseModifier`'s comment states the inverse of what the code does. |
| AUD-031 | Null safety | Low | Fixed | `EnemyCollisions` dereferences `enemyController`, `transform.parent`, and `BasketBall.instance` without null checks. |
| AUD-032 | Build/test | Low | Fixed | 8 automated tests total for ~42k lines; all of the math bugs above are pure functions that unit tests would have caught. |
| AUD-033 | Legacy separation | Low | Fixed | `StartManager_original.cs` (1683 lines) still compiles into the shipping assembly. |
| AUD-034 | Match timing | Medium | Fixed | `Timer` and `GameRules` both initialize `timeStart` with different formulas, in an undefined `Start()` order. |
| AUD-035 | Null safety | Low | Fixed | `BodyGuardCollisions` retains the three unguarded dereferences that AUD-031 fixed in its `EnemyCollisions` twin. |
| AUD-036 | Progression | Low | Fixed | Experience-per-level (`3000`) is hardcoded in eight places plus a ninth derivation. |
| AUD-037 | Randomness/UI | Low | Fixed | `getCriticalPercentage` guards `CriticalRolled` but divides by `ShotAttempt`. |
| AUD-038 | Input lifecycle | Low | Fixed | Queued touch inputs are not cleared on scene exit, so a tap can carry into the next scene. |
| AUD-039 | Spawn contract | Low | Fixed | `EnemyHealth.ResetForSpawn` silently skips everything when no `EnemyController` is found, leaving the enemy invincible. |

AUD-022 to AUD-033 came from the first pass; AUD-034 to AUD-037 from the
[second pass](#second-pass---2026-08-06-post-fix) run after those fixes landed. All sixteen are now
fixed in code and pending Unity compile/playtest verification. A third pass added AUD-038 and
AUD-039, also fixed. All eighteen await Unity verification.

## Verification Status

What was and was not checked:

- **Verified**: `scripts/validate-repository.ps1` passes (including a deliberately planted
  missing-`.meta` probe, to confirm the new `~`-folder exclusion did not disable the check).
  All XP arithmetic asserted in the new tests was computed independently and matches, including
  Unity's banker's rounding in `Mathf.RoundToInt`.
- **Not verified**: nothing here has been compiled by Unity or playtested. The edit-mode tests are
  written but have not been run. Compile and run `Level5CombatMathTests` and
  `Level5SceneContractTests` before trusting any of it.
- **Expect `Level5SceneContractTests` to fail on the first run** if any gameplay scene is genuinely
  missing a HUD or pause-menu object. That is the test doing its job, not a broken test - the
  failure message names the scene and the object.

### Behaviour changes to be aware of

Three fixes change gameplay numbers rather than just preventing a crash:

1. **Players earn roughly 500 less XP per match** (more after multipliers - about 900 in a
   hardcore + enemies run). That flat bonus was the AUD-022 bug paying out in every mode, including
   modes with no sniper. XP already banked is unaffected; this only changes future awards. If the
   progression curve was tuned while that bonus was silently in place, it will now feel slower and
   may want rebalancing - that is a design call, not something this fix should decide.
2. **Extreme stat values now behave as written**: a 0 stat never triggers and a 100 stat always
   does. Previously a 0 luck still dodged ~1% of the time and a 99 stat was a guaranteed success.
   Enemies with `luck` unset no longer dodge at all.
3. **CPU clutch threshold uses float division** (`Clutch / 2f` instead of `Clutch / 2`), so odd
   Clutch values no longer round down - a Clutch of 5 is now 2.5% rather than 2%.

## Finding Details

### AUD-022: Sniper evasion XP bonus uses a percentage as a fraction (High)

`Assets/Scripts/basketball/GameStats.cs:113-124`

```csharp
float inverseSniperAccuracy = (1 - UtilityFunctions.getPercentageFloat(SniperHits, SniperShots));
if (inverseSniperAccuracy > 0)
{
    experience += Mathf.RoundToInt(500 * inverseSniperAccuracy);
}
```

`UtilityFunctions.getPercentageFloat` (`Assets/Scripts/Utility/UtilityFunctions.cs:46-57`) returns
`made / attempt * 100` - a value in 0-100, not 0-1. The comment above the block ("if sniper accuracy =
30%, return 70%") describes the intended 0-1 scaling that the helper does not provide.

Consequences, in order of impact:

1. **Sniper disabled (the common case).** `SniperShots == 0`, so `getPercentageFloat` returns 0,
   `inverseSniperAccuracy` becomes 1, and the player receives the full +500 bonus. Every match in every
   mode - including modes with no sniper at all - gets a flat +500 XP for evading a sniper that never
   fired. This lands before the traffic/enemies/hardcore/sniper multipliers, so in a hardcore + enemies
   run it compounds to roughly +900.
2. **Sniper enabled, one or more hits.** Accuracy is at least ~1, so `1 - accuracy` is `<= 0` and the
   bonus is skipped entirely. The graded "lower accuracy, higher bonus" reward is unreachable.
3. **Sniper enabled, zero hits.** Returns 0, so `inverseSniperAccuracy` is 1 and the bonus is +500.

The net behaviour is binary (500 or 0) and inverted relative to intent, and it fires in modes it was
never meant to apply to. `getExperienceGainedFromSession()` is called for every match end via
`GameRules.ApplyMatchProgressionResult` (`Assets/Scripts/game manager/GameRules.cs:541-544`), so this
is on the live progression path, not a diagnostic.

Fix: divide by 100, or add a `SniperShots > 0` guard plus a fraction-returning helper. Decide
explicitly whether the bonus should apply when the sniper is disabled - "no sniper" and "perfect
evasion" are currently indistinguishable to this code.

**Fix applied.** The award moved to `Assets/Level5/Core/MatchExperience.cs` as a pure function over
a `MatchExperienceInput` struct; `GameStats.BuildExperienceInput()` now only reads stats and mode
flags into that struct. `MatchExperience.SniperEvasionBonus` returns 0 when `sniperShots == 0` and
otherwise scales the bonus by the fraction of shots dodged, clamping hits to shots. Every other term
and multiplier is byte-for-byte the original order and rounding. Covered by
`Level5CombatMathTests`.

### AUD-023: `currentState` is static across all instances of four AI controllers (High)

- `Assets/Scripts/bodyguard/BodyGuardController.cs:52`
- `Assets/Scripts/character_specific/BehaviorNpcAutonomous.cs:30`
- `Assets/Scripts/character_specific/BehaviorPrimo.cs:30`
- `Assets/Scripts/character_specific/BehaviorVehicleLawnmower.cs:32`

Each declares `static int currentState;` and writes it every frame from its own animator:

```csharp
currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
currentState = currentStateInfo.fullPathHash;   // BodyGuardController.cs:158-159
```

The sibling `AnimatorState_*` hash fields are legitimately static (they are constants). `currentState`
is not - it is per-instance animator state that was almost certainly made static by copy-paste from
the hash fields directly above it.

With two or more instances of the same type active, whichever one ran `Update()` last owns the value
that every instance then reads. Concrete consequences in `BodyGuardController`:

- `FixedUpdate` line 140 gates `pursuePlayer()` on `currentState != Knockdown/Disintegrated` - a
  knocked-down bodyguard keeps pursuing if another bodyguard is walking.
- Line 174 gates the idle transition on `currentState != Attack`.
- Lines 214-215 gate movement on knockdown/disintegrated state.
- `struckByLighning()` line 348 does `yield return new WaitUntil(() => currentState == AnimatorState_Lightning)`,
  and `knockedDown()` line 368 waits for the inverse. Both coroutines can be released by a *different*
  bodyguard's animator entering or leaving the lightning state, so the knockdown sequence can advance
  before this bodyguard's animation has actually started.

Fix is a one-word change per file (drop `static`), but each should be spot-checked in a scene with
multiple instances since some behaviour may have been tuned around the shared value. Make the
`AnimatorState_*` fields `static readonly` while there.

This is the concrete, actionable core of the generic AUD-007.

**Fix applied.** `static` dropped from `currentState` in all four controllers; the `AnimatorState_*`
/ `idleState` / `walkState` hash fields became `static readonly`, which is what they always were in
spirit. No static method referenced the field, so this was a keyword change with no call-site churn.
**Playtest a scene with two or more bodyguards** - some behaviour may have been tuned around the
shared value.

### AUD-024: `updateScoreText()` permanently rebinds a basketball's stats to player 1 (High)

`Assets/Scripts/basketball/BasketBall.cs:652-654`

```csharp
public void updateScoreText()
{
    gameStats = GameLevelManager.instance.players[0].gameStats;
    scoreText.text = ...
```

This is a display method that mutates the component's `gameStats` field. `gameStats` is the same field
used by the shot-recording path - `gameStats.ThreePointerAttempts++`, `gameStats.ShotAttempt++`, and
so on at lines 345-364 - and it is initialized in `Start()` from `GetComponent<GameStats>()` (line 52),
i.e. this basketball's own stats.

Every `BasketBall` runs `InvokeRepeating("displayUiStats", 0, 0.5f)` (line 97), which calls
`updateScoreText()` whenever `UiStatsEnabled` is on. So the first time the UI-stats overlay is toggled
on, *every* human basketball in the scene rebinds its `gameStats` to `players[0]`'s - permanently, since
nothing ever rebinds it back. From that point, a second human player's attempts and makes are recorded
into player 1's stats.

Compounding it, `Start()` line 45 does an unguarded `instance = this`, so `BasketBall.instance` is
simply the last human basketball to initialize. That is the root cause of the reassignment hazard
AUD-016 worked around in `GameRules.GetStatsTotals`; the hazard itself is still here.

Fix: `updateScoreText()` should read into a local (or take the target stats as a parameter) and never
assign the field. Separately, guard `instance` like the other singletons do, or drop the static in
favour of resolving through `GameLevelManager.instance.players`.

Scope note: single-human modes are unaffected, since `players[0]` is the only human. This bites local
multiplayer, and only once the overlay is enabled - which is why it has survived.

**Fix applied.** `updateScoreText()` no longer assigns `gameStats`; it reads this ball's own stats.
Since the overlay is one shared `Text` object, `displayUiStats()` now returns early unless
`instance == this`, so only the primary ball drives it - which produces the same display as before
without the field mutation. `Start()` claims `instance` only when `instance == null` or
`IsPrimaryBasketball()` (this ball is `players[0].basketball`), making the static deterministic
instead of last-writer-wins. `displayUiStats()` also gained a null guard on the overlay fields,
which incidentally fixes an NRE twice a second in any scene without a `ui_stats` object.

### AUD-025: Enemy kills are credited to the wrong player's stats (Medium)

`Assets/Scripts/enemy/EnemyCollisions.cs` (in `enemyIsDead`)

```csharp
BasketBall.instance.GameStats.EnemiesKilled++;
if (enemyController.IsBoss) { BasketBall.instance.GameStats.BossKilled++; }
else { BasketBall.instance.GameStats.MinionsKilled++; }
```

`BasketBall.instance` is whichever human basketball ran `Start()` last (see AUD-024), not the player
who landed the kill - the `playerAttackBox` that caused the death is available right there in the same
method and is ignored for attribution. `MinionsKilled` and `BossKilled` feed XP directly
(`GameStats.getExperienceGainedFromSession`, +50 and +150 each), so this is a progression-affecting
misattribution, not just a display issue.

Two smaller issues in the same block:

- An enemy that is neither `IsBoss` nor `IsMinion` still increments `MinionsKilled` via the `else`.
- `GameLevelManager.instance.PlayerHealth` and `BasketBall.instance` are both dereferenced with no null
  check, on a code path that runs in enemies-only modes.

Fix: resolve the owning player from `playerAttackBox.transform.root`'s `PlayerIdentifier` and credit
that player's `gameStats`.

**Fix applied.** New `Assets/Scripts/combat/CombatCredit.cs` resolves the killing player by matching
the attacker's `transform.root` against the registered `GameLevelManager.players` entries, falling
back to the primary player when no attacker is known. `EnemyCollisions.enemyIsDead` passes the
`playerAttackBox`; `EnemyController.enemyIsDead` (no attacker on that path) passes null. Kills to an
unidentifiable source still land on player 1, as before, but a real attacker is now credited
correctly. The `else` branch still counts a non-boss non-minion as a minion - unchanged, since that
matches the health defaults.

### AUD-026: Percentage rolls are off-by-one and inconsistent between helpers (Medium)

Two independent implementations of "roll a percentage", with different bounds:

`Assets/Scripts/Utility/UtilityFunctions.cs:58-65`

```csharp
int percent = Random.Range(0, 100);   // yields 0..99
if (percent <= max)                   // <= , not <
```

`Assets/Scripts/basketball/BasketBall.cs:506-530` and `BasketBallAuto.cs:542-570`

```csharp
float percent = Random.Range(1, 100); // int overload -> yields 1..99, never 100
if (percent <= maxPercent)
```

Effects:

- `rollForCriticalInt(0)` returns true when `percent == 0` - a **0% chance still fires 1% of the time**.
  This is the enemy dodge roll (`EnemyCollisions.OnTriggerEnter`), where `luck` is 0 for any enemy that
  is neither boss nor minion, and for all enemies during the frames before `Start()` assigns it.
  `CpuBaseStats.LUCK` and `CLUTCH` are also 0.
- `rollForCriticalInt(99)` is certain, and `rollForCriticalShotChance(99)` is likewise certain because
  `Random.Range(1, 100)` never returns 100. A 99-luck character crits 100% of the time.
- The two helpers disagree on whether the roll is 0-based or 1-based, so the same stat value means a
  different probability depending on which one reads it.
- `Assets/Scripts/basketball/BasketBallAuto.cs:595`: `float accuracyVariationValue = Random.Range(-5, 5);`
  is also the int overload - it yields -5..+4, a distribution biased 0.5 low that can never reach +5.
  Same class of issue at line 602, `Random.Range(1, 10)` yields 1..9.

These are all the C# int/float overload trap: two integer literals select `Random.Range(int, int)`,
which is max-**exclusive**, even when the result is assigned to a `float`.

Fix: one shared helper using `Random.Range(0f, 100f)` with a strict `<` comparison, and `-5f, 5f` /
`1f, 10f` for the variation rolls. Then delete the duplicates in `BasketBall`/`BasketBallAuto` (this
is a tractable first slice of AUD-017's deduplication).

**Fix applied.** `Assets/Level5/Core/PercentChance.cs` is now the single rule: explicit endpoint
checks (`<= 0` never succeeds, `>= 100` always does) and `roll01 * 100f < chancePercent` in between,
with the roll injected so it is testable without the engine RNG. `UtilityFunctions.RollPercent`
wraps it with `Random.value`; `rollForCriticalInt` delegates to it. The six copied roll helpers in
`BasketBall`/`BasketBallAuto`, plus `BehaviorNpcCritical.rollForPhotoChance` and
`RacingVehicleCollisions.rollForPlayerEvadeAttackChance`, all delegate too. The asymmetric variation
rolls became float overloads (`Random.Range(-5f, 5f)`, `Random.Range(1f, 10f)`). Covered by
`Level5CombatMathTests`. See the behaviour-change note above - 0 and 99 stats now mean what they say.

### AUD-027: `Time.timeScale` is never restored when leaving a paused scene (Medium)

`Assets/Scripts/game manager/Pause.cs`

`setTimeScaleToActive()` (line 400) sets `Time.timeScale = 1f` and **has no callers anywhere in the
project**. `reloadScene()` handles the problem by hand (`if (paused) { TogglePause(); }`, line 265),
which shows the author knew about it - but the two other exits do not:

- `loadstartScreen()` (line 234) loads the loading scene with `timeScale` still 0.
- `Quit()` (line 223) leaves it 0 (harmless on exit, but the asymmetry is the smell).
- `GameRules.HandleMatchEnded` pauses via `Pause.instance.TogglePause()` and then
  `LoadNextCampaignLevel` loads the end-round scene, also at `timeScale` 0.

`Time.timeScale` is global and survives scene loads. Gameplay scenes self-heal via the reconciliation
check at `Pause.cs:153`, so the impact is confined to menu scenes, which have no `Pause` component.
Today that is masked because the menu scripts happen to use `WaitForSecondsRealtime` throughout - I
checked, there are no scaled `WaitForSeconds` calls under `menu_*`. So this is currently latent rather
than broken.

It is worth fixing anyway because the invariant is undocumented and one ordinary `WaitForSeconds` in a
menu script, or any scaled-time animation, silently hangs the menu with no obvious cause. Call
`setTimeScaleToActive()` on every scene exit (or reset `timeScale` from a `sceneLoaded` handler) and
keep a note in the UI docs.

**Fix applied.** New `Assets/Scripts/Utility/SceneTransition.cs` restores `Time.timeScale` and then
loads, making the invariant explicit at the call site. Every scene load that exits gameplay routes
through it: `Pause.loadstartScreen`, `Pause.reloadScene`, `Pause.QuitApplication`,
`GameRules.LoadNextCampaignLevel` (both error paths and the end-round load), and
`LoadGame.LoadDevLevelVersus`. `setTimeScaleToActive()` was kept - scene UnityEvents may be wired to
it - but now delegates rather than sitting dead.

### AUD-028: Manager initialization is an unchecked `GameObject.Find` chain (Medium)

`Assets/Scripts/game manager/GameRules.cs:154-160` - six consecutive
`GameObject.Find(name).GetComponent<Text>()` calls plus `GameObject.Find("timer").GetComponent<Timer>()`,
none null-checked.

`Assets/Scripts/game manager/Pause.cs:74-88` - the same pattern for `fade_texture`, `load_scene`,
`cancel_menu`, `load_start`, `quit_game`, and the toggle texts, followed by
`EventSystem.current.firstSelectedGameObject = loadSceneButton.gameObject` at line 107.

If any one of those objects is renamed, deactivated, or missing from a scene, the `NullReferenceException`
aborts `Start`/`Awake` partway through. The manager stays alive with half its fields assigned, its
static `instance` already published, and no error that names the missing object. `GameRules` in
particular would then have `matchEndHandled`/timer state uninitialized while other systems call into it.

Note that `GameRules` is careful about nulls *afterwards* (`ClearEndGameHudText` checks every field),
which makes the unguarded acquisition the odd one out.

`Level5ProjectValidator` (`Assets/Level5/Editor/Level5ProjectValidator.cs`) already validates build
scenes, selectable levels, and input actions at build time. Extending it to assert the required HUD
object names per gameplay scene would turn this whole class of failure into a build error. That is the
highest-leverage fix here - more than adding null checks one at a time.

**Fix applied.** Two halves:

*Runtime* - new `Assets/Scripts/Utility/SceneObjects.cs` logs the name that failed and returns null
instead of throwing. `GameRules.Start` uses it and, if the HUD is incomplete, leaves
`gameRulesEnabled` false with one clear error rather than throwing a `NullReferenceException` per
frame out of `SetScoreDisplayText`; the durable match-end work is already null-guarded and still
runs. `Pause.Awake` collects every missing object, reports them in a single message, restores time
scale, and disables itself rather than half-initializing. `setTimer` and the `footer` uses gained
null guards.

*Build/CI* - the required names now live in `GameRules.RequiredHudObjectNames` and
`Pause.RequiredPauseObjectNames`, and `Level5ProjectValidator.CollectGameplaySceneObjectErrors()`
checks every enabled build scene against them, driven by the new `Level5SceneContractTests` and a
`Level5/Validate Gameplay Scene Objects` menu item.

Note this deliberately does **not** run from `OnPreprocessBuild`: it opens scenes, which is not safe
inside the build pipeline. CI already runs edit-mode tests on every PR, so PRs are still gated.

### AUD-029: `BodyGuardHealthBar.instance` has the wrong type and no purpose (Low)

`Assets/Scripts/bodyguard/BodyGuardHealthBar.cs:13`

```csharp
public class BodyGuardHealthBar : MonoBehaviour
{
    ...
    public static PlayerHealthBar instance;   // wrong type, never assigned
```

Copy-pasted from `PlayerHealthBar`. It is never assigned and never read (confirmed by grep across
`Assets/Scripts`), so it is inert - but `BodyGuardHealthBar.instance` currently resolves to a
`PlayerHealthBar`, which is exactly the kind of thing that will be "fixed" later by assigning something
to it. Delete it.

**Fix applied.** Field deleted. It was never assigned and never read.

### AUD-030: `getReleaseModifier` comment is the inverse of the code (Low)

`Assets/Scripts/basketball/BasketBall.cs:589-599`

```csharp
// get random chance for removing release modifier
// ex if release = 85, 15% chance to remove modifiers
if (rollForCriticalReleaseChance(characterProfile.Release))
{
    return 0;
}
```

`rollForCriticalReleaseChance(85)` returns true ~85% of the time, so the code gives an 85% chance to
remove the modifier, not 15%. The code reads as the intended behaviour (higher release stat = better
shot); the comment is wrong. Worth correcting because someone tuning shot feel will read the comment
and "fix" the code.

**Fix applied.** Comment corrected in both `BasketBall.cs` and `BasketBallAuto.cs` (the auto path
had the same copy-pasted comment). The code was right; only the comment was inverted.

### AUD-031: Missing null checks in `EnemyCollisions` (Low)

`Assets/Scripts/enemy/EnemyCollisions.cs`

- `Start()`: `enemyController = gameObject.transform.root.GetComponent<EnemyController>();` then
  immediately `enemyController.IsBoss` with no null check.
- `enemyStepOnRake`: `other.transform.parent.GetComponentInChildren<Animator>().Play("attack");` -
  three chained dereferences, any of which can be null for an attack box parented differently.
- `enemyIsDead`: `GameLevelManager.instance.PlayerHealth` and `BasketBall.instance` unchecked (also
  noted under AUD-025).

Low severity because the prefab hierarchy currently satisfies all three. It is a prefab-contract
dependency with no validation behind it - same underlying gap as AUD-028.

**Fix applied.** `EnemyCollisions.Start` null-checks `transform.parent` before the health-bar lookup
and disables itself with a named error if no `EnemyController` is on the root. `enemyStepOnRake`
null-checks the parent and the animator. The `BasketBall.instance` and `PlayerHealth` dereferences
in `enemyIsDead` were resolved as part of AUD-025.

### AUD-032: Test coverage cannot catch this class of bug (Low, but structural)

`Assets/Tests/Editor/Level5CoreTests.cs` holds 7 tests; `Assets/Tests/PlayMode/CampaignRoundPlayModeTests.cs`
holds 1. Eight tests for ~42k lines of runtime code.

The relevant point is not the raw number - it is that AUD-022 and AUD-026 are **pure functions over
plain data**. `getExperienceGainedFromSession`, `getPercentageFloat`, and the roll helpers need no
scene, no prefab, and no Unity lifecycle to test. A dozen edit-mode assertions ("zero sniper shots
awards no evasion bonus", "a 0% roll never succeeds", "a 100% roll always succeeds") would have caught
both, and they are cheap to write now that `Level5.Core.asmdef` and the CI edit-mode job
(`.github/workflows/repository-quality.yml`) already exist.

Recommendation: when fixing AUD-022 and AUD-026, move the math into `Assets/Level5/Core` and land the
tests with the fix. That converts a one-off repair into a regression barrier for the whole
progression/randomness surface.

**Fix applied.** `Assets/Tests/Editor/Level5CombatMathTests.cs` adds 15 tests over the extracted
math - roll endpoints and the off-by-one cases, the sniper evasion curve including the
no-sniper-fire case, arcade zeroing, difficulty scaling, enemy-kill gating, and multiplier
compounding - plus a check that the scene-contract name lists are non-empty and duplicate-free.
`Level5SceneContractTests.cs` adds the AUD-028 scene check. Test count goes from 8 to 24.

One thing the tests pinned down that is worth knowing: `MatchExperienceInput` defaults
`DifficultySelected` to 0, and 0 is *easy*, which halves the award. Tests asserting exact totals
opt into normal difficulty explicitly.

### AUD-033: `StartManager_original.cs` ships in the build (Low)

`Assets/Scripts/menu_start/StartManager_original.cs` is 1683 lines living in the runtime tree beside the
2261-line `StartManager.cs`. It is in the default `Assembly-CSharp` assembly, so it compiles into
player builds and is reachable by any `GameObject.Find`/`GetComponent` lookup.

This is the concrete instance of AUD-012. It also drags on iteration time: only `Assets/Level5/Core`
and the test folders have `.asmdef` files, so all 245 scripts under `Assets/Scripts` sit in the default
assembly and any single-line edit triggers a full recompile.

**Fix applied.** Moved to `Assets/Scripts/menu_start/Legacy~/StartManager_original.cs`. Unity ignores
any folder ending in `~`, so it no longer imports as an asset or compiles into `Assembly-CSharp`,
but it stays in git and stays readable. Its `.meta` was deleted (Unity does not track files in `~`
folders), and `scripts/validate-repository.ps1` now skips `~` paths in its missing-`.meta` check -
otherwise CI would demand a `.meta` that Unity will never create. Verified first that no code
referenced the type and no scene or prefab referenced its GUID.

## Areas Reviewed and Found Sound

Recording these so the next pass does not re-derive them.

- **`Assets/Scripts/restapi/APIHelper.cs`** - genuinely well built. Absolute-URI and http/https scheme
  validation before every request, 10s timeouts, a single-flight lock with a 12s deadline and
  `ReferenceEquals` ownership check so a slow request cannot release someone else's lock, generic
  user-facing error strings that do not leak server detail, `ClearSession()` on token post, and
  `request.Dispose()` in a `finally`. No changes recommended.
- **`Assets/Scripts/player/CharacterProgressStore.cs` / `AtomicFile`** - correct temp-write +
  `File.Replace` + `.bak` fallback, with validator-checked reads that fall back to the backup. Sound.
- **`Assets/Scripts/pooling/RuntimeObjectPool.cs`** - double-release guarded by the `released` flag,
  pools cleared on scene unload and on `SubsystemRegistration`. Sound. (Minor: `Spawn` dereferences
  `pooledObject` without the null check that `Release` has; unreachable given `CreateInstance` always
  adds the component.)
- **`Assets/Scripts/input/PlayerControlsProvider.cs`** - properly ref-counted enable/disable with
  underflow guards and a full static reset on `SubsystemRegistration`. Sound.
- **`GameRules` match-end flow** - `RequestGameOver`/`matchEndHandled`/`HandleMatchEnded` with
  per-stage completion flags, retry backoff, and try/catch isolation per stage. The AUD-019 fix holds up.
- **Update-loop performance** - swept every `Update`/`FixedUpdate`/`LateUpdate` for `GameObject.Find`,
  `FindObjectOfType`, and `Resources.Load`. The only hits are in `Pause.Update` (two `GetComponent<Button>()`
  calls per frame while the pause menu is open, which is negligible) and the legacy
  `StartManager_original.cs`. No per-frame allocation hot spots found.
- **Repository hygiene** - no secrets in `Assets/Scripts` or `ProjectSettings`; the Android keystore is
  a `{dedicated}` reference, not an embedded credential; no `Library/`, `Temp/`, `obj/`, `.csproj`, or
  `.sln` files tracked in git (6735 tracked files, zero matches).

## Client/Server Trust Notes

Not defects in this repository, but they are contracts this client depends on and they should be
confirmed against `Level5Backend`:

- **Account enumeration is by design.** `UserNameExists`, `EmailExists`, and `GetUserByUserName`
  (`APIHelper.cs:246-266`) are unauthenticated, and `AccountManager.LoginUserCoroutine`
  (`AccountManager.cs:604-615`) fetches the **full user record by username** before it has any
  credential, then posts the password separately. Given that API shape the client has no better option,
  but the server must be returning a minimal projection on that endpoint - no password hash, no email,
  no PII. Worth verifying directly.
- **Score fields are client-authored.** `PostUnsubmittedHighscores` stamps
  `score.Userid = GameOptions.userid` and `score.UserName = GameOptions.userName` client-side
  (`APIHelper.cs:105-107`), and the score values themselves come from local `GameStats`. The server
  must derive identity from the bearer token and never trust the posted `Userid`. Client-side score
  integrity is not achievable and should not be attempted here.

## Second Pass - 2026-08-06 (post-fix)

A second sweep run after the AUD-022..033 fixes landed. Two goals: re-review the changes above, and
open the files the first pass never got to - `DBHelper`, `DBConnector`, `PlayerAttackQueue`,
`BodyGuardCollisions`, `Timer`, and the progression/level math.

### Re-review of the first-pass fixes

No defects found. All eight roll call sites resolve to the shared helper, no orphaned locals or
unused `Random` aliases remain, every file using the new `Assets.Scripts.Utility` types imports the
namespace, and `players[0].setBasketball` runs before any `BasketBall.Start`, so
`IsPrimaryBasketball()` cannot read a half-built roster.

### AUD-034: `Timer` and `GameRules` both initialize the match clock, with different formulas (Medium)

`Assets/Scripts/game manager/Timer.cs:63-73` and `Assets/Scripts/game manager/GameRules.cs:200-207`

Both `Start()` methods write the same field - `Timer.timeStart` - and they do not agree:

```csharp
// Timer.Start()
if (gameModeThreePointContest || gameModeFourPointContest || gameModeSevenPointContest
    || gameModeAllPointContest || GameOptions.customTimer > 0)
{
    timeStart = GameOptions.customTimer;      // 0 if a contest mode has no custom timer
}
else { timeStart = 180; }

// GameRules.Start()
if (GameOptions.customTimer > 0) { setTimer(GameOptions.customTimer); }
else { setTimer(180); }
```

`Timer`'s condition mixes "is a contest mode" with "has a custom timer", but the assignment only
makes sense for the second. A contest mode whose prefab leaves `CustomTimer` at 0 gets
`timeStart = 0` from `Timer` and `180` from `GameRules`. With `timeStart = 0`, `timeRemaining` goes
negative on the first frame and the match ends instantly.

Which one wins is undefined. Unity does not order `Start()` between two components, and this project
sets no execution order at all - `ProjectSettings/MonoManager.asset` has no `m_ExecutionOrder`
entries and no script carries `[DefaultExecutionOrder]` (both verified).

Today the divergence needs a contest-mode prefab with `CustomTimer` unset to bite, so it may not be
reachable with current data - `GameOptions.customTimer` is fed from each mode prefab's `CustomTimer`
field (`StartManager.cs:1777-1785`) and nothing validates that contest modes set it. But two owners
computing one value by different rules in an undefined order is a defect regardless of whether the
data currently hides it.

Recommended: give `timeStart` a single owner. `GameRules` already resolves the `Timer` and calls
`setTimer`, so `Timer.Start()` should stop computing it. If contest modes are meant to require a
custom timer, assert that in `Level5ProjectValidator` alongside the other mode-prefab checks.

**Fix applied.** `GameRules` is now the only writer of `timeStart`. `Timer.Start()` no longer computes
it and instead falls back to `MatchClock.DefaultMatchSeconds` only if nothing has set it - which makes
the outcome identical in either `Start()` order, since `GameRules.setTimer` overwrites the fallback
whenever it runs. The rule itself moved to `Assets/Level5/Core/MatchClock.cs`, whose
`StartSeconds(customTimer)` deliberately does **not** treat "is a contest mode" as a reason to use
`customTimer` - that conflation was the bug.

`Level5ProjectValidator.ValidateContestModeTimers` now fails the build on any contest-mode prefab
that leaves `CustomTimer` at 0. The runtime no longer breaks on that data, but a contest silently
running at the default length instead of its intended one is still wrong, and this catches it.

### AUD-035: `BodyGuardCollisions` still has the null-safety defects fixed in `EnemyCollisions` (Low)

`Assets/Scripts/bodyguard/BodyGuardCollisions.cs:18-23` and its `enemyStepOnRake`

This one is a scoping miss in AUD-031 rather than a new discovery. `BodyGuardCollisions` is a
near-copy of `EnemyCollisions` and carries the same three unguarded dereferences:

- `Start()`: `transform.parent.GetComponentInChildren<BodyGuardHealthBar>()` with no parent check.
- `Start()`: `gameObject.transform.root.GetComponent<BodyGuardController>()`, then the controller is
  used without a null check.
- `enemyStepOnRake`: `other.transform.parent.GetComponentInChildren<Animator>().Play("attack")` -
  the identical three-deep chain that was fixed on the enemy side.

AUD-031 was written against `EnemyCollisions` specifically and the fix followed that scope, so the
bodyguard copy was left behind. Worth applying the same guards, and worth noting as an argument for
collapsing the two collision handlers rather than continuing to patch them in parallel.

**Fix applied.** The same three guards from the `EnemyCollisions` fix, applied to its bodyguard twin:
`transform.parent` is checked before the health-bar lookup, a missing `BodyGuardController` on the
root logs a named error and disables the component, and `enemyStepOnRake` null-checks both the parent
and the animator. Collapsing the two near-identical collision handlers is still the real fix and is
listed under Follow-Up.

### AUD-036: experience-per-level is hardcoded in eight places (Low)

The literal `3000` appears as the XP-per-level divisor in:

- `Assets/Scripts/database/DBHelper.cs:385`, `:386`, `:526`
- `Assets/Scripts/menu_loading/LoadManager.cs:330`
- `Assets/Scripts/menu_start/StartManager.cs:1650`, `:1651`
- `Assets/Scripts/player/CharacterProfile.cs:105`
- `Assets/Scripts/player/CharacterProgressMigration.cs:43`
- `Assets/Scripts/player/CharacterProgressParityLogger.cs:76`

`StartManager` also derives "XP to next level" as `(level + 1) * 3000 - experience`, which silently
assumes the same constant a second way. Changing the progression curve means finding all nine sites,
and a missed one produces a level shown in the menu that disagrees with the level written to the
database.

This is the same shape as AUD-018 (mode dispatch duplicated with inconsistent constants), and it now
has an obvious home: `MatchExperience` in `Level5.Core` already owns the earn side of progression. A
`CharacterLevel.FromExperience(int)` / `ExperienceToNextLevel(int)` pair beside it would give the
spend side one owner too, and both are pure functions that the existing edit-mode suite can cover.

**Fix applied.** `Assets/Level5/Core/CharacterLevel.cs` owns the curve:
`ExperiencePerLevel`, `FromExperience(int)`, `FromExperience(float)` (DBHelper accumulates in float
when applying an award, the menus use int), and `ExperienceToNextLevel`. All nine sites now call it -
`DBHelper` x3, `LoadManager`, `StartManager` x2, `CharacterProfile`, `CharacterProgressMigration`,
`CharacterProgressParityLogger` - and no bare `3000` remains in the runtime tree. Negative
experience clamps to level 0 rather than going negative, and a player sitting exactly on a level
boundary is shown a full level remaining rather than 0. Covered by four tests including one that
asserts the int and float curves agree across the range.

### AUD-037: `getCriticalPercentage` guards on the wrong variable (Low)

`Assets/Scripts/basketball/BasketBall.cs:700-711` and `BasketBallAuto.cs:753-764`

```csharp
if (gameStats.CriticalRolled > 0)
{
    float accuracy = (float)gameStats.CriticalRolled / gameStats.ShotAttempt;
```

The guard tests `CriticalRolled` but the divisor is `ShotAttempt`, so it does not actually protect
the division. Float division by zero yields Infinity rather than throwing, so the overlay would
render `Infinity%` instead of crashing.

Currently unreachable: `shootBasketBall` increments `ShotAttempt` before `Launch` runs the critical
roll, so `CriticalRolled > 0` implies `ShotAttempt > 0`. It is a latent wrong-guard that any
reordering of the shot pipeline would expose - worth correcting to `ShotAttempt > 0` while the
neighbouring accuracy getters (which do guard correctly) are right there for comparison.

**Fix applied.** Both copies now guard `ShotAttempt > 0` - the divisor - instead of `CriticalRolled`.

### Checked and found sound in this pass

- **`DBConnector` / `DBHelper` locking.** I twice suspected a leaked `databaseLocked` flag and was
  wrong both times, so recording it: all seven `DBConnector` acquire/release pairs are matched, and
  every one of the 30 `DBHelper` methods that takes the lock releases it on both the success and the
  exception path. The newer methods use `try/finally`; the older ones release before each `return`
  and again in `catch`. The first false alarm came from grepping the `DatabaseLocked` property while
  several methods release via the lowercase `databaseLocked` backing field.
- **`PlayerAttackQueue`.** Reservation, release, and stale-entry cleanup are consistent; entries are
  keyed both by attacker and in a parallel list that is kept in sync. One observation, not a defect:
  `RefreshBodyGuards()` only runs in `Start()`, so a bodyguard spawned mid-match is not tracked.
- **`Timer` counter modes.** `timeRemaining` stays 0 in counter modes, but every branch that could
  misread it is gated on `!modeRequiresCounter`.
- **Accuracy getters.** All eight per-shot-type accuracy getters in both basketball scripts guard
  their divisor correctly. Only `getCriticalPercentage` (AUD-037) does not.

## Third Pass - 2026-08-06 (post-fix, second round)

Run after AUD-034..037 landed. Re-verified the second batch, re-checked whether AUD-023's scope was
complete, and opened `PlayerController`, `StartManager`, `StatsManager`, `ProgressionManager`, and
the touch input layer.

Two findings, both Low, both since fixed.

### AUD-038: queued touch inputs survive a scene change (Low)

`Assets/Scripts/input/PlayerTouchInputState.cs` and `Assets/Scripts/input/TouchInputController.cs:71-80`

`PlayerTouchInputState` holds four pieces of static input state. `TouchInputController.OnDisable`
clears exactly one of them:

```csharp
private void OnDisable()
{
    hold1Detected = false;
    PlayerTouchInputState.BlockHeld = false;   // cleared
    // jumpOrShootQueued, attackQueued, specialQueued are not
    ...
}
```

The reset that does cover all four is `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, which
runs once per application launch - not per scene load. So an attack, special, or jump/shoot queued on
the last frame before a scene change stays queued, and the next scene's freshly spawned player
consumes it as a phantom input on its first frame.

Narrow - it needs a tap on the frame the scene tears down, before `PlayerController.Update` consumes
it. But the asymmetry is the tell: whoever added the `BlockHeld` line understood the hazard and
covered only the state whose stuck value would be most visible. Fix is three more lines in the same
`OnDisable`, or better, a single `PlayerTouchInputState.Clear()` that `OnDisable` calls.

**Fix applied.** `PlayerTouchInputState.Clear()` drops all four fields, and
`TouchInputController.OnDisable` calls it instead of clearing `BlockHeld` alone. The
`SubsystemRegistration` reset now delegates to the same method, so there is one definition of
"drop all touch input" rather than two lists that can drift. Covered by two tests.

### AUD-039: `EnemyHealth.ResetForSpawn` silently does nothing without a controller (Low)

`Assets/Scripts/enemy/EnemyHealth.cs:30-54`

The whole body is wrapped in `if (enemyController != null)`, so when the lookup in `Awake`
(`transform.root.GetComponent<EnemyController>()`) finds nothing, spawn reset is skipped entirely -
no log, no error. `maxEnemyHealth` stays 0, which makes the `Health` setter clamp every write to
`Mathf.Clamp(value, 0, 0)`, so the enemy cannot take damage and cannot die.

`ResetForSpawn` is called from `OnEnable`, which is the pooling reuse path, so this is the same
prefab-contract class as AUD-031/035: currently masked by correct prefab data, silent when that data
is wrong, and the symptom (an invincible enemy) points nowhere near the cause. Should log an error
and leave the component disabled rather than returning quietly.

**Fix applied.** `ResetForSpawn` no longer wraps its body in the null check. A missing controller
now logs an error naming the object and falls back to the health configured on the prefab, using
the default only when that is also unset - so the zero case, which is the actual bug, cannot occur.

Two things surfaced while fixing it that the finding had not:

- The original preserved any inspector-set `maxEnemyHealth` when the controller was missing, so the
  "invincible enemy" outcome needed *both* no controller and no configured health. The finding
  overstated it as following from the missing controller alone. The fix keeps that inspector value
  rather than overwriting it with a default.
- `ResetForSpawn` runs on every `OnEnable`, which is every pool reuse, and applies the hardcore +25%
  bonus each time. Scaling a value that the previous spawn had already scaled would compound it
  across respawns. `Awake` now captures the configured base into `configuredMaxEnemyHealth` so each
  reset scales from a stable number. The controller path already avoided this by reassigning from a
  constant; only the new fallback path needed it.

### Confirmed complete

- **AUD-023 scope.** Re-scanned every mutable static in the runtime tree. `PlayerController` and
  `EnemyController` keep `currentState` as instance fields, and the only other occurrence is
  commented out in `AutoPlayerControllerTest`. The four controllers fixed were all of them.
- **Second-batch fixes.** No bare `3000` remains outside a comment; all nine call sites resolve to
  `CharacterLevel`; `MatchClock`/`CharacterLevel` take no dependency on `GameOptions`,
  `GameLevelManager`, or `PlayerData`, so `Level5.Core` stays independent of `Assembly-CSharp`.
- **Menu index safety.** `StartManager` validates all four selection indices against their list
  counts before use (`StartManager.cs:701-716`); the wrap-around decrements handle the 0 case.

### Not yet covered by any pass

`PlayerController` (1050 lines), `StartManager` (2261), `StatsManager` (1151), and
`ProgressionManager` (976) have been sampled but not read end to end - together roughly 5,400 lines,
the largest remaining gap. `AutoPlayerController` and `RacingVehicleController` (807 each) are
untouched. A fourth pass should start there rather than re-sweeping what three passes have covered.

## What To Do Next

All sixteen findings are fixed in code. The remaining work is verification, in this order:

1. **Open the project in Unity and let it compile.** New files under `Assets/Level5/Core`,
   `Assets/Scripts/Utility`, `Assets/Scripts/combat`, and `Assets/Tests/Editor` have hand-written
   `.meta` files with fresh GUIDs; Unity will accept them and import normally.
2. **Run the edit-mode tests** (33 now). `Level5CombatMathTests` should pass outright.
   `Level5SceneContractTests` may legitimately fail if a gameplay scene is missing a HUD or
   pause-menu object - read the names it reports before changing anything.
3. **Run `Level5/Validate Project`.** The new contest-mode timer check may legitimately fail if a
   contest prefab never set `CustomTimer`; that is real data to fix, not a broken check.
4. **Playtest a scene with two or more bodyguards** (AUD-023). The shared-state fix is a keyword
   change, but behaviour may have been tuned around the old value.
5. **Playtest local multiplayer with the UI-stats overlay toggled on** (AUD-024). Confirm each
   player's shot counts stay on their own stat line.
6. **Check a contest mode's clock** (AUD-034). It should run at its prefab's `CustomTimer`, and a
   mode with none should run at 180s regardless of component start order.
7. **Decide whether the progression curve needs rebalancing** now that the phantom +500 XP per
   match is gone (see Behaviour changes above). This is a design call, not a code fix.

## Follow-Up Worth Considering

Not defects, and not done here:

- AUD-017 (the duplicated `BasketBall`/`BasketBallAuto` shot pipeline) is now smaller: the six roll
  helpers across both files delegate to one implementation. The rest of the duplication -
  `shootBasketBall`, `Launch`, modifier math, score-text formatting - is still copy-pasted and still
  wants its own focused slice.
- `Assets/Scripts` remains one 244-script default assembly with no `.asmdef`. Splitting it would cut
  iteration time and let the Runtime/Editor/Dev boundary be enforced rather than described.
- `EnemyCollisions` and `BodyGuardCollisions` are near-identical copies that have now been patched in
  parallel twice (AUD-031, AUD-035). Collapsing them behind the existing `IDamageable`/`ICombatAgent`
  contracts would stop the next fix needing to be applied twice, and feeds AUD-004.
- `Level5.Core` now holds four pure modules (`CampaignRoundDecision`, `PercentChance`,
  `MatchExperience`, `CharacterLevel`, `MatchClock`). It is proving to be a good home for rules that
  were previously duplicated across MonoBehaviours - worth continuing to pull into as AUD-010 and
  AUD-017 get addressed.
