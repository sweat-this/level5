# Deep Audit - 2026-08-06

Scope: full runtime pass over `Assets/Scripts` (~42k lines, 245 scripts), concentrating on areas the
existing [Architecture Audit](architecture-audit.md) register left thin: progression/XP math, stat
ownership across multiple players, AI instance state, persistence and networking, and lifecycle
hygiene.

Every finding below was traced to specific lines and cross-checked against its callers. Nothing here
duplicates an existing AUD entry, though several sharpen a previously generic one (noted per finding).
IDs continue the main register's sequence.

**All findings below were fixed on 2026-08-06.** Code changes are described per finding under
"Fix applied". The fixes have not been compiled or playtested in Unity - see
[Verification Status](#verification-status).

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

## What To Do Next

All twelve findings are fixed in code. The remaining work is verification, in this order:

1. **Open the project in Unity and let it compile.** New files under `Assets/Level5/Core`,
   `Assets/Scripts/Utility`, `Assets/Scripts/combat`, and `Assets/Tests/Editor` have hand-written
   `.meta` files with fresh GUIDs; Unity will accept them and import normally.
2. **Run the edit-mode tests.** `Level5CombatMathTests` should pass outright.
   `Level5SceneContractTests` may legitimately fail if a gameplay scene is missing a HUD or
   pause-menu object - read the names it reports before changing anything.
3. **Playtest a scene with two or more bodyguards** (AUD-023). The shared-state fix is a keyword
   change, but behaviour may have been tuned around the old value.
4. **Playtest local multiplayer with the UI-stats overlay toggled on** (AUD-024). Confirm each
   player's shot counts stay on their own stat line.
5. **Decide whether the progression curve needs rebalancing** now that the phantom +500 XP per
   match is gone (see Behaviour changes above). This is a design call, not a code fix.

## Follow-Up Worth Considering

Not defects, and not done here:

- AUD-017 (the duplicated `BasketBall`/`BasketBallAuto` shot pipeline) is now smaller: the six roll
  helpers across both files delegate to one implementation. The rest of the duplication -
  `shootBasketBall`, `Launch`, modifier math, score-text formatting - is still copy-pasted and still
  wants its own focused slice.
- `Assets/Scripts` remains one 245-script default assembly with no `.asmdef`. Splitting it would cut
  iteration time and let the Runtime/Editor/Dev boundary be enforced rather than described.
