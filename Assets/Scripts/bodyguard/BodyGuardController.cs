using System;
using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Level5.Core.Match;

public class BodyGuardController : MonoBehaviour, ICombatAgent
{
    Animator anim;
    private Rigidbody rigidBody;
    BodyGuardDetection bodyGuardDetection;
    BodyGuardHealth bodyGuardHealth;
    SpriteRenderer spriteRenderer;
    PlayerSwapAttack playerSwapAttack;

    // how long after attacking the enemy can attack again
    public float attackCooldown;
    // target for enemy to move to
    [SerializeField]
    private Vector3 targetPosition;

    Vector3 movement;
    private float movementSpeed;
    public float walkMovementSpeed;
    public float runMovementSpeed;
    public float attackMovementSpeed;

    [SerializeField]
    public bool facingRight;
    [SerializeField]
    private float relativePositionToEnemy;
    [SerializeField]
    private float distanceFromPlayer;
    [SerializeField]
    private float minDistanceCloseAttack;
    [SerializeField]
    private float maxDistanceLongRangeAttack;
    [SerializeField]
    private float minDistanceLongRangeAttack;
    [SerializeField]
    bool hasLongRangeAttack;
    [SerializeField]
    private bool longRangeAttack;
    [SerializeField]
    private float knockDownTime;
    [SerializeField]
    private float takeDamageTime;

    //const string lightningAnimName = "lightning";

    private AnimatorStateInfo currentStateInfo;
    // per-instance: this is THIS bodyguard's animator state. it was static, so every
    // bodyguard read whichever one updated last - knockdown/attack gating and the
    // WaitUntil in struckByLighning could all be driven by a different bodyguard.
    int currentState;

    // the hashes are genuinely shared constants
    static readonly int AnimatorState_Attack = Animator.StringToHash("base.attack");
    static readonly int AnimatorState_Walk = Animator.StringToHash("base.walk");
    static readonly int AnimatorState_Idle = Animator.StringToHash("base.idle");
    static readonly int AnimatorState_Knockdown = Animator.StringToHash("base.knockdown");
    static readonly int AnimatorState_Lightning = Animator.StringToHash("base.lightning");
    static readonly int AnimatorState_Disintegrated = Animator.StringToHash("base.disintegrated");

    public bool stateWalk = false;
    public bool stateIdle = false;
    public bool stateAttack = false;
    public bool statePatrol = false;
    public bool stateKnockDown = false;

    public bool bodyGuardEngaged = false;

    //bool playerInLineOfSight = false;
    public float lineOfSight;
    public float lineOfSightVariance;

    public bool canAttack;
    bool inAttackQueue;

    [SerializeField]
    bool enemyUsesPhysics;
    GameObject dropShadow;

    GameObject enemyAttacking;

    Vector3 originalPosition;

    // AI architecture: explicit protected-actor assignment (STEP 1). Replaces
    // GameLevelManager.instance.players[0]/PlayerController1 lookups scattered through this
    // class - everything below resolves through this one field instead.
    [SerializeField]
    private PlayerIdentifier protectedActor;
    private PlayerAttackQueue targetQueue;

    // STEP 7/13: follow/intercept/return tuning. 0 on an existing prefab means "not configured"
    // and falls back to the defaults set in Start(), matching this file's existing convention for
    // attackCooldown/lineOfSightVariance/minDistanceCloseAttack below.
    [SerializeField]
    private float preferredFollowDistance;
    [SerializeField]
    private float protectionRadius;
    [SerializeField]
    private float maximumInterceptionDistance;
    [SerializeField]
    private float hardReturnDistance;

    // STEP 12/13: how often threat selection re-scans candidates - matches the cadence
    // EnemyController's equivalent selection already runs on (its 0.1s InvokeRepeating), so
    // neither side reevaluates who to fight every rendered frame.
    [SerializeField]
    private float decisionInterval;
    private float nextThreatRefreshTime;

    private ICombatAgent currentThreat;
    private readonly List<ICombatAgent> threatCandidateBuffer = new List<ICombatAgent>();
    private CombatTacticalState currentAiState = CombatTacticalState.Idle;
    private string lastTransitionReason = "spawn";
    public bool StateWalk { get => stateWalk; set => stateWalk = value; }
    public float RelativePositionToPlayer { get => relativePositionToEnemy; set => relativePositionToEnemy = value; }
    //public float DistanceFromPlayer { get => distanceFromPlayer; set => distanceFromPlayer = value; }
    public Vector3 OriginalPosition { get => originalPosition; set => originalPosition = value; }
    public SpriteRenderer SpriteRenderer { get => spriteRenderer; set => spriteRenderer = value; }
    public bool InAttackQueue { get => inAttackQueue; set => inAttackQueue = value; }
    public Vector3 TargetPosition { get => targetPosition; set => targetPosition = value; }
    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }

    // Use this for initialization
    void Start()
    {
        facingRight = true;
        movementSpeed = walkMovementSpeed;
        rigidBody = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bodyGuardDetection = gameObject.GetComponent<BodyGuardDetection>();
        bodyGuardHealth = gameObject.GetComponentInChildren<BodyGuardHealth>();
        Transform dropShadowTransform = transform.FindDeepChild("drop_shadow");
        dropShadow = dropShadowTransform != null ? dropShadowTransform.gameObject : null;
        originalPosition = transform.position;
        canAttack = true;

        playerSwapAttack = GetComponent<PlayerSwapAttack>();

        if (attackCooldown == 0) { attackCooldown = 1f; }
        //if (knockDownTime == 0) { knockDownTime = 2f; }
        if (lineOfSightVariance == 0) { lineOfSightVariance = 0.5f; }
        //if (takeDamageTime == 0) { takeDamageTime = 0.3f; }
        if (minDistanceCloseAttack == 0) { minDistanceCloseAttack = 0.6f; }
        if (preferredFollowDistance == 0) { preferredFollowDistance = 1.5f; }
        if (protectionRadius == 0) { protectionRadius = 4f; }
        if (maximumInterceptionDistance == 0) { maximumInterceptionDistance = 6f; }
        if (hardReturnDistance == 0) { hardReturnDistance = 9f; }
        if (decisionInterval == 0) { decisionInterval = 0.1f; }
        if (MatchRuntime.Rules.Hardcore)
        {
            movementSpeed *= 1.25f;
            attackCooldown *= 0.5f;
        }
        // try this as default
        takeDamageTime = 0.3f;

        // put enemy on the ground. some are spawning up pretty high
        gameObject.transform.position = new Vector3(gameObject.transform.position.x, 0, gameObject.transform.position.z);

        // defensive re-attempt: OnEnable's attempt can run before GameLevelManager.Awake has
        // populated the roster if script execution order puts this object first. By Start(),
        // every Awake in the scene has already run, so this is guaranteed to succeed if a
        // protected actor exists at all.
        ResolveProtectedActorIfMissing();
    }

    private void OnDisable()
    {
        ReleaseAttackReservation();
        UnregisterBodyGuard();
        currentThreat = null;
    }

    private void OnEnable()
    {
        ResolveProtectedActorIfMissing();
        RegisterBodyGuard();
        currentAiState = CombatTacticalState.Idle;
        lastTransitionReason = "enable";
        nextThreatRefreshTime = 0f;
    }

    private void FixedUpdate()
    {
        // STEP 7: stateWalk now also covers "moving to stay near/return to the protected actor",
        // not only "chasing a sighted enemy" - so it and the legacy fixed-spawn patrol below must
        // be mutually exclusive, or both could call MovePosition in the same tick.
        if (stateWalk && currentState != AnimatorState_Knockdown && currentState != AnimatorState_Disintegrated)
        {
            pursuePlayer();
        }
        else if (statePatrol)
        {
            returnToPatrol();
        }
        if (enemyUsesPhysics && dropShadow != null)
        {
            dropShadow.transform.position = new Vector3(dropShadow.transform.position.x, 0.01f, dropShadow.transform.position.z);
        }
    }

    void Update()
    {
        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // ================== bodyguard threat targeting ==========================
        PlayerAttackQueue queue = TargetQueue;
        if (queue == null)
        {
            // STEP 1: no resolvable protected actor yet - stand down safely rather than reach
            // into a null chain (this used to be an unguarded
            // GameLevelManager.instance.PlayerController1.PlayerAttackQueue.GetFirstQueuedEnemy()).
            stateIdle = true;
            stateWalk = false;
            rigidBody.linearVelocity = Vector3.zero;
            anim.SetBool("walk", false);
            return;
        }

        // STEP 2/3/12: reusable target-selection policy, scored for threat to the protected actor
        // rather than "whichever enemy queued first" - re-scored on decisionInterval, not every
        // frame, but the selected threat's live position is still tracked every frame below.
        if (Time.time >= nextThreatRefreshTime)
        {
            RefreshThreatTarget();
            nextThreatRefreshTime = Time.time + decisionInterval;
        }

        if (currentThreat != null)
        {
            enemyAttacking = currentThreat.CombatObject;
            relativePositionToEnemy = enemyAttacking.transform.position.x - transform.position.x;
        }
        else
        {
            enemyAttacking = null;
        }

        // ================== bodyguard attack state ==========================
        if (math.abs(relativePositionToEnemy) <= maxDistanceLongRangeAttack
            && math.abs(relativePositionToEnemy) >= minDistanceLongRangeAttack
            && hasLongRangeAttack
            && math.abs(lineOfSight) <= lineOfSightVariance
            && canAttack
            && enemyAttacking != null)
        {
            longRangeAttack = true;
            stateAttack = true;
        }
        else if (math.abs(relativePositionToEnemy) < minDistanceCloseAttack
            && math.abs(lineOfSight) <= lineOfSightVariance
            && !longRangeAttack
            && canAttack
            && enemyAttacking != null)
        {
            stateAttack = true;
            longRangeAttack = false;
        }
        else
        {
            stateAttack = false;
            longRangeAttack = false;
        }
        // ================== bodyguard walk state ==========================
        // STEP 7: walking is no longer gated purely on "an enemy is sighted" - a bodyguard also
        // walks to stay near, or return to, its protected actor with no threat around at all.
        stateWalk = !stateAttack
            && canAttack
            && currentState != AnimatorState_Knockdown
            && currentState != AnimatorState_Disintegrated
            && ShouldMoveForProtection();
        // ================== bodyguard idle ==========================
        if ((!canAttack || (!stateWalk && enemyAttacking == null)) && currentState != AnimatorState_Attack)
        {
            stateIdle = true;
            //if idle stop rigidbody
            rigidBody.linearVelocity = Vector3.zero;
        }
        else
        {
            stateIdle = false;
        }
        // ================== animation walk state ==========================
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
            FreezeEnemyPosition();
            if (playerSwapAttack != null && !longRangeAttack)
            {
                playerSwapAttack.setCloseAttack();
            }
            if (playerSwapAttack != null && longRangeAttack)
            {
                playerSwapAttack.setLongRangeAttack();
            }
            //Debug.Log("trigger attack");
            anim.SetTrigger("attack");
            StartCoroutine(AttackCooldown(attackCooldown));
        }
        if (relativePositionToEnemy < 0 && facingRight)
        {
            Flip();
        }
        if (relativePositionToEnemy > 0 && !facingRight)
        {
            Flip();
        }

        UpdateAiStateDiagnostics();
    }

    // STEP 2/3: candidates are every active enemy (hostility is structural - a bodyguard only
    // ever fights enemies), scored by CombatTargetSelector.SelectBodyguardThreat so an enemy
    // actively closing on the protected actor outranks one that merely holds a queue reservation,
    // which outranks one simply nearby, which outranks any other valid hostile (STEP 3's tiers).
    private void RefreshThreatTarget()
    {
        threatCandidateBuffer.Clear();
        foreach (EnemyController enemy in EnemyController.ActiveEnemies)
        {
            if (enemy != null)
            {
                threatCandidateBuffer.Add(enemy);
            }
        }

        currentThreat = CombatTargetSelector.SelectBodyguardThreat(
            threatCandidateBuffer,
            transform.position,
            protectedActor.transform.position,
            currentThreat,
            protectionRadius);
    }

    // STEP 7: default objective is staying near the protected actor; a high-priority threat
    // (one within the interception leash) is worth breaking that to intercept.
    private bool ShouldMoveForProtection()
    {
        if (protectedActor == null)
        {
            return false;
        }

        float distanceFromProtectedActor = Vector3.Distance(transform.position, protectedActor.transform.position);
        if (distanceFromProtectedActor > preferredFollowDistance)
        {
            return true;
        }

        if (currentThreat != null)
        {
            float threatDistanceFromProtectedActor = Vector3.Distance(
                protectedActor.transform.position, currentThreat.CombatTransform.position);
            return threatDistanceFromProtectedActor <= maximumInterceptionDistance;
        }

        return false;
    }

    // STEP 4/17: diagnostic label only - stateWalk/stateAttack/statePatrol/canAttack still own
    // actual behaviour. Only updates on an actual transition, so this never spams per-frame.
    private void UpdateAiStateDiagnostics()
    {
        CombatTacticalState nextState;
        if (stateAttack)
        {
            nextState = CombatTacticalState.Attack;
        }
        else if (stateKnockDown)
        {
            nextState = CombatTacticalState.Recover;
        }
        else if (currentThreat != null && stateWalk)
        {
            nextState = CombatTacticalState.InterceptThreat;
        }
        else if (stateWalk)
        {
            nextState = CombatTacticalState.ReturnToProtectedActor;
        }
        else if (currentThreat != null)
        {
            nextState = CombatTacticalState.Engage;
        }
        else
        {
            nextState = CombatTacticalState.FollowProtectedActor;
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
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezePositionX;
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
        // wait for animator state to get to attack 
        yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).IsTag("attack"));
        // wait for animation to finish
        yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("attack"));
        stateAttack = false;
        // enemy can move again
        UnFreezeEnemyPosition();
        //wait for cooldown
        yield return new WaitForSecondsRealtime(seconds);
        canAttack = true;
    }
    //void isWalking(float speed)
    //{
    //    // if moving
    //    if (speed > 0)
    //    {
    //        anim.SetBool("run", true);
    //    }
    //    else
    //    {
    //        anim.SetBool("run", false);
    //    }
    //}

    void Flip()
    {
        //Debug.Log(" Flip()");
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }

    public void setPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }
    public void playAnimation(string animationName)
    {
        anim.Play(animationName);
    }

    public IEnumerator struckByLighning()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        // AUD-053: see the same guard in EnemyController - throwing here abandoned the coroutine
        // and left the bodyguard frozen mid-knockdown
        Animator cameraFlash = SceneObjects.Find<Animator>("camera_flash", this);
        if (cameraFlash != null)
        {
            cameraFlash.Play("camera_flash");
        }

        anim.Play("lightning");
        yield return new WaitUntil(() => currentState == AnimatorState_Lightning);
        //anim.SetBool("knockdown", true);
        //yield return new WaitForSeconds(1);
        StartCoroutine(knockedDown());

        ////anim.SetBool("knockdown", true);
        //playAnimation("knockdown");
        //yield return new WaitForSeconds(knockDownTime);
        //anim.SetBool("knockdown", false);
        //stateKnockDown = false;
        //UnFreezeEnemyPosition();

        //stateKnockDown = false;
    }

    public IEnumerator knockedDown()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        anim.SetBool("knockdown", true);
        yield return new WaitUntil(() => currentState != AnimatorState_Lightning);
        playAnimation("knockdown");
        yield return new WaitForSeconds(knockDownTime);
        anim.SetBool("knockdown", false);
        stateKnockDown = false;
        UnFreezeEnemyPosition();

        stateKnockDown = false;
    }

    public IEnumerator killEnemy()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        playAnimation("disintegrated");
        yield return new WaitForSeconds(1.5f);

        if (bodyGuardDetection.Attacking)
        {
            //Debug.Log("========================== enemy killed : " + gameObject.name + " :  remove from attack queue");
            int attackPositionId = bodyGuardDetection.AttackPositionId;
            ReleaseAttackReservation(attackPositionId);
        }
        UnregisterBodyGuard();
        //yield return new WaitUntil( ()=> PlayerAttackQueue.instance.AttackSlotOpen);
        Destroy(gameObject);
    }

    public IEnumerator takeDamage()
    {
        stateKnockDown = true;

        FreezeEnemyPosition();
        //GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
        anim.SetBool("takeDamage", true);
        playAnimation("takeDamage");
        //yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("lightning"));
        //yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("knockdown"));
        yield return new WaitForSecondsRealtime(takeDamageTime);
        anim.SetBool("takeDamage", false);
        UnFreezeEnemyPosition();

        stateKnockDown = false;
        //anim.ResetTrigger("exitAnimation");
    }

    public IEnumerator disintegrated()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        playAnimation("disintegrated");
        //yield return new WaitUntil(() => currentState == AnimatorState_Disintegrated);
        // remove from body giard list in queue
        UnregisterBodyGuard();
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
        stateKnockDown = false;
    }

    //int RandomNumber(int min, int max)
    //{
    //    System.Random rnd = new System.Random();
    //    int randNum = rnd.Next(min, max);
    //    //Debug.Log("generate randNum : " + randNum);
    //    return randNum;
    //}

    public void pursuePlayer()
    {
        if (protectedActor == null)
        {
            return;
        }

        float distanceFromProtectedActor = Vector3.Distance(transform.position, protectedActor.transform.position);

        // STEP 7/8: the hard-return threshold always wins, even mid-intercept - a bodyguard must
        // not permanently chase an enemy away from the actor it protects.
        if (distanceFromProtectedActor > hardReturnDistance)
        {
            MoveToward(protectedActor.transform.position);
            return;
        }

        if (currentThreat != null)
        {
            Vector3 threatPosition = currentThreat.CombatTransform.position;
            float threatDistanceFromProtectedActor = Vector3.Distance(protectedActor.transform.position, threatPosition);
            if (threatDistanceFromProtectedActor <= maximumInterceptionDistance)
            {
                MoveToward(threatPosition);
                return;
            }
        }

        MoveToward(protectedActor.transform.position);
    }

    private void MoveToward(Vector3 destination)
    {
        targetPosition = (destination - transform.position).normalized;
        movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
        rigidBody.MovePosition(transform.position + movement);
    }
    public void returnToPatrol()
    {
        //Debug.Log(gameObject.name + "  is returning to Vector3  : " + originalPosition);
        if (Vector3.Distance(gameObject.transform.position, OriginalPosition) > 1)
        {
            targetPosition = (originalPosition - transform.position).normalized;
            movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
            //movement = targetPosition * (movementSpeed * Time.deltaTime);
            rigidBody.MovePosition(transform.position + movement);
        }
        else
        {
            statePatrol = false;
        }
    }

    private void RegisterBodyGuard()
    {
        // STEP 10: lifecycle registration (OnEnable/OnDisable/death) is the authoritative path;
        // PlayerAttackQueue.RefreshBodyGuards' FindGameObjectsWithTag fallback only covers
        // instances that existed before the queue's own Start ran.
        TargetQueue?.RegisterBodyGuard(transform.root.gameObject);
    }

    private void UnregisterBodyGuard()
    {
        // tearing down - use the resolved reference as-is rather than trying to re-resolve one
        targetQueue?.UnregisterBodyGuard(transform.root.gameObject);
    }

    private void ReleaseAttackReservation(int attackPositionId = -1)
    {
        PlayerAttackQueue queue = targetQueue;
        if (queue == null)
        {
            return;
        }

        if (attackPositionId >= 0)
        {
            queue.RemoveFromQueue(gameObject, attackPositionId);
            return;
        }

        queue.ReleaseReservation(this);
    }

    public GameObject CombatObject => gameObject;
    public Transform CombatTransform => transform;
    public bool CanAct => isActiveAndEnabled && (bodyGuardHealth == null || !bodyGuardHealth.IsDead);

    /// <summary>
    /// The actor this bodyguard protects. STEP 1: explicit, assignable - not derived from
    /// <c>GameLevelManager.instance.players[0]</c> at every call site.
    /// </summary>
    public PlayerIdentifier ProtectedActor => protectedActor;

    /// <summary>The protected actor's own attack queue - resolves once, then stays cached.</summary>
    public PlayerAttackQueue TargetQueue
    {
        get
        {
            if (targetQueue == null)
            {
                ResolveProtectedActorIfMissing();
            }

            return targetQueue;
        }
    }

    public void AssignProtectedActor(PlayerIdentifier actor)
    {
        protectedActor = actor;
        targetQueue = actor != null ? actor.GetComponent<PlayerAttackQueue>() : null;
    }

    private void ResolveProtectedActorIfMissing()
    {
        // A prefab can have protectedActor wired directly in the Inspector (the field is
        // serialized precisely so that's possible) without ever going through
        // AssignProtectedActor - so targetQueue still needs deriving from it here, not just from
        // the fallback path below. Bailing out early whenever protectedActor was already non-null
        // used to skip that derivation entirely, leaving TargetQueue permanently null for any
        // bodyguard wired this way.
        if (protectedActor == null)
        {
            if (GameLevelManager.instance == null)
            {
                return;
            }

            // Transitional fallback for bodyguards with no protected actor at all - resolves the
            // primary local human, which is the same actor every enemy in the scene currently
            // fights over (this game has one PlayerAttackQueue per match today). A future
            // multi-queue setup should assign this explicitly instead.
            protectedActor = GameLevelManager.instance.Player1;
            if (protectedActor == null)
            {
                return;
            }
        }

        targetQueue = protectedActor.GetComponent<PlayerAttackQueue>();
    }

    // ---- diagnostics (STEP 17) - read-only, no per-frame logging ----
    public CombatTacticalState CurrentAiState => currentAiState;
    public GameObject CurrentThreat => currentThreat?.CombatObject;
    public float DistanceToProtectedActor => protectedActor != null
        ? Vector3.Distance(transform.position, protectedActor.transform.position)
        : -1f;
    public float DistanceToThreat => currentThreat != null
        ? Vector3.Distance(transform.position, currentThreat.CombatTransform.position)
        : -1f;
    public string LastTransitionReason => lastTransitionReason;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredFollowDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maximumInterceptionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hardReturnDistance);

        if (protectedActor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(protectedActor.transform.position, protectionRadius);
            Gizmos.DrawLine(transform.position, protectedActor.transform.position);
        }

        if (currentThreat != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentThreat.CombatTransform.position);
        }
    }
#endif
}
