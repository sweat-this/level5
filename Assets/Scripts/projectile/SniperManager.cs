using Assets.Scripts.Utility;
using System.Collections;
using UnityEngine;

public class SniperManager : MonoBehaviour
{
    private const float InitializationTimeoutSeconds = 10f;
    [SerializeField]
    private GameObject playerHitbox;
    [SerializeField]
    private AudioSource audioSource;
    private Vector3 playerPosAtShoot;
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    GameObject projectileLaserPrefab;
    [SerializeField]
    GameObject projectileBulletPrefab;    
    [SerializeField]
    GameObject projectileAutomaticBulletPrefab;
    [SerializeField]
    GameObject projectileBulletInstantKillPrefab;
    [SerializeField]
    float bulletDelay;

    public bool locked = false;

    public static SniperManager instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
    private void Start()
    {
        StartCoroutine(LoadVariables());
        //GameOptions.sniperEnabled = true; 
        //test flag
        // auto start autonomous sniper system
        if (GameOptions.sniperEnabled || GameOptions.sniperEnabledLaser || GameOptions.sniperEnabledBullet)
        {
            instance = this;
            InvokeRepeating("startSniper", 0, 0.5f);
        }
        //else
        //{
        //    gameObject.SetActive(false);
        //}
    }

    IEnumerator LoadVariables()
    {
        float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
        while (!TryResolvePlayer() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (!TryResolvePlayer())
        {
            Debug.LogError("SniperManager could not resolve a playable character and has been disabled.");
            CancelInvoke();
            enabled = false;
            yield break;
        }

        audioSource = GetComponent<AudioSource>();
    }

    private bool TryResolvePlayer()
    {
        if (GameLevelManager.instance == null
            || GameLevelManager.instance.players == null
            || GameLevelManager.instance.players.Count == 0
            || GameLevelManager.instance.players[0] == null
            || GameLevelManager.instance.players[0].playerController == null)
        {
            return false;
        }

        Transform hitbox = GameLevelManager.instance.players[0].transform.Find("hitbox");
        if (hitbox == null)
        {
            return false;
        }

        playerController = GameLevelManager.instance.players[0].playerController;
        playerHitbox = hitbox.gameObject;
        return true;
    }

    void startSniper()
    {
        if (!locked && playerHitbox != null && playerController != null && playerController.PlayerHealth != null)
        {
            locked = true;
            float random = UtilityFunctions.GetRandomFloat(0, 4);

            //// test flag to enable
            //GameOptions.sniperEnabledBullet = true;
            if (GameOptions.sniperEnabledBullet && !playerController.PlayerHealth.IsDead)
            {
                StartCoroutine(StartSniperBullet(random));
            }
            if (GameOptions.sniperEnabledLaser && !playerController.PlayerHealth.IsDead)
            {
                StartCoroutine(StartSniperLaser(random));
            }
            if (GameOptions.sniperEnabledBulletAuto && !playerController.PlayerHealth.IsDead)
            {
                StartCoroutine(StartSniperBulletAuto(random));
            }
        }
    }

    public IEnumerator StartSniperBullet(float shootdelay)
    {
        // wait until player is not knocked down
        yield return new WaitUntil( ()=> playerController.currentState != playerController.knockedDownState);
        // add shoot delay
        yield return new WaitForSeconds(shootdelay);
        // update stats
        GameLevelManager.instance.players[0].gameStats.SniperShots++;

        // get player position to attack
        PlayerPosAtShoot = playerHitbox.transform.position;
        // get vector to player
        Vector3 direction = PlayerPosAtShoot - (gameObject.transform.position);
        //play sound
        audioSource.PlayOneShot(SFXBB.instance.shootGun);
        InstantiateInstantBullet(direction);
    }

    public IEnumerator StartSniperBulletAuto(float shootdelay)
    {
        // wait until player is not knocked down
        yield return new WaitUntil(() => playerController.currentState != playerController.knockedDownState);
        // add shoot delay
        yield return new WaitForSeconds(shootdelay);

        // get player position to attack
        PlayerPosAtShoot = playerHitbox.transform.position;
        //PlayerPosAtShoot = GameLevelManager.instance.Player.transform.Find("hitbox").gameObject.transform.position;
        // get vector to player
        Vector3 direction = PlayerPosAtShoot - (gameObject.transform.position);
        //play sound
        audioSource.PlayOneShot(SFXBB.instance.shootAutomaticAK47);
        StartCoroutine(InstantiateProjectileAutomaticBullet(direction, 10));
    }
    IEnumerator StartSniperLaser(float shootdelay)
    {
        yield return new WaitForSeconds(shootdelay);

        GameLevelManager.instance.players[0].gameStats.SniperShots++;

        // get player position to attack
        PlayerPosAtShoot = playerHitbox.transform.position;
        // get vector to player
        Vector3 direction = PlayerPosAtShoot - (gameObject.transform.position);
        //play sound
        audioSource.PlayOneShot(SFXBB.instance.deathRay);
        StartCoroutine(InstantiateLaser(direction));
    }
    public IEnumerator StartSniperBulletInstantKill(float shootdelay)
    {
        yield return new WaitForSeconds(shootdelay);

        GameLevelManager.instance.players[0].gameStats.SniperShots++;

        // get player position to attack
        PlayerPosAtShoot = playerHitbox.transform.position;
        // get vector to player
        Vector3 direction = PlayerPosAtShoot - (gameObject.transform.position);
        //play sound
        audioSource.PlayOneShot(SFXBB.instance.shootGun);
        StartCoroutine(InstantiateBulletInstantKill(direction));
    }
    void InstantiateInstantBullet(Vector3 projectileForceSniper)
    {
        //yield return new WaitForSeconds(bulletDelay);
        // instantiate bullet
        InstantiateConfiguredProjectile(projectileBulletPrefab, projectileForceSniper);
        locked = false;
    }

    IEnumerator InstantiateBullet(Vector3 projectileForceSniper)
    {
        yield return new WaitForSeconds(bulletDelay);
        // instantiate bullet
        InstantiateConfiguredProjectile(projectileBulletPrefab, projectileForceSniper);
        locked = false;
    }
    IEnumerator InstantiateProjectileAutomaticBullet(Vector3 projectileForceSniper, int numBullets)
    {

        yield return new WaitForSeconds(bulletDelay);
        for (int i = 0; i < numBullets; i++)
        {
            instantiateProjectileBulletAuto(projectileForceSniper);
            yield return new WaitForSeconds(0.2f);
        }
        locked = false;
    }

    public void instantiateProjectileBulletAuto(Vector3 projectileForceSniper)
    {
        float random = UtilityFunctions.GetRandomFloat(-0.35f, 0.35f);
        Vector3 target = new Vector3(projectileForceSniper.x + random, projectileForceSniper.y, projectileForceSniper.z);
        InstantiateConfiguredProjectile(projectileAutomaticBulletPrefab, target);
        // update stats
        GameLevelManager.instance.players[0].gameStats.SniperShots++;
    }

    IEnumerator InstantiateLaser(Vector3 projectileForceSniper)
    {
        yield return new WaitForSeconds(bulletDelay);
        // instantiate laser
        InstantiateConfiguredProjectile(projectileLaserPrefab, projectileForceSniper);
        locked = false;
    }
    IEnumerator InstantiateBulletInstantKill(Vector3 projectileForceSniper)
    {
        yield return new WaitForSeconds(bulletDelay);
        // instantiate laser
        InstantiateConfiguredProjectile(projectileBulletInstantKillPrefab, projectileForceSniper);
        locked = false;
    }

    private void InstantiateConfiguredProjectile(GameObject projectilePrefab, Vector3 projectileForceSniper)
    {
        GameObject projectileInstance = ProjectilePool.Spawn(projectilePrefab, gameObject.transform.position, Quaternion.identity, projectile =>
        {
            EnemyProjectile enemyProjectile = projectile.GetComponentInChildren<EnemyProjectile>();
            if (enemyProjectile == null)
            {
                return;
            }

            enemyProjectile.sniperProjectile = true;
            enemyProjectile.impactProjectile = true;
            enemyProjectile.projectileForceSniper = projectileForceSniper;
        });

        if (projectileInstance == null)
        {
            Debug.LogError("SniperManager is missing a configured projectile prefab.");
            locked = false;
        }
    }
    public Vector3 PlayerPosAtShoot { get => playerPosAtShoot; set => playerPosAtShoot = value; }
}
