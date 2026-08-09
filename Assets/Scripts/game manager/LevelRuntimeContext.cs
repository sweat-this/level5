using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// The gameplay scene's composition root.
///
/// One place that says what this scene is playing and which services it has, so a new system can
/// take a reference to this instead of reaching for another static singleton. It holds references
/// and validates them; it does not contain gameplay logic. If something here starts deciding what
/// happens during a match, it belongs in one of the services it exposes, not in the root - the
/// whole point is to not grow a second GameLevelManager under a new name.
/// </summary>
[DefaultExecutionOrder(-100)]
public class LevelRuntimeContext : MonoBehaviour
{
    [SerializeField] private MatchController matchController;

    public static LevelRuntimeContext instance;

    /// <summary>The validated configuration for this match, or null when the scene was entered directly.</summary>
    public MatchConfiguration Configuration => MatchRuntime.Configuration;

    /// <summary>The rules being played under, whether or not a launch produced them.</summary>
    public ResolvedMatchRules Rules { get; private set; }

    /// <summary>Who should be playing.</summary>
    public PlayerRoster Roster { get; private set; }

    /// <summary>Who is actually in the scene.</summary>
    public PlayerRegistry Players { get; private set; }

    public MatchController MatchController => matchController;

    /// <summary>True when a validated configuration produced this match.</summary>
    public bool HasValidatedConfiguration => MatchRuntime.HasConfiguration;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Only this component, never the object it sits on - it is attached to a manager that
            // has nothing to do with the duplication.
            Destroy(this);
            return;
        }

        instance = this;
        MatchRuntime.WarnIfUnconfigured(this);

        // Snapshotted here rather than read through on every access: within a scene these do not
        // change, and pinning them makes it obvious that nothing during the match is allowed to.
        Rules = MatchRuntime.Rules;
        Roster = MatchRuntime.Roster;
        Players = new PlayerRegistry();

        if (matchController == null)
        {
            matchController = GetComponent<MatchController>() ?? FindAnyObjectByType<MatchController>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Adopts a registry another system already built. During migration
    /// <c>GameLevelManager</c> owns the spawning, so the registry it fills is the one this exposes
    /// rather than a second copy that could disagree with it.
    /// </summary>
    public void AdoptPlayerRegistry(PlayerRegistry registry)
    {
        if (registry != null)
        {
            Players = registry;
        }
    }

    /// <summary>Ends the match through the one door, when a controller exists to hear it.</summary>
    public bool RequestMatchEnd(MatchEndReason reason)
    {
        return matchController != null && matchController.RequestEnd(reason);
    }
}
