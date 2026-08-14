//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class StartScreenPlayerSelected : MonoBehaviour
//{
//    [SerializeField]
//    public string playerDisplayName;
//    [SerializeField]
//    public string playerObjectName;

//    [SerializeField]
//    private Sprite playerPortrait;

//    float accuracy2pt;
//    float accuracy3pt;
//    float accuracy4pt;
//    [SerializeField]
//    float jumpForce;
//    float speed;
//    float runSpeed;
//    float hangTime;
//    [SerializeField]
//    float range;
//    float release;

//    [SerializeField]
//    public float jumpStatFloor;
//    [SerializeField]
//    public float jumpStatCeiling;

//    public float criticalPercent;
//    [SerializeField]
//    private shooterProfile shooterProfile;
//    [SerializeField]
//    private GameObject shooterProfileObject;
//    private string shooterProfilePrefabName;

//    void Awake()
//    {
//       //Debug.Log("StartScreenPlayerSelected");
//        shooterProfilePrefabName = "player"  + playerDisplayName;

//       //Debug.Log(shooterProfilePrefabName);
//        shooterProfileObject = Resources.Load("Prefabs /characters/"+shooterProfilePrefabName) as GameObject;
//        Instantiate(shooterProfileObject);

//        //basketball = Resources.Load("Prefabs/objects/basketball_nba") as GameObject;
//        intializeShooterStatsFromProfile();
//    }

//    private void intializeShooterStatsFromProfile()
//    {
//       //Debug.Log("initializeStats()");
//        Accuracy2Pt = shooterProfile.Accuracy2pt;
//        Accuracy3Pt = shooterProfile.Accuracy3pt;
//        Accuracy4Pt = shooterProfile.Accuracy4pt;
//        JumpForce = shooterProfile.JumpForce;
//       //Debug.Log(shooterProfile.JumpForce);
//        CriticalPercent = shooterProfile.criticalPercent;

//    }

//    public float  calculateJumpValueToPercent()
//    {

//        //modifier
//        float modifier = 100 /((jumpStatCeiling - jumpStatFloor) * 10);
//        // percent
//        float percent = (JumpForce - jumpStatFloor) * modifier *10;

//        //Debug.Log(jumpStatCeiling - jumpStatFloor);
//        //Debug.Log(" modifier : "+ modifier + "      percent : "+ percent);

//        return percent;
//    }

//    public string PlayerObjectName
//    {
//        get => playerObjectName;
//        set => playerObjectName = value;
//    }

//    public string PlayerName
//    {
//        get => playerDisplayName;
//        set => playerDisplayName = value;
//    }

//    public Sprite PlayerPortrait
//    {
//        get => playerPortrait;
//        set => playerPortrait = value;
//    }

//    public float Accuracy2Pt
//    {
//        get => accuracy2pt;
//        set => accuracy2pt = value;
//    }

//    public float Accuracy3Pt
//    {
//        get => accuracy3pt;
//        set => accuracy3pt = value;
//    }

//    public float Accuracy4Pt
//    {
//        get => accuracy4pt;
//        set => accuracy4pt = value;
//    }

//    public float JumpForce
//    {
//        get => jumpForce;
//        set => jumpForce = value;
//    }

//    public float Speed
//    {
//        get => speed;
//        set => speed = value;
//    }

//    public float RunSpeed
//    {
//        get => runSpeed;
//        set => runSpeed = value;
//    }

//    public float HangTime
//    {
//        get => hangTime;
//        set => hangTime = value;
//    }

//    public float Range
//    {
//        get => range;
//        set => range = value;
//    }

//    public float Release
//    {
//        get => release;
//        set => release = value;
//    }

//    public float CriticalPercent
//    {
//        get => criticalPercent;
//        set => criticalPercent = value;
//    }
//}
