using Assets.Scripts.Utility;
using UnityEngine;
using Level5.Core.Match;

public class GameStats : MonoBehaviour
{
    public int _experienceGained;
    public int _totalPoints;
    public int _bonusPoints;
    public int _twoPointerMade;
    public int _threePointerMade;
    public int _fourPointerMade;
    public int _sevenPointerMade;
    public int _moneyBallMade;
    public int _twoPointerAttempts;
    public int _threePointerAttempts;
    public int _fourPointerAttempts;
    public int _sevenPointerAttempts;
    public int _moneyBallAttempts;
    public int _shotAttempt;
    public int _shotMade;
    public float _longestShotMade;
    public float _totalDistance;

    public float _makeThreePointersLowTime;
    public float _makeFourPointersLowTime;
    public float _makeAllPointersLowTime;

    public float _makeThreePointersMoneyBallLowTime;
    public float _makeFourPointersMoneyBallLowTime;
    public float _makeAllPointersMoneyBallLowTime;

    public int _criticalRolled;
    public int _mostConsecutiveShots;

    //enemies
    public int _enemiesKilled;
    public int _minionsKilled;
    public int _bossKilled;

    public int _sniperShots;
    public int _sniperHits;

    public float _timePlayed;
    [SerializeField]
    int _consecutiveShotsMade = 0;
    [SerializeField]
    int _currentShotMade = 0;
    [SerializeField]
    int _currentShotAttempts = 0;
    [SerializeField]
    int _expectedShotMade = 1;
    [SerializeField]
    int _expectedShotAttempts = 1;

    public int campaignWins;
    public int campaignLosses;
    public int campaignTies;
    public int campaignGamesPlayed;

    ////init from game options
    //void Start()
    //{
    //    // for saving character specific info
    //    // id and name use to construct key that will be stored
    //    PlayerId = MatchRuntime.PrimaryCharacterId;
    //    PlayerName = MatchRuntime.PrimaryCharacterObjectName;
    //}

    public void calculateConsecutiveShot(BasketBallState basketBallState)
    {
        // get current state of shots made/attempted
        _currentShotMade = (int)ShotMade;
        _currentShotAttempts = (int)ShotAttempt;

        // if current is == expected made/attempt, increment consecutive and not a 2 point shot
        // 
        if (_currentShotMade == _expectedShotMade
            && _currentShotAttempts == _expectedShotAttempts
            && !basketBallState.TwoAttempt)
        {
            _consecutiveShotsMade++;
            // increment expected values for next shot
            _expectedShotMade = _currentShotMade + 1;
            _expectedShotAttempts = _currentShotAttempts + 1;
        }
        // else, not consecutive shot. get current, increment for next expected consecutive shot
        else
        {
            _consecutiveShotsMade = 1;
            // increment expected values for next shot
            _expectedShotMade = _currentShotMade + 1;
            _expectedShotAttempts = _currentShotAttempts + 1;
        }
        // if current consecutive greater than previous high consecutive
        if (_consecutiveShotsMade > MostConsecutiveShots)
        {
            MostConsecutiveShots = _consecutiveShotsMade;
        }
    }

    public float getTotalPointAccuracy()
    {
        float accuracy;
        if (ShotAttempt > 0)
        {
            accuracy = (float)ShotMade / ShotAttempt;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }
    public int getExperienceGainedFromSession()
    {
        ExperienceGained = MatchExperience.Calculate(BuildExperienceInput());
        return ExperienceGained;
    }

    // the award itself lives in Level5.Core so it can be unit tested without a scene.
    // this method's only job is reading the session's stats and mode flags into plain data.
    public MatchExperienceInput BuildExperienceInput()
    {
        return new MatchExperienceInput
        {
            ShotAttempts = ShotAttempt,
            TwoPointerMade = TwoPointerMade,
            ThreePointerMade = ThreePointerMade,
            FourPointerMade = FourPointerMade,
            SevenPointerMade = SevenPointerMade,
            TotalDistance = TotalDistance,
            MostConsecutiveShots = MostConsecutiveShots,
            TotalPoints = TotalPoints,

            SniperShots = SniperShots,
            SniperHits = SniperHits,

            MinionsKilled = MinionsKilled,
            BossKilled = BossKilled,

            TrafficEnabled = MatchRuntime.Rules.TrafficEnabled,
            EnemiesEnabled = MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly,
            HardcoreEnabled = MatchRuntime.Rules.Hardcore,
            SniperEnabled = MatchRuntime.Rules.SniperEnabled,
            ArcadeMode = MatchRuntime.Rules.ArcadeMode,
            DifficultySelected = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty)
        };
    }

    public float MakeThreePointersMoneyBallLowTime
    {
        get => _makeThreePointersMoneyBallLowTime;
        set => _makeThreePointersMoneyBallLowTime = value;
    }

    public float MakeFourPointersMoneyBallLowTime
    {
        get => _makeFourPointersMoneyBallLowTime;
        set => _makeFourPointersMoneyBallLowTime = value;
    }

    public float MakeAllPointersMoneyBallLowTime
    {
        get => _makeAllPointersMoneyBallLowTime;
        set => _makeAllPointersMoneyBallLowTime = value;
    }

    public float MakeThreePointersLowTime
    {
        get => _makeThreePointersLowTime;
        set => _makeThreePointersLowTime = value;
    }

    public float MakeFourPointersLowTime
    {
        get => _makeFourPointersLowTime;
        set => _makeFourPointersLowTime = value;
    }

    public float MakeAllPointersLowTime
    {
        get => _makeAllPointersLowTime;
        set => _makeAllPointersLowTime = value;
    }

    public int CriticalRolled
    {
        get => _criticalRolled;
        set => _criticalRolled = value;
    }

    public int ShotAttempt
    {
        get => _shotAttempt;
        set => _shotAttempt = value;
    }

    public int ShotMade
    {
        get => _shotMade;
        set => _shotMade = value;
    }

    public float LongestShotMade
    {
        get => _longestShotMade;
        set => _longestShotMade = value;
    }

    public float TotalDistance
    {
        get => _totalDistance;
        set => _totalDistance = value;
    }

    public int TotalPoints
    {
        get => _totalPoints;
        set => _totalPoints = value;
    }

    public int TwoPointerMade
    {
        get => _twoPointerMade;
        set => _twoPointerMade = value;
    }

    public int ThreePointerMade
    {
        get => _threePointerMade;
        set => _threePointerMade = value;
    }

    public int FourPointerMade
    {
        get => _fourPointerMade;
        set => _fourPointerMade = value;
    }

    public int TwoPointerAttempts
    {
        get => _twoPointerAttempts;
        set => _twoPointerAttempts = value;
    }

    public int ThreePointerAttempts
    {
        get => _threePointerAttempts;
        set => _threePointerAttempts = value;
    }

    public int FourPointerAttempts
    {
        get => _fourPointerAttempts;
        set => _fourPointerAttempts = value;
    }
    public int SevenPointerMade
    {
        get => _sevenPointerMade;
        set => _sevenPointerMade = value;
    }

    public int SevenPointerAttempts
    {
        get => _sevenPointerAttempts;
        set => _sevenPointerAttempts = value;
    }

    public int MoneyBallMade
    {
        get => _moneyBallMade;
        set => _moneyBallMade = value;
    }

    public int MoneyBallAttempts
    {
        get => _moneyBallAttempts;
        set => _moneyBallAttempts = value;
    }

    public float TimePlayed
    {
        get => _timePlayed;
        set => _timePlayed = value;
    }
    public int MostConsecutiveShots
    {
        get => _mostConsecutiveShots;
        set => _mostConsecutiveShots = value;
    }
    public int ExperienceGained
    {
        get => _experienceGained;
        set => _experienceGained = value;
    }
    public int EnemiesKilled { get => _enemiesKilled; set => _enemiesKilled = value; }
    public int BossKilled { get => _bossKilled; set => _bossKilled = value; }
    public int MinionsKilled { get => _minionsKilled; set => _minionsKilled = value; }
    public int BonusPoints { get => _bonusPoints; set => _bonusPoints = value; }
    public int SniperShots { get => _sniperShots; set => _sniperShots = value; }
    public int SniperHits { get => _sniperHits; set => _sniperHits = value; }
    public int ConsecutiveShotsMade { get => _consecutiveShotsMade; set => _consecutiveShotsMade = value; }
    public int blockedShots { get; internal set; }
}
