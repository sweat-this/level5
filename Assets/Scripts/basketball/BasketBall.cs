using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Utility;
using Random = UnityEngine.Random;
using Level5.Core;
using Level5.Core.Match;

public class BasketBall : MonoBehaviour, IBasketballRuntime
{
    SpriteRenderer spriteRenderer;
    Rigidbody rigidbody;
    AudioSource audioSource;

    /// <summary>
    /// Phase 1c seam: one place that reads the shooter into the shot pipeline's contract, so the
    /// call sites that need it build it the same way. Player↔basketball cycle-cut slice: now read
    /// once from <see cref="IShooterActor.ShooterAttributes"/> in <see cref="Start"/> rather than
    /// resolved from a <c>CharacterProfile</c> directly - <c>actor</c> owns that mapping now.
    ///
    /// Code review: resolved once in <see cref="Start"/> rather than recomputed on every read.
    /// The underlying actor is assigned once and never reassigned, so a computed property gained
    /// nothing but cost - and cost that mattered specifically on the missing-profile path, where
    /// <c>displayUiStats</c>'s twice-a-second <c>InvokeRepeating</c> would have re-logged the same
    /// warning forever instead of once at startup.
    /// </summary>
    private ShooterAttributes currentShooter;

    BasketBallState basketBallState;
    GameStats gameStats;
    Animator anim;
    IShooterActor actor;
    GameObject basketBallSprite;
    GameObject basketBallPosition;
    GameObject player;
    GameObject uiStatsBackground;
    GameObject dropShadow;

    /// <summary>AUD-013 runtime ownership, bound once by <see cref="SpawnCoordinator.GiveBall"/> via <see cref="BindOwner"/>, before <see cref="Start"/> runs.</summary>
    int participantId;
    bool isCpu;
    bool isPrimary;
    bool bound;

    /// <summary>
    /// AUD-010 Phase 1c: the no-active-Terrain drop-shadow fallback, bound once by
    /// <see cref="SpawnCoordinator.GiveBall"/> via <see cref="BindGroundHeightProvider"/>, before
    /// <see cref="Start"/> runs. Read live in <see cref="Update"/> - never cached - since the bound
    /// <c>GameLevelManager</c> updates its own value after spawning.
    /// </summary>
    IGroundHeightProvider groundHeightProvider;

    /// <summary>
    /// AUD-010 Phase 1c: live money-ball session state, bound once by
    /// <see cref="GameRules"/>'s own composition step, before <see cref="Start"/> runs. Never a
    /// mandatory <see cref="Start"/> dependency - a ball whose shot path never reaches a qualifying
    /// marker shot is valid with this left null; <see cref="BasketballShotPipeline"/> is what checks
    /// for a missing binding, at the point it would actually be used.
    /// </summary>
    IMoneyBallState moneyBallState;

    /// <summary>
    /// AUD-010 Phase 2b0: the rules this match is being played under, bound once by composition
    /// (<see cref="SpawnCoordinator.GiveBall"/>), before <see cref="Start"/> runs. Not serialized: it
    /// is runtime-only, set after the component already exists, and <see cref="ResolvedMatchRules"/>
    /// is not itself <c>[Serializable]</c>.
    /// </summary>
    private ResolvedMatchRules matchRules;

    Text scoreText;
    Text shootProfileText;

    float lastShotDistance;
    float maxBasketballSpeed = 0f;

    bool playHitRimSound;
    bool facingRight = true;

    public static BasketBall instance;

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

    // =========================================================== Start() ========================================================
    // Use this for initialization
    void Start()
    {
        if (!bound)
        {
            // Setting enabled = false here would not be enough: Unity still dispatches
            // OnCollisionEnter/OnTriggerEnter/etc. to a disabled component, and both fire
            // unconditionally on basketBallState/actor below. Deactivating the whole GameObject is
            // the only thing that actually quarantines it, and also stops the child GroundCheck's
            // own trigger handlers from running against this same unbound state.
            Debug.LogError($"BasketBall on '{gameObject.name}' reached Start() with no bound owner.", this);
            gameObject.SetActive(false);
            return;
        }

        if (groundHeightProvider == null)
        {
            // Same fail-closed shape as the missing-owner branch above: sibling collision/trigger
            // behavior on this GameObject assumes a fully composed basketball, so disabling only this
            // component is not enough.
            Debug.LogError($"BasketBall on '{gameObject.name}' reached Start() with no bound ground-height provider.", this);
            gameObject.SetActive(false);
            return;
        }

        if (matchRules == null)
        {
            // Same fail-closed shape as the missing-owner/missing-provider branches above.
            Debug.LogError($"BasketBall on '{gameObject.name}' reached Start() with no bound match rules.", this);
            gameObject.SetActive(false);
            return;
        }

        // player 1's ball owns the static. every consumer of BasketBall.instance means
        // "the local player's ball" - camera follow, the free-play stat save, the ui-stats
        // toggle - not "whichever ball happened to run Start() last".
        if (instance == null || isPrimary)
        {
            instance = this;
        }

        currentShooter = actor.ShooterAttributes;
        basketBallPosition = player.transform.Find("basketBall_position").gameObject;
        rigidbody = GetComponent<Rigidbody>();
        gameStats =  GetComponent<GameStats>();

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponentInChildren<Animator>();

        //basketball drop shadow
        dropShadow = transform.root.Find("drop shadow").gameObject;

        //objects
        basketBallSprite = GameObject.Find("basketball_sprite"); //used to reset drop shadow. on launch, euler position gets changed

        //bool flags
        basketBallState.Locked = false;
        basketBallState.CanPullBall = true;
        playHitRimSound = true;

        //todo: move to game manager
        //UiStatsEnabled = true;

        // cap ball speed
        maxBasketballSpeed = 25f;
        // check for ui stats ON/OFF. i know this is sloppy. its just a quick test
        if (GameObject.Find("ui_stats") != null)
        {
            shootProfileText = GameObject.Find("shooterProfileTextObject").GetComponent<Text>();
            scoreText = GameObject.Find("shootStatsTextObject").GetComponent<Text>();
            uiStatsBackground = GameObject.Find("textBackground");

            if (UiStatsEnabled)
            {
                updateScoreText();
                BasketballShotPipeline.UpdateShooterProfileText(shootProfileText, currentShooter);
                uiStatsBackground.SetActive(true);
            }
            else
            {
                scoreText.text = "";
                shootProfileText.text = "";
                uiStatsBackground.SetActive(false);
            }
        }
        InvokeRepeating("CheckIsBallFacingGoal", 0, 0.5f);
        InvokeRepeating("displayUiStats", 0, 0.5f);

        if (matchRules.EnemiesOnly || matchRules.IsBattleRoyal)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y + 20, transform.position.z);
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            dropShadow.SetActive(false);
        }
    }

    // =========================================================== Update() ========================================================

    // Update is called once per frame
    private void Update()
    {
        // get speed for basketball animation
        if (!matchRules.EnemiesOnly)
        {
            if (rigidbody.linearVelocity.magnitude > maxBasketballSpeed && !basketBallState.InAir)
            {
                rigidbody.linearVelocity = rigidbody.linearVelocity.normalized * maxBasketballSpeed;
            }
            // drop shadow lock to bball transform on the ground
            float shadowHeight = ResolveDropShadowHeight();
            dropShadow.transform.position = new Vector3(transform.root.position.x, shadowHeight, transform.root.position.z);

            // change this to reduce opacity
            if (!actor.HasBasketball)
            {
                SetBallVisible(true);
                dropShadow.SetActive(true);
                basketBallState.CanPullBall = true;
                basketBallSprite.transform.rotation = Quaternion.Euler(13.6f, 0, transform.root.position.z);
            }
            //if player has ball and hasnt shot
            if (actor.HasBasketball
                && !actor.InDunkState)//&& !basketBallState.Thrown)
            {
                basketBallState.CanPullBall = false;
                SetBallVisible(false);
                dropShadow.SetActive(false);
                actor.SetAnimBool("hasBasketball", true);
                //playerState.setPlayerAnim("walking", false);
                actor.SetAnimBool("moonwalking", false);

                // move basketball to launch position and disable sprite
                transform.position = new Vector3(basketBallPosition.transform.position.x,
                    basketBallPosition.transform.position.y,
                    basketBallPosition.transform.position.z);
            }
        }
    }

    /// <summary>
    /// The drop shadow's Y position: an active Terrain's own sampled height where one exists, else
    /// the bound <see cref="groundHeightProvider"/>'s current value.
    /// AUD-052: guarded like PlayerController - no active Terrain otherwise NREs per frame.
    /// AUD-010 Phase 1c: <see cref="IGroundHeightProvider.GroundHeight"/> is read here, at the point of
    /// use, every call - never cached - since the bound <c>GameLevelManager</c> updates its own value
    /// after spawning.
    /// </summary>
    private float ResolveDropShadowHeight()
    {
        return Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f
            : groundHeightProvider.GroundHeight + 0.02f;
    }

    /// <summary>
    /// Shows or hides the ball itself.
    ///
    /// This used to be done by tinting <c>spriteRenderer.color</c> to alpha 0. A play-mode trace
    /// confirmed the tint is applied - the ball sits exactly on the owner's hold point with
    /// alpha 0.00 - and the material is a correctly configured transparent one
    /// (<c>_SURFACE_TYPE_TRANSPARENT</c>, SrcAlpha/OneMinusSrcAlpha), yet the ball is still drawn
    /// above the player's head. The sprite is rendered with
    /// <c>Universal Render Pipeline/Particles/Unlit</c>, a particle shader rather than a sprite
    /// shader, and it does not honour the SpriteRenderer's per-renderer colour the way
    /// Sprites-Default does.
    ///
    /// Toggling the renderer says exactly what is meant and does not depend on the shader
    /// respecting vertex colour. The tint is kept so anything that does read the colour still sees
    /// the old values.
    /// </summary>
    private void SetBallVisible(bool visible)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.enabled = visible;
        spriteRenderer.color = new Color(1f, 1f, 1f, visible ? 1f : 0f);
    }

    public void CheckIsBallFacingGoal()
    {
        anim.SetFloat("speed", rigidbody.linearVelocity.sqrMagnitude);
        if (rigidbody.linearVelocity.x > 0 && !facingRight)
        {
            Flip();
        }
        if (rigidbody.linearVelocity.x < 0f && facingRight)
        {
            Flip();
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }

    // =========================================================== Collisions ========================================================

    private void OnCollisionEnter(Collision other)
    {
        // collision : basketball + rim
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("basketballrim")
            && playHitRimSound
            && !actor.HasBasketball)
        {
            playHitRimSound = false;
            audioSource.PlayOneShot(SFXBB.instance.basketballHitRim);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
        // collision : basketball + ground
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("ground")
            && !actor.HasBasketball)
        {
            basketBallState.CanPullBall = true;
            //reset rotation
            transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));
            dropShadow.transform.rotation = Quaternion.Euler(90, 0, 0);
            basketBallState.Locked = false;
            basketBallState.Thrown = false;
            audioSource.PlayOneShot(SFXBB.instance.basketballBounce);
        }
        // collision : basketball + fence
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("fence")
            && !actor.HasBasketball)
        {
            audioSource.PlayOneShot(SFXBB.instance.basketballHitFence);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("basketballrim") && !playHitRimSound)
        {
            playHitRimSound = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // if basketball enters player hitbox
        if (gameObject.CompareTag("basketball")
            && other.gameObject.CompareTag("playerHitbox")
            && !basketBallState.Thrown)
        {
            actor.HasBasketball = true;
            basketBallState.Thrown = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // if basketball exits player hitbox
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("playerHitbox") &&
            basketBallState.Thrown)
        {
            basketBallState.Thrown = true;
        }
    }

    // =================================== shoot ball function =======================================

    public void shootBasketBall(bool two, bool three, bool four, bool seven)
    {

        // set side or front shooting animation
        if (actor.FacingFront) // facing straight toward bball goal
        {
            actor.SetAnimTrigger("basketballShootFront");
        }
        else // side of goal, relative postion
        {
            actor.SetAnimTrigger("basketballShoot");
        }

        // reset ball rotation
        // #NOTE : hopefully this check works for issue : ball is hot but doesnt go toward goal
        transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));

        //// check for and set money ball
        //if (GameRules.instance.MoneyBallEnabled)
        //{
        //    basketBallState.MoneyBallEnabledOnShoot = true;
        //    PlayerStats.instance.Money -= 5; // moneyball spent
        //    BasketBallStats.MoneyBallAttempts++;
        //    GameRules.instance.MoneyBallEnabled = false;
        //}
        //else
        //{
        //    basketBallState.MoneyBallEnabledOnShoot = false;
        //}


        //// let basketball rim know current statistics of made/attempt for every shot
        //// this is more determining consecutive shots
        //// on make, if made = made+1 && attempt = attempt +1 ---> consecutive++
        //BasketBallShotMade.instance.setCurrentShotsMadeAttempted((int)basketBallStats.ShotMade, (int)basketBallStats.ShotAttempt);

        // let basketball state know what type of shot is attempted
        updateBasketBallStateShotTypeOnShoot(two, three, four, seven);

        // player on shot marker and game mode requires markers
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(this, moneyBallState);
        //calculate shot distance 
        Vector3 tempPos = new Vector3(
            basketBallState.BasketBallTarget.transform.position.x,
            0, 
            basketBallState.BasketBallTarget.transform.position.z);

        float tempDist = Vector3.Distance(tempPos, basketBallPosition.transform.position);
        lastShotDistance = tempDist;

        // wait for shot meter to finish calculations for accurate launch values
        StartCoroutine(LaunchBasketBall());

        //reset state flags
        basketBallState.Thrown = true;
        actor.LockCallBallToPlayer(false);
    }

    public void updateBasketBallStateShotTypeOnShoot(bool two, bool three, bool four, bool seven)
    {
        //Debug.Log("*********************************************** 2 : " + two);
        //Debug.Log("*********************************************** 3 : " + three);
        //Debug.Log("*********************************************** 4 : " + four);
        //Debug.Log("*********************************************** 7 : " + seven);
        // Clear stale shot snapshot data from a previous miss before setting the new attempt.
        basketBallState.ResetShotAttemptSnapshot();

        // identify is in 2 or 3 point range for stat counters
        if (two && !three)
        {
            basketBallState.TwoAttempt = true;
            gameStats.Stats.TwoPointerAttempts++;
            gameStats.Stats.ShotAttempt++;
        }
        if (three && !four)
        {
            basketBallState.ThreeAttempt = true;
            gameStats.Stats.ThreePointerAttempts++;
            gameStats.Stats.ShotAttempt++;
        }
        if (four && !three)
        {
            basketBallState.FourAttempt = true;
            gameStats.Stats.FourPointerAttempts++;
            gameStats.Stats.ShotAttempt++;
        }
        if (seven)
        {
            basketBallState.SevenAttempt = true;
            gameStats.Stats.SevenPointerAttempts++;
            gameStats.Stats.ShotAttempt++;
        }
        //GameRules.instance.updatePlayerScore();
    }

    // =================================== Launch ball function =======================================
    void Launch(GameObject ballPositionAtLaunch)
    {
        BasketballShotPipeline.LaunchComputation computation = BasketballShotPipeline.ComputeLaunch(
            transform,
            ballPositionAtLaunch.transform.position,
            basketBallState.BasketBallTarget.transform.position,
            currentShooter,
            basketBallState,
            gameStats,
            LastShotDistance,
            actor.ShotMeterSliderValue);

        if (computation.IsSwish && BehaviorNpcCritical.instance != null && !isCpu)
        {
            BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
        }

        actor.DisplayShotMeterMessage(computation.ShotMeterMessage);

        // launch the object by setting its initial velocity and flipping its state
        rigidbody.linearVelocity = computation.GlobalVelocity;
        actor.HasBasketball = false;
        actor.SetAnimBool("hasBasketball", false);
        // Symmetric with BasketBallAuto.Launch's CPU-2 call - a no-op on the human implementation.
        actor.EndShootCycle();

        // analytics
        AnaylticsManager.PlayerShoot(actor.ShotMeterSliderValue);
    }

    // ============================ Functions and Properties ==========================================

    // wair for shotmeter value calculation, launch ball
    IEnumerator LaunchBasketBall()
    {
        // get position of ball when shot
        GameObject currentBallPosition = player.transform.Find("basketBall_position").gameObject;
        // wait for shot meter to finish
        yield return new WaitUntil(() => actor.ShotMeterEnded == false);
        //launch ball to goal      
        Launch(currentBallPosition);
    }

    // ========================== ui display ===============================

    public bool displayUiStats()
    {
        // the overlay is one shared Text object in the scene, so every ball writing to it
        // would just fight over it. only the primary ball drives it.
        if (instance != this)
        {
            return false;
        }

        if (scoreText == null || shootProfileText == null || uiStatsBackground == null)
        {
            return false;
        }

        if (UiStatsEnabled)
        {
            updateScoreText();
            BasketballShotPipeline.UpdateShooterProfileText(shootProfileText, currentShooter);
            uiStatsBackground.SetActive(true);
            return true;
        }
        else
        {
            scoreText.text = "";
            shootProfileText.text = "";
            uiStatsBackground.SetActive(false);
            return false;
        }
    }

    public void toggleUiStats()
    {
        UiStatsEnabled = !UiStatsEnabled;
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "ui stats = " + UiStatsEnabled;

        // turn off text display after 5 seconds
        StartCoroutine(turnOffMessageLogDisplayAfterSeconds(3));
    }

    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "";
    }

    public void updateScoreText()
    {
        // reads this ball's own gameStats. it used to reassign the field to players[0]'s stats
        // here, which also redirected every shot counter that writes through the same field -
        // a second human player's attempts were recorded onto player 1. the overlay is a single
        // shared Text object, so only the primary ball writes to it (see displayUiStats).
        BasketballShotPipeline.UpdateScoreText(scoreText, gameStats, lastShotDistance);
    }

    // ============================= getters/ setters ======================================

    public float LastShotDistance { get => lastShotDistance; set => lastShotDistance = value; }
    public GameStats GameStats => gameStats;
    public BasketBallState BasketBallState => basketBallState;
    public bool UiStatsEnabled { get; private set; }
    public GameObject BasketBallPosition { get => basketBallPosition; set => basketBallPosition = value; }
    public Rigidbody Rigidbody { get => rigidbody; set => rigidbody = value; }

    // ======================= IBasketballRuntime (AUD-013) =======================

    public int ParticipantId => participantId;
    public bool IsCpu => isCpu;
    public bool IsPrimary => isPrimary;
    public GameObject OwnerActor => player;
    IShooterActor IBasketballRuntime.Actor => actor;
    BasketBallState IBasketballRuntime.State => basketBallState;
    GameStats IBasketballRuntime.Stats => gameStats;

    public void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor)
    {
        if (bound)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' is already bound; ignoring a second BindOwner call.", this);
            return;
        }

        this.participantId = participantId;
        this.isCpu = isCpu;
        this.isPrimary = isPrimary;
        player = ownerActor;
        this.actor = actor;
        bound = true;

        basketBallState = GetComponent<BasketBallState>();
        basketBallState.BindOwner(isCpu, ownerActor);
    }

    // ======================= IGroundHeightProvider binding (AUD-010 Phase 1c) =======================

    /// <summary>
    /// Explicit ground-height binding from <see cref="SpawnCoordinator.GiveBall"/>, called once
    /// immediately after <see cref="BindOwner"/> and before Unity calls <see cref="Start"/>.
    /// Ownership-only - no visual or physics side effects, and the reference is never read here.
    /// </summary>
    public void BindGroundHeightProvider(IGroundHeightProvider provider)
    {
        if (provider == null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' was bound with a null ground-height provider.", this);
            return;
        }

        if (groundHeightProvider != null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' already has a bound ground-height provider; ignoring a second BindGroundHeightProvider call.", this);
            return;
        }

        groundHeightProvider = provider;
    }

    // ======================= IMoneyBallState binding (AUD-010 Phase 1c) =======================

    /// <summary>
    /// Explicit money-ball-state binding from <see cref="GameRules"/>'s own composition step, called
    /// once immediately after it resolves this participant's spawned ball. Ownership-only - no
    /// gameplay side effects, and the reference is never read here.
    /// </summary>
    public void BindMoneyBallState(IMoneyBallState state)
    {
        if (state == null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' was bound with a null money-ball state provider.", this);
            return;
        }

        if (moneyBallState != null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' already has a bound money-ball state provider; ignoring a second BindMoneyBallState call.", this);
            return;
        }

        moneyBallState = state;
    }

    // ======================= Match rules binding (AUD-010 Phase 2b0) =======================

    /// <summary>
    /// Explicit match-rules binding from the same composition operation that spawns the ball
    /// (<see cref="SpawnCoordinator.GiveBall"/>), so <see cref="Start"/>/<see cref="Update"/> no
    /// longer read <c>MatchRuntime.Rules</c> directly. Mirrors <see cref="BasketBallState"/>'s and
    /// <see cref="BasketBallAuto"/>'s own <c>BindMatchRules</c> bind-once/null-guard/no-rebind shape.
    /// </summary>
    public void BindMatchRules(ResolvedMatchRules rules)
    {
        // Checked before the null-argument branch below: a null second call after a real bind
        // already succeeded must report "already bound", not "remaining unbound" - matchRules is
        // still the original valid reference either way, and the log should say so.
        if (matchRules != null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' already has bound match rules; ignoring a second BindMatchRules call.", this);
            return;
        }

        if (rules == null)
        {
            Debug.LogError($"BasketBall on '{gameObject.name}' was bound with null match rules; remaining unbound.", this);
            return;
        }

        matchRules = rules;
    }
}
