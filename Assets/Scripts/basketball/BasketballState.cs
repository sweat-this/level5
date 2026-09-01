using System;
using System.Collections;
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

    // AUD-010 Phase 1c: the participant's marker occupancy is the marker reference itself, not an
    // id resolved through GameRules.BasketBallShotMarkersList - see BasketBallShotMarker.EnterShotMarker
    // / ExitShotMarker (pushed from this participant's own hitbox trigger) and CaptureShotMarkerForAttempt
    // (the launch-time snapshot). Runtime-only: never serialized, never restored across a scene load.
    private BasketBallShotMarker currentShotMarker;
    private BasketBallShotMarker onShootShotMarker;

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

    /// <summary>Whether <see cref="BindOwner"/> has run. Set once, at spawn time.</summary>
    public bool Bound { get; private set; }

    /// <summary>
    /// AUD-013: explicit owner/role binding from the same composition operation that spawns the ball
    /// (<see cref="SpawnCoordinator.GiveBall"/>, via <see cref="IBasketballRuntime.BindOwner"/>), so
    /// <see cref="Start"/> no longer independently rediscovers ownership from a ball-side
    /// <c>PlayerIdentifier</c>.
    /// </summary>
    public void BindOwner(bool isCpu, GameObject ownerActor)
    {
        if (Bound)
        {
            Debug.LogError($"BasketBallState on '{gameObject.name}' is already bound; ignoring a second BindOwner call.", this);
            return;
        }

        this.isCpu = isCpu;
        player = ownerActor;
        Bound = true;
    }

    void Start()
    {
        if (!Bound)
        {
            // SetActive rather than enabled = false: this class has no collision/trigger handlers of
            // its own, but sibling components on the same GameObject (BasketBall/BasketBallAuto) do,
            // and deactivating here is consistent with their guards for the same unbound state.
            Debug.LogError($"BasketBallState on '{gameObject.name}' reached Start() with no bound owner.", this);
            gameObject.SetActive(false);
            return;
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

            // AUD-010 Phase 1c: marker occupancy is no longer polled here through an id-indexed
            // GameRules list - BasketBallShotMarker pushes it directly via EnterShotMarker/ExitShotMarker
            // as this participant's own hitbox enters/exits a marker's trigger volume.

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

    public void ResetShotAttemptSnapshot()
    {
        TwoAttempt = false;
        ThreeAttempt = false;
        FourAttempt = false;
        SevenAttempt = false;
        MoneyBallEnabledOnShoot = false;
        PlayerOnMarkerOnShoot = false;
        onShootShotMarker = null;
    }

    /// <summary>
    /// This participant enters <paramref name="marker"/>'s trigger volume. Called by
    /// <see cref="BasketBallShotMarker"/> from its own OnTriggerEnter, resolved to this exact
    /// participant through the actor-side <c>PlayerIdentifier</c> - never a role-wide flag. The most
    /// recently entered marker wins; this deliberately does not stack/restore an earlier marker when
    /// two overlap.
    /// </summary>
    public void EnterShotMarker(BasketBallShotMarker marker)
    {
        currentShotMarker = marker;
        PlayerOnMarker = marker != null;
    }

    /// <summary>
    /// This participant exits <paramref name="marker"/>'s trigger volume. Only clears occupancy when
    /// <paramref name="marker"/> is still the current marker - exiting a marker the participant has
    /// already left (the A-then-B overlap case) must not clobber the newer occupancy.
    /// </summary>
    public void ExitShotMarker(BasketBallShotMarker marker)
    {
        if (currentShotMarker != marker)
        {
            return;
        }

        currentShotMarker = null;
        PlayerOnMarker = false;
    }

    /// <summary>
    /// Snapshots <see cref="CurrentShotMarker"/> as the marker this shot was launched from. Called
    /// once per attempt, at launch - see <see cref="BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot"/>.
    /// Once captured, <see cref="OnShootShotMarker"/> must not change for the rest of this attempt,
    /// even if the participant exits or enters another marker while the ball is airborne.
    /// </summary>
    public void CaptureShotMarkerForAttempt()
    {
        onShootShotMarker = currentShotMarker;
        PlayerOnMarkerOnShoot = PlayerOnMarker && onShootShotMarker != null;
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
    /// <summary>The marker this participant occupies right now, or null.</summary>
    public BasketBallShotMarker CurrentShotMarker => currentShotMarker;

    /// <summary>The marker captured at this attempt's launch, or null. See <see cref="CaptureShotMarkerForAttempt"/>.</summary>
    public BasketBallShotMarker OnShootShotMarker => onShootShotMarker;
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
