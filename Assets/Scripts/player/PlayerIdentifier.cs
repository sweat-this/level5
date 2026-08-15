using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;

public class PlayerIdentifier : MonoBehaviour
{
    public int pid;
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


    public void setIds(int pid, bool isCpu)
    {
        this.pid = pid;
        this.isCpu = isCpu;
    }
    /// <summary>
    /// Wires this identifier to a human actor. Component resolution only.
    ///
    /// This used to also call <c>intializeShooterStatsFromProfile()</c>, which read
    /// <see cref="MatchRuntime.PrimaryCharacterId"/> - roster slot zero - no matter which human it
    /// was called for, so every human past the first was rebuilt as the slot zero character. Stat
    /// initialization now belongs to <c>SpawnCoordinator</c>, which is the only thing that knows
    /// which roster slot a spawned actor came from. It is also called on the basketball's
    /// identifier, which shares the owner's CharacterProfile - so leaving initialization here ran
    /// it twice on the same component.
    /// </summary>
    public void setPlayer(GameObject player)
    {
        this.player = player;
        playerController = player.GetComponent<PlayerController>();
        characterProfile = player.GetComponent<CharacterProfile>();
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
