using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : MonoBehaviour, ICombatAgent
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
        if (GameOptions.hardcoreModeEnabled || GameOptions.difficultySelected == 2)
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
        if (GameOptions.customCamera)
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
        PlayerAttackQueue playerAttackQueue = GameLevelManager.instance.PlayerController1.PlayerAttackQueue;
        GameObject firstBodyGuard = playerAttackQueue.GetFirstBodyGuard();
        if (firstBodyGuard == null)
        {
            relativePositionToPlayer = GameLevelManager.instance.Player1.transform.position.x - transform.position.x;
        }
        else
        {
            relativePositionToPlayer = firstBodyGuard.transform.position.x - transform.position.x;
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
        bool enemyKilledByHit = !enemyHealth.IsDead && enemyHealth.ApplyDamage(new DamageInfo(damage));

        StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay("-"+damage.ToString()));

        stateKnockDown = true;
        FreezeEnemyPosition();
        GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
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
        //targetPosition = (GameLevelManager.instance.Player.transform.position - transform.position).normalized;

        // if no bodyguards found
        PlayerAttackQueue playerAttackQueue = GameLevelManager.instance.PlayerController1.PlayerAttackQueue;
        GameObject firstBodyGuard = playerAttackQueue.GetFirstBodyGuard();
        if (firstBodyGuard == null)
        {
            Transform attackPosition = playerAttackQueue.GetAttackPositionTransform(enemyDetection.AttackPositionId);
            if (attackPosition == null)
            {
                return;
            }

            targetPosition = (attackPosition.position - transform.position).normalized;
        }
        // if bodyguards, attack 1 first bodyguard
        else
        {
            targetPosition = (firstBodyGuard.transform.position - transform.position).normalized;
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
        PlayerAttackQueue playerAttackQueue = GameLevelManager.instance.PlayerController1.PlayerAttackQueue;
        GameObject firstBodyGuard = playerAttackQueue.GetFirstBodyGuard();
        if (firstBodyGuard == null)
        {
            distanceFromPlayer = Vector3.Distance(GameLevelManager.instance.Player1.transform.position, transform.position);
            lineOfSight = GameLevelManager.instance.Player1.transform.position.z - transform.position.z;
        }
        else
        {
            distanceFromPlayer = Vector3.Distance(firstBodyGuard.transform.position, transform.position);
            lineOfSight = firstBodyGuard.transform.position.z - transform.position.z;
        }
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

    private void enemyIsDead()
    {
        enemyHealth.IsDead = true;

        PlayerHealth playerHealth = GameLevelManager.instance != null
            ? GameLevelManager.instance.PlayerHealth
            : null;
        if (playerHealth != null && playerHealth.Health < playerHealth.MaxHealth)
        {
            if (IsBoss)
            {
                playerHealth.Heal(7);
            }
            if (IsMinion)
            {
                playerHealth.Heal(3);
            }
        }

        // no attacker is known on this path, so it credits the primary player
        CombatCredit.CreditEnemyKill(null, IsBoss);

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
    public GameObject CombatObject => gameObject;
    public Transform CombatTransform => transform;
    public bool CanAct => isActiveAndEnabled && (enemyHealth == null || !enemyHealth.IsDead);
    public static IReadOnlyCollection<EnemyController> ActiveEnemies => ActiveEnemySet;
}
