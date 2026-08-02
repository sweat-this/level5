using System;
using UnityEngine;

public class EnemyAnimationEvents : MonoBehaviour
{
    [SerializeField]
    GameObject attackBox;

    [SerializeField]
    GameObject projectileLaserPrefab;
    [SerializeField]
    GameObject projectileBulletPrefab;
    [SerializeField]
    GameObject projectileDartPrefab;
    [SerializeField]
    GameObject projectileFlameThrower;
    [SerializeField]
    GameObject projectileSpawn;
    [SerializeField]
    EnemyController enemyController;
    [SerializeField]
    bool attackBoxAlwaysOn;
    [SerializeField]
    private AudioSource audioSource;

    private void Start()
    {

        audioSource = gameObject.GetComponent<AudioSource>();
        Transform projectileSpawnTransform = transform.Find("projectileSpawn");
        if (projectileSpawnTransform != null)
        {
            projectileLaserPrefab = LoadProjectilePrefab(projectileLaserPrefab, "Prefabs/projectile/projectile_laser_enemy");
            projectileBulletPrefab = LoadProjectilePrefab(projectileBulletPrefab, "Prefabs/projectile/projectile_bullet_enemy");
            projectileDartPrefab = LoadProjectilePrefab(projectileDartPrefab, "Prefabs/projectile/projectile_dart_enemy");
            projectileFlameThrower = LoadProjectilePrefab(projectileFlameThrower, "Prefabs/projectile/projectile_flamethrower");
            projectileSpawn = projectileSpawnTransform.gameObject;
        }
        if (transform.parent.GetComponent<EnemyController>() != null)
        {
            enemyController = transform.parent.GetComponent<EnemyController>();
        }

        Transform attackBoxTransform = transform.Find("attackBox");
        attackBox = attackBoxTransform != null ? attackBoxTransform.gameObject : null;
        if (!attackBoxAlwaysOn)
        {
            disableAttackBox();
        }
        // checks for attack boxes not disabling properly animations
        InvokeRepeating("checkAttackBoxDisabledCorrectly", 0, 3);
    }

    public void enableAttackBox()
    {
        if (attackBox != null)
        {
            attackBox.SetActive(true);
        }
    }

    public void disableAttackBox()
    {
        if (attackBox != null)
        {
            attackBox.SetActive(false);
        }
    }
    public void setKnockDownAttack(int value)
    {
        if (attackBox != null)
        {
            attackBox.GetComponent<EnemyAttackBox>().knockDownAttack = Convert.ToBoolean(value);
        }
    }

    void checkAttackBoxDisabledCorrectly()
    {
        if (enemyController != null && !enemyController.stateAttack)
        {
            disableAttackBox();
        }
    }
    // need to add paramters for knockdown + damage
    public void instantiateProjectileLazer(int damage)
    {
        SpawnProjectile(projectileLaserPrefab, projectile =>
        {
            EnemyAttackBox enemyAttackBox = projectile.GetComponentInChildren<EnemyAttackBox>();
            if (enemyAttackBox != null)
            {
                enemyAttackBox.attackDamage = damage;
            }

            SetProjectileFacing(projectile);
        });
    }
    public void instantiateProjectileFlameThrower()
    {
        Vector3 temp = transform.root.localScale;
        temp.x = transform.root.localScale.x;

        ////player localscale will be -1
        //if (!enemyController.facingRight)
        //{
        //    Vector3 temp = transform.localScale;
        //    temp.x *= transform.localScale.x;

        //    projectileFlameThrower.transform.localScale = temp;
        //    Debug.Log("flip the flame transform");
        //}
        ////player localscale will be -1
        //else
        //{
        //    Vector3 temp = transform.localScale;
        //    temp.x *= transform.localScale.x;

        //    projectileFlameThrower.transform.localScale = temp;
        //    Debug.Log("flip the flame transform");
        //    Debug.Log("transform.localScale : " + transform.localScale);
        //}
        //projectileFlameThrower.GetComponentInChildren<EnemyProjectile>().facingRight = enemyController.facingRight;
        GameObject projectile = SpawnProjectile(projectileFlameThrower);
        if (projectile != null)
        {
            projectile.transform.localScale = temp;
        }

    }
    public void instantiateProjectileBullet()
    {
        SpawnProjectile(projectileBulletPrefab, SetProjectileFacing);
    }

    public void instantiateProjectileDart()
    {
        SpawnProjectile(projectileDartPrefab, SetProjectileFacing);
    }

    private GameObject LoadProjectilePrefab(GameObject currentPrefab, string resourcePath)
    {
        return currentPrefab != null ? currentPrefab : Resources.Load(resourcePath) as GameObject;
    }

    private GameObject SpawnProjectile(GameObject projectilePrefab, Action<GameObject> configure = null)
    {
        if (projectilePrefab == null || projectileSpawn == null)
        {
            return null;
        }

        return ProjectilePool.Spawn(projectilePrefab, projectileSpawn.transform.position, Quaternion.identity, configure);
    }

    private void SetProjectileFacing(GameObject projectile)
    {
        EnemyProjectile enemyProjectile = projectile.GetComponentInChildren<EnemyProjectile>();
        if (enemyProjectile != null)
        {
            enemyProjectile.facingRight = enemyController != null && enemyController.facingRight;
        }
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
        try
        {
            audioSource.PlayOneShot(SFXBB.instance.knockedDown);
        }
        catch (Exception e)
        {
            Debug.Log("exception e :" + e);
        }
    }
    public void playSfxTakeDamage()
    {
        audioSource.PlayOneShot(SFXBB.instance.takeDamage);
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

    public void applyForceToDirectionFacingXAndY(float force)
    {
        enemyController.UnFreezeEnemyPosition();
        // get direction facing
        if (enemyController.facingRight)
        {
            //apply to X
            enemyController.RigidBody.AddForce(force, force, 0, ForceMode.VelocityChange);
        }
        if (!enemyController.facingRight)
        {
            enemyController.RigidBody.AddForce(-force, force, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction

    }

    public void applyForceToDirectionFacing()
    {
        // get direction facing
        if (enemyController.facingRight)
        {
            enemyController.RigidBody.AddForce(2.5f, 1.5f, 0, ForceMode.VelocityChange);
        }
        if (!enemyController.facingRight)
        {
            enemyController.RigidBody.AddForce(-2.5f, 1.5f, 0, ForceMode.VelocityChange);
        }
    }
    public void applyForceToXDirectionFacing(float Xforce)
    {
        // get direction facing
        if (enemyController.facingRight)
        {
            //apply to X
            enemyController.RigidBody.AddForce(Xforce, 0, 0, ForceMode.VelocityChange);
        }
        if (!enemyController.facingRight)
        {
            enemyController.RigidBody.AddForce(-Xforce, 0, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction
    }

    public void applyForceToXDirectionNotFacing(float Xforce)
    {
        // get direction facing
        if (enemyController.facingRight)
        {
            Debug.Log("enemy name : " + enemyController.name.ToString() + "  force : " + Xforce);
            Debug.Log("facing right : " + enemyController.facingRight);
            //apply to X
            enemyController.RigidBody.AddForce(-Xforce, 2, 0, ForceMode.VelocityChange);
        }
        if (!enemyController.facingRight)
        {
            Debug.Log("enemy name : " + enemyController.name.ToString() + "  force : " + Xforce);
            Debug.Log("facing right : " + enemyController.facingRight);
            enemyController.RigidBody.AddForce(Xforce, 2, 0, ForceMode.VelocityChange);
        }
        // apply for in x direction
    }
}
