using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;


public class BasketBallState : MonoBehaviour
{
    [SerializeField]
    private bool _twoPoints;
    [SerializeField]
    private bool _threePoints;
    [SerializeField]
    private bool _fourPoints;
    [SerializeField]
    private bool _sevenPoints;
    private bool _twoAttempt;
    private bool _threeAttempt;
    private bool _fourAttempt;
    private bool _sevenAttempt;
    private bool _dunk;
    [SerializeField]
    private bool _inAir;
    [SerializeField]
    private bool _thrown;
    [SerializeField]
    private bool _locked;
    private bool _canPullBall;
    [SerializeField]
    private bool _grounded;

    private bool _playerOnMarker;
    private bool _playerOnMarkerOnShoot;
    private bool _moneyBallEnabledOnShoot;

    private int _currentShotMarkerId;
    private int _onShootShotMarkerId;

    [SerializeField]
    private float _playerDistanceFromRim;
    [SerializeField]
    public bool isCpu;
    [SerializeField]
    GameObject player;

    private GameObject _basketBallPosition;
    private GameObject _basketBallTarget;
    [SerializeField]
    private int _currentShotType;
    private int _currentShotMade;

    public int CurrentShotType => _currentShotType;
    //public static BasketBallState instance;

    private void Awake()
    {
        //instance = this;
    }

    void Start()
    {
        isCpu = GetComponent<PlayerIdentifier>().isCpu;
        if (isCpu)
        {
            player = GetComponent<PlayerIdentifier>().autoPlayer;
        }
        else
        {
            player = GetComponent<PlayerIdentifier>().player;
        }
        //position to shoot basketball at (middle of rim)
        _basketBallTarget = GameObject.Find("basketBall_target");

    }
    void Update()
    {
        if (MatchRuntime.Rules.RequiresBasketball)
        {
            PlayerDistanceFromRim = Vector3.Distance(new Vector3(player.transform.position.x,0, player.transform.position.z), new Vector3(_basketBallTarget.transform.position.x,0, _basketBallTarget.transform.position.z));
            //PlayerDistanceFromRim = Mathf.Abs( GameLevelManager.instance.Player.transform.position.z - _basketBallTarget.transform.position.z);

            // is player on  marker  +  is marker required for game mode
            if (GameRules.instance != null && GameRules.instance.PositionMarkersRequired)
            {
                BasketBallShotMarker marker = CurrentShotMarker();
                PlayerOnMarker = marker != null && (isCpu ? marker.AutoPlayerOnMarker : marker.PlayerOnMarker);
            }

            if (PlayerDistanceFromRim < Constants.DISTANCE_3point)
            {
                TwoPoints = true;
                _currentShotType = 2;
            }
            else
            {
                TwoPoints = false;
            }
            if (PlayerDistanceFromRim >= Constants.DISTANCE_3point && PlayerDistanceFromRim < Constants.DISTANCE_4point)
            {
                ThreePoints = true;
                _currentShotType = 3;
            }
            else
            {
                ThreePoints = false;
            }
            if (PlayerDistanceFromRim >= Constants.DISTANCE_4point && PlayerDistanceFromRim < Constants.DISTANCE_7point)
            {
                FourPoints = true;
                _currentShotType = 4;
            }
            else
            {
                FourPoints = false;
            }

            if (PlayerDistanceFromRim > Constants.DISTANCE_7point)
            {
                SevenPoints = true;
                _currentShotType = 7;
            }
            else
            {
                SevenPoints = false;
            }
        }
    }

    private BasketBallShotMarker CurrentShotMarker()
    {
        List<BasketBallShotMarker> markers =
            GameRules.instance != null ? GameRules.instance.BasketBallShotMarkersList : null;

        if (markers == null || CurrentShotMarkerId < 0 || CurrentShotMarkerId >= markers.Count)
        {
            return null;
        }

        return markers[CurrentShotMarkerId];
    }

    public void ResetShotAttemptSnapshot()
    {
        TwoAttempt = false;
        ThreeAttempt = false;
        FourAttempt = false;
        SevenAttempt = false;
        MoneyBallEnabledOnShoot = false;
        PlayerOnMarkerOnShoot = false;
        OnShootShotMarkerId = 0;
    }

    //public bool isConsecutiveShot(GameStats gameStats)
    //{
    //    // get current state of shots made/attempted
    //    _currentShotMade = (int)gameStats.ShotMade;
    //    _currentShotAttempts = (int)gameStats.ShotAttempt;

    //    // if current is == expected made/attempt, increment consecutive and not a 2 point shot
    //    // 
    //    if (_currentShotMade == _expectedShotMade
    //        && _currentShotAttempts == _expectedShotAttempts
    //        && !basketBallState.TwoAttempt)
    //    {
    //        _consecutiveShotsMade++;
    //        // increment expected values for next shot
    //        _expectedShotMade = _currentShotMade + 1;
    //        _expectedShotAttempts = _currentShotAttempts + 1;
    //    }
    //    // else, not consecutive shot. get current, increment for next expected consecutive shot
    //    else
    //    {
    //        _consecutiveShotsMade = 1;
    //        // increment expected values for next shot
    //        _expectedShotMade = _currentShotMade + 1;
    //        _expectedShotAttempts = _currentShotAttempts + 1;
    //    }
    //    // if current consecutive greater than previous high consecutive
    //    if (ConsecutiveShotsMade > gameStats.MostConsecutiveShots)
    //    {
    //        gameStats.MostConsecutiveShots = ConsecutiveShotsMade;
    //    }
    //    return true;
    //}
    public bool MoneyBallEnabledOnShoot
    {
        get => _moneyBallEnabledOnShoot;
        set => _moneyBallEnabledOnShoot = value;
    }
    public bool PlayerOnMarker
    {
        get => _playerOnMarker;
        set => _playerOnMarker = value;
    }
    public bool PlayerOnMarkerOnShoot
    {
        get => _playerOnMarkerOnShoot;
        set => _playerOnMarkerOnShoot = value;
    }
    public int CurrentShotMarkerId
    {
        get => _currentShotMarkerId;
        set => _currentShotMarkerId = value;
    }
    public int OnShootShotMarkerId
    {
        get => _onShootShotMarkerId;
        set => _onShootShotMarkerId = value;
    }
    public bool TwoPoints
    {
        get => _twoPoints;
        set => _twoPoints = value;
    }
    public bool ThreePoints
    {
        get => _threePoints;
        set => _threePoints = value;
    }
    public bool FourPoints
    {
        get => _fourPoints;
        set => _fourPoints = value;
    }
    public bool SevenPoints
    {
        get => _sevenPoints;
        set => _sevenPoints = value;
    }
    public bool TwoAttempt
    {
        get => _twoAttempt;
        set => _twoAttempt = value;
    }
    public bool ThreeAttempt
    {
        get => _threeAttempt;
        set => _threeAttempt = value;
    }
    public bool FourAttempt
    {
        get => _fourAttempt;
        set => _fourAttempt = value;
    }

    public bool SevenAttempt
    {
        get => _sevenAttempt;
        set => _sevenAttempt = value;
    }
    public bool Dunk
    {
        get => _dunk;
        set => _dunk = value;
    }
    public bool InAir
    {
        get => _inAir;
        set => _inAir = value;
    }
    public bool Thrown
    {
        get => _thrown;
        set => _thrown = value;
    }
    public bool Locked
    {
        get => _locked;
        set => _locked = value;
    }
    public bool CanPullBall
    {
        get => _canPullBall;
        set => _canPullBall = value;
    }
    public bool Grounded
    {
        get => _grounded;
        set => _grounded = value;
    }
    public float PlayerDistanceFromRim
    {
        get => _playerDistanceFromRim;
        set => _playerDistanceFromRim = value;
    }
    public GameObject BasketBallPosition
    {
        get => _basketBallPosition;
        set => _basketBallPosition = value;
    }
    public GameObject BasketBallTarget
    {
        get => _basketBallTarget;
        set => _basketBallTarget = value;
    }
    public GameObject Player { get => player; set => player = value; }
}
