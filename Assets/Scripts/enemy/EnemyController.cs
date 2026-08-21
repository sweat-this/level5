using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Level5.Core.Match;

public class EnemyController : MonoBehaviour, ICombatAgent, IPooledSpawnReset
{
    private static readonly HashSet<EnemyController> ActiveEnemySet = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveEnemies()
    {
        ActiveEnemySet.Clear();
    }
    private Animator anim;
    private Rigidbody rigidBody;
    private EnemyDetection enemyDetection;
    [SerializeField]
    private EnemyHealth enemyHealth;
    private EnemyHealthBar enemyHealthBar;
    private SpriteRenderer spriteRenderer;
    private PlayerSwapAttack playerSwapAttack;
    // target for enemy to move to
    private Vector3 targetPosition;

    Vector3 movement;
    private float movementSpeed;
    public float walkMovementSpeed;

    [SerializeField]
    public bool facingRight;
    // how long after attacking the enemy can attack again
    [SerializeField]
    public float attackCooldown;
    [SerializeField]
    private float relativePositionToPlayer;
    [SerializeField]
    private float distanceFromPlayer;
    [SerializeField]
    private float distanceFromBodyGuard;
    [SerializeField]
    private float minDistanceCloseAttack;
    [SerializeField]
    private float maxDistanceLongRangeAttack;
    [SerializeField]
    private float minDistanceLongRangeAttack;
    [SerializeField]
    private float knockBackForce;
    [SerializeField]
    bool hasLongRangeAttack;
    [SerializeField]
    private bool longRangeAttack;
    [SerializeField]
    private float knockDownTime;
    [SerializeField]
    private float takeDamageTime;
    [SerializeField]
    bool isMinion;
    [SerializeField]
    bool isBoss;

    // AI architecture: explicit target assignment (replaces reaching for
    // GameLevelManager.instance.PlayerController1 from every method that needed the queue).
    // EnemySpawner assigns this via RuntimeObjectPool's configure callback before OnEnable; the
    // lazy fallback in the getter below covers enemies placed directly in a scene.
    [SerializeField]
    private PlayerAttackQueue targetQueue;
    private ICombatAgent currentBodyguardTarget;
    private readonly List<ICombatAgent> bodyGuardCandidateBuffer = new List<ICombatAgent>();
    private CombatTacticalState currentAiState = CombatTacticalState.Idle;
    private string lastTransitionReason = "spawn";

    private AnimatorStateInfo currentStateInfo;
    private int currentState;
    private static readonly int AnimatorState_Attack = Animator.StringToHash("base.attack");
    private static readonly int AnimatorState_Walk = Animator.StringToHash("base.walk");
    private static readonly int AnimatorState_Idle = Animator.StringToHash("base.idle");
    private static readonly int AnimatorState_Knockdown = Animator.StringToHash("base.knockdown");
    private static readonly int AnimatorState_Lightning = Animator.StringToHash("base.lightning");
    private static readonly int AnimatorState_Disintegrated = Animator.StringToHash("base.disintegrated");

    public bool stateWalk = false;
    public bool stateIdle = false;
    public bool stateAttack = false;
    public bool statePatrol = false;
    public bool stateKnockDown = false;

    private float lineOfSight;
    public float lineOfSightVariance;
    [SerializeField]
    public bool canAttack;
    bool inAttackQueue;

    [SerializeField]
    bool enemyUsesPhysics;
    [SerializeField]
    GameObject dropShadow;

    Vector3 originalPosition;
    [SerializeField]
    private GameObject damageDisplayObject;
    [SerializeField]
    private GameObject spriteObject;

    private Vector3 initialScale;

    private void Awake()
    {
        SpriteRenderer childSprite = transform.GetComponentInChildren<SpriteRenderer>();
        spriteObject = childSprite != null ? childSprite.gameObject : null;
        rigidBody = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        spriteRenderer = childSprite;

        enemyDetection = gameObject.GetComponent<EnemyDetection>();
        enemyHealthBar = gameObject.GetComponentInChildren<EnemyHealthBar>();
        enemyHealth = gameObject.GetComponentInChildren<EnemyHealth>();
        Transform damageDisplay = transform.FindDeepChild("enemy_damage_display_text");
        damageDisplayObject = damageDisplay != null ? damageDisplay.gameObject : null;
        playerSwapAttack = GetComponent<PlayerSwapAttack>();
        initialScale = transform.localScale;
        //if (attackCooldown == 0) { attackCooldown = 0.75f; }
        //if (knockDownTime == 0) { knockDownTime = 2f; }
        if (lineOfSightVariance == 0) { lineOfSightVariance = 0.4f; }
        if (takeDamageTime == 0) { takeDamageTime = 0.5f; }
        if (minDistanceCloseAttack == 0) { minDistanceCloseAttack = 0.6f; }
        if (knockBackForce == 0) { knockBackForce = 3f; }

        if (isMinion)
        {
            attackCooldown = 1.5f;
            takeDamageTime = 0.5f;
            walkMovementSpeed = 1.75f;
        }
        if (isBoss)
        {
            attackCooldown = 1.15f;
            takeDamageTime = 0.5f;
            walkMovementSpeed = 2.5f;
        }

        movementSpeed = walkMovementSpeed;
        if (MatchRuntime.Rules.Hardcore || MatchRuntime.Rules.Difficulty == MatchDifficulty.Hardcore)
        {
            // +25% speed
            movementSpeed *= 1.25f;
            // 50% reduction in attack cooldown 
            attackCooldown *= 0.5f;
        }
        // for enemy damagae display over head
        if (damageDisplayObject != null && damageDisplayObject.GetComponent<Canvas>() != null)
        {
            damageDisplayObject.transform.parent.GetComponent<Canvas>().worldCamera = Camera.main;
        }
        // if level has custom level specific camera
        if (MatchRuntime.CustomCamera)
        {
            if (spriteObject != null)
            {
                spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
            if (enemyHealthBar != null)
            {
                enemyHealthBar.gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
    }

    private void OnEnable()
    {
        ActiveEnemySet.Add(this);
        ResetForSpawn();
        InvokeRepeating(nameof(UpdateDistanceFromPlayer), 0, 0.1f);
    }

    private void OnDisable()
    {
        ActiveEnemySet.Remove(this);
        CancelInvoke();
        StopAllCoroutines();
        ReleaseAttackReservation();
    }

    public void ResetForSpawn()
    {
        facingRight = true;
        canAttack = true;
        inAttackQueue = false;
        longRangeAttack = false;
        stateWalk = false;
        stateIdle = false;
        stateAttack = false;
        statePatrol = false;
        stateKnockDown = false;
        transform.localScale = initialScale;
        originalPosition = transform.position;

        // STEP 15: a pooled reuse must not carry the previous life's target/state forward.
        // targetQueue is intentionally left alone - EnemySpawner reassigns it via
        // AssignTargetQueue on every spawn, and it is match-scoped identity, not per-life state.
        currentBodyguardTarget = null;
        currentAiState = CombatTacticalState.Idle;
        lastTransitionReason = "spawn";

        if (rigidBody != null)
        {
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }

        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        enemyHealth?.ResetForSpawn();
    }

    private void FixedUpdate()
    {
        if (stateWalk 
            && currentState != AnimatorState_Knockdown 
            && currentState != AnimatorState_Disintegrated
            && enemyDetection.Attacking)
        {
            pursueTarget();
        }
        if (statePatrol)
        {
            returnToPatrol();
        }
        if (enemyUsesPhysics)
        {
            dropShadow.transform.position = new Vector3(dropShadow.transform.position.x,
                gameObject.transform.position.y + 0.01f, dropShadow.transform.position.z);
        }
    }

    void Update()
    {
        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;
        // ================== enemy facing player ==========================
        PlayerAttackQueue playerAttackQueue = TargetQueue;
        if (playerAttackQueue == null)
        {
            // STEP 1: no resolvable target queue (e.g. spawned before match wiring finished) -
            // fail safe instead of chaining into a null reference. UpdateDistanceFromPlayer will
            // pick this back up once a queue becomes available.
            return;
        }

        Vector3 facingAnchor = currentBodyguardTarget != null
            ? currentBodyguardTarget.CombatTransform.position
            : playerAttackQueue.transform.position;
        relativePositionToPlayer = facingAnchor.x - transform.position.x;

        // ================== enemy idle ==========================
        if ((/*GameLevelManager.instance.PlayerController.KnockedDown*/
            !canAttack
            || !enemyDetection.Attacking)
            && currentState != AnimatorState_Attack)
        {
            stateIdle = true;
            //if idle stop rigidbody
            rigidBody.linearVelocity = Vector3.zero;
        }
        else
        {
            stateIdle = false;
        }
        // ================== enemy attack state ==========================
        if (math.abs(relativePositionToPlayer) <= maxDistanceLongRangeAttack
            && math.abs(relativePositionToPlayer) >= minDistanceLongRangeAttack
            && hasLongRangeAttack
            && math.abs(lineOfSight) <= lineOfSightVariance
            && canAttack)
        {
            longRangeAttack = true;
            stateAttack = true;
        }
        else if (math.abs(relativePositionToPlayer) < minDistanceCloseAttack
            && math.abs(lineOfSight) <= lineOfSightVariance
            && !longRangeAttack
            && canAttack)
        {
            stateAttack = true;
            longRangeAttack = false;
        }
        else
        {
            stateAttack = false;
            longRangeAttack = false;
        }
        // ================== enemy walk state ==========================
        if (enemyDetection.Attacking
            && !stateAttack
            && !stateIdle
            && canAttack
            && currentState != AnimatorState_Knockdown
            && currentState != AnimatorState_Disintegrated)
        {
            stateWalk = true;
        }
        else
        {
            stateWalk = false;
        }
        // ================== animation walk state ==========================
        //if (rigidBody.velocity.sqrMagnitude > 0)
        if (stateWalk || statePatrol)
        {
            anim.SetBool("walk", true);
        }
        else
        {
            anim.SetBool("walk", false);
        }
        if (stateAttack && canAttack)
        {
            canAttack = false;
            FreezeEnemyPosition();
            if (playerSwapAttack != null
                && !longRangeAttack
                && playerSwapAttack.closeAttacks != null
                && playerSwapAttack.AnimatorOverrideController != null)
            {
                playerSwapAttack.setCloseAttack();
            }
            if (playerSwapAttack != null
                && playerSwapAttack.AnimatorOverrideController != null
                && longRangeAttack
                && playerSwapAttack.longRangeAttack != null)
            {
                playerSwapAttack.setLongRangeAttack();
            }
            anim.SetTrigger("attack");
            StartCoroutine(AttackCooldown(attackCooldown));
        }
        if (relativePositionToPlayer < 0 && facingRight)
        {
            Flip();
        }
        if (relativePositionToPlayer > 0 && !facingRight)
        {
            Flip();
        }

        UpdateAiStateDiagnostics();
    }

    // STEP 4/17: a diagnostic label derived from the flags above, not a parallel state machine -
    // stateWalk/stateAttack/statePatrol/canAttack still own actual behaviour. Only updates on an
    // actual transition, so this never spams per-frame.
    private void UpdateAiStateDiagnostics()
    {
        CombatTacticalState nextState;
        if (statePatrol)
        {
            nextState = CombatTacticalState.ReturnToPatrol;
        }
        else if (stateAttack)
        {
            nextState = CombatTacticalState.Attack;
        }
        else if (stateKnockDown)
        {
            nextState = CombatTacticalState.Recover;
        }
        else if (stateWalk)
        {
            nextState = currentBodyguardTarget != null || enemyDetection.Attacking
                ? CombatTacticalState.Approach
                : CombatTacticalState.AcquireTarget;
        }
        else
        {
            nextState = CombatTacticalState.Idle;
        }

        if (CombatTacticalStateTransitions.TryCommit(ref currentAiState, nextState, out string reason))
        {
            lastTransitionReason = reason;
        }
    }

    public void FreezeEnemyPosition()
    {
        if (enemyUsesPhysics)
        {
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezePositionZ;
            //| RigidbodyConstraints.FreezePositionX;
        }
        else
        {
            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ
                | RigidbodyConstraints.FreezeRotationY
                //| RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezePositionX;
        }
    }

    public void UnFreezeEnemyPosition()
    {
        if (enemyUsesPhysics)
        {
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ
                | RigidbodyConstraints.FreezeRotationY;
        }
        else
        {
            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ
                | RigidbodyConstraints.FreezeRotationY;
            //| RigidbodyConstraints.FreezePositionY;
        }
    }

    IEnumerator AttackCooldown(float seconds)
    {
        canAttack = false;
        stateAttack = false;
        // ENM-6: `currentState = AnimatorState_Idle;` was here. currentState is re-read from the
        // animator at the top of every Update, so the assignment could not outlive the frame it
        // happened in - it read as though it suppressed a state check that it never affected.
        // wait for animator state to get to attack
        yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).IsTag("attack"));
        // wait for animation to finish
        yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("attack"));
        UnFreezeEnemyPosition();
        //wait for cooldown
        yield return new WaitForSecondsRealtime(seconds);
        // enemy can move again
        //UnFreezeEnemyPosition();
        canAttack = true;
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;

        if ((MatchRuntime.Rules.EnemiesEnabled
            || MatchRuntime.Rules.EnemiesOnly
            || MatchRuntime.Rules.SniperEnabled
            || MatchRuntime.Rules.IsBattleRoyal)
            && damageDisplayObject != null)
        {
            Vector3 messageScale = damageDisplayObject.transform.localScale;
            messageScale.x *= -1;
            damageDisplayObject.transform.localScale = messageScale;
        }
    }

    public void setPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }
    public void playAnimation(string animationName)
    {
        anim.Play(animationName);
    }

    public IEnumerator struckByLighning(int damage)
    {
        // enemyHealth is legitimately null here too (see CanAct's null check below) - a null
        // enemyHealth enemy is never "dead" and can't be killed by this hit either.
        bool enemyKilledByHit = enemyHealth != null && !enemyHealth.IsDead && enemyHealth.ApplyDamage(new DamageInfo(damage));

        // AUD-074: enemyHealthBar is legitimately null for some enemy types (this class already
        // guards it elsewhere, e.g. the custom-camera rotation above) - matches the guard
        // EnemyCollisions already uses for the same coroutine call.
        if (enemyHealthBar != null)
        {
            StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay("-"+damage.ToString()));
        }

        stateKnockDown = true;
        FreezeEnemyPosition();
        // AUD-053: a scene without the camera_flash object otherwise threw here and abandoned the
        // rest of the lightning coroutine, leaving the enemy frozen mid-knockdown
        Animator cameraFlash = SceneObjects.Find<Animator>("camera_flash", this);
        if (cameraFlash != null)
        {
            cameraFlash.Play("camera_flash");
        }

        anim.Play("lightning");
        yield return new WaitUntil(() => currentState == AnimatorState_Lightning);

        if (enemyKilledByHit)
        {
            enemyIsDead();
        }
        else
        {
            StartCoroutine(knockedDown());
        }
    }

    public IEnumerator knockedDown()
    {
        //Debug.Log("asdasdasd");
        yield return new WaitUntil(() => currentState != AnimatorState_Lightning);
        stateKnockDown = true;
        //FreezeEnemyPosition();
        anim.SetBool("knockdown", true);
        playAnimation("knockdown");
        // get direction facing
        if (facingRight)
        {
            UnFreezeEnemyPosition();
            rigidBody.linearVelocity = Vector3.zero;
            //apply to X
            RigidBody.AddForce(-knockBackForce, knockBackForce / 2, 0, ForceMode.VelocityChange);
        }
        if (!facingRight)
        {
            UnFreezeEnemyPosition();
            rigidBody.linearVelocity = Vector3.zero;
            RigidBody.AddForce(knockBackForce, knockBackForce / 2, 0, ForceMode.VelocityChange);
        }
        yield return new WaitForSeconds(knockDownTime);
        anim.SetBool("knockdown", false);
        stateKnockDown = false;
        //UnFreezeEnemyPosition();

        stateKnockDown = false;
    }

    public IEnumerator killEnemy()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        playAnimation("disintegrated");
        yield return new WaitForSeconds(1.5f);

        if (enemyDetection.Attacking)
        {
            int attackPositionId = enemyDetection.AttackPositionId;
            ReleaseAttackReservation(attackPositionId);
        }
        RuntimeObjectPool.Release(gameObject);
    }

    public IEnumerator takeDamage()
    {
        stateKnockDown = true;
        //FreezeEnemyPosition();
        anim.SetBool("takeDamage", true);
        playAnimation("takeDamage");
        if (facingRight)
        {
            UnFreezeEnemyPosition();
            //apply to X
            RigidBody.AddForce(-knockBackForce / 2, 0, 0, ForceMode.VelocityChange);
        }
        if (!facingRight)
        {
            UnFreezeEnemyPosition();
            RigidBody.AddForce(knockBackForce / 2, 0, 0, ForceMode.VelocityChange);
        }
        yield return new WaitForSecondsRealtime(takeDamageTime);
        anim.SetBool("takeDamage", false);
        //UnFreezeEnemyPosition();
        stateKnockDown = false;
    }

    public IEnumerator disintegrated()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        playAnimation("disintegrated");
        //yield return new WaitUntil(() => currentState == AnimatorState_Disintegrated);
        yield return new WaitForSeconds(1.5f);
        RuntimeObjectPool.Release(gameObject);
        stateKnockDown = false;
    }

    public void pursueTarget()
    {
        PlayerAttackQueue playerAttackQueue = TargetQueue;
        if (playerAttackQueue == null)
        {
            return;
        }

        // if no valid bodyguard target, advance on the reserved attack position instead
        if (currentBodyguardTarget == null)
        {
            Transform attackPosition = playerAttackQueue.GetAttackPositionTransform(enemyDetection.AttackPositionId);
            if (attackPosition == null)
            {
                return;
            }

            targetPosition = (attackPosition.position - transform.position).normalized;
        }
        // otherwise engage the selected bodyguard (STEP 2/5 - selection happens in
        // RefreshBodyguardTarget, on the same cadence as UpdateDistanceFromPlayer)
        else
        {
            targetPosition = (currentBodyguardTarget.CombatTransform.position - transform.position).normalized;
        }
        movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
        rigidBody.MovePosition(transform.position + movement);
    }

    public void moveToTarget(List<GameObject> waypoints)
    {
        //targetPosition = (GameLevelManager.instance.Player.transform.position - transform.position).normalized;
        //// if no bodyguards found
        //if (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards.Count == 0 && !GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuardEngaged)
        //{
        //    targetPosition = (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.AttackPositions[enemyDetection.AttackPositionId].transform.position - transform.position).normalized;
        //}
        //// if bodyguards, attack 1 first bodyguard
        //else
        //{
        //    targetPosition = (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards[0].transform.position - transform.position).normalized;
        //}
        movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
        //movement = targetPosition * (movementSpeed * Time.deltaTime);
        rigidBody.MovePosition(transform.position + movement);
        //transform.Translate(movement);

        //Debug.Log(gameObject.transform.root.name + " -- currentSpeed : " + currentSpeed);

    }
    public void returnToPatrol()
    {
        //Debug.Log(gameObject.name + "  is returning to Vector3  : " + originalPosition);
        if (Vector3.Distance(gameObject.transform.position, OriginalPosition) > 1)
        {
            targetPosition = (originalPosition - transform.position).normalized;
            movement = targetPosition * (movementSpeed * Time.deltaTime);
            //movement = targetPosition * (movementSpeed * Time.deltaTime);
            rigidBody.MovePosition(transform.position + movement);
        }
        else
        {
            statePatrol = false;
        }
    }

    public void UpdateDistanceFromPlayer()
    {
        PlayerAttackQueue playerAttackQueue = TargetQueue;
        if (playerAttackQueue == null)
        {
            return;
        }

        // STEP 2/12: this runs on the existing 0.1s InvokeRepeating cadence (see OnEnable), which
        // doubles as the AI's decision interval - Update()/pursueTarget() just read the cached
        // result instead of re-selecting a bodyguard target every frame.
        RefreshBodyguardTarget(playerAttackQueue);

        Vector3 anchorPosition = currentBodyguardTarget != null
            ? currentBodyguardTarget.CombatTransform.position
            : playerAttackQueue.transform.position;

        distanceFromPlayer = Vector3.Distance(anchorPosition, transform.position);
        lineOfSight = anchorPosition.z - transform.position.z;
        distanceFromBodyGuard = currentBodyguardTarget != null
            ? Vector3.Distance(currentBodyguardTarget.CombatTransform.position, transform.position)
            : 0f;
    }

    // STEP 2/3: reusable target-selection policy - hostility is structural here (bodyguards are
    // the only candidates an enemy ever engages), so this only has to filter for
    // alive/active/able-to-participate and score by distance/continuity.
    private void RefreshBodyguardTarget(PlayerAttackQueue playerAttackQueue)
    {
        bodyGuardCandidateBuffer.Clear();
        IReadOnlyList<GameObject> bodyGuards = playerAttackQueue.BodyGuards;
        for (int i = 0; i < bodyGuards.Count; i++)
        {
            GameObject bodyGuardObject = bodyGuards[i];
            if (bodyGuardObject == null)
            {
                continue;
            }

            ICombatAgent agent = bodyGuardObject.GetComponent<ICombatAgent>();
            if (agent != null)
            {
                bodyGuardCandidateBuffer.Add(agent);
            }
        }

        currentBodyguardTarget = CombatTargetSelector.SelectNearestValidTarget(
            bodyGuardCandidateBuffer, transform.position, currentBodyguardTarget);
    }

    private void ReleaseAttackReservation(int attackPositionId = -1)
    {
        PlayerAttackQueue playerAttackQueue = TargetQueue;
        if (playerAttackQueue == null)
        {
            return;
        }

        if (attackPositionId >= 0)
        {
            playerAttackQueue.RemoveFromQueue(gameObject, attackPositionId);
            return;
        }

        playerAttackQueue.ReleaseReservation(this);
    }

    // AUD-001: heal-on-kill amounts. These used to be written twice - 7/3 here and 5/2 in
    // EnemyCollisions - so the same enemy death rewarded the player differently depending on which
    // script noticed it. Melee kills go through EnemyCollisions and are the overwhelming majority,
    // so its 5/2 is what the game has always actually felt like; the 7/3 pair only ever fired on
    // the rarer non-attacker paths. Unified on 5/2 to preserve that. Worth a balance review.
    private const int BossKillHealAmount = 5;
    private const int MinionKillHealAmount = 2;

    /// <summary>
    /// The single owner of "this enemy died" - healing the player, crediting the kill, the critical
    /// flourish, and the death coroutine.
    ///
    /// AUD-001: this existed twice, here and in <c>EnemyCollisions.enemyIsDead</c>, with everything
    /// identical except the heal amounts. Both call sites now route here so a change to death
    /// behaviour cannot apply to only one of them.
    /// </summary>
    /// <param name="attacker">
    /// The attack box that landed the killing blow, or null when the killer is not identifiable
    /// from a collider (lightning, scripted kills). A null attacker credits the primary player.
    /// </param>
    /// <param name="creditToPlayer">
    /// Whether the player earns the kill - the heal, the stat credit, and the flourish. False for
    /// friendly fire: an enemy killed by another enemy's attack box still dies and still runs
    /// <c>killEnemy</c>, but rewards nobody. The two original copies of this method disagreed on
    /// exactly this point, which is why it is now an explicit argument rather than inferred from
    /// <paramref name="attacker"/> being null.
    /// </param>
    public void HandleDeath(GameObject attacker, bool creditToPlayer)
    {
        // idempotent - ActorHealth ignores a repeat set, and the damage that got us here has
        // usually already latched it
        enemyHealth.IsDead = true;

        if (creditToPlayer)
        {
            PlayerHealth playerHealth = GameLevelManager.instance != null
                ? GameLevelManager.instance.PlayerHealth
                : null;
            if (playerHealth != null && playerHealth.Health < playerHealth.MaxHealth)
            {
                if (IsBoss)
                {
                    playerHealth.Heal(BossKillHealAmount);
                }
                if (IsMinion)
                {
                    playerHealth.Heal(MinionKillHealAmount);
                }
            }

            CombatCredit.CreditEnemyKill(attacker, IsBoss);

            if (BehaviorNpcCritical.instance != null)
            {
                BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
            }
        }

        StartCoroutine(killEnemy());
    }

    private void enemyIsDead()
    {
        // no collider identifies the killer on this path (lightning), but the player caused it,
        // so it credits the primary player - which is what this path always did
        HandleDeath(null, true);
    }

    public bool StateWalk { get => stateWalk; set => stateWalk = value; }
    public float RelativePositionToPlayer { get => relativePositionToPlayer; set => relativePositionToPlayer = value; }
    public float DistanceFromPlayer { get => distanceFromPlayer; }
    public Vector3 OriginalPosition { get => originalPosition; set => originalPosition = value; }
    public SpriteRenderer SpriteRenderer { get => spriteRenderer; set => spriteRenderer = value; }
    public bool InAttackQueue { get => inAttackQueue; set => inAttackQueue = value; }
    public Vector3 TargetPosition { get => targetPosition; set => targetPosition = value; }
    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }
    public bool IsMinion { get => isMinion; set => isMinion = value; }
    public bool IsBoss { get => isBoss; set => isBoss = value; }
    public float DistanceFromBodyGuard { get => distanceFromBodyGuard; }
    public GameObject CombatObject => gameObject;
    public Transform CombatTransform => transform;
    public bool CanAct => isActiveAndEnabled && (enemyHealth == null || !enemyHealth.IsDead);
    public static IReadOnlyCollection<EnemyController> ActiveEnemies => ActiveEnemySet;

    /// <summary>
    /// The attack queue this enemy fights against. Assigned explicitly by
    /// <see cref="EnemySpawner"/> before spawn; resolves once from <see cref="GameLevelManager"/>
    /// as a transitional fallback for enemies placed directly in a scene. STEP 1: replaces the
    /// scattered <c>GameLevelManager.instance.PlayerController1.PlayerAttackQueue</c> chains that
    /// used to appear in every method here.
    /// </summary>
    public PlayerAttackQueue TargetQueue
    {
        get
        {
            if (targetQueue == null && GameLevelManager.instance != null)
            {
                targetQueue = GameLevelManager.instance.PlayerAttackQueue;
            }

            return targetQueue;
        }
    }

    public void AssignTargetQueue(PlayerAttackQueue queue)
    {
        targetQueue = queue;
    }

    // ---- diagnostics (STEP 17) - read-only, no per-frame logging ----
    public CombatTacticalState CurrentAiState => currentAiState;
    public GameObject CurrentTarget => currentBodyguardTarget?.CombatObject;
    public float DistanceToTarget => currentBodyguardTarget != null
        ? Vector3.Distance(transform.position, currentBodyguardTarget.CombatTransform.position)
        : -1f;
    public string LastTransitionReason => lastTransitionReason;
}
