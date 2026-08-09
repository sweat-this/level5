# `GameOptions` inventory

Status: Phase 0 baseline for the [game options and match system overhaul](game-options-match-system-overhaul-plan.md)
Last reviewed: 2026-08-08

`GameOptions` is a legacy compatibility surface being retired. This is the inventory the plan asks
for before anything is deleted: every field, which category it belongs to, who owns it now, and
where it is going.

Two automated guards keep this honest, both in `Assets/Tests/Editor/Level5MatchArchitectureTests.cs`:

- `NoNewFileReachesForGameOptions` - a file not on the migration allowlist may not touch
  `GameOptions` at all. The allowlist is the remaining debt, written down.
- `GameOptionsGrowsNoNewMatchFields` - the field count is a ratchet. It may go down, never up.

## Where the authority is now

```text
start menu selection  ->  MatchRequest  ->  MatchConfigurationBuilder  ->  MatchConfiguration
                                                     |                            |
                                                     |                            +--> ActiveMatch (authoritative)
                                                     |                            |
                                              GameModeCompatibility               +--> LegacyGameOptionsBridge --> GameOptions
```

`MatchConfiguration` is authoritative. `GameOptions` receives a copy, once, from
`LegacyGameOptionsBridge`. Nothing reads back the other way. A gameplay scene entered directly -
with no launch - reads the globals through `MatchRuntime`, which is the only exception and is
read-only.

## Field categories

These say where each field's *meaning* now lives. Some of the fields listed have since been deleted
outright - see "Fields already deleted" at the end for which.

### Match rules - migrated, written only by the bridge

| Field | Replaced by |
| --- | --- |
| `gameModeSelectedId` | `MatchConfiguration.ModeId` (`GameModeId`) |
| `gameModeSelectedName` | `GameModeDefinition.DisplayName` |
| `gameModeHasBeenSelected` | `ActiveMatch.IsActive` |
| `gameModeRequiresCounter`, `gameModeRequiresCountDown` | `MatchClockMode` |
| `customTimer` | `ResolvedMatchRules.CustomTimerSeconds` / `MatchLengthSeconds` |
| `gameModeThreePointContest`, `gameModeFourPointContest`, `gameModeSevenPointContest`, `gameModeAllPointContest` | `ShotRule` |
| `gameModeRequiresShotMarkers3s`, `...4s`, `...7s` | `ShotMarkerRequirement` (flags) |
| `battleRoyalEnabled`, `cageMatchEnabled` | `CombatMode` |
| `EnemiesOnlyEnabled` | `GameModeDefinition.EnemiesOnly` |
| `gameModeRequiresBasketball` | `ResolvedMatchRules.RequiresBasketball` |
| `gameModeRequiresMoneyBall` | `ResolvedMatchRules.RequiresMoneyBall` |
| `gameModeRequiresConsecutiveShot` | `ResolvedMatchRules.RequiresConsecutiveShots` |
| `gameModeRequiresPlayerSurvive` | `ResolvedMatchRules.RequiresPlayerSurvive` |
| `gameModeAllowsCpuShooters` | `ResolvedMatchRules.AllowsCpuShooters` |
| `arcadeModeEnabled` | `ResolvedMatchRules.ArcadeMode` |

### Level capabilities - migrated

| Field | Replaced by |
| --- | --- |
| `levelId`, `levelSelected`, `levelDisplayName` | `LevelDefinition` |
| `levelRequiresTimeOfDay` | `ArenaCapability.TimeOfDay` |
| `levelRequiresWeather` | `ArenaCapability.Weather` |
| `levelHasSevenPointers` | `ArenaCapability.SevenPointLine` |
| `customCamera` | `LevelDefinition.CustomCamera` |
| `levelSelectedName` | unused by the new path; still written by older navigation code |

### Roster - migrated

| Field | Replaced by |
| --- | --- |
| `numPlayers`, `numCpuPlayers` | `PlayerRoster.Count` / `CpuCount` |
| `player1IsCpu` .. `player4IsCpu` | `PlayerSlot.ControlType` |
| `characterObjectNames` | `PlayerSlot.Character.ObjectName` |
| `characterObjectName`, `characterId`, `characterDisplayName` | `PlayerRoster.PrimaryLocalHuman.Character` |
| `GetHumanPlayerInputSlot` | `PlayerSlot.LocalInputSlot` |
| `playerIds` | unused |

### Player modifiers - migrated

| Field | Replaced by |
| --- | --- |
| `trafficEnabled` | `ResolvedMatchRules.TrafficEnabled` (resolved against the arena) |
| `enemiesEnabled` | `ResolvedMatchRules.EnemiesEnabled` (resolved against the mode) |
| `obstaclesEnabled` | `ResolvedMatchRules.ObstaclesEnabled` |
| `difficultySelected` | `MatchDifficulty` |
| `hardcoreModeEnabled` | `ResolvedMatchRules.Hardcore` |
| `sniperEnabled`, `sniperEnabledBullet`, `sniperEnabledBulletAuto`, `sniperEnabledLaser` | `SniperMode` |

### Cheerleader - migrated

All of these became `CheerleaderSelection` on the configuration. Only `cheerleaderObjectName` and
`cheerleaderDisplayName` still exist as globals; `cheerleaderId`, `cheerleaderSelectedName` and
every `friendBonus*` have been deleted.

### Menu preferences - not match state

`playerSelectedIndex`, `levelSelectedIndex`, `modeSelectedIndex`, `friendSelectedIndex`,
`cpu1SelectedIndex`, `cpu2SelectedIndex`, `cpu3SelectedIndex`.

Owned by `StartMenuSelectionState`, which is the only place that reads or writes them
(`LoadPersistedPreferences` / `SavePersistedPreferences`). That pair is the seam for moving them to
a menu preference store - plan phase 11.

### Account, session and application - separate owners, not this overhaul

| Field | Target owner |
| --- | --- |
| `userName`, `userid`, `numOfLocalUsers` | account / session service |
| `bearerToken` | API authentication only |
| `applicationVersion`, `operatingSystemVersion` | application/platform service |
| `previousSceneName` | navigation / session flow service |
| `matchResultId` | `MatchSession` |
| `levelsList` | campaign session owner |
| `tipDialogueLoadedOnStart` | start menu state |

## Consumers still reading `GameOptions` directly

The allowlist in `Level5MatchArchitectureTests` is the live list; it is not duplicated here so the
two cannot disagree. It holds four groups:

1. the boundary itself (`LegacyGameOptionsBridge`, `MatchRuntime`, `StartMenuSelectionState`);
2. menu, navigation and start-screen widgets;
3. account, API and persistence, which are a separate overhaul;
4. gameplay scripts that have not moved yet.

Group 4 shrank by 36 files across two migration slices - enemies, combat, projectiles, cameras,
traffic, time of day, the basketball scripts, the shot and range meters, collisions, health, the
character profile, the pause menu, the enemy spawner and the NPC behaviours all take their answer
from `MatchRuntime` now. Every one was a pure read of a value the bridge had just written, so the
substitution changes nothing about what they see, only where they look.

Four files remain in that group, and none of them is a simple substitution: `GameLevelManager`,
`GameRules` and `SpawnCoordinator` sit on the boundary themselves, and `RacingGameManager` belongs
to a subsystem that is mostly commented out.

`StartManager` is the exception worth noting - it went from 185 direct `GameOptions` uses to six,
none of them match rules: the previous scene name, the application version and platform, the
campaign level list, and the menu index the progression screen reads.

## Reading the match from gameplay

`MatchRuntime` is the boundary. It resolves to the validated configuration when one exists and to
the legacy globals when the scene was entered directly:

```csharp
if (MatchRuntime.Rules.EnemiesEnabled) { ... }          // instead of GameOptions.enemiesEnabled
if (MatchRuntime.Rules.Sniper == SniperMode.Laser) { }  // instead of GameOptions.sniperEnabledLaser
if (MatchRuntime.CustomCamera) { ... }                  // instead of GameOptions.customCamera
```

## Fields already deleted

Twenty reached zero consumers and are gone:

| Field | Why it could go |
| --- | --- |
| `gameModeRequiresMoneyBall`, `gameModeRequiresConsecutiveShot` | only `GameRules` read them, and it reads `ResolvedMatchRules` now |
| `gameModeRequiresPlayerSurvive` | its last read was already commented out |
| `arcadeModeEnabled` | `ResolvedMatchRules.ArcadeMode` |
| `customCamera` | `ArenaCapability` / `LevelDefinition.CustomCamera` |
| `friendBonus3Accuracy`, `friendBonus4Accuracy`, `friendBonus7Accuracy`, `friendBonusRelease`, `friendBonusRange`, `friendBonusLuck`, `friendBonusClutch` | `CheerleaderSelection` on the configuration |
| `friendBonusSpeed`, `friendBonusAttack`, `friendBonusHealth`, `friendBonusDefense` | already dead before this work |
| `cheerleaderId`, `cheerleaderSelectedName`, `cheerleaderSelectedIndex` | written by the launch path, read by nothing |
| `playerIds` | never read by anything |

`GameOptionsGrowsNoNewMatchFields` holds the count at 65, down from 85. Lower the number when more
go; never raise it.

## What is deliberately not done

Deleting the rest of the migrated fields. They still have consumers, and deleting them now would be
the all-at-once migration the plan exists to avoid. The order is: migrate a consumer, remove it from
the allowlist, and delete the field once nothing outside the boundary names it.

The boundary for that judgement is four files - `GameOptions` itself, `LegacyGameOptionsBridge`,
`MatchRuntime` and the tests' `GameOptionsSnapshot`. A field only those four mention is dead.
