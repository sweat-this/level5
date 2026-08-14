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
        // CHR-4: this dereferenced the Find result directly, so any scene without a "rim" object
        // threw here and left the crowd unpositioned and unfiltered.
        basketBallGoalPosition = GameObject.Find("rim");
        if (basketBallGoalPosition == null)
        {
            Debug.LogError($"FanManager on {name} found no 'rim' object to position the crowd against.", this);
            return;
        }

        float terrainHeight = GameLevelManager.instance != null ? GameLevelManager.instance.TerrainHeight : transform.position.y;
        transform.position = new Vector3(
            basketBallGoalPosition.transform.position.x,
            terrainHeight,
            basketBallGoalPosition.transform.position.z);
        fansList = getFans();
    }

    /// <summary>
    /// Hides the fan representing the character the player is playing as - you do not watch
    /// yourself from the stands - and returns the rest.
    ///
    /// The substring match below is deliberately left as it was. Fan objects are named
    /// <c>npc_&lt;character&gt;</c> and no shipped character object name is a segment-prefix of
    /// another fan's name, so it is correct for the current content; tightening it to a whole-name
    /// match would change which fan is hidden for compound names such as <c>rad_tony</c>
    /// (whose fan is <c>npc_radtony_skateboard</c>) without a way to verify every scene's crowd
    /// here. Worth revisiting when a character is added whose name contains another's.
    /// </summary>
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
                tempList.Add(fan);
            }
        }

        return tempList;
    }
}
