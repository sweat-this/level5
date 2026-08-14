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

    // ENM-4: the one Rob currently in the scene, or null. A static outlives a scene load, so it is
    // cleared on subsystem registration the same way EnemyController clears its active set.
    private static GameObject activeRob;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRob()
    {
        activeRob = null;
    }

    private void Start()
    {
        setNextEventTime();
    }

    /// <summary>
    /// ENM-4: this indexed <c>GameLevelManager.instance.players[0]</c> and instantiated a
    /// <c>Resources.Load</c> result without checking either, on a timer that fires every 10 to 40
    /// seconds for the whole match. Cleanup depends on Rob's own <c>DestroyRob</c> animation event
    /// being reached, so a clone whose animation is interrupted stays forever and the next tick
    /// adds another. Returns whether a Rob was actually spawned so the caller can reschedule
    /// rather than silently doing nothing.
    /// </summary>
    public static bool InstantiateRob()
    {
        PlayerIdentifier primary = GameLevelManager.instance != null
            ? GameLevelManager.instance.Player1
            : null;
        if (primary == null || primary.player == null)
        {
            return false;
        }

        // One Rob at a time. He is a scripted cameo, not a population. Tracked by reference rather
        // than by tag or a scene search - the prefab is untagged, and Unity's overloaded == reports
        // the destroyed instance as null once DestroyRob has run.
        if (activeRob != null)
        {
            return false;
        }

        GameObject robPrefab = Resources.Load(Constants.PREFAB_PATH_character_rob_perillo) as GameObject;
        if (robPrefab == null)
        {
            Debug.LogError(
                $"RandomEvents could not load the Rob prefab at Resources/{Constants.PREFAB_PATH_character_rob_perillo}.");
            return false;
        }

        Transform playerTransform = primary.player.transform;
        Vector3 spawn = new Vector3(
            playerTransform.position.x + 1.5f,
            playerTransform.position.y,
            playerTransform.position.z);

        activeRob = Instantiate(robPrefab, spawn, Quaternion.identity);
        return true;
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
