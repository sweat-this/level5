
using System;
using UnityEngine;
using Level5.Core;
using Level5.Core.Match;
using Random = UnityEngine.Random;


public class BasketBallShotMade : MonoBehaviour
{
    BasketBallState basketBallState;
    AudioSource audioSource;
    [SerializeField]
    public GameObject rimSprite;
    Animator anim;
    bool isColliding;

    const string moneyPrefabPath = "Prefabs/objects/money";
    private GameObject moneyClone;
    //int _consecutiveShotsMade = 0;
    //int _currentShotMade = 0;
    //int _currentShotAttempts = 0;
    //int _expectedShotMade = 1;
    //int _expectedShotAttempts = 1;

    public static BasketBallShotMade instance;
    public event Action<MadeShotResult> ShotResolved;

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
    private bool shotMade1;
    private bool shotMade2;

    private void Awake()
    {
        instance = this;
        //_currentShotMade = 0;
        //_currentShotAttempts = 0;
        //_expectedShotMade = 1;
        //_expectedShotAttempts = 1;
    }

    // Use this for initialization
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        anim = rimSprite.GetComponent<Animator>();
        // path to money prfab
        moneyClone = Resources.Load(moneyPrefabPath) as GameObject;

        // AUD-008: the swish sound and rim animation are a presentation reaction to a made shot,
        // not part of deciding or recording one. Subscribing here instead of calling them inline in
        // shotMade() is this event's first real subscriber - proving ShotResolved (added for
        // AUD-010) actually carries a working audience, not just an unused publish. Same object,
        // same frame, so behavior is identical to the inline calls this replaces.
        ShotResolved += PlayMadeShotPresentation;
    }

    private void PlayMadeShotPresentation(MadeShotResult result)
    {
        audioSource.PlayOneShot(SFXBB.instance.basketballNetSwish);
        anim.Play("madeshot");
    }

    void Update()
    {
        isColliding = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if ((other.gameObject.CompareTag("basketball") || other.gameObject.CompareTag("basketballAuto") ) /*&& (!playerState.hasBasketball || !autoPlayerState.hasBasketball) */
            && !ShotMade1
            && !ShotMade2
            && gameObject.name.Equals("basketBallMadeShot1"))
        {
            shotMade1 = true;
        }
    }

    /// <summary>
    /// AUD-013: scores the shot the collision detected, reading every piece of ball/owner state
    /// through the basketball runtime binding instead of a ball-side <c>PlayerIdentifier</c>. One
    /// method for both human and CPU shots - <paramref name="runtime"/> is whichever of
    /// <see cref="BasketBall"/>/<see cref="BasketBallAuto"/> the colliding ball implements it.
    /// </summary>
    public void shotMade(IBasketballRuntime runtime)
    {
        GameStats gameStats = runtime.Stats;
        basketBallState = runtime.State;
        if (isColliding)
        {
            return;
        }
        else
        {
            isColliding = true;
        }
        shotMade1 = false;
        shotMade2 = false;
        float shotDistance = runtime.LastShotDistance;
        // add to total shot distance made total
        float shotDistanceFeet = shotDistance * 6;
        gameStats.Stats.TotalDistance += shotDistanceFeet;
        // is this the longest shot made?
        if (shotDistanceFeet > gameStats.Stats.LongestShotMade)
        {
            gameStats.Stats.LongestShotMade = shotDistanceFeet;
        }
        // updates shots made/shot attempted
        ShotScore score = updateShotMadeBasketBallStats(gameStats, basketBallState, shotDistance);
        ShotResolved?.Invoke(new MadeShotResult(
            runtime.ParticipantId,
            runtime.IsCpu,
            ShotKindOf(basketBallState),
            score,
            shotDistance,
            gameStats.Stats.TotalPoints));

        // instantiate money if game requires it
        if (GameRules.instance.GameModeRequiresMoneyBall
            && basketBallState.PlayerOnMarkerOnShoot)
        //&& basketBallState.MoneyBallEnabledOnShoot)
        //&& PlayerStats.instance.Money >= 5
        //&& GameRules.instance.MoneyBallEnabled)
        {
            //Debug.Log(" instantiate moeny : player on marker at shoot");
            instantiateMoney(1);
        }
        // reset states
        basketBallState.ResetShotAttemptSnapshot();

        //GameRules.instance.updatePlayerScore();
        //// update onscreen ui stats
        //if (!runtime.IsCpu)
        //{
        //    //basketBall.updateScoreText();
        //    GameRules.instance.updatePlayerScore();
        //}
        //if (runtime.IsCpu)
        //{
        //    //basketBallAuto.updateScoreText();
        //    GameRules.instance.updatePlayerScore();
        //}
        // update game rules ui
    }

    void instantiateMoney(float value)
    {
        // set value of shot
        moneyClone.GetComponent<PickupObject>().updateMoneyValue(value);
        float distance = Random.value;
        Vector3 tempPos = new Vector3(transform.position.x + distance, 0, transform.position.z - 2);
        Instantiate(moneyClone, tempPos, Quaternion.identity);
    }

    /// <summary>
    /// Which line the shot came from.
    ///
    /// The attempt flags are set when the shot is launched and are mutually exclusive by the time a
    /// make is registered, so the first one that is set is the answer. Tested in the order the
    /// original evaluated them, which matters only if they ever stop being exclusive.
    /// </summary>
    private static ShotKind ShotKindOf(BasketBallState basketBallState)
    {
        if (basketBallState.TwoAttempt)
        {
            return ShotKind.Two;
        }

        if (basketBallState.ThreeAttempt)
        {
            return ShotKind.Three;
        }

        if (basketBallState.FourAttempt)
        {
            return ShotKind.Four;
        }

        return basketBallState.SevenAttempt ? ShotKind.Seven : ShotKind.None;
    }

    /// <summary>
    /// The marker the player was standing on when they shot, or null.
    ///
    /// The original indexed the marker list directly at three separate points. It is read once here
    /// and bounds-checked, because <c>OnShootShotMarkerId</c> is a plain int that defaults to 0 -
    /// so a shot taken off any marker in a mode with no markers at all indexed an empty list.
    /// </summary>
    private static BasketBallShotMarker ShotMarkerFor(BasketBallState basketBallState)
    {
        System.Collections.Generic.List<BasketBallShotMarker> markers =
            GameRules.instance != null ? GameRules.instance.BasketBallShotMarkersList : null;

        if (markers == null)
        {
            return null;
        }

        int id = basketBallState.OnShootShotMarkerId;
        return id >= 0 && id < markers.Count ? markers[id] : null;
    }

    private ShotScore updateShotMadeBasketBallStats(GameStats gameStats, BasketBallState basketBallState, float shotDistance)
    {
        // first thing, update shot made total
        // ==================== consecutive shots logic ==============================

        //// get current state of shots made/attempted
        //_currentShotMade = (int)gameStats.ShotMade;
        //_currentShotAttempts = (int)gameStats.ShotAttempt;

        //// if current is == expected made/attempt, increment consecutive and not a 2 point shot
        //// 
        //if (_currentShotMade == _expectedShotMade
        //    && _currentShotAttempts == _expectedShotAttempts
        //    && !basketBallState.TwoAttempt)
        //{
        //    _consecutiveShotsMade++;
        //    // increment expected values for next shot
        //    _expectedShotMade = _currentShotMade + 1;
        //    _expectedShotAttempts = _currentShotAttempts + 1;
        //}
        //// else, not consecutive shot. get current, increment for next expected consecutive shot
        //else
        //{
        //    _consecutiveShotsMade = 1;
        //    // increment expected values for next shot
        //    _expectedShotMade = _currentShotMade + 1;
        //    _expectedShotAttempts = _currentShotAttempts + 1;
        //}
        //// if current consecutive greater than previous high consecutive
        //if (ConsecutiveShotsMade > gameStats.MostConsecutiveShots)
        //{
        //    gameStats.MostConsecutiveShots = ConsecutiveShotsMade;
        //}

        // ==================== point total logic ==============================
        // The rules themselves live in Level5.Core.ShotScoring, where they can be tested without a
        // basketball, a marker list or a running match. This method's job is to describe the shot
        // that was just made and apply the answer.
        //
        // The original tested "is this a marker contest" and "is this Points by Distance" as two
        // branches that could in principle both run. No authored mode is both - Points by Distance
        // has no shot markers - so the extraction treats them as the alternatives they are, and
        // this comment is the record of that being a decision rather than an oversight.
        BasketBallShotMarker marker = ShotMarkerFor(basketBallState);

        ShotScoringInput input = new ShotScoringInput
        {
            Kind = ShotKindOf(basketBallState),
            IsMarkerContest = GameRules.instance.GameModeThreePointContest
                || GameRules.instance.GameModeFourPointContest
                || GameRules.instance.GameModeSevenPointContest
                || GameRules.instance.GameModeAllPointContest,
            ScoresByDistance = MatchRuntime.RawModeId == Modes.PointsByDistance,
            HasStreakBonus = MatchRuntime.RawModeId == Modes.InThePocket,
            ConsecutiveShotsMade = gameStats.Stats.ConsecutiveShotsMade,
            StreakBonusThreshold = GameRules.instance.InThePocketActivateValue,
            OnEnabledMarker = basketBallState.PlayerOnMarkerOnShoot
                && marker != null
                && marker.MarkerEnabled,
            IsFinalMarkerAttempt = marker != null && marker.ShotAttempt == marker.MaxShotAttempt,
            MarkerFinalShotScoresDouble = MatchRuntime.Rules.IsThreePointContest
                || MatchRuntime.Rules.IsFourPointContest
                || MatchRuntime.Rules.IsSevenPointContest,
            MoneyBallActive = basketBallState.MoneyBallEnabledOnShoot,
            ShotDistance = shotDistance
        };

        // AUD-065: scores the shot and updates the made-shot/streak state it depends on, in the one
        // order both require. Covered directly by Level5GameStatsApplyMadeShotTests - see that file
        // for why the regression this guards against needs GameStats/BasketballState only, not a
        // running GameRules/MatchRuntime.
        ShotScore score = gameStats.ApplyMadeShot(basketBallState, input);

        gameStats.Stats.TotalPoints += score.Points;
        gameStats.Stats.MoneyBallMade += score.MoneyBallMade;

        // ==================== requires position markers logic ==============================
        if (basketBallState.PlayerOnMarkerOnShoot 
            && (MatchRuntime.Rules.RequiresShotMarkers3s || MatchRuntime.Rules.RequiresShotMarkers4s || MatchRuntime.Rules.RequiresShotMarkers7s))
        {
            // if money ball enabled
            if (basketBallState.MoneyBallEnabledOnShoot)
            {
                int max = GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].MaxShotMade;
                GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotMade = max;
            }
            // no money ball, update current shot marker stats
            else
            {
                GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotMade++;
            }
        }

        return score;
    }

    //public int ConsecutiveShotsMade { get => _consecutiveShotsMade; }
    public bool ShotMade1 { get => shotMade1; set => shotMade1 = value; }
    public bool ShotMade2 { get => shotMade2; set => shotMade2 = value; }
}

