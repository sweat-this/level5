# Deep Audit - 2026-08-09

Scope: a fresh pass over `Assets/Scripts` (206 scripts, ~43.5k lines) plus the new
`Assets/Level5/Core/Versus` domain, deliberately aimed at ground the four earlier passes did **not**
cover: security of credentials at rest, Unity-specific null semantics, singleton teardown, per-frame
allocation, device/input assumptions, build contents, and assembly structure.

Everything below was traced to specific lines and checked against its callers before being written
down. **Three candidate findings were investigated and discarded** as false - they are listed in
[Checked and clean](#checked-and-clean) rather than dropped silently, because knowing what was
verified is as useful as knowing what broke.

IDs continue the register in [Architecture Audit](architecture-audit.md). Previous passes covered
AUD-001 to AUD-056; this one adds AUD-057 to AUD-063.

**All seven were fixed on 2026-08-09.** Code changes are described per finding under "Fix applied".
293 edit-mode tests and 4 play-mode tests pass. AUD-059 is fixed in the sense that mattered - gameplay
code is now testable and has tests - but its structural half, the assembly split, remains open.

A follow-up pass then analysed that split rather than deferring it, and found it is **blocked** on
breaking the `player` / `game manager` / `basketball` cycle - see
[why it is blocked](#the-assembly-split-why-it-is-blocked-aud-059-follow-up). That pass also added
AUD-064.

## Correction to this document

As first written, AUD-057 claimed the bearer token "is persisted to SQLite in plaintext" and rated it
High. Tracing the writers before fixing it showed that is **not true of the current build**: no
`INSERT` or `UPDATE` anywhere writes the `bearerToken` column. The accurate finding is narrower -
older builds wrote tokens there, so upgraded installs can still be carrying one at rest, and the
reader still hydrated it into memory on every user load. Severity corrected to Medium. The fix is
unchanged, and the exposure on existing devices was real.

## Summary

| ID | Area | Severity | Status | Problem |
| --- | --- | --- | --- | --- |
| AUD-057 | Credential security | Medium | Fixed | A stale bearer token from older builds sits at rest in SQLite and was still read back into memory. Passwords were deliberately scrubbed; the token was not. |
| AUD-058 | Dev scene in production | **Medium** | Fixed | A production gameplay collision drops the player into `level_23_dev`, which ships in the build. Confirmed live: `projectile_bullet_instantkill_enemy` sets the flag. |
| AUD-059 | Assembly structure | **Medium** | Fixed (partly) | `Assets/Scripts` is one 43.5k-line predefined assembly, which structurally prevented play-mode tests from covering any runtime gameplay code. Testing is unblocked; the assembly split itself remains open. |
| AUD-060 | Lifecycle / statics | Low | Fixed | 34 singletons never cleared their static on teardown. Latent today; live the moment it meets AUD-061. |
| AUD-061 | Unity null semantics | Low | Fixed | `?.` on a `UnityEngine.Object` bypasses Unity's destroyed-object check. Three sites, all safe by accident of placement rather than by design. |
| AUD-062 | Input / device assumptions | Low | Fixed | `SniperCameraController` caches `Gamepad.current` in `Start` and dereferences it unguarded every tick. Null on any device without a controller. |
| AUD-063 | Performance | Low | Fixed | `RacingVehicleController.Update` builds a four-part debug string into a live UI `Text` every frame. |
| AUD-064 | Dead code | Low | Fixed | `email/SendEmail.cs` is an entirely commented-out file with no references anywhere. Its dormant body carries a public plaintext password field. Deleted. |

---

## AUD-057 - Stale bearer token at rest, and a live one in global state (Medium)

**Where:** `database/DBConnector.cs:534,774` (schema), `database/DBHelper.cs:1174-1203` (read back),
`restapi/APIHelper.cs:47,384-388`, `menu_start/GameOptions.cs:123`, `Models/UserModel.cs:18`.

The `User` table carries a `bearerToken TEXT` column. `DBHelper.getUser...` reads it back into
`UserModel.BearerToken`, and `APIHelper` mirrors the live token into `public static
GameOptions.bearerToken`.

**Scope, corrected before fixing:** no `INSERT` or `UPDATE` in the current code writes that column,
so this build does not create the exposure. Older builds did - which is why the column exists and why
the reader handles a non-null value. The two live problems are therefore a **stale** token sitting at
rest on any upgraded install, which the reader faithfully hydrated back into memory on every user
load, and a **current** token copied into a `public static` that nothing reads.

What makes this a finding rather than a preference is the contrast sitting next to it.
`DBConnector.cs:551-554` runs:

```sql
UPDATE User SET password = NULL WHERE password IS NOT NULL
```

with the comment *"scrub any plaintext password persisted by older app versions"*. Someone correctly
decided a password must not sit at rest on the device. The bearer token is the credential that
password is exchanged **for** - it authenticates every `Authorization` header the game sends - and it
never got the same treatment. Whatever an old build left in
`Application.persistentDataPath/level5.db` is still sitting there in clear text.

**Impact.** On a rooted or jailbroken device, or through an unencrypted platform backup, a stale
token is readable and replayable for whatever remains of its server-side lifetime.

**Answered against `Level5Backend` on 2026-08-09**, which settles the severity:

- **Lifetime: 24 hours.** `TokenController` issues `expires: DateTime.UtcNow.AddDays(1)`, and
  expiry is enforced - `ValidateLifetime` defaults to true and nothing turns it off.
- **Revocation: none.** The token is a stateless JWT. There is no blacklist, no refresh token and
  no logout endpoint anywhere in the backend. The only way to invalidate one is rotating `Jwt:Key`,
  which invalidates every token for every player at once.

So a stolen token is usable for at most a day and cannot be cancelled - but a token sitting in an
old local database was written by a build that stopped writing them some time ago, and is therefore
long expired and worthless. **The residual risk is effectively nil, and the scrub is hygiene rather
than an incident.** Keeping the scrub is still right: it costs nothing, and it means the next person
to read this table does not have to re-derive any of the above.

Worth knowing rather than fixing: with no revocation, "log this account out everywhere" is not
something the backend can currently do. That is a product decision, not a defect.

**Fix applied.** `DBConnector.createDatabase` now scrubs the token alongside the password:
`UPDATE User SET bearerToken = NULL WHERE bearerToken IS NOT NULL`, so every device closes the
exposure on its next launch. `DBHelper` no longer reads column 9 into the model, `UserModel` no
longer has a `BearerToken` field, and `GameOptions.bearerToken` - a `public static` holding a live
credential that nothing ever read - is gone. The token now exists only in `APIHelper`'s private
static for the life of the session; callers ask `APIHelper.HasSession`.

No behaviour changes: nothing restored a session from the stored value, so there is no new login
prompt and no offline regression.

---

## AUD-058 - A production collision loads the dev scene (Medium)

**Where:** `player/PlayerCollisions.cs:109-112` -> `game manager/GameRules.cs:204-209` ->
`Utility/LoadGame.cs:34` -> `level_23_dev`.

```csharp
// PlayerCollisions.OnTriggerEnter
if (enemyAttackBox.isKilledOnIdle)
{
    GameRules.instance.killedOnIdle = true;
}
```

```csharp
// GameRules.Update
if (killedOnIdle && !killedOnIdleTransitionStarted)
{
    killedOnIdleTransitionStarted = true;
    StartCoroutine(LoadGame.LoadDevLevelVersus(5));   // loads level_23_dev
}
```

`level_23_dev.unity` **is** in `EditorBuildSettings`, so this path is live in a shipping build. Any
enemy attack box authored with `isKilledOnIdle` sends the player from a real arena into the developer
test level five seconds later.

This is not the same thing AUD-012 fixed. That pass isolated dev *scripts* and added a validator for
production→Dev references. The dev *scene* and the production code path that loads it were untouched,
and `LoadGame` is in `Assets/Scripts/Utility`, so no isolation test flags it.

**Impact: live, not dormant.** `Assets/Resources/Prefabs/projectile/projectile_bullet_instantkill_enemy.prefab`
sets `isKilledOnIdle: 1`. Being hit by that projectile in any shipped arena sent the player to the
developer test level five seconds later.

**Recommendation:** the flag has a second, legitimate job - it selects the "You dead bruh"
end-of-match display - so it is kept and the scene load removed, rather than deleting the concept.
Separately, extend the `Level5ProjectValidator` dev-isolation rule to cover scenes, not only
scripts; `level_23_dev` is still in the build settings and nothing stops another path loading it.

**Fix applied.** The dev-level load is gone from `GameRules.Update`; the death now runs the same
end-of-match path as any other. `killedOnIdle` is kept, because it is what makes the end-of-match
display read "You dead bruh" rather than a score summary - it is now presentation only, and says so.

**Deliberate behaviour change:** `TryStartCampaignTransition` used to skip when `killedOnIdle` was
set, because the dev-scene load handled leaving. With that load removed, keeping the skip would
strand a campaign run on the end-of-match screen with no way forward, so an instant-kill death now
advances the campaign like any other loss.

`LoadGame.LoadDevLevelVersus` is kept - it still has a legitimate caller in `Dev/DevFunctions`, which
AUD-012's isolation guard already covers.

---

## AUD-059 - One 43.5k-line assembly blocks all runtime testing (Medium)

**Where:** absence of `.asmdef` under `Assets/Scripts`; `Assets/Tests/PlayMode/Level5.PlayModeTests.asmdef`.

AUD-012 closed with a note that the assembly split was still open. This pass turns that from a
tidiness concern into a blocking one, because I hit it.

Everything in `Assets/Scripts` compiles into the predefined `Assembly-CSharp`. A Unity assembly
definition **cannot reference a predefined assembly** - that is a hard engine rule, not a
configuration gap. `Level5.PlayModeTests` therefore cannot see `GameRules`, `PlayerController`,
`GameLevelManager`, `VersusLauncher`, or any other runtime gameplay type, and it never will while
they live where they do.

The visible consequence: `Assets/Tests/PlayMode` contains exactly one test, which exercises
`CampaignRoundDecision` - a type in `Level5.Core`. Every edit-mode test the project has works only
because `Assembly-CSharp-Editor` auto-references everything. **There is no way to write a play-mode
test for gameplay code as the project is currently structured.** For a Unity project this is the
single largest testability constraint in the tree, and it is invisible until you try.

Secondary costs: any one-line script edit recompiles all 43.5k lines, and no layering is enforced -
`Assets/Level5/Core` is a clean domain assembly precisely because it *is* an asmdef, and nothing
stops a new script from reaching backwards.

**Recommendation:** the incremental path is to move leaf systems into asmdefs, lowest-dependency
first (`Utility`, `pooling`, `combat`, `database`), leaving the managers in `Assembly-CSharp` until
last. `Level5.Core` already proves the pattern works here. This is a substantial piece of work and
should be planned, not squeezed into another change.

**Fix applied, partly.** `playModeTestRunnerEnabled` is now on, and
`Assets/Tests/PlayModeGameplay/` holds play-mode tests in a folder with no asmdef - which is what
lets them compile alongside the code they test. The whole file is behind `UNITY_INCLUDE_TESTS` so
none of it can reach a shipping player build.

Three new tests exercise code that was previously unreachable from any test: a competitive turn
driven from a live `GameStats` component through `VersusMatchReporter`, a scene-scoped singleton
being destroyed and releasing its static, and an ordinary match reporting nothing. This also closes
the verification gap left by the versus work - that flow had never run outside edit mode.

**Still open:** the structural half. `Assets/Scripts` is still one assembly, so a script edit still
recompiles 43.5k lines and no layering is enforced. That is the AUD-012 asmdef split, and it is a
planned piece of work rather than something to fold into an audit-fix change. What is fixed is the
consequence that was blocking: gameplay code can now be tested, and now is.

---

## AUD-060 - Singletons do not clear their statics (Low, latent)

**Where:** 42 of the 48 files declaring `public static <T> instance`.

Only six clear the reference on teardown. `MatchController` and `LevelRuntimeContext` model the
correct shape:

```csharp
private void OnDestroy()
{
    if (instance == this)
    {
        instance = null;
    }
}
```

The other 42 - including `GameRules`, `Pause`, `Timer`, `StatsManager`, `ProgressionManager`,
`StartManager`, `CameraManager`, `PlayerHealthBar`, `RuntimeObjectPool`, `ProjectilePool` - leave the
static pointing at a destroyed object once their scene unloads.

**Why this has not bitten yet, stated honestly:** Unity overloads `==` on `UnityEngine.Object` so a
destroyed object compares equal to `null`. Every `if (X.instance != null)` guard in the codebase
therefore behaves correctly, and there are a lot of them. This is latent, not live.

It stops being latent the moment it meets AUD-061, or the moment someone caches
`var rules = GameRules.instance;` across a scene load.

**Recommendation:** add the `OnDestroy` guard uniformly. It is mechanical and independently safe. A
project validator rule asserting that any `public static <T> instance` on a `MonoBehaviour` has a
matching clear would keep it from regressing.

**Fix applied.** All 34 affected `MonoBehaviour` singletons now release their static:

```csharp
private void OnDestroy()
{
    if (instance == this)
    {
        instance = null;
    }
}
```

32 were mechanical insertions; `GameRules` and `PlayerHealthBar` already had an `OnDestroy` and were
edited by hand. In `PlayerHealthBar` the release is placed *before* the existing early return, so a
bar destroyed before it resolved its health source still releases.

`Level5SingletonLifetimeTests.EverySingletonReleasesItsStaticOnDestroy` fails the build if a new one
appears, and a play-mode test checks a real destroy actually clears the static.

---

## AUD-061 - `?.` bypasses Unity's destroyed-object check (Low, latent)

**Where:** `database/PendingMatchPersistenceStore.cs:65`, `menu_start/UnlockService.cs:62,88`.

```csharp
DBHelper.instance?.setGameScoreSubmitted(score.Scoreid, false);
```

The null-conditional operator compiles to a **reference** null check. It does not call Unity's
overloaded `==`, so a destroyed `UnityEngine.Object` is not treated as null and the call proceeds on
a destroyed component. This is the standard Unity trap and it reads as defensive code, which is what
makes it worth a register entry.

**All three sites are currently safe**, and I verified rather than assumed it: `DBHelper` and
`DBConnector` sit on the same `database.prefab`, `DBConnector.Awake` calls
`DontDestroyOnLoad(gameObject)`, and both guard duplicates correctly before assigning their statics.
`LoadedData` is likewise `DontDestroyOnLoad`. They are safe by placement, not by the operator.

**Recommendation:** use `if (X.instance != null)` on anything deriving from `UnityEngine.Object`, and
add an architecture test banning `instance?.` on those types - the project already has this style of
ratchet in `Level5MatchArchitectureTests` and `Level5VersusArchitectureTests`.

**Fix applied.** All three sites now compare with `!=` first.
`PendingMatchPersistenceStore` guards `DBHelper.instance` explicitly. `UnlockService` routes through
a small `LevelCatalogOf(LoadedData)` helper - the `?.` that remains after it is on `LevelCatalog`,
which is a plain class rather than a `UnityEngine.Object`, where the operator behaves as written.

`Level5SingletonLifetimeTests.NoNullConditionalOnASingletonInstance` keeps the pattern out.

---

## AUD-062 - Gamepad assumed present, cached once (Low as shipped)

**Where:** `camera/SniperCameraController.cs:35-38,50,62-67`.

```csharp
void Start()
{
    Debug.Log(Gamepad.all);
    gamepad = Gamepad.current;      // null when no controller is connected
}

void FixedUpdate()
{
    Vector2 move = gamepad.leftStick.ReadValue();     // NRE every physics tick
}

private void Update()
{
    if (gamepad.buttonSouth.wasPressedThisFrame ...)  // NRE every frame
}
```

Two problems: `Gamepad.current` is `null` on any device with no controller attached - which is the
entire mobile audience - and it is captured once in `Start`, so a controller connected later is never
picked up and one disconnected mid-session leaves a stale device.

**Severity is Low only because the component is unreachable:** its GUID appears solely in
`Assets/Scenes/sniper_multiplayer_test.unity`, which is **not** in `EditorBuildSettings`. If that
scene is ever added to the build, this becomes an immediate hard failure.

**Recommendation:** guard the dereference, re-resolve through `InputSystem.onDeviceChange`, and move
the file to `Assets/Scripts/Dev/` so AUD-012's isolation validator covers it. The stray
`Debug.Log(Gamepad.all)` should go with it.

**Fix applied.** The cached `gamepad` field is gone. Both `Update` and `FixedUpdate` resolve
`Gamepad.current` through a single `ActiveGamepad` property and return early when it is null, so a
device with no controller does nothing instead of throwing every tick - and a controller connected
or unplugged mid-session is picked up, which the cached field never did. The stray
`Debug.Log(Gamepad.all)` in `Start` went with it.

---

## AUD-063 - Per-frame string building in a vehicle `Update` (Low)

**Where:** `player_racing/RacingVehicleController.cs:247-250`.

```csharp
vehicleCurrentSpeedText.text = "speed : " + movementSpeed.ToString()
    + "\nmax speed : " + vehicleProfile.MaxSpeed
    + "\nacceleration : " + vehicleProfile.Acceleration
    + "\njump : " + vehicleProfile.JumpForce;
```

Roughly seven string allocations per frame per vehicle, plus a UI `Text` rebuild, feeding what is
plainly a developer readout rather than player-facing UI. Three of the four values are constants read
from `vehicleProfile` and never change during a run.

This is the **only** per-frame allocation site the pass found in production code, which is worth
saying plainly - the rest of the `Update` bodies are clean.

**Recommendation:** delete it, or gate it behind `#if DEVELOPMENT_BUILD` and only rebuild the string
when `movementSpeed` actually changes.

**Fix applied.** The readout moved into `UpdateSpeedReadout`, marked
`[Conditional("UNITY_EDITOR")]` / `[Conditional("DEVELOPMENT_BUILD")]` so the call disappears
entirely from a release build, and it rebuilds the string only when the speed actually changes.

---

## AUD-064 - `SendEmail.cs` is a dead file (Low)

**Where:** `Assets/Scripts/email/SendEmail.cs`.

The entire class body is commented out. What remains compiling is an empty `MonoBehaviour`. Its GUID
appears in **no** scene, prefab or script anywhere under `Assets/`, and the type name has no code
references.

Worth a register entry rather than a silent deletion for one reason: the commented-out body contains

```csharp
//public string password = "YourGmailAccountPassword";
```

a public inspector field for an account password, alongside hardcoded from/to addresses. That is a
pattern nobody should copy back in, and a commented-out file is exactly where someone eventually
finds it and uncomments it.

**Fix applied.** Deleted, on the owner's instruction. Nothing referenced it and its history is in
git. The `email` folder is now empty and gone with it.

---

## The assembly split: why it is blocked (AUD-059 follow-up)

The structural half of AUD-059 was carried forward as "a planned piece of work". Analysing it
produced a more useful answer than a plan: **the split cannot proceed meaningfully until the core
gameplay cycle is broken**, which makes it dependent on AUD-002 and AUD-010 rather than independent
of them.

Every folder under `Assets/Scripts` was checked for outbound references to types owned by other
folders.

> **The first run of this analysis was wrong in two places, and the corrections went in opposite
> directions.** Its string-stripping mishandled verbatim strings: it reported `constants` as
> depending on `player` and `menu_start` when those "references" were the string literals
> `"CharacterProfile"` and `"CheerleaderProfile"` - database table names - and it reported
> `Documentation` as a leaf when the exporter genuinely reads six folders' types. Both were caught by
> reading the files rather than trusting the tool. The numbers below are from the corrected run.

Four folders are true leaves with no outbound dependencies at all:

| Folder | Files | Referenced by | Status |
| --- | --- | --- | --- |
| `constants` | 4 | **19 folders** | extracted as `Level5.Constants` |
| `SFX manager` | 1 | 7 folders | extracted as `Level5.Audio` |
| `pooling` | 2 | 2 folders | extracted as `Level5.Pooling` |
| `email` | 1 | 0 folders | left alone - it is dead, see AUD-064 |

`constants` is the one that mattered: pure constants and enums, referenced by more folders than
anything else in the tree, and it turned out to need no untangling at all - only the analysis said
otherwise.

Everything else is entangled, and the entanglement is concentrated in four folders that reference
each other in both directions:

```
player  <--->  game manager      player -> game manager (20), game manager -> player (13)
player  <--->  basketball        player -> basketball (15), basketball -> player (18)
game manager <---> menu_start    game manager -> menu_start (8), menu_start -> ... (via player/input)
```

Those four folders are 73 of the 206 files. An assembly cannot contain a cycle with another
assembly, so no arrangement of asmdefs separates them - they would all have to land in one assembly,
which is the situation that exists today.

One near-miss is worth naming because it looks extractable and is not:

- **`Models`** (referenced by 8) depends on `GameStats`, `Modes`, `MatchRuntime`, `GameOptions` and
  `PlayerIdentifier` - `HighScoreModel` converts live gameplay state into a save row, so it is a
  gameplay consumer wearing a data-model name. Extracting it means separating the save-row shape
  from the conversion, which is real work rather than a file move.

**What was done now.** Three assemblies, in ascending order of how much they matter:

- `Level5.Pooling` - `RuntimeObjectPool` is shared infrastructure that must not grow a dependency on
  gameplay, and now it structurally cannot.
- `Level5.Audio` - the same argument, and it is reached from seven folders.
- `Level5.Constants` - the foundation. Nineteen folders depend on it and it depends on nothing, so
  it can never be part of a cycle again.

None of this changes behaviour; the predefined assembly automatically references all three.

**What should happen next**, in order:

1. Break the `player` ↔ `basketball` cycle. That is AUD-010's "single shot result event" - the shot
   pipeline is what ties them together in both directions, and it is the larger of the two knots
   (18 references one way, 15 the other).
2. Break `player` ↔ `game manager`. That is AUD-002.
3. Separate `Models` from the gameplay state it converts.
4. Only then does splitting the remainder become a mechanical exercise.

Attempting steps beyond the leaves before those is not a matter of effort; it is not possible.

---

## Checked and clean

Recorded because a future pass should not spend the time again, and because two of these were
candidate findings that verification killed.

| Area | Result |
| --- | --- |
| `GameObject.Find` / `GetComponent` inside `Update` | **None** in production code. The only match is the quarantined `Legacy~/StartManager_original.cs`. Earlier passes cleaned this up thoroughly. |
| SQL injection | **Not present.** 205 parameterised bindings; the two dynamic-identifier sites (`ORDER BY " + field + " " + order`) are gated by `RequireSqlIdentifier` and `RequireSqlSortOrder`, which validate before use. |
| Threading / async misuse | **None.** No `async void`, no `Task.Run`, no raw `Thread`, no `.Result`/`.Wait()`. Networking is coroutine-based `UnityWebRequest`. |
| Event subscription leaks on pooled objects | **Clean.** `EnemyHealthBar` / `BodyGuardHealthBar` subscribe in `Start` (which runs once per component lifetime, not per pool re-activation) and unsubscribe in `OnDestroy`. Correct for pooling. Investigated as a suspected leak; it is not one. |
| Per-frame `Debug.Log` | **None.** The two matches inside `Update` bodies are gated by `wasPressedThisFrame` and a trigger collision respectively. |
| `PlayerPrefs` | **Zero uses.** All persistence goes through SQLite / JSON / the API, as documented in `persistence-boundaries.md`. |
| `RacingVehicleController.cs:179` | Investigated as a suspected syntax error from a grep rendering artifact. It is a commented-out line. Not a finding. |

---

## Still open from earlier passes

Carried forward unchanged, for a complete picture. None was re-investigated in this pass.

| ID | Problem |
| --- | --- |
| AUD-002 | `PlayerController` (1,083 lines) owns movement, input, basketball, combat, animation and state. |
| AUD-007 | Enemy/bodyguard/CPU behaviour uses ad-hoc booleans rather than explicit state machines. |
| AUD-008 | Several UI flows still share ownership of gameplay/progression state. |
| AUD-010 | Basketball shot lifecycle is spread across systems with no single shot-result event. |
| AUD-017 | `BasketBall` / `BasketBallAuto` still duplicate `shootBasketBall`, `Launch` and score-text formatting. The three accuracy modifiers were extracted; the rest was not. |
| AUD-001 | `PlayerCollisions` still combines damage, controller state, UI and audio. |
| AUD-005 | `PlayerAttackQueue` is still named for, and lives on, the player rather than being a scene-level reservation system. |
| AUD-009 | `ProjectilePool` remains a second pool implementation alongside `RuntimeObjectPool`. |
| AUD-011 | `CharacterProgressMigration` is written but has no callers - wire it or delete it. |
| AUD-012 | The `.asmdef` split - now sharpened by AUD-059. |
| AUD-013 | Controllers retain duplicate `isCpu`/`isDefensivePlayer` fields; two `PlayerIdentifier` instances per slot. |

## Note on the versus domain

`Assets/Level5/Core/Versus` was added on 2026-08-08 and carries its own architecture review, in
[`versus-correspondence-plan.md`](versus-correspondence-plan.md) §5. One defect was found and fixed
during that review. It is not re-audited here beyond confirming that its ten architecture tests still
pass and that it contributes no new findings above: it has no scene dependency, no file system
access, no networking, and does not touch `GameOptions`.

Its one unverified area is the same one flagged in that document and in AUD-059 - nothing in it has
been exercised in play mode, because nothing in this project can be.

## Verification status

All seven fixes are applied and compiled. **293 edit-mode tests and 4 play-mode tests pass**, and
`scripts/validate-repository.ps1` is clean.

Three of those tests exist because of this pass and did not previously have anywhere to live:
`Level5SingletonLifetimeTests` (two ratchets, for AUD-060 and AUD-061) and the play-mode suite, which
is the first automated coverage this project has ever had of runtime gameplay code.

Not covered by automation, and worth a manual look before release:

- **AUD-058's behaviour change.** An instant-kill death now ends the match normally and, in the
  campaign, advances to the end-round screen. Worth playing once with
  `projectile_bullet_instantkill_enemy` to confirm the flow reads correctly.
- **AUD-057's scrub** runs inside `createDatabase` on startup. Worth confirming against a device
  database that actually has a non-null `bearerToken` from an older build.
- **AUD-062** cannot be exercised without a controller, and its scene is not in the build.
