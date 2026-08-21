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
    // #57: the subset of threatCandidateBuffer close enough (to this guard or to the protected
    // actor) to be worth breaking formation for - see IsActionableThreat. Selection prefers this
    // subset; CheckReturnToPatrolStatus reads only the cached hasActionableThreat result, not this
    // buffer directly.
    private readonly List<ICombatAgent> actionableThreatBuffer = new List<ICombatAgent>();
    private bool hasActionableThreat;
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
        CancelInvoke();
        UnregisterBodyGuard();
        currentThreat = null;
        hasActionableThreat = false;
    }

    private void OnEnable()
    {
        ResolveProtectedActorIfMissing();
        RegisterBodyGuard();
        currentAiState = CombatTacticalState.Idle;
        lastTransitionReason = "enable";
        nextThreatRefreshTime = 0f;
        hasActionableThreat = false;
        // #57: patrol ownership, migrated from BodyGuardDetection.CheckReturnToPatrolStatus onto
        // this controller - cadence (3s) preserved from the original so return-to-patrol timing
        // does not change feel, only the signal it is based on.
        InvokeRepeating(nameof(CheckReturnToPatrolStatus), 0f, 3f);
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
        // BG-1: RefreshThreatTarget below dereferences protectedActor.transform unguarded, while
        // ShouldMoveForProtection and pursuePlayer both null-check the same field. That is safe
        // today only because targetQueue is always GetComponent'd off protectedActor's own
        // GameObject (see AssignProtectedActor / ResolveProtectedActorIfMissing), so the two die
        // together and the queue check below already covers it. Testing protectedActor explicitly
        // makes that invariant enforced at this one entry point rather than assumed by every
        // method downstream of it.
        PlayerAttackQueue queue = TargetQueue;
        if (queue == null || protectedActor == null)
        {
            // STEP 1: no resolvable protected actor yet - stand down safely rather than reach
            // into a null chain (this used to be an unguarded
            // GameLevelManager.instance.PlayerController1.PlayerAttackQueue.GetFirstQueuedEnemy()).
            //
            // #57: hasActionableThreat must reset here too - it is only otherwise written by
            // RefreshThreatTarget below, which this early return skips. Without this, a bodyguard
            // whose protected actor disappears mid-life (destroyed/eliminated) keeps whatever
            // hasActionableThreat value it last computed forever, since CheckReturnToPatrolStatus's
            // own InvokeRepeating keeps running independently of this method and would otherwise
            // never see it clear.
            stateIdle = true;
            stateWalk = false;
            hasActionableThreat = false;
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
    //
    // #57: also partitions candidates into the actionable subset (see IsActionableThreat) and
    // prefers it during selection - otherwise the reservation tier bonus above (500, see
    // ReservedThreatScore) can make a reserved attacker on the far side of the map outrank an
    // unreserved enemy already standing next to the protected actor (100, NearProtectedActorScore).
    // Falls back to the full candidate list when nothing is actionable, which preserves this
    // controller's prior behaviour of still tracking/facing/pursuing the best available candidate
    // even when nothing is close enough to matter yet.
    private void RefreshThreatTarget()
    {
        threatCandidateBuffer.Clear();
        actionableThreatBuffer.Clear();
        foreach (EnemyController enemy in EnemyController.ActiveEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            threatCandidateBuffer.Add(enemy);
            if (IsActionableThreat(enemy))
            {
                actionableThreatBuffer.Add(enemy);
            }
        }

        currentThreat = CombatTargetSelector.SelectBodyguardThreat(
            threatCandidateBuffer,
            actionableThreatBuffer,
            transform.position,
            protectedActor.transform.position,
            currentThreat,
            protectionRadius,
            out hasActionableThreat);
    }

    // #57: single owner of "is this threat worth breaking formation for", replacing
    // BodyGuardDetection's independent proximity scan over PlayerAttackQueue.EnemiesQueued (which
    // only ever considered reservation-holding enemies and was measured purely from this guard's
    // own position). See BodyGuardDetection.IsActionableRange for why both reference points matter.
    private bool IsActionableThreat(ICombatAgent candidate)
    {
        if (candidate == null || protectedActor == null)
        {
            return false;
        }

        // #57: the highest-priority tier (reserved and about to land a hit) is always actionable,
        // independent of this bodyguard's own authored maximumInterceptionDistance/sight - a
        // misconfigured maximumInterceptionDistance smaller than
        // CombatTargetSelector.ImminentThreatRange must not be able to filter out the one candidate
        // CombatTargetSelector's own tiering would score highest.
        if (CombatTargetSelector.IsImminentThreat(candidate, protectedActor.transform.position))
        {
            return true;
        }

        float distanceToProtectedActor = Vector3.Distance(protectedActor.transform.position, candidate.CombatTransform.position);
        float distanceToGuard = Vector3.Distance(transform.position, candidate.CombatTransform.position);
        float authoredSight = bodyGuardDetection != null ? bodyGuardDetection.EnemySightDistance : 0f;

        return BodyGuardDetection.IsActionableRange(
            distanceToProtectedActor, distanceToGuard, authoredSight, maximumInterceptionDistance);
    }

    // #57: patrol decision, migrated from BodyGuardDetection.CheckReturnToPatrolStatus. Same
    // structure as before - idle, away from the spawn point, and nothing worth fighting - but the
    // signal is now this controller's own cached hasActionableThreat (refreshed alongside
    // currentThreat on decisionInterval) instead of BodyGuardDetection's independent 0.5s scan.
    private void CheckReturnToPatrolStatus()
    {
        if (stateIdle
            && transform.position != OriginalPosition
            && !hasActionableThreat)
        {
            statePatrol = true;
        }
        else
        {
            statePatrol = false;
        }
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

    /// <summary>
    /// BG-3: bodyguards are destroyed on death where enemies are returned to
    /// <c>RuntimeObjectPool</c>. Left asymmetric on purpose. Pooling requires the actor to be able
    /// to reset every piece of per-life state on reuse - which is what
    /// <c>EnemyController.ResetForSpawn</c> exists to do, and which this class has no equivalent
    /// of - and bodyguards are placed per level rather than spawned continuously, so the churn
    /// pooling solves for enemies does not exist here.
    /// </summary>
    public IEnumerator killEnemy()
    {
        stateKnockDown = true;
        FreezeEnemyPosition();
        playAnimation("disintegrated");
        yield return new WaitForSeconds(1.5f);

        // #57: no reservation release here - bodyguards never hold a PlayerAttackQueue reservation
        // (only EnemyDetection calls TryAddToQueue/TryReserve), so BodyGuardDetection no longer
        // carries reservation state to check.
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

    public GameObject CombatObject => gameObject;
    public Transform CombatTransform => transform;
    public bool CanAct => isActiveAndEnabled && (bodyGuardHealth == null || !bodyGuardHealth.IsDead);

    /// <summary>
    /// The actor this bodyguard protects. STEP 1: explicit, assignable - not derived from
    /// <c>GameLevelManager.instance.players[0]</c> at every call site.
    /// </summary>
    public PlayerIdentifier ProtectedActor => protectedActor;

    /// <summary>
    /// The range, measured from the protected actor, at which this bodyguard already considers a
    /// threat worth breaking formation to intercept. #57: also one of the two reference points
    /// <see cref="IsActionableThreat"/> uses (via <see cref="BodyGuardDetection.IsActionableRange"/>)
    /// so patrol/actionability agrees with the interception behaviour this value already drives in
    /// <see cref="ShouldMoveForProtection"/>/<see cref="pursuePlayer"/>.
    /// </summary>
    public float MaximumInterceptionDistance => maximumInterceptionDistance;

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
