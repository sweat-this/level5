using UnityEngine;

public class CollisionCheckDefense : MonoBehaviour
{
    [SerializeField] AutoPlayerDefense autoPlayerDefense;
    [SerializeField] bool isLocked;
    [SerializeField] GameStats gameStats;

    private void Start()
    {
        autoPlayerDefense = GetComponentInParent<AutoPlayerDefense>();
        gameStats = autoPlayerDefense.playerIdentifier.gameStats;
    }

    private void Update()
    {
        if (autoPlayerDefense.grounded)
        {
            isLocked = false;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("basketball") 
            && gameObject.CompareTag("cpuDefenseBlockBox")
            && !isLocked
            && autoPlayerDefense.playerIdentifier.playerController.InAir)
        {
            isLocked = true;
            autoPlayerDefense.blockedShots++;
            gameStats.Stats.BlockedShots++;
            gameStats.Stats.ShotAttempt++;
            Debug.Log("shot blocked");
        }
    }

    //public void OnTriggerExit(Collider other)
    //{
    //    if (other.gameObject.CompareTag("basketball")
    //        && gameObject.CompareTag("cpuDefenseBlockBox"))
    //    {
    //        isLocked = false;
    //    }
    //}
}