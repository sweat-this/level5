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
| AUD-040 | Stats paging | Medium | Fixed | The stats page count is queried with a different filter set than the rows, so it is too small with no filters and too large with them. |
| AUD-041 | Stats paging | Low | Fixed | Paging left with zero results sets the page number to -1 and displays "page 0 / 0". |
| AUD-042 | Stats paging | Low | Fixed | `ResultsPerPage` is declared but the page arithmetic hardcodes 10 in eight places. |
| AUD-043 | Dead parameter | Low | Fixed | `getNumberOfResults` accepts a `pageNumber` it never uses. |
| AUD-044 | Combat ownership | Medium | Fixed | `PlayerAnimationEvents` resolves its controller from `players[0]`, so every actor's attack box and lunge drive player 1. |
| AUD-045 | Account identity | Medium | Fixed | `GameOptions.userid != 0` is the "authenticated" test but is set on paths that never authenticate. |
| AUD-046 | Progression | Medium | Fixed | `ProgressionManager` divides by an uninitialized `[SerializeField]` XP-per-level field - the tenth AUD-036 site, and an integer divide-by-zero if the inspector value was never set. |
| AUD-047 | Scene contracts | Medium | Fixed | `ProgressionManager.getUiObjectReferences` is 17 unchecked `Find(...).GetComponent<T>()` calls in `Awake`, plus three unguarded `confirmationDialogueBox` dereferences. |
| AUD-048 | Mode gating | Medium | Fixed | `PlayerJump` gates the shot meter on `!battleRoyal \|\| !enemiesOnly`, which is only false when *both* are on - the inverse of the intent. |
| AUD-049 | Operator precedence | Low | Fixed | `&&` binds tighter than `\|\|`, so the `!InAir && !KnockedDown` guard on the idle/walk speed rule applied only to the `bIdle` branch. |
| AUD-050 | CPU movement | Medium | Fixed | `AutoPlayerController.moveToPosition` overwrites the `targetPosition` field with a normalized direction, and `FixedUpdate` reads `movement.y` as the depth axis. |
| AUD-051 | Animator state | Low | Fixed | Two animator hashes omit the `base.` prefix in three files, so they can never match a `fullPathHash`; `AutoPlayerController` masked it with a re-assignment in `Start`. |
| AUD-052 | Null safety | Low | Fixed | `Terrain.activeTerrain` is dereferenced unguarded in three files - the twins of the one place `PlayerController` already guards. |
| AUD-053 | Null safety | Low | Fixed | The CPU, racing, enemy, bodyguard and NPC copies of the HUD/`camera_flash` lookups never got the guards their player-side twin has. |
| AUD-054 | Camera state | Low | Fixed | `ShrinkPlayer` halves the camera FOV and restores it to a hardcoded `50` rather than the value it captured. |
| AUD-055 | CPU shot selection | Low | Fixed | `cpuShootSevenpointers` divides by `Accuracy7Pt` unguarded; an unset stat yields `Infinity`, which passes the threshold and makes every CPU a seven-point specialist. |
| AUD-056 | Mode gating | Medium | Fixed | `EnemySpawner`'s enemy-count chain tests `\|\| enemiesEnabled`, which `StartManager` forces true for every battle royal - making two branches dead and spawning 4 enemies where the branch order says 2. |

AUD-022 to AUD-033 came from the first pass; AUD-034 to AUD-037 from the
[second pass](#second-pass---2026-08-06-post-fix) run after those fixes landed. A third pass added
AUD-038 and AUD-039. A fourth added AUD-040 to AUD-043. A fifth added AUD-044 and AUD-045. A
[sixth pass](#sixth-pass---2026-08-06-the-unread-controllers) added AUD-046 to AUD-055, closing the
gap the third and fourth passes had recorded as unread. All thirty-four are fixed in code and await
Unity compile/playtest verification.

## Verification Status

What was and was not checked:

- **Verified**: `scripts/validate-repository.ps1` passes (including a deliberately planted
  missing-`.meta` probe, to confirm the new `~`-folder exclusion did not disable the check).
  All XP arithmetic asserted in the new tests was computed independently and matches, including
  Unity's banker's rounding in `Mathf.RoundToInt`.
- **Not verified**: nothing here has been compiled by Unity or playtested. The edit-mode tests are
  written but have not been run. Compile and run `Level5CombatMathTests` and
  `Level5SceneContractTests` before trusting any of it.
- **Not verifiable from outside Unity**: this project serializes scenes and prefabs in binary
  (`ForceBinary`), so no inspector value can be read from the repository - `.unity` files are not
  greppable and script GUIDs do not appear in any text asset. Every finding that turns on a
  serialized value (AUD-046 most directly, and the `AutoPlayerController` dead-initialization note
  under Follow-Up) is stated as a conditional for that reason, not hedged out of caution.
- **Retired 2026-08-07**: this section used to warn that `Level5SceneContractTests` should be
  expected to fail on its first run. Once Unity converted the scenes to text, the contract could be
  checked directly: all 17 `GameRules.RequiredHudObjectNames` / `Pause.RequiredPauseObjectNames`
  entries are present in all three scenes that carry a `GameRules`. The gameplay half of that test
  should pass outright. A failure now means something real.
- **`ProgressionManager.RequiredProgressionObjectNames` (AUD-047) is still unverified.** Its objects
  live in `Assets/Resources/Prefabs/menu_progression/progressionScreen.prefab`, which is one of the
  ~39 prefabs Unity has not yet reserialized, so it cannot be read from outside the editor. The
  names were taken verbatim from the strings the runtime already passes to `GameObject.Find`, so
  they are as correct as the shipping code - but run `Level5/Reserialize Project Assets` and re-run
  the suite before trusting that.

### Pre-existing Unity console issues (recorded 2026-08-07)

The first Unity open surfaced a set of warnings. **None of them come from this audit** - recorded
here so the next person does not re-diagnose them, and so they are not mistaken for regressions.

How that was established: no audit commit touched a `.unity`, `.prefab`, or `.asset` file (the four
such files on this branch came from `6411e874`, which predates the audit); every script moved to
`Assets/Scripts/Dev/` moved with its `.meta`, so its GUID still resolves; and the two `.meta` files
the audit did delete - `BasketBallShotMarkerAuto` (`ef41aeb5...`) and `StartManager_original`
(`9d996086...`) - appear nowhere in the unresolved set, which confirms the claim made at the time
that neither was referenced by any scene or prefab.

**Missing script references.** Scanning every `m_Script` GUID in the text scenes and prefabs against
every `.meta` in `Assets/`, `Packages/`, and `Library/PackageCache/`: 149 script GUIDs referenced,
23 unresolved against `Assets/` alone, of which 15 resolve to packages. Eight are genuinely missing:

| GUID | Referenced by | Console object |
| --- | --- | --- |
| `954300b2...` | the three `level_00_account*` scenes | likely `nextScene` |
| `ab2fe067...` | `level_00_start` | likely `nextScene` |
| `8b9a305e...`, `948f4100...`, `a79441f3...` | 3-4 gameplay levels each | likely the camera / `PostProcessing` behaviours |
| `cadd54e4...` | `Standard Hose` / `Standard HoseMobile` prefabs | third-party asset |
| `474bcb49...` | `level_01_scrapyard_lights` | one-off |
| `a215fc91...` | `level_01_scrapyard_cpu_defense_test` | one-off |

These are long-standing rot from scripts or asset packages removed earlier in the project's history.
The accompanying *"Serialized files [version 17] before 2019.1 are deprecated"* message points the
same way - the project was last opened by a much older Unity.

**Recommended order:** close Unity, delete `Library/` (generated, untracked), reopen. That clears the
version-17 complaint and forces a clean reimport after the reserialization. Judge every other
warning only after that, because a stale import cache produces phantom errors. Whatever is still
missing afterwards is a real decision per object: reinstall the package, or delete the dead
component.

**`BoxCollider does not support negative scale` warnings** on `Boundaries/boundary *` and
`basketball(Clone)/groundCheck` are scene authoring issues, not code. Worth fixing rather than
ignoring: a negatively-scaled collider silently has different collision geometry than the scene
shows, and `groundCheck` is the ball's ground detection. Fix with positive scale plus rotation.

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

The sixth pass adds three more:

4. **CPU movement, walk animation, and facing all change** (AUD-050). The navigation target is no
   longer clobbered mid-frame and the animation vector reads the correct axis. This should look like
   CPU players moving more smoothly, but it is a real behaviour change and wants a playtest.
5. **The shot meter no longer starts on jump in a plain battle royal or a plain enemies-only run**
   (AUD-048). It previously started in both, since the gate only suppressed when the two were
   combined.
6. **A CPU character with `Accuracy7Pt` of 0 stops attempting seven-pointers** (AUD-055). Previously
   the division produced `Infinity` and classified exactly those characters as specialists.

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

## Fourth Pass - 2026-08-06 (menus and unread controllers)

Aimed at the gap the third pass named: the large files no earlier pass had read end to end.
`StatsManager`, `ProgressionManager`, `StartManager`, `PlayerController`, `AutoPlayerController`,
`RacingVehicleController`.

Four findings, all in the stats browser, all since fixed.

### AUD-040: the stats page count is computed from a different filter set than the rows (Medium)

`Assets/Scripts/database/DBHelper.cs:1694-1717` (`getNumberOfResults`) against
`BuildSqlQueryForGetHighScoreRows` at `:1642` and `StatsManager.cs:834-845`

The stats screen runs two queries per refresh: one for the rows on the current page, one for the
total used to compute the page count. They do not filter the same way.

| | rows query | count query |
| --- | --- | --- |
| no filters selected | `WHERE modeid` | `WHERE modeid AND hardcoreEnabled = 0` |
| any filter selected | `WHERE modeid` + hardcore + traffic + enemies + sniper | `WHERE modeid AND hardcoreEnabled = 0/1` |

So the count is wrong in **both** directions depending on filter state:

- **No filters.** Rows include hardcore and non-hardcore entries; the count only counts
  non-hardcore. The page count comes out too small, so real result pages are unreachable - the
  player cannot page to scores that exist.
- **Traffic, enemies, or sniper filter on.** Rows are narrowed by three more conditions the count
  ignores, so the count comes out too large and the extra pages render empty.

Both are user-visible in the normal stats flow, and neither produces an error - just a page counter
that disagrees with the data. Fix is to build both queries from one filter clause, which is most of
the way to merging them into a single method that returns rows plus total.

**Fix applied.** Both queries now build their `WHERE` from one `BuildHighScoreFilterClause`, so the
count is filtered exactly like the rows by construction. `getNumberOfResults` takes all four filter
flags and binds them; `StatsManager` passes the same four it passed to the rows query a few lines
above. The mode-dependent sort direction moved to a named `HighScoreFieldIsAscending`, which
collapsed the four near-identical SQL strings into one.

The reader that walks the result columns branches on the same `HasHighScoreFilters` predicate the
`SELECT` list is built from, so the column indices cannot drift away from the query shape - that
coupling was implicit before and is the same failure mode one level down.

### AUD-041: paging left with no results produces a negative page number (Low)

`Assets/Scripts/menu_stats/StatsManager.cs:1058-1080`

```csharp
if ((localResultsPageNumber - 1) >= 0) { localResultsPageNumber--; }
else { localResultsPageNumber = numPages - 1; }   // numPages is 0 when there are no results
```

With no local scores for the selected mode, `numLocalResults` is 0, so `numPages` is 0 and the
wrap-around lands on `-1`. The display then reads `page 0 / 0`, and the query runs with
`OFFSET -10`.

It does not crash: SQLite treats a negative OFFSET as 0, and the next press of "increase" resets to
0 because `(-1 + 2) <= 0` is false. So this is a nonsense display and an invalid intermediate state
rather than a data error. The wrap-around itself is correct for the non-empty case - only the empty
list is unguarded.

**Fix applied.** Page arithmetic moved to `Assets/Level5/Core/StatsPaging.cs`. `PageCount` returns at
least 1, so an empty table reads "page 1 / 1" and the wrap-around always has a valid page to land on;
`NextPage`/`PreviousPage` clamp their input before wrapping, so an already-invalid page number is
brought back into range rather than propagated; `OffsetFor` never returns a negative offset. Applied
to the online paging as well, which had the same defect. Covered by six tests.

### AUD-042: `ResultsPerPage` exists but the paging math hardcodes 10 (Low)

`StatsManager.cs:38` declares `const int ResultsPerPage = 10;`, and then the page arithmetic uses
the literal `10` in six places (`:982`, `:984`, `:988`, `:1037`, `:1039`, `:1043`, and the online
equivalents), as does `DBHelper.cs:1523` (`pageNumber * 10`) and the `LIMIT 10` inside
`BuildSqlQueryForGetHighScoreRows`.

Changing the page size means finding all of them, and a miss desynchronises the page count from the
rows per page - the same failure AUD-040 already produces by a different route. Same shape as
AUD-036, and the same fix: one constant, referenced everywhere, including as a bound parameter in
the SQL rather than a literal.

**Fix applied.** `StatsPaging.ResultsPerPage` is the only definition. `StatsManager`'s local
constant now derives from it, the SQL takes `LIMIT @limit` as a bound parameter rather than a
literal, the offset comes from `StatsPaging.OffsetFor`, and the short-page padding uses it too. No
bare `10` remains anywhere in the stats query path.

### AUD-043: `getNumberOfResults` takes a `pageNumber` it never uses (Low)

`Assets/Scripts/database/DBHelper.cs:1694`

The parameter is accepted and ignored - the count query has no `LIMIT`/`OFFSET`. `StatsManager.cs:845`
dutifully passes `localResultsPageNumber`, which reads as though the count were page-scoped. Harmless
today, but it is exactly the kind of signature that invites someone to "fix" the count by honouring
it. Drop the parameter.

(The same query also carries an `ORDER BY` on a `COUNT(*)`, which does nothing.)

**Fix applied.** The parameter is gone and the `ORDER BY` is off the `COUNT`. `field` is still passed
and still validated through `RequireSqlIdentifier` even though the count no longer interpolates it,
so an invalid field is rejected by both queries identically rather than only by one of them.

### Checked and found sound in this pass

- **`StartManager` selection indices.** All four are bounds-checked against their list counts before
  use (`:701-716`), and the wrap-around decrements handle 0 correctly - unlike AUD-041.
- **`PlayerController`.** `currentState` is per-instance (already confirmed in pass three). The
  special-attack gate compares `PlayerHealth.Special == PlayerHealth.MaxSpecial`, a float against an
  int, which is fragile in principle - but `special` only ever changes by whole units and is clamped
  to an integral `maxSpecial`, so the value is exactly representable and the comparison holds. Noted
  rather than filed.
- **`RequireSqlIdentifier`.** The two places that interpolate a column name into SQL
  (`getNumberOfResults`, `BuildSqlQueryForGetHighScoreRows`) both sanitise it first; every value is
  a bound parameter. No injection path found in the stats queries.

### Still not read end to end

*(Closed by the [sixth pass](#sixth-pass---2026-08-06-the-unread-controllers), which read all of
these. The `ProgressionManager` lookups became AUD-047; `AutoPlayerController` turned out not to be
clean - the sampling here missed AUD-050, AUD-051, and AUD-055, which is a fair warning about how
much pattern-sampling a large file can survive.)*

`ProgressionManager` carries 20 unchecked `GameObject.Find(...).GetComponent<T>()` chains - the same
pattern AUD-028 fixed in `GameRules` and `Pause`, and the same fix would apply, including adding its
object names to `Level5ProjectValidator`. It is a known, bounded piece of work rather than an
unknown, so it is recorded here rather than filed as a separate finding.

`AutoPlayerController` and `RacingVehicleController` were sampled for the patterns this audit has
been tracking (shared statics, unguarded lookups, roll helpers) and came back clean, but neither has
been read line by line.

## Fifth Pass - 2026-08-06 (combat ownership and account identity)

Re-reviewed the stats-paging changes, then opened `PlayerAnimationEvents` and the account/identity
layer (`LocalAccount`, `UserAccountManager`), none of which any earlier pass had read.

### Self-review catch: parameterised SQL LIMIT

The AUD-042 fix introduced `LIMIT @limit` as a bound parameter. SQLite accepts a parameter there, but
this codebase has no precedent for it - `OFFSET @offset` is long-standing, a bound `LIMIT` is not -
and it is not something I can verify without running the driver. Since `ResultsPerPage` is a
compile-time `int` constant, concatenating it carries no injection risk and keeps the single
definition, so the `LIMIT` is now inlined and the parameter dropped. Bound parameters still carry
every value that is not a compile-time constant.

Committed alongside the AUD-044/AUD-045 fixes.

### AUD-044: `PlayerAnimationEvents` drives player 1 regardless of which actor it is on (Medium)

`Assets/Scripts/player/PlayerAnimationEvents.cs:61`

```csharp
playerController = GameLevelManager.instance.players[0].playerController;
```

This is a per-actor component - it lives on the player's animated rig, resolves its own attack box
with `transform.Find`, and reads `transform.root` for the capsule collider. But it resolves the
controller it acts through from `players[0]`, so every instance drives player 1.

For a second human player that means:

- `enableAttackBox` / `disableAttackBox` gate on `playerController.CurrentState`, so **player 1's**
  animator state decides whether player 2's attack box is live (`:117-129`).
- The lunge events call `playerController.RigidBody.AddForce(...)` (`:200-232`), so player 2's attack
  animation shoves **player 1's** rigidbody across the scene.
- Direction comes from `playerController.FacingRight`, so player 1's facing picks the direction.

The sibling scripts show what the intended pattern is: `EnemyAnimationEvents` and
`BodyGuardAnimationEvents` both resolve through `transform.root`, not through a global. Only the
player copy reaches for `players[0]`.

Invisible in single-player, where `players[0]` is the only human - which is why it has survived. Same
ownership class as AUD-024 and AUD-025, and this one is in the combat path.

Fix: `GetComponentInParent<PlayerController>()` (or `transform.root.GetComponent`), matching the
enemy and bodyguard scripts, with the `players[0]` lookup kept only as a fallback if that misses.

**Fix applied.** `PlayerAnimationEvents` now resolves its controller with
`GetComponentInParent<PlayerController>()`, falling back to a root search, matching what the enemy
and bodyguard copies already did. A missing controller logs a named error instead of silently
acting on the wrong actor.

The four `applyForce*` animation events gained a shared `CanApplyForce()` guard. Animation events
are invoked by the Animator, so they can fire before `Start` has resolved the controller or after
the actor is torn down - previously that could not happen because the reference came from a global
that was always populated, and removing the global reintroduces the window.

### AUD-045: `userid != 0` is used as the "authenticated" test, and it is set before authentication (Medium)

`Assets/Scripts/account/LocalAccount.cs:65-67` and `:88-98`, against
`GameRules.cs:509` and `EndRoundMenuManager.cs:196`

Both score-upload sites gate on the same expression:

```csharp
if (savedLocally && !string.IsNullOrEmpty(GameOptions.userName) && GameOptions.userid != 0)
{
    StartCoroutine(APIHelper.PostHighscore(user));
}
```

That treats "we know a user id" as "we hold a session". Two paths set the identity without one:

1. **`LocalAccount.LoginButton`** writes `GameOptions.userName` and `GameOptions.userid` from the
   selected local account and *then* navigates to the login screen. Nothing has authenticated yet,
   and backing out of that screen leaves both set.
2. **`LocalAccount.LoginAsGuestCoroutine`** is more direct: on token failure it calls `ClearSession()`
   - which zeroes both - and then immediately re-sets them to the guest values. A *failed* guest
   login therefore ends with `userid = 74`, `userName = "guest"`, and no bearer token.

The gate then passes and the client posts a score. `SendJson` only attaches the header
`if (authenticated && !string.IsNullOrEmpty(bearerToken))`, so the request goes out with no
`Authorization` at all.

What that costs depends on the server, which I cannot check from here:

- If the endpoint enforces auth (it should - the call passes `authenticated: true`), the post 401s,
  `accepted` stays false, the row stays marked unsubmitted, and it retries forever. The user-visible
  symptom is "my scores never upload", with no error surfaced.
- If it does not enforce auth, this is score attribution to an account the client never proved it
  owns.

Client-side fix either way: gate on the session, not on the id. `APIHelper.BearerToken` already
exists; a `HasSession` property over it is the honest test, and `GameOptions.userid` should stop
doubling as an auth flag.

Related, lower severity: `UserAccountManager` hardcodes `guestUserid = 74` and
`guestPassword = "guest"` (`:20-22`). A shared guest credential in the shipped binary is readable by
anyone, so that account should be assumed writable by anyone. Worth confirming the server treats it
as untrusted rather than as a normal account.

**Fix applied.** `APIHelper.HasSession` (a token actually exists) is now the test for "may we call
an authenticated endpoint", replacing `GameOptions.userid != 0` at both score-upload sites.

A **third** upload path turned up while fixing it, which the finding had missed: the manual
"submit scores" button in `StatsManager.SubmitUnsubmittedScoresCoroutine` had no gate at all, and
`PostUnsubmittedHighscores` stamps every queued score with `GameOptions.userid`/`userName` before
sending. That is the path most likely to be hit in practice - a signed-out player pressing submit -
and it now reports "sign in to submit" instead of firing off an unauthenticated batch.

Rather than rely on three call sites staying correct, the guard is also enforced inside
`PostHighscore` and `PostUnsubmittedHighscores` themselves. `PostHighscore` treats a missing session
exactly like a failed submission - the row stays marked unsubmitted and retries once a session
exists - so the outcome matches the old 401 path without the doomed request.

Two things deliberately **not** changed:

- `LocalAccount` still sets `GameOptions.userName`/`userid` when an account is picked and when the
  offline guest falls back. Those are a local identity: `AccountManager` prefills the username from
  them and `CharacterProgressAccountId` scopes local progress by them, so clearing them would send
  offline progress to a nameless file. Both sites now carry a comment saying they are a selection,
  not a session.
- The hardcoded `guestPassword = "guest"` stands. A shared credential in a shipped binary cannot be
  made secret by client changes; the useful fix is server-side, treating that account as untrusted.
  Filing it as fixed here would be pretending.

Covered by two tests asserting that a cleared session does not report as authenticated and that a
guest identity with no token is not a session.

### Checked and found sound in this pass

- **`EnemyAnimationEvents`, `BodyGuardAnimationEvents`, `RacingAnimationEvents`** resolve their actor
  through `transform.root` and do not reach for a global. AUD-044 is confined to the player copy.
- **The other `players[0]` lookups** are correct: `TouchInputController`, `SniperManager`,
  `BasketBallShotMarker`, `Timer`, and `RandomEvents` are scene-level and genuinely mean "the local
  human player", not "whichever actor I am attached to".
- **Stats-paging fix.** `getListOfHighScoreRowsFromTableByModeIdAndField` has two call sites and
  neither passes the changed signature; only `getNumberOfResults` changed shape, and its single call
  site was updated. The result reader and the `SELECT` column list now derive from one predicate.

## Sixth Pass - 2026-08-06 (the unread controllers)

Aimed squarely at what the third and fourth passes recorded as **not read end to end**:
`ProgressionManager` (976 lines), `PlayerController` (1050), `AutoPlayerController` (807),
`RacingVehicleController` (807), plus `AutoPlayerDefense`. Ten findings, all fixed.

The theme is different from earlier passes. AUD-046 to AUD-048 are three separate cases of a rule
being expressed twice and the two copies disagreeing; AUD-049 to AUD-055 are almost all the same
"the twin file never got the fix" shape that AUD-035 first named - and it turns out to be the single
most productive pattern in this codebase.

### Re-review of the fifth-pass changes

No defects found. `APIHelper.HasSession` is checked at all three upload call sites *and* inside
`PostHighscore`/`PostUnsubmittedHighscores`; `PlayerAnimationEvents.CanApplyForce` covers all four
`applyForce*` events; `UserAccountManager.GuestUserid`/`GuestUsername` are public accessors over the
private consts, so the new tests compile against real API. The `LIMIT` inlining is correct -
`StatsPaging.ResultsPerPage` is a `const int`, so the concatenation has one definition and no
injection surface.

### AUD-046: the progression menu divides by an uninitialized XP-per-level field (Medium)

`Assets/Scripts/menu_progression/ProgressionManager.cs:89` and `:890-892` (pre-fix)

```csharp
[SerializeField] int experienceRequiredForNextLevel;   // no initializer
...
progressionState.Level = progressionState.Experience / experienceRequiredForNextLevel;
int nextlvl = ((progressionState.Level + 1) * experienceRequiredForNextLevel) - progressionState.Experience;
```

This is a **tenth** site of the XP curve, and AUD-036 missed it - the earlier sweep grepped for the
literal `3000`, and this file spells the constant as a serialized field instead. Two consequences:

1. **`progressionState.Experience` is an `int`** (`ProgressionState.cs:137`), so this is *integer*
   division. If the inspector value was never set, the field is 0 and this throws
   `DivideByZeroException` - not the `Infinity` a float divide would give. It is inside
   `initializePlayerDisplay`'s `try`, which does `Debug.Log("ERROR : " + e)` and returns, so the
   symptom is the progression screen's level/XP panel silently never updating, plus
   `GameOptions.characterObjectName` (assigned after the throw point) never being set.
2. Even when set correctly, it is a second definition of a curve `CharacterLevel` already owns. The
   start screen reads `CharacterLevel.FromExperience`; this screen read the inspector. They can
   disagree, which is exactly the failure AUD-036 was filed to prevent.

I could **not** determine the actual inspector value: this project serializes scenes in binary
(`ForceBinary`), so `level_00_progression.unity` cannot be grepped and the `ProgressionManager`
script GUID does not appear in any text-readable asset. So whether case 1 is live or latent is
genuinely unknown from outside Unity - worth checking on the first run.

**Fix applied.** The serialized field is gone. `ExperienceRequiredForNextLevel` is now a read-only
property returning `CharacterLevel.ExperiencePerLevel`, and the two arithmetic sites call
`CharacterLevel.FromExperience` / `CharacterLevel.ExperienceToNextLevel` - the same calls
`StartManager` makes, so the two screens cannot drift. Nothing outside this class read the property,
so dropping its setter breaks no caller. Covered by a new test asserting the two halves of the curve
stay consistent across the range.

### AUD-047: `ProgressionManager` initialization is an unchecked `GameObject.Find` chain (Medium)

`Assets/Scripts/menu_progression/ProgressionManager.cs:730-759` (pre-fix)

The fourth pass recorded this as "a known, bounded piece of work rather than an unknown" and did not
file it. Reading the file end to end, it is worse than the note implied, so it is filed now.

`getUiObjectReferences()` is 17 consecutive `GameObject.Find(name).GetComponent<T>()` calls, run from
`Awake` - the identical shape AUD-028 fixed in `GameRules` and `Pause`. Three things make it sharper
than the generic pattern:

- It runs **after** `instance = this` and after `StartCoroutine(getLoadedData())`, so a throw leaves
  a published static instance and a running coroutine attached to a half-built manager.
- It runs **before** `playerSelectedIndex = GameOptions.playerSelectedIndex`, so a throw silently
  resets the player selection to 0 as a side effect.
- Nine of the 17 names are bare literals (`"3accuracy"`, `"range"`, `"luck"`, ...) rather than the
  consts the rest of the file uses, so nothing could validate them.

Two related gaps in the same file:

- `saveChanges`, `cancelChanges`, and `ConfirmChangesInternal` all call
  `confirmationDialogueBox.SetActive(...)` with no null check, while `Awake` explicitly null-checks
  the same field. Because `RunProgressionAction` wraps the action in `try/catch`, a missing dialogue
  turned "save" into a silent no-op rather than a visible error.
- `InitializeDisplay` gates on `dataLoaded` alone and then indexes
  `playerSelectedData[playerSelectedIndex]` directly, while every user-driven path goes through
  `HasSelectedCharacterData()`. `playerSelectedIndex` comes from `GameOptions`, written by
  `StartManager` against a possibly different roster, and was never bounds-checked here.

**Fix applied.** The nine literals became consts; all 19 names now live in
`ProgressionManager.RequiredProgressionObjectNames`. `getUiObjectReferences` resolves through
`SceneObjects.Find<T>(name, missing, this)`, collects every missing name, reports them in one
message, and returns false - and `Awake` then disables the component and returns *before* publishing
work, rather than half-initializing. The dialogue toggles route through a null-guarded
`SetConfirmationDialogueActive`; `disableButtonsNotUsedForTouchInput` null-checks its button;
`InitializeDisplay` now gates on `HasSelectedCharacterData()`; and `playerSelectedIndex` is clamped
once when the roster arrives.

`Level5ProjectValidator.CollectGameplaySceneObjectErrors` now checks scenes containing a
`ProgressionManager` against that name list, so a rename fails the edit-mode suite (and therefore
CI) rather than the play session - the same gate `GameRules` and `Pause` already have.

### AUD-048: the shot-meter mode gate is inverted (Medium)

`Assets/Scripts/player/PlayerController.cs:729` (pre-fix)

```csharp
if (!GameOptions.battleRoyalEnabled || !GameOptions.EnemiesOnlyEnabled)
{
    Shotmeter.MeterStarted = true;
    Shotmeter.MeterStartTime = Time.time;
}
```

`!A || !B` is false only when **both** A and B are true. The intent - visible in every other
shooting gate in the same file, which use `&&` (`:456`, `:486`) - is "not in a mode where shooting
is disabled". So in a plain battle royal, or a plain enemies-only run, the condition is true and the
shot meter starts anyway. It only correctly suppresses when the two modes are combined, which is the
one case the author was least likely to be testing.

The shot meter starting in a mode with no shooting means `MeterStarted`/`MeterStartTime` are live
while `PlayerShoot` is unreachable, so the meter runs to its timeout every jump.

**Fix applied.** `&&`, matching the other gates in the file. This is the De Morgan inverse, so the
combined-mode case behaves as before and the two single-mode cases now suppress as intended.

`EnemySpawner.cs:71` carries the same `!A || !B || C` shape when choosing `maxNumberOfEnemies`, and
its effect is to make the `cageMatchEnabled` branch below it nearly unreachable. Deliberately **not**
changed: unlike the shot meter, correcting it changes how many enemies spawn per mode, which is a
tuning decision rather than a defect fix. Recorded under Follow-Up.

### AUD-049: `&&` binds tighter than `||` in the movement-speed rule (Low)

`Assets/Scripts/player/PlayerController.cs:404-406` and `AutoPlayerController.cs:321-323` (pre-fix)

```csharp
if (currentState == idleState || currentState == walkState || currentState == bIdle
    && !InAir
    && !KnockedDown)
```

The indentation says the two guards apply to all three states. C# says otherwise: this parses as
`idle || walk || (bIdle && !InAir && !KnockedDown)`, so idle and walk reset `movementSpeed` to ground
speed regardless of being airborne or knocked down.

Currently masked by ordering rather than by correctness - the `if (InAir)` block ~20 lines later
overwrites `movementSpeed` with the in-air value, and knockdown is gated out of the movement path in
`FixedUpdate`. It is a guard that does nothing, in the exact form that will start mattering the
moment those speed rules are reordered.

**Fix applied.** Parenthesised in both files. Because the later `InAir` assignment already won, this
is not expected to change felt movement - but it is a movement change in principle, so it belongs in
the playtest list.

### AUD-050: the CPU overwrites its own navigation target and reads the wrong movement axis (Medium)

`Assets/Scripts/player/AutoPlayerController.cs:515-520` and `:204-206` (pre-fix)

Two defects in one data path.

```csharp
public void moveToPosition(Vector3 target)
{
    targetPosition = (target-transform.position).normalized;   // field, not a local
    movement = targetPosition * (movementSpeed * Time.deltaTime);
    rigidBody.MovePosition(transform.position + movement);
}
```

`targetPosition` is the serialized field holding the CPU's destination, and it is called as
`moveToPosition(targetPosition)` - so the method overwrites the destination it was just handed with
a **unit direction vector**. `Update` recomputes it from `getClosestPositionMarker()` each frame,
which is why this mostly works. But `FixedUpdate` can run more than once between `Update` calls, and
on the second run the field is a point near the origin: the CPU steers toward roughly world zero for
that step. The bug is therefore frame-rate dependent and shows up as CPU players stuttering or
drifting under load, which is close to the least debuggable symptom available.

Directly above, in `FixedUpdate`:

```csharp
movementHorizontal = movement.x;
movementVertical = movement.y;                              // y is height, not depth
movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.fixedDeltaTime);
```

`movement` is world-space, so its depth component is `z`. This reads the **height** component and
writes it back as depth, and rescales the whole vector by `speed * dt` a second time on top of the
scaling `moveToPosition` already applied. The pair feeds `IsWalking(horizontal, vertical)`, which
picks the walk animation and calls `Flip()` - so CPU facing and animation were driven by a scrambled
vector. The rim sits above the floor, so the direction `moveToPosition` computes has a real non-zero
`y`; this is not a case where the wrong axis happens to be zero.

**Fix applied.** `moveToPosition` uses a local for the direction and no longer writes the field, so
`getClosestPositionMarker` is the only writer of `targetPosition`. `FixedUpdate` reads `movement.x`
and `movement.z`, and the self-referential rescale is gone.

One thing that surfaced while fixing it and is worth recording: that rescale, wrong as it was, also
*decayed* `movement` toward zero on frames where no step was taken, which is what eventually made
`IsWalking` report "not moving" and let the idle animation play. Simply deleting it would have left
the last step's vector latched forever and kept the walk animation running while the CPU stood still.
`FixedUpdate` now zeroes `movement` explicitly when no step is taken, which preserves that behaviour
by intent instead of by accident.

### AUD-051: two animator state hashes are missing the `base.` prefix (Low)

- `Assets/Scripts/player/PlayerController.cs:616-617`
- `Assets/Scripts/player/AutoPlayerDefense.cs:226-227`
- `Assets/Scripts/player/AutoPlayerController.cs:499-500`

All three files hash `"inair.inair_hasBasketball_front"` / `_side`, while every other hash in the
same method carries the `base.` prefix (`"base.inair.basketball_shoot"`, and so on). Those hashes are
compared against `currentStateInfo.fullPathHash`, which is the *full* path - so the unprefixed values
can never match anything.

The tell is `AutoPlayerController`, the only one of the three that actually uses these two fields:
its `Start()` re-assigns both with the prefixed strings immediately after calling
`getAnimatorStateHashes()`. Someone hit the bug, fixed it at the call site, and left the shared
helper wrong. In `PlayerController` and `AutoPlayerDefense` the fields are assigned but never read,
so the wrong value is currently inert - and would silently evaluate to "never" for anyone who
started reading them.

**Fix applied.** The prefix added in all three `getAnimatorStateHashes` methods, and the now-redundant
re-assignment removed from `AutoPlayerController.Start`. `AutoPlayerController`'s behaviour is
unchanged (it was already getting the prefixed value); the other two are corrected before use.

### AUD-052: `Terrain.activeTerrain` dereferenced without the guard its twin has (Low)

- `Assets/Scripts/player/AutoPlayerController.cs:287`
- `Assets/Scripts/player/AutoPlayerDefense.cs:104`
- `Assets/Scripts/basketball/BasketBall.cs:128`

`PlayerController.cs:355` already does this correctly, falling back to
`GameLevelManager.instance.TerrainHeight` when there is no active terrain. The other three call
`Terrain.activeTerrain.SampleHeight(...)` directly, in `Update`, so a scene with no active Terrain
throws once per frame per instance.

**Fix applied.** The `PlayerController` guard and its `TerrainHeight` fallback, applied to all three.

### AUD-053: the HUD and `camera_flash` lookups are guarded only on the player side (Low)

The player-side copies of these lookups were hardened in earlier passes; their twins were not:

| Site | Missing guard |
| --- | --- |
| `AutoPlayerController.cs:169-185` | `damageDisplayObject` and its `Canvas`, guarded in `PlayerController:256` |
| `RacingVehicleController.cs:117-128` | the same pair |
| `PlayerController.cs:218`, `:960` | `player_damage_display_text`, `messageDisplay` |
| `RacingVehicleController.cs:734` | `messageDisplay` |
| `EnemyController.cs:425`, `BodyGuardController.cs:351` | `camera_flash`, inside the lightning coroutine |
| `BehaviorNpcCritical.cs:25` | `camera_flash` in `Start` |

The two coroutine cases are the worst of them: a throw inside `struckByLighning` abandons the rest of
the coroutine, which is what un-freezes the actor - so a missing `camera_flash` object leaves the
enemy or bodyguard permanently frozen mid-knockdown rather than just skipping a visual effect.

**Fix applied.** All of them route through `SceneObjects.Find`, which logs the name that failed and
returns null, and each call site checks before use. `PlayerAnimationEvents` already resolved
`camera_flash` this way - that was the model.

### AUD-054: `ShrinkPlayer` restores the camera FOV to a literal (Low)

`Assets/Scripts/player/PlayerController.cs:900-908` (pre-fix)

```csharp
float camFOV = CameraManager.instance.Cameras[0].GetComponent<Camera>().fieldOfView;
...
CameraManager.instance.Cameras[0].GetComponent<Camera>().fieldOfView = camFOV/2;
yield return new WaitForSeconds(10);
...
CameraManager.instance.Cameras[0].GetComponent<Camera>().fieldOfView = 50;   // not camFOV
```

The captured value is used to halve and then discarded; the restore hardcodes `50`. Any camera not
already at 50 is permanently retuned by shrinking once. The unused `originalFacingRight` local two
lines up suggests this block was edited and not finished.

**Fix applied.** Restores `camFOV`. The camera is resolved once into a local with a null check
(`CameraManager.instance`, the array, and element 0 were all dereferenced unguarded across a 10
second `yield`, during which a scene change can invalidate them), and the dead local is gone.

### AUD-055: `cpuShootSevenpointers` divides by an unguarded stat (Low)

`Assets/Scripts/player/AutoPlayerController.cs:461`

```csharp
float rangePercent = ((float)characterProfile.Range / characterProfile.Accuracy7Pt) * 100;
if (GameOptions.levelHasSevenPointers && (rangePercent > 70)) { returnValue = true; }
```

Float division, so `Accuracy7Pt == 0` yields `Infinity` rather than throwing - and `Infinity > 70`
is true. A character with no seven-point accuracy at all is therefore classified as a seven-point
specialist and will preferentially take the shot it is worst at. Same class as AUD-037, and the same
reason it is invisible: the wrong answer is a plausible-looking `true`.

**Fix applied.** Returns false when `Accuracy7Pt <= 0`, before the division.

### AUD-056: two enemy-count branches are dead, and battle royal spawns double (Medium)

`Assets/Scripts/enemy/EnemySpawner.cs:71`

Filed 2026-08-07, after building the branch table the sixth pass had deferred as "a tuning decision".
The table showed it is not a tuning decision - it is dead code.

```csharp
else if (!GameOptions.battleRoyalEnabled || !GameOptions.gameModeHasBeenSelected || GameOptions.enemiesEnabled)
{ maxNumberOfEnemies = 4; }
else if (GameOptions.cageMatchEnabled) { maxNumberOfEnemies = 4; }
else                                   { maxNumberOfEnemies = 2; }
```

`StartManager.setGameOptions` contains:

```csharp
// if enemies only mode, enable enemies whether it was selected or not
if (GameOptions.EnemiesOnlyEnabled || GameOptions.battleRoyalEnabled)
{
    GameOptions.enemiesEnabled = true;
}
```

So `battleRoyalEnabled` **implies** `enemiesEnabled` on every menu path, which makes the third
disjunct always true whenever the first two are false. The two branches below it can never run.
`GameLevelManager.cs:299` forces the same thing on the dev/direct-load path, so there is no route
that reaches them.

Effect: a non-cage battle royal spawns **4** enemies where the branch order says **2**. Cage match
still gets 4, but via the wrong branch - so the `cageMatchEnabled` test has never once executed.

**Fix applied.** The `|| GameOptions.enemiesEnabled` disjunct is gone. The `!battleRoyalEnabled ||
!gameModeHasBeenSelected` test remains, so non-battle-royal modes and the unconfigured fallback are
unchanged; cage match and battle royal now reach their own branches. This changes spawn counts for
one mode and wants a playtest.

### Checked and found sound in this pass

- **`RunProgressionAction`.** The `buttonPressed` / `lastActionFrame` pair correctly prevents both
  re-entrancy and double-firing when a button is driven by a click and a key in the same frame, and
  `buttonPressed` is reset in a `finally`.
- **`CharacterProgressionService.CommitDraft`.** Captures all eight profile fields before writing and
  restores every one of them when `UpdateCharacterProfile` fails. A genuine transaction; no partial
  commit path.
- **`PlayerControlsProvider` acquire/release in `PlayerController`.** `OnEnable`/`OnDisable` are
  balanced through `hasStarted`, and `inputPlayerId` is reset to -1 on release so a re-enable cannot
  double-release.
- **`AutoPlayerController` defensive-player null handling.** `basketball` and `gameStats` are
  deliberately null for a defensive player, and every dereference of either is behind an
  `isDefensivePlayer` check earlier in the same `&&` chain. Checked all of them.
- **`getClosestPositionMarker` shot selection.** The three overlapping `if` blocks are order-dependent
  rather than exclusive, but the final `targetPosition == Vector3.zero` fallback means no branch
  combination leaves it unset. Awkward, not wrong.

## What To Do Next

All thirty-four findings are fixed in code. The remaining work is verification, in this order:

1. **Open the project in Unity and let it compile.** New files under `Assets/Level5/Core`,
   `Assets/Scripts/Utility`, `Assets/Scripts/combat`, and `Assets/Tests/Editor` have hand-written
   `.meta` files with fresh GUIDs; Unity will accept them and import normally.
2. **Run the edit-mode tests** (42 now). `Level5CombatMathTests` should pass outright.
   `Level5SceneContractTests` may legitimately fail if a gameplay scene is missing a HUD,
   pause-menu, or (new) progression-menu object - read the names it reports before changing
   anything.
3. **Run `Level5/Validate Project`.** The contest-mode timer check may legitimately fail if a
   contest prefab never set `CustomTimer`; that is real data to fix, not a broken check.
4. **Open the progression screen** (AUD-046). This is the highest-value single check in the sixth
   pass: the level / XP / "to next level" panel should now populate. If it was blank or stale
   before, the uninitialized divisor was live rather than latent, and the same run confirms the
   level shown here matches the start screen's for the same character.
5. **Playtest CPU players** (AUD-050). Their movement, walk animation, and facing all changed. Watch
   for stutter or drift under load, which is what the frame-rate-dependent target clobber produced.
6. **Playtest a battle royal and an enemies-only run separately** (AUD-048). The shot meter should
   no longer start on jump in either.
7. **Playtest a scene with two or more bodyguards** (AUD-023). The shared-state fix is a keyword
   change, but behaviour may have been tuned around the old value.
8. **Playtest local multiplayer with the UI-stats overlay toggled on** (AUD-024). Confirm each
   player's shot counts stay on their own stat line.
9. **Check a contest mode's clock** (AUD-034). It should run at its prefab's `CustomTimer`, and a
   mode with none should run at 180s regardless of component start order.
10. **Decide whether the progression curve needs rebalancing** now that the phantom +500 XP per
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
- `Level5.Core` now holds six pure modules (`CampaignRoundDecision`, `PercentChance`,
  `MatchExperience`, `CharacterLevel`, `MatchClock`, `StatsPaging`). It is proving to be a good home
  for rules that were previously duplicated across MonoBehaviours - worth continuing to pull into as
  AUD-010 and AUD-017 get addressed.
- **`EnemySpawner.cs:71`** decides `maxNumberOfEnemies` with
  `!battleRoyalEnabled || !gameModeHasBeenSelected || enemiesEnabled`, which is true for almost every
  mode and therefore makes the `cageMatchEnabled` branch below it nearly unreachable. It is the same
  De Morgan shape as AUD-048 but, unlike the shot meter, fixing it changes spawn counts per mode -
  a tuning decision that needs a design call and a playtest, not a quiet correction.
- **`PlayerController`/`AutoPlayerController` are now the clearest duplication case in the tree.**
  Four of the ten sixth-pass findings (AUD-049, AUD-051, AUD-052, AUD-053) existed only because a
  fix landed in one copy and not the other, and AUD-035 was the same story for the collision
  handlers. The `Update` speed rules, `IsWalking`, `Flip`, `CheckIsPlayerFacingGoal`, and all four
  freeze/knockdown coroutines are near-identical across `PlayerController`, `AutoPlayerController`,
  and `RacingVehicleController`. A shared base or component would stop the next fix needing to be
  applied three times.
- **`AutoPlayerController` dead initialization.** `Start` assigns `anim` twice (from
  `playerIdentifier.autoPlayer`, then from `GetComponentInChildren`) and `movementSpeed` twice
  (from `characterProfile.Speed`, then from the uninitialized `runMovementSpeed`); the second of each
  wins. `attackSpeed`, `walkMovementSpeed`, `attackMovementSpeed`, `playerCanAttack`, and
  `playerCanBlock` are declared and never read, and `PlayerJump()` is dead (`AutoPlayerJump()` is
  what runs). Left alone here because untangling which assignment was intended needs the inspector
  values, which binary scenes do not expose.
