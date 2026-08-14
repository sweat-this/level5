# Versus Dev Console Guide

Last updated: 2026-08-11

There is no production versus menu yet. `VersusDevConsole` is the development-only way to create,
resume and play a versus series through the same coordinator and launcher a real menu will use.

Use this guide when you want to actually play the current local-alternating or correspondence flow
inside Unity.

## What It Plays

The current versus-series model is one participant per match:

```text
create series
  -> player A takes one run
  -> player B takes one run later
  -> the game resolves
  -> the series advances or completes
```

That means local versus is **local alternating**, not same-screen simultaneous multiplayer. The old
Versus CPU game mode is still ordinary gameplay and is not this series system.

## Setup

1. Open Unity.
2. Open a dev or menu scene.
3. Create an empty GameObject named `Versus Dev Console`.
4. Add the `VersusDevConsole` component.
5. Configure the Inspector fields.

Recommended first run:

| Field | Value |
| --- | --- |
| `Participant A Id` | `local-a` |
| `Participant A Display Name` | `Player A` |
| `Participant B Id` | `local-b` |
| `Participant B Display Name` | `Player B` |
| `Game Count` | `1` |
| `Mode` | `LocalAlternating` |
| `Information Policy` | `SealedAttempt` |
| `Playlist` | `most-points` |
| `Level Id` | `1` |
| `Character Object Name` | `drblood` |
| `Use In Memory Store` | off |

Leave `Use In Memory Store` off when you want the series to survive scene loads or editor play-mode
restarts. Turn it on only when you want a disposable session.

## Play A Series

1. In the component context menu, choose `Versus/Create series`.
2. Check the Unity Console for the current series state and id.
3. Choose `Versus/Take next turn`.
4. Play the launched match normally.
5. Finish the match.
6. Return to the scene containing `VersusDevConsole`.
7. Choose `Versus/Report` to inspect the series.
8. Choose `Versus/Take next turn` again.
9. Repeat until the series completes.

For correspondence simulation, do the same thing with time in between. You can take player A's turn,
quit, reopen later, paste the stored series id into `Current Series Id`, and take player B's turn.

## Useful Context Menu Commands

| Command | Use |
| --- | --- |
| `Versus/Create series` | Creates a new series from the Inspector settings and stores its id. |
| `Versus/Take next turn` | Launches whoever is currently allowed to play. |
| `Versus/Take turn as A` | Tries to launch player A's turn explicitly. |
| `Versus/Take turn as B` | Tries to launch player B's turn explicitly. |
| `Versus/Report` | Prints the series as both participants are allowed to see it. |
| `Versus/List series` | Lists stored series ids so one can be resumed. |

## Good Smoke Tests

Run these manually after changing versus launch, match reporting, persistence or rulesets:

- `most-points`, best of 1: player A scores higher, player A wins.
- `most-points`, best of 3: player A wins game 1, player B wins games 2 and 3, player B wins the
  series.
- `three-point-contest`: equal score should fall through to faster completion time.
- `points-by-distance`: verify score follows distance scoring, not line value.
- `bash-up-some-nerds`: works for `LocalAlternating`.
- `bash-up-some-nerds` with `Asynchronous`: creation is refused because the ruleset is local-only.
- Start a turn, leave the match, play an ordinary match: the ordinary match must not submit as the
  abandoned versus attempt.

## Where Results Are Stored

With the normal file store, series documents live under the platform persistent-data path in the
`versus` folder. The exact location depends on Unity's `Application.persistentDataPath` for the
current editor/player environment.

With `Use In Memory Store` enabled, nothing is written and the series disappears when the runtime
state resets.

## Troubleshooting

- `No series is selected`: create a series, list stored series, or paste an id into
  `Current Series Id`.
- `UnknownRuleset`: the playlist has an id not present in `DefaultCompetitiveRulesets` or authored
  ruleset assets.
- `SeriesNotPlayable`: the selected level cannot play the ruleset's game mode.
- `VersusModeNotImplemented`: live online realtime is intentionally refused right now.
- Match launches as ordinary gameplay: that is expected. The gameplay scene receives a normal
  `MatchConfiguration`; the active attempt is reported only at match end.
