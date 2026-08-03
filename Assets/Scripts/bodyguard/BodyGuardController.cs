using System;
using Assets.Scripts.Utility;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

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
    static int currentState;
    static int AnimatorState_Attack = Animator.StringToHash("base.attack");
    static int AnimatorState_Walk = Animator.StringToHash("base.walk");
    static int AnimatorState_Idle = Animator.StringToHash("base.idle");
    static int AnimatorState_Knockdown = Animator.StringToHash("base.knockdown");
    static int AnimatorState_Lightning = Animator.StringToHash("base.lightning");
    static int AnimatorState_Disintegrated = Animator.StringToHash("base.disintegrated");

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
        if (GameOptions.hardcoreModeEnabled)
        {
            movementSpeed *= 1.25f;
            attackCooldown *= 0.5f;
        }
        // try this as default
        takeDamageTime = 0.3f;

        // put enemy on the ground. some are spawning up pretty high
        gameObject.transform.position = new Vector3(gameObject.transform.position.x, 0, gameObject.transform.position.z);

        //InvokeRepeating("UpdateDistanceFromPlayer", 0, 0.1f);
    }

    private void OnDisable()
    {
        ReleaseAttackReservation();
        UnregisterBodyGuard();
    }

    private void OnEnable()
    {
        RegisterBodyGuard();
    }

    private void FixedUpdate()
    {
        if (stateWalk && currentState != AnimatorState_Knockdown && currentState != AnimatorState_Disintegrated)
        //&& bodyGuardDetection.Attacking)
        {
            pursuePlayer();
        }
        if (statePatrol)
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
        // ================== enemy facing player ==========================
        //relativePositionToPlayer = GameLevelManager.instance.Player.transform.position.x - transform.position.x;
        GameObject firstQueuedEnemy = GameLevelManager.instance.PlayerController1.PlayerAttackQueue.GetFirstQueuedEnemy();
        if (firstQueuedEnemy != null)
        {
            enemyAttacking = firstQueuedEnemy;
            //Debug.Log("enemy to attack : " + enemyAttacking);
            relativePositionToEnemy = enemyAttacking.transform.position.x - transform.position.x;
        }

        // ================== enemy idle ==========================
        //if ((GameLevelManager.instance.PlayerState.KnockedDown
        if ((!canAttack
            || !bodyGuardDetection.EnemySighted)
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
        // ================== enemy walk state ==========================
        if (bodyGuardDetection.EnemySighted
            && !stateAttack
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
        GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
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
        //targetPosition = (GameLevelManager.instance.Player.transform.position - transform.position).normalized;
        //targetPosition = (PlayerAttackQueue.instance.AttackPositions[enemyDetection.AttackPositionId].transform.position - transform.position).normalized;
        GameObject enemyAttackingPlayer = GameLevelManager.instance.PlayerController1.PlayerAttackQueue.GetFirstQueuedEnemy();
        if (enemyAttackingPlayer != null)
        {
            targetPosition = (enemyAttackingPlayer.transform.position - transform.position).normalized;
            //Debug.Log("enemyAttackingPlayer : " + enemyAttackingPlayer);
            movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
            //movement = targetPosition * (movementSpeed * Time.deltaTime);
            rigidBody.MovePosition(transform.position + movement);
            //transform.Translate(movement);
        }
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
        if (GameLevelManager.instance == null
            || GameLevelManager.instance.PlayerController1 == null
            || GameLevelManager.instance.PlayerController1.PlayerAttackQueue == null)
        {
            return;
        }

        GameLevelManager.instance.PlayerController1.PlayerAttackQueue.RegisterBodyGuard(transform.root.gameObject);
    }

    private void UnregisterBodyGuard()
    {
        if (GameLevelManager.instance == null
            || GameLevelManager.instance.PlayerController1 == null
            || GameLevelManager.instance.PlayerController1.PlayerAttackQueue == null)
        {
            return;
        }

        GameLevelManager.instance.PlayerController1.PlayerAttackQueue.UnregisterBodyGuard(transform.root.gameObject);
    }

    private void ReleaseAttackReservation(int attackPositionId = -1)
    {
        if (GameLevelManager.instance == null
            || GameLevelManager.instance.PlayerController1 == null
            || GameLevelManager.instance.PlayerController1.PlayerAttackQueue == null)
        {
            return;
        }

        if (attackPositionId >= 0)
        {
            GameLevelManager.instance.PlayerController1.PlayerAttackQueue.RemoveFromQueue(gameObject, attackPositionId);
            return;
        }

        GameLevelManager.instance.PlayerController1.PlayerAttackQueue.ReleaseReservation(this);
    }

    public GameObject CombatObject => gameObject;
    public Transform CombatTransform => transform;
    public bool CanAct => isActiveAndEnabled && (bodyGuardHealth == null || !bodyGuardHealth.IsDead);

    //public void UpdateDistanceFromPlayer()
    //{
    //    distanceFromPlayer = Vector3.Distance(GameLevelManager.instance.Player.transform.position, transform.position);
    //    lineOfSight = GameLevelManager.instance.Player.transform.position.z - transform.position.z;
    //}
}
