using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
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

    private AnimatorStateInfo currentStateInfo;
    [SerializeField]
    static int currentState;
    [SerializeField]
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

    // Use this for initialization
    void Start()
    {
        facingRight = true;
        canAttack = true;

        spriteObject = transform.GetComponentInChildren<SpriteRenderer>().gameObject;
        rigidBody = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        enemyDetection = gameObject.GetComponent<EnemyDetection>();
        enemyHealthBar = gameObject.GetComponentInChildren<EnemyHealthBar>();
        enemyHealth = gameObject.GetComponentInChildren<EnemyHealth>();
        damageDisplayObject = transform.FindDeepChild("enemy_damage_display_text").gameObject;
        playerSwapAttack = GetComponent<PlayerSwapAttack>();

        originalPosition = transform.position;
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
        if (GameOptions.hardcoreModeEnabled || GameOptions.difficultySelected == 2)
        {
            // +25% speed
            movementSpeed *= 1.25f;
            // 50% reduction in attack cooldown 
            attackCooldown *= 0.5f;
        }
        // for enemy damagae display over head
        if (damageDisplayObject.GetComponent<Canvas>() != null)
        {
            damageDisplayObject.transform.parent.GetComponent<Canvas>().worldCamera = Camera.main;
        }
        // if level has custom level specific camera
        if (GameOptions.customCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            enemyHealthBar.gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        // put enemy on the ground. some are spawning up pretty high
        //gameObject.transform.position = new Vector3(gameObject.transform.position.x, GameLevelManager.instance.TerrainHeight, gameObject.transform.position.z);

        InvokeRepeating("UpdateDistanceFromPlayer", 0, 0.1f);
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
        if (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards.Count == 0 && !GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuardEngaged)
        {
            relativePositionToPlayer = GameLevelManager.instance.Player1.transform.position.x - transform.position.x;
        }
        else
        {
            relativePositionToPlayer = GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards[0].transform.position.x - transform.position.x;
        }

        // ================== enemy idle ==========================
        if ((/*GameLevelManager.instance.PlayerController.KnockedDown*/
            !canAttack
            || !enemyDetection.PlayerSighted)
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
        if (enemyDetection.PlayerSighted
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
        currentState = AnimatorState_Idle;
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

        if ((GameOptions.enemiesEnabled
            || GameOptions.EnemiesOnlyEnabled
            || GameOptions.sniperEnabled
            || GameOptions.battleRoyalEnabled)
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
        enemyHealth.Health -= damage;
        enemyHealthBar.setHealthSliderValue();

        StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay("-"+damage.ToString()));

        stateKnockDown = true;
        FreezeEnemyPosition();
        GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
        anim.Play("lightning");
        yield return new WaitUntil(() => currentState == AnimatorState_Lightning);

        if (enemyHealth.Health <= 0 && !enemyHealth.IsDead)
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
            GameLevelManager.instance.PlayerController1.PlayerAttackQueue.removeEnemyFromQueue(gameObject, attackPositionId);
        }
        Destroy(gameObject);
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
        Destroy(gameObject);
        stateKnockDown = false;
    }

    public void pursueTarget()
    {
        //targetPosition = (GameLevelManager.instance.Player.transform.position - transform.position).normalized;

        // if no bodyguards found
        if (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards.Count == 0 && !GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuardEngaged)
        {
            targetPosition = (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.AttackPositions[enemyDetection.AttackPositionId].transform.position - transform.position).normalized;
        }
        // if bodyguards, attack 1 first bodyguard
        else
        {
            targetPosition = (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards[0].transform.position - transform.position).normalized;
        }
        movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
        //movement = targetPosition * (movementSpeed * Time.deltaTime);
        rigidBody.MovePosition(transform.position + movement);
        //transform.Translate(movement);
        //Debug.Log(gameObject.transform.root.name + " -- currentSpeed : " + currentSpeed);
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
        if (GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards.Count == 0 && !GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuardEngaged)
        {
            distanceFromPlayer = Vector3.Distance(GameLevelManager.instance.Player1.transform.position, transform.position);
            lineOfSight = GameLevelManager.instance.Player1.transform.position.z - transform.position.z;
        }
        else
        {
            distanceFromPlayer = Vector3.Distance(GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards[0].transform.position, transform.position);
            lineOfSight = GameLevelManager.instance.PlayerController1.PlayerAttackQueue.BodyGuards[0].transform.position.z - transform.position.z;
        }
    }

    private void enemyIsDead()
    {
        enemyHealth.IsDead = true;

        if (GameLevelManager.instance.PlayerHealth.Health < GameLevelManager.instance.PlayerHealth.MaxHealth)
        {
            if (IsBoss)
            {
                GameLevelManager.instance.PlayerHealth.Heal(7);
            }
            if (IsMinion)
            {
                GameLevelManager.instance.PlayerHealth.Heal(3);
            }
        }
        BasketBall.instance.GameStats.EnemiesKilled++;
        if (IsBoss)
        {

            BasketBall.instance.GameStats.BossKilled++;
        }
        else
        {
            BasketBall.instance.GameStats.MinionsKilled++;
        }
        if (BehaviorNpcCritical.instance != null)
        {
            BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
        }
        StartCoroutine(killEnemy());
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
}
