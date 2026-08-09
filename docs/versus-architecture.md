# Versus and Correspondence Multiplayer

Status: implemented, 141 edit-mode tests
Last reviewed: 2026-08-08

Two people can keep a competitive rivalry going without ever being online at the same time. One
plays their turn tonight; the other answers on Thursday; the game resolves and the series moves on.
That is what this system is for, and everything below exists to make it possible without the game
modes knowing anything about it.

The decision record - what the repository looked like before, what was considered and rejected, and
the two review passes - is in [`versus-correspondence-plan.md`](versus-correspondence-plan.md). This
document describes what was actually built.

---

## 1. The shape of it

```
GAMEPLAY                 GameRules, GameStats, the scenes
   |                     produces numbers; knows nothing about competition
   |  GameStatsAttemptResults
   v
ATTEMPT                  Attempt, AttemptResult, AttemptState
   |                     one participant's run at one game
   v
RULESET                  CompetitiveRuleset, ComparisonKey, VersusCapability
   |                     how two runs are compared, and what kinds of competition are allowed
   v
VERSUS GAME              VersusGame, GameResult, InformationPolicy
   |                     one contest: one ruleset, two attempts, one verdict
   v
SERIES                   VersusSeries, SeriesFormat, SeriesSnapshot, SeriesScore
   |                     best-of-N, early termination, frozen rules
   v
SOCIAL                   RivalryRecord  (derived, never stored)

                         everything above sits on one seam:
                         IVersusSeriesRepository
                              |                     |
                    FileVersusSeriesRepository   a remote store, later
```

Dependencies only ever point downward. `Level5.Core.Versus` is plain C# - no `MonoBehaviour`, no
`GameObject`, no file system, no networking - which is why an entire series can be created, played
and resolved inside an edit-mode test with no scene loaded. `Assets/Scripts/versus` holds the Unity
adapters that join it to the game.

Three architecture tests keep this true: `TheDomainHasNoSceneDependencies`,
`TheDomainHasNoNetworkingAndNoFileSystem` and `TheGameplayFootprintIsOneCall`.

---

## 2. Competitive rulesets

A game mode says *how the game is played*. A ruleset says *how two runs at it are compared*. They
are separate because they change for different reasons and version independently: a scoring tweak
is a new ruleset version without being a new mode.

```csharp
new CompetitiveRuleset(
    new RulesetId("three-point-contest"),   // stable, stored, never renamed
    version: 1,                             // rules version, NOT the build number
    GameModeId.ThreePointContest,           // the mode that produces attempts
    VersusCapability.LocalAlternating | VersusCapability.Asynchronous,
    new[]
    {
        ComparisonKey.Highest(AttemptMetric.Score),                   // the objective
        ComparisonKey.Lowest(AttemptMetric.CompletionTimeSeconds),    // its own tie-break
        ComparisonKey.Highest(AttemptMetric.Accuracy)                 // then this
    },
    minimumCompatibleVersion: 1);
```

**Comparison is data.** The keys are checked in order; the first difference decides it; equal on all
of them is a draw. There is no global tie-break rule anywhere in this domain and no per-mode
`switch` - a mode that wants to be decided on the fastest time says so by listing that key first.

**Capabilities are explicit and default to nothing.** A mode with no ruleset is not competitive, so
nothing reaches correspondence play by accident. `VersusCapability` is a flags enum because these
genuinely are a set - a mode can be both locally alternating and asynchronous.

**Ruleset version is separate from build version.** Builds 1.6.0 and 1.7.0 can both play
`three-point-contest` version 4. `MinimumCompatibleVersion` is the one number that makes a later
migration engine possible without one existing now: a series snapshotted at a version this build can
no longer score is refused with a stated reason rather than quietly mis-scored.

### Metrics

`AttemptMetric` is a fixed, mode-agnostic set: `Score`, `ShotsMade`, `ShotsAttempted`, `Accuracy`,
`CompletionTimeSeconds`, `LongestStreak`, `TotalDistance`, `BonusPoints`. A fixed enum rather than an
open dictionary keeps results comparable, keeps the stored form stable and keeps comparison
allocation-free. **Never renumber a member** - the value indexes the stored metric array.

### Where rulesets come from

`VersusCatalogs` prefers authored `CompetitiveRulesetDefinition` assets under
`Resources/Versus/Rulesets` and falls back to `DefaultCompetitiveRulesets` for anything not yet
authored - the same arrangement `MatchCatalogs` uses. The code registry is authored data, not logic:
nothing in it branches on which mode it is looking at.

Modes deliberately absent from it, each for a stated reason: **Battle Royal, Cage Match, Versus
(CPU) and Lockdown** need both sides in the same match at once, so there is no such thing as one
participant's separate run to compare; **Beat tha Computahs** is a campaign against the game;
**Arcade** and **Free Play** have no scoring contract worth competing over. **Bash Up Some Nerds**
has a ruleset but declares local play only - one person at a time is fine, but enemy spawning is not
reproducible enough for two runs a week apart to be a fair contest.

---

## 3. Attempts

An `Attempt` is one participant's run at one game, and it is also the attempt ticket: it carries the
id, participant, game, ruleset version, issue time and status a server would eventually sign. A
separate ticket type mirroring those fields would have been two objects owning one lifecycle.

```
Created --MarkReady--> Ready --Start--> Started --Complete--> Completed
                                            \--Abandon--> Abandoned
```

The state is explicit and never inferred from "the score is still null". A run that legitimately
scored zero and a run that was never played look identical under that kind of inference, and telling
them apart is the whole basis of a correspondence turn.

Repeating a transition is a no-op - a double-tapped button and a scene reload mid-run both land
there. Everything else throws:

| Refused | Why |
| --- | --- |
| completing twice | the retry exploit: play, dislike the score, play again, submit the better one |
| completing an abandoned attempt | it was given up |
| abandoning a completed attempt | a finished run cannot be taken back |
| a result from another ruleset, or another version of the same one | it was not scored under these rules |
| submitting under the opponent's attempt id | somebody playing on the other person's behalf |

**Issuing is idempotent.** Asking for a turn when one is already outstanding returns the existing
attempt. A failed save, a double tap, and the application dying between "issued" and "scene loaded"
all come back to that call, and none of them should mint a second attempt at the same game.

---

## 4. Games and information policy

A `VersusGame` is one contest inside a series: one frozen ruleset, two attempts, one verdict. It
resolves exactly once, when both attempts are in.

```
Pending --> Active --both attempts complete--> Resolved
                 \--forfeit--> Forfeited
                 \--cancel--> Cancelled
```

**The attempts are private and there is no accessor that returns them.** Everything a screen can ask
goes through `ViewFor(participantId)`, which answers *as* that participant:

| | `SealedAttempt` | `OpenTarget` |
| --- | --- | --- |
| before both finish | opponent's **state** only (`Created` / `Started` / `Completed`) | same, plus the target once the leader has finished |
| what is never returned | score, accuracy, shot count, time, any derived value | everything except the primary metric |
| after the game resolves | both results, together | both results, together |

Under `OpenTarget` the responder is also refused a turn until the target exists - otherwise the
format is a sealed attempt wearing a different name - and the right to go first alternates between
games, because setting the target blind is a real disadvantage.

This is a property of the type, not a convention. `AGameNeverHandsOutItsAttempts` asserts by
reflection that no public member of `VersusGame` returns an `Attempt`;
`TheParticipantViewCannotBeAskedToShowEverything` asserts that `ViewFor` takes no "reveal anyway"
flag; and `OnlyStorageTouchesTheSerializationDocuments` keeps everything except the repository away
from the stored documents, which are the one road round it.

---

## 5. Series

One `VersusSeries` type covers every format. Best-of-three and best-of-seven differ by two integers,
and separate implementations would mean two places to get early termination wrong.

| Format | Games | Wins needed | Presentation calls it |
| --- | --- | --- | --- |
| `BestOf1` | 1 | 1 | Quick Challenge |
| `BestOf3` | 3 | 2 | Standard Series |
| `BestOf5` | 5 | 3 | Extended Series |
| `BestOf7` | 7 | 4 | Championship Series |

The domain does not depend on those names. Even lengths are refused: a best-of-N that cannot be
settled by wins alone would need a decider that does not exist.

```
Invited --Accept--> Active --game resolved--> Active | Completed
   \--Decline--> Declined      \--Forfeit--> Forfeited
```

**A series ends the moment it is decided.** Every game object exists from creation, but only the
current one is `Active`; the rest stay `Pending` and never receive an attempt. A 4-0 best of seven
stops at four games, and asking for a turn at game five is refused. It also ends when the playlist
runs out, which is the only thing that stops a series full of draws from waiting forever for a win
that cannot arrive - an exhausted level series is an honest `SeriesOutcomeKind.Draw`.

The score is computed from the games rather than accumulated alongside them, so there is no counter
to drift.

### Series snapshot

A correspondence series can outlive a patch, so it carries its own rules:

```
SeriesSnapshot
├── FormatVersion            shape of the snapshot itself
├── Format                   best of 1/3/5/7
├── Games[]                  a full immutable copy of each ruleset - id, version,
│                            capabilities and comparison keys
├── InformationPolicy
└── AlternatesFirstAttempt
```

Resolution reads the snapshot and never the catalog. Editing a ruleset cannot change a game already
under way; new series pick up the new rules, existing ones keep the deal they started with. The
catalog is consulted for exactly one thing - whether this build can still *play* these versions -
and that check happens when a turn is issued, so an aged-out series can still be read even when it
cannot be continued.

---

## 6. Persistence

`IVersusSeriesRepository` is the only seam: `Save`, `Load`, `Exists`, `ListSummaries`, `Delete`,
`Archive`. There is no `CreateChallenge` or `AcceptChallenge` - a challenge is a series in
`Invited` status and accepting one is a domain operation followed by a save.

The coordinator holds no series between calls and saves after every mutation, so the stored document
is always the truth. That is what makes "stop the application anywhere" work.

Stored form: `VersusSeriesDocument` and friends - plain `[Serializable]` classes of public fields,
read and written by `JsonUtility`.

- **Only ids and values.** No scene reference, no ScriptableObject reference, nothing that means
  anything only inside a loaded scene.
- **Every enum is stored by name**, never by number. Reordering an enum is a normal thing to do to
  source and a catastrophic thing to do to stored competitive data.
- **Times are round-trip ISO 8601 UTC strings**, with empty standing for "not yet".
- **The rules travel with the series**, in full.

Two implementations: `FileVersusSeriesRepository` (one JSON file per series under
`Application.persistentDataPath/versus`, written to a temporary file and moved into place so an
interrupted write cannot destroy the previous version) and `InMemoryVersusSeriesRepository`, which
stores the **serialized string** - so the tests round-trip through the real serializer rather than
handing back the object they were given.

---

## 7. How a turn actually runs

```
VersusLauncher.Launch(seriesId, participantId, levelId, character)
  1. coordinator.IssueAttempt          -> attempt, saved before anything else happens
  2. read the mode from the series' FROZEN ruleset
  3. build an ordinary MatchRequest -> MatchCatalogs.Builder -> MatchConfiguration
  4. ActiveMatch.Begin + ActiveVersusAttempt.Begin + LegacyGameOptionsBridge.Apply
  5. coordinator.StartAttempt
  6. load the scene

          ... the match plays exactly as any other match ...

GameRules.HandleMatchEnded
  VersusMatchReporter.TryReport(stats, modeId, timePlayed)
    - no active attempt?  return true, nothing happens
    - GameStatsAttemptResults.Build turns GameStats into an AttemptResult
    - coordinator.SubmitResult -> game may resolve -> series may advance
    - could not save?  return false; the existing match-end retry loop tries again
```

The roster is one local human. An attempt is one participant's run whether the opponent is sitting
next to them or answering on Thursday, which is exactly why the same code covers local alternating
play and correspondence with nothing switching between them.

The gameplay scene is handed a normal `MatchConfiguration` and never learns that a series exists.
The whole footprint inside gameplay is one call in `GameRules`, asserted by
`TheGameplayFootprintIsOneCall`.

---

## 8. Adding a versus-compatible mode

No central file changes. There is no coordinator switch to extend.

1. Have a gameplay mode with a `GameModeId` and a `GameModeDefinition`.
2. Create a `CompetitiveRulesetDefinition` asset (**Level 5 > Versus > Competitive Ruleset**) under
   `Resources/Versus/Rulesets`, or add a row to `DefaultCompetitiveRulesets` until the asset exists.
3. Give it a stable kebab-case id. It goes into save data - it is never renamed.
4. Set the version to 1. Bump it whenever scoring changes.
5. Declare the capabilities it genuinely supports. Leave `Asynchronous` off if two runs a week apart
   would not be a fair contest - see Bash Up Some Nerds.
6. List the comparison keys in order: the objective first, then the mode's own tie-breaks.
7. If the mode's objective is a metric `GameStatsAttemptResults` does not yet produce, add it there
   and bump the ruleset version.
8. Add a test alongside `Level5VersusRulesetTests`.
9. Check a single game, then a series, then correspondence if it supports it.

Two things worth knowing before choosing keys:

- **Score a contest on points, with time as the tie-break, not on time alone.** Contest modes end
  when the markers are cleared *or* when the clock runs out, so a run that never finished still has
  a completion time - and a time-first comparison hands the win to whoever failed fastest.
- **A mode with randomness is not automatically unfair**, but nothing here supplies a shared seed. If
  a mode needs one to be comparable across a delay, it should declare local capabilities only until
  that exists.

---

## 9. Rivalries

`RivalryRecord` folds completed series into head-to-head history: series and game wins, draws,
sweeps, deciding games, current and longest streaks, last played. It is **derived on demand and
never stored**. A counter kept alongside the series it summarises is a counter that will eventually
disagree with them, and the series are the record of what actually happened.

---

## 10. Where a backend plugs in

Nothing about this design assumes the store is local.

| Eventually server-owned | Where it already lives |
| --- | --- |
| series identity, participants, ruleset version, series state | the series document, behind `IVersusSeriesRepository` |
| attempt issuance and lifecycle | `Attempt`, issued by `VersusGame` through `IVersusIdSource` |
| accepted result, game winner, series winner, turn state | `VersusSeries.SubmitResult`, the single write path |
| challenge lifecycle | `SeriesStatus.Invited` plus `Accept` / `Decline` |

Client-owned, now and for the foreseeable future: the gameplay simulation itself. There is no
server-side Unity simulation here and this design does not assume one arrives.

Turning a local participant remote is a `ParticipantKind` on the launch path. Nothing below the
coordinator reads it: `VersusSeries` knows only `ParticipantId`s, and gameplay never learns who the
opponent is.

`IVersusClock` and `IVersusIdSource` are injected rather than read from `DateTime.UtcNow` and
`Guid.NewGuid` - partly so a correspondence delay can be tested, and partly because id issuance is
the first thing a server takes over. `TheDomainDoesNotInventItsOwnClockOrIds` keeps it that way.

---

## 11. Driving it without menus

There are no versus menus yet. `VersusDevConsole` (development only) does what one will eventually
do - create a series, show whose turn it is, launch that turn, print the state - through the same
coordinator a real screen would call. It is also the correspondence simulation: "player A plays now,
player B answers later" is *Take turn as A*, then anything at all, then *Take turn as B*, including
quitting the game in between.

---

## 12. Known limitations

- **On a shared device, the local stats database is not sealed.** The versus domain will not show
  you the opponent's attempt, but both players' own high-score rows land in the same local database
  and are visible in the stats menu. This is a property of two people sharing one save, not of this
  design, and it disappears when the opponent is remote. Versus results are not added to any
  leaderboard.
- **No shared seed for randomness.** Modes that would need one to be fair across a delay should
  declare local capabilities only.
- **Local simultaneous play is not implemented.** The attempt model is one participant per match, so
  modes that need both sides in one match have no ruleset at all rather than a half-working one.
- **No turn inbox.** The coordinator raises `SeriesCreated`, `AttemptIssued`, `AttemptStarted`,
  `AttemptCompleted`, `GameResolved`, `SeriesAdvanced` and `SeriesCompleted` so an inbox can be
  built as a projection later. Deliberately not built now, and notification delivery must stay
  separate from the series domain when it is.
- **An abandoned turn stays outstanding.** Quitting a competitive match to the menu leaves the
  attempt live in the stored series; the next request hands the same turn back. That is deliberate -
  it is the same behaviour that survives a crash - but nothing yet offers the player a way to
  abandon it explicitly from a screen. `AbandonAttempt` exists for when one does.
- **Production versus UI is follow-up work.**

---

## 13. Tests

141 edit-mode tests in `Assets/Tests/Editor`:

| File | Covers |
| --- | --- |
| `Level5VersusRulesetTests` | identity, versions, capabilities, comparison, tie-breaks, the shipped registry |
| `Level5VersusAttemptTests` | the lifecycle, every illegal transition, duplicate completion, result mismatch |
| `Level5VersusGameTests` | resolution with zero/one/both attempts, wins, draws, forfeit, double resolution |
| `Level5VersusSeriesTests` | best of 1/3/5/7, early termination, games never activated, 3-3 into game seven, draws |
| `Level5VersusInformationPolicyTests` | sealed leakage checks, open target, alternating lead |
| `Level5VersusPersistenceTests` | restore at every interruption point, enum names, corruption, archiving |
| `Level5VersusVersioningTests` | frozen rules, catalog updates not touching active series, aged-out versions |
| `Level5VersusCorrespondenceTests` | the whole flow through the coordinator, a new session per turn |
| `Level5VersusIntegrationTests` | `GameStats` -> result -> resolved series, and the `GameRules` hook |
| `Level5VersusArchitectureTests` | the boundaries no single file shows |
