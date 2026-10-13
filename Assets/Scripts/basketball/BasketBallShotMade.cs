
using System;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core;
using Level5.Core.Match;
using Random = UnityEngine.Random;


public class BasketBallShotMade : MonoBehaviour
{
    AudioSource audioSource;
    [SerializeField]
    public GameObject rimSprite;
    Animator anim;
    // One entry per live basketball, owned only by this hoop. Repeated callbacks allocate nothing.
    private readonly List<AttemptCrossing> crossings = new List<AttemptCrossing>();

    private sealed class AttemptCrossing
    {
        public IBasketballRuntime Runtime;
        public ulong Generation;
        public bool UpperEntered;
        public bool Scored;
    }

    const string moneyPrefabPath = "Prefabs/objects/money";
    private GameObject moneyClone;

    /// <summary>
    /// AUD-010 Phase 1c: replaces the read of <c>GameRules.instance.InThePocketActivateValue</c>,
    /// which is serialized on <c>GameManager.prefab</c>/every gameplay scene, reset to 0 in
    /// <c>GameRules.Awake()</c>, and never written by any production code path - so this is the
    /// value every made shot has always scored In the Pocket against. Making it explicit here
    /// removes the last live basketball -&gt; GameRules dependency without changing that behaviour.
    /// Whether In the Pocket should use a nonzero/configurable threshold is a separate gameplay
    /// decision, not addressed by this constant.
    /// </summary>
    private const int CurrentInThePocketStreakBonusThreshold = 0;

    /// <summary>
    /// AUD-010 Phase 2b0: explicit bind-once match context, replacing this component's own
    /// <c>MatchRuntime.Rules</c>/<c>MatchRuntime.RawModeId</c> reads. Composition
    /// (<c>GameLevelManager.Awake</c>) supplies both once, the same bind-once/null-guard/no-rebind
    /// shape <c>BasketBall.BindMatchRules</c> already established. <see cref="hasBoundMatchContext"/>
    /// is a separate flag rather than checking <see cref="gameModeId"/> against a sentinel, because
    /// <see cref="GameModeId.None"/> is itself a valid bound value, not "unbound".
    /// </summary>
    private ResolvedMatchRules matchRules;
    private GameModeId gameModeId;
    private bool hasBoundMatchContext;
    private Func<bool> canAcceptMadeShot;
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
        crossings.Clear();
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

    private void OnDisable()
    {
        // A disabled trigger loses pending crossings. Retain consumed attempts while this hoop
        // exists so disabling/re-enabling it cannot award a rebound for an already scored attempt.
        foreach (AttemptCrossing crossing in crossings)
            crossing.UpperEntered = false;
        shotMade1 = false;
        shotMade2 = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled
            && (other.gameObject.CompareTag("basketball") || other.gameObject.CompareTag("basketballAuto"))
            && gameObject.name.Equals("basketBallMadeShot1"))
        {
            IBasketballRuntime runtime = other.GetComponent<IBasketballRuntime>();
            if (runtime == null)
            {
                Debug.LogError($"'{other.gameObject.name}' triggered a made shot with no basketball runtime binding.", other.gameObject);
                return;
            }
            RegisterUpperCrossing(runtime);
        }
    }

    private bool HasRuntimeState(IBasketballRuntime runtime)
    {
        if (runtime == null || (runtime is UnityEngine.Object obj && obj == null)
            || runtime.State == null || runtime.Stats == null)
        {
            Debug.LogError($"BasketBallShotMade on '{name}' received an invalid basketball runtime binding; skipping crossing.", this);
            return false;
        }
        return true;
    }

    private AttemptCrossing FindCrossing(IBasketballRuntime runtime)
    {
        AttemptCrossing found = null;
        for (int i = crossings.Count - 1; i >= 0; i--)
        {
            AttemptCrossing crossing = crossings[i];
            if (crossing.Runtime is UnityEngine.Object obj && obj == null)
                crossings.RemoveAt(i);
            else if (ReferenceEquals(crossing.Runtime, runtime))
                found = crossing;
        }
        return found;
    }

    private void RegisterUpperCrossing(IBasketballRuntime runtime)
    {
        if (!isActiveAndEnabled || !HasRuntimeState(runtime)) return;
        ulong generation = runtime.State.ShotAttemptGeneration;
        if (generation == 0) return; // A ball that has never attempted a shot cannot score.

        AttemptCrossing crossing = FindCrossing(runtime);
        if (crossing == null)
        {
            crossing = new AttemptCrossing { Runtime = runtime };
            crossings.Add(crossing);
        }
        if (crossing.Generation != generation)
        {
            crossing.Generation = generation;
            crossing.Scored = false;
        }
        if (crossing.Scored) return;
        crossing.UpperEntered = true;
        shotMade1 = true;
    }

    /// <summary>
    /// AUD-013: scores the shot the collision detected, reading every piece of ball/owner state
    /// through the basketball runtime binding instead of a ball-side <c>PlayerIdentifier</c>. One
    /// method for both human and CPU shots - <paramref name="runtime"/> is whichever of
    /// <see cref="BasketBall"/>/<see cref="BasketBallAuto"/> the colliding ball implements it.
    /// </summary>
    public void shotMade(IBasketballRuntime runtime)
    {
        shotMade1 = false;
        shotMade2 = false;
        if (!isActiveAndEnabled || !HasRuntimeState(runtime)) return;

        GameStats gameStats = runtime.Stats;
        BasketBallState basketBallState = runtime.State;
        AttemptCrossing crossing = FindCrossing(runtime);
        if (crossing == null || !crossing.UpperEntered || crossing.Scored) return;
        crossing.UpperEntered = false;
        if (crossing.Generation != basketBallState.ShotAttemptGeneration) return;

        // AUD-010 Phase 2b0: an unbound component is a composition failure, not reachable gameplay
        // state on any production instance - GameLevelManager.Awake binds every scene's
        // BasketBallShotMade before any Start() can run. Fails closed before any scoring/marker/
        // money-ball mutation or MadeShotResult publication. The pending crossing is consumed above
        // so binding a context later cannot authorize an old lower crossing.
        if (!hasBoundMatchContext)
        {
            Debug.LogError($"BasketBallShotMade on '{gameObject.name}' has no bound match context; skipping scoring for participant {runtime.ParticipantId}'s made shot.", this);
            return;
        }

        // Consume before eligibility/scoring/events. Rejected late attempts cannot re-arm through
        // another upper crossing or hoop disable/enable, even if an eligibility provider changes.
        crossing.Scored = true;
        if (canAcceptMadeShot == null || !canAcceptMadeShot()) return;

        // AUD-010 Phase 1c/2b0: one immutable-rules snapshot for the whole made-shot operation,
        // reused by every decision below instead of each resolving MatchRuntime.Rules separately.
        ResolvedMatchRules rules = matchRules;

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
        ShotScore score = updateShotMadeBasketBallStats(gameStats, basketBallState, shotDistance, rules);
        ShotResolved?.Invoke(new MadeShotResult(
            runtime.ParticipantId,
            runtime.IsCpu,
            ShotKindOf(basketBallState),
            score,
            shotDistance,
            gameStats.Stats.TotalPoints));

        // instantiate money if game requires it
        if (rules.RequiresMoneyBall
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

    /// <summary>
    /// AUD-010 Phase 2b0: composition's one-time seam for this component's match context. Called by
    /// <c>GameLevelManager.Awake</c>, which already resolves <see cref="ResolvedMatchRules"/> and the
    /// scene's <see cref="GameModeId"/> for every other basketball binding - this component is
    /// scene-authored on the hoop (not spawned by <c>SpawnCoordinator</c>), so it is found and bound
    /// the same way <c>LevelRuntimeContext</c> already is in that method.
    /// </summary>
    public void BindMatchContext(ResolvedMatchRules rules, GameModeId modeId, Func<bool> canAcceptMadeShot = null)
    {
        // Checked before the null-argument branch below: a null second call after a real bind
        // already succeeded must report "already bound", not "leaving it unbound" - matchRules is
        // still the original valid reference either way, and the log should say so. Mirrors
        // BasketBall/BasketBallAuto/BasketBallState's BindMatchRules ordering.
        if (hasBoundMatchContext)
        {
            Debug.LogError($"BasketBallShotMade on '{gameObject.name}' already has bound match context; ignoring a second BindMatchContext call.", this);
            return;
        }

        if (rules == null)
        {
            Debug.LogError($"BasketBallShotMade on '{gameObject.name}' received a null ResolvedMatchRules in BindMatchContext; leaving it unbound.", this);
            return;
        }

        matchRules = rules;
        gameModeId = modeId;
        // Missing lifecycle composition fails closed. The optional argument keeps older callers
        // source-compatible; scoring callers must explicitly supply live match eligibility.
        this.canAcceptMadeShot = canAcceptMadeShot;
        // Crossings observed before composition cannot authorize a make in the bound match.
        crossings.Clear();
        shotMade1 = false;
        shotMade2 = false;
        hasBoundMatchContext = true;
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

    private ShotScore updateShotMadeBasketBallStats(GameStats gameStats, BasketBallState basketBallState, float shotDistance, ResolvedMatchRules rules)
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
        //
        // AUD-010 Phase 1c: the marker is the exact reference captured at launch
        // (BasketBallState.CaptureShotMarkerForAttempt), not an id resolved back through
        // GameRules.BasketBallShotMarkersList - that resolution could return a different marker than
        // the one this shot was actually taken from if the list's contents or ordering ever changed.
        BasketBallShotMarker marker = basketBallState.OnShootShotMarker;

        ShotScoringInput input = new ShotScoringInput
        {
            Kind = ShotKindOf(basketBallState),
            IsMarkerContest = rules.IsThreePointContest
                || rules.IsFourPointContest
                || rules.IsSevenPointContest
                || rules.IsAllPointContest,
            // AUD-010 Phase 2b0: typed GameModeId identity replaces the raw MatchRuntime.RawModeId
            // comparison. Consecutive Shots is a distinct mode (ResolvedMatchRules.RequiresConsecutiveShots)
            // and is deliberately not treated as In the Pocket here - see docs/shot-lifecycle.md.
            ScoresByDistance = gameModeId == GameModeId.PointsByDistance,
            HasStreakBonus = gameModeId == GameModeId.InThePocket,
            ConsecutiveShotsMade = gameStats.Stats.ConsecutiveShotsMade,
            StreakBonusThreshold = CurrentInThePocketStreakBonusThreshold,
            OnEnabledMarker = basketBallState.PlayerOnMarkerOnShoot
                && marker != null
                && marker.MarkerEnabled,
            IsFinalMarkerAttempt = marker != null && marker.ShotAttempt == marker.MaxShotAttempt,
            MarkerFinalShotScoresDouble = rules.IsThreePointContest
                || rules.IsFourPointContest
                || rules.IsSevenPointContest,
            MoneyBallActive = basketBallState.MoneyBallEnabledOnShoot,
            ShotDistance = shotDistance
        };

        // AUD-065: scores the shot and updates the made-shot/streak state it depends on, in the one
        // order both require. Covered directly by Level5MatchStatsTests - see that file for why the
        // regression this guards against needs only MatchStats/a bool, not a running
        // GameRules/MatchRuntime.
        //
        // AUD-010 Phase 1c: wasTwoPointAttempt is captured here, before basketBallState.TwoAttempt is
        // cleared by ResetShotAttemptSnapshot() (called by shotMade() once this method returns) - it
        // must be the launch-time snapshot, not a value read after this shot resolves.
        bool wasTwoPointAttempt = basketBallState.TwoAttempt;
        ShotScore score = gameStats.Stats.ApplyMadeShot(wasTwoPointAttempt, input);

        gameStats.Stats.TotalPoints += score.Points;
        gameStats.Stats.MoneyBallMade += score.MoneyBallMade;

        // ==================== requires position markers logic ==============================
        if (basketBallState.PlayerOnMarkerOnShoot
            && (rules.RequiresShotMarkers3s || rules.RequiresShotMarkers4s || rules.RequiresShotMarkers7s))
        {
            if (marker == null)
            {
                // PlayerOnMarkerOnShoot is only ever set true alongside a captured marker (see
                // BasketBallState.CaptureShotMarkerForAttempt) - an ownership/composition bug, not a
                // reachable gameplay state. Never guess marker zero.
                Debug.LogError("BasketBallShotMade: PlayerOnMarkerOnShoot is true but OnShootShotMarker is null - skipping marker made-count update.");
            }
            // if money ball enabled
            else if (basketBallState.MoneyBallEnabledOnShoot)
            {
                marker.ShotMade = marker.MaxShotMade;
            }
            // no money ball, update current shot marker stats
            else
            {
                marker.ShotMade++;
            }
        }

        return score;
    }

    //public int ConsecutiveShotsMade { get => _consecutiveShotsMade; }
    // Compatibility diagnostics only. No production caller uses these flags to authorize a make;
    // assigning them cannot substitute for matching a runtime and its attempt generation.
    public bool ShotMade1 { get => shotMade1; set => shotMade1 = value; }
    public bool ShotMade2 { get => shotMade2; set => shotMade2 = value; }
}

