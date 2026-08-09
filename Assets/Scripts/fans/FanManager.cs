using System.Collections.Generic;
using UnityEngine;
using Level5.Core.Match;

public class FanManager : MonoBehaviour
{
    GameObject basketBallGoalPosition;
    [SerializeField]
    List<GameObject> fansList;

    private void Start()
    {
        // position transform relative to basketball goal
        basketBallGoalPosition = GameObject.Find("rim");
        //Debug.Log("vector : " + basketBallGoalPosition.transform.position);
        transform.position = new Vector3(basketBallGoalPosition.transform.position.x, GameLevelManager.instance.TerrainHeight, basketBallGoalPosition.transform.position.z);
        //Debug.Log("vector : " + position);
        //transform.position = position;
        fansList = getFans();
    }

    List<GameObject> getFans()
    {
        List<GameObject> tempList = new List<GameObject>();
        int count = transform.childCount;

        for (int i = 0; i < count; i++)
        {
            GameObject fan = transform.GetChild(i).gameObject;
            if (!string.IsNullOrEmpty(MatchRuntime.PrimaryCharacterObjectName)
                && fan.name.Contains(MatchRuntime.PrimaryCharacterObjectName))
            {
                fan.SetActive(false);
            }
            else
            {
                tempList.Add(transform.GetChild(i).gameObject);
            }
        }

        return tempList;
    }
}
