//using System.Collections;
//using System.Collections.Generic;
//using Unity.Mathematics;
//using UnityEngine;
//using Random = UnityEngine.Random;

//public class AutoPlayerControllerTest : MonoBehaviour
//{
//    private Animator anim;
//    [SerializeField]
//    private Rigidbody rigidBody;
//    private SpriteRenderer spriteRenderer;
//    // target for enemy to move to
//    [SerializeField]
//    private Vector3 targetPosition;
//   [SerializeField]
//    public bool targetCreated;
//    [SerializeField]
//    GameObject prefabMarkerToInstantiate;
//    [SerializeField]
//    GameObject basketballRim;

//    Vector3 movement;
//    private float movementSpeed;
//    public float walkMovementSpeed;
//    public float runMovementSpeed;
//    public float attackMovementSpeed;

//    // constant values that have to be hardcoded
//    private const float threePointDistance = 3.8f;
//    private const float fourPointDistance = 6.4f;
//    private const float sevenPointDistance = 16.7f;

//    [SerializeField]
//    public bool facingRight;
//    // how long after attacking the enemy can attack again
//    public float attackCooldown;
//    [SerializeField]
//    private float relativePositionToGoal;
//    [SerializeField]
//    private float knockDownTime;
//    [SerializeField]
//    private float takeDamageTime;
//    [SerializeField]
//    bool isMinion;
//    [SerializeField]
//    bool isBoss;

//    [SerializeField]
//    bool hasBall;

 

//    //const string lightningAnimName = "lightning";

//    private AnimatorStateInfo currentStateInfo;
//    static int currentState;
//    static int AnimatorState_Attack = Animator.StringToHash("base.attack");
//    static int AnimatorState_Walk = Animator.StringToHash("base.walk");
//    static int AnimatorState_Idle = Animator.StringToHash("base.idle");
//    static int AnimatorState_Knockdown = Animator.StringToHash("base.knockdown");
//    static int AnimatorState_Lightning = Animator.StringToHash("base.lightning");
//    static int AnimatorState_Disintegrated = Animator.StringToHash("base.disintegrated");

//    public bool stateWalk = false;
//    public bool stateIdle = false;
//    //public bool stateAttack = false;
//    //public bool statePatrol = false;
//    public bool stateKnockDown = false;

//    [SerializeField]
//    bool enemyUsesPhysics;
//    [SerializeField]
//    GameObject dropShadow;

//    Vector3 randomShootingPosition;

//    public float jumpForce = 4f;
//    bool jumptrigger = false;

//    public bool HasBall { get => hasBall; set => hasBall = value; }

//    // Use this for initialization
//    void Start()
//    {
//        facingRight = true;
//        movementSpeed = walkMovementSpeed;
//        rigidBody = GetComponent<Rigidbody>();
//        anim = GetComponentInChildren<Animator>();
//        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

//        if (isMinion)
//        {
//            attackCooldown = 1.5f;
//            walkMovementSpeed = 1.25f;
//            runMovementSpeed = 1.6f;
//            takeDamageTime = 0.4f;
//        }
//        if (isBoss)
//        {
//            attackCooldown = 1.2f;
//            walkMovementSpeed = 1.5f;
//            runMovementSpeed = 2f;
//            takeDamageTime = 0.3f;
//        }

//        basketballRim = GameObject.Find("rim");
//        // put enemy on the ground. some are spawning up pretty high
//        gameObject.transform.position = new Vector3(gameObject.transform.position.x, 0, gameObject.transform.position.z);
//    }

//    private void FixedUpdate()
//    {
//        if (stateWalk 
//            && currentState != AnimatorState_Knockdown 
//            && currentState != AnimatorState_Disintegrated)
//        {
//            goToShootingPosition();
//        }

//        if (enemyUsesPhysics)
//        {
//            dropShadow.transform.position = new Vector3(dropShadow.transform.position.x, 0.01f, dropShadow.transform.position.z);
//        }
//        if (jumptrigger)
//        {
//            jumptrigger = false;
//            AutoPlayerJump();
//        }
//    }

//    void Update()
//    {
//        //currentSpeed = rigidBody.velocity.magnitude;

//        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
//        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
//        currentState = currentStateInfo.fullPathHash;
//        // ================== enemy facing goal ==========================

//        relativePositionToGoal = GameLevelManager.instance.BasketballRimVector.x + transform.position.x;

//        //if (PlayerAttackQueue.instance.BodyGuards.Count == 0 && !PlayerAttackQueue.instance.BodyGuardEngaged)
//        //{
//        //    //relativePositionToPlayer = GameLevelManager.instance.Player.transform.position.x - transform.position.x;
//        //    relativePositionToGoal = GameLevelManager.instance.BasketballRimVector.x - transform.position.x;
//        //}
//        //else
//        //{
//        //    relativePositionToPlayer = PlayerAttackQueue.instance.BodyGuards[0].transform.position.x - transform.position.x;
//        //}

//        // ================== enemy idle ==========================
//        //if (currentState != AnimatorState_Attack)
//        //{
//        //    stateIdle = true;
//        //    //if idle stop rigidbody
//        //    rigidBody.velocity = Vector3.zero;
//        //}
//        //else
//        //{
//        //    stateIdle = false;
//        //}

//        //// if ball is in air, enemy idle
//        //if (GameLevelManager.instance.Basketball.BasketBallState.InAir
//        //    && currentState != AnimatorState_Knockdown
//        //    && currentState != AnimatorState_Walk
//        //    && currentState != AnimatorState_Attack
//        //    && currentState != AnimatorState_Lightning)
//        //{
//        //    stateIdle = true;
//        //    //if idle stop rigidbody
//        //    rigidBody.velocity = Vector3.zero;
//        //}
//        //else
//        //{
//        //    stateIdle = false;
//        //}
//        // ================== if does not have ball ==========================
//        if (!hasBall
//            && currentState != AnimatorState_Knockdown
//            && currentState != AnimatorState_Disintegrated)
//        {
//            Debug.Log("CPU call ball");
//            CallBallToPlayer.instance.Locked = true;
//            CallBallToPlayer.instance.pullBallToPlayer();
//            CallBallToPlayer.instance.Locked = false;
//        }

//        // ================== enemy walk state ==========================
//        if ( !stateIdle
//            && currentState != AnimatorState_Knockdown
//            && currentState != AnimatorState_Disintegrated)
//        {
//            Debug.Log("stateWalk : true");
//            stateWalk = true;
//        }
//        else
//        {
//            Debug.Log("stateWalk : false");
//            stateWalk = false;
//        }
//        // ================== animation walk state ==========================
//        //if (rigidBody.velocity.sqrMagnitude > 0)
//        if (stateWalk)//|| statePatrol)
//        {
//            Debug.Log("if(statewalk)");
//            anim.SetBool("walking", true);
//            // generate position
//            if (!targetCreated) 
//            {
//                generateRandomShootingPosition();
//            }
//            // move to position
//            // check if arrived at position
//            // -- if arrived, statewalk = false
//            // ---- stateShoot = true
//        }
//        else
//        {
//            anim.SetBool("walking", false);
//        }

//        if (relativePositionToGoal < 0 && !facingRight)
//        {
//            Flip();
//        }
//        if (relativePositionToGoal > 0 && facingRight)
//        {
//            Flip();
//        }
//    }

//    public void FreezeEnemyPosition()
//    {
//        if (enemyUsesPhysics)
//        {
//            rigidBody.velocity = Vector3.zero;
//            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
//                | RigidbodyConstraints.FreezeRotationZ
//                | RigidbodyConstraints.FreezeRotationY
//                | RigidbodyConstraints.FreezePositionZ;
//        }
//        else
//        {
//            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
//                | RigidbodyConstraints.FreezeRotationZ
//                | RigidbodyConstraints.FreezeRotationY
//                | RigidbodyConstraints.FreezePositionY
//                | RigidbodyConstraints.FreezePositionZ
//                | RigidbodyConstraints.FreezePositionX;
//        }
//    }

//    public void UnFreezeEnemyPosition()
//    {
//        if (enemyUsesPhysics)
//        {
//            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
//                | RigidbodyConstraints.FreezeRotationZ
//                | RigidbodyConstraints.FreezeRotationY;
//        }
//        else
//        {
//            rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
//                | RigidbodyConstraints.FreezeRotationZ
//                | RigidbodyConstraints.FreezeRotationY
//                | RigidbodyConstraints.FreezePositionY;
//        }
//    }

//    void Flip()
//    {
//        //Debug.Log(" Flip()");
//        facingRight = !facingRight;
//        Vector3 thisScale = transform.localScale;
//        thisScale.x *= -1;
//        transform.localScale = thisScale;
//    }

//    public void setPlayerAnim(string animationName, bool isTrue)
//    {
//        anim.SetBool(animationName, isTrue);
//    }
//    public void playAnimation(string animationName)
//    {
//        anim.Play(animationName);
//    }

//    public void AutoPlayerJump()
//    {
//        Debug.Log("player jump");
//        rigidBody.velocity = Vector3.up * jumpForce; //+ (Vector3.forward * rigidBody.velocity.x)) 
//        //jumpStartTime = Time.time;

//        //Shotmeter.MeterStarted = true;
//        //Shotmeter.MeterStartTime = Time.time;
//        //// if not dunking, start shot meter
//        //if (currentState != inAirDunkState)
//        //{
//        //    Shotmeter.MeterStarted = true;
//        //    Shotmeter.MeterStartTime = Time.time;
//        //}
//    }

//    public IEnumerator struckByLighning()
//    {
//        stateKnockDown = true;
//        FreezeEnemyPosition();
//        GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
//        anim.Play("lightning");
//        yield return new WaitUntil(() => currentState == AnimatorState_Lightning);
//        //anim.SetBool("knockdown", true);
//        //yield return new WaitForSeconds(1);
//        StartCoroutine(knockedDown());

//        ////anim.SetBool("knockdown", true);
//        //playAnimation("knockdown");
//        //yield return new WaitForSeconds(knockDownTime);
//        //anim.SetBool("knockdown", false);
//        //stateKnockDown = false;
//        //UnFreezeEnemyPosition();

//        //stateKnockDown = false;
//    }

//    public IEnumerator knockedDown()
//    {
//        stateKnockDown = true;
//        FreezeEnemyPosition();
//        anim.SetBool("knockdown", true);
//        yield return new WaitUntil(() => currentState != AnimatorState_Lightning);
//        playAnimation("knockdown");
//        yield return new WaitForSeconds(knockDownTime);
//        anim.SetBool("knockdown", false);
//        stateKnockDown = false;
//        UnFreezeEnemyPosition();

//        stateKnockDown = false;
//    }

//    public IEnumerator killEnemy()
//    {
//        stateKnockDown = true;
//        FreezeEnemyPosition();
//        playAnimation("disintegrated");
//        yield return new WaitForSeconds(1.5f);
//        //yield return new WaitUntil( ()=> PlayerAttackQueue.instance.AttackSlotOpen);
//        Destroy(gameObject);
//    }

//    public IEnumerator takeDamage()
//    {
//        stateKnockDown = true;

//        FreezeEnemyPosition();
//        //GameObject.Find("camera_flash").GetComponent<Animator>().Play("camera_flash");
//        anim.SetBool("takeDamage", true);
//        playAnimation("takeDamage");
//        //yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("lightning"));
//        //yield return new WaitUntil(() => !anim.GetCurrentAnimatorStateInfo(0).IsTag("knockdown"));
//        yield return new WaitForSecondsRealtime(takeDamageTime);
//        anim.SetBool("takeDamage", false);
//        UnFreezeEnemyPosition();

//        stateKnockDown = false;
//        //anim.ResetTrigger("exitAnimation");
//    }

//    public IEnumerator disintegrated()
//    {
//        stateKnockDown = true;
//        FreezeEnemyPosition();
//        playAnimation("disintegrated");
//        //yield return new WaitUntil(() => currentState == AnimatorState_Disintegrated);
//        yield return new WaitForSeconds(1.5f);
//        Destroy(gameObject);
//        stateKnockDown = false;
//    }

//    public void goToShootingPosition()
//    {
//        Debug.Log("goToShootingPosition()");

//        targetPosition = (randomShootingPosition - transform.position).normalized;
//        movement = targetPosition * (movementSpeed * Time.deltaTime);
//        rigidBody.MovePosition(transform.position + movement);
//    }

//    public void AutoPlayerArrivedAtMarker()
//    {
//        Debug.Log("");
//        stateWalk = false;
//        stateIdle = true;
//        jumptrigger = true;
//    }

//    private void generateRandomShootingPosition()
//    {

//        Debug.Log("-----generateRandomShootingPosition()");
//        randomShootingPosition = GetRandomFourPointPosition(relativePositionToGoal);
//        targetPosition = randomShootingPosition;

//        GameObject prefabClone =
//        Instantiate(prefabMarkerToInstantiate, randomShootingPosition, Quaternion.Euler(new Vector3(-90, 0, 0)));
//        // set parent to object with vertical layout
//        //prefabClone.transform.SetParent(basketballRim.transform, false);
//        //Destroy(prefabClone.gameObject, 3);
//        //return 0.0f;
//        targetCreated = true;
//    }

//    private Vector3 GetRandomThreePointPosition(float relativePos)
//    {
//        //// get random side of basketball goal (left or right, viewed head on)
//        //List<int> list = new List<int> { 1, -1 };
//        //int finder = Random.Range(0, 2); //Then you just use this; nameDisplayString = names[finder];
//        //int randomXDirection = list[finder];

//        // get basketball goal posiiton
//        Vector3 rimVectorOnGround = basketballRim.transform.position;
//        // set to ground (Y vector)
//        rimVectorOnGround.y = 0.0f;
//        // get random radius between 3 and 4 point line
//        float randomRadius = (Random.Range(threePointDistance + 0.5f, fourPointDistance - 0.8f));
//        // random angle
//        float randomAngle = 0;
//        // get X, Z points for vector
//        float x = 0;
//        // generate position on same side of goal as shooter
//        if (relativePos < 0)
//        {
//            // between pi - 3pi/2
//            randomAngle = Random.Range(math.PI, 1.5f * math.PI);
//            x = (math.cos(randomAngle) * randomRadius) + rimVectorOnGround.x;
//        }
//        if (relativePos > 0)
//        {
//            // between  3pi/2 - 2pi
//            randomAngle = Random.Range( 1.5f * math.PI, 2 * math.PI);
//            x = math.cos(randomAngle) * randomRadius + rimVectorOnGround.x;
//        }
//        float z = math.sin(randomAngle) * randomRadius + rimVectorOnGround.z;

//        Vector3 randomShootingPosition = new Vector3(x, 0, z);
//        randomShootingPosition.y = 0.01f;

//        return randomShootingPosition;
//    }

//    private Vector3 GetRandomFourPointPosition(float relativePos)
//    {
//        //// get random side of basketball goal (left or right, viewed head on)
//        //List<int> list = new List<int> { 1, -1 };
//        //int finder = Random.Range(0, 2); //Then you just use this; nameDisplayString = names[finder];
//        //int randomXDirection = list[finder];

//        // get basketball goal posiiton
//        Vector3 rimVectorOnGround = basketballRim.transform.position;
//        // set to ground (Y vector)
//        rimVectorOnGround.y = 0.0f;
//        // get random radius between 3 and 4 point line
//        float randomRadius = (Random.Range(fourPointDistance, fourPointDistance + 1f));
//        //Debug.Log("randomRadius : " + randomRadius);
//        // random angle
//        float randomAngle = 0;
//        // get X, Z points for vector
//        float x = 0;
//        // generate position on same side of goal as shooter
//        if (relativePos < 0)
//        {
//            // between pi - 3pi/2
//            randomAngle = Random.Range(math.PI, 1.5f * math.PI);
//            x = (math.cos(randomAngle) * randomRadius) + rimVectorOnGround.x;
//        }
//        if (relativePos > 0)
//        {
//            // between  3pi/2 - 2pi
//            randomAngle = Random.Range(1.5f * math.PI, 2 * math.PI);
//            x = math.cos(randomAngle) * randomRadius + rimVectorOnGround.x;
//        }
//        float z = math.sin(randomAngle) * randomRadius + rimVectorOnGround.z;

//        Vector3 randomShootingPosition = new Vector3(x, 0, z);
//        randomShootingPosition.y = 0.01f;

//        return randomShootingPosition;
//    }
//}
