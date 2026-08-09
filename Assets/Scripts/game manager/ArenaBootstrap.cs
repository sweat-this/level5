using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Arena setup that depends on the mode: switching scene objects on and off for the match about to
/// be played.
///
/// This is where the level-start adjustments live now. They used to sit in
/// <c>GameLevelManager.Awake</c>/<c>Start</c> and, worse, some of them wrote back into the match
/// configuration - a battle royal would switch <c>GameOptions.enemiesEnabled</c> on at scene start,
/// so the scene was deciding a rule the menu had already decided. That decision now belongs to the
/// builder; what is left here only touches scene objects.
/// </summary>
public static class ArenaBootstrap
{
    private const string BasketballGoalObjectName = "basketball_goal";
    private const string ShotClockObjectName = "shot_clock";
    private const string RimObjectName = "rim";

    /// <summary>Applies the mode-driven arena setup.</summary>
    public static void Apply(ResolvedMatchRules rules, bool hasValidatedConfiguration)
    {
        if (rules == null)
        {
            return;
        }

        // Preserved as authored, oddity included: the goal is only hidden for a battle royal in a
        // scene that was entered without a launch. Launching a battle royal from the menu leaves
        // the goal standing, because the old condition also required that no mode had been
        // selected. Changing that is a behaviour fix and belongs in its own change.
        if (!hasValidatedConfiguration && rules.IsBattleRoyal)
        {
            GameObject goal = GameObject.Find(BasketballGoalObjectName);
            if (goal != null)
            {
                goal.SetActive(false);
            }
        }

        // The shot clock renders in world space, so point it at the active camera rather than
        // letting it fall back to an overlay.
        GameObject shotClock = GameObject.Find(ShotClockObjectName);
        if (shotClock != null)
        {
            Canvas canvas = shotClock.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.worldCamera = Camera.main;
            }
        }
    }

    /// <summary>The rim position flattened to ground level, or zero when the scene has no rim.</summary>
    public static Vector3 FindRimVector()
    {
        GameObject rim = GameObject.Find(RimObjectName);
        if (rim == null)
        {
            return Vector3.zero;
        }

        Vector3 position = rim.transform.position;
        return new Vector3(position.x, 0f, position.z);
    }

    /// <summary>
    /// Hides the scene's stand-in for a character the player is currently playing as - the vehicle
    /// or NPC version of them - so the same character is not in the level twice.
    /// </summary>
    public static void HideDuplicateCharacterActors(string characterObjectName, bool trafficEnabled)
    {
        if (string.IsNullOrEmpty(characterObjectName))
        {
            return;
        }

        if (trafficEnabled)
        {
            foreach (GameObject vehicle in GameObject.FindGameObjectsWithTag("vehicle"))
            {
                if (vehicle.name.Contains(characterObjectName))
                {
                    vehicle.SetActive(false);
                }
            }
        }

        foreach (GameObject npc in GameObject.FindGameObjectsWithTag("auto_npc"))
        {
            if (npc.name.Contains(characterObjectName))
            {
                npc.SetActive(false);
            }
        }
    }
}
