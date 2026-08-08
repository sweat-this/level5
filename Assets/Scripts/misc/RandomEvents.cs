using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;

public class RandomEvents : MonoBehaviour
{
    [SerializeField]
    float startTimer;
    [SerializeField]
    float lengthTimer;
    [SerializeField]
    float invokeEventTime = 30;

    private void Start()
    {
        setNextEventTime();
    }

    public static void InstantiateRob()
    {
        GameObject _playerSpawnLocation = GameLevelManager.instance.players[0].player;
        Vector3 spawn = new Vector3(_playerSpawnLocation.transform.position.x + 1.5f,
            _playerSpawnLocation.transform.position.y,
            _playerSpawnLocation.transform.position.z);
        GameObject _playerClone = Resources.Load(Constants.PREFAB_PATH_character_rob_perillo) as GameObject;

        Instantiate(_playerClone, spawn, Quaternion.identity);
    }

    private void Update()
    {
        if (Time.time > invokeEventTime
            && (MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.IsBattleRoyal))
        {
            invokeGodOfThunder();
            setNextEventTime();
        }
    }

    private void setNextEventTime()
    {
        startTimer = Time.time;
        //Debug.Log("start timer : " + startTimer);
        lengthTimer = UtilityFunctions.GetRandomFloat(10, 40);
        //Debug.Log("next event between : " + startTimer + " -- " + (startTimer + 40));
        invokeEventTime = startTimer + lengthTimer;
        //Debug.Log("next rob sighting : " + invokeEventTime);
    }

    private void invokeGodOfThunder()
    {
        InstantiateRob();
        // if enemies
        // if health < 50
        // every 30 seconds
        // random 1-30 seconds every 30 seconds
        // roll for critical

        // n= 0
        /* random 1- 30
         * if random > time.time
         * call function
         * reset timer
        */
    }
}
