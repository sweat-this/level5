using System;
using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Puts the participants, their basketballs and the cheerleader into the scene.
///
/// Lifted out of <c>GameLevelManager</c> unchanged in behaviour, but driven by the roster and the
/// resolved rules instead of by <c>GameOptions.numPlayers</c> and the
/// <c>player1IsCpu</c>..<c>player4IsCpu</c> booleans. The roster already knows what each slot is,
/// so the four near-identical "if slot N and slot N is a CPU" blocks collapse into one loop.
/// </summary>
public sealed class SpawnCoordinator
{
    private readonly SpawnLocations locations;
    private readonly PlayerRegistry registry;
    private readonly ResolvedMatchRules rules;
    private readonly PlayerRoster roster;
    private readonly GameModeId modeId;

    public SpawnCoordinator(
        SpawnLocations locations,
        PlayerRegistry registry,
        ResolvedMatchRules rules,
        PlayerRoster roster,
        GameModeId modeId)
    {
        this.locations = locations;
        this.registry = registry;
        this.rules = rules;
        this.roster = roster;
        this.modeId = modeId;
    }

    /// <summary>The spawn points a gameplay scene has to provide, resolved once.</summary>
    public sealed class SpawnLocations
    {
        public GameObject Player1;
        public GameObject Player2;
        public GameObject Player3;
        public GameObject Player4;
        public GameObject Basketball;
        public GameObject Cheerleader;

        public GameObject ForSlot(int slotId)
        {
            switch (slotId)
            {
                case 0: return Player1;
                case 1: return Player2;
                case 2: return Player3;
                case 3: return Player4;
                default: return null;
            }
        }

        /// <summary>
        /// Finds the spawn points by the names the scenes author them under. Kept as a scene search
        /// for now: replacing all of these with serialized references at once would be a scene edit
        /// across every gameplay scene, which the plan sequences separately.
        /// </summary>
        public static SpawnLocations FindInScene()
        {
            return new SpawnLocations
            {
                Player1 = GameObject.Find("player_spawn_location1"),
                Player2 = GameObject.Find("player_spawn_location2"),
                Player3 = GameObject.Find("player_spawn_location3"),
                Player4 = GameObject.Find("player_spawn_location4"),
                Basketball = GameObject.Find("ball_spawn_location"),
                Cheerleader = GameObject.Find("cheerleader_spawn_location")
            };
        }

        /// <summary>
        /// Whether this scene can seat the given roster. Reports the missing point by name so a
        /// renamed or absent spawn point fails with something actionable instead of a null
        /// reference partway through spawning.
        /// </summary>
        public bool Validate(PlayerRoster roster, ResolvedMatchRules rules)
        {
            if (Player1 == null || Basketball == null)
            {
                Debug.LogError("GameLevelManager missing required player or basketball spawn locations.");
                return false;
            }

            int required = Mathf.Max(roster == null ? 1 : roster.Count, rules != null && rules.AddsImplicitDefender ? 2 : 1);

            if (required > 1 && Player2 == null)
            {
                Debug.LogError("GameLevelManager requires player_spawn_location2 for the selected mode.");
                return false;
            }

            if (required > 2 && Player3 == null)
            {
                Debug.LogError("GameLevelManager requires player_spawn_location3 for three-player games.");
                return false;
            }

            if (required > 3 && Player4 == null)
            {
                Debug.LogError("GameLevelManager requires player_spawn_location4 for four-player games.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Spawns every participant. Throws when a required prefab is missing, which the caller turns
    /// into a disabled manager rather than a half-built level.
    /// </summary>
    public void SpawnPlayers()
    {
        int pid = 0;

        PlayerSlot primarySlot = roster.GetBySlotId(0);
        string primaryObjectName = ResolveObjectName(primarySlot, "drblood");
        string primaryPath = Constants.PREFAB_PATH_CHARACTER_human + primaryObjectName;
        GameObject primaryPrefab = Resources.Load(primaryPath) as GameObject;
        if (primaryPrefab == null)
        {
            throw new InvalidOperationException($"player prefab not found at Resources/{primaryPath}");
        }

        GameObject primary = UnityEngine.Object.Instantiate(
            primaryPrefab,
            locations.Player1.transform.position,
            Quaternion.identity);
        RegisterHuman(primary, pid);
        pid++;

        // A scene can already contain an auto player. It takes the next slot as it always has.
        GameObject sceneAutoPlayer = GameObject.FindWithTag("autoPlayer");
        if (sceneAutoPlayer != null)
        {
            RegisterCpu(sceneAutoPlayer, pid);
            pid++;
        }

        // Lockdown's defender: not a roster slot, spawned by the mode.
        if (rules.AddsImplicitDefender)
        {
            GameObject defenderPrefab = Resources.Load(Constants.PREFAB_PATH_CHARACTER_DEFENSE_cpu + "oldreal") as GameObject;
            if (defenderPrefab == null)
            {
                throw new InvalidOperationException("lockdown defender prefab not found");
            }

            GameObject defender = UnityEngine.Object.Instantiate(
                defenderPrefab,
                locations.Player2.transform.position,
                Quaternion.identity);
            RegisterCpu(defender, pid);
            pid++;
        }

        // CPU shooters are gated by the mode, exactly as they were: a mode that does not allow
        // them spawns nobody past the first slot.
        if (!rules.AllowsCpuShooters)
        {
            return;
        }

        for (int slotId = 1; slotId < roster.Count; slotId++)
        {
            PlayerSlot slot = roster.GetBySlotId(slotId);
            GameObject spawnPoint = locations.ForSlot(slotId);
            if (slot == null || spawnPoint == null)
            {
                continue;
            }

            GameObject prefab = ResolveParticipantPrefab(slot, slotId);
            if (prefab == null)
            {
                Debug.LogError($"GameLevelManager could not load a prefab for roster slot {slotId}.");
                continue;
            }

            GameObject spawned = UnityEngine.Object.Instantiate(prefab, spawnPoint.transform.position, Quaternion.identity);
            if (slot.IsCpu)
            {
                RegisterCpu(spawned, pid);
            }
            else
            {
                RegisterHuman(spawned, pid);
            }

            pid++;
        }
    }

    /// <summary>
    /// Gives each participant a ball. The human prefab and the CPU prefab differ, and the count is
    /// resolved configuration rather than something worked out here.
    /// </summary>
    public void SpawnBasketballs()
    {
        if (registry.Count == 0 || locations.Basketball == null)
        {
            Debug.LogError("Cannot spawn basketballs before players and basketball spawn location are initialized.");
            return;
        }

        GameObject humanBallPrefab = Resources.Load(Constants.PREFAB_PATH_BASKETBALL_human) as GameObject;
        GameObject cpuBallPrefab = Resources.Load(Constants.PREFAB_PATH_BASKETBALL_cpu) as GameObject;
        if (humanBallPrefab == null || cpuBallPrefab == null)
        {
            throw new InvalidOperationException("required human or CPU basketball prefab is missing");
        }

        Vector3 spawnPosition = locations.Basketball.transform.position;

        // Slot 0 always gets a ball; it is the one the HUD and the stats read.
        GiveBall(0, humanBallPrefab, spawnPosition, false);

        if (!rules.AllowsCpuShooters)
        {
            return;
        }

        int balls = Mathf.Min(rules.BasketballCount, registry.Count);
        for (int slotId = 1; slotId < balls; slotId++)
        {
            // Ask the spawned participant, not the roster: when a scene supplies its own auto
            // player it occupies a registry slot the roster knows nothing about, and it is the
            // spawned thing that needs the CPU ball.
            PlayerIdentifier participant = registry.GetBySlot(slotId);
            bool isCpu = participant != null && participant.isCpu;
            GiveBall(slotId, isCpu ? cpuBallPrefab : humanBallPrefab, spawnPosition, isCpu);
        }
    }

    /// <summary>Spawns the chosen cheerleader when the scene has somewhere to put one.</summary>
    public void SpawnCheerleader(string cheerleaderObjectName, float terrainHeight)
    {
        if (GameObject.FindWithTag("cheerleader") != null
            || string.IsNullOrEmpty(cheerleaderObjectName)
            || locations.Cheerleader == null)
        {
            return;
        }

        GameObject prefab = Resources.Load("Prefabs/characters/cheerleaders/cheerleader_" + cheerleaderObjectName) as GameObject;
        if (prefab == null)
        {
            return;
        }

        Vector3 position = locations.Cheerleader.transform.position;
        locations.Cheerleader.transform.position = new Vector3(position.x, terrainHeight, position.z);
        UnityEngine.Object.Instantiate(prefab, locations.Cheerleader.transform.position, Quaternion.identity);
    }

    private GameObject ResolveParticipantPrefab(PlayerSlot slot, int slotId)
    {
        // The campaign picks its opponent from the level's authored CPU character rather than from
        // the roster. That is campaign data and stays where it is until the campaign flow migrates.
        if (slot.IsCpu && modeId == GameModeId.BeatThaComputahs)
        {
            List<LevelSelected> levels = GameOptions.levelsList;
            int levelIndex = GameOptions.levelSelectedIndex;
            if (levels != null && levelIndex >= 0 && levelIndex < levels.Count)
            {
                return levels[levelIndex].CpuPlayer;
            }
        }

        string prefix = slot.IsCpu
            ? Constants.PREFAB_PATH_CHARACTER_cpu
            : Constants.PREFAB_PATH_CHARACTER_human;

        return Resources.Load(prefix + ResolveObjectName(slot, "drblood")) as GameObject;
    }

    private static string ResolveObjectName(PlayerSlot slot, string fallback)
    {
        if (slot == null || slot.Character == null || string.IsNullOrEmpty(slot.Character.ObjectName))
        {
            return fallback;
        }

        return slot.Character.ObjectName;
    }

    private void RegisterHuman(GameObject spawned, int pid)
    {
        PlayerIdentifier identifier = spawned.GetComponent<PlayerIdentifier>();
        if (identifier == null)
        {
            Debug.LogError($"Spawned participant '{spawned.name}' has no PlayerIdentifier.", spawned);
            return;
        }

        identifier.setIds(pid, pid, pid, false);
        identifier.player = spawned;
        identifier.setPlayer(identifier.player);
        registry.Add(identifier);
    }

    private void RegisterCpu(GameObject spawned, int pid)
    {
        PlayerIdentifier identifier = spawned.GetComponent<PlayerIdentifier>();
        if (identifier == null)
        {
            Debug.LogError($"Spawned participant '{spawned.name}' has no PlayerIdentifier.", spawned);
            return;
        }

        identifier.setIds(pid, pid, pid, true);
        identifier.autoPlayer = spawned;
        identifier.setAutoPlayer(identifier.autoPlayer);
        registry.Add(identifier);
    }

    private void GiveBall(int slotId, GameObject prefab, Vector3 position, bool forCpu)
    {
        PlayerIdentifier owner = registry.GetBySlot(slotId);
        if (owner == null)
        {
            return;
        }

        GameObject ball = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        PlayerIdentifier ballIdentifier = ball.GetComponent<PlayerIdentifier>();
        if (ballIdentifier == null)
        {
            Debug.LogError($"Basketball prefab '{prefab.name}' has no PlayerIdentifier.", ball);
            return;
        }

        ballIdentifier.setIds(owner.pid, owner.pid, owner.pid, forCpu);
        if (forCpu)
        {
            ballIdentifier.setAutoBasketball(ball);
            owner.setAutoBasketball(ball);
            ballIdentifier.setAutoPlayer(owner.autoPlayer);
        }
        else
        {
            ballIdentifier.setBasketball(ball);
            owner.setBasketball(ball);
            ballIdentifier.setPlayer(owner.player);
        }
    }
}
