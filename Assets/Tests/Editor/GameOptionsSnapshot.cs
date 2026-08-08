using System.Collections.Generic;

/// <summary>
/// Saves and restores the match-related <see cref="GameOptions"/> fields around a test.
///
/// They are process-wide mutable statics, so a test that writes them leaks into every test after
/// it - and into the editor session. This is the discipline that global state forces on its tests,
/// and it goes away when the fields do.
/// </summary>
public sealed class GameOptionsSnapshot
{
    private int gameModeSelectedId;
    private string gameModeSelectedName;
    private bool gameModeHasBeenSelected;
    private bool gameModeRequiresCounter;
    private bool gameModeRequiresCountDown;
    private bool gameModeRequiresShotMarkers3s;
    private bool gameModeRequiresShotMarkers4s;
    private bool gameModeRequiresShotMarkers7s;
    private bool gameModeThreePointContest;
    private bool gameModeFourPointContest;
    private bool gameModeSevenPointContest;
    private bool gameModeAllPointContest;
    private bool gameModeRequiresBasketball;
    private bool gameModeAllowsCpuShooters;
    private float customTimer;
    private bool enemiesOnlyEnabled;
    private bool battleRoyalEnabled;
    private bool cageMatchEnabled;

    private string levelSelected;
    private int levelId;
    private string levelDisplayName;
    private bool levelRequiresTimeOfDay;
    private bool levelRequiresWeather;
    private bool levelHasSevenPointers;

    private bool enemiesEnabled;
    private bool trafficEnabled;
    private bool obstaclesEnabled;
    private int difficultySelected;
    private bool hardcoreModeEnabled;
    private bool sniperEnabled;
    private bool sniperEnabledBullet;
    private bool sniperEnabledBulletAuto;
    private bool sniperEnabledLaser;

    private List<string> characterObjectNames;
    private string characterObjectName;
    private int characterId;
    private string characterDisplayName;
    private int numPlayers;
    private int numCpuPlayers;
    private bool player1IsCpu;
    private bool player2IsCpu;
    private bool player3IsCpu;
    private bool player4IsCpu;

    private string cheerleaderObjectName;
    private string cheerleaderDisplayName;

    public static GameOptionsSnapshot Capture()
    {
        return new GameOptionsSnapshot
        {
            gameModeSelectedId = GameOptions.gameModeSelectedId,
            gameModeSelectedName = GameOptions.gameModeSelectedName,
            gameModeHasBeenSelected = GameOptions.gameModeHasBeenSelected,
            gameModeRequiresCounter = GameOptions.gameModeRequiresCounter,
            gameModeRequiresCountDown = GameOptions.gameModeRequiresCountDown,
            gameModeRequiresShotMarkers3s = GameOptions.gameModeRequiresShotMarkers3s,
            gameModeRequiresShotMarkers4s = GameOptions.gameModeRequiresShotMarkers4s,
            gameModeRequiresShotMarkers7s = GameOptions.gameModeRequiresShotMarkers7s,
            gameModeThreePointContest = GameOptions.gameModeThreePointContest,
            gameModeFourPointContest = GameOptions.gameModeFourPointContest,
            gameModeSevenPointContest = GameOptions.gameModeSevenPointContest,
            gameModeAllPointContest = GameOptions.gameModeAllPointContest,
            gameModeRequiresBasketball = GameOptions.gameModeRequiresBasketball,
            gameModeAllowsCpuShooters = GameOptions.gameModeAllowsCpuShooters,
            customTimer = GameOptions.customTimer,
            enemiesOnlyEnabled = GameOptions.EnemiesOnlyEnabled,
            battleRoyalEnabled = GameOptions.battleRoyalEnabled,
            cageMatchEnabled = GameOptions.cageMatchEnabled,

            levelSelected = GameOptions.levelSelected,
            levelId = GameOptions.levelId,
            levelDisplayName = GameOptions.levelDisplayName,
            levelRequiresTimeOfDay = GameOptions.levelRequiresTimeOfDay,
            levelRequiresWeather = GameOptions.levelRequiresWeather,
            levelHasSevenPointers = GameOptions.levelHasSevenPointers,

            enemiesEnabled = GameOptions.enemiesEnabled,
            trafficEnabled = GameOptions.trafficEnabled,
            obstaclesEnabled = GameOptions.obstaclesEnabled,
            difficultySelected = GameOptions.difficultySelected,
            hardcoreModeEnabled = GameOptions.hardcoreModeEnabled,
            sniperEnabled = GameOptions.sniperEnabled,
            sniperEnabledBullet = GameOptions.sniperEnabledBullet,
            sniperEnabledBulletAuto = GameOptions.sniperEnabledBulletAuto,
            sniperEnabledLaser = GameOptions.sniperEnabledLaser,

            characterObjectNames = GameOptions.characterObjectNames,
            characterObjectName = GameOptions.characterObjectName,
            characterId = GameOptions.characterId,
            characterDisplayName = GameOptions.characterDisplayName,
            numPlayers = GameOptions.numPlayers,
            numCpuPlayers = GameOptions.numCpuPlayers,
            player1IsCpu = GameOptions.player1IsCpu,
            player2IsCpu = GameOptions.player2IsCpu,
            player3IsCpu = GameOptions.player3IsCpu,
            player4IsCpu = GameOptions.player4IsCpu,

            cheerleaderObjectName = GameOptions.cheerleaderObjectName,
            cheerleaderDisplayName = GameOptions.cheerleaderDisplayName
        };
    }

    public void Restore()
    {
        GameOptions.gameModeSelectedId = gameModeSelectedId;
        GameOptions.gameModeSelectedName = gameModeSelectedName;
        GameOptions.gameModeHasBeenSelected = gameModeHasBeenSelected;
        GameOptions.gameModeRequiresCounter = gameModeRequiresCounter;
        GameOptions.gameModeRequiresCountDown = gameModeRequiresCountDown;
        GameOptions.gameModeRequiresShotMarkers3s = gameModeRequiresShotMarkers3s;
        GameOptions.gameModeRequiresShotMarkers4s = gameModeRequiresShotMarkers4s;
        GameOptions.gameModeRequiresShotMarkers7s = gameModeRequiresShotMarkers7s;
        GameOptions.gameModeThreePointContest = gameModeThreePointContest;
        GameOptions.gameModeFourPointContest = gameModeFourPointContest;
        GameOptions.gameModeSevenPointContest = gameModeSevenPointContest;
        GameOptions.gameModeAllPointContest = gameModeAllPointContest;
        GameOptions.gameModeRequiresBasketball = gameModeRequiresBasketball;
        GameOptions.gameModeAllowsCpuShooters = gameModeAllowsCpuShooters;
        GameOptions.customTimer = customTimer;
        GameOptions.EnemiesOnlyEnabled = enemiesOnlyEnabled;
        GameOptions.battleRoyalEnabled = battleRoyalEnabled;
        GameOptions.cageMatchEnabled = cageMatchEnabled;

        GameOptions.levelSelected = levelSelected;
        GameOptions.levelId = levelId;
        GameOptions.levelDisplayName = levelDisplayName;
        GameOptions.levelRequiresTimeOfDay = levelRequiresTimeOfDay;
        GameOptions.levelRequiresWeather = levelRequiresWeather;
        GameOptions.levelHasSevenPointers = levelHasSevenPointers;

        GameOptions.enemiesEnabled = enemiesEnabled;
        GameOptions.trafficEnabled = trafficEnabled;
        GameOptions.obstaclesEnabled = obstaclesEnabled;
        GameOptions.difficultySelected = difficultySelected;
        GameOptions.hardcoreModeEnabled = hardcoreModeEnabled;
        GameOptions.sniperEnabled = sniperEnabled;
        GameOptions.sniperEnabledBullet = sniperEnabledBullet;
        GameOptions.sniperEnabledBulletAuto = sniperEnabledBulletAuto;
        GameOptions.sniperEnabledLaser = sniperEnabledLaser;

        GameOptions.characterObjectNames = characterObjectNames;
        GameOptions.characterObjectName = characterObjectName;
        GameOptions.characterId = characterId;
        GameOptions.characterDisplayName = characterDisplayName;
        GameOptions.numPlayers = numPlayers;
        GameOptions.numCpuPlayers = numCpuPlayers;
        GameOptions.player1IsCpu = player1IsCpu;
        GameOptions.player2IsCpu = player2IsCpu;
        GameOptions.player3IsCpu = player3IsCpu;
        GameOptions.player4IsCpu = player4IsCpu;

        GameOptions.cheerleaderObjectName = cheerleaderObjectName;
        GameOptions.cheerleaderDisplayName = cheerleaderDisplayName;
    }
}
