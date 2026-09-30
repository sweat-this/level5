# Versus and Correspondence Multiplayer

Status: implemented, with edit-mode and play-mode coverage (the local simultaneous slice in section 15 is
see its "Verification status" for what was and was not run)
Last reviewed: 2026-09-30

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

                         everything above sits on one seam, for LOCAL storage:
                         IVersusSeriesRepository
                              |                     |
                    FileVersusSeriesRepository   InMemoryVersusSeriesRepository

                         remote correspondence does not implement this seam - see §10.
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
VersusLauncher.Launch(seriesId, participantId, levelId, character, unlockSnapshot)
  0. LevelEligibility.ValidateForLaunch(level, levelId, unlock) -> fail closed, no attempt issued,
     if the level is unknown, not selectable, locked, or no snapshot was supplied (issue #203)
  1. coordinator.IssueAttempt          -> attempt, saved before anything else happens
  2. read the mode from the series' FROZEN ruleset
  3. build an ordinary MatchRequest -> MatchCatalogs.Builder.Build(request, unlock) -> MatchConfiguration
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

Step 0 and step 3 fail for different reasons on purpose. Step 0 is local content/account
eligibility - unknown, not selectable, or locked - which is known for free before anything is spent
on the attempt, so it must never reach `IssueAttempt`. Step 3's builder revalidation can still refuse
an otherwise eligible level if the series' frozen ruleset cannot be played there (an arena/mode
mismatch); that can only be known once the ruleset is read in step 2, which is after the attempt in
step 1 already exists and is persisted. That attempt is deliberately left outstanding rather than
abandoned, so the same turn can be retried on a compatible arena instead of costing the participant
their go for a reason that has nothing to do with them. `RemoteAttemptLauncher` (Backend V2
correspondence, issue #198) is the same join with the same two-stage shape, built against a server
attempt instead of a local one.

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

**Correction (2026-09, Competition Protocol V1 audit - see
[`Level5Backend/v2/docs/competition-protocol/README.md`](https://github.com/sweat-this/Level5Backend/blob/dev/v2/docs/competition-protocol/README.md),
issue #8):** this section previously said "nothing about this design assumes the store is local"
and implied a remote backend plugs in by implementing `IVersusSeriesRepository` over the network.
That is wrong and has been corrected below. `IVersusSeriesRepository` is the seam for **local**
storage only (§1, §9) and stays exactly as it is. A remote backend does not implement it - handing
a server `Save(VersusSeries)`/`Load(SeriesId)` would make the client authoritative for state the
server must own. Remote correspondence instead goes through a **separate typed client** speaking
narrow command/query calls (`CreateChallenge`, `AcceptChallenge`, `DeclineChallenge`,
`CancelChallenge`, `StartAttempt`, `CompleteAttempt`, `GetSeries`, `ListSeries`) against the
backend's HTTP API, translating between Unity's local types and the wire contract
("Competition Protocol V1") at that one boundary. Nothing below the coordinator changes.

| Eventually server-owned over the remote protocol | Local equivalent it is modeled on |
| --- | --- |
| series identity, participants, frozen rules, series state | the series document, behind `IVersusSeriesRepository` |
| attempt issuance and lifecycle | `Attempt`, issued by `VersusGame` through `IVersusIdSource` |
| accepted result, game winner, series winner, turn state | `VersusSeries.SubmitResult`, the single write path |
| challenge lifecycle | `SeriesStatus.Invited` plus `Accept` / `Decline` |
| opponent-result visibility (`SealedAttempt` / `OpenTarget`) | `VersusGame.ViewFor` |

Client-owned, now and for the foreseeable future: the gameplay simulation itself. There is no
server-side Unity simulation here and this design does not assume one arrives.

Turning a local participant remote is a `ParticipantKind` on the launch path. Nothing below the
coordinator reads it: `VersusSeries` knows only `ParticipantId`s, and gameplay never learns who the
opponent is.

**Implemented (2026-09, issue #158):** the typed client described above now exists at
`Assets/Scripts/backendv2/Level5BackendV2/`. See
[`docs/backend-v2-client.md`](backend-v2-client.md) for its architecture and the
`AttemptDescriptorDto -> MatchConfiguration` mapping.

**Implemented (2026-09, issue #159):** the UI/E2E flow itself - a Friends / Incoming / Outgoing /
Your Turn / Active / Completed correspondence screen (`CorrespondenceScreenController`), the
missing `StartAttempt -> mapper -> ActiveMatch/ActiveRemoteAttempt/LegacyGameOptionsBridge/
SceneTransition` launch sequence (`RemoteAttemptLauncher`), and a retry path for a failed result
submission (`PendingRemoteAttemptResult`, `RemoteAttemptResultSubmitter.TryRetryPending`). See
[`docs/backend-v2-correspondence-ui.md`](backend-v2-correspondence-ui.md) for the UI flow and
[`docs/backend-v2-correspondence-certification.md`](backend-v2-correspondence-certification.md)
for end-to-end certification results.

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

For step-by-step use, see [Versus Dev Console Guide](versus-dev-console-guide.md).

---

## 12. Known limitations

- **On a shared device, the local stats database is not sealed.** The versus domain will not show
  you the opponent's attempt, but both players' own high-score rows land in the same local database
  and are visible in the stats menu. This is a property of two people sharing one save, not of this
  design, and it disappears when the opponent is remote. Versus results are not added to any
  leaderboard.
- **No shared seed for randomness.** Modes that would need one to be fair across a delay should
  declare local capabilities only.
- **Local simultaneous play exists for exactly one combination.** Most Points, two local humans, The
  Scrapyard (section 15). Every other ruleset still has no `LocalSimultaneous` capability: modes that need
  both sides in one match (battle royal, cage match, versus CPU, lockdown) have no ruleset at all rather
  than a half-working one, and the score/make-count/contest rulesets have not been certified for two
  humans in one match. The generic two-human *runtime* prerequisite (certified 2026-09-29) is input, roster,
  spawn and camera plumbing only; it is verified with virtual devices, and physical keyboard + gamepad and
  two-gamepad certification is still outstanding. Open blockers for wider two-human work (modes and arenas
  mostly do not declare two-human support, three or more local players refused) are listed in
  [`player-input-architecture.md`](player-input-architecture.md) under "Match-Local Gameplay Device Plan".
- **No turn inbox.** The coordinator raises `SeriesCreated`, `AttemptIssued`, `AttemptStarted`,
  `AttemptCompleted`, `GameResolved`, `SeriesAdvanced` and `SeriesCompleted` so an inbox can be
  built as a projection later. Deliberately not built now, and notification delivery must stay
  separate from the series domain when it is.
- **An interrupted turn stays outstanding.** A crash, a process kill or a load error leaves the
  attempt live in the stored series; the next request hands the same turn back. A *deliberate* exit
  from the pause menu is different for local versus: it forfeits the game, but only once the forfeit
  is durable (section 14, "Leaving a turn"). `AbandonAttempt` still exists for a screen that wants to
  abandon a turn without forfeiting it.
- **Realtime and open-target play are still not exposed, and simultaneous play is one ruleset.** The
  Local Versus screen creates `LocalAlternating` series for every alternating-capable ruleset and
  `LocalSimultaneous` series for Most Points only (section 15). `VersusLauncher.Launch` still starts
  exactly one local human; `VersusLauncher.LaunchSimultaneous` starts two.

---

## 14. Production Local Versus (local alternating)

Two people on one device take separate gameplay attempts in turn. This is the first production
versus surface, and it is deliberately just a composition and presentation layer over everything
above; it adds no competition state.

| | Local Versus | Correspondence |
| --- | --- | --- |
| Screen | `level_00_local_versus` | `level_00_multiplayer` |
| Entry | authored **Local Versus** button on the Start screen | Multiplayer footer button |
| Storage | local file repository (`FileVersusSeriesRepository`) | Backend V2 |
| Account | none - works signed out and offline | authenticated Backend V2 account |
| Mode | `VersusMode.LocalAlternating` (and `LocalSimultaneous` for Most Points only - section 15) | asynchronous remote play |

Local Versus never calls `BackendV2Runtime`, `CorrespondenceApiClient` or `FriendsApiClient` and
leaves any existing Backend V2 session untouched.

### Flow

```text
Start -> Local Versus -> create (or select) a series -> pick character + arena -> Play Turn
      -> gameplay -> ordinary end-of-match summary -> Continue Series
      -> loading scene (refreshes profile data) -> Local Versus, on the series just played
```

- **Creation.** `LocalVersusFlow.BuildRequest` builds a normal `SeriesRequest` (display names,
  ruleset, format) and `VersusRuntime.Coordinator.CreateSeries` creates it. The fixed semantics are
  `LocalAlternating`, `SealedAttempt`, no invitation, alternating first attempt; the request's
  `source` is `"local versus UI"` (diagnostic only). Formats are `BestOf1/3/5/7` resolved through
  `SeriesFormat.FromGameCount`. The selected ruleset is repeated for every game; there is no mixed
  playlist editor. A persistence failure is shown as the coordinator's validation and nothing is
  selected.
- **Identity.** Each new series gets two fresh opaque `ParticipantId`s
  (`Guid.NewGuid().ToString("N")`); display names live only in `MatchParticipant.DisplayName`.
  Participants are never derived from `LocalAccountIdentity`, `GameOptions.userid` or a Backend V2
  player id, and there are no persistent local-player profiles.
- **Rulesets.** Offered from `VersusCatalogs.Rulesets.Supporting(VersusCapability.LocalAlternating)`
  and shown by `DisplayName`; the request carries the stable `RulesetId`.
- **Lists.** `ListSeries()` summaries filtered to `Mode == LocalAlternating` and not archived, active
  first. Only the selected series is loaded in full.
- **Next turn.** Read from the loaded series, never stored: the side the current game designates
  first is checked with `CanIssueAttempt`, then the other (the same rule as `VersusDevConsole`). If
  neither can attempt, the screen shows the terminal state.
- **Character and arena belong to the turn.** Characters come from the ordinary player-select
  projection and are limited to unlocked ones; arenas from the current `UnlockSnapshot`
  (`UnlockSnapshotBuilder`) filtered to known, selectable, unlocked and compatible with the frozen
  ruleset's mode, so the normal path never relies on `VersusLauncher`'s issue-then-fail behavior for
  mode/arena incompatibility. Modifiers are `MatchModifiers.Default`.
- **Launch.** `VersusLauncher.Launch(seriesId, participantId, levelId, character, unlock, modifiers)`
  and nothing else: the UI does not issue or start attempts, call `ActiveMatch.Begin`, write
  `GameOptions` or load scenes. The launcher revalidates level eligibility and character unlock
  against the snapshot authoritatively, both before `IssueAttempt`, so a locked level or character
  never spends the turn.
- **Return navigation.** `LocalVersusNavigationState` (`ReturnPending`, `PreferredSeriesId`) is set
  immediately around the launch and cleared if the launch fails. It is tied to the launched
  `MatchConfiguration`, so a later ordinary match is unaffected, and it holds no competition state.
  `Pause` relabels its return action **Continue Series** while it is pending; the loading scene
  (`LoadManager.ResolvePostLoadScene`) then routes to `level_00_local_versus` instead of Start. The
  in-game summary is unchanged and nothing auto-transitions after `VersusMatchReporter`.
- **Resume.** On open the screen lists stored series, selects `PreferredSeriesId` if it exists and
  consumes it. If the app restarted the hint is gone and the first unfinished series is shown - the
  stored series is the only authority. An abandoned turn stays outstanding and is handed back by the
  idempotent `IssueAttempt` - including an attempt that had already `Started` (`VersusGame.IssueAttempt`
  used to try to re-ready it, which only a `Created` attempt can be, so a turn left mid-match could
  not be taken again; pinned by `AnInterruptedStartedAttemptIsHandedBackByIssueAttemptRatherThanRefused`).
- **Leaving a turn.** Every deliberate exit from gameplay - the pause menu's Start/Menu (**Continue
  Series** / **Quit Turn (Forfeit)**) and **Quit** - goes through `VersusQuitPolicy.TryPrepareForExplicitExit()`
  before any database wait, scene load or `Application.Quit`. It says "leave" only when nothing is
  owed to the stored series:

  | State when the player leaves | Result |
  |---|---|
  | no series attempt active (an ordinary match) | leave; nothing is written |
  | turn still being played | the current game is forfeited to the opponent (`ForfeitGame` -> `VersusSeries.ForfeitCurrentGame` -> `repository.Save`); the player leaves **only if that save succeeded**, and `ActiveVersusAttempt` is then cleared |
  | forfeit save failed | the player **stays in the match**, `ActiveVersusAttempt` stays, the stored series is unchanged, and a later press retries (nothing loops behind the player's back) |
  | match ended but the result is not saved yet (`MatchController` is `Ending` and the attempt is still active) | the player **cannot leave**, and nothing is forfeited - the run is earned and `VersusMatchReporter` owns saving it through `GameRules`' existing match-end retry loop; once it succeeds and clears the attempt, the same action leaves normally |
  | crash, process kill or load error | not a quit: no policy runs, the `Started` attempt stays outstanding in the stored series and `IssueAttempt` hands it back |

  An in-memory forfeit is never enough - a forfeit that could not be saved would leave the stored
  series with the original `Started` attempt, i.e. a free retake. A refused exit is not silent: the
  pressed pause button reads "Couldn't save - try again" for a few seconds. Restart is refused while an attempt
  is outstanding (and its button is disabled), for the same reason. `Pause` cannot reference
  `Level5.Versus` (`Level5.Versus` depends on `Level5.Match`), so `GameLevelManager` binds the policy
  through `Pause.BindVersusContext`; an unbound `Pause` behaves as an ordinary match.
- **No general MatchResult.** A local-versus attempt reports to its local `VersusSeries` through
  `VersusMatchReporter` and is excluded from `POST /api/v2/match-results`.

Code: `Assets/Scripts/menu_local_versus/` (`LocalVersusFlow`, `LocalVersusScreenModel`,
`LocalVersusController`, `LocalVersusUiObjects`), `LocalVersusNavigationState` in `Level5.Match`.
The scene is regenerated with **Tools > Local Versus > Generate Local Versus Scene** and the Start
button with **Tools > Local Versus > Author Start Screen Button** (`LocalVersusSceneBootstrap`); the
output is an ordinary serialized scene and nothing is built at runtime.

### Local Versus certification (2026-09-29)

Certification of the production Local Versus flow against `dev` `d28767bbf8a4c428fd9565166a100e11c67f204f`
(PR #213 was the head; it does not touch this flow). No open issues or PRs superseded the work.
Certification-first: production code changed only where a defect was demonstrated (two, below).

| | |
| --- | --- |
| Unity | 6000.5.7f1 (`017862109af0`), editor and standalone player |
| Player | Windows x64, IL2CPP, non-development - built by `LocalVersusCertificationBuild` (Editor-only) |
| Machine | Windows 10 Pro, one RTX 4060, primary display 2560x1440 |
| Repository isolation | Editor sessions: an explicit `LEVEL5_LOCAL_VERSUS_CERT_ROOT` temp directory (`VersusRuntime.Override`) plus a temporarily isolated product name so the real `GameRules` path cannot touch a developer's data. Player: its own product name (`level5-local-versus-cert`), so `Application.persistentDataPath/versus` is a private directory. No certification session (process fixture, player or headless render) read or wrote a developer's ordinary `LocalLow/level5/level5`; the ordinary EditMode and PlayMode suites still use it as they always have. |
| Input devices | Physical keyboard and mouse (real `SendInput` into the standalone player). Gamepad: **virtual only** (Input System test devices). No physical controller was attached. |

**Automated suites (actual totals, final code).** Clean batchmode compile: 0 errors. Focused EditMode
(`Level5LocalVersusTests`, `Level5VersusLauncherTests`, `Level5VersusPersistenceTests`,
`Level5VersusIntegrationTests`): `Level5LocalVersusTests` 44/44 (42 existing + 2 new) and the other three classes 45/45. Full EditMode: 1938/1938 passed (includes the 2 new `LocalVersusSceneContractTests`). Full PlayMode: 117 total, 97 passed, 0 failed, 20 skipped (skipped tests are
opt-in fixtures gated by environment variables, including the process-certification fixture).
`./scripts/validate-repository.ps1`: passed.

**Process-level persistence** (`LocalVersusProcessCertificationPlayModeTests`, opt-in, each session its own
`Unity.exe`; only the series files carry state; the handoff file holds a series id, display names and an
attempt id, never a serialized series):

| Session | Result |
| --- | --- |
| 1 | Create Best-of-3 through the real screens, launch and complete one participant's attempt; the series file holds it before the process exits. |
| 2 (fresh process) | Locates the series from the repository alone, next participant is the other side, screen says so, Play Turn works, game 1 resolves, Continue Series lands on game 2. |
| C1 | Live turn started from the real screen, attempt confirmed durable as `Started`, process kills itself with no pause action. |
| C2 (fresh process) | Same game, same `Started` attempt id, same participant up, no forfeit, no game awarded; Play Turn reissues **the same attempt id**; completing it works. |
| GameRules loop | The production `GameRules.HandleMatchEnded` retry loop, with a repository that refuses saves: it retried (>= 2 refused saves), the attempt stayed outstanding, the series file was untouched, leaving was refused without forfeiting, and the next pass after the disk recovered stored the result and cleared the attempt. No production timing or structure was changed. |

**Standalone player, real input.** Driven through the shipped player with real keyboard and mouse
events, in windowed 1920x1080 unless stated:

- Start screen exposes **Local Versus** as its own row; the screen opens; both names are typed; Ruleset cycles
  and wraps (17 rulesets); Best-of selector cycles exactly 1, 3, 5, 7; Create stores exactly one series file;
  list and detail are readable; the correct participant is up; Character and Arena cycle (19 arenas offered;
  `isSelectable: 0` Dev and Boneyard are absent); Play Turn launches the ordinary gameplay scene.
- Pause presentation: mid-turn it reads Play Again (dimmed, refused), **Quit Turn (Forfeit)**, Cancel, Quit;
  after the match ends it reads **Continue Series**. A real 3-minute attempt ended by the clock, was reported
  through `GameRules` and `VersusMatchReporter`, and Continue Series returned to the same series with the
  other participant up. (Both sides scored 0: aimed shots cannot be driven reliably from synthetic input, so a
  scored win was not produced in the player.)
- **Successful `Application.Quit` (Q1/Q2):** from the live pause menu, Quit forfeited game 1 (opponent won,
  series 1-0, advanced to game 2, the quitter's `Started` attempt became `Abandoned`), the series file was
  written at 18:13:11.756, and the process exited 0.74 s later. A fresh Editor process verified the player's
  repository (`SessionQ2`), and a relaunched player showed "Alice 1 - 0 Bob | Game 2 | Bob is up."
- **Process kill (real player):** a live turn was killed with `Stop-Process -Force`; the series file was
  byte-for-byte unchanged; the relaunched player showed the same game and participant, and reissued the same
  attempt id with no forfeit recorded.
- Restart during an outstanding attempt: Play Again was refused in the player (no scene change, series file
  and attempt unchanged). Quit Turn on the outstanding turn gave the opponent the game and, at 2-0, the series:
  "Alice wins the series.", Play Turn / Character / Arena dimmed.
- Keyboard-only: arrow keys reach every control, Enter starts and ends editing a field, Enter on a field does
  not activate another control, and a Best-of-7 with two 16-character names (`W` / `M`) was created and rendered
  without clipping alongside a finished series. Back returns to Start.

**Rendered layout** (1920x1080, headless render of the shipped scene; every text and control checked against the
screen and its own box): empty, fresh, longest names + Best-of-7, Best-of-7 in progress, completed, and eight
stored series. Also observed in the player at 1280x720 (scales uniformly, legible) and 1024x768 (see limitations).

**Gamepad** (`LocalVersusGamepadNavigationPlayModeTests`, 4 tests, virtual gamepad through the real
`InputSystemUIInputModule`): every control is reachable, Format / Character / Arena / Create / Play Turn work with
Submit, all four pause entries are reachable, Start begins the match and Select (Back) pauses it, and pressing
"Quit Turn (Forfeit)" with Submit forfeits what it says. Back returns to Start. The Start-screen entry itself is submitted directly in these tests (its input routing depends on which fixture ran before); the Start screen was exercised with keyboard and mouse in the player, not with a gamepad.

**Defects found and fixed**

1. *Series list silently truncated.* With more series than the list box holds (seven lines) the rest were cut
   off with no indication, and the `>` selection marker could leave the screen. `LocalVersusScreenModel.ListText`
   now shows a window that always contains the selected series and counts the rest ("... N more above/below");
   when everything fits, the text is unchanged. Regression: `AListLongerThanTheBoxIsWindowedAroundTheSelectionAndCountsWhatIsHidden`
   (fails without the fix). `LocalVersusSceneContractTests` keeps the scene facts the fix depends on (list box holds `ListLineBudget` lines, name limit, no activate-on-select) in the ordinary EditMode run.
2. *Name fields trapped a gamepad.* Moving onto a field with the d-pad started editing it (`shouldActivateOnSelect`),
   and an active field swallowed the d-pad, stick and Cancel - only Submit let go. Both fields now do not activate on
   selection (scene and `LocalVersusSceneBootstrap`); click, Enter and Submit still edit. Trade-off: a keyboard-only player now presses Enter to start typing in a field (until then WASD navigate, as everywhere else in the menus); there is no on-screen hint. Regression:
   `AGamepadThatMovesOntoANameFieldIsNotTrappedInIt` (fails without the fix); confirmed with real keyboard input in
   the player.

**Known limitations and not certified**

- **No physical gamepad was tested.** Gamepad certification is virtual devices only.
- **Not exercised in the player: a failed forfeit or result save.** A player build has no seam to refuse saves;
  those paths are certified in PlayMode (`LocalVersusProductionSmokePlayModeTests`, and the GameRules loop above).
- **No scored win was produced by real play** (see above); win/draw/forfeit logic is covered by the automated suites.
- **Narrow aspect ratios clip.** The screen is laid out for the project's 16:9 canvas contract (1920x1080 reference,
  match 0.5). Below about 1.43:1 (4:3, 5:4 - e.g. the project's 1024x768 default window) the outer few percent of
  both panels are cut off. 16:9, 16:10 and 3:2 are unaffected. Not changed: fixing it means leaving the shared canvas
  contract or re-authoring the panels, and other menu screens have the same 16:9 assumption.
- **Navigation is asymmetric** (automatic geometric navigation): Down from Player 1 goes Player 2 -> Format (skipping
  Ruleset) and Up from Format goes Ruleset -> Player 1 (skipping Player 2). Every control is reachable; it is not a trap.
- **Selection highlight on the name fields is subtle** (light grey on white); the buttons highlight clearly.
- Observed and out of scope: the Start footer labels are truncated at these sizes ("ACCOU", "CONTRO", "QUI"); a fresh
  install logs `no such table: User` once while the local profile screen loads; the shipped scripting backend
  is IL2CPP and was the backend certified; Windows only (no mobile, console or macOS/Linux).
- Arenas Crank Zone and the second Rumble Pit are not offered for Most Points; consistent with the mode/arena
  compatibility gate but not independently traced.

Reproduce: build the player with `LocalVersusCertificationBuild.BuildWindows64` (Editor `-executeMethod`), and run
each `LocalVersusProcessCertificationPlayModeTests` method as its own process (`SessionC1` kills its own process and additionally needs `LEVEL5_LOCAL_VERSUS_CERT_ALLOW_KILL=1`) - see that fixture's header for the
environment variables.


---

## 15. Local simultaneous play (Most Points / The Scrapyard)

Two people on one device play the **same** gameplay match at the same time. Their separate results
resolve the existing `VersusGame`, and the existing series advances normally. This is one production
vertical slice, not general local-simultaneous support:

```text
Most Points (most-points / GameModeId.TotalPoints) + VersusMode.LocalSimultaneous
  + two local humans + The Scrapyard (the only ArenaCapability.Multiplayer arena) + Best of 1/3/5/7
```

There is no second versus domain. `VersusMode.LocalSimultaneous`, `VersusCapability.LocalSimultaneous`,
capability validation, `AttemptResult`, the ruleset's comparison keys, `GameResult`,
`VersusSeries.Advance` and persistence are the existing ones; this slice adds the operations that keep
a *pair* of attempts together.

### What is enabled, and what deliberately is not

`DefaultCompetitiveRulesets` gives `most-points` `Anytime | SimultaneousCertified` - a separate constant,
because every score, make-count and contest ruleset shares `Anytime` and none of the others has been
certified. `most-points` keeps ruleset version 1 and its comparison keys (score, accuracy, fewer
attempts): this is a new competition topology, not a scoring change, and series frozen before this change
keep their own snapshot (capabilities are stored per ruleset in the document). A simultaneous series must
be sealed: `VersusSeriesValidator` refuses `LocalSimultaneous` with `OpenTarget`, which needs one side to
finish before the other starts.

Not enabled: marker contests, distance, streak, Bash Up Some Nerds, Battle Royal, Cage Match, Lockdown,
Versus CPU, three or four local players, split screen.

### Domain operations (all existing types, one save per pair)

| Operation | Does |
| --- | --- |
| `VersusSeries.IssueSimultaneousAttempts` / `VersusMatchCoordinator.IssueSimultaneousAttempts(seriesId)` | series active, `Mode == LocalSimultaneous`, current game exists and its frozen ruleset supports the capability, both participants eligible; issues one attempt each for the *same current game*, both checked before either is issued. Idempotent per participant (a retry, double tap or crash before the scene loaded returns the same two attempts). One save. |
| `StartSimultaneousAttempts(seriesId, first, second)` | marks both attempts started; one save. |
| `VersusSeries.SubmitSimultaneousResults` / `VersusMatchCoordinator.SubmitSimultaneousResults(seriesId, AttemptSubmission, AttemptSubmission)` | see below. |

The single-participant `IssueAttempt` and `SubmitResult` **refuse** a simultaneous series (and
`CanIssueAttempt` says why): a same-time game has no turn, and recording one side alone is exactly the
half-played state the pair operations exist to prevent. Alternating and asynchronous series are untouched.

### Atomic result submission

A pair is applied to one loaded series and saved once; it is never two durable operations.

1. Series active, simultaneous; both participants are series participants and *different* ones (so exactly
   the two); both attempt ids are valid, distinct and **in the current game** (an attempt from another game
   is refused, not completed out of order).
2. `VersusGame.SubmitSimultaneousResults` validates *everything* before changing anything: each attempt
   belongs to the participant it is submitted for, each can still be completed (not completed - a completed
   attempt is never silently overwritten - and not abandoned), each result matches its attempt's ruleset id
   and version, and `Ruleset.Compare` accepts the pair. Only then are both attempts completed, and the game
   resolves through the ruleset's own comparison (draws included).
3. `VersusSeries.Advance` decides the series exactly as after any resolved game (Best-of-N completion, early
   termination, next game activation).
4. The coordinator saves once. If the domain refuses or the save fails, neither result is durable - nothing
   caches the mutated series, so the next load is the last saved one with both attempts still outstanding -
   and the same pair can be resubmitted. `AttemptCompleted` (both), `GameResolved`, `SeriesAdvanced` and
   `SeriesCompleted` fire only after the save succeeds.

### Launch

`VersusLauncher.LaunchSimultaneous(seriesId, firstCharacter, secondCharacter, levelId, unlock, modifiers)`;
`Launch` is unchanged. It checks everything that costs nothing before anything is spent, then:

```text
load series -> CanIssueSimultaneousAttempts -> level eligibility (LevelEligibility) -> both characters unlocked
  -> device preflight for two humans (PlayerControlsProvider.TryPreflightGameplayDevices, as StartManager does)
  -> build an ordinary MatchRequest:  slot 0 = LocalHuman / Participants.First, slot 1 = LocalHuman / Participants.Second
  -> MatchConfigurationBuilder.Build  (ArenaCapability.Multiplayer, character/mode compatibility, lock state: the gate)
  -> IssueSimultaneousAttempts -> ActiveMatch.Begin -> ActiveVersusAttempt.BeginSimultaneous
  -> LegacyGameOptionsBridge.Apply -> StartSimultaneousAttempts -> scene load
```

It never spawns a player or assigns a device: the gameplay scene composes the match from the roster and
the match-local device plan (`player-input-architecture.md`). Unlike the alternating path, the match is
*built before the attempts are issued*, so an arena/mode/character the builder refuses consumes nothing.
(`OverrideDevicePreflight` is the test seam: a test machine cannot be assumed to have two gamepads.)

### Participant identity

Never inferred from score order:

```text
Participants.First  -> roster slot 0 -> runtime Player1 (GameLevelManager.Player1) -> GameStats for First
Participants.Second -> roster slot 1 -> runtime Player2 (GameLevelManager.Player2) -> GameStats for Second
```

`ActiveVersusAttempt` keeps its existing `AttemptId`/`ParticipantId` (the first participant's) and gains
`SecondAttemptId`, `SecondParticipantId`, `IsSimultaneous` and `BeginSimultaneous`. `IsActive` is still bound
to the exact `ActiveMatch.Configuration`. At match end `GameRules` passes slot 0's stats and
`GetSecondaryGameStats()` (runtime `Player2`, not an entry of the sorted list) to
`VersusMatchReporter.TryReport(first, second, ...)`; `GameStatsAttemptResults.Build` is still the only
mapping from stats to metrics, and both attempts use the common match completion time (not a
`most-points` key). The three-argument `TryReport` still reports an alternating turn.

### Persistence and progression: competition-only

There is no player-specific local identity or progression contract for two humans yet, so a simultaneous
match is competition-only. `GameRules` latches `matchIsCompetitionOnly` when the match first ends and, for
such a match, skips: the ordinary high-score save, the all-time stats save, the Backend V2 `/match-results`
queue, and primary-player character/account progression. The series document is the only record. LocalAlternating and
ordinary matches are unchanged (an alternating turn still persists as an ordinary single-player match, as
before). If a two-player local identity contract is introduced, this is the place that changes.

### Leaving and restarting

Fails closed rather than guessing who conceded.

- **Restart** is refused while a simultaneous attempt is active (`Pause.reloadScene` already refuses while
  any attempt is outstanding).
- **Menu / Quit** are refused mid-game: `VersusQuitPolicy.TryPrepareForExplicitExit` returns false for a
  simultaneous game and forfeits nobody (`ForfeitActiveTurn` also refuses - slot 0 is never assumed to be the
  forfeiting player). The pause menu reads `SimultaneousGameInProgress` to show "Exit unavailable during a
  game" and, when pressed, "Unavailable until the game ends" instead of the single-player forfeit wording.
- After the pair is durably recorded `ActiveVersusAttempt` is cleared and leaving is ordinary. A match that
  has ended but whose pair is still waiting on a save retry cannot be left either (same as alternating).
- A crash or process exit is an interruption, not a forfeit: both attempts stay outstanding and the next
  launch returns the same two.
- No participant-owned pause, concede button, no-contest or consensual restart exists.

### Screen

The existing Local Versus screen, not a new one. The create form gains a **Mode** selector (Local
Alternating / Local Simultaneous); the ruleset list is re-read through `VersusCapability` for the chosen mode
(only Most Points for simultaneous). An alternating series keeps `NextParticipant`, one character selector
and **Play Turn**. A simultaneous series has no next participant: it shows **Player 1 (name)** and
**Player 2 (name)** character selectors, the arena selector (only `Multiplayer` arenas) and **Play Game**.
Nothing designates one side as "the turn". The list shows both kinds of local series; the series remains the
only authority, so the detail text, score and resume all read the loaded series (a finished game returns to
the series on the next game; a restart between games resumes it; a completed series and its history survive a
restart). The screen is rebuilt after every gameplay scene, so the two character picks are kept in
`LocalVersusSimultaneousPicks` (character ids keyed by series; selection only, one series remembered, forgotten
on a process restart) and recalled when the series is next selected; a recalled character that is no longer
offered falls back to the default. The authored scene gained the `modeButton` and `character2Button` controls (the latter hidden
unless a simultaneous series is selected), and the panels' buttons were compacted from 80 to 64px with 8px
spacing so the taller simultaneous state fits the 1080px canvas; `LocalVersusSceneBootstrap` produces the
same layout from scratch (`Add Simultaneous Controls` upgrades an existing scene in place).

### HUD

Total Points with a second human now shows both scores: the clock-side score reads "P1 - P2", each player has
a panel in roster order ("Player 1" is always slot 0, never re-sorted by who leads), the ordinary high
score line is blank (a two-human match does not update it), and the end summary lists both players. This
applies to any two-human Total Points match, competitive or not; single-player HUDs are unchanged.
`MatchHudPresenter.BindSecondHumanContext` supplies slot 1; unbound it is the single-player HUD.

### Certified combinations

| Ruleset | Mode | Arena | Humans | Status |
| --- | --- | --- | --- | --- |
| `most-points` (Total Points) | `LocalSimultaneous`, Best of 1/3/5/7 | The Scrapyard | two | the only enabled combination |

### Verification status

Run on 2026-09-30 against Unity 6000.5.7f1 in batch mode:

- **Compile:** clean. **EditMode (full):** 2032 / 2032 passed (includes 28 `Level5VersusSimultaneousDomainTests`,
  42 `Level5LocalSimultaneousVersusTests`, 44 existing `Level5LocalVersusTests`, `LocalVersusSceneContractTests`
  with the new canvas-fit measurement, `Level5VersusArchitectureTests`). **PlayMode (full, with graphics):**
  106 passed, 0 failed, 20 skipped - the skips are the opt-in tests that need a live backend or
  `LEVEL5_LOCAL_VERSUS_CERTIFICATION=1`. This includes the 3 `LocalSimultaneousVersusProductionPlayModeTests`, the
  existing two-human input/shared-camera fixtures (12), Local Versus production smoke (5), gamepad navigation
  (4) and menu screens (7). `Level5ProjectValidator.ValidateOrThrow` and `scripts/validate-repository.ps1`
  pass.
- **End to end:** `ABestOf3SimultaneousSeriesIsPlayedToCompletionThroughTheProductionScreens` drives the real
  Start -> Local Versus -> Mode -> Create -> Play Game (two humans, The Scrapyard, real `GameRules` match end) ->
  Continue Series loop for a whole Best of 3 and restarts between series and history. This is automated
  (virtual gamepads, scores set by the test); nobody played it by hand.
- **Layout:** the tallest series state ends at 1064 of the 1080 reference canvas and the create panel at 862,
  clear of Back at 930 (`TheTallestPanelStatesStillFitTheReferenceCanvasAndClearBack`). The rendered
  certification was not run when this slice merged; it has since been - see "Certification (2026-09-30)".
- **Not done at merge, done since:** the process-kill and fresh-process restart sessions and the rendered layout
  (see "Certification (2026-09-30)"). **Still not done:** keyboard + physical gamepad and two physical gamepads
  (hardware), and a hand-played run.
- The scene was edited as YAML (the simultaneous controls were cloned from the existing buttons) rather than in the
  editor; the suites above load and use it. Unity's own save of it is semantically identical but reorders the file
  (see "Certification (2026-09-30)", scene serialization).

### Certification (2026-09-30)

Certification of the slice above against `dev` `8ce32033afc02c4612a2e9fd00d39568a7341952` (PR #217, "Do not submit a
simultaneous versus pair when a slot has no stats", is the one commit past the audited `f955aef`). Certification-first:
production data changed only for the one defect below. Unity 6000.5.7f1 (`017862109af0`), Windows 10 Pro, one RTX 4060.
Every process session ran as its own `Unity.exe`, with the isolated product name `level5-local-versus-cert-editor` (set
temporarily in `ProjectSettings`, never committed), a temporary repository root and a handoff file outside it that holds
identifiers and display names only. Two virtual gamepads stand in for the two humans; **no physical controller was
attached to the machine** (see "Not certified").

**Final suites on the certified tree.** Full EditMode 2040/2040. Full PlayMode (graphics enabled): 135 tests, 108 passed,
0 failed, 27 skipped - the skips are the opt-in fixtures (12 live-backend, 8 process-certification, plus the 7 added
here), all of which were then run individually (below). `./scripts/validate-repository.ps1` and
`Level5ProjectValidator.ValidateFromMenu` pass. The pre-change baseline was 2039/2039 and 127/107/0/20, which also
closes the gap #216 reported (its last same-character selection-test fix had no full run after it: it passes).

| Gate | Result |
| --- | --- |
| Scene serialization (`level_00_local_versus`, opened in the editor) | all `LocalVersusUiObjects` references resolve; buttons register callbacks in code (no persistent listeners, by design); navigation is Automatic everywhere; `character2Button` is authored hidden and `modeButton` visible; EventSystem selects `createButton` first. Unity's save of the scene is semantically a no-op (175 YAML documents, one reordered) but rewrites 1272 lines, so it was **not** committed: the authored file is valid, just not in Unity's canonical order, and the next editor save of it will produce that noise. |
| Rendered layout, simultaneous | 15 states at 1920x1080, screenshots + boundary, truncation (rendered height and TMP's own `isTextTruncated`), control-overlap, selection, navigation-reachability and mode-visibility checks: create form; fresh Best-of-3; both selectors swept through every character and held on the longest; longest names Best-of-7 fresh / 2-2 in progress / completed; completed Best-of-3; mixed alternating+simultaneous lists (each kind selected, a finished alternating one, the longest completed row); create error; launch error (device preflight); launch error (pair could not be saved). Passes. |
| Rendered layout, alternating | 7 existing states, now with the same stronger checks. Passes. |
| Fresh-process restoration | S1 (create Best-of-3 through the real screens, one shared game through the real `GameRules`, both results durable) -> S2 (fresh process: series found from the repository, game 1 verified incl. attempt ids, games 2-3 played, series complete 2-1) -> S3 (third process: completed series and history intact, no game offered). |
| Forced process kill | SC1 (shared game live, both attempts durably `Started`, ids recorded, process killed with no pause action) -> SC2 (fresh process: same game, neither forfeited, score 0, both original attempts reused on relaunch, completes with exactly one durable result). |
| Atomic recovery | the real `GameRules` loop with a repository that refuses saves: retried (>= 2 refused saves), both attempts and the competitive context stay outstanding, the game does not advance, leaving is refused; after recovery the pair is recorded once and the game advances once; winner by roster slot. Domain events-after-save, slot identity against score order and single-attempt refusal were already pinned by `Level5VersusSimultaneousDomainTests` / `Level5LocalSimultaneousVersusTests` and were not duplicated. |
| Competition-only persistence | `ASimultaneousMatchEndRetriesAtomicallyAndWritesOnlyTheSeries` snapshots the isolated persistent data path around the match end: an alternating control turn wrote `level5.db` and `guest-pending-progression.json(.bak)`; the simultaneous match wrote **nothing** outside the series. Mutation check: with the latch in `GameRules` disabled, the test fails on exactly those files. |
| Gamepad navigation, simultaneous | Mode, Create, Player 1 and Player 2 selectors (independent), Play Game seating two humans, by real d-pad/Submit through `InputSystemUIInputModule`. The Arena selector is disabled (one multiplayer arena) and correctly not a navigation stop. |
| Alternating regression | Session1/2 (restart), C1/C2 (kill), the `GameRules` retry loop and the alternating layout all pass again. |

**Defect found and fixed.** The `turnMessage` label was a fixed 30pt with Truncate overflow in a 60px box. A launch
refused by the two-human device preflight ("Could not start the game: Two local players need two gamepads, or a keyboard
plus one gamepad. Detected: no keyboard, 1 gamepad(s).") needs three lines, so the sentence's second half - the devices
actually detected, the part a player can act on - was cut off (state `sim-14`). Owner: authored scene data. The label now
auto-sizes 18-30pt in the scene and in `LocalVersusSceneBootstrap`, in a two-line YAML edit, not a reserialization.
Regression: `LocalVersusSceneContractTests.ARefusedTwoPlayerLaunchIsReadableInTheTurnMessageBox` (fails without the fix;
also measures the longer touchscreen variant at the smallest permitted size). The opt-in layout run failed on exactly
that state before the fix and passes after it.

**A check defect, not a product defect.** The first simultaneous layout run also flagged `seriesSelectButton` ("needs
68px but its box is 64px") in states shared with alternating play, which the alternating layout run of the same tree
reproduced. TMP measures `preferredHeight` at the *maximum* size when auto-sizing is on, and these labels auto-size
20-34pt; the screenshots show them fitted. The check now measures the rendered height for auto-sized text and asks TMP
whether an overflow mode cut anything off, which is stricter for truncation and correct for fitted labels.

**Not certified**

- **No physical device was tested.** Keyboard/mouse + one gamepad and two physical gamepads (initial assignment,
  independent movement/shooting, stable ownership through pause/resume and across a Best-of-3, camera playability) are
  **untested**; the machine had no controller. The virtual-device tests are regression coverage, not certification.
- **No hand-played run.** Nothing here was played by a person; scores are set by the test and matches end through the
  real `GameRules.RequestGameOver`, so real shooting, scoring and shared-camera play are unobserved.
- The certification ran in the Editor's PlayMode (and its Game view at 1920x1080), not in a built player.
- Only 1920x1080 was rendered; the 16:9 canvas contract and its narrow-aspect limitation (see the Local Versus
  certification above) are unchanged.

**Prerequisites for `most-3-pointers` (not implemented).**

1. Declare the capability: `most-3-pointers` is `MakeCount(...)` with `Anytime` only; it needs the same separate
   "certified for simultaneous" grant `most-points` has, and `OnlyMostPointsDeclaresLocalSimultaneousInTheShippedRulesets`
   and the screen-model tests that assert "only Most Points" change with it.
2. The two-player HUD: `BindSecondHumanContext` and the dual-score presentation exist for Total Points only. The 3s-made
   clock-side score, per-player panel and end summary need their own two-slot presentation.
3. Attribution: audit that `ThreePointerMade` lands on the launch-time shooter for both humans (the shot pipeline is
   launch-state based - see `docs/shot-lifecycle.md`), including one player's ball scoring while the other is mid-shot, and
   that nothing in the mode's end condition or timer assumes one player.
4. Match construction: `MatchConfigurationBuilder`'s mode/arena/character compatibility for two humans in
   `Total3Pointers` on The Scrapyard (the only multiplayer arena), and whether the mode needs anything Most Points does not.
5. `GameStatsAttemptResults` already maps `ThreePointerMade`; confirm the ruleset's comparison keys (count, then fewer
   attempts, or accuracy) are what two players should be ranked by, and keep ruleset version rules for the new topology.
6. Tests to add, mirroring this slice: a Best-of-3 through the real screens on two virtual gamepads, a rendered layout
   state with the ruleset's labels, atomic-pair/retry through the real `GameRules`, the competition-only persistence
   probe (`ASimultaneousMatchEndRetriesAtomicallyAndWritesOnlyTheSeries` generalizes to it), and the process sessions.
7. Do it after the two physical-device configurations above have been certified, so a second ruleset is not stacked on
   an uncertified input path.

### Limitations

- One ruleset and one arena. `points-by-distance`, `in-the-pocket`, make-count, distance, streak and contests
  need their own certification (for example: contests and marker modes need a shared marker/ball contract for
  two players, distance/streak need per-player state audited) - none is implied by this.
- Two humans only; no hot-plug reassignment, remapping, persistent local profiles, dual-player progression or
  player-owned pause/concede.
- Both participants always share the same arena, modifiers (default) and the match clock.
- Both humans may pick the same character; this is exercised in PlayMode (separate actors, stats and results).
- `GameRules` has one other versus touchpoint besides the reporter call: the single latch that makes a
  simultaneous match competition-only. `Level5VersusArchitectureTests.TheGameplayFootprintIsOneCall` pins both.
- The Local Versus panels lay out 64px buttons (was 80px). The rendered layout certification has been re-run against
  them for alternating and simultaneous series - see "Certification (2026-09-30)".
- The dual-score HUD exists for Total Points only; any other ruleset made simultaneous needs its own
  two-player score presentation.

**Recommended next ruleset to certify:** `most-3-pointers`. The stats-to-metric mapping for it already exists
(`GameStatsAttemptResults` reads `ThreePointerMade`), and it needs no shared shot markers or ball-call state,
so it adds the least new per-player state. It still needs its HUD presentation and its own two-human
PlayMode fixture; certify one ruleset at a time.

---

## 13. Tests

The versus suite lives in `Assets/Tests/Editor`, with runtime smoke coverage in
`Assets/Tests/PlayMode/Level5GameplayPlayModeTests.cs`:

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
| `Level5LocalVersusTests` | the production Local Versus flow: creation contract, ruleset/format, list filtering, resume, turn ownership, character/arena eligibility, launcher composition, return navigation, Backend V2 independence, and the explicit-exit durability gate (live-turn forfeit, failed forfeit save, ended match awaiting its result save, reporter retry, crash reissue, Pause exit coroutines, Restart refusal) |
| `Level5VersusSimultaneousDomainTests` | the simultaneous domain operations: capability and validation (most-points only, sealed only), paired issue (same game, one attempt each, idempotent, retry after interruption), paired results (either side wins, tie-breaks, draw, Best-of-3 and Best-of-7 early termination), pairing validation (swapped/duplicated/unknown/foreign/earlier-game attempts, overwrite, rules mismatch), atomicity (nothing completed on refusal, persistence failure leaves neither durable and the retry is safe, events only after save), single-participant issue/submit refused |
| `Level5LocalSimultaneousVersusTests` | `VersusLauncher.LaunchSimultaneous` (two `LocalHuman` slots in series order, both characters, non-multiplayer/locked arena, locked/missing character on either side, device preflight, nothing spent on refusal, bound to the exact match), match-end reporting by slot (winner mapping with scores arranged against sort order, tie keys, draw, retry after a save failure), exit/restart policy (no forfeit, crash recoverable, exit after the pair is durable), persistence separation (GameRules guard), and the Local Versus screen model (mode selector, capability-filtered rulesets, two selectors, multiplayer arenas only, series resume and restart) |
| `LocalSimultaneousVersusProductionPlayModeTests` | the production Most Points slice through the real scenes on two virtual gamepads: Best of 3 played to completion and surviving a restart, roster/device-plan/HUD checks, refused exit and restart, and the real `GameRules` loop saving the pair atomically under a failing disk |
| `LocalVersusProductionSmokePlayModeTests` | the production Local Versus loop through the real scenes (in-process restart, live-turn forfeit, failed forfeit save, result awaiting its save) |
| `LocalVersusGamepadNavigationPlayModeTests` | the Local Versus screen and pause menu driven only by a virtual gamepad: reachability, Format/Character/Arena/Create/Play/Back, no focus trap, Quit Turn (Forfeit) |
| `LocalVersusSceneContractTests` | the authored scene facts the screen model and input behavior depend on: list box vs `ListLineBudget`, name-field limit, no activate-on-select |
| `LocalVersusProcessCertificationPlayModeTests` | opt-in (`LEVEL5_LOCAL_VERSUS_CERTIFICATION=1`): genuine process restart, process kill, the real `GameRules` retry loop, player-quit verification and rendered layout - see "Local Versus certification"; and, for simultaneous play, sessions S1-S3 / SC1-SC2, the competition-only atomic-recovery test and the simultaneous layout check - see "Certification (2026-09-30)" |
