using System;
using System.Collections.Generic;
using Level5.Core;
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
    private readonly IGroundHeightProvider groundHeightProvider;
    private readonly bool hasActiveMatchConfiguration;

    /// <summary>
    /// AUD-010 Phase 1c: <paramref name="groundHeightProvider"/> is optional so every existing direct
    /// construction site (tests exercising <c>RegisterHuman</c>/<c>RegisterCpu</c>/<c>GiveBall</c>
    /// without a running <c>GameLevelManager</c>) keeps compiling unchanged. Production
    /// (<c>GameLevelManager</c>) always supplies one; a human ball spawned through a coordinator built
    /// without one fails clearly in its own <c>Start()</c> instead of silently inventing a fallback -
    /// see <see cref="GiveBall"/> and <c>BasketBall.BindGroundHeightProvider</c>.
    ///
    /// AUD-010 Phase 2b0: <see cref="hasActiveMatchConfiguration"/> is captured here rather than taken
    /// as a parameter, exactly once for this coordinator's lifetime - mirroring how <paramref
    /// name="rules"/> is already owned - instead of re-reading <c>MatchRuntime.HasConfiguration</c> on
    /// every <see cref="BindRangeMeters"/> call. Production (<c>GameLevelManager.Awake</c>) always
    /// constructs a coordinator after <c>ActiveMatch</c> is already established for this scene load and
    /// before any participant is spawned, so this capture point is equivalent to reading it at the top
    /// of <see cref="SpawnPlayers"/> - but it is now a structural fact instead of one relying on
    /// re-reads happening to agree.
    /// </summary>
    public SpawnCoordinator(
        SpawnLocations locations,
        PlayerRegistry registry,
        ResolvedMatchRules rules,
        PlayerRoster roster,
        GameModeId modeId,
        IGroundHeightProvider groundHeightProvider = null)
    {
        this.locations = locations;
        this.registry = registry;
        this.rules = rules;
        this.roster = roster;
        this.modeId = modeId;
        this.groundHeightProvider = groundHeightProvider;
        this.hasActiveMatchConfiguration = MatchRuntime.HasConfiguration;
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
        RegisterHuman(primary, pid, primarySlot);
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

            // DEF-3: tell the defender who it guards rather than letting it reach for
            // GameLevelManager.instance.players[0] itself. This is the mode that spawns it, so
            // this is the one place that knows.
            AutoPlayerDefense defense = defender.GetComponent<AutoPlayerDefense>();
            if (defense != null)
            {
                defense.AssignGuardedPlayer(registry.GetBySlot(0));
            }

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
                RegisterHuman(spawned, pid, slot);
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

    /// <summary>
    /// AUD-012 Phase 2b Slice 21: forwards already-resolved arena context - the basketball rim vector
    /// and the live ground-height provider - to every registered human participant's already-wired
    /// <see cref="PlayerController"/>, replacing that controller's former direct
    /// <c>GameLevelManager.instance.BasketballRimVector</c>/<c>TerrainHeight</c> reads. Called once
    /// from <c>GameLevelManager.Start()</c>, after <c>ArenaBootstrap.Apply</c> has resolved the final
    /// rim - never from <see cref="SpawnPlayers"/>/<see cref="RegisterHuman"/>, when the rim is not yet
    /// ready. CPU participants are unaffected: <c>AutoPlayerController</c> does not take this context.
    /// </summary>
    public void BindHumanArenaContext(Vector3 basketballRimVector, IGroundHeightProvider groundHeightProvider)
    {
        BindEveryHumanController(
            "arena context",
            controller => controller.BindArenaContext(basketballRimVector, groundHeightProvider));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 26: forwards the scene's legacy mobile joystick axes to every registered
    /// human participant's <see cref="PlayerController"/>, replacing <c>PlayerInputReader</c>'s former
    /// direct <c>GameLevelManager.instance.Joystick</c> read - that class's last edge into
    /// <c>Assembly-CSharp</c>. <paramref name="legacyTouchMovementReader"/> is a
    /// <see cref="Func{TResult}"/> so the axes stay synchronous: the reader asks for them when it runs,
    /// as the old singleton read did, rather than consuming a value cached a frame earlier. The
    /// <c>FloatingJoystick</c> itself is never handed across - only its current values - so
    /// <c>Level5.Input</c> gains no dependency on it.
    ///
    /// Called from <c>GameLevelManager.Awake</c>'s spawn pass rather than <c>Start()</c>, unlike
    /// <see cref="BindHumanArenaContext"/>: the joystick is resolved before the coordinator is even
    /// constructed, so there is nothing to wait for, and binding inside <c>Awake</c> guarantees the
    /// source is present before any spawned <c>PlayerController.Start()</c> builds its first reader.
    /// Ordering is not load-bearing regardless - <c>PlayerController</c> stores this independently of
    /// its current reader and hands every reader an indirection that resolves it at call time.
    ///
    /// CPU participants are deliberately skipped: <c>AutoPlayerController</c> reads no player input.
    /// </summary>
    public void BindHumanLegacyTouchMovement(Func<Vector2> legacyTouchMovementReader)
    {
        BindEveryHumanController(
            "legacy touch movement",
            controller => controller.BindLegacyTouchMovementReader(legacyTouchMovementReader));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 32: forwards the scene's live shrink-camera resolver to every registered
    /// human participant's <see cref="PlayerController"/>, replacing <c>PlayerDamageReactions</c>'
    /// former direct <c>CameraManager.instance.Cameras[0]</c> read. <paramref
    /// name="damageReactionCameraReader"/> is a <see cref="Func{TResult}"/>, not a captured
    /// <c>Camera</c>, so the shrink reaction resolves it live when shrink begins - the same timing the
    /// old direct read had - rather than a snapshot taken at composition time.
    ///
    /// Called from <c>GameLevelManager.Awake</c>'s spawn pass, adjacent to
    /// <see cref="BindHumanLegacyTouchMovement"/>: ordering is not load-bearing regardless, since
    /// <c>PlayerController</c> stores this independently of <c>PlayerDamageReactions</c> and only
    /// invokes it when the helper actually asks for a shrink camera.
    ///
    /// CPU participants are deliberately skipped: <c>AutoPlayerController</c>'s reactions have no
    /// shrink behavior to bind a camera for.
    /// </summary>
    public void BindHumanDamageReactionCamera(Func<Camera> damageReactionCameraReader)
    {
        BindEveryHumanController(
            "damage reaction camera",
            controller => controller.BindDamageReactionCameraReader(damageReactionCameraReader));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 33: forwards the scene's live idle-sniper runtime resolver to every
    /// registered human participant's <see cref="PlayerController"/>, replacing that controller's
    /// former direct <c>SniperManager.instance</c> read. <paramref name="reader"/> is a
    /// <see cref="Func{TResult}"/>, not a captured <c>IPlayerIdleSniperRuntime</c>, so the idle-sniper
    /// check resolves it live at the point of use - the same timing the old direct static read had -
    /// rather than a snapshot taken at composition time. No sniper runtime needs to exist yet when
    /// this is called.
    ///
    /// Called from <c>GameLevelManager.Awake</c>'s spawn pass, adjacent to
    /// <see cref="BindHumanLegacyTouchMovement"/>/<see cref="BindHumanDamageReactionCamera"/>: ordering
    /// is not load-bearing regardless, since <c>PlayerController</c> stores this independently and only
    /// invokes it from its own idle-sniper check.
    ///
    /// CPU participants are deliberately skipped: <c>AutoPlayerController</c> has no idle-sniper check.
    /// </summary>
    public void BindHumanIdleSniperRuntime(Func<IPlayerIdleSniperRuntime> reader)
    {
        BindEveryHumanController(
            "idle sniper runtime",
            controller => controller.BindIdleSniperRuntimeReader(reader));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 37: forwards the scene's live <see cref="IPlayerMatchRuntime"/> boundary -
    /// <c>GameLevelManager</c>'s explicit forwarding implementation over <c>MatchRuntime</c> - to every
    /// registered human participant's <see cref="PlayerController"/>, replacing that controller's former
    /// direct <c>MatchRuntime.Rules</c>/<c>CustomCamera</c>/<c>LocalInputSlotFor</c> reads - its last
    /// direct <c>Assembly-CSharp</c> dependency. Called from <c>GameLevelManager.Awake</c>'s spawn pass,
    /// adjacent to the other <c>BindHuman*</c> calls: ordering is not load-bearing regardless, since
    /// <c>PlayerController</c> only dereferences the bound provider from its own <c>Start()</c>, which
    /// cannot run before <c>Awake</c> finishes.
    ///
    /// CPU participants are deliberately skipped: <c>AutoPlayerController</c> still reads
    /// <c>MatchRuntime</c> directly.
    /// </summary>
    public void BindHumanMatchRuntime(IPlayerMatchRuntime runtime)
    {
        BindEveryHumanController(
            "match runtime",
            controller => controller.BindMatchRuntime(runtime));
    }

    /// <summary>
    /// AUD-012 Phase 2b: the CPU twin of <see cref="BindHumanMatchRuntime"/>, forwarding the same live
    /// <see cref="IPlayerMatchRuntime"/> boundary to every registered CPU participant's
    /// <see cref="AutoPlayerController"/>, replacing that controller's former direct
    /// <c>MatchRuntime.Rules</c>/<c>CustomCamera</c>/<c>LevelHasSevenPointers</c> reads. Called from
    /// <c>GameLevelManager.Awake</c>'s spawn pass, adjacent to <see cref="BindHumanMatchRuntime"/>.
    /// </summary>
    public void BindCpuMatchRuntime(IPlayerMatchRuntime runtime)
    {
        foreach (PlayerIdentifier participant in registry.Participants)
        {
            if (participant == null || !participant.isCpu)
            {
                continue;
            }

            AutoPlayerController controller = participant.autoPlayerController;
            if (controller == null)
            {
                continue;
            }

            controller.BindMatchRuntime(runtime);
        }
    }

    /// <summary>
    /// AUD-012 Phase 2b: the CPU twin of <see cref="BindHumanArenaContext"/>, forwarding the same
    /// finalized rim vector and live ground-height provider to every registered CPU participant's
    /// <see cref="AutoPlayerController"/> and <see cref="AutoPlayerDefense"/>, replacing their former
    /// direct <c>GameLevelManager.instance.BasketballRimVector</c>/<c>TerrainHeight</c> reads. Called
    /// once from <c>GameLevelManager.Start()</c>, after <c>ArenaBootstrap.Apply</c> has resolved the
    /// final rim - the same point <see cref="BindHumanArenaContext"/> is called from. A CPU shooter
    /// carries an <see cref="AutoPlayerController"/>, the Lockdown defender carries an
    /// <see cref="AutoPlayerDefense"/>; both are bound here independently since neither component is
    /// guaranteed present on a given CPU participant.
    /// </summary>
    public void BindCpuArenaContext(Vector3 basketballRimVector, IGroundHeightProvider groundHeightProvider)
    {
        foreach (PlayerIdentifier participant in registry.Participants)
        {
            if (participant == null || !participant.isCpu)
            {
                continue;
            }

            AutoPlayerController controller = participant.autoPlayerController;
            if (controller != null)
            {
                controller.BindArenaContext(basketballRimVector, groundHeightProvider);
            }

            AutoPlayerDefense defense = participant.GetComponent<AutoPlayerDefense>();
            if (defense != null)
            {
                defense.BindArenaContext(basketballRimVector, groundHeightProvider);
            }
        }
    }

    /// <summary>
    /// The one human-participant iteration every <c>BindHuman*</c> pass shares: registered, non-CPU,
    /// with a <see cref="PlayerController"/> to bind to. A human whose prefab has no controller fails
    /// closed on that participant with a named error and the pass continues, rather than throwing and
    /// aborting binding for everyone after it.
    /// </summary>
    private void BindEveryHumanController(string context, Action<PlayerController> bind)
    {
        foreach (PlayerIdentifier participant in registry.Participants)
        {
            if (participant == null || participant.isCpu)
            {
                continue;
            }

            PlayerController controller = participant.playerController;
            if (controller == null)
            {
                Debug.LogError($"Registered human participant '{participant.name}' has no PlayerController to bind {context} to.", participant);
                continue;
            }

            bind(controller);
        }
    }

    /// <summary>
    /// Spawns the chosen cheerleader when the scene has somewhere to put one.
    ///
    /// CHR-5: the cheerleader's stat bonuses and the cheerleader you can see reach the match by
    /// completely separate routes - the bonuses through CheerleaderSelection into CharacterProfile,
    /// the actor through this Resources.Load by name - joined only by the convention that the
    /// prefab is named for the selection's ObjectName. Nothing checks that they agree. A failed
    /// load used to return silently, leaving a match that is quietly paying out bonuses for a
    /// cheerleader who is not there; it now says so.
    /// </summary>
    public void SpawnCheerleader(string cheerleaderObjectName, float terrainHeight)
    {
        if (GameObject.FindWithTag("cheerleader") != null
            || string.IsNullOrEmpty(cheerleaderObjectName)
            || locations.Cheerleader == null)
        {
            return;
        }

        // "none" is the authored default selection, not a missing prefab. It reached the Resources
        // load below and reported every cheerleader-less match as an error, which is most of them.
        // There is no bonus mismatch to warn about here either - the "none" record carries no
        // bonuses, so nothing is being paid out for an absent actor.
        if (string.Equals(cheerleaderObjectName, CheerleaderProfile.NoneObjectName,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string prefabPath = "Prefabs/characters/cheerleaders/cheerleader_" + cheerleaderObjectName;
        GameObject prefab = Resources.Load(prefabPath) as GameObject;
        if (prefab == null)
        {
            Debug.LogError(
                $"Cheerleader '{cheerleaderObjectName}' has no prefab at Resources/{prefabPath}, "
                + "but its shooting bonuses are already applied to the player.");
            return;
        }

        Vector3 position = locations.Cheerleader.transform.position;
        locations.Cheerleader.transform.position = new Vector3(position.x, terrainHeight, position.z);
        GameObject cheerleader = UnityEngine.Object.Instantiate(prefab, locations.Cheerleader.transform.position, Quaternion.identity);
        BindPlayerAnimationEventsContext(cheerleader);
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

    private void RegisterHuman(GameObject spawned, int pid, PlayerSlot slot)
    {
        PlayerIdentifier identifier = spawned.GetComponent<PlayerIdentifier>();
        if (identifier == null)
        {
            Debug.LogError($"Spawned participant '{spawned.name}' has no PlayerIdentifier.", spawned);
            return;
        }

        identifier.setIds(pid, false);
        identifier.player = spawned;
        identifier.setPlayer(identifier.player);
        InitializeHumanProfile(identifier, slot);
        BindRangeMeters(spawned, identifier.Actor, isCpu: false);
        BindShotMeters(spawned, identifier.Actor, isCpu: false);
        BindCallBallMatchRules(spawned);
        BindPlayerHealthMatchRules(spawned);
        BindPlayerAttackQueueContext(spawned);
        BindPlayerCollisionsContext(spawned, slot);
        BindPlayerAnimationEventsContext(spawned);
        registry.Add(identifier);
    }

    /// <summary>
    /// Rebuilds a human's CharacterProfile from the saved data for that human's own roster slot.
    ///
    /// This used to happen inside <c>PlayerIdentifier.setPlayer</c>, which had no idea which slot
    /// it was wiring and so always loaded <c>MatchRuntime.PrimaryCharacterId</c> - slot zero. Every
    /// human past the first therefore played with slot zero's stats, level, display name and
    /// PlayerId. Only this class knows the roster, so the decision belongs here.
    ///
    /// A slot with no character of its own still falls back to the primary id, which is what a
    /// single-human match has always resolved to.
    ///
    /// AUD-012 Phase 2b Slice 27: the context the profile used to discover for itself - the saved
    /// profile lookup (<c>LoadedData</c>), the cheerleader and the resolved rules - is now handed to it
    /// from here. That preparation deliberately happens <i>before</i> the configured-match gate below:
    /// a directly entered gameplay scene still skips the saved-profile rebuild, exactly as it always
    /// has, but its humans must still reach <c>CharacterProfile.Start</c> holding this match's rules,
    /// which is what the Arcade/easy maximum-stat override reads. Only the rebuild is configured-match
    /// work; the rules are not.
    ///
    /// The gate reads this coordinator's captured <see cref="hasActiveMatchConfiguration"/> rather than
    /// <c>MatchRuntime.HasConfiguration</c>. The live re-read existed only because this method used to
    /// be <c>static</c>; now that it is an instance method it uses the value this class already owns,
    /// which is the point of capturing it once (see the constructor).
    /// </summary>
    private void InitializeHumanProfile(PlayerIdentifier identifier, PlayerSlot slot)
    {
        if (identifier.characterProfile == null)
        {
            Debug.LogError($"Spawned human '{identifier.name}' has no CharacterProfile to initialize.", identifier);
            return;
        }

        identifier.characterProfile.PrepareHumanMatchContext(
            ResolveLoadedCharacterProfile,
            MatchRuntime.Cheerleader,
            rules);

        if (!hasActiveMatchConfiguration)
        {
            return;
        }

        identifier.characterProfile.intializeShooterStatsFromProfile(ResolveHumanCharacterId(slot));
    }

    /// <summary>
    /// The saved-profile lookup <see cref="InitializeHumanProfile"/> hands to a human's
    /// <c>CharacterProfile</c>, so that type asks for a saved profile without knowing that
    /// <c>LoadedData</c> is what answers (AUD-012 Phase 2b Slice 27). Deliberately a narrow adapter
    /// over the existing accessor - the null guard is the one the profile itself used to carry, moved
    /// to the side that owns the singleton - and not a persistence redesign.
    /// </summary>
    private static CharacterProfile ResolveLoadedCharacterProfile(int characterId)
    {
        return LoadedData.instance != null
            ? LoadedData.instance.getSelectedCharacterProfile(characterId)
            : null;
    }

    /// <summary>
    /// Which saved character a human roster slot loads its stats from: its own, falling back to
    /// the primary slot's id when the slot carries no character of its own.
    ///
    /// Separated from <see cref="InitializeHumanProfile"/> so the rule that regressed - every
    /// human resolving to slot zero - is coverable without spawning prefabs.
    /// </summary>
    public static int ResolveHumanCharacterId(PlayerSlot slot)
    {
        return slot != null && slot.Character != null && slot.Character.CharacterId != 0
            ? slot.Character.CharacterId
            : MatchRuntime.PrimaryCharacterId;
    }

    private void RegisterCpu(GameObject spawned, int pid)
    {
        PlayerIdentifier identifier = spawned.GetComponent<PlayerIdentifier>();
        if (identifier == null)
        {
            Debug.LogError($"Spawned participant '{spawned.name}' has no PlayerIdentifier.", spawned);
            return;
        }

        identifier.setIds(pid, true);
        identifier.autoPlayer = spawned;
        identifier.setAutoPlayer(identifier.autoPlayer);
        PrepareCpuMatchContext(identifier);
        BindRangeMeters(spawned, identifier.Actor, isCpu: true);
        BindShotMeters(spawned, identifier.Actor, isCpu: true);
        BindCallBallMatchRules(spawned);
        BindPlayerHealthMatchRules(spawned);
        BindPlayerAttackQueueContext(spawned);
        BindCpuParticipantRegistry(spawned);
        BindAutoPlayerCollisionsContext(spawned);
        BindPlayerAnimationEventsContext(spawned);
        registry.Add(identifier);
    }

    /// <summary>
    /// AUD-012 Phase 2b: binds this coordinator's own <see cref="registry"/> to the spawned CPU's
    /// <see cref="AutoPlayerController"/> (score-deficit shot selection) and/or
    /// <see cref="AutoPlayerDefense"/> (the lockdown defender's primary-human fallback), replacing
    /// their former direct <c>GameLevelManager.instance.players</c>/<c>.Player1</c> reads. Bound
    /// immediately - unlike the arena-context pass, which must wait for <c>GameLevelManager.Start()</c>
    /// to finalize the rim - since the registry reference itself never changes for the life of this
    /// coordinator, only its contents grow as more participants register.
    /// </summary>
    private void BindCpuParticipantRegistry(GameObject participant)
    {
        AutoPlayerController controller = participant.GetComponent<AutoPlayerController>();
        if (controller != null)
        {
            controller.BindParticipantRegistry(registry);
        }

        AutoPlayerDefense defense = participant.GetComponent<AutoPlayerDefense>();
        if (defense != null)
        {
            defense.BindParticipantRegistry(registry);
        }
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 24: binds this coordinator's already-resolved <see cref="rules"/> to the
    /// participant's own <c>CallBallToPlayer</c>, from both <see cref="RegisterHuman"/> and
    /// <see cref="RegisterCpu"/> - the two composition paths that produce a participant carrying one -
    /// so that component's existing call-enabled policy no longer reads <c>MatchRuntime.Rules</c>
    /// itself. Called during <c>GameLevelManager.Awake</c>'s spawn pass, so it always precedes that
    /// component's own <c>Start()</c>.
    ///
    /// <c>GetComponent</c>, not <c>GetComponentsInChildren</c>: this component is authored on the
    /// participant root, which is where both <c>PlayerController</c> and <c>AutoPlayerController</c>
    /// resolve it from. A participant without one is not a defect and is silently skipped - Lockdown's
    /// defender prefab (<c>cpu_player_defense_oldreal</c>) carries no <c>CallBallToPlayer</c> at all.
    /// Nothing is added here: composition supplies the rules, it does not compose the participant, and
    /// it does not decide whether calling the ball is enabled.
    /// </summary>
    private void BindCallBallMatchRules(GameObject participant)
    {
        CallBallToPlayer callBall = participant.GetComponent<CallBallToPlayer>();
        if (callBall == null)
        {
            return;
        }

        callBall.BindMatchRules(rules);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 25: binds this coordinator's already-resolved <see cref="rules"/> to the
    /// participant's own <c>PlayerHealth</c>, from both <see cref="RegisterHuman"/> and
    /// <see cref="RegisterCpu"/> - mirroring <see cref="BindCallBallMatchRules"/> above - so that
    /// component's existing regeneration gate no longer reads <c>MatchRuntime.Rules</c> itself. Called
    /// during <c>GameLevelManager.Awake</c>'s spawn pass, so it always precedes that component's own
    /// <c>Start()</c>.
    ///
    /// <c>GetComponentInChildren</c>, not <c>GetComponent</c>: unlike <c>CallBallToPlayer</c>, every
    /// live consumer resolves this component that way (<c>PlayerController</c>,
    /// <c>AutoPlayerController</c>, <c>PlayerCollisions</c>, <c>AutoPlayerCollisions</c> and
    /// <c>GameLevelManager</c> all use <c>GetComponentInChildren&lt;PlayerHealth&gt;()</c>), so
    /// composition must not assume it is authored on the participant root. <c>(true)</c> so an
    /// inactive authored copy is reached as well; binding has no side effects. A participant without
    /// one is not a defect and is silently skipped. Nothing is added here: composition supplies the
    /// rules, it does not compose the participant, and it does not decide when regeneration runs.
    /// </summary>
    private void BindPlayerHealthMatchRules(GameObject participant)
    {
        PlayerHealth health = participant.GetComponentInChildren<PlayerHealth>(true);
        if (health == null)
        {
            return;
        }

        health.BindMatchRules(rules);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 30: binds this coordinator's already-resolved <see cref="rules"/> and the
    /// spawned participant root itself, as the queue's anchor <c>Transform</c>, to the participant's own
    /// <c>PlayerAttackQueue</c>, from both <see cref="RegisterHuman"/> and <see cref="RegisterCpu"/> -
    /// mirroring <see cref="BindPlayerHealthMatchRules"/> above - so that component's former
    /// <c>MatchRuntime.Rules</c> read and its own <c>PlayerIdentifier</c> lookup are both replaced by
    /// composition. Called during <c>GameLevelManager.Awake</c>'s spawn pass, so it always precedes that
    /// component's own <c>Start()</c>.
    ///
    /// <c>GetComponent</c>, not <c>GetComponentsInChildren</c>: like <c>CallBallToPlayer</c>, this
    /// component is authored on the participant root, which is where its own former
    /// <c>GetComponent&lt;PlayerIdentifier&gt;()</c> resolved from. A participant without one is not a
    /// defect and is silently skipped. Nothing is added here: composition supplies the rules and the
    /// anchor, it does not compose the participant, and it does not decide queue capacity or slot
    /// selection.
    /// </summary>
    private void BindPlayerAttackQueueContext(GameObject participant)
    {
        PlayerAttackQueue queue = participant.GetComponent<PlayerAttackQueue>();
        if (queue == null)
        {
            return;
        }

        queue.BindMatchContext(rules, participant.transform);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 50a: binds this coordinator's already-resolved <see cref="rules"/>, this
    /// human's own roster-slot spawn point (the fall-respawn destination), and the
    /// <c>GameRules.killedOnIdle</c> forwarding callback to the participant's own
    /// <see cref="PlayerCollisions"/>, from <see cref="RegisterHuman"/> only -
    /// <see cref="PlayerCollisions"/> is the human-only collision handler;
    /// <see cref="AutoPlayerCollisions"/> is its CPU twin, bound separately below. Replaces that
    /// component's former direct <c>MatchRuntime.Rules</c> read and its
    /// <c>GameLevelManager.instance.Player1</c>/<c>PlayerSpawnLocation</c>/<c>GameRules.instance</c>
    /// reach-throughs. <c>GetComponentsInChildren</c>, not <c>GetComponent</c>: this component is
    /// authored on the hitbox child, not the participant root, mirroring how it resolves its own
    /// <c>PlayerIdentifier</c> via <c>GetComponentInParent</c>.
    ///
    /// <paramref name="slot"/> is <see cref="RegisterHuman"/>'s own roster slot for this participant,
    /// resolved through <see cref="SpawnLocations.ForSlot"/> - the same slot-to-spawn mapping
    /// <see cref="SpawnPlayers"/> already uses to place the participant in the first place. Slice 50
    /// bound every human to <see cref="SpawnLocations.Player1"/> unconditionally, so a second local
    /// human who fell off the level respawned at slot 0's spawn point instead of their own. A null
    /// slot (no known production call site; only direct-construction tests exercise
    /// <see cref="RegisterHuman"/> without one) falls back to <see cref="SpawnLocations.Player1"/>,
    /// preserving the original single-human behavior rather than inventing a new destination. A slot
    /// whose <c>ForSlot</c> result is missing from the scene binds a null destination, which
    /// <see cref="PlayerCollisions.OnTriggerEnter"/> already treats as "do not teleport" - scene
    /// validation (<see cref="SpawnLocations.Validate"/>) is what actually guarantees a required spawn
    /// point exists, not this bind.
    /// </summary>
    private void BindPlayerCollisionsContext(GameObject participant, PlayerSlot slot)
    {
        GameObject respawnPoint = slot != null ? locations.ForSlot(slot.SlotId) : locations.Player1;
        Transform respawnDestination = respawnPoint != null ? respawnPoint.transform : null;

        foreach (PlayerCollisions collisions in participant.GetComponentsInChildren<PlayerCollisions>(true))
        {
            collisions.BindMatchRules(rules);
            collisions.BindFallRespawnDestination(respawnDestination);
            collisions.BindKilledOnIdleCallback(MarkKilledOnIdle);
        }
    }

    /// <summary>
    /// AUD-012 Phase 2b: the CPU twin of <see cref="BindPlayerCollisionsContext"/>, for
    /// <see cref="AutoPlayerCollisions"/> from <see cref="RegisterCpu"/> only. No
    /// <c>GameRules.killedOnIdle</c> callback: unlike its human twin, <see cref="AutoPlayerCollisions"/>
    /// never read that field.
    ///
    /// AUD-012 Phase 2b Slice 50a: deliberately still binds <see cref="SpawnLocations.Player1"/>
    /// unconditionally, unlike its human twin's slot-aware destination since that slice. Not an
    /// oversight - CPU fall-respawn is a separate, currently inert behavior:
    /// <c>AutoPlayerCollisions.OnTriggerEnter</c>'s fall branch tests <c>CompareTag("playerHitbox")</c>,
    /// but authored CPU hitboxes carry <c>autoPlayerHitbox</c>, so this destination is never actually
    /// reached today. Giving CPUs a per-slot destination here requires its own intended-behavior
    /// decision (and likely the tag fix first), not a mechanical copy of the human fix.
    /// </summary>
    private void BindAutoPlayerCollisionsContext(GameObject participant)
    {
        foreach (AutoPlayerCollisions collisions in participant.GetComponentsInChildren<AutoPlayerCollisions>(true))
        {
            collisions.BindMatchRules(rules);
            collisions.BindFallRespawnDestination(locations.Player1 != null ? locations.Player1.transform : null);
        }
    }

    /// <summary>
    /// Marks the match as killed-on-idle, forwarding to the same <c>GameRules.instance.killedOnIdle</c>
    /// field <see cref="PlayerCollisions"/> used to write directly. A named method rather than a
    /// captured lambda so the exact behaviour (including the lack of a null guard - <c>GameRules</c>
    /// is expected to exist for the life of a gameplay scene, matching the original direct write) is
    /// visible at a glance rather than inlined at every call site.
    /// </summary>
    private static void MarkKilledOnIdle()
    {
        GameRules.instance.killedOnIdle = true;
    }

    /// <summary>
    /// AUD-012 Phase 2b: binds the spawned participant's <see cref="PlayerAnimationEvents"/> (present
    /// on human, CPU and cheerleader actors alike - see <see cref="SpawnCheerleader"/>) to a projectile
    /// spawn delegate and a live "does this scene have an auto player" reader, replacing that
    /// component's former direct <c>ProjectilePool.Spawn</c> call and
    /// <c>GameLevelManager.instance.AutoPlayer</c> read. <c>ProjectilePool</c> lives in loose
    /// <c>Assets/Scripts/projectile/</c> (<c>Assembly-CSharp</c>, not <c>Level5.Pooling</c>), so a
    /// direct reference is not a legal leaf dependency for <c>Level5.Player</c> the way <c>SFXBB</c> is -
    /// this is the "single outward action" delegate the architecture rules call for instead.
    /// <c>GetComponentsInChildren</c>, not <c>GetComponent</c>: this component sits under the actor's
    /// animator hierarchy, not the participant root.
    /// </summary>
    private void BindPlayerAnimationEventsContext(GameObject participant)
    {
        foreach (PlayerAnimationEvents animationEvents in participant.GetComponentsInChildren<PlayerAnimationEvents>(true))
        {
            animationEvents.BindProjectileSpawner(SpawnProjectile);
            animationEvents.BindHasAutoPlayerReader(HasAutoPlayer);
        }
    }

    private static GameObject SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return ProjectilePool.Spawn(prefab, position, rotation);
    }

    private static bool HasAutoPlayer()
    {
        return GameLevelManager.instance != null && GameLevelManager.instance.AutoPlayer != null;
    }

    /// <summary>
    /// AUD-010 Phase 1c: binds every RangeMeter under the spawned participant to that participant's
    /// own IShooterActor, immediately after the actor is resolved and before Unity calls Start() on
    /// any of them - the same explicit-binding shape GiveBall already uses for basketball ownership
    /// (see below). Most player prefabs carry no RangeMeter at all; GetComponentsInChildren(true) also
    /// reaches inactive/disabled authored copies, which is harmless since binding itself has no
    /// presentation side effects.
    ///
    /// AUD-010 Phase 2b0: also binds this coordinator's already-resolved <see cref="rules"/> and its
    /// captured <see cref="hasActiveMatchConfiguration"/> to the same meters, mirroring
    /// <see cref="BindShotMeters"/>, so a RangeMeter never has to reach for MatchRuntime itself. An
    /// instance method (unlike the previous static shape) so it can reach the coordinator's own fields
    /// without threading them through as parameters.
    ///
    /// Code review note: this binds through the concrete RangeMeter type rather than an interface,
    /// unlike GiveBall's IBasketballRuntime below - RangeMeter has exactly one implementation, so an
    /// interface here would be ceremony with no second consumer. Widens the game-manager -> basketball
    /// edge by one concrete reference; worth naming explicitly the next time that edge is remeasured.
    /// </summary>
    private void BindRangeMeters(GameObject participant, IShooterActor actor, bool isCpu)
    {
        foreach (RangeMeter meter in participant.GetComponentsInChildren<RangeMeter>(true))
        {
            meter.BindOwner(actor, isCpu);
            meter.BindMatchContext(rules, hasActiveMatchConfiguration);
        }
    }

    /// <summary>
    /// AUD-010 Phase 1c: binds every ShotMeter under the spawned participant to that participant's
    /// own IShooterActor, mirroring <see cref="BindRangeMeters"/> above. Runs before Unity calls
    /// Start() on any of them. A participant's basketball runtime (needed only for a CPU's automatic
    /// meter resolution) is bound separately, once that participant's ball exists - see GiveBall.
    ///
    /// AUD-010 Phase 2b0: also binds this coordinator's already-resolved <see cref="rules"/> to the
    /// same meters, immediately alongside actor ownership, so a ShotMeter never has to reach for
    /// MatchRuntime itself. An instance method (unlike the static <see cref="BindRangeMeters"/>) so it
    /// can reach the coordinator's own <see cref="rules"/> field without threading it through as a
    /// parameter.
    /// </summary>
    private void BindShotMeters(GameObject participant, IShooterActor actor, bool isCpu)
    {
        foreach (ShotMeter meter in participant.GetComponentsInChildren<ShotMeter>(true))
        {
            meter.BindOwner(actor, isCpu);
            meter.BindMatchRules(rules);
        }
    }

    /// <summary>
    /// Gives the spawned CPU's CharacterProfile the primary human's Level and the resolved rules for
    /// this match, before CharacterProfile.Start applies Hardcore/contest initialization (#71).
    ///
    /// Registered before this call: RegisterHuman for roster slot 0 always runs first in
    /// SpawnPlayers, so registry.GetBySlot(0) is already the primary human by the time any CPU is
    /// registered - including the Lockdown defender and a scene-supplied auto player, both of which
    /// this prepares harmlessly; CharacterProfile.Start's own isDefensiveCpuPlayer gate still decides
    /// whether the prepared context is ever applied.
    /// </summary>
    private void PrepareCpuMatchContext(PlayerIdentifier identifier)
    {
        if (identifier.characterProfile == null)
        {
            return;
        }

        PlayerIdentifier primary = registry.GetBySlot(0);
        int primaryLevel = primary != null && primary.characterProfile != null
            ? primary.characterProfile.Level
            : identifier.characterProfile.Level;

        identifier.characterProfile.PrepareCpuMatchContext(primaryLevel, rules);
    }

    /// <summary>
    /// AUD-013: binds the spawned ball's runtime ownership explicitly, immediately after
    /// <c>Instantiate</c> and before Unity calls <c>Start()</c> on any of its components - instead of
    /// hand-syncing a second, ball-side <c>PlayerIdentifier</c> instance to match the owner's.
    ///
    /// Slot 0 is always spawned first (<see cref="SpawnBasketballs"/>) and is always human, so it is
    /// the existing, guaranteed rule <see cref="IBasketballRuntime.IsPrimary"/> derives from - not a
    /// new primary-selection rule.
    /// </summary>
    private void GiveBall(int slotId, GameObject prefab, Vector3 position, bool forCpu)
    {
        PlayerIdentifier owner = registry.GetBySlot(slotId);
        if (owner == null)
        {
            return;
        }

        GameObject ball = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();
        if (runtime == null)
        {
            Debug.LogError($"Basketball prefab '{prefab.name}' has no basketball runtime binding.", ball);
            return;
        }

        GameObject ownerActor = forCpu ? owner.autoPlayer : owner.player;
        runtime.BindOwner(owner.pid, forCpu, slotId == 0, ownerActor, owner.Actor);

        // AUD-010 Phase 2b0: binds this match's already-resolved rules to the CPU implementation
        // directly, immediately after BindOwner - the same seam BasketBallAuto.Start()/Update() now
        // read instead of MatchRuntime.Rules.EnemiesOnly. Not part of IBasketballRuntime: only the
        // concrete CPU type has this dependency today.
        if (runtime is BasketBallAuto autoBall)
        {
            autoBall.BindMatchRules(rules);

            // AUD-010 Phase 2b0: binds the CPU ball's swish/critical-success presentation to the
            // same late-resolving adapter as the human ball below, inverting BasketBallAuto's former
            // direct BehaviorNpcCritical.instance dependency. CPU swishes receive this presentation
            // exactly like human ones, so this is bound unconditionally here rather than only inside
            // the `is BasketBall humanBall` branch the telemetry binding above lives in.
            autoBall.BindCriticalSuccessPresentation(PlayCriticalSuccessPresentation);
        }

        // AUD-010 Phase 2b0: binds this match's already-resolved rules to the ball's own
        // BasketBallState, immediately after BindOwner - runtime.State is already valid at this
        // point, since both BasketBall.BindOwner and BasketBallAuto.BindOwner resolve and bind their
        // BasketBallState synchronously within their own BindOwner before returning here.
        //
        // Code review: on both current implementations this null branch cannot actually fire - a
        // missing BasketBallState component would already have thrown inside runtime.BindOwner above
        // (it dereferences the same GetComponent result unconditionally). Kept as defense-in-depth
        // against a future BindOwner that resolves/binds BasketBallState some other way, mirroring the
        // GameStats null-check just below (which is independently reachable, since it calls
        // GetComponent<GameStats> itself rather than reusing BindOwner's result).
        BasketBallState state = runtime.State;
        if (state == null)
        {
            Debug.LogError($"Basketball prefab '{prefab.name}' has no bound BasketBallState.", ball);
        }
        else
        {
            state.BindMatchRules(rules);
        }

        // AUD-010 Phase 2b0: binds this match's already-resolved rules to the ball's own GameStats,
        // immediately after BindOwner, so match-XP calculation never has to reach for MatchRuntime
        // itself. Every production basketball prefab carries a GameStats component (phase 1b
        // measurement); a prefab that does not is a composition defect, logged here rather than
        // patched over by adding one.
        GameStats stats = ball.GetComponent<GameStats>();
        if (stats == null)
        {
            Debug.LogError($"Basketball prefab '{prefab.name}' has no GameStats component.", ball);
        }
        else
        {
            stats.BindMatchRules(rules);
        }

        // AUD-010 Phase 1c: only the human ball's drop-shadow fallback needs a live ground height -
        // CPU composition (BasketBallAuto) is unaffected. Passing the possibly-null coordinator-level
        // provider through is deliberate: BindGroundHeightProvider's own null-provider guard is what
        // makes an incomplete coordinator fail clearly instead of the ball inventing a fallback.
        if (runtime is BasketBall humanBall)
        {
            humanBall.BindGroundHeightProvider(groundHeightProvider);

            // AUD-010 Phase 2b0: binds this match's already-resolved rules to the human implementation
            // directly, immediately after the ground-height provider - the same seam BasketBall.Start()/
            // Update() now read instead of MatchRuntime.Rules.EnemiesOnly/IsBattleRoyal. Not part of
            // IBasketballRuntime: only the concrete human type has this dependency today.
            humanBall.BindMatchRules(rules);

            // AUD-010 Phase 2b0: binds human shot telemetry to the existing AnaylticsManager.PlayerShoot
            // analytics call, inverting BasketBall's former direct dependency on it - the ball's own
            // Launch() now invokes a bound Action<float> instead of calling AnaylticsManager itself.
            // CPU shots (BasketBallAuto) deliberately receive no telemetry binding, preserving the
            // existing human-only PlayerShoot behavior.
            humanBall.BindShotTelemetry(AnaylticsManager.PlayerShoot);

            // AUD-010 Phase 2b0: binds human swish/critical-success presentation to the same
            // late-resolving adapter as the CPU ball above, inverting BasketBall's former direct
            // BehaviorNpcCritical.instance dependency.
            humanBall.BindCriticalSuccessPresentation(PlayCriticalSuccessPresentation);
        }

        if (forCpu)
        {
            owner.setAutoBasketball(ball);
        }
        else
        {
            owner.setBasketball(ball);
        }

        BindShotMeterRuntime(ownerActor, runtime);
    }

    /// <summary>
    /// AUD-010 Phase 2b0: the swish/critical-success presentation callback bound to both
    /// <see cref="BasketBall"/> and <see cref="BasketBallAuto"/> above, replacing their former direct
    /// <c>BehaviorNpcCritical.instance.playAnimationCriticalSuccesful()</c> calls.
    ///
    /// A named method rather than a captured local: at basketball-spawn time the cheerleader (whose
    /// <c>Start()</c> assigns <see cref="BehaviorNpcCritical.instance"/>) may not exist yet -
    /// <see cref="SpawnCoordinator.SpawnBasketballs"/> runs before the cheerleader is spawned - so the
    /// singleton must be resolved here, at the point a swish actually invokes this callback, never
    /// captured at composition time.
    /// </summary>
    private static void PlayCriticalSuccessPresentation()
    {
        if (BehaviorNpcCritical.instance != null)
        {
            BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
        }
    }

    /// <summary>
    /// AUD-010 Phase 1c: associates the just-bound basketball runtime with the owning participant's
    /// own ShotMeter(s), immediately after IBasketballRuntime.BindOwner and the owner's
    /// basketball/autoBasketball reference are set - never before, since ShotMeter.BindBasketballRuntime
    /// requires a fully bound runtime to validate against. Most player prefabs carry exactly one
    /// ShotMeter; a participant with none is unaffected.
    /// </summary>
    private static void BindShotMeterRuntime(GameObject ownerActor, IBasketballRuntime runtime)
    {
        foreach (ShotMeter meter in ownerActor.GetComponentsInChildren<ShotMeter>(true))
        {
            meter.BindBasketballRuntime(runtime);
        }
    }
}
