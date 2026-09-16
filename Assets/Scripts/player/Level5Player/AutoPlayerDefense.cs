using Assets.Scripts.Utility;
using Level5.Core;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.PlayerLoop;

public class AutoPlayerDefense : MonoBehaviour
{
    public PlayerIdentifier playerIdentifier;
    //private CharacterProfile cpuCharacterProfile;

    [SerializeField] Vector3 playerPosition;
    [SerializeField] float playerRelativePositioning;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private Vector3 movement;
    [SerializeField] private float playerGuardingDistance; //hustle
    [SerializeField] private float speed; // speed
    [SerializeField] private float inAirSpeed; //acceleration
    [SerializeField] private float jumpForce; //jump

    [SerializeField] private float jumpDelay; //awareness
    [SerializeField] private float delayPercent; //awareness
    [SerializeField] private float crossoverPercent; //agility
    [SerializeField] private float stamina; //stamina
    [SerializeField] private float knockDownTime = 1f; //hustle

    private Animator anim;
    private Rigidbody rigidBody;
    public float movementSpeed;
    GameObject dropShadow;

    public CpuBaseStats.DefensiveType defensiveType;

    public int blockedShots;

    private AnimatorStateInfo currentStateInfo;

    // AUD-012 Phase 2b: rim vector, ground-height fallback and the primary-human fallback registry,
    // composed by SpawnCoordinator (BindArenaContext / BindParticipantRegistry) instead of this
    // component reading GameLevelManager.instance directly - the same seams AutoPlayerController uses.
    private Vector3 bballRimVector;
    private IGroundHeightProvider groundHeightProvider;
    private bool groundHeightProviderMissingLogged;
    private PlayerRegistry participantRegistry;

    // Code review, 2026-09-12: the last value ResolveDropShadowHeight actually resolved, returned as
    // its ultimate fallback instead of a hardcoded 0f - mirrors AutoPlayerController/PlayerController's
    // own terrainYHeight field, so an unbound provider (only reachable via a composition defect) leaves
    // the shadow where it last legitimately was rather than snapping to world-space Y=0.
    private float lastResolvedDropShadowHeight;

    /// <summary>
    /// Explicit arena-context binding from <c>SpawnCoordinator.BindCpuArenaContext</c>, called once
    /// from <c>GameLevelManager.Start()</c> after arena bootstrap has resolved the final basketball rim -
    /// mirrors <c>AutoPlayerController.BindArenaContext</c>/<c>PlayerController.BindArenaContext</c>.
    /// </summary>
    public void BindArenaContext(Vector3 basketballRimVector, IGroundHeightProvider groundHeightProvider)
    {
        bballRimVector = basketballRimVector;
        this.groundHeightProvider = groundHeightProvider;
    }

    /// <summary>
    /// Explicit binding of this coordinator's participant registry, from
    /// <c>SpawnCoordinator.BindCpuParticipantRegistry</c>, called immediately at CPU registration time -
    /// replaces <see cref="ResolveGuardedPlayerIfMissing"/>'s former direct
    /// <c>GameLevelManager.instance.Player1</c> read.
    /// </summary>
    public void BindParticipantRegistry(PlayerRegistry registry)
    {
        participantRegistry = registry;
    }

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
    private float movementHorizontal;
    private float movementVertical;
    private bool FacingRight;
    public float distanceToTarget;
    public float playerDistanceToGoal;

    public bool arrivedAtTarget;
    public bool jumpTrigger;
    public bool grounded;
    public bool inAir;
    public bool isLocked;
    public bool playerCrossover;

    // DEF-2: upper bound on one shot contest, from leaving the ground to releasing isLocked. A
    // jump arc is well under a second; this only ever fires when the guarded player stops
    // reporting Grounded at all.
    private const float MaxContestDuration = 4f;

    // DEF-4: shortest interval between two crossover rolls, so rapid direction changes cannot
    // reroll it every frame.
    private const float CrossoverRollCooldown = 0.5f;
    private float nextCrossoverRollTime;

    // Start is called before the first frame update
    void Start()
    {
        ResolveGuardedPlayerIfMissing();
        //cpuCharacterProfile = gameObject.GetComponent<CharacterProfile>();
        rigidBody = GetComponent<Rigidbody>();
        movementSpeed = speed;
        anim = gameObject.GetComponentInChildren<Animator>();
        FacingRight = true;
        // AUD-081: same unguarded-lookup shape AUD-079 fixed in RacingVehicleController - this
        // used to dereference Find's result directly, so a root without a "drop_shadow" child
        // threw here and aborted the rest of Start().
        Transform dropShadowTransform = transform.Find("drop_shadow");
        if (dropShadowTransform == null)
        {
            Debug.LogError("AutoPlayerDefense on " + name + " found no 'drop_shadow' child.", this);
        }
        else
        {
            dropShadow = dropShadowTransform.gameObject;
        }

        getAnimatorStateHashes();
#if UNITY_ANDROID || UNITY_IOS
        inAirSpeed = 0;
        //jumpForce *= 0.8f;
        //speed *= 0.8f;
#endif
    }
    void FixedUpdate()
    {
        // DEF-5: `!playerCrossover` was the only condition here. The knockdown and disintegrate
        // checks its counterparts carry were absent - currently masked, because PlayerKnockedDown
        // freezes the X and Z constraints so the MovePosition below has no visible effect, but the
        // guard was missing rather than unnecessary, and DEF-1 changed how that step is computed.
        //
        // AUD-012 Phase 4 Slice 73: that "no visible effect" masking no longer holds now that
        // moveToPosition commands a persistent Rigidbody velocity instead of a one-shot MovePosition
        // step - a velocity commanded on the tick just before this guard trips would otherwise sit on
        // the Rigidbody, frozen out of position by the knockdown constraints but still present, and
        // snap the defender sideways the instant those constraints lift. This gate now releases it
        // explicitly on every suppressed tick, matching AutoPlayerController's own arrival-release
        // requirement under the same velocity-driven motor.
        if (playerCrossover
            || currentState == knockedDownState
            || currentState == disintegratedState)
        {
            ReleasePlanarVelocity();
            return;
        }

        moveToPosition(moveCpuPlayer());
    }
    // Update is called once per frame
    void Update()
    {
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // drop shadow lock to bball transform on the ground
        // AUD-052: guarded like PlayerController - no active Terrain otherwise NREs every frame
        float shadowHeight = ResolveDropShadowHeight();
        if (dropShadow != null)
        {
            dropShadow.transform.position = new Vector3(transform.root.position.x, shadowHeight, transform.root.position.z);
        }
        distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        playerDistanceToGoal = Vector3.Distance(playerPosition, bballRimVector);

        if(distanceToTarget < 0.05)
        {
            arrivedAtTarget = true;
        }
        else
        {
            arrivedAtTarget = false;
        }
        if (!arrivedAtTarget)
        {
            anim.SetBool("walking", true);
        }
        // not moving
        else
        {
            anim.SetBool("walking", false);
            anim.SetBool("moonwalking", false);
        }

        // player moving right, not facing right
        if (playerRelativePositioning > 0 && !FacingRight)//&& canMove)
        {
            Flip();
        }
        // player moving left, and facing right
        if (playerRelativePositioning < 0  && FacingRight)//&& canMove)
        {
            Flip();
        }
        if (playerIdentifier.playerController.currentState == playerIdentifier.playerController.inAirHasBasketball 
            && !inAir
            && !isLocked)
        {
            isLocked = true;
            jumpTrigger = true;
        }      
        if (jumpTrigger)
        {
            jumpTrigger = false;
            StartCoroutine( AutoPlayerJump(playerIdentifier));
        }
        if(inAir) { SetPlayerAnim("jump",true); }
        if(grounded) { SetPlayerAnim("jump",false); }

        playerRelativePositioning = playerIdentifier.player.transform.position.x - transform.position.x;
        playerPosition = playerIdentifier.player.transform.position;

        if (inAir)
        {
            movementSpeed = inAirSpeed;
        }
        else
        {
            movementSpeed = speed;
        }
        //if (!playerCrossover)
        //{
        //    moveToPosition(moveCpuPlayer());
        //}
    }


    public void SetPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }

    /// <summary>
    /// The player this defender guards. DEF-3: explicit and assignable, matching the seam
    /// <see cref="BodyGuardController.AssignProtectedActor"/> and
    /// <see cref="EnemyController.AssignTargetQueue"/> already use.
    /// </summary>
    public PlayerIdentifier GuardedPlayer => playerIdentifier;

    public void AssignGuardedPlayer(PlayerIdentifier player)
    {
        playerIdentifier = player;
    }

    /// <summary>
    /// DEF-3: this was <c>playerIdentifier = GameLevelManager.instance.players[0]</c> inline in
    /// Start - an unguarded index into a global roster, resolved once, with no assignment path.
    /// The transitional fallback still resolves the primary local human, which is the only actor
    /// the lockdown mode's defender has ever guarded; a mode that needs to guard someone else
    /// should call <see cref="AssignGuardedPlayer"/> instead of changing this.
    /// </summary>
    private void ResolveGuardedPlayerIfMissing()
    {
        if (playerIdentifier != null)
        {
            return;
        }

        playerIdentifier = participantRegistry != null
            ? participantRegistry.GetBySlot(0)
            : null;

        if (playerIdentifier == null)
        {
            Debug.LogError(
                $"AutoPlayerDefense on {name} could not resolve a player to guard; disabling.",
                this);
            enabled = false;
        }
    }

    /// <summary>
    /// The drop shadow's Y position while airborne: an active Terrain's own sampled height where one
    /// exists, else the bound <see cref="groundHeightProvider"/>'s current value. Mirrors
    /// <c>PlayerController.ResolveDropShadowHeight</c>/<c>AutoPlayerController.ResolveDropShadowHeight</c>.
    /// </summary>
    private float ResolveDropShadowHeight()
    {
        if (Terrain.activeTerrain != null)
        {
            lastResolvedDropShadowHeight = Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f;
            return lastResolvedDropShadowHeight;
        }

        if (groundHeightProvider != null)
        {
            lastResolvedDropShadowHeight = groundHeightProvider.GroundHeight + 0.02f;
            return lastResolvedDropShadowHeight;
        }

        if (!groundHeightProviderMissingLogged)
        {
            groundHeightProviderMissingLogged = true;
            Debug.LogError($"AutoPlayerDefense on {name} has no bound ground-height provider for its no-Terrain drop-shadow fallback.", this);
        }

        return lastResolvedDropShadowHeight;
    }

    Vector3 moveCpuPlayer()
    {
        // AUD-012 Phase 2b: LerpByDistance below is the only line here that ever wrote targetPosition -
        // the directionOfTravel local this replaced was computed and never read.
        targetPosition = LerpByDistance(playerPosition, bballRimVector, playerGuardingDistance);
        return targetPosition;
    }
    IEnumerator AddDelayToMove(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerCrossover = false;
    }

    public Vector3 LerpByDistance(Vector3 A, Vector3 B, float x)
    {
        Vector3 P = x * Vector3.Normalize(B - A) + A;

        return P;
    }

    /// <summary>
    /// Steps toward <paramref name="target"/> at <see cref="movementSpeed"/> units per second.
    ///
    /// DEF-1: this used to multiply the raw, un-normalized <c>(target - position)</c> vector by
    /// speed and delta time, so the step was proportional to how far away the target was -
    /// <c>speed</c> behaved as a spring constant, not a speed, and a large displacement produced a
    /// single oversized MovePosition step. This is the same defect AUD-050 fixed in
    /// <see cref="AutoPlayerController.moveToPosition"/>, which this now matches: normalize the
    /// direction, and clamp the step to the remaining distance so arriving cannot overshoot.
    ///
    /// This changes how the lockdown defender feels. The old form's effective speed was
    /// <c>speed * separation</c>, so the two agree at exactly one unit of separation; beyond that
    /// the old code was faster, inside it slower.
    ///
    /// The important difference is not close-out speed but tracking. The old form had no cap and
    /// self-corrected at any separation, settling at a lag of <c>v / speed</c> against a player
    /// moving at <c>v</c>. This form cannot exceed <c>speed</c>, so a defender slower than the
    /// player it guards falls behind without bound. The authored 6 tied the fastest ball handlers
    /// (<c>runSpeedHasBall</c> tops out at 6) and lost to a free runner (<c>runSpeed</c> 6.5), so
    /// it could neither close a gap nor hold one. It is now 10: clear of every authored player
    /// speed, and a match for the old close-out authority over the 1.5-3 unit band where a
    /// contest actually happens. Tuned by construction, not by feel - worth a Play Mode pass.
    ///
    /// AUD-012 Phase 4 Slice 73: the Rigidbody write moved from <c>rigidBody.MovePosition(rigidBody.
    /// position + movement)</c> to <see cref="RigidbodyLocomotionMotor"/>, matching
    /// <see cref="AutoPlayerController.moveToPosition"/>'s own migration. Two consequences of that
    /// move, both matching the reasoning documented there:
    ///
    /// - The direction is flattened to the X/Z plane before normalizing, rather than normalizing the
    ///   full 3D <c>(target - position)</c> and clamping the 3D distance. Under the old MovePosition
    ///   path a 3D-normalized step was harmless - <c>target</c>'s Y component (LerpByDistance's blend
    ///   between the guarded player's height and the rim's) was still physically written to Y along
    ///   with X/Z every step. <see cref="RigidbodyLocomotionMotor"/> only ever writes X/Z, so that Y
    ///   component is no longer driven by navigation at all - gravity and <see cref="AutoPlayerJump"/>
    ///   own Y now, matching every other Phase 4 role. Flattening first keeps horizontal speed exactly
    ///   <c>movementSpeed</c> regardless of the target's Y delta, rather than silently throttling it.
    /// - This method is called every gated <see cref="FixedUpdate"/> tick, not just while some
    ///   "haven't arrived yet" flag is unset (this defender has no such gate - it tracks continuously).
    ///   A one-shot <c>MovePosition</c> step produced no further displacement the instant it stopped
    ///   being called; a commanded Rigidbody velocity persists until overwritten. The near-zero-distance
    ///   case below - previously a same-tick no-op - now has to explicitly release the previously
    ///   commanded velocity, or the defender would keep sliding at its last speed while sitting on top
    ///   of its target. <see cref="ReleasePlanarVelocity"/> is shared with <see cref="FixedUpdate"/>'s
    ///   crossover/knockdown/disintegrate gate for the same reason.
    /// </summary>
    public void moveToPosition(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        Vector3 planarToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float distanceRemaining = planarToTarget.magnitude;
        if (distanceRemaining <= Mathf.Epsilon)
        {
            ReleasePlanarVelocity();
            return;
        }

        float step = Mathf.Min(movementSpeed * Time.fixedDeltaTime, distanceRemaining);
        Vector3 direction = planarToTarget / distanceRemaining;
        movement = direction * step;
        Vector3 velocity = direction * (step / Time.fixedDeltaTime);
        RigidbodyLocomotionMotor.SetPlanarVelocity(rigidBody, velocity.x, velocity.z);
    }

    /// <summary>
    /// Releases any planar (X/Z) velocity <see cref="moveToPosition"/> last commanded, leaving
    /// whatever Y velocity gravity or <see cref="AutoPlayerJump"/> already owns untouched. Shared by
    /// <see cref="moveToPosition"/>'s own zero-distance case and <see cref="FixedUpdate"/>'s
    /// crossover/knockdown/disintegrate gate - AUD-012 Phase 4 Slice 73, see
    /// <see cref="moveToPosition"/>'s doc comment for why a persistent velocity command needs an
    /// explicit release everywhere ordinary locomotion can stop.
    /// </summary>
    private void ReleasePlanarVelocity()
    {
        movement = Vector3.zero;
        RigidbodyLocomotionMotor.SetPlanarVelocity(rigidBody, 0f, 0f);
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
        // AUD-051: "base." prefix, matching every other hash here and the animator's full path
        inAirHasBasketballFrontState = Animator.StringToHash("base.inair.inair_hasBasketball_front");
        inAirHasBasketballSideState = Animator.StringToHash("base.inair.inair_hasBasketball_side");
        inAirShootState = Animator.StringToHash("base.inair.basketball_shoot");
        inAirShootFrontState = Animator.StringToHash("base.inair.basketball_shoot_front");
        jumpState = Animator.StringToHash("base.inair.jump");
        inAirHasBasketball = Animator.StringToHash("base.inair.inair_hasBasketball");
        disintegratedState = Animator.StringToHash("base.disintegrated");
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;

        // DEF-4: the chance of being crossed over is rolled here, once per sprite flip. Flips
        // happen whenever the guarded player crosses this defender's x axis, so a player who
        // jitters left and right rerolls it as fast as they can change direction - the probability
        // was effectively per-direction-change rather than per-move. The cooldown below bounds how
        // often it can be rolled without moving the roll itself, which belongs with a dribble move
        // rather than with a facing change and is a larger change than this.
        if (Time.time < nextCrossoverRollTime)
        {
            return;
        }

        nextCrossoverRollTime = Time.time + CrossoverRollCooldown;

        float randomNum = UtilityFunctions.GetRandomFloat(0, 100);
        if (randomNum < crossoverPercent && !playerCrossover && !inAir)
        {
            playerCrossover = true;
            StartCoroutine(PlayerKnockedDown());
        }
        //if (GameOptions.enemiesEnabled || GameOptions.EnemiesOnlyEnabled || GameOptions.sniperEnabled)
        //{
        //    Vector3 damageScale = damageDisplayObject.transform.localScale;
        //    damageScale.x *= -1;
        //    damageDisplayObject.transform.localScale = damageScale;
        //}
    }


    public IEnumerator PlayerKnockedDown()
    {
        //Debug.Log("PlayerKnockedDown");
        rigidBody.constraints =
        RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        anim.SetBool("knockedDown", true);
        anim.Play("knockedDown");
        //yield return new WaitUntil(() => currentState == knockedDownState); // anim started

        float startTime = Time.time;
        float endTime = startTime + knockDownTime;
        yield return new WaitUntil(() => Time.time > endTime);
        anim.SetBool("knockedDown", false);
        yield return new WaitUntil(() => currentState != knockedDownState);

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
        playerCrossover = false;
    }
    IEnumerator AutoPlayerJump(PlayerIdentifier player)
    {
        //Debug.Log("AutoPlayerJump");
        float randomNum = UtilityFunctions.GetRandomFloat(0, 100);
        if (randomNum < delayPercent && !playerCrossover)
        {
            playerCrossover = true;
        }
        // DEF-2: the two branches differed only in this pre-jump delay; the jump and the wait that
        // releases isLocked were duplicated below them. Shared now so the release path exists once.
        if (playerCrossover)
        {
            yield return new WaitForSeconds(jumpDelay);
            yield return new WaitUntil(() => currentState != knockedDownState);
            playerCrossover = false;
        }

        // AUD-012 Phase 4 Slice 73: Y-only write (was a full-vector `linearVelocity = Vector3.up *
        // jumpForce` overwrite) so the contest jump composes correctly with moveToPosition's planar
        // velocity command regardless of which runs first in a given tick - matching the same fix
        // AutoPlayerJump()/PlayerJump() already carry in AutoPlayerController/PlayerController.
        Vector3 jumpVelocity = rigidBody.linearVelocity;
        jumpVelocity.y = jumpForce;
        rigidBody.linearVelocity = jumpVelocity;
        yield return WaitForGuardedPlayerToLand(player);
        isLocked = false;
    }

    /// <summary>
    /// Waits for the guarded player to land, or for the contest to time out.
    ///
    /// DEF-2: this was an unbounded <c>WaitUntil(() =&gt; player.playerController.Grounded)</c> and
    /// was the only path that cleared <see cref="isLocked"/>. A guarded player who never reported
    /// grounded - knocked into a frozen state, disabled, destroyed, or landed somewhere the ground
    /// check misses - left the defender locked for the remainder of the match, silently never
    /// contesting another shot. The deadline is far longer than a jump arc, so a healthy contest
    /// still ends on the landing rather than on the clock.
    /// </summary>
    private IEnumerator WaitForGuardedPlayerToLand(PlayerIdentifier player)
    {
        float deadline = Time.time + MaxContestDuration;
        yield return new WaitUntil(() =>
            player == null
            || player.playerController == null
            || player.playerController.Grounded
            || Time.time >= deadline);
    }
}
