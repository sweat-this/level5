
using System;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;

public class CharacterProfile : MonoBehaviour
{
    [SerializeField] private int playerId;
    [SerializeField] private int userid;
    [SerializeField] public bool isCpu;  
    [SerializeField] private bool isDefensiveCpuPlayer;
    [SerializeField] private string playerDisplayName;
    [SerializeField] private string playerObjectName;
    [SerializeField] private Sprite playerPortrait;
    [SerializeField] public Sprite winPortrait;
    [SerializeField] public Sprite losePortrait;

    private float jumpStatFloor = 3.5f;
    private float jumpStatCeiling = 6;

    private float speedStatFloor = 2.5f;
    private float speedStatCeiling = 6.5f;

    [SerializeField] private float accuracy2pt;
    [SerializeField] private float accuracy3pt;
    [SerializeField] private float accuracy4pt;
    [SerializeField] private float accuracy7pt;
    [SerializeField] private float fadeaway;

    [SerializeField] private string shooterProfilePrefabName;

    [SerializeField] private float jumpForce;
    [SerializeField] private float speed;
    [SerializeField] private float runSpeedHasBall;

    [SerializeField] private float runSpeed;
    [SerializeField] private float inAirSpeed;

    [SerializeField] private int range;
    [SerializeField] private int release;

    [SerializeField] private int luck;
    [SerializeField] private int clutch;

    [SerializeField] private int shootAngle;

    [SerializeField] private int level;
    [SerializeField] private int experience;
    [SerializeField] private int pointsAvailable;
    [SerializeField] private int pointsUsed;
    [SerializeField] private decimal money;
    [SerializeField] private bool isFighter;
    [SerializeField] private bool isShooter;
    [SerializeField] private bool isLocked;
    [SerializeField] private CpuBaseStats.ShooterType cpuType;
  

    void Start()
    {
        //if (MatchRuntime.HasConfiguration && !isCpu)
        //{
        //    //GameOptions.gameModeHasBeenSelected = false;
        //    intializeShooterStatsFromProfile();
        //}
        fadeaway =  level < 50 ? 50 : level;
        InAirSpeed = (float)fadeaway / 100;
        if (isCpu && !isDefensiveCpuPlayer)
        {
            intializeCpuShooterStats();
        }
        if (MatchRuntime.Rules.ArcadeMode || MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty) == 0 )
        {
            Accuracy2Pt = 100;
            Accuracy3Pt = 100;
            Accuracy4Pt = 100;
            Accuracy7Pt = 100;
            Release = 100;
            Range = 150;
            Clutch = 100;
            Luck = 10;
        }
    }

    public CharacterProfile() 
    {
        //Debug.Log("CharacterProfile() ");
        //if (isCpu)
        //{
        //    intializeCpuShooterStats();
        //}
    }

    /// <summary>
    /// Rebuilds this profile from the saved data for <paramref name="characterId"/>, plus the
    /// match cheerleader's bonuses.
    ///
    /// The character id is a parameter rather than <see cref="MatchRuntime.PrimaryCharacterId"/>
    /// read in here: this runs for every human in the match, and reading slot zero's id meant a
    /// second local human was rebuilt as - and credited as - the first one. The cheerleader is
    /// still match-wide, because the match configuration only carries one.
    /// </summary>
    public void intializeShooterStatsFromProfile(int characterId)
    {
        CharacterProfile temp = LoadedData.instance != null
            ? LoadedData.instance.getSelectedCharacterProfile(characterId)
            : null;
        if (temp == null)
        {
            Debug.LogError($"CharacterProfile could not resolve the selected player profile for character id {characterId}.");
            return;
        }

        experience = temp.Experience;
        level = CharacterLevel.FromExperience(temp.Experience);
        fadeaway = level;
        InAirSpeed = fadeaway / 10;
        playerObjectName = temp.playerObjectName != null ? temp.playerObjectName : "";
        playerDisplayName = temp.playerDisplayName;
        playerId = temp.playerId;

        Speed = temp.speed;
        RunSpeed = temp.runSpeed;
        runSpeedHasBall = temp.runSpeedHasBall;

        JumpForce = temp.jumpForce;
        shootAngle = temp.shootAngle;

        Accuracy2Pt = temp.accuracy2pt;
        Accuracy3Pt = temp.accuracy3pt + MatchRuntime.Cheerleader.BonusThreeAccuracy;
        Accuracy4Pt = temp.accuracy4pt + MatchRuntime.Cheerleader.BonusFourAccuracy;
        Accuracy7Pt = temp.accuracy7pt + MatchRuntime.Cheerleader.BonusSevenAccuracy;

        Range = temp.range + MatchRuntime.Cheerleader.BonusRange;
        Release = temp.release + MatchRuntime.Cheerleader.BonusRelease;

        clutch = temp.clutch + MatchRuntime.Cheerleader.BonusClutch;

        pointsAvailable = temp.PointsAvailable;
        pointsUsed = temp.PointsUsed;

        // if 3/4/All point contest, disable Luck/citical %
        if (MatchRuntime.Rules.IsThreePointContest
            || MatchRuntime.Rules.IsFourPointContest
            || MatchRuntime.Rules.IsSevenPointContest
            || MatchRuntime.Rules.IsAllPointContest)
        {
            Luck = 0;
            clutch = 0;
        }
        else
        {
            Luck = temp.Luck + MatchRuntime.Cheerleader.BonusLuck;
        }
    }
    public void intializeCpuShooterStats()
    {
        // in hardcore if CPU level less than player level, set equal to player level+10
        if (MatchRuntime.Rules.Hardcore)
        {
            int playerLevel = GameLevelManager.instance.players[0].characterProfile.level;
            if(playerLevel > level)
            {
                level = playerLevel+10;
            }
            else
            {
                level += 10;
            }
            //Debug.Log(GameLevelManager.instance.players[0].characterProfile.PlayerDisplayName + " lvl : " + GameLevelManager.instance.players[0].characterProfile.level);
        }
        int release = level > 25 ? 25 : level;
        int three = level > 50 ? 25 : level - release;
        int four = level > 75 ? 25 : level - (three + release);
        int seven = level > 100 ? 25 : level - (three + four + release);

        calculateAccuracyAttributeRatings();

        Range = CpuBaseStats.RANGE + (level * 5);
        int luckCalc = CpuBaseStats.LUCK + (level / CpuBaseStats.LUCK_DIVIDER);
        Luck = luckCalc <= 10 ? luckCalc : 10;
        clutch = level <= 100 ? level : 100;

        // if 3/4/All point contest, disable Luck/citical %
        if (MatchRuntime.Rules.IsThreePointContest
            || MatchRuntime.Rules.IsFourPointContest
            || MatchRuntime.Rules.IsSevenPointContest
            || MatchRuntime.Rules.IsAllPointContest)
        {
            Luck = 0;
            clutch = 0;
        }
        if (isDefensiveCpuPlayer)
        {
            inAirSpeed = ((float)level / 100 )* 3;
        }
    }

    public void calculateAccuracyAttributeRatings()
    {
        int three = 0;
        int four = 0;
        int seven = 0;
        int remainder = 0;
        int release = 0;

        Accuracy2Pt = 90;
        if (cpuType == CpuBaseStats.ShooterType.Three)
        {
            three = (int)(level * 0.5f);
            four = (int)(level * 0.15f);
            seven = (int)(level * 0.15f);
            release = (int)(level * 0.2f);

            remainder = level - (three + four + seven + release);
        }
        if (cpuType == CpuBaseStats.ShooterType.Four)
        {
            three = (int)(level * 0.15f);
            four = (int)(level * 0.5f);
            seven = (int)(level * 0.15f);
            release = (int)(level * 0.2f);
            remainder = level - (three + four + seven + release);
        }
        if (cpuType == CpuBaseStats.ShooterType.Seven)
        {
            three = (int)(level * 0.15f);
            four = (int)(level * 0.15f);
            seven = (int)(level * 0.5f);
            release = (int)(level * 0.2f);
            remainder = level - (three + four + seven + release);
        }
        if (three > 25) { remainder += (three - 25); three = 25; }
        if (four > 25) { remainder += (four - 25); four = 25; }
        if (seven > 25) { remainder += (seven - 25); seven = 25; }
        if (release > 25) { remainder += (release - 25); release = 25; }

        // redistribute points
        int[] attributes = new int[] { three, four, seven, release };
        //int icount = 0;
        //int jcount = 0;
        for (int i = 0; i < remainder; i++)
        {
            //icount++;
            for (int j = 0; j < attributes.Length; j++)
            {
                if (attributes[j] < 25)
                {
                    attributes[j]++;
                    //jcount++;
                    j++;
                }
            }
        }

        //Debug.Log("---icount : "+ icount);
        //Debug.Log("------jcount : "+ jcount);
        //Debug.Log("exit while loop ");
        Accuracy3Pt = CpuBaseStats.ACCURACY_3PT + attributes[0];
        Accuracy4Pt = CpuBaseStats.ACCURACY_4PT + attributes[1];
        Accuracy7Pt = CpuBaseStats.ACCURACY_7PT + attributes[2];
        Release = CpuBaseStats.RELEASE + +attributes[3];
    }

    //private void calculateCpu3ptAccuracy(int accuracy)
    //{
    //    int levelpoints = level;
    //    if (cpuType == CpuBaseStats.ShooterType.Three)
    //    {
    //        if(levelpoints > 10) 
    //        {
    //            accuracy3pt += 10;
    //            levelpoints -= 10;
    //        }
    //    }
    //}
    //private void calculateCpu4ptAccuracy(int accuracy)
    //{

    //}
    //private void calculateCpu7ptAccuracy(int accuracy)
    //{

    //}

    public float calculateJumpValueToPercent()
    {
        //modifier
        float modifier = 100 / ((jumpStatCeiling - jumpStatFloor) * 10);
        // percent
        float percent = (JumpForce - jumpStatFloor) * modifier * 10;
        return percent;
    }
    public float calculateSpeedToPercent()
    {
        //modifier
        float modifier = 100 / ((speedStatCeiling - speedStatFloor) * 10);
        // percent
        float percent = (runSpeed - speedStatFloor) * modifier * 10;
        return percent;
    }
    public float RunSpeedHasBall
    {
        get => runSpeedHasBall;
        set => runSpeedHasBall = value;
    }
    public int PlayerId
    {
        get => playerId;
        set => playerId = value;
    }
    public int Level
    {
        get => level;
        set => level = value;
    }
    public int Experience
    {
        get => experience;
        set => experience = value;
    }
    public string PlayerDisplayName
    {
        get => playerDisplayName;
        set => playerDisplayName = value;
    }
    public string PlayerObjectName
    {
        get => playerObjectName;
        set => playerObjectName = value;
    }
    public float Accuracy2Pt
    {
        get => accuracy2pt;
        set => accuracy2pt = value;
    }
    public float Accuracy3Pt
    {
        get => accuracy3pt;
        set => accuracy3pt = value;
    }
    public float Accuracy4Pt
    {
        get => accuracy4pt;
        set => accuracy4pt = value;
    }
    public float Accuracy7Pt
    {
        get => accuracy7pt;
        set => accuracy7pt = value;
    }
    public float JumpForce
    {
        get => jumpForce;
        set => jumpForce = value;
    }
    public float Speed
    {
        get => speed;
        set => speed = value;
    }
    public float RunSpeed
    {
        get => runSpeed;
        set => runSpeed = value;
    }
    public int Luck
    {
        get => luck;
        set => luck = value;
    }
    public int ShootAngle
    {
        get => shootAngle;
        set => shootAngle = value;
    }
    public Sprite PlayerPortrait { get => playerPortrait; set => playerPortrait = value; }
    public int PointsAvailable { get => pointsAvailable; set => pointsAvailable = value; }
    public int PointsUsed { get => pointsUsed; set => pointsUsed = value; }
    public int Range { get => range; set => range = value; }
    public int Release { get => release; set => release = value; }
    public bool IsFighter { get => isFighter; set => isFighter = value; }
    public bool IsLocked { get; internal set; }
    public bool IsShooter { get => isShooter; set => isShooter = value; }
    public int Clutch { get => clutch; set => clutch = value; }
    public int Userid { get => userid; set => userid = value; }
    public float InAirSpeed { get => inAirSpeed; set => inAirSpeed = value; }
}
