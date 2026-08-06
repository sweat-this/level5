using System.Collections;
using System.Collections.Generic;
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

            //mode
            GameOptions.gameModeSelectedId = Modes.VersusCpu;
            GameOptions.gameModeSelectedName = "Versus";
            GameOptions.gameModeRequiresCountDown = true;
            GameOptions.gameModeRequiresBasketball = true;
            GameOptions.gameModeAllowsCpuShooters = true;
            //level
            GameOptions.levelId = Levels.Dev;
            GameOptions.levelHasSevenPointers = true;
            GameOptions.levelDisplayName = "Dev";
            //options
            GameOptions.gameModeHasBeenSelected = true;
            GameOptions.customTimer = 0;
            //character
            GameOptions.characterObjectNames = new List<string>
            {
                pi.characterProfile.PlayerObjectName,
                "pony"
            };

            GameOptions.ConfigureSingleHumanRoster(GameOptions.characterObjectNames.Count);
            GameOptions.levelsList = PlayerData.instance.LevelsList;

            yield return new WaitForSecondsRealtime(seconds);

            if (sceneLoadPending)
            {
                yield break;
            }

            sceneLoadPending = true;
            MatchSession.BeginNewMatch();

            string sceneName;
            sceneName = Constants.SCENE_NAME_level_23_dev;
            SceneTransition.LoadScene(sceneName);
        }

        //internal static void LoadDevLevelVersus()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
