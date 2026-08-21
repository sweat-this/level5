# Phase 1b — Match-stats owner behind a `GameStats` facade

Last updated: 2026-08-18 (revised after adversarial review — see
[the accumulation contract](#the-accumulation-contract) and risk 4)

Slice 1b of [the systems restructure](systems-restructure-plan.md). Phase 1a introduced
`ShooterAttributes` — the inbound half of the shot seam — and migrated nothing onto it. This slice
does the same thing for match stats: the state gets a real owner in `Level5.Core`, `GameStats`
becomes a thin facade over it, and **no call site changes behaviour**. Phase 1c moves consumers.

The restructure plan names the risk this slice has to answer:

> Phase 1b assumes a `GameStats` facade can hold the line while consumers migrate. If scoring or win
> conditions read through it in ways the facade cannot preserve, stop and re-plan rather than push
> through.

That question is now answered with measurement rather than assumption. See
[What could still make this wrong](#what-could-still-make-this-wrong).

## What was measured

Taken 2026-08-18 against `refactor/phase1-cut-shot-cycle`, excluding `Legacy~`.
Commands in [the appendix](#appendix-how-these-were-measured).

`GameStats` is mentioned **147 times across 32 files**:

| Folder | Mentions | What it does there |
| --- | --- | --- |
| `game manager` | 31 | reads — HUD polling, win conditions, match save; 3 writes (`TimePlayed`, bonus) |
| `basketball` | 14 | the shot path — attempt and made counters, distance, crit |
| `menu_end_round` | 10 | campaign win/loss/tie tallies, and a destroy/re-add cycle |
| `database` | 7 | all-time stats read and save |
| `versus` | 6 | reads only, into an immutable `AttemptResult` |
| `player` | 6 | `PlayerIdentifier.gameStats` per-player hub, plus block credit |
| `combat` | 4 | kill credit |
| `player_racing` | 3 | component lookup |
| `analytics` | 2 | reads |
| `Models` | 2 | reads, into `HighScoreModel` |

### Four facts that shape the design

**1. Zero authored serialized values.** Six assets carry a `GameStats` component —
`basketball.prefab`, `basketballAuto.prefab`, `GameManager.prefab`, `level_17_rumble_pit.unity`,
`level_18_aveb2.unity`, `minigame_racing.unity`. Every serialized field in all six is at its default.
The only non-zero entries are `_expectedShotMade: 1` and `_expectedShotAttempts: 1`, which are C#
field initializers, not authored tuning.

This is the fact that de-risks the slice. The component holds no authored data, so restructuring how
it serializes cannot lose any.

**2. The stats logic's entire dependency on `basketball` is one bool.**
`ApplyMadeShot(BasketBallState, ShotScoringInput)` passes `basketBallState` to exactly one place —
`calculateConsecutiveShot` — which reads exactly one member: `TwoAttempt`. Nothing else in
`GameStats` touches a scene type. The owner's signature is therefore
`ApplyMadeShot(bool wasTwoPointAttempt, ShotScoringInput)`, and that is the whole seam.

**3. `BuildExperienceInput()` cannot move.** It reads `MatchRuntime.Rules`, and `MatchRuntime` is a
static in `Assets/Scripts/game manager/` — Assembly-CSharp, which `Level5.Core` cannot reference. It
stays on the Assembly-CSharp side of the boundary, exactly as `ShooterAttributesFactory` does for 1a.
This is a constraint, not a defect: it is the boundary doing its job.

**4. `GameStats` is one type doing three unrelated jobs.**

| Role | Where | Lifetime |
| --- | --- | --- |
| Live per-player match stats | component on the basketball prefabs | the match |
| Campaign accumulator | `PlayerData.campaignGameStats`, `AddComponent` at runtime | the campaign |
| All-time stats read from SQLite | `DBHelper.getAllTimeStats`, `AddComponent` then `Destroy(prevStats, 5)` | a DTO |

Roles two and three are plain data records wearing a `MonoBehaviour`. They need a `GameObject` only
because the type they reuse happens to be a component. One record type serves all three, because
campaign and all-time totals are accumulations of the same field set — `PlayerData.updateCampaignStats`
is 23 lines of hand-written `+=` proving it.

## The design

### `Level5.Core.Match.MatchStats`

A plain `[Serializable]` C# class holding the counters and the logic that maintains them.

`Level5.Core` already uses `[SerializeField]` in 12 files (`GameModeDefinition`, `LevelDefinition`
and others), so this follows the assembly's existing convention rather than introducing anything.
Marking it `[Serializable]` with `[SerializeField]` private backing fields means **Unity serializes
it inline on the facade** — the inspector still shows live stats during play, from one owner, with no
mirrored second copy of the state. `AGENTS.md` rejects duplicate state across owners; this avoids it
while keeping the debugging affordance.

Members, all moved from `GameStats` unchanged:

- the counters — points, per-line made and attempts, money ball, distance, longest, streak, crit,
  kills, sniper, time played, campaign win/loss/tie tallies;
- `ApplyMadeShot(bool wasTwoPointAttempt, ShotScoringInput)` — the AUD-065 ordering, verbatim;
- `CalculateConsecutiveShot(bool wasTwoPointAttempt)`;
- `TotalPointAccuracy`;
- `Accumulate(MatchStats session)` — `updateCampaignStats`, moved into the owner and tested. See
  [the accumulation contract](#the-accumulation-contract) — it is not "max two, sum the rest".

### The accumulation contract

An earlier draft of this plan described `Accumulate` as "`LongestShotMade` and `MostConsecutiveShots`
take the max; everything else sums". **That is not what `PlayerData.updateCampaignStats` does.** Its
23 lines silently omit nine fields, and summing some of them would be actively wrong — the
`*LowTime` fields are best-times, so a sum is meaningless. Implementing the earlier description would
have changed campaign behaviour inside a slice whose premise is that no call site changes behaviour.

The contract `Accumulate` must reproduce, enumerated explicitly so that adding a field later forces a
decision instead of inheriting an assumption:

| Rule | Fields |
| --- | --- |
| **SUM** | `TotalPoints`, `TotalDistance`, `TwoPointerMade`, `ThreePointerMade`, `FourPointerMade`, `SevenPointerMade`, `TwoPointerAttempts`, `ThreePointerAttempts`, `FourPointerAttempts`, `SevenPointerAttempts`, `MoneyBallMade`, `MoneyBallAttempts`, `ShotMade`, `ShotAttempt`, `TimePlayed`, `CriticalRolled`, `EnemiesKilled`, `BossKilled`, `MinionsKilled`, `SniperHits`, `SniperShots` |
| **MAX** | `LongestShotMade`, `MostConsecutiveShots` |
| **NOT ACCUMULATED** — omitted today; preserved as an omission, not fixed | `ExperienceGained`, `BonusPoints`, `blockedShots`, and all six `Make*LowTime` fields |
| **DERIVED** — never stored | `getTotalPointAccuracy()`, and the four private streak-prediction fields |

Whether the `*LowTime` and `BonusPoints` omissions are bugs is a scoring question. It is not answered
here. `Accumulate` reproduces the omission, and the test suite names it, so changing it later is a
deliberate diff.

### `GameStats` stays, as a facade

```csharp
public class GameStats : MonoBehaviour
{
    [SerializeField] private MatchStats _stats = new MatchStats();

    // every delegating member goes through this, not through _stats directly, so a component
    // deserialized from an asset authored before _stats existed cannot surface as a null deref
    public MatchStats Stats => _stats ??= new MatchStats();

    public int TotalPoints { get => Stats.TotalPoints; set => Stats.TotalPoints = value; }
    // ...every existing member, delegating

    // keeps the BasketBallState overload so no caller changes in this slice
    public ShotScore ApplyMadeShot(BasketBallState state, ShotScoringInput input)
        => Stats.ApplyMadeShot(state.TwoAttempt, input);

    // stays here: reads MatchRuntime, which Level5.Core cannot see
    public MatchExperienceInput BuildExperienceInput() { ... }
}
```

Every one of the 147 mention sites keeps compiling and behaving identically. `Stats` is the seam 1c
migrates consumers onto, one at a time.

**The external surface is not all properties.** Two-thirds of the state is public *fields*, and the
facade has to reproduce the surface as it actually is rather than as "every existing property":

- The 31 public `_underscore` fields have **zero external readers** — swept, and only `GameStats`
  itself touches them. They are deleted outright, not delegated.
- `campaignWins`, `campaignLosses`, `campaignTies`, `campaignGamesPlayed` are public fields **with
  external call sites**, including `++` in `EndRoundMenuManager` and reads in `HighScoreModel` and
  `DBHelper`. Field → property is source-compatible for every one of those uses (nothing passes them
  by `ref`/`out`), but it is an API shape change, and it is named here rather than discovered.
- `getTotalPointAccuracy()` is a **method** with five call sites, not a `TotalPointAccuracy`
  property. The owner exposes the property; the facade keeps the method name.
- `calculateConsecutiveShot(BasketBallState)` has no external callers, but stays on the facade for
  the duration of 1b anyway — retiring it is 1c's decision, not a side effect of moving it.

### Three small things that will break the build or the diff if missed

- **`blockedShots { get; internal set; }`** — `CollisionCheckDefense` does `gameStats.blockedShots++`,
  which compiles today only because both types are in Assembly-CSharp. On the owner in `Level5.Core`
  that setter has to be public. Note it is currently an auto-property and therefore *not* serialized;
  as a backing field on `MatchStats` it becomes serialized. Harmless — it defaults to 0 — but it is a
  real change in what the six assets store.
- **The 40 serialized fields change shape.** They move from flat (`_totalPoints: 0`) to nested under
  `_stats:`. Six assets get rewritten. No data is lost — fact 1 — but the diff must be committed
  deliberately, and the three scenes among them re-opened to confirm Unity rewrote them cleanly.
  Unity discards unknown YAML keys on load, so an asset that is never re-saved keeps its stale flat
  keys harmlessly; "the old fields are gone" is a tidiness check, not a correctness gate.
- **`campaignGamesPlayed` has no `updateCampaignStats` line but does have a `++` call site.** It is
  incremented directly by `EndRoundMenuManager`, so it must survive the field → property change with
  a public setter even though `Accumulate` never touches it.

## Steps

Each step compiles and leaves the suite green on its own.

0. **Serialization preflight.** Done — see [what could still make this wrong](#what-could-still-make-this-wrong).
   The six component blocks hold only default values, no prefab-instance override anywhere in the
   repo names a stats field, and no `.anim`/`.controller`/`.asset` binds one. Recorded rather than
   repeated.
1. **Add `MatchStats` to `Level5.Core/Match/`, wired to nothing.** Counters, `ApplyMadeShot(bool, …)`,
   `CalculateConsecutiveShot(bool)`, `TotalPointAccuracy`, `Accumulate`. `GameStats` untouched.
2. **Add the owner's EditMode tests. Do not delete the existing ones.** The AUD-065 cases are
   *translated* onto `MatchStats`, not moved: `Level5GameStatsApplyMadeShotTests` needs two
   `GameObject`s and an `AddComponent` per test to exercise pure counter arithmetic, and against
   `MatchStats` it needs neither — but the old suite is what proves the facade still behaves once
   step 3 lands. Both suites pass here and after step 3; that is much stronger evidence than moving a
   suite from one implementation to the other. Plus full `Accumulate` coverage against the contract
   above.
3. **Re-point `GameStats` to delegate.** Every member forwards through `Stats`; `ApplyMadeShot` keeps
   its `BasketBallState` overload; `BuildExperienceInput` stays; `getTotalPointAccuracy()` keeps its
   method shape; the four `campaign*` fields become properties. `blockedShots` setter to public.
4. **Add the facade-contract tests.** A hand-written delegating property is invisible to the
   compiler when it is wrong — `get => Stats.TotalPoints; set => Stats.TotalAttempts = value;`
   compiles, passes every `MatchStats` test, and breaks the game. Rather than 40 duplicate
   hand-written tests, one reflection-driven round-trip over every writable property on `GameStats`
   (write a distinct value, read it back, assert it landed) covers the whole class of error, plus
   explicit cases for the behavioural members and a two-instance isolation assert.
5. **Commit the serialized-asset pass.** Six assets, values confirmed still default.
6. **Move `updateCampaignStats` onto `Accumulate`.** `PlayerData.updateCampaignStats(GameStats)`
   becomes a one-line call through the facade. This is the one consumer that moves in 1b, because it
   is 23 lines of untested arithmetic that the owner makes testable, and because it has no scene
   dependency to negotiate. It reproduces the omissions in [the contract](#the-accumulation-contract)
   exactly.
7. **Validate.** Below.

## Invariants asserted

The restructure plan requires each phase to land with its invariant in the validator or a test. For
this slice the strongest one is free:

- **Build-time: the owner cannot reach into a scene.** `Level5.Core` is a separate assembly that
  cannot reference Assembly-CSharp. Any future attempt to make the stats owner read a
  `MonoBehaviour`, a `GameRules` singleton or `MatchRuntime` is a compile error, not a play-session
  surprise. This is exactly the "contracts that fail at build time" the plan asks for, and it needs
  no new tooling.
- **EditMode: `MatchStats` tests construct no `GameObject`.** A test that has to spin up a scene
  object to check a counter is the signal that ownership regressed.
- **EditMode: parity, at two layers.** The AUD-065 ordering tests run against both `MatchStats` and
  the `GameStats` facade, and stay that way until 1c retires the facade. The owner suite proves the
  logic; the facade suite proves the 147 existing call sites still reach it. Neither substitutes for
  the other.
- **EditMode: the facade delegates to one owner.** Every writable property on `GameStats` round-trips
  through `Stats`, and two `GameStats` instances never share a `MatchStats`.
- **EditMode: `Accumulate` matches [the contract](#the-accumulation-contract) field by field**,
  including the fields it deliberately does not accumulate.

`scripts/validate-repository.ps1` is 50 lines and only checks forbidden tracked paths, so it is not
the right home for this; the compiler and the EditMode suite are.

## Preserved oddities carried in this slice

Named here so that changing any of them later is a visible decision, per
[the shot lifecycle](shot-lifecycle.md).

- **The AUD-065 ordering.** `calculateConsecutiveShot` must run after `ShotMade` is finalized and
  before `ResetShotAttemptSnapshot` clears `TwoAttempt`, and `ApplyMadeShot` deliberately scores
  twice — once to learn which counter moves, again after the streak updates. Moving this code is the
  highest-risk edit in the slice; it moves verbatim, comments included.
- **A broken streak resets `_consecutiveShotsMade` to 1, not 0.** Both branches of
  `calculateConsecutiveShot` then set the same `_expected*` values, so the two branches differ only
  in that reset. It looks like a bug and is left exactly as it is.
- **The streak tracker is predictive.** It compares live totals against totals it predicted on the
  previous shot, rather than counting. Carried unchanged.
- **Two-point shots never extend a streak** (`&& !basketBallState.TwoAttempt`).

Untouched by this slice but adjacent, so worth stating: the contest-shot-off-marker rule and the
double money-ball credit live in `ShotScoring`, which 1b does not open.

## Validation

1. `./scripts/validate-repository.ps1`.
2. Compile under the version in `ProjectSettings/ProjectVersion.txt`.
3. EditMode: `Level5MatchStatsTests` and `Level5GameStatsFacadeTests` (new), the original
   `Level5GameStatsApplyMadeShotTests` (retained, unmodified), `Level5ShotScoringTests`,
   `Level5BasketballShotPipelineTests`, `Level5VersusIntegrationTests` — the last because
   `GameStatsAttemptResults` reads eight properties through the facade.
4. PlayMode: `Level5GameplayPlayModeTests`, which writes stats directly.
5. Re-open the three affected scenes and both basketball prefabs; confirm the component still
   inspects and every value is still default.

Play Mode, per the phase 1 row of the restructure plan's matrix — automation has not caught a feel
regression and will not:

- free play — attempt and made counters, points by distance;
- a marker contest — the marker `ShotMade` counter feeds `GameRules.IsGameOver()` directly, so a
  facade that dropped a write would hang the win condition rather than mis-score it;
- In The Pocket — the only mode where the streak bonus changes points, and so the only mode where the
  AUD-065 double-scoring is observable;
- CPU shooter — `basketballAuto.prefab` is a separate serialized asset;
- local multiplayer — two `PlayerIdentifier`s must still hold two distinct `GameStats`;
- end of round — campaign accumulation, step 6's only behavioural move, and the one place the
  corrected accumulation contract is observable;
- `minigame_racing` — it carries a `GameStats` in the scene and reaches it through
  `RacingGameManager`, a path nothing else exercises.

## Scope

**REQUIRED**

- `MatchStats` in `Level5.Core`, `GameStats` delegating, the serialized pass, owner tests.
- `blockedShots` setter visibility; the four `campaign*` fields as properties.
- The facade-contract test layer, retained alongside the original `GameStats` suite until 1c.

**HIGH-VALUE**

- `Accumulate` replacing `updateCampaignStats` (step 6). 23 lines of untested per-field arithmetic
  become testable, and it is the cheapest proof the owner is usable rather than just present. Note
  this is no longer a free step: the earlier draft's description of its contract was wrong, so it
  carries the slice's only real behaviour-preservation risk and needs the field-by-field tests.

**LATER**

- Migrating the 31 `game manager` reads, the HUD's per-frame poll, and the `versus`/`Models`/`database`
  readers. That is 1c, one consumer per verification pass.
- Retiring `GameStats` as a component. Possible once 1c finishes; not a 1b goal.
- Giving the all-time-stats DTO its own type instead of reusing the match record.

**REJECT**

- An `IMatchStats` interface. One implementation; `AGENTS.md` rejects abstractions ahead of a second.
- Converting the HUD off polling. A design decision the shot lifecycle doc already flags as needing
  its own pass, not a mechanical extraction.
- Fixing the streak-reset-to-1 oddity, or any other preserved behaviour, inside this slice.

## What could still make this wrong

The restructure plan's stated stop condition is scoring or win conditions reading through the facade
in a way it cannot preserve. Measurement says it can, for a specific reason: of 147 mentions, the
overwhelming majority are **property reads and writes**, which a delegating property preserves by
construction. Only two members are behaviour — `ApplyMadeShot` and `calculateConsecutiveShot` — and
both keep their current signature on the facade.

The residual risks, in order:

1. **The AUD-065 ordering is subtle enough to break while moving verbatim.** Mitigated by step 2:
   the tests are ported and passing against `MatchStats` *before* `GameStats` delegates to it.
2. **Unity's nested-serialization rewrite of three scenes.** Mechanical, but scenes are large and the
   diff needs reading rather than trusting. Mitigated by fact 1 — there is no authored value that a
   bad rewrite could silently drop.
3. ~~A reflection or string-based access to a serialized field name.~~ **Checked and clear.** A
   rename from `_totalPoints` to `_stats._totalPoints` would break such an access silently, so this
   was swept rather than assumed. Every string literal matching a stats field name
   (`DBConnector`, `StatsTableAllTime`, `PlayerData`) is a **SQLite column name**, not a serialized
   Unity field — the two vocabularies happen to overlap because the columns were named after the
   fields. `getAllTimeStats` maps by ordinal, and no code reflects over `GameStats` (the only
   `GetField` hits are third-party editor GUI helpers). Nothing reads the serialized field names, so
   changing their shape is safe.
4. ~~A prefab-instance override carrying an authored stats value outside the six component blocks.~~
   **Checked and clear.** Searching by script GUID finds components, not overrides: a scene can carry
   `m_Modifications: - propertyPath: _totalPoints, value: 5` on a `PrefabInstance`, and the GUID scan
   would never see it. Every distinct `propertyPath:` in every `.unity`, `.prefab` and `.asset` in
   the repo was extracted — 227 of them — and **none** matches any of the 40 stats field names. The
   same sweep over `.anim`, `.controller` and `.playable` for animated bindings is also empty. The
   six component blocks are the only serialized stats data that exists.

With 3 and 4 both closed, the claim "there is no state this slice can silently lose" is measured
rather than assumed, which was the weakest part of the earlier draft.

If any of these turns out worse than described, the stop condition applies: re-plan rather than push
through.

## Defects found while measuring

Not in scope. Recorded here with evidence, per `AGENTS.md`, rather than fixed inside a refactor.

- **`DBHelper.getAllTimeStats` returns a component it has scheduled for destruction.**
  `Destroy(prevStats, 5)` on the record it hands back, so a caller that holds the all-time stats
  longer than five seconds — across a menu transition, say — reads a destroyed component. Recommend
  a plain record with no `GameObject` and no timer. **Not `MatchStats`:** a type carrying
  `ApplyMadeShot` and predictive streak state is not the right shape for a row read out of SQLite by
  ordinal, and deciding otherwise here would turn today's extraction into tomorrow's universal stats
  object. The all-time DTO gets its own type, which is why it stays under LATER.
- **`CollisionCheckDefense` may dereference a null `GameStats` on every successful block.**
  `Start()` assigns `gameStats = autoPlayerDefense.playerIdentifier.gameStats`, which is populated
  only by `setBasketball`/`setAutoBasketball`. Those run from `SpawnCoordinator.GiveBall`, and a
  defensive CPU has no ball — `AutoPlayerController` line 182 nulls its own reference for exactly
  that reason. If `GiveBall` is not called for a defensive slot, `gameStats.blockedShots++` throws.
  Needs one Play Mode check with a CPU defender before being called a bug or dismissed.
- **`CollisionCheckDefense` credits the blocked shot to whoever `gameStats` resolves to as a
  `ShotAttempt`.** Whether a block should increment the *defender's* attempt counter is a scoring
  question, not a refactor question.
- **`CollisionCheckDefense.gameStats` is a dead `[SerializeField]`.** Unconditionally overwritten in
  `Start()`, so any inspector wiring is inert and misleading.
- **`EndRoundMenuManager` destroys and re-adds `GameStats` in the same frame.**
  `Destroy(PlayerData.instance.GetComponent<GameStats>())` followed immediately by
  `AddComponent<GameStats>()`. `Destroy` is deferred to end of frame, so between those two lines the
  `PlayerData` GameObject carries two `GameStats` components and `GetComponent<GameStats>()` still
  returns the doomed one. Latent today — nothing reads it in that window, because callers go through
  `CampaignGameStats` — but it sits directly on the campaign path step 6 touches, and it is the
  clearest evidence that the campaign accumulator should never have been a component. Retiring it is
  1c/LATER work; the fix is not attempted here.

## Appendix: how these were measured

```sh
# mentions per folder, comments excluded
for d in player "game manager" basketball database versus analytics combat \
         player_racing menu_end_round Models; do
  grep -rn "GameStats" --include=*.cs "Assets/Scripts/$d" \
    | grep -v "Legacy~" | grep -vE ":\s*//" | wc -l
done

# assets carrying the component, via its meta guid
grep -rl 179c8393904979c4faea16cf0061a011 Assets \
  --include=*.prefab --include=*.unity --include=*.asset

# authored values: dump the serialized block from each and confirm defaults
grep -n -A 45 179c8393904979c4faea16cf0061a011 \
  Assets/Resources/Prefabs/basketball/basketball.prefab

# non-default authored values across all six, in one pass
for f in <the six assets>; do
  grep -A 48 179c8393904979c4faea16cf0061a011 "$f" \
    | grep -E "^  _|^  campaign" | grep -vE ": 0$|: 0[.]0*$"
done
# -> only _expectedShotMade: 1 and _expectedShotAttempts: 1, in all six

# prefab-instance overrides: enumerate every propertyPath in the repo, then
# intersect with the stats field names. A GUID scan cannot see these.
grep -rhno "propertyPath: [A-Za-z_][A-Za-z0-9_]*" Assets \
  --include=*.unity --include=*.prefab --include=*.asset \
  | sed 's/.*propertyPath: //' | sort -u > paths.txt   # 227 distinct
grep -E "^(_totalPoints|_shotMade|...|campaignGamesPlayed)$" paths.txt   # -> empty

# animated or asset-bound field names
grep -rln "_totalPoints|_shotMade|campaignGamesPlayed" Assets \
  --include=*.anim --include=*.controller --include=*.asset --include=*.playable
# -> empty
```
