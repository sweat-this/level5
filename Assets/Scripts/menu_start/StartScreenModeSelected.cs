using System;
using System.Collections.Generic;
using UnityEngine;

public class StartScreenModeSelected : MonoBehaviour
{

    [SerializeField] private int modeId;
    [SerializeField] private string modeDisplayName;
    [SerializeField] private string modeObjectName;
    [SerializeField] private string modeDescription;

    [SerializeField] private bool modeRequiresCounter;
    [SerializeField] private bool modeRequiresCountDown;

    [SerializeField] private bool modeRequiresShotMarkers3s;
    [SerializeField] private bool modeRequiresShotMarkers4s;
    [SerializeField] private bool modeRequiresShotMarkers7s;

    [SerializeField] private bool gameModeThreePointContest;
    [SerializeField] private bool gameModeFourPointContest;
    [SerializeField] private bool gameModeSevenPointContest;
    [SerializeField] private bool gameModeAllPointContest;
    [SerializeField] private float customTimer;

    [SerializeField] private bool modeRequiresMoneyBall;
    [SerializeField] private bool modeRequiresConsecutiveShots;
    [SerializeField] private string highScoreField;
    [SerializeField] private bool arcadeModeActive;
    [SerializeField] private bool enemiesOnlyEnabled;
    [SerializeField] private bool isBattleRoyal;
    [SerializeField] private bool isCageMatch;
    [SerializeField] private bool gameModeRequiresPlayerSurvive;
    [SerializeField] private bool gameModeRequiresBasketball;
    [SerializeField] private bool gameModeAllowsCpuShooters;
    [Tooltip("Certified for exactly two local humans, without CPU participants or an implicit opponent.")]
    [SerializeField] private bool supportsTwoLocalHumans;

    public bool SupportsTwoLocalHumans => supportsTwoLocalHumans;

    public bool EnemiesOnlyEnabled => enemiesOnlyEnabled;

    public bool ModeRequiresMoneyBall => modeRequiresMoneyBall;

    public bool ModeRequiresShotMarkers3S
    {
        get => modeRequiresShotMarkers3s;
    }

    public bool ModeRequiresShotMarkers4S
    {
        get => modeRequiresShotMarkers4s;
    }
    public bool ModeRequiresCounter
    {
        get => modeRequiresCounter;
    }
    public bool ModeRequiresCountDown
    {
        get => modeRequiresCountDown;
    }

    public int ModeId
    {
        get => modeId;
    }

    public string ModeDisplayName
    {
        get => modeDisplayName;
    }

    public string ModeObjectName
    {
        get => modeObjectName;
    }

    public string ModeDescription
    {
        get => modeDescription;
    }
    public bool ModeRequiresConsecutiveShots { get => modeRequiresConsecutiveShots; set => modeRequiresConsecutiveShots = value; }
    public string HighScoreField { get => highScoreField; set => highScoreField = value; }
    public bool GameModeThreePointContest { get => gameModeThreePointContest; set => gameModeThreePointContest = value; }
    public bool GameModeFourPointContest { get => gameModeFourPointContest; set => gameModeFourPointContest = value; }
    public bool GameModeAllPointContest { get => gameModeAllPointContest; set => gameModeAllPointContest = value; }
    public float CustomTimer { get => customTimer; set => customTimer = value; }
    public bool ArcadeModeActive { get => arcadeModeActive; set => arcadeModeActive = value; }
    public bool IsBattleRoyal { get => isBattleRoyal; }
    public bool GameModeRequiresPlayerSurvive { get => gameModeRequiresPlayerSurvive;  }
    public bool IsCageMatch { get => isCageMatch;  }
    public bool GameModeRequiresBasketball { get => gameModeRequiresBasketball; set => gameModeRequiresBasketball = value; }
    public bool GameModeAllowsCpuShooters { get => gameModeAllowsCpuShooters; set => gameModeAllowsCpuShooters = value; }
    public bool ModeRequiresShotMarkers7s { get => modeRequiresShotMarkers7s; set => modeRequiresShotMarkers7s = value; }
    public bool GameModeSevenPointContest { get => gameModeSevenPointContest; set => gameModeSevenPointContest = value; }
}
