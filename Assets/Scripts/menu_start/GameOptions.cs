using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameOptions
{
    static public string applicationVersion;
    static public string operatingSystemVersion;

    static public int numPlayers = 1;
    static public int numCpuPlayers;
    static public bool player1IsCpu;
    static public bool player2IsCpu;
    static public bool player3IsCpu;
    static public bool player4IsCpu;

    public static void ConfigureSingleHumanRoster(int rosterPlayerCount, bool hasImplicitSecondCpu = false)
    {
        numPlayers = Math.Max(1, Math.Min(4, rosterPlayerCount));
        numCpuPlayers = Math.Max(0, numPlayers - 1) + (hasImplicitSecondCpu ? 1 : 0);
        player1IsCpu = false;
        player2IsCpu = numPlayers > 1 || hasImplicitSecondCpu;
        player3IsCpu = numPlayers > 2;
        player4IsCpu = numPlayers > 3;
    }

    public static bool IsCpuPlayer(int playerId)
    {
        switch (playerId)
        {
            case 0: return player1IsCpu;
            case 1: return player2IsCpu;
            case 2: return player3IsCpu;
            case 3: return player4IsCpu;
            default: return false;
        }
    }

    public static int GetHumanPlayerInputSlot(int playerId)
    {
        if (IsCpuPlayer(playerId))
        {
            return -1;
        }

        int inputSlot = 0;
        for (int slot = 0; slot < playerId; slot++)
        {
            if (!IsCpuPlayer(slot))
            {
                inputSlot++;
            }
        }

        return inputSlot;
    }

    // selected options
    static public string characterDisplayName;
    static public string cheerleaderDisplayName;
    static public int characterId;
    static public string levelSelected;
    static public int gameModeSelectedId;
    static public int levelId;
    // object names
    static public string characterObjectName;
    static public List<string> characterObjectNames;
    static public string cheerleaderObjectName;
    static public string levelSelectedName;
    static public string levelDisplayName;
    static public string gameModeSelectedName;


    // game mode flags for game rules
    static public bool gameModeHasBeenSelected;
    static public bool gameModeRequiresCounter;
    static public bool gameModeRequiresCountDown;
    // if game requires markers be active
    // 3 / 4 point contest / moneyball / etc
    static public bool gameModeRequiresShotMarkers3s;
    static public bool gameModeRequiresShotMarkers4s;
    static public bool gameModeRequiresBasketball;
    static public bool gameModeAllowsCpuShooters;
    // 3 / 4 point contest + trequires timer
    static public bool gameModeThreePointContest;
    static public bool gameModeFourPointContest;
    static public bool gameModeAllPointContest;
    // custom timer used for 3 / 4 / all point contest, change from default of 120 to 80 / 160
    static public float customTimer;

    // start manager selected option indices
    // set default values = 0 (first element in list)
    // using values from game options, will load previous values on next load of start manager
    static public int playerSelectedIndex = 0;
    static public int levelSelectedIndex = 0;
    static public int modeSelectedIndex = 0;
    static public int friendSelectedIndex = 0;
    static public int cpu1SelectedIndex = 0;
    static public int cpu2SelectedIndex = 0;
    static public int cpu3SelectedIndex = 0;

    static public bool trafficEnabled = false;
    static public bool enemiesEnabled = false;

    static public bool sniperEnabled = false;
    static public bool sniperEnabledBullet = false;
    static public bool sniperEnabledBulletAuto = false;
    static public bool sniperEnabledLaser = false;

    static public int difficultySelected = 1;

    static public string previousSceneName;

    static public bool hardcoreModeEnabled = false;
    static public bool EnemiesOnlyEnabled = false;

    static public bool levelRequiresTimeOfDay = true;
    static public bool levelRequiresWeather = false;
    static public bool levelHasSevenPointers = false;

    static public string userName;
    static public int userid;
    static public string bearerToken;
    static public string matchResultId;
    static public int numOfLocalUsers;

    static public bool tipDialogueLoadedOnStart;
    static public bool obstaclesEnabled;
    static public bool battleRoyalEnabled;
    static public bool cageMatchEnabled;

    static public bool gameModeRequiresShotMarkers7s;
    static public bool gameModeSevenPointContest;

    // campaign mode stats
    //static public bool isCampaignMode = true;
    static public List<LevelSelected> levelsList;
    //static public bool currentRoundWinnerIsCpu;
    //static public bool currentRoundLoserIsCpu;
    //static public int numberOfContinues = 0;
    //static public int currentRoundWinnerScore;
    //static public int currentRoundLoserScore;
    //static public Sprite currentRoundPlayerWinnerImage;
    //static public Sprite currentRoundPlayerLoserImage;
    //static public Sprite currentRoundCpuWinnerImage;
    //static public Sprite currentRoundCpuLoserImage;
}
