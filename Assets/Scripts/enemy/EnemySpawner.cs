using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    //GameObject basketBallGoalPosition;
    public List<GameObject> spawnPositions;
    [SerializeField]
    public List<GameObject> enemyMinionPrefabs;
    [SerializeField]
    public List<GameObject> enemyBossPrefabs;
    [SerializeField]
    int numberOfMinions;
    [SerializeField]
    int numberOfBoss;
    [SerializeField]
    int maxNumberOfEnemies;
    [SerializeField]
    int maxNumberOfBoss = 1;
    [SerializeField]
    int maxNumberOfMinions;
    [SerializeField]
    GameObject battleRoyalSpawnPosition;
    [SerializeField]
    GameObject steelcage;

    private void Awake()
    {
        //GameOptions.battleRoyalEnabled = true;
        // get number of enemies already in scene
        //if (GameObject.FindGameObjectWithTag("enemy") != null)
        if (GameOptions.enemiesEnabled)
        {
            //GameOptions.enemiesEnabled = true;
            // this needs to second option or enabling it will spawn enemies
            // ***** DISABLE FOR TESTING
            //GameOptions.enemiesEnabled = true;
            RefreshEnemyCounts();
        }
        steelcage = GameObject.Find("steelCageRootObject");
        battleRoyalSpawnPosition = GameObject.Find("battleRoyalSpawnPosition");
    }

    private void Start()
    {

        //// this needs to second option or enabling it will spawn enemies
        //if (GameObject.FindGameObjectWithTag("enemy") != null)
        //{
        //    GameOptions.enemiesEnabled = true;
        //}
        // if enemies in scene, spawn max
        //Debug.Log(GameOptions.enemiesEnabled);
        //Debug.Log(GameOptions.EnemiesOnlyEnabled);
        //Debug.Log(GameOptions.gameModeHasBeenSelected);
        if ((GameOptions.enemiesEnabled || GameOptions.EnemiesOnlyEnabled) /* && GameOptions.gameModeHasBeenSelected */)
        {
            if (!HasSpawnConfiguration())
            {
                enabled = false;
                return;
            }
            if (GameOptions.hardcoreModeEnabled && GameOptions.EnemiesOnlyEnabled)
            {
                maxNumberOfEnemies = 8;
            }
            else if (GameOptions.hardcoreModeEnabled && !GameOptions.EnemiesOnlyEnabled)
            {
                maxNumberOfEnemies = 6;
            }
            // AUD-056: this used to also test `|| GameOptions.enemiesEnabled`. StartManager forces
            // enemiesEnabled = true for every battle royal (setGameOptions: "if enemies only mode,
            // enable enemies whether it was selected or not"), so that disjunct was always true
            // whenever battleRoyalEnabled was - which made the two branches below unreachable.
            // A non-cage battle royal therefore spawned 4 enemies where the branch order says 2.
            else if (!GameOptions.battleRoyalEnabled || !GameOptions.gameModeHasBeenSelected)
            {
                maxNumberOfEnemies = 4;
            }
            else if (GameOptions.cageMatchEnabled)
            {
                maxNumberOfEnemies = 4;
            }
            else
            {
                maxNumberOfEnemies = 2;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            maxNumberOfEnemies = maxNumberOfEnemies/2;
#endif
            maxNumberOfMinions = maxNumberOfEnemies - maxNumberOfBoss;
            //Debug.Log(GameOptions.battleRoyalEnabled);
            //Debug.Log(GameOptions.cageMatchEnabled);
            //Debug.Log(steelcage != null);
            if ((!GameOptions.battleRoyalEnabled || GameOptions.cageMatchEnabled))
            {
                // spawn enemies if necessary
                spawnDefaultMinions();
                spawnDefaultBoss();
                // start function to check status of current enemies
                InvokeRepeating("getNumberOfCurrentEnemiesInScene", 5, 2f);
            }
            if (!GameOptions.cageMatchEnabled && steelcage != null)
            {
                steelcage.SetActive(false);
            }
            if (GameOptions.battleRoyalEnabled && !GameOptions.cageMatchEnabled)
            {
                maxNumberOfEnemies = 20;
                //battleRoyalSpawnPosition = GameObject.Find("battleRoyalSpawnPosition");
                InvokeRepeating("spawnBattleRoyalContestant", 0, 10f);
                //spawnBattleRoyalContestant();
            }
        }
        //if (!GameOptions.battleRoyalEnabled || GameOptions.cageMatchEnabled)
        //{
        //    Debug.Log("spawn");
        //    // spawn enemies if necessary
        //    spawnDefaultMinions();
        //    spawnDefaultBoss();
        //    // start function to check status of current enemies
        //    InvokeRepeating("getNumberOfCurrentEnemiesInScene", 5, 2f);
        //}
        //if (!GameOptions.cageMatchEnabled && steelcage != null)
        //{
        //    steelcage.SetActive(false);
        //}
        //if (GameOptions.battleRoyalEnabled && !GameOptions.cageMatchEnabled)
        //{
        //    maxNumberOfEnemies = 20;
        //    battleRoyallSpawnPosition = GameObject.Find("battleRoyalSpawnPosition");
        //    InvokeRepeating("spawnBattleRoyalContestant", 0, 10f);
        //    //spawnBattleRoyalContestant();
        //}
    }

    void spawnDefaultMinions()
    {
        int numberToSpawn = maxNumberOfMinions - numberOfMinions;
        if (numberToSpawn > 0)
        {
            for (int i = 0; i < numberToSpawn; i++)
            {
                int randomIndex = Random.Range(0, enemyMinionPrefabs.Count);
                int spawnIndex = i % spawnPositions.Count;
                Spawn(enemyMinionPrefabs[randomIndex], spawnPositions[spawnIndex].transform.position);
            }
        }
    }

    void spawnDefaultBoss()
    {
        int numberToSpawn = maxNumberOfBoss - numberOfBoss;
        if (numberToSpawn > 0)
        {
            for (int i = 0; i < numberToSpawn; i++)
            {
                int randomIndex = Random.Range(0, enemyBossPrefabs.Count);
                int spawnIndex = i % spawnPositions.Count;
                Spawn(enemyBossPrefabs[randomIndex], spawnPositions[spawnIndex].transform.position);
            }
        }
    }

    void spawnSingleMinion()
    {
        int randomIndex = Random.Range(0, enemyMinionPrefabs.Count);
        int spawnIndex = Random.Range(0, spawnPositions.Count);
        Spawn(enemyMinionPrefabs[randomIndex], spawnPositions[spawnIndex].transform.position);
    }

    void spawnBoss()
    {
        int randomIndex = Random.Range(0, enemyBossPrefabs.Count);
        int spawnIndex = Random.Range(0, spawnPositions.Count);
        Spawn(enemyBossPrefabs[randomIndex], spawnPositions[spawnIndex].transform.position);
        numberOfBoss++;
    }

    void getNumberOfCurrentEnemiesInScene()
    {
        // *note : dont need to check for boss. if boss killed, doesnt respawn

        RefreshEnemyCounts();

        //Debug.Log("numberOfMinions : " + numberOfMinions);
        if (numberOfMinions < maxNumberOfMinions)
        {
            // update spawner location so spawn locations is near player
            spawnSingleMinion();
        }
        if (numberOfBoss < maxNumberOfBoss)
        {
            // update spawner location so spawn locations is near player
            spawnBoss();
        }
    }

    int getNumberOfBoss()
    {
        RefreshEnemyCounts();
        return numberOfBoss;
    }

    void spawnBattleRoyalContestant()
    {
        if (battleRoyalSpawnPosition == null)
        {
            Debug.LogError("EnemySpawner requires battleRoyalSpawnPosition for battle royal mode.");
            CancelInvoke(nameof(spawnBattleRoyalContestant));
            return;
        }

        RefreshEnemyCounts();
        if (numberOfMinions + numberOfBoss >= maxNumberOfEnemies)
        {
            return;
        }

        int randomIndex;

        if (GameOptions.battleRoyalEnabled && !GameOptions.hardcoreModeEnabled)
        {
            if(getNumberOfBoss() == 0)
            {
                randomIndex = Random.Range(0, enemyBossPrefabs.Count);
                Spawn(enemyBossPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position);
            }
            else
            {
                randomIndex = Random.Range(0, enemyMinionPrefabs.Count);
                Spawn(enemyMinionPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position);
            }
        }
        if (GameOptions.battleRoyalEnabled && GameOptions.hardcoreModeEnabled)
        {
            randomIndex = Random.Range(0, enemyBossPrefabs.Count);
            Spawn(enemyBossPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position);
        }
    }

    private void RefreshEnemyCounts()
    {
        numberOfBoss = 0;
        numberOfMinions = 0;
        foreach (EnemyController enemy in EnemyController.ActiveEnemies)
        {
            if (enemy != null && enemy.IsBoss)
            {
                numberOfBoss++;
            }
            else if (enemy != null)
            {
                numberOfMinions++;
            }
        }
    }

    private static void Spawn(GameObject prefab, Vector3 position)
    {
        RuntimeObjectPool.Spawn(prefab, position, Quaternion.identity);
    }

    private bool HasSpawnConfiguration()
    {
        bool usesBattleRoyalSpawn = GameOptions.battleRoyalEnabled && !GameOptions.cageMatchEnabled;
        bool hasSpawnLocations = usesBattleRoyalSpawn
            ? battleRoyalSpawnPosition != null
            : spawnPositions != null
                && spawnPositions.Count > 0
                && spawnPositions.TrueForAll(position => position != null);
        bool valid = hasSpawnLocations
            && enemyMinionPrefabs != null
            && enemyMinionPrefabs.Count > 0
            && enemyMinionPrefabs.TrueForAll(prefab => prefab != null)
            && enemyBossPrefabs != null
            && enemyBossPrefabs.Count > 0
            && enemyBossPrefabs.TrueForAll(prefab => prefab != null);

        if (!valid)
        {
            Debug.LogError("EnemySpawner is missing the active mode's spawn position or enemy prefabs.");
        }

        return valid;
    }
}
