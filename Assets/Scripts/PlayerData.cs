using System;
using System.Collections.Generic;
using UnityEngine;

/* This class is used to store Player data from the database such as high scores
 * and data that will be used to update high scores and inserted into database
 */

public class PlayerData : MonoBehaviour
{
    //private int _playerId = 0;
    //private string _playerName = "";

    private float _totalPoints = 0;
    private float _totalPointsLockDown = 0;
    private float _totalPointsByDistance = 0;
    private float _totalPointsBonus = 0;
    //private float _twoPointerMade = 0;
    private float _threePointerMade = 0;
    private float _sevenPointerMade = 0;

    private float _fourPointerMade = 0;
    //private float _twoPointerAttempts = 0;
    //private float _threePointerAttempts = 0;
    //private float _fourPointerAttempts = 0;
    //private float _sevenPointerAttempts = 0;

    //private float _shotAttempt = 0;
    //private float _shotMade = 0;
    //private float _longestShotMade = 0;

    private float _longestShotMadeFreePlay = 0;
    private float _longestShotMadeArcade = 0;
    private float _totalDistance = 0;

    private float _makeThreePointersLowTime = 0;
    private float _makeFourPointersLowTime = 0;
    private float _makeSevenPointersLowTime = 0;
    private float _makeAllPointersLowTime = 0;

    private float _makeThreePointersMoneyBallLowTime = 0;
    private float _makeFourPointersMoneyBallLowTime = 0;
    private float _makeAllPointersMoneyBallLowTime = 0;

    private int _mostConsecutiveShots = 0;

    private float _threePointContestScore = 0;
    private float _fourPointContestScore = 0;
    private float _sevenPointContestScore = 0;
    private float _allPointContestScore = 0;

    private int _enemiesKilled = 0;
    private int _enemiesKilledBattleRoyal = 0;
    private int _enemiesKilledCageMatch = 0;
    [SerializeField]
    private int _currentExperience = 0;
    [SerializeField]
    private int _currentLevel = 0;
    [SerializeField]
    private int _updatePointsAvailable = 0;
    [SerializeField]
    private int _updatePointsUsed = 0;

    [SerializeField]
    GameStats campaignGameStats;

    [SerializeField]
    List<LevelSelected> levelsList;

    public static PlayerData instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        campaignGameStats = gameObject.AddComponent<GameStats>();
        //if (GameObject.FindGameObjectsWithTag("database") != null)
        //{
        //    Debug.Log("load player high scores");
        //    loadStatsFromDatabase();
        //}
        if (GameObject.FindGameObjectsWithTag("database") != null)
        {
            //Debug.Log("load player high scores");
            loadStatsFromDatabase();
        }
    }

    public void loadStatsFromDatabase()
    {
        //Debug.Log("Player Data -- load from database -- high scores");
        //Debug.Log("DBHelper.instance == null : "+ (DBHelper.instance == null));

        if (DBHelper.instance != null)
        {
            int hardcoreValue = 0;
            if (GameOptions.hardcoreModeEnabled)
            {
                hardcoreValue = 1;
            }
            //Debug.Log("hardcoreValue : "+ hardcoreValue);

            _totalPoints = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 1, "DESC", hardcoreValue);
            TotalPointsLockDown = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 27, "DESC", hardcoreValue);

            TotalPointsBonus = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 15, "DESC", hardcoreValue);
            _threePointerMade = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "maxShotMade", 2, "DESC", hardcoreValue);
            _fourPointerMade = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "maxShotMade", 3, "DESC", hardcoreValue);
            _sevenPointerMade = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "maxShotMade", 4, "DESC", hardcoreValue);

            _longestShotMadeFreePlay = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "longestShot", 99, "DESC", hardcoreValue);
            _longestShotMadeArcade = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "longestShot", 98, "DESC", hardcoreValue);

            _totalDistance = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "totalDistance", 6, "DESC", hardcoreValue);
            _makeThreePointersLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 7, "ASC", hardcoreValue);
            _makeFourPointersLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 8, "ASC", hardcoreValue);
            MakeSevenPointersLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 25, "ASC", hardcoreValue);
            _makeAllPointersLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 9, "ASC", hardcoreValue);
            _makeThreePointersMoneyBallLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 10, "ASC", hardcoreValue);
            _makeFourPointersMoneyBallLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 11, "ASC", hardcoreValue);
            _makeAllPointersMoneyBallLowTime = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "time", 12, "ASC", hardcoreValue);
            LongestShotMadeFreePlay = DBHelper.instance.getFloatValueHighScoreFromTableByFieldAndModeId("HighScores", "longestShot", 99, "DESC", hardcoreValue);
            _mostConsecutiveShots = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "consecutiveShots", 14, "DESC", hardcoreValue);
            _totalPointsBonus = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 15, "DESC", hardcoreValue);
            _threePointContestScore = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 16, "DESC", hardcoreValue);
            _fourPointContestScore = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 17, "DESC", hardcoreValue);
            _sevenPointContestScore = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 24, "DESC", hardcoreValue);
            _allPointContestScore = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 18, "DESC", hardcoreValue);
            TotalPointsByDistance = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "totalPoints", 19, "DESC", hardcoreValue);
            _enemiesKilled = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "enemiesKilled", 20, "DESC", hardcoreValue);
            _enemiesKilledBattleRoyal = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "enemiesKilled", 21, "DESC", hardcoreValue);
            _enemiesKilledCageMatch = DBHelper.instance.getIntValueHighScoreFromTableByFieldAndModeId("HighScores", "enemiesKilled", 22, "DESC", hardcoreValue);
        }
    }

    public void updateCampaignStats(GameStats gameStats)
    {
        campaignGameStats.TotalPoints += gameStats.TotalPoints;
        campaignGameStats.TotalDistance += gameStats.TotalDistance;
        campaignGameStats.ThreePointerMade += gameStats.ThreePointerMade;
        campaignGameStats.FourPointerMade += gameStats.FourPointerMade;
        campaignGameStats.SevenPointerMade += gameStats.SevenPointerMade;
        campaignGameStats.ThreePointerAttempts += gameStats.ThreePointerAttempts;
        campaignGameStats.FourPointerAttempts += gameStats.FourPointerAttempts;
        campaignGameStats.SevenPointerAttempts += gameStats.SevenPointerAttempts;
        campaignGameStats.LongestShotMade = gameStats.LongestShotMade > campaignGameStats.LongestShotMade ? gameStats.LongestShotMade : campaignGameStats.LongestShotMade;
        campaignGameStats.TimePlayed += gameStats.TimePlayed;
        campaignGameStats.CriticalRolled += gameStats.CriticalRolled;
        campaignGameStats.EnemiesKilled += gameStats.EnemiesKilled;
        campaignGameStats.BossKilled += gameStats.BossKilled;
        campaignGameStats.MinionsKilled += gameStats.MinionsKilled;
        campaignGameStats.MoneyBallMade += gameStats.MoneyBallMade;
        campaignGameStats.MoneyBallAttempts += gameStats.MoneyBallAttempts;
        campaignGameStats.ShotMade += gameStats.ShotMade;
        campaignGameStats.ShotAttempt += gameStats.ShotAttempt;
        campaignGameStats.SniperHits += gameStats.SniperHits;
        campaignGameStats.SniperShots += gameStats.SniperShots;
        campaignGameStats.TwoPointerMade += gameStats.TwoPointerMade;
        campaignGameStats.TwoPointerAttempts += gameStats.TwoPointerAttempts;
        campaignGameStats.MostConsecutiveShots = gameStats.MostConsecutiveShots > campaignGameStats.MostConsecutiveShots ? gameStats.MostConsecutiveShots : campaignGameStats.MostConsecutiveShots;
    }

    public float TotalPoints => _totalPoints;

    //public float TwoPointerMade => _twoPointerMade;

    public float ThreePointerMade => _threePointerMade;

    public float FourPointerMade => _fourPointerMade;

    //public float TwoPointerAttempts => _twoPointerAttempts;

    //public float ThreePointerAttempts => _threePointerAttempts;

    //public float FourPointerAttempts => _fourPointerAttempts;

    public float SevenPointerMade => _sevenPointerMade;

    //public float ShotAttempt => _shotAttempt;

    //public float ShotMade => _shotMade;

    //public float LongestShotMade => _longestShotMade;

    public float TotalDistance => _totalDistance;
    public float MakeThreePointersLowTime => _makeThreePointersLowTime;
    public float MakeFourPointersLowTime => _makeFourPointersLowTime;
    public float MakeAllPointersLowTime => _makeAllPointersLowTime;
    public float MakeThreePointersMoneyBallLowTime => _makeThreePointersMoneyBallLowTime;
    public float MakeFourPointersMoneyBallLowTime => _makeFourPointersMoneyBallLowTime;
    public float MakeAllPointersMoneyBallLowTime => _makeAllPointersMoneyBallLowTime;
    public float LongestShotMadeFreePlay { get => _longestShotMadeFreePlay; set => _longestShotMadeFreePlay = value; }
    public int MostConsecutiveShots { get => _mostConsecutiveShots; set => _mostConsecutiveShots = value; }
    public float TotalPointsBonus { get => _totalPointsBonus; set => _totalPointsBonus = value; }
    public float ThreePointContestScore { get => _threePointContestScore; set => _threePointContestScore = value; }
    public float FourPointContestScore { get => _fourPointContestScore; set => _fourPointContestScore = value; }
    public float SevenPointContestScore { get => _sevenPointContestScore; set => _sevenPointContestScore = value; }
    public float AllPointContestScore { get => _allPointContestScore; set => _allPointContestScore = value; }
    public int CurrentExperience { get => _currentExperience; set => _currentExperience = value; }
    public int CurrentLevel { get => _currentLevel; set => _currentLevel = value; }
    public int UpdatePointsAvailable { get => _updatePointsAvailable; set => _updatePointsAvailable = value; }
    public int UpdatePointsUsed { get => _updatePointsUsed; set => _updatePointsUsed = value; }
    public float TotalPointsByDistance { get => _totalPointsByDistance; set => _totalPointsByDistance = value; }
    public int EnemiesKilled { get => _enemiesKilled; set => _enemiesKilled = value; }
    public int EnemiesKilledBattleRoyal { get => _enemiesKilledBattleRoyal; set => _enemiesKilledBattleRoyal = value; }
    public int EnemiesKilledCageMatch { get => _enemiesKilledCageMatch; set => _enemiesKilledCageMatch = value; }
    public float MakeSevenPointersLowTime { get => _makeSevenPointersLowTime; set => _makeSevenPointersLowTime = value; }
    public GameStats CampaignGameStats { get => campaignGameStats; set => campaignGameStats = value; }
    public List<LevelSelected> LevelsList { get => levelsList; set => levelsList = value; }
    public float TotalPointsLockDown { get => _totalPointsLockDown; set => _totalPointsLockDown = value; }
    //public float LongestShotMadeArcade { get => _longestShotMadeArcade; set => _longestShotMadeArcade = value; }
}
