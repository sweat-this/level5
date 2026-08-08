using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;

public class PlayerIdentifier : MonoBehaviour
{
    public int pid;
    public int bid;
    public int bsid;
    public bool isCpu;
    public bool isDefensivePlayer;
    [SerializeField]
    public GameObject player;
    [SerializeField]
    public GameObject basketball;
    [SerializeField]
    public GameObject autoPlayer;
    [SerializeField]
    public GameObject autoBasketball;
    public PlayerController playerController;
    public AutoPlayerController autoPlayerController;
    public BasketBall basketBallController;
    public BasketBallAuto basketBallAutoController;
    public BasketBallState basketBallState;
    public CharacterProfile characterProfile;
    public GameStats gameStats;


    public void setIds(int pid, int bid, int bsid, bool isCpu)
    {
        this.pid = pid;
        this.bid = bid;
        this.bsid = bsid;
        this.isCpu = isCpu;
    }
    public void setPlayer(GameObject player)
    {
        this.player = player;
        playerController = player.GetComponent<PlayerController>();
        characterProfile = player.GetComponent<CharacterProfile>();
        if(MatchRuntime.HasConfiguration) { 
            characterProfile.intializeShooterStatsFromProfile();
        }
    }
    public void setAutoPlayer(GameObject autoPlayer)
    {
        this.autoPlayer = autoPlayer;
        autoPlayerController = autoPlayer.GetComponent<AutoPlayerController>();
        characterProfile = autoPlayer.GetComponent<CharacterProfile>();
    }
    public void setBasketball(GameObject basketball)
    {
        this.basketball = basketball;
        basketBallController = basketball.GetComponent<BasketBall>();
        basketBallState = basketball.GetComponent<BasketBallState>();
        gameStats = basketball.GetComponent<GameStats>();
    }
    public void setAutoBasketball(GameObject autoBasketball)
    {
        this.autoBasketball = autoBasketball;
        basketBallAutoController = autoBasketball.GetComponent<BasketBallAuto>();
        basketBallState = autoBasketball.GetComponent<BasketBallState>();
        gameStats = autoBasketball.GetComponent<GameStats>();
    }
}
