using System.Collections;
using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource;
    const string attackBoxText = "attackBox";
    const string attackBoxSpecialText = "attackBoxSpecial";
    const string hitboxBoxText = "hitbox";

    [SerializeField]
    GameObject attackBox;
    [SerializeField]
    GameObject attackBoxSpecial;
    [SerializeField]
    GameObject hitBox;
    [SerializeField]
    GameObject projectileLaserPrefab;
    [SerializeField]
    GameObject projectileBulletPrefab;
    [SerializeField]
    GameObject projectileAutomaticBulletPrefab;
    [SerializeField]
    GameObject projectileMolotovPrefab;
    [SerializeField]
    GameObject projectileRabbitPrefab;
    [SerializeField]
    GameObject projectileRocketPrefab;
    [SerializeField]
    GameObject projectileCigarettePrefab;
    [SerializeField]
    GameObject projectileSpawn;
    [SerializeField]
    CapsuleCollider capsuleCollider;

    bool attackBoxEnabled;
    bool attackBoxSpecialEnabled;

    Animator animOnCamera;
    PlayerController playerController;

    private bool hitBoxEnabled;

    private void Start()
    {
        Transform projectileSpawnTransform = transform.Find("projectileSpawn");
        if (projectileSpawnTransform != null)
        {
            projectileLaserPrefab = LoadProjectilePrefab(projectileLaserPrefab, "Prefabs/projectile/projectile_laser_player");
            projectileBulletPrefab = LoadProjectilePrefab(projectileBulletPrefab, "Prefabs/projectile/projectile_bullet_player");
            projectileAutomaticBulletPrefab = LoadProjectilePrefab(projectileAutomaticBulletPrefab, "Prefabs/projectile/projectile_automatic_bullet");
            projectileMolotovPrefab = LoadProjectilePrefab(projectileMolotovPrefab, "Prefabs/projectile/projectile_molotov");
            projectileRocketPrefab = LoadProjectilePrefab(projectileRocketPrefab, "Prefabs/projectile/projectile_rocket");
            projectileRabbitPrefab = LoadProjectilePrefab(projectileRabbitPrefab, "Prefabs/projectile/projectile_rabbit");
            projectileCigarettePrefab = LoadProjectilePrefab(projectileCigarettePrefab, "Prefabs/projectile/projectile_cigarette");
            projectileSpawn = projectileSpawnTransform.gameObject;
        }
        capsuleCollider = transform.root.GetComponent<CapsuleCollider>();

        // resolve the actor this component is actually attached to, the way EnemyAnimationEvents
        // and BodyGuardAnimationEvents do. reading players[0] meant every player's animation
        // events gated on player 1's animator state and applied lunge force to player 1's
        // rigidbody - invisible in single player, wrong for anyone but player 1.
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            playerController = transform.root.GetComponentInChildren<PlayerController>();
        }

        audioSource = GetComponent<AudioSource>();

        Transform attackBoxTransform = transform.Find(attackBoxText);
        if (attackBoxTransform != null)
        {
            attackBox = attackBoxTransform.gameObject;
            disableAttackBox();
        }
        else
        {
            attackBox = null;
        }
        Transform attackBoxSpecialTransform = transform.Find(attackBoxSpecialText);
        if (attackBoxSpecialTransform != null)
        {
            attackBoxSpecial = attackBoxSpecialTransform.gameObject;
            disableAttackBoxSpecial();
        }
        else
        {
            attackBoxSpecial = null;
        }
        Transform hitBoxTransform = gameObject.transform.parent.Find(hitboxBoxText);
        if (hitBoxTransform != null)
        {
            hitBox = hitBoxTransform.gameObject;
        }
        else
        {
            hitBox = null;
        }
        GameObject cameraFlash = GameObject.Find("camera_flash");
        if (cameraFlash != null && cameraFlash.TryGetComponent(out Animator cameraFlashAnimator))
        {
            animOnCamera = cameraFlashAnimator;
        }
        else
        {
            animOnCamera = null;
        }
        // CHR-1: every cheerleader prefab carries this component, because it is what receives the
        // playSfxCameraFlash animation event on jessica_critical_success.anim. A cheerleader has
        // no PlayerController and no attack boxes, so the unconditional LogError above fired for a
        // condition that is correct and expected - a guaranteed red error in every match that
        // spawned one, which trains the error log to be ignored.
        //
        // An instance with none of the player-driven children is being used purely as a sound
        // host, and has nothing to report or to police. An instance that owns any of them and has
        // no controller really is misconfigured, and still says so.
        bool drivesPlayerColliders = attackBox != null || attackBoxSpecial != null || hitBox != null;
        if (playerController == null)
        {
            if (drivesPlayerColliders)
            {
                Debug.LogError(
                    "PlayerAnimationEvents on " + name + " found no PlayerController on its hierarchy; "
                    + "attack boxes and lunge events will not run.",
                    this);
            }

            return;
        }

        // check if attack box is active and should not be
        if (GameLevelManager.instance != null && !GameLevelManager.instance.AutoPlayer)
        {
            InvokeRepeating("checkCollidersDisabledProperly", 0, 1);
        }
    }

    // function - Invoke Repeating
    private void checkCollidersDisabledProperly()
    {
        if (playerController == null)
        {
            return;
        }

        if (playerController.CurrentState != playerController.AttackState
            && playerController.CurrentState != playerController.SpecialState
            && playerController.CurrentState != playerController.dunkState
            && attackBoxEnabled)
        {
            disableAttackBox();
        }
        if (playerController.CurrentState != playerController.BlockState && hitBoxEnabled)
        {
            disableHitBox();
        }
        if (playerController.CurrentState != playerController.SpecialState
            && playerController.CurrentState != playerController.AttackState
            && attackBoxSpecialEnabled)
        {
            disableAttackBoxSpecial();
        }
    }

    private GameObject LoadProjectilePrefab(GameObject currentPrefab, string resourcePath)
    {
        return currentPrefab != null ? currentPrefab : Resources.Load(resourcePath) as GameObject;
    }

    private void SpawnProjectile(GameObject projectilePrefab)
    {
        if (projectilePrefab == null || projectileSpawn == null)
        {
            return;
        }

        ProjectilePool.Spawn(projectilePrefab, projectileSpawn.transform.position, Quaternion.identity);
    }

    public void instantiateProjectileLazer()
    {
        SpawnProjectile(projectileLaserPrefab);
    }
    public void instantiateProjectileBullet()
    {
        SpawnProjectile(projectileBulletPrefab);
    }
    public void instantiateProjectileBulletAuto()
    {
        SpawnProjectile(projectileAutomaticBulletPrefab);
    }

    public void instantiateProjectileAutomaticBullet(int numOfBullets)
    {
        StartCoroutine(ShootAutomaticWeapon(numOfBullets));
        //Instantiate(projectileBulletPrefab, projectileSpawn.transform.position, Quaternion.identity);
    }

    IEnumerator ShootAutomaticWeapon(int numBullets)
    {
        for (int i = 0; i < numBullets; i++)
        {
            instantiateProjectileBulletAuto();
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void instantiateProjectileMolotov()
    {
        SpawnProjectile(projectileMolotovPrefab);
    }
    public void instantiateProjectileRabbit()
    {
        SpawnProjectile(projectileRabbitPrefab);
    }
    public void instantiateProjectileCigarette()
    {
        SpawnProjectile(projectileCigarettePrefab);
    }
    public void instantiateProjectileRocket()
    {
        SpawnProjectile(projectileRocketPrefab);
    }


    // animation events fire from the Animator, so they can arrive before Start has resolved the
    // controller or after the actor has been torn down
    private bool CanApplyForce()
    {
        return playerController != null && playerController.RigidBody != null;
    }

    public void applyForceToDirectionFacingXAndY(float force)
    {
        if (!CanApplyForce())
        {
            return;
        }

        // get direction facing
        if (playerController.FacingRight)
        {
            //apply to X
            playerController.RigidBody.AddForce(force, force, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(-force, force, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction

    }

    public void applyForceToDirectionFacingProjectile(float force)
    {
        if (!CanApplyForce())
        {
            return;
        }

        if (playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(force, 0, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(-force, 0, 0, ForceMode.VelocityChange);
        }
    }

    public void applyForceToDirectionFacing()
    {
        if (!CanApplyForce())
        {
            return;
        }

        // get direction facing
        if (playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(2.5f, 1.5f, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(-2.5f, 1.5f, 0, ForceMode.VelocityChange);
        }
    }
    public void applyForceToXDirectionFacing(float Xforce)
    {
        if (!CanApplyForce())
        {
            return;
        }

        // get direction facing
        if (playerController.FacingRight)
        {
            //apply to X
            playerController.RigidBody.AddForce(Xforce, 0, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(-Xforce, 0, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction
    }

    public void applyForceToXDirectionNotFacing(float Xforce)
    {
        Debug.Log("force : " + Xforce);
        // get direction facing
        if (playerController.FacingRight)
        {
            //apply to X
            playerController.RigidBody.AddForce(-Xforce, 2, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            playerController.RigidBody.AddForce(Xforce, 2, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction
    }


    public void enableAttackBox()
    {
        if (attackBox == null)
        {
            return;
        }

        attackBox.SetActive(true);
        attackBoxEnabled = true;
    }

    public void disableAttackBox()
    {
        if (attackBox == null)
        {
            attackBoxEnabled = false;
            return;
        }

        attackBox.SetActive(false);
        attackBoxEnabled = false;
    }
    public void enableAttackBoxSpecial()
    {
        if (attackBoxSpecial == null)
        {
            return;
        }

        attackBoxSpecial.SetActive(true);
        attackBoxSpecialEnabled = true;
    }

    public void disableAttackBoxSpecial()
    {
        if (attackBoxSpecial == null)
        {
            attackBoxSpecialEnabled = false;
            return;
        }

        attackBoxSpecial.SetActive(false);
        attackBoxSpecialEnabled = false;
    }

    public void enableHitBox()
    {
        if (hitBox == null)
        {
            return;
        }

        hitBox.SetActive(true);
        hitBoxEnabled = true;
    }

    public void disableHitBox()
    {
        if (hitBox == null)
        {
            hitBoxEnabled = false;
            return;
        }

        hitBox.SetActive(false);
        hitBoxEnabled = false;
    }

    //public void enableCapsuleCollider()
    //{
    //    capsuleCollider.enabled = true;
    //    //capsuleColliderEnabled = true;
    //}

    //public void disableCapsuleCollider()
    //{
    //    capsuleCollider.enabled = false;
    //    //capsuleColliderEnabled = false;
    //}

    public void enableRigidBodyIsKinematic()
    {
        GameLevelManager.instance.Player1.GetComponent<Rigidbody>().isKinematic = true;
    }

    public void disableRigidBodyIsKinematic()
    {
        GameLevelManager.instance.Player1.GetComponent<Rigidbody>().isKinematic = false;
    }

    public void playSfxBasketballHitRim()
    {
        audioSource.PlayOneShot(SFXBB.instance.basketballHitRim);
    }

    public void playSfxBasketballDribbling()
    {
        audioSource.PlayOneShot(SFXBB.instance.basketballBounce);
    }

    public void playSfxAlienWalking()
    {
        audioSource.PlayOneShot(SFXBB.instance.alien_walk);
    }

    public void playSfxGameChanger()
    {
        audioSource.PlayOneShot(SFXBB.instance.gamechanger);
    }

    public void playSfxCameraFlash()
    {
        audioSource.PlayOneShot(SFXBB.instance.cameraFlash);
    }

    public void playSfxWerewolfHowl()
    {
        audioSource.PlayOneShot(SFXBB.instance.werewolfHowl);
    }

    public void playSfxWorkerParasite()
    {
        audioSource.PlayOneShot(SFXBB.instance.worker_parasite);
    }

    public void playSfxAirHorn()
    {
        audioSource.PlayOneShot(SFXBB.instance.airhorn);
    }
    public void playSfxLightningStrike()
    {
        audioSource.PlayOneShot(SFXBB.instance.lightningStrike);
    }

    public void playSfxRimShot()
    {
        audioSource.PlayOneShot(SFXBB.instance.rimShot);
    }
    public void playSfxKnockedDown()
    {
        audioSource.PlayOneShot(SFXBB.instance.knockedDown);
    }
    public void playSfxTakeDamage()
    {
        audioSource.PlayOneShot(SFXBB.instance.knockedDown);
    }

    public void playSfxSkateGrind()
    {
        audioSource.PlayOneShot(SFXBB.instance.skateGrind);
    }

    public void playSfxGlitch()
    {
        audioSource.PlayOneShot(SFXBB.instance.glitch);
    }

    public void playSfxCloudOfSmoke()
    {
        audioSource.PlayOneShot(SFXBB.instance.turnIntoBat);
    }

    public void playSfxAirGuitar()
    {
        audioSource.PlayOneShot(SFXBB.instance.airGuitar);
    }

    public void playSfxChainRattle()
    {
        audioSource.PlayOneShot(SFXBB.instance.chainRattle);
    }

    public void playSfxDeathRay()
    {
        audioSource.PlayOneShot(SFXBB.instance.deathRay);
    }

    public void playSfxProbeDroidCritical()
    {
        audioSource.PlayOneShot(SFXBB.instance.probeCritical);
    }
    public void playSfxVampireHiss()
    {
        audioSource.PlayOneShot(SFXBB.instance.vampireHiss);
    }
    public void playSfxHitMetalBang()
    {
        audioSource.PlayOneShot(SFXBB.instance.metalBang);
    }

    public void playSfxStoneCold()
    {
        audioSource.PlayOneShot(SFXBB.instance.stoneCold);
    }

    public void playSfxChopWood()
    {
        audioSource.PlayOneShot(SFXBB.instance.chopWood);
    }

    public void playSfxShootGun()
    {
        audioSource.PlayOneShot(SFXBB.instance.shootGun);
    }
    public void playSfxShotgunRack()
    {
        audioSource.PlayOneShot(SFXBB.instance.shotgunRack);
    }
    public void playSfxProjectileRocket()
    {
        audioSource.PlayOneShot(SFXBB.instance.projectileRocket);
    }
    public void playSfxWhipCrack()
    {
        audioSource.PlayOneShot(SFXBB.instance.whipCrack);
    }
    public void playSfxAK47()
    {
        audioSource.PlayOneShot(SFXBB.instance.shootAutomaticAK47);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (gameObject.CompareTag("hanger") && other.gameObject.CompareTag("basketball"))
        {
            if (TryGetComponent(out Animator hangerAnimator))
            {
                hangerAnimator.SetTrigger("hit");
            }
        }

        if (gameObject.transform.parent != null
            && gameObject.transform.parent.name.Contains("mega_robot")
            && other.gameObject.CompareTag("playerHitbox")
            && TryGetComponent(out Animator robotAnimator))
        {
            robotAnimator.SetTrigger("attack");
        }
    }

    void CheckAttackBoxActiveStatus()
    {
        if (attackBox != null
            && !GameLevelManager.instance.PlayerController1.IsSpecialState()
            && attackBox.activeSelf)
        {
            attackBox.SetActive(false);
        }
    }
    //private void playAnimationCameraFlash()
    //{
    //    animOnCamera.Play("camera_flash");
    //}
}
