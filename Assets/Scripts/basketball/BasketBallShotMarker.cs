using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

public class BasketBallShotMarker : MonoBehaviour
{
    [SerializeField]
    private bool _playerOnMarker;
    [SerializeField]
    private bool _autoPlayerOnMarker;
    private bool markerEnabled; // flag used to indicate max shots have not been achieved

    private GameObject basketBallTarget;
    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// AUD-010 Phase 1c: the participant whose attempt first reached <see cref="maxShotAttempt"/> on
    /// this marker - captured once by <see cref="RegisterAttempt"/>, not re-derived. Final-attempt
    /// completion below waits on this exact runtime's actor/ball state, not
    /// <c>GameLevelManager.instance.players[0]</c>, so a non-primary participant's final shot is not
    /// gated on the primary player's state.
    ///
    /// Same caveat as <see cref="IShooterActor"/>'s own doc comment: this is an interface-typed
    /// reference to a UnityEngine.Object-backed implementer, so <c>== null</c> below is plain
    /// reference equality, not Unity's destroyed-object-aware overload. Not a live bug - nothing in
    /// the current codebase <c>Destroy()</c>s a player/ball mid-match - but worth knowing before that
    /// changes.
    /// </summary>
    private IBasketballRuntime finalAttemptRuntime;
    private bool loggedMissingFinalAttemptRuntime;

    [SerializeField] public int positionMarkerId; // identitfy specific marker
    // spcific marker's stats
    [SerializeField] private int _shotMade;
    [SerializeField] private int _shotAttempt;
    [SerializeField] private int maxShotAttempt;
    [SerializeField] private int maxShotMade;

    // flags used to idenify marker
    // true value determines whether or not marker is active in Gamerules.cs, aprox. line 250
    [SerializeField] public bool shotTypeThree;
    [SerializeField] public bool shotTypeFour;
    [SerializeField] public bool shotTypeSeven;

    private bool detectCollisions;
    private float distanceFromRim;

    // text stuff todo: move to game rules
    private Text displayCurrentMarkerStats;
    private const string displayStatsTextObject = "shot_marker_stats";

    public bool locked = false;

    // Start is called before the first frame update
    void Start()
    {
        _shotMade = 0;
        _shotAttempt = 0;

        displayCurrentMarkerStats = GameObject.Find(displayStatsTextObject).GetComponent<Text>();
        displayCurrentMarkerStats.text = "";

        // used to control opacity of marker image 
        // todo: maybe just disable object. might require more work than it's worth
        spriteRenderer = GetComponent<SpriteRenderer>();

        // initial text display
        setDisplayText();
        // set what type of shot marker is based on distance from rim
        // using basketball state
        setMarkerShotType();
        //test flag
        //MatchRuntime.Rules.RequiresShotMarkers4s = true;
        if (MatchRuntime.Rules.RequiresShotMarkers3s || MatchRuntime.Rules.RequiresShotMarkers4s || MatchRuntime.Rules.RequiresShotMarkers7s)
        {
            markerEnabled = true;
            setDisplayText();
            // set what type of shot marker is based on distance from rim
            // using basketball state
            setMarkerShotType();
        }
        else // marker is not needed
        {
            // disable text and disable script
            displayCurrentMarkerStats.text = "";
            this.enabled = false;
        }

        // failsafe check. data is serialzed and can be set manually but automatic is better. trust the code
        //if (GameRules.instance.GameModeThreePointContest
        //    || GameRules.instance.GameModeFourPointContest
        //    || GameRules.instance.GameModeSevenPointContest
        //    || GameRules.instance.GameModeAllPointContest)
        //{
        //    maxShotAttempt = 5;
        //}

        // if script disabled, disable collisions flag.
        // collisions/colliders still detected if script disabled
        detectCollisions = this.enabled;
    }

    // Update is called once per frame
    void Update()
    {
        // if time's up
        if (Time.timeScale <= 0)
        {
            displayCurrentMarkerStats.text = "";
        }
        // this needs to be turned off if ball hits ground
        if (PlayerOnMarker /*|| _autoPlayerOnMarker && MatchRuntime.ParticipantCount >= 1*/)
        {
            // if marker not completed yet
            if (markerEnabled)
            {
                setDisplayText();
            }
        }
        bool isPointContestMode = IsPointContestMode();

        // if game mode is 3/4/all point contest
        if (isPointContestMode)
        {
            // max shot attempts reached
            if (ShotAttempt >= maxShotAttempt && markerEnabled)
            {
                if (finalAttemptRuntime == null)
                {
                    // The counter reached max with no captured final-attempt runtime - an
                    // ownership/composition bug (RegisterAttempt always sets it the moment
                    // ShotAttempt first equals maxShotAttempt). Never substitute player 0; log once
                    // rather than every frame this condition holds.
                    if (!loggedMissingFinalAttemptRuntime)
                    {
                        Debug.LogError($"BasketBallShotMarker '{name}': ShotAttempt reached MaxShotAttempt with no captured final-attempt runtime - marker completion is blocked.", this);
                        loggedMissingFinalAttemptRuntime = true;
                    }
                }
                // player NOT in air, player does NOT have ball, ball ! in air - the participant who
                // took the final attempt, not GameLevelManager.instance.players[0].
                else if (!finalAttemptRuntime.Actor.HasBasketball
                    && !finalAttemptRuntime.Actor.InAir
                    && !finalAttemptRuntime.State.InAir)
                {
                    markerEnabled = false;
                    // decrease markers remaining
                    GameRules.instance.MarkersRemaining--;
                    spriteRenderer.color = new Color(1f, 1f, 1f, 0f); // opacity to 0
                    setDisplayText();

                    //check if last remaining shot marker
                    if (GameRules.instance.IsGameOver())
                    {
                        //GameRules.instance.CounterTime = Timer.instance.CurrentTime;
                        GameRules.instance.RequestGameOver();
                    }
                }
            }
        }
        // game mode is NOT 3/4/All point contest
        if (!isPointContestMode)
        {
            // if made # of shots required at shot marker
            if (ShotMade >= MaxShotMade && markerEnabled)
            {
                markerEnabled = false;
                // decrease markers remaining
                GameRules.instance.MarkersRemaining--;
                spriteRenderer.color = new Color(1f, 1f, 1f, 0f); // opacity to 0
                setDisplayText();

                // check if last remaining shot marker
                if (GameRules.instance.IsGameOver())
                {
                    //GameRules.instance.CounterTime = Timer.instance.CurrentTime;
                    GameRules.instance.RequestGameOver();
                }
            }
        }

    }

    void OnTriggerEnter(Collider other)
    {
        // if player enters shot marker area
        if (other.gameObject.CompareTag("playerHitbox") && gameObject.CompareTag("shot_marker")
            && detectCollisions)
        {
            // Code review: only flip the role-wide presentation flag once the participant is
            // actually resolved, so a failed resolution (logged by ResolveParticipantState) truly
            // ignores the whole transition instead of leaving the marker's display state
            // inconsistent with no participant's BasketBallState having been updated.
            BasketBallState state = ResolveParticipantState(other, cpu: false);
            if (state != null)
            {
                _playerOnMarker = true;
                state.EnterShotMarker(this);
            }
        }
        // if player enters shot marker area
        if (other.gameObject.CompareTag("autoPlayerHitbox") && gameObject.CompareTag("shot_marker")
            && detectCollisions)
        {
            BasketBallState state = ResolveParticipantState(other, cpu: true);
            if (state != null)
            {
                _autoPlayerOnMarker = true;
                state.EnterShotMarker(this);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // if player exits shot marker area
        if (other.gameObject.CompareTag("playerHitbox") && gameObject.CompareTag("shot_marker")
                && detectCollisions)
        {
            _playerOnMarker = false;
            setDisplayText(); // update display to empty
            ResolveParticipantState(other, cpu: false)?.ExitShotMarker(this);
        }
        // if player exits shot marker area
        if (other.gameObject.CompareTag("autoPlayerHitbox") && gameObject.CompareTag("shot_marker")
                && detectCollisions)
        {
            _autoPlayerOnMarker = false;
            locked = false;
            setDisplayText(); // update display to empty
            ResolveParticipantState(other, cpu: true)?.ExitShotMarker(this);
        }
    }

    /// <summary>
    /// Resolves the exact participant this hitbox belongs to, through the actor-side
    /// <see cref="PlayerIdentifier"/> - never a role-wide flag or GameLevelManager.players[0]. Logs
    /// and returns null rather than throwing or falling back to another participant; an ordinary
    /// trigger callback must not crash the scene over an incomplete composition.
    /// </summary>
    private BasketBallState ResolveParticipantState(Collider other, bool cpu)
    {
        PlayerIdentifier identifier = other.GetComponentInParent<PlayerIdentifier>();
        GameObject ball = identifier != null ? (cpu ? identifier.autoBasketball : identifier.basketball) : null;
        BasketBallState state = ball != null ? ball.GetComponent<BasketBallState>() : null;

        if (state == null)
        {
            string role = cpu ? "CPU" : "human";
            Debug.LogError($"BasketBallShotMarker '{name}': could not resolve the {role} participant's BasketBallState from '{other.name}' - ignoring this marker transition.", this);
        }

        return state;
    }

    /// <summary>
    /// Registers one attempt on this marker for <paramref name="runtime"/>'s shot. Only captures
    /// <see cref="finalAttemptRuntime"/> the moment the counter first reaches
    /// <see cref="maxShotAttempt"/> - a later extra attempt (taken before the marker disables) must
    /// not overwrite it.
    /// </summary>
    public void RegisterAttempt(IBasketballRuntime runtime)
    {
        ShotAttempt++;

        if (ShotAttempt == maxShotAttempt)
        {
            finalAttemptRuntime = runtime;
        }
    }

    private void setDisplayText()
    {
        bool isPointContestMode = IsPointContestMode();

        // if player on marker and markers necessary for game mode and IS 3,4,All point contest
        if ((PlayerOnMarker || _autoPlayerOnMarker) && markerEnabled
            && isPointContestMode)
        {
            displayCurrentMarkerStats.text = "total points : " + BasketBall.instance.GameStats.TotalPoints + "\n"
                                             // + "current marker : " + positionMarkerId + "\n"
                                             + "made : " + ShotMade + " / " + ShotAttempt + "\n"
                                             + "remaining : " + (maxShotAttempt - ShotAttempt);
        }
        // if player on marker and markers necessary for game mode and NOT 3,4,All point contest
        if ((PlayerOnMarker || _autoPlayerOnMarker) && markerEnabled
            && !isPointContestMode)
        {
            displayCurrentMarkerStats.text = "markers remaining : " + GameRules.instance.MarkersRemaining + "\n"
                                             // + "current marker : " + positionMarkerId + "\n"
                                             + "made : " + ShotMade + " / " + ShotAttempt + "\n"
                                             + "remaining : " + (maxShotMade - ShotMade);
        }
        // if player not on marker or marker disabled (max shots made)
        if (!(PlayerOnMarker || _autoPlayerOnMarker) || !markerEnabled)//&& markerEnabled)
        {
            displayCurrentMarkerStats.text = "markers remaining : " + GameRules.instance.MarkersRemaining + "\n"
                                             //   + "current marker : \n"
                                             + "made : \n"
                                             + "remaining : ";
        }
    }

    private static bool IsPointContestMode()
    {
        return GameRules.instance.GameModeThreePointContest
            || GameRules.instance.GameModeFourPointContest
            || GameRules.instance.GameModeSevenPointContest
            || GameRules.instance.GameModeAllPointContest;
    }

    // the shot type is set manually but this is a failsafe check that sets it automatically based 
    // on distance from the rim
    void setMarkerShotType()
    {
        // get distance from rim
        //basketBallTarget = basketBallState.BasketBallTarget;
        basketBallTarget = GameObject.Find("basketBall_target");
        distanceFromRim = Vector3.Distance(transform.position,new Vector3( basketBallTarget.transform.position.x,0, basketBallTarget.transform.position.z));

        if (distanceFromRim > Constants.DISTANCE_3point)
        {
            shotTypeThree = true;
            shotTypeFour = false;
            shotTypeSeven = false;
        }

        if (distanceFromRim > Constants.DISTANCE_4point)
        {
            shotTypeThree = false;
            shotTypeFour = true;
            shotTypeSeven = false;
        }

        if (distanceFromRim > Constants.DISTANCE_7point)
        {
            shotTypeThree = false;
            shotTypeFour = false;
            shotTypeSeven = true;
        }
    }

    public int ShotMade
    {
        get => _shotMade;
        set => _shotMade = value;
    }

    public int ShotAttempt
    {
        get => _shotAttempt;
        set => _shotAttempt = value;
    }

    public int PositionMarkerId
    {
        get => positionMarkerId;
        set => positionMarkerId = value;
    }
    public int MaxShotMade => maxShotMade;
    public bool PlayerOnMarker => _playerOnMarker;
    public bool ShotTypeThree => shotTypeThree;
    public bool ShotTypeFour => shotTypeFour;
    public bool ShotTypeSeven => shotTypeSeven;
    public bool MarkerEnabled { get => markerEnabled; set => markerEnabled = value; }
    public bool AutoPlayerOnMarker { get => _autoPlayerOnMarker; set => _autoPlayerOnMarker = value; }
    public int MaxShotAttempt { get => maxShotAttempt; set => maxShotAttempt = value; }
}


