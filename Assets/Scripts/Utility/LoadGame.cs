using System.Collections;
using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Utility
{
	public static class LoadGame
	{
        private static bool sceneLoadPending;

        static LoadGame()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneLoadPending = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneLoadState()
        {
            sceneLoadPending = false;
        }

		public static void LoadGameMode(StartScreenModeSelected mode, LevelSelected level, PlayerIdentifier player)
		{

		}
        //public static void LoadGameMode(StartScreenModeSelected mode, LevelSelected level, PlayerIdentifier player, PlayerIdentifier cpuPlayer)
        public static IEnumerator LoadDevLevelVersus(int seconds)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Debug.LogWarning("Dev level loading is unavailable in release builds.");
            yield break;
#else
            // get level id, mode id
            // get player prefab
            // get cpu prefab
            GameObject player;
            GameObject go1 = GameObject.FindGameObjectWithTag("Player");
            //GameObject go2 = Resources.Load(cpuPrefabPath1) as GameObject;

            player = go1;
            //cpuPlayer = go2;

            if (player == null)
            {
                yield break;
            }

            PlayerIdentifier pi = player.GetComponent<PlayerIdentifier>();
            if (pi == null || pi.characterProfile == null)
            {
                yield break;
            }

            MatchConfiguration configuration = BuildDevVersusMatch(pi.characterProfile.PlayerObjectName);
            ActiveMatch.Begin(configuration);
            // Same one-way push the menu does, for the consumers that have not migrated yet.
            LegacyGameOptionsBridge.Apply(configuration);
            GameOptions.levelsList = PlayerData.instance.LevelsList;

            yield return new WaitForSecondsRealtime(seconds);

            if (sceneLoadPending)
            {
                yield break;
            }

            sceneLoadPending = true;
            SceneTransition.LoadScene(Constants.SCENE_NAME_level_23_dev);
#endif
        }

        /// <summary>
        /// The dev versus match, as a real configuration.
        ///
        /// This used to hand-write a dozen GameOptions fields and load the scene, which made it the
        /// one launch path that skipped validation entirely - the scene then had to reconstruct its
        /// rules from those globals. It now produces a configuration like every other launch source,
        /// so the dev level plays under the same resolution as anything else.
        ///
        /// The mode and level are built in code rather than looked up: the dev level is not in the
        /// authored catalog, which is exactly why this path exists.
        /// </summary>
        private static MatchConfiguration BuildDevVersusMatch(string playerObjectName)
        {
            GameModeDefinitionData modeData = GameModeDefinitionData.Default(Modes.VersusCpu);
            modeData.DisplayName = "Versus";
            modeData.ObjectName = "versus";
            modeData.Objective = MatchObjective.Score;
            modeData.ClockMode = MatchClockMode.Countdown;
            modeData.RequiresBasketball = true;
            modeData.AllowsCpuShooters = true;
            modeData.MinPlayers = 2;
            modeData.RequiresCpuOpponent = true;
            GameModeDefinition mode = GameModeDefinition.Create(modeData);

            LevelDefinitionData levelData = LevelDefinitionData.Default(Levels.Dev);
            levelData.DisplayName = "Dev";
            levelData.ObjectName = Constants.SCENE_NAME_level_23_dev;
            levelData.Capabilities = ArenaCapability.Basketball
                | ArenaCapability.SevenPointLine
                | ArenaCapability.Multiplayer;
            LevelDefinition level = LevelDefinition.Create(levelData);

            PlayerRoster roster = PlayerRoster.Build(new[]
            {
                PlayerRosterEntry.LocalHuman(new CharacterSelection(0, playerObjectName, playerObjectName, true, true)),
                PlayerRosterEntry.Cpu(new CharacterSelection(0, "pony", "pony", true, true))
            });

            return new MatchConfiguration(
                mode,
                level,
                roster,
                MatchModifiers.Default,
                MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
                CheerleaderSelection.None,
                "dev versus");
        }

        //internal static void LoadDevLevelVersus()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
