using System.Collections.Generic;
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

    /// <summary>
    /// AUD-010 Phase 1c: marker-local presentation occupancy, keyed by the exact collider still
    /// inside the trigger volume - not by participant. <see cref="BasketBallState.CurrentShotMarker"/>
    /// remains each participant's own gameplay occupancy and is unaffected by these sets. One
    /// occupant's collider exiting must not clear another same-role occupant's presentation state, so
    /// <see cref="_playerOnMarker"/>/<see cref="_autoPlayerOnMarker"/> are mirrors of "is this set
    /// non-empty", synchronized by <see cref="SyncHumanPresentationOccupancy"/>/
    /// <see cref="SyncCpuPresentationOccupancy"/> - never serialized, never restored across a scene
    /// load. Live-lifecycle check (AUD-010 Phase 1c preflight): no current code destroys, deactivates,
    /// or disables a player's "playerHitbox"/"autoPlayerHitbox" collider mid-match (SpawnCoordinator
    /// only instantiates at match setup), so no stale-entry pruning is added here.
    /// </summary>
    private readonly HashSet<Collider> humanOccupants = new HashSet<Collider>();
    private readonly HashSet<Collider> cpuOccupants = new HashSet<Collider>();

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

    /// <summary>
    /// AUD-010 Phase 1c: live session-wide marker state, bound once by <see cref="GameRules"/>'s own
    /// composition step (<see cref="BindShotMarkerSession"/>), before <see cref="Start"/> runs. Unlike
    /// <see cref="BasketBall"/>'s <c>IMoneyBallState</c> binding, this one is a mandatory <see cref="Start"/>
    /// dependency - every production marker reads it for presentation and completion, so a missing
    /// binding fails the marker closed instead of proceeding with a null reference.
    /// </summary>
    private IShotMarkerSession markerSession;

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
        // AUD-010 Phase 1c: fail-closed composition check, before the first call below that reads
        // markerSession (setDisplayText). Every production marker receives its session from
        // GameRules.Awake() before this Start() can run - a null here is a composition bug, not
        // reachable gameplay state. detectCollisions is set explicitly (not just this.enabled) because
        // OnTriggerEnter/OnTriggerExit gate on that flag directly, not on the component's enabled state.
        if (markerSession == null)
        {
            Debug.LogError($"BasketBallShotMarker '{name}': Start() reached with no bound IShotMarkerSession - marker composition is incomplete; disabling this marker.", this);
            detectCollisions = false;
            this.enabled = false;
            return;
        }

        _shotMade = 0;
        _shotAttempt = 0;

        displayCurrentMarkerStats = GameObject.Find(displayStatsTextObject).GetComponent<Text>();
        displayCurrentMarkerStats.text = "";

        // used to control opacity of marker image 
        // todo: maybe just disable object. might require more work than it's worth
        spriteRenderer = GetComponent<SpriteRenderer>();

        // initial text display
        setDisplayText(IsPointContestMode());
        // set what type of shot marker is based on distance from rim
        // using basketball state
        setMarkerShotType();
        //test flag
        //MatchRuntime.Rules.RequiresShotMarkers4s = true;
        if (MatchRuntime.Rules.RequiresShotMarkers3s || MatchRuntime.Rules.RequiresShotMarkers4s || MatchRuntime.Rules.RequiresShotMarkers7s)
        {
            markerEnabled = true;
            setDisplayText(IsPointContestMode());
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

        // AUD-010 Phase 1c: resolved once per frame and reused by every call below - IsPointContestMode()
        // resolves a fresh MatchRuntime.Rules snapshot for a directly-entered scene (no MatchConfiguration),
        // so calling it once here instead of once per call site avoids allocating that snapshot repeatedly
        // in a single Update().
        bool isPointContestMode = IsPointContestMode();

        // this needs to be turned off if ball hits ground
        if (PlayerOnMarker /*|| _autoPlayerOnMarker && MatchRuntime.ParticipantCount >= 1*/)
        {
            // if marker not completed yet
            if (markerEnabled)
            {
                setDisplayText(isPointContestMode);
            }
        }

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
                    CompleteMarker(isPointContestMode);
                }
            }
        }
        // game mode is NOT 3/4/All point contest
        if (!isPointContestMode)
        {
            // if made # of shots required at shot marker
            if (ShotMade >= MaxShotMade && markerEnabled)
            {
                CompleteMarker(isPointContestMode);
            }
        }

    }

    /// <summary>
    /// AUD-010 Phase 1c: the shared completion sequence both the point-contest and non-point branches
    /// above reach once their own readiness condition succeeds. Preserves the exact externally visible
    /// order the two branches previously duplicated: disable this marker, record exactly one session
    /// completion, hide the sprite, refresh the display, then - only if the objective just cleared -
    /// request match end. <see cref="Level5.Core.Match.IShotMarkerSession.RecordMarkerCompleted"/>
    /// itself never requests match end, so that ordering is enforced here, not by the session.
    /// </summary>
    private void CompleteMarker(bool isPointContestMode)
    {
        markerEnabled = false;

        bool objectiveCleared = markerSession.RecordMarkerCompleted();

        spriteRenderer.color = new Color(1f, 1f, 1f, 0f); // opacity to 0
        setDisplayText(isPointContestMode);

        if (objectiveCleared)
        {
            markerSession.RequestMatchEnd();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // if player enters shot marker area
        if (other.gameObject.CompareTag("playerHitbox") && gameObject.CompareTag("shot_marker")
            && detectCollisions)
        {
            // Code review: only add to presentation membership once the participant is actually
            // resolved, so a failed resolution (logged by ResolveParticipantState) truly ignores the
            // whole transition instead of leaving the marker's display state inconsistent with no
            // participant's BasketBallState having been updated.
            BasketBallState state = ResolveParticipantState(other, cpu: false);
            if (state != null)
            {
                AddHumanOccupant(other);
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
                AddCpuOccupant(other);
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
            // The collider has physically left the marker regardless of whether its participant can
            // still be resolved below, so membership removal must not depend on that resolution
            // succeeding - see RemoveHumanOccupant.
            RemoveHumanOccupant(other);
            setDisplayText(IsPointContestMode()); // update display to reflect any remaining occupant
            ResolveParticipantState(other, cpu: false)?.ExitShotMarker(this);
        }
        // if player exits shot marker area
        if (other.gameObject.CompareTag("autoPlayerHitbox") && gameObject.CompareTag("shot_marker")
                && detectCollisions)
        {
            RemoveCpuOccupant(other);
            locked = false;
            setDisplayText(IsPointContestMode()); // update display to reflect any remaining occupant
            ResolveParticipantState(other, cpu: true)?.ExitShotMarker(this);
        }
    }

    /// <summary>
    /// Adds <paramref name="hitbox"/> to human presentation membership. A collider already present is
    /// a no-op (HashSet semantics), so a duplicate OnTriggerEnter cannot drift occupancy.
    /// </summary>
    private void AddHumanOccupant(Collider hitbox)
    {
        humanOccupants.Add(hitbox);
        SyncHumanPresentationOccupancy();
    }

    /// <summary>
    /// Removes <paramref name="hitbox"/> from human presentation membership. Removing a collider that
    /// was never added, or that was already removed, is a harmless no-op. This does not touch
    /// <see cref="BasketBallState"/> - only the marker's own presentation occupancy.
    /// </summary>
    private void RemoveHumanOccupant(Collider hitbox)
    {
        humanOccupants.Remove(hitbox);
        SyncHumanPresentationOccupancy();
    }

    private void AddCpuOccupant(Collider hitbox)
    {
        cpuOccupants.Add(hitbox);
        SyncCpuPresentationOccupancy();
    }

    private void RemoveCpuOccupant(Collider hitbox)
    {
        cpuOccupants.Remove(hitbox);
        SyncCpuPresentationOccupancy();
    }

    /// <summary>
    /// Human presentation occupancy is true while at least one qualifying human collider remains in
    /// the trigger - one occupant exiting cannot clear another occupant's presence.
    /// </summary>
    private void SyncHumanPresentationOccupancy()
    {
        _playerOnMarker = humanOccupants.Count > 0;
    }

    /// <summary>Same rule as <see cref="SyncHumanPresentationOccupancy"/>, for the CPU role.</summary>
    private void SyncCpuPresentationOccupancy()
    {
        _autoPlayerOnMarker = cpuOccupants.Count > 0;
    }

    /// <summary>
    /// Resolves the exact participant this hitbox belongs to, through the basketball-domain
    /// <see cref="IBasketballParticipantStateProvider"/> - never a role-wide flag or
    /// GameLevelManager.players[0]. Logs and returns null rather than throwing or falling back to
    /// another participant; an ordinary trigger callback must not crash the scene over an incomplete
    /// composition.
    ///
    /// Same caveat as <see cref="finalAttemptRuntime"/>'s doc comment: <c>provider</c> is
    /// interface-typed, so <c>!= null</c> below is plain reference equality, not Unity's
    /// destroyed-object-aware overload. Not a live bug - nothing in the current codebase
    /// <c>Destroy()</c>s a player/ball mid-match - but worth knowing before that changes.
    /// </summary>
    private BasketBallState ResolveParticipantState(Collider other, bool cpu)
    {
        IBasketballParticipantStateProvider provider = other.GetComponentInParent<IBasketballParticipantStateProvider>();
        BasketBallState state = provider != null && provider.TryGetBasketballState(cpu, out BasketBallState resolved)
            ? resolved
            : null;

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

    private void setDisplayText(bool isPointContestMode)
    {
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
            displayCurrentMarkerStats.text = "markers remaining : " + markerSession.MarkersRemaining + "\n"
                                             // + "current marker : " + positionMarkerId + "\n"
                                             + "made : " + ShotMade + " / " + ShotAttempt + "\n"
                                             + "remaining : " + (maxShotMade - ShotMade);
        }
        // if player not on marker or marker disabled (max shots made)
        if (!(PlayerOnMarker || _autoPlayerOnMarker) || !markerEnabled)//&& markerEnabled)
        {
            displayCurrentMarkerStats.text = "markers remaining : " + markerSession.MarkersRemaining + "\n"
                                             //   + "current marker : \n"
                                             + "made : \n"
                                             + "remaining : ";
        }
    }

    private static bool IsPointContestMode()
    {
        ResolvedMatchRules rules = MatchRuntime.Rules;

        return rules.IsThreePointContest
            || rules.IsFourPointContest
            || rules.IsSevenPointContest
            || rules.IsAllPointContest;
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

    // ======================= IShotMarkerSession binding (AUD-010 Phase 1c) =======================

    /// <summary>
    /// Explicit shot-marker-session binding from <see cref="GameRules"/>'s own composition step
    /// (<c>GameRules.Awake</c>), called once for every active, scene-authored marker before any
    /// marker's own <see cref="Start"/> runs. Ownership-only - no gameplay side effects.
    /// </summary>
    public void BindShotMarkerSession(IShotMarkerSession session)
    {
        if (session == null)
        {
            Debug.LogError($"BasketBallShotMarker on '{gameObject.name}' was bound with a null shot-marker session.", this);
            return;
        }

        if (markerSession != null)
        {
            Debug.LogError($"BasketBallShotMarker on '{gameObject.name}' already has a bound shot-marker session; ignoring a second BindShotMarkerSession call.", this);
            return;
        }

        markerSession = session;
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


