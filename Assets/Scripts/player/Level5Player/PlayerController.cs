using Assets.Scripts.Utility;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

public class PlayerController : MonoBehaviour, IShooterActor, IPlayerDamageReactionHost, IPlayerDunkHost
{
    [SerializeField]
    bool isPlayer1;
    [SerializeField]
    bool isPlayer2;
    [SerializeField]
    bool isPlayer3;
    [SerializeField]
    bool isPlayer4;
    [SerializeField] public bool isShrunk;
    // components
    // AUD-002: internal rather than private for other same-assembly helpers extracted alongside this
    // controller.
    //
    // AUD-012 Phase 2b Slice 32: PlayerDamageReactions no longer needs this internal access - it moved
    // into Level5.Player and now reaches controller state through IPlayerDamageReactionHost's public
    // members instead.
    //
    // AUD-012 Phase 2b Slice 38: this controller moved into Level5.Player too. These fields stay
    // internal, now visible within that assembly and to Assembly-CSharp only through the existing
    // InternalsVisibleTo("Assembly-CSharp") bridge in AssemblyInfo.cs.
    internal Animator anim;
    private AnimatorStateInfo currentStateInfo;
    private GameObject dropShadow;
    internal Rigidbody rigidBody;
    private CharacterProfile characterProfile;
    [SerializeField]
    private BasketBall basketball;
    private ShotMeter shotmeter;
    private PlayerSwapAttack playerSwapAttack;
    internal PlayerHealth playerHealth;
    private CallBallToPlayer callBallToPlayer;
    private PlayerAttackQueue playerAttackQueue;
    private PlayerDunk playerDunk;

    // AUD-002: the damage/knockdown/lightning/shrink reaction coroutines live here now - see
    // PlayerDamageReactions. A plain object, not a component: it runs under this MonoBehaviour's
    // own StartCoroutine exactly as before, so no prefab, lifecycle, or GetComponent wiring changes.
    //
    // AUD-012 Phase 2b Slice 32: PlayerDamageReactions now lives in Level5.Player and reaches this
    // controller only through the IPlayerDamageReactionHost contract this class implements below -
    // it is no longer a same-assembly helper.
    //
    // AUD-012 Phase 2b Slice 38: this controller moved into Level5.Player too, so it and
    // PlayerDamageReactions are same-assembly again - but the host-contract boundary above is
    // retained unchanged; this was a pure ownership move, not a reason to re-wire it.
    private readonly PlayerDamageReactions damageReactions;

    /// <summary>
    /// AUD-012 Phase 2b Slice 32: the shrink reaction's camera lookup, composed live from
    /// <c>GameLevelManager.Awake</c> (via <c>SpawnCoordinator.BindHumanDamageReactionCamera</c> -&gt;
    /// <see cref="BindDamageReactionCameraReader"/>) rather than this controller or
    /// <c>PlayerDamageReactions</c> reading <c>CameraManager</c> directly. A <c>Func&lt;Camera&gt;</c>
    /// rather than a captured <c>Camera</c>, so the reaction resolves whatever camera composition
    /// currently answers at the moment shrink begins, matching the old direct-read's timing. Stored
    /// independently of <see cref="damageReactions"/>, which may be constructed before this is ever
    /// bound - an unbound reader is safe, not an error; see <see cref="IPlayerDamageReactionHost.GetShrinkCamera"/>.
    /// </summary>
    private Func<Camera> damageReactionCameraReader;

    /// <summary>
    /// AUD-012 Phase 2b Slice 33: the idle-sniper runtime's live resolver, composed from
    /// <c>GameLevelManager.ReadPlayerIdleSniperRuntime</c> (via
    /// <c>SpawnCoordinator.BindHumanIdleSniperRuntime</c> -&gt; <see cref="BindIdleSniperRuntimeReader"/>)
    /// rather than this controller reading <c>SniperManager.instance</c> directly. Resolved at the
    /// point of use in <see cref="checkIdleTimeForSniper"/>, never cached - matching the old direct
    /// read's timing, since an unbound resolver (or one currently answering null) is equivalent to no
    /// sniper runtime existing.
    /// </summary>
    private Func<IPlayerIdleSniperRuntime> idleSniperRuntimeReader;

    /// <summary>
    /// AUD-012 Phase 2b Slice 37: the live match-runtime boundary this controller now composes for its
    /// remaining <c>Assembly-CSharp</c> dependency, from <c>SpawnCoordinator.BindHumanMatchRuntime</c>
    /// during <c>GameLevelManager.Awake</c>'s spawn pass - human participants only, before this
    /// controller's own <see cref="Start"/> can run. Replaces this controller's former direct
    /// <c>MatchRuntime.Rules</c>/<c>CustomCamera</c>/<c>LocalInputSlotFor</c> reads. Read at the point of
    /// use everywhere it is dereferenced below, never cached into a snapshot: <c>GameLevelManager</c>'s
    /// explicit <see cref="IPlayerMatchRuntime"/> implementation forwards live to <c>MatchRuntime</c> on
    /// every call, and this controller must keep observing whatever that currently answers - see
    /// <see cref="InitializeInput"/>'s guard for what happens when a human never receives one.
    /// </summary>
    private IPlayerMatchRuntime matchRuntime;

    public PlayerController()
    {
        damageReactions = new PlayerDamageReactions(this);
    }

    /// <summary>
    /// Explicit binding of the live idle-sniper runtime resolver, from
    /// <c>SpawnCoordinator.BindHumanIdleSniperRuntime</c> during <c>GameLevelManager.Awake</c>'s spawn
    /// pass - human participants only. No sniper runtime needs to exist yet when this is called; see
    /// <see cref="idleSniperRuntimeReader"/>.
    /// </summary>
    public void BindIdleSniperRuntimeReader(Func<IPlayerIdleSniperRuntime> reader)
    {
        idleSniperRuntimeReader = reader;
    }

    /// <summary>
    /// Explicit binding of the live shrink-camera resolver, from
    /// <c>SpawnCoordinator.BindHumanDamageReactionCamera</c> during <c>GameLevelManager.Awake</c>'s
    /// spawn pass - human participants only; CPU shrink (if any) does not take this. Safe to call
    /// before or after <see cref="damageReactions"/> already exists: the resolver is only invoked when
    /// the shrink reaction actually asks for a camera, through <see cref="IPlayerDamageReactionHost.GetShrinkCamera"/>.
    /// </summary>
    public void BindDamageReactionCameraReader(Func<Camera> reader)
    {
        damageReactionCameraReader = reader;
    }

    /// <summary>
    /// Explicit binding of the live match-runtime boundary, from
    /// <c>SpawnCoordinator.BindHumanMatchRuntime</c> during <c>GameLevelManager.Awake</c>'s spawn
    /// pass - human participants only. Required before <see cref="InitializeInput"/> can resolve a local
    /// input slot; see that method's guard.
    /// </summary>
    public void BindMatchRuntime(IPlayerMatchRuntime runtime)
    {
        matchRuntime = runtime;
    }


    // walk speed #review can potentially remove
    [SerializeField]
    private float movementSpeed;
    [SerializeField]
    private float inAirSpeed; // leave serialized
    [SerializeField]
    private float blockSpeed; // leave serialized
    //[SerializeField]
    //private float attackSpeed; // leave serialized

    // get/set for following at bottom of class
    private bool _facingRight;
    private bool _facingFront;
    private bool _locked;
    [SerializeField]
    private bool _inAir;
    private bool _grounded;
    private bool _knockedDown;
    private bool _takeDamage;
    private bool _avoidedKnockDown;
    private bool _disintegrated;
    private bool canAttack;
    private bool canBlock;

    // player state bools
    //private bool running = false;
    private bool runningToggle;
    public bool hasBasketball;

    // trigger player jump. bool used because activated in fixed update
    // to ensure animaion is synced with camera. camera is updated in fixed update 
    // as well
    private bool jumpTrigger = false;
    private bool dunkTrigger;

    float bballRelativePositioning; // which side of the player the ball is on
    [SerializeField]
    float playerDistanceFromRim; // player distance from rim
    [SerializeField]
    float playerDistanceFromRimFeet; // player distance from rim

    Vector3 playerRelativePositioning;
    Vector3 bballRimVector;

    // AUD-012 Phase 2b Slice 21: live ground-height context for the no-Terrain drop-shadow fallback
    // below, bound explicitly through BindArenaContext - see that method's remarks.
    private IGroundHeightProvider groundHeightProvider;
    private bool groundHeightProviderMissingLogged;

    // customizable options
    [SerializeField]
    private bool playerCanBlock;
    [SerializeField]
    private bool playerCanAttack;
    [SerializeField]
    float _knockDownTime;
    [SerializeField]
    float _takeDamageTime;

    // movement variables
    Vector3 movement;
    [SerializeField]
    float movementHorizontal;
    [SerializeField]
    float movementVertical;

    float screenXRange;
    float screenYRange;

    // player take damage display
    Text damageDisplayValueText;
    GameObject damageDisplayObject;
    const string damageDisplayValueName = "player_damage_display_text";

    //player sprite object
    [SerializeField]
    GameObject spriteObject;

    // control movement speed based on state
    public int currentState;
    public int idleState;
    public int walkState;
    public int run;
    public int bWalk;
    public int bIdle;
    public int knockedDownState;
    public int takeDamageState;
    public int specialState;
    public int attackState;
    public int blockState;
    public int inAirDunkState;
    public int inAirHasBasketballFrontState;
    public int inAirHasBasketballSideState;
    public int inAirShootState;
    public int inAirShootFrontState;
    public int jumpState;
    public int inAirHasBasketball;
    public int disintegratedState;
    public int dunkState;
    public int lightningState;

    PlayerControls controls;
    private PlayerInputReader inputReader;

    /// <summary>
    /// AUD-012 Phase 2b Slice 26: the legacy mobile joystick axes source composed for this human
    /// participant, held here rather than only on <see cref="inputReader"/> because that reader is
    /// destroyed and rebuilt several times over a controller's life - <see cref="OnDisable"/> clears
    /// it, <see cref="OnEnable"/>/<see cref="TryEnsureInputReader"/> rebuild it after gameplay
    /// controls are released and reacquired, and the <see cref="Controls"/> setter replaces it
    /// outright. Every one of those paths reconstructs the reader from this field, so a
    /// disable/re-enable cycle cannot silently lose mobile movement.
    /// </summary>
    private Func<Vector2> legacyTouchMovementReader;
    private float terrainYHeight;
    private int inputPlayerId = -1;
    private bool hasStarted;

    /// <summary>
    /// AUD-012 Phase 2b Slice 37: set only by <see cref="InitializeInput"/>'s new match-runtime guard,
    /// and checked once by <see cref="Start"/> immediately after calling it - narrowly so, since
    /// disabling this <c>MonoBehaviour</c> inside <see cref="InitializeInput"/> does not itself abort the
    /// rest of <see cref="Start"/>, which would otherwise dereference the still-null
    /// <see cref="matchRuntime"/>. Left <c>false</c> for every other <see cref="InitializeInput"/> exit
    /// (the pre-existing CPU-misuse and invalid-input-slot guards), which keep their existing behaviour
    /// of leaving <see cref="Start"/> to continue past them unchanged.
    ///
    /// That is a deliberate, spec-directed asymmetry, not an oversight: unlike those two pre-existing
    /// guards - which still fully populate <c>anim</c>/<c>playerHealth</c>/<c>Shotmeter</c>/
    /// <c>basketball</c>/<c>characterProfile</c>/etc. before leaving the disabled component behind - this
    /// guard leaves every one of them unset. Do not assume a <c>PlayerController</c> found in the scene is
    /// fully initialized just because it exists; check <see cref="enabled"/> (or, for this specific
    /// failure, that <see cref="matchRuntime"/> is bound) first.
    /// </summary>
    private bool matchRuntimeRequiredButMissing;
    [SerializeField] private float idleTime;
    [SerializeField] private float idleStartTime;
    private bool isLocked;

    private void OnEnable()
    {
        if (hasStarted)
        {
            InitializeInput();
        }
    }
    private void OnDisable()
    {
        if (inputPlayerId >= 0)
        {
            PlayerControlsProvider.ReleaseGameplayControls(inputPlayerId);
            inputPlayerId = -1;
        }

        controls = null;
        inputReader = null;
    }

    private void Awake()
    {
    }

    private void InitializeInput()
    {
        matchRuntimeRequiredButMissing = false;

        IPlayerControllerParticipantState participant = GetComponent<IPlayerControllerParticipantState>();
        int playerId = participant != null ? participant.PlayerId : 0;

        if (participant != null && participant.IsCpu)
        {
            // This guard intentionally does not set matchRuntimeRequiredButMissing, so Start() still
            // runs to completion for this pre-existing composition defect - unchanged from before this
            // slice. A CPU never receives BindMatchRuntime (SpawnCoordinator skips CPUs), so matchRuntime
            // stays null here, and Start()'s later matchRuntime reads are therefore only safe because
            // Start() already dereferences GetComponent<IPlayerControllerParticipantState>().
            // BasketballObject first (null for a CPU - RegisterCpu never sets the human `basketball`
            // field) and throws there before ever reaching a matchRuntime read. That masking is
            // incidental, not structural: a future change to that earlier lookup should re-verify this
            // path still fails before Start() reaches matchRuntime.
            Debug.LogError("PlayerController cannot own input for a CPU player. Use AutoPlayerController.", this);
            enabled = false;
            return;
        }

        if (matchRuntime == null)
        {
            Debug.LogError(
                "PlayerController has no bound IPlayerMatchRuntime - SpawnCoordinator.BindHumanMatchRuntime "
                + "must run before this controller's Start().", this);
            enabled = false;
            matchRuntimeRequiredButMissing = true;
            return;
        }

        inputPlayerId = matchRuntime.LocalInputSlotFor(playerId);
        if (inputPlayerId < 0)
        {
            Debug.LogError("PlayerController could not resolve a human input slot.", this);
            enabled = false;
            return;
        }

        controls = PlayerControlsProvider.AcquireGameplayControls(inputPlayerId);
        inputReader = new PlayerInputReader(controls, ReadLegacyTouchMovement);
    }

    private bool TryEnsureInputReader(out PlayerInputReader reader)
    {
        if (inputReader != null)
        {
            reader = inputReader;
            return true;
        }

        if (controls == null)
        {
            if (inputPlayerId < 0)
            {
                InitializeInput();
            }
            else
            {
                controls = PlayerControlsProvider.AcquireGameplayControls(inputPlayerId);
            }
        }

        if (controls == null || !enabled)
        {
            reader = null;
            return false;
        }

        inputReader = new PlayerInputReader(controls, ReadLegacyTouchMovement);
        reader = inputReader;
        return true;
    }
    void Start()
    {
        hasStarted = true;
        InitializeInput();
        if (matchRuntimeRequiredButMissing)
        {
            return;
        }

        getAnimatorStateHashes();
        playerDunk = GetComponent<PlayerDunk>();
        callBallToPlayer = GetComponent<CallBallToPlayer>();
        playerAttackQueue = GetComponent<PlayerAttackQueue>();
        spriteObject = transform.GetComponentInChildren<SpriteRenderer>().gameObject;
        damageDisplayObject = SceneObjects.Find(damageDisplayValueName, this);
        damageDisplayValueText = damageDisplayObject != null
            ? damageDisplayObject.GetComponent<Text>()
            : null;
        anim = GetComponentInChildren<Animator>();
        basketball = GetComponent<IPlayerControllerParticipantState>().BasketballObject.GetComponent<BasketBall>();
        characterProfile = GetComponent<CharacterProfile>();
        rigidBody = GetComponent<Rigidbody>();
        Shotmeter = GetComponentInChildren<ShotMeter>();
        PlayerHealth = GetComponentInChildren<PlayerHealth>();

        // AUD-081: same unguarded-lookup shape AUD-079 fixed in RacingVehicleController - this
        // used to dereference Find's result directly, so a player root without a "drop_shadow"
        // child threw here and aborted the rest of Start().
        Transform dropShadowTransform = transform.root.transform.Find("drop_shadow");
        if (dropShadowTransform == null)
        {
            Debug.LogError("PlayerController on " + name + " found no 'drop_shadow' child on its root.", this);
        }
        else
        {
            dropShadow = dropShadowTransform.gameObject;
        }

        FacingRight = true;

        movementSpeed = characterProfile.Speed;
        runningToggle = true;

        if (_knockDownTime == 0) { _knockDownTime = 1.5f; }
        if (_takeDamageTime == 0) { _takeDamageTime = 0.5f; }
        if (blockSpeed == 0) { blockSpeed = 0.2f; }
        //if (attackSpeed == 0) { attackSpeed = 0f; }
        //inAirSpeed = 0;

        screenXRange = Screen.width / 10;
        screenYRange = Screen.height / 10;

        if (matchRuntime.CustomCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            if (damageDisplayObject != null)
            {
                damageDisplayObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
        //GameOptions.sniperEnabled = true; // test flag;
        if (matchRuntime.Rules.EnemiesEnabled || matchRuntime.Rules.EnemiesOnly || matchRuntime.Rules.SniperEnabled)
        {
            if (GetComponent<PlayerSwapAttack>() != null)
            {
                playerSwapAttack = GetComponent<PlayerSwapAttack>();
            }
            if (damageDisplayObject != null && damageDisplayObject.GetComponent<Canvas>() != null)
            {
                damageDisplayObject.GetComponent<Canvas>().worldCamera = Camera.main;
            }
        }
        else if (damageDisplayObject != null)
        {
            damageDisplayObject.SetActive(false);
        }

        // custom knockdown time for sniper mode
        if (matchRuntime.Rules.SniperEnabled)
        {
            _knockDownTime = 0.75f;
        }
    }

    // ==================== Arena context composition (AUD-012 Phase 2b Slice 21) ====================

    /// <summary>
    /// Explicit arena-context binding from <c>SpawnCoordinator.BindHumanArenaContext</c>,
    /// called once from <c>GameLevelManager.Start()</c> after arena bootstrap has resolved the final
    /// basketball rim and updated the live ground-height state - never during spawn/registration
    /// (<c>SpawnCoordinator.RegisterHuman</c>), when the rim is not yet ready. Replaces this
    /// controller's former direct <c>GameLevelManager.instance.BasketballRimVector</c>/
    /// <c>TerrainHeight</c> reads.
    ///
    /// <paramref name="basketballRimVector"/> is stored as a value, matching how this controller
    /// already treated it as a cached snapshot rather than a live read. <paramref
    /// name="groundHeightProvider"/> is stored, not read here - it must stay live, since
    /// <c>GameLevelManager</c> keeps updating its ground height after this call - see
    /// <see cref="ResolveDropShadowHeight"/>.
    /// </summary>
    public void BindArenaContext(Vector3 basketballRimVector, IGroundHeightProvider groundHeightProvider)
    {
        bballRimVector = basketballRimVector;
        this.groundHeightProvider = groundHeightProvider;
    }

    // ============ Legacy mobile movement composition (AUD-012 Phase 2b Slice 26) ============

    /// <summary>
    /// Explicit binding of the scene's legacy mobile joystick axes, from
    /// <c>SpawnCoordinator.BindHumanLegacyTouchMovement</c> during
    /// <c>GameLevelManager.Awake</c>'s spawn pass - human participants only; CPUs never read player
    /// input. Replaces <c>PlayerInputReader</c>'s former direct
    /// <c>GameLevelManager.instance.Joystick</c> read, which was that class's last edge into
    /// <c>Assembly-CSharp</c>. This controller does not discover <c>GameLevelManager</c> or
    /// <c>FloatingJoystick</c> itself; it only forwards whatever composition supplied.
    ///
    /// Safe to call before or after an input reader exists: readers are handed
    /// <see cref="ReadLegacyTouchMovement"/>, which resolves this field at call time.
    /// </summary>
    public void BindLegacyTouchMovementReader(Func<Vector2> reader)
    {
        legacyTouchMovementReader = reader;
    }

    /// <summary>
    /// The indirection every <see cref="PlayerInputReader"/> this controller builds is given, so the
    /// reader reads the bound source live rather than capturing whatever was bound at its own
    /// construction time. Zero when nothing was composed - which is every non-mobile run, since the
    /// reader only invokes the legacy fallback under the mobile preprocessor conditions.
    /// </summary>
    private Vector2 ReadLegacyTouchMovement()
    {
        return legacyTouchMovementReader != null ? legacyTouchMovementReader.Invoke() : Vector2.zero;
    }

    // not affected by framerate
    void FixedUpdate()
    {
        //------MOVEMENT---------------------------
        if (!KnockedDown && !Locked
            && currentState != takeDamageState)
        {
            if (!TryEnsureInputReader(out PlayerInputReader reader))
            {
                return;
            }

            Vector2 moveInput = reader.ReadMove(screenXRange, screenYRange);
            movementHorizontal = moveInput.x;
            movementVertical = moveInput.y;
            movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.fixedDeltaTime);

            // check jump trigger and execute jump
            if (jumpTrigger)
            {
                jumpTrigger = false;
                PlayerJump();
            }
            if (dunkTrigger
                && currentState != inAirDunkState
                && !InAir
                && Grounded
                && !Locked)
            {
                dunkTrigger = false;
                PlayerDunk.playerDunk();
            }

            if (currentState != specialState)
            {
                ApplyHorizontalMovement();
                //isWalking(movement);
                IsWalking(movementHorizontal, movementVertical);
            }
        }
    }


    // Update :: once once per frame
    void Update()
    {

        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // check if player has been idle long enough to get shot at
        checkIdleTimeForSniper();

        // knocked down
        if (KnockedDown && !Locked)
        {
            Locked = true;
            StartCoroutine(PlayerKnockedDown());
        }
        if (!KnockedDown && TakeDamage && !Locked)
        {
            Locked = true;
            StartCoroutine(PlayerTakeDamage());
        }
        if (!KnockedDown && !TakeDamage && !Locked && Disintegrated)
        {
            Locked = true;
            StartCoroutine(PlayerDisintegrated());
        }

        if (!TryEnsureInputReader(out PlayerInputReader reader))
        {
            return;
        }

        if (reader.DebugLightningPressed)
        {
            StartCoroutine(PlayerStruckByLightning());
        }

        // keep drop shadow on ground at all times
        if (dropShadow != null && Grounded)
        {
            dropShadow.transform.position = new Vector3(transform.root.position.x, transform.position.y + 0.015f,
                transform.root.position.z);
        }
        if (dropShadow != null && !Grounded) // player in air
        {
            terrainYHeight = ResolveDropShadowHeight();
            dropShadow.transform.position = new Vector3(transform.root.position.x, terrainYHeight,
            transform.root.position.z);
        }

        bballRelativePositioning = bballRimVector.x - rigidBody.position.x;
        playerRelativePositioning = rigidBody.position - bballRimVector;

        playerDistanceFromRim = Vector3.Distance(transform.position, new Vector3(bballRimVector.x, transform.position.y, bballRimVector.z));
        playerDistanceFromRimFeet = playerDistanceFromRim * 6;

        // if run input or run toggle on
        if (reader.RunHeld //if button is held
            && !InAir
            && !KnockedDown
            && rigidBody.linearVelocity.magnitude > 0.1f
            && !Locked)
        {
            //running = true;
            anim.SetBool("moonwalking", true);
        }
        //else
        //{
        //    //running = false;
        //    //anim.SetBool("moonwalking", false);
        //}
        // determine if player animation is shooting from or facing basket
        if (Math.Abs(playerRelativePositioning.x) > 2 &&
            Math.Abs(playerRelativePositioning.z) < 2)
        {
            FacingFront = false;
        }
        else
        {
            FacingFront = true;
        }
        // set player shoot anim based on position
        if (FacingFront) // facing straight toward bball goal
        {
            SetPlayerAnim("basketballFacingFront", true);
        }
        else // side of goal, relative postion
        {
            SetPlayerAnim("basketballFacingFront", false);
        }
        // ----- control speed based on commands----------
        // idle, walk, walk with ball state
        // AUD-049: the parentheses are load-bearing. `&&` binds tighter than `||`, so without them
        // the !InAir/!KnockedDown guard applied only to the bIdle branch and idle/walk reset to
        // ground speed regardless of state.
        if ((currentState == idleState || currentState == walkState || currentState == bIdle)
            && !InAir
            && !KnockedDown)
        {
            movementSpeed = characterProfile.Speed;
        }
        // if run state
        if (currentState == run && !hasBasketball) //|| (runningToggle || running) )
        {
            movementSpeed = characterProfile.RunSpeed; ;
        }
        // if run state
        if (currentState == bWalk && hasBasketball) //|| (runningToggle || running) )
        {
            movementSpeed = characterProfile.RunSpeedHasBall;
        }
        if (currentState == attackState || currentState == blockState)
        {
            movementSpeed = blockSpeed;
        }
        // inair state
        if (InAir)//&& currentState != inAirDunkState)
        {
            CheckIsPlayerFacingGoal();
            if (currentState != inAirDunkState)
            {
                movementSpeed = characterProfile.InAirSpeed;
            }
        }
        if (Grounded
            && !KnockedDown
            && !hasBasketball
            && !InAir
            && currentState != dunkState)
        {
            canAttack = true;
            if (playerHealth.Block > 0)
            {
                canBlock = true;
            }
            else
            {
                canBlock = false;
            }
        }
        else
        {
            canBlock = false;
            canAttack = false;
        }

        Vector2 touchJumpOrShootPosition;
        if (reader.ConsumeTouchJumpOrShoot(out touchJumpOrShootPosition) && !matchRuntime.Rules.EnemiesOnly)
        {
            TouchControlJumpOrShoot(touchJumpOrShootPosition);
        }

        //------------------ jump -----------------------------------
        if (reader.JumpPressed
            //&& !controls.Player.shoot.triggered
            && hasBasketball
            && Grounded
            && !KnockedDown
            && !matchRuntime.Rules.EnemiesOnly
            && !InAir)
        {
            if (PlayerDunk != null
                && PlayerDunk.PlayerCanDunk
                && playerDistanceFromRimFeet < PlayerDunk.DunkRangeFeet)
            {
                dunkTrigger = true;
            }
            else
            {
                jumpTrigger = true;
            }
        }
        //------------------ shoot -----------------------------------
        // if has ball, is in air, and pressed shoot button.
        if (InAir
            && hasBasketball
            && reader.ShootPressed
            && !matchRuntime.Rules.EnemiesOnly
            && currentState != inAirDunkState)
        {
            //Debug.Log("shoot");
            callBallToPlayer.Locked = true;
            basketball.BasketBallState.Locked = true;
            CheckIsPlayerFacingGoal(); // turns player facing rim
            Shotmeter.MeterEnded = true;
            PlayerShoot();
        }
        //------------------ call ball -----------------------------------
        if (reader.CallBallPressed
            && !reader.DebugChangeHeld
            && CurrentState != BlockState
            && !hasBasketball
            && basketball.BasketBallState.CanPullBall
            && !basketball.BasketBallState.Locked
            && Grounded
            //&& !Locked
            && callBallToPlayer.CallEnabled)
        {
            //Locked = true;
            callBallToPlayer.pullBallToPlayer(basketball.gameObject);
            //Locked = false;
        }
        //------------------ attack -----------------------------------

        if (reader.AttackPressed
            //&& controls.Player.jump.ReadValue<float>() == 1
            && !hasBasketball
            && canAttack
            && matchRuntime.Rules.EnemiesEnabled
            && currentState != attackState
            && currentState != specialState)
        {
            PlayerAttack();
        }
        else
        {
            anim.SetBool("attack", false);
        }
        //------------------ block -----------------------------------
        if (reader.BlockHeld
            //&& controls.Player.run.ReadValue<float>() == 1
            //&& !hasBasketball
            && canBlock
            && (matchRuntime.Rules.EnemiesOnly || matchRuntime.Rules.EnemiesEnabled || matchRuntime.Rules.IsBattleRoyal)
            && PlayerHealth.Block > 0)
        {
            if (playerCanBlock)
            {
                PlayerBlock();
            }
            if (!playerCanBlock)
            {
                jumpTrigger = true;
            }
        }

        else
        {
            if (!reader.TouchBlockHeld || SystemInfo.deviceType != DeviceType.Handheld)
            {
                anim.SetBool("block", false);
            }
        }

        //------------------ special -----------------------------------
        if (reader.SpecialPressed
            && !InAir
            && Grounded
            && !KnockedDown
            && matchRuntime.Rules.EnemiesEnabled
            && PlayerHealth.Special == PlayerHealth.MaxSpecial)
        {
            PlayerSpecial();
        }

        // if player is falling, nto sure what this is useful for. comment out
        //if (rigidBody.velocity.y > 0)
        //{
        //    //updates "highest point" as long at player still moving upwards ( velcoity > 0)
        //    finalHeight = transform.position.y;
        //    //Debug.Log("intialHeight : " + initialHeight);  
        //    //Debug.Log("finalHeight : " + finalHeight);
        //}
    }

    /// <summary>
    /// The drop shadow's Y position while airborne: an active Terrain's own sampled height where one
    /// exists, else the bound <see cref="groundHeightProvider"/>'s current value.
    ///
    /// AUD-012 Phase 2b Slice 21: mirrors <c>BasketBall.ResolveDropShadowHeight</c>'s identical
    /// no-Terrain fallback. Unlike <c>BasketBall</c> (bound synchronously before its own <c>Start()</c>
    /// ever runs), this controller's arena context is bound later, from <c>GameLevelManager.Start()</c>
    /// - so an unbound provider here is a real, if narrow, composition-timing possibility rather than
    /// an unreachable branch, and is reported rather than dereferenced. Logged once per controller
    /// (<see cref="groundHeightProviderMissingLogged"/>), not on every call, since an airborne player
    /// with no active Terrain re-enters this branch every Update() frame for the whole arc. <see
    /// cref="IGroundHeightProvider.GroundHeight"/> is read here, at the point of use, every call -
    /// never cached - since the bound <c>GameLevelManager</c> updates its own value after spawning.
    /// </summary>
    private float ResolveDropShadowHeight()
    {
        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f;
        }

        if (groundHeightProvider != null)
        {
            return groundHeightProvider.GroundHeight + 0.02f;
        }

        if (!groundHeightProviderMissingLogged)
        {
            groundHeightProviderMissingLogged = true;
            Debug.LogError($"PlayerController on {name} has no bound ground-height provider for its no-Terrain drop-shadow fallback.", this);
        }

        return terrainYHeight;
    }

    private void checkIdleTimeForSniper()
    {
        IPlayerIdleSniperRuntime sniper = idleSniperRuntimeReader != null
            ? idleSniperRuntimeReader.Invoke()
            : null;

        if (!matchRuntime.Rules.SniperEnabled || sniper == null)
        {
            idleStartTime = Time.time;
            idleTime = 0;
            return;
        }

        if (movementHorizontal == 0 && movementVertical == 0 && Grounded)
        {
            idleTime = Time.time - idleStartTime;
        }
        else
        {
            idleStartTime = Time.time;
            idleTime = 0;
        }
        if (idleTime > 150 && !sniper.Locked)
        {
            sniper.Locked = true;
            idleStartTime = Time.time;
            idleTime = 0;
            Debug.Log(" kill player");
            float random = UtilityFunctions.GetRandomFloat(0, 4);
            StartCoroutine(sniper.GetInstantKillRoutine(random));
        }
    }

    private void getAnimatorStateHashes()
    {
        idleState = Animator.StringToHash("base.idle");
        walkState = Animator.StringToHash("base.movement.walk");
        run = Animator.StringToHash("base.movement.run");
        bWalk = Animator.StringToHash("base.movement.basketball_dribbling");
        bIdle = Animator.StringToHash("base.movement.basketball_idle");
        knockedDownState = Animator.StringToHash("base.knockedDown");
        takeDamageState = Animator.StringToHash("base.takeDamage");
        specialState = Animator.StringToHash("base.special");
        attackState = Animator.StringToHash("base.attack.attack");
        blockState = Animator.StringToHash("base.attack.block");
        inAirDunkState = Animator.StringToHash("base.inair.inair_dunk");
        // AUD-051: these two were the only hashes here without the "base." prefix, so they could
        // never match a fullPathHash. AutoPlayerController papered over it by re-assigning the
        // prefixed value in Start; the fix belongs here.
        inAirHasBasketballFrontState = Animator.StringToHash("base.inair.inair_hasBasketball_front");
        inAirHasBasketballSideState = Animator.StringToHash("base.inair.inair_hasBasketball_side");
        inAirShootState = Animator.StringToHash("base.inair.basketball_shoot");
        inAirShootFrontState = Animator.StringToHash("base.inair.basketball_shoot_front");
        jumpState = Animator.StringToHash("base.inair.jump");
        inAirHasBasketball = Animator.StringToHash("base.inair.inair_hasBasketball");
        disintegratedState = Animator.StringToHash("base.disintegrated");
        dunkState = dunkState = Animator.StringToHash("base.inair.dunk");
        lightningState = Animator.StringToHash("base.lightning");
    }

    public void TouchControlJumpOrShoot(Vector2 touchPosition)
    {
        if (Grounded
            && !KnockedDown
            && hasBasketball
            && playerDistanceFromRimFeet > PlayerDunk.DunkRangeFeet
            && touchPosition.x > Screen.safeArea.center.x
            && !Locked)
        {
            jumpTrigger = true;
        }
        if (PlayerDunk != null
            && PlayerDunk.PlayerCanDunk
            && playerDistanceFromRimFeet < PlayerDunk.DunkRangeFeet
            && currentState != inAirDunkState
            && !InAir
            && Grounded
            && hasBasketball
            && touchPosition.x > Screen.safeArea.center.x
            && !Locked)
        {
            dunkTrigger = true;
        }
        // if has ball, is in air, and pressed shoot button.
        // shoot ball
        if (InAir
            && hasBasketball
            && touchPosition.x > Screen.safeArea.center.x
            && currentState != inAirDunkState)
        {
            callBallToPlayer.Locked = true;
            basketball.BasketBallState.Locked = true;
            CheckIsPlayerFacingGoal(); // turns player facing rim
            Shotmeter.MeterEnded = true;
            PlayerShoot();
        }
        // call ball
        if (!hasBasketball
            && !InAir
            && basketball.BasketBallState.CanPullBall
            && !basketball.BasketBallState.Locked
            && Grounded
            && !callBallToPlayer.Locked
            && touchPosition.x > Screen.safeArea.center.x)
        {
            callBallToPlayer.Locked = true;
            callBallToPlayer.pullBallToPlayer(basketball.gameObject);
            callBallToPlayer.Locked = false;
        }
    }
    public void PlayerAttack()
    {
        if (playerCanAttack)
        {
            // get random close attack if more than one
            playerSwapAttack.setCloseAttack();
            anim.Play("attack");
        }
    }

    public void PlayerBlock()
    {
        if (canBlock)
        {
            anim.SetBool("block", true);
        }
    }

    public void PlayerShoot()
    {
        basketball.shootBasketBall(basketball.BasketBallState.TwoPoints,
            basketball.BasketBallState.ThreePoints,
            basketball.BasketBallState.FourPoints,
            basketball.BasketBallState.SevenPoints);
    }

    public void PlayerSpecial()
    {
        playerHealth.SpendSpecial(playerHealth.Special);
        PlayAnim("special");
    }
    public void CheckIsPlayerFacingGoal()
    {
        if (bballRelativePositioning > 0 && !FacingRight
            && currentState != specialState
            && currentState != attackState)
        {
            Flip();
        }

        if (bballRelativePositioning < 0f && FacingRight
            && currentState != specialState
            && currentState != attackState)
        {
            Flip();
        }
    }

    /// <summary>
    /// Drives horizontal movement through the rigidbody's velocity, leaving the vertical axis to
    /// gravity and <see cref="PlayerJump"/>.
    ///
    /// This used to be <c>rigidBody.MovePosition(rigidBody.position + movement)</c>, which does not
    /// mean "teleport" on a non-kinematic body - the player is <c>m_IsKinematic: 0</c> with
    /// <c>m_LinearDamping: 0</c> and discrete collision detection, so PhysX derives an implicit
    /// velocity of delta/fixedDeltaTime to reach the requested position. That produced two bugs:
    ///
    /// - walking into another character drove a dynamic body into a collider, and the resulting
    ///   depenetration impulse had no damping to bleed it off, so the player shot away at
    ///   enormous speed;
    /// - jumping wrote <c>linearVelocity</c> directly while MovePosition kept running every physics
    ///   step (there is no grounded check on movement), so holding a direction and jumping had the
    ///   two mechanisms compound and the player flew a long way, fast.
    ///
    /// Writing velocity composes correctly with both: the jump's vertical component is preserved,
    /// gravity still applies, and contacts resolve through the solver instead of by teleporting into
    /// them. Overwriting the horizontal component every step also means any residual push from a
    /// contact is gone on the next step rather than accumulating.
    ///
    /// Speed is unchanged - <c>movementSpeed</c> was already units per second, which is why the old
    /// line multiplied it by <c>Time.fixedDeltaTime</c> to get a per-step displacement. Air control
    /// still uses the <c>InAirSpeed</c> that Update selects.
    /// </summary>
    private void ApplyHorizontalMovement()
    {
        // Something else owns the body in these states, and writing locomotion over it erases the
        // motion entirely rather than blending with it:
        //   - PlayerDunk.Launch sets a ballistic velocity and then immediately clears Locked, so
        //     FixedUpdate resumes while the dunk is still in flight. Zeroing x/z there means the
        //     player never travels to the rim.
        //   - PlayerAnimationEvents applies attack lunges and projectile recoil with
        //     ForceMode.VelocityChange. Attacks never set Locked, so a lunge would be wiped one
        //     physics step later.
        if (currentState == inAirDunkState || currentState == attackState)
        {
            return;
        }

        bool steering = movementHorizontal != 0f || movementVertical != 0f;
        if (!steering && !Grounded)
        {
            // Airborne and not steering: leave the body alone. The MovePosition this replaced added
            // a zero delta here, which was a no-op, so zeroing would be new behaviour rather than
            // restored behaviour - and it would flatten every jump and knockback arc.
            return;
        }

        // Grounded with no input still writes zero, which is what stops a contact impulse from
        // surviving into the next step.
        RigidbodyLocomotionMotor.SetPlanarVelocity(
            rigidBody, movementHorizontal * movementSpeed, movementVertical * movementSpeed);
    }

    public void PlayerJump()
    {
        // AUD-012 Phase 4 Slice 72: Y-only write (was a full-vector `linearVelocity = Vector3.up *
        // jumpForce` overwrite) so this composes correctly with RigidbodyLocomotionMotor's planar write
        // in ApplyHorizontalMovement() regardless of call order, matching the same fix applied to
        // AutoPlayerController.AutoPlayerJump().
        Vector3 velocity = rigidBody.linearVelocity;
        velocity.y = characterProfile.JumpForce;
        rigidBody.linearVelocity = velocity; //+ (Vector3.forward * rigidBody.velocity.x))
        //jumpStartTime = Time.time;
        // AUD-048: this was `!battleRoyal || !enemiesOnly`, which is only false when BOTH are on -
        // so the shot meter still started in a plain battle royal and in a plain enemies-only run,
        // the two modes it exists to exclude. The other shooting gates in Update use `&&`.
        if (!matchRuntime.Rules.IsBattleRoyal && !matchRuntime.Rules.EnemiesOnly)
        {
            Shotmeter.MeterStarted = true;
            Shotmeter.MeterStartTime = Time.time;
        }
        //// if not dunking, start shot meter
        //if (currentState != inAirDunkState)
        //{
        //    Shotmeter.MeterStarted = true;
        //    Shotmeter.MeterStartTime = Time.time;
        //}
    }

    //-----------------------------------Walk function -----------------------------------------------------------------------
    //void isWalking(Vector3 movement)
    void IsWalking(float horizontal, float vertical)
    {
        // if moving
        //if (horizontal > 0f || horizontal < 0f || vertical > 0f || vertical < 0f)
        if (horizontal != 0 || vertical != 0f)
        {
            // not in air
            if (!InAir) // dont want walking animation playing while inAir
            {
                anim.SetBool("walking", true);
                // walking but running toggle is ON
                if (runningToggle)
                {
                    anim.SetBool("moonwalking", true);
                }
            }
        }
        // not moving
        else
        {
            anim.SetBool("walking", false);
            anim.SetBool("moonwalking", false);
            //moonwalkAudio.enabled = false;
            //running = false;
        }

        // player moving right, not facing right
        if (horizontal > 0 && !FacingRight)//&& canMove)
        {
            Flip();
        }
        // player moving left, and facing right
        if (horizontal < 0f && FacingRight)//&& canMove)
        {
            Flip();
        }
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;

        if (damageDisplayObject != null
            && (matchRuntime.Rules.EnemiesEnabled || matchRuntime.Rules.EnemiesOnly || matchRuntime.Rules.SniperEnabled))
        {
            Vector3 damageScale = damageDisplayObject.transform.localScale;
            damageScale.x *= -1;
            damageDisplayObject.transform.localScale = damageScale;
        }
    }

    // ------------------------------- take damage -------------------------------------------------------
    public IEnumerator PlayerTakeDamage()
    {
        return damageReactions.PlayerTakeDamage(_takeDamageTime);
    }

    public IEnumerator PlayerFreezeForXSeconds(float time)
    {
        return damageReactions.PlayerFreezeForXSeconds(time);
    }

    public IEnumerator PlayerKnockedDown()
    {
        return damageReactions.PlayerKnockedDown(_knockDownTime);
    }

    public IEnumerator PlayerDisintegrated()
    {
        return damageReactions.PlayerDisintegrated();
    }

    public IEnumerator PlayerStruckByLightning()
    {
        return damageReactions.PlayerStruckByLightning();
    }

    public IEnumerator ShrinkPlayer()
    {
        return damageReactions.ShrinkPlayer();
    }

    public void PlayerAvoidKnockedDown()
    {
        damageReactions.PlayerAvoidKnockedDown();
    }

    //------------------------- set animator parameters -----------------------
    public void SetPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }

    //------------------------- set animator parameters -----------------------
    public void SetPlayerAnimTrigger(string animationName)
    {
        anim.SetTrigger(animationName);
    }

    //-------------------play animation function ------------------------------
    // provide access to what should be private animator
    public void PlayAnim(string animationName)
    {
        anim.Play(animationName);
    }
    // ----------------------- freeze player postion ------------------------
    public void FreezePlayerPosition()
    {
        RigidbodyFreezeHelper.FreezePosition(rigidBody);
    }

    public void UnFreezePlayerPosition()
    {
        RigidbodyFreezeHelper.UnfreezeRotationOnly(rigidBody);
    }

    // #todo find all these messageDisplay coroutines and move to seprate generic class MessageLog od something
    public void ToggleRun()
    {
        runningToggle = !runningToggle;
        Text messageText = SceneObjects.Find<Text>("messageDisplay", this);
        if (messageText == null)
        {
            return;
        }

        messageText.text = "running toggle = " + runningToggle;

        // turn off text display after 5 seconds
        StartCoroutine(basketball.turnOffMessageLogDisplayAfterSeconds(3));
    }

    public bool IsSpecialState()
    {
        return currentState == specialState;
    }

    public bool Grounded
    {
        get { return _grounded; }
        set { _grounded = value; }
    }

    public bool InAir
    {
        get { return _inAir; }
        set { _inAir = value; }
    }

    public bool Locked
    {
        get { return _locked; }
        set { _locked = value; }
    }

    //public float RigidBodyYVelocity
    //{
    //    get { return rigidBody.velocity.y; }
    //}
    public bool FacingFront
    {
        get => _facingFront;
        set => _facingFront = value;
    }
    public ShotMeter Shotmeter
    {
        get => shotmeter;
        set => shotmeter = value;
    }

    public bool KnockedDown
    {
        get => _knockedDown;
        set => _knockedDown = value;
    }
    public bool AvoidedKnockDown
    {
        get => _avoidedKnockDown;
        set => _avoidedKnockDown = value;
    }

    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }
    //public float MovementSpeed { get => movementSpeed; set => movementSpeed = value; }
    public bool TakeDamage { get => _takeDamage; set => _takeDamage = value; }
    public int CurrentState { get => currentState; set => currentState = value; }
    public int AttackState { get => attackState; set => attackState = value; }
    public int BlockState { get => blockState; set => blockState = value; }
    public int SpecialState { get => specialState; set => specialState = value; }
    public bool FacingRight { get => _facingRight; set => _facingRight = value; }
    public bool CanAttack { get => canAttack; set => canAttack = value; }
    public bool PlayerCanBlock { get => playerCanBlock; set => playerCanBlock = value; }
    public bool CanBlock { get => canBlock; set => canBlock = value; }
    public Animator Anim { get => anim; set => anim = value; }
    //public AudioSource Audiosource { get => audiosource; set => audiosource = value; }
    public Text DamageDisplayValueText { get => damageDisplayValueText; set => damageDisplayValueText = value; }
    public float PlayerDistanceFromRim { get => playerDistanceFromRim; set => playerDistanceFromRim = value; }
    public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
    public CallBallToPlayer CallBallToPlayer { get => callBallToPlayer; set => callBallToPlayer = value; }
    public PlayerControls Controls
    {
        get => controls;
        set
        {
            controls = value;
            inputReader = controls != null ? new PlayerInputReader(controls, ReadLegacyTouchMovement) : null;
        }
    }
    public PlayerAttackQueue PlayerAttackQueue { get => playerAttackQueue; set => playerAttackQueue = value; }
    public PlayerDunk PlayerDunk { get => playerDunk; set => playerDunk = value; }
    public CharacterProfile CharacterProfile { get => characterProfile; set => characterProfile = value; }
    public BasketBall Basketball { get => basketball; set => basketball = value; }
    public bool Disintegrated { get => _disintegrated; set => _disintegrated = value; }

    // ==================== IShooterActor (player<->basketball cycle-cut slice) ====================
    // Explicit implementation: these exist only for basketball-side code reaching this controller
    // through PlayerIdentifier.Actor, so they stay off the ordinary public surface. FacingFront and
    // Grounded already satisfy the interface implicitly via the properties above - no code needed.

    bool IShooterActor.HasBasketball { get => hasBasketball; set => hasBasketball = value; }

    bool IShooterActor.InDunkState => currentState == dunkState;

    float IShooterActor.DistanceFromRim => playerDistanceFromRim;

    private ShooterAttributes? _shooterAttributes;

    // Lazily cached on first access rather than in Start(), so a basketball-side reader (e.g.
    // ShotMeter.Start()) can never race this controller's own Start() - see the cycle-cut plan's
    // execution-order note. Preserves ShooterAttributesMapper's "warn once on a missing profile"
    // behavior, since the underlying CharacterProfile reference itself does not change after Awake.
    ShooterAttributes IShooterActor.ShooterAttributes =>
        _shooterAttributes ??= ShooterAttributesMapper.From(GetComponent<CharacterProfile>());

    // Deliberately not memoized - see IShooterActor.Clutch. The human path never reads this, but the
    // read has to stay live regardless, since a MonoBehaviour can't memoize a member only for one
    // implementer.
    int IShooterActor.Clutch => GetComponent<CharacterProfile>()?.Clutch ?? 0;

    float IShooterActor.ShotMeterSliderValue => shotmeter.SliderValueOnButtonPress;

    bool IShooterActor.ShotMeterEnded => shotmeter.MeterEnded;

    void IShooterActor.SetAnimBool(string name, bool value) => SetPlayerAnim(name, value);

    void IShooterActor.SetAnimTrigger(string name) => SetPlayerAnimTrigger(name);

    void IShooterActor.LockCallBallToPlayer(bool locked) => callBallToPlayer.Locked = locked;

    void IShooterActor.DisplayShotMeterMessage(string message) => shotmeter.displaySliderMessageText(message);

    // No CPU-style shot-cycle reset on the human path - a no-op lets BasketBall.Launch call this
    // unconditionally, symmetric with BasketBallAuto.Launch's call to the real CPU implementation.
    void IShooterActor.EndShootCycle() { }
    public bool KilledOnIdle { get; internal set; }

    // ==================== IPlayerDamageReactionHost (AUD-012 Phase 2b Slice 32) ====================
    // Anim, RigidBody, CurrentState, TakeDamage, KnockedDown, Locked, AvoidedKnockDown and FacingRight
    // are satisfied implicitly by the ordinary public properties above - they already expose exactly
    // this state. Only the members below have no existing public equivalent; explicit implementation
    // keeps them off PlayerController's ordinary public surface rather than adding new general-purpose
    // public members for a single helper's use.

    Transform IPlayerDamageReactionHost.ActorTransform => transform;

    int IPlayerDamageReactionHost.TakeDamageStateHash => takeDamageState;
    int IPlayerDamageReactionHost.KnockedDownStateHash => knockedDownState;
    int IPlayerDamageReactionHost.DisintegratedStateHash => disintegratedState;
    int IPlayerDamageReactionHost.LightningStateHash => lightningState;

    bool IPlayerDamageReactionHost.IsShrunk { get => isShrunk; set => isShrunk = value; }

    void IPlayerDamageReactionHost.MarkDead() => playerHealth.IsDead = true;

    Camera IPlayerDamageReactionHost.GetShrinkCamera() => damageReactionCameraReader != null ? damageReactionCameraReader.Invoke() : null;

    // ==================== IPlayerDunkHost (AUD-012 Phase 2b Slice 35) ====================
    // RigidBody, CurrentState and Locked are satisfied implicitly by the ordinary public properties
    // above - they already expose exactly this state. Only the members below have no existing public
    // equivalent; explicit implementation keeps them off PlayerController's ordinary public surface
    // rather than adding new general-purpose public members for a single helper's use.

    Vector3 IPlayerDunkHost.BasketballRimVector => bballRimVector;

    int IPlayerDunkHost.DunkStateHash => dunkState;

    bool IPlayerDunkHost.HasBasketball { get => hasBasketball; set => hasBasketball = value; }

    void IPlayerDunkHost.SetCallBallLocked(bool locked) => callBallToPlayer.Locked = locked;

    void IPlayerDunkHost.FaceBasketballGoal() => CheckIsPlayerFacingGoal();

    void IPlayerDunkHost.PlayAnimation(string animationName) => PlayAnim(animationName);

    void IPlayerDunkHost.SetAnimationBool(string parameterName, bool value) => SetPlayerAnim(parameterName, value);

    void IPlayerDunkHost.FreezePosition() => FreezePlayerPosition();

    void IPlayerDunkHost.UnfreezePosition() => UnFreezePlayerPosition();
}
