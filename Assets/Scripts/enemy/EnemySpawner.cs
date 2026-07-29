using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

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
            GameObject[] enemyHealthList = GameObject.FindGameObjectsWithTag("enemy");
            foreach (GameObject go in enemyHealthList)
            {
                if (go.GetComponentInChildren<EnemyController>().IsBoss)
                {
                    numberOfBoss++;
                }
                else
                {
                    numberOfMinions++;
                }
            }
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
            if (GameOptions.hardcoreModeEnabled && GameOptions.EnemiesOnlyEnabled)
            {
                maxNumberOfEnemies = 8;
            }
            else if (GameOptions.hardcoreModeEnabled && !GameOptions.EnemiesOnlyEnabled)
            {
                maxNumberOfEnemies = 6;
            }
            else if (!GameOptions.battleRoyalEnabled || !GameOptions.gameModeHasBeenSelected || GameOptions.enemiesEnabled)
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
        Debug.Log("spawn");
        int numberToSpawn = maxNumberOfMinions - numberOfMinions - numberOfBoss;
        Random random = new Random();
        if (numberToSpawn > 0)
        {
            for (int i = 0; i < numberToSpawn; i++)
            {
                //Random random = new Random();
                int randomIndex = random.Next(0, enemyMinionPrefabs.Count);
                //Debug.Log("randomIndex : " + randomIndex + "  max : " + (enemyMinionPrefabs.Count));
                if (i > spawnPositions.Count - 1)
                {
                    Instantiate(enemyMinionPrefabs[randomIndex], spawnPositions[0].transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(enemyMinionPrefabs[randomIndex], spawnPositions[i].transform.position, Quaternion.identity);
                }
            }
        }
    }

    void spawnDefaultBoss()
    {
        //Debug.Break();
        Debug.Log("spawn");
        int numberToSpawn = maxNumberOfBoss - numberOfBoss;
        Random random = new Random();
        int randomIndex = random.Next(0, enemyBossPrefabs.Count);
        if (numberToSpawn > 0)
        {
            for (int i = 0; i < numberToSpawn; i++)
            {
                // if spawn more enemies than enemy list has, spawn random enemy
                if (i >= enemyBossPrefabs.Count)
                {
                    //Random random = new Random();
                    //int randomIndex = random.Next(0, enemyBossPrefabs.Count);
                    Instantiate(enemyBossPrefabs[randomIndex], spawnPositions[i].transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(enemyBossPrefabs[randomIndex], spawnPositions[i].transform.position, Quaternion.identity);
                }
            }
        }
    }

    void spawnSingleMinion()
    {
        Random random = new Random();
        int randomIndex = random.Next(0, enemyMinionPrefabs.Count);

        int spawnIndex = 0;
        if (randomIndex >= spawnPositions.Count - 1)
        {
            spawnIndex = spawnPositions.Count - 1;
        }
        else
        {
            spawnIndex = randomIndex;
        }
        // if spawn more enemies than enemy list has, spawn random enemy
        if (randomIndex >= enemyMinionPrefabs.Count)
        {
            Instantiate(enemyMinionPrefabs[0], spawnPositions[spawnIndex].transform.position, Quaternion.identity);
        }
        else
        {
            Instantiate(enemyMinionPrefabs[randomIndex], spawnPositions[spawnIndex].transform.position, Quaternion.identity);
        }
    }

    void spawnBoss()
    {
        //Debug.Log("spawn");
        Random random = new Random();
        int randomIndex = random.Next(0, enemyBossPrefabs.Count);

        // if spawn more enemies than enemy list has, spawn random enemy
        if (randomIndex >= enemyBossPrefabs.Count)
        {
            Instantiate(enemyBossPrefabs[0], spawnPositions[0].transform.position, Quaternion.identity);
            numberOfBoss++;
        }
        else
        {
            Instantiate(enemyBossPrefabs[randomIndex], spawnPositions[randomIndex].transform.position, Quaternion.identity);
            numberOfBoss++;
        }
    }

    void getNumberOfCurrentEnemiesInScene()
    {
        // *note : dont need to check for boss. if boss killed, doesnt respawn

        numberOfMinions = GameObject.FindGameObjectsWithTag("enemy").Length;
        numberOfBoss = getNumberOfBoss();

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
        int value = 0;
        //GameObject[] enemyHealthList = GameObject.FindGameObjectsWithTag("enemy");
        //foreach (GameObject go in enemyHealthList)
        //{
        //    if (go.GetComponentInChildren<EnemyController>().IsBoss)
        //    {
        //        value++;
        //    }
        //}
        EnemyController[] enemyHealthList = UnityEngine.Object.FindObjectsByType<EnemyController>();
        foreach (EnemyController ec in enemyHealthList)
        {
            if (ec.IsBoss)
            {
                value++;
            }
        }
        return value;
    }

    void spawnBattleRoyalContestant()
    {
        int randomIndex = 0;
        Random random = new Random();

        if (GameOptions.battleRoyalEnabled && !GameOptions.hardcoreModeEnabled)
        {
            if(getNumberOfBoss() == 0)
            {
                randomIndex = random.Next(0, enemyBossPrefabs.Count);
                Instantiate(enemyBossPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position, Quaternion.identity);
            }
            else
            {
                randomIndex = random.Next(0, enemyMinionPrefabs.Count);
                // if spawn more enemies than enemy list has, spawn random enemy
                if (randomIndex >= enemyMinionPrefabs.Count)
                {
                    Instantiate(enemyMinionPrefabs[0], battleRoyalSpawnPosition.transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(enemyMinionPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position, Quaternion.identity);
                }
            }
        }
        if (GameOptions.battleRoyalEnabled && GameOptions.hardcoreModeEnabled)
        {
            randomIndex = random.Next(0, enemyBossPrefabs.Count);
            Instantiate(enemyBossPrefabs[randomIndex], battleRoyalSpawnPosition.transform.position, Quaternion.identity);
        }
    }
}
