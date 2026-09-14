using System;
using System.Collections.Generic;
using System.Linq;
using Level5.Core;
using Level5.Core.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The gameplay scene's manager, now mostly a facade.
///
/// Spawning moved to <see cref="SpawnCoordinator"/>, the participant list to
/// <see cref="PlayerRegistry"/>, and the mode-driven scene setup to <see cref="ArenaBootstrap"/>.
/// What is left here is the wiring those need plus the accessors the rest of the game already
/// calls (<c>Player1</c>, <c>players</c>, <c>Controls</c>, ...), kept so nothing else has to change
/// at the same time.
///
/// It no longer decides any match rule. It used to: "if this mode has no basketball, switch enemies
/// on" ran here, at scene start, after the menu had already settled the question. That resolution
/// belongs to the configuration builder now, and this reads the answer.
/// </summary>
public class GameLevelManager : MonoBehaviour, IGroundHeightProvider, IPlayerMatchRuntime
{
    public bool isMultiplePlayersTotalPoints;
    public int currentHighScoreTotalPoints;
    public int numPlayers;

    private readonly PlayerRegistry registry = new PlayerRegistry();

    [SerializeField]
    private GameObject _autoPlayer;
    [SerializeField]
    private PlayerController _playerController1;
    private PlayerHealth _playerHealth;
    [SerializeField]
    private PlayerAttackQueue _playerAttackQueue;

    private SpawnCoordinator.SpawnLocations _spawnLocations;
    private SpawnCoordinator _spawnCoordinator;
    private ResolvedMatchRules _rules;
    private PlayerRoster _roster;
    private GameModeId _modeId;

    private Vector3 _basketballRimVector;

    private PlayerControls controls;
    FloatingJoystick joystick;

    float terrainHeight;

    public static GameLevelManager instance;
    private bool _locked;

    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
        PlayerControlsProvider.EnableOther();
    }
    private void OnDisable()
    {
        PlayerControlsProvider.DisableOther();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        controls = PlayerControlsProvider.Controls;

        // The rules and the roster for this match. Read once, never written: a scene does not get
        // to change what it was launched as.
        _rules = MatchRuntime.Rules;
        _roster = MatchRuntime.Roster;
        _modeId = MatchRuntime.ModeId;
        numPlayers = Mathf.Max(1, _roster.Count);
        MatchSession.EnsureCurrentMatch();

        registry.Clear();

        // The scene's composition root. Created here while no gameplay scene carries the component
        // yet, so new systems have somewhere to ask for the configuration and the registry instead
        // of adding another singleton. A scene that gains one of its own is used as-is.
        LevelRuntimeContext context = FindAnyObjectByType<LevelRuntimeContext>()
            ?? gameObject.AddComponent<LevelRuntimeContext>();
        context.AdoptPlayerRegistry(registry);

        BindBasketballShotMadeContext();

        _spawnLocations = SpawnCoordinator.SpawnLocations.FindInScene();
        if (!_spawnLocations.Validate(_roster, _rules))
        {
            enabled = false;
            return;
        }

        terrainHeight = setTerrainHeight();

        //ui touch controls
        if (GameObject.FindGameObjectWithTag("joystick") != null)
        {
            joystick = GameObject.FindGameObjectWithTag("joystick").GetComponentInChildren<FloatingJoystick>();
        }

        _spawnCoordinator = new SpawnCoordinator(
            _spawnLocations,
            registry,
            _rules,
            _roster,
            _modeId,
            this,
            TryResolveCampaignCpuPrefab,
            SpawnProjectileForAnimationEvents,
            HasAutoPlayerForAnimationEvents);

        try
        {
            _spawnCoordinator.SpawnPlayers();
            _spawnCoordinator.SpawnBasketballs();
        }
        catch (Exception exception)
        {
            Debug.LogError($"GameLevelManager could not initialize the level: {exception.Message}");
            enabled = false;
            return;
        }

        _spawnCoordinator.BindHumanLegacyTouchMovement(ReadLegacyTouchMovement);
        _spawnCoordinator.BindHumanDamageReactionCamera(ReadPlayerDamageReactionCamera);
        _spawnCoordinator.BindHumanIdleSniperRuntime(ReadPlayerIdleSniperRuntime);
        _spawnCoordinator.BindHumanMatchRuntime(this);
        _spawnCoordinator.BindCpuMatchRuntime(this);

        _spawnCoordinator.SpawnCheerleader(MatchRuntime.Cheerleader.ObjectName, terrainHeight);

        ArenaBootstrap.HideDuplicateCharacterActors(MatchRuntime.PrimaryCharacterObjectName, _rules.TrafficEnabled);

        // Code review, 2026-09-12: this used to be called from Start(), which raced PlayerHealthBar's
        // own Start() - Unity does not order Start() across independent components, and this project
        // defines no explicit Script Execution Order. If PlayerHealthBar.Start() happened to run first,
        // it saw contextBound == false, deactivated its own GameObject, and this Find (active-only)
        // would then find nothing - leaving the HUD silently, permanently disabled for the match. Called
        // from Awake() instead, which every Start() in the scene is guaranteed to follow. Everything it
        // reads is already final by the end of Awake(): PlayerHealth.Awake() sets Health/Block/Special,
        // and a human's CharacterProfile.playerDisplayName is either the prefab's serialized default or
        // set synchronously above by SpawnCoordinator.RegisterHuman - never by CharacterProfile.Start().
        BindPlayerHealthBarContext(registry.GetBySlot(0));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 26: the legacy mobile joystick's current axes, as a value rather than as
    /// the component. <c>PlayerInputReader</c> used to read <c>GameLevelManager.instance.Joystick</c>
    /// itself; it now receives this method (via <see cref="SpawnCoordinator.BindHumanLegacyTouchMovement"/>
    /// -&gt; <c>PlayerController.BindLegacyTouchMovementReader</c>) so <c>Level5.Input</c> never names
    /// this class or <c>FloatingJoystick</c>. Reads <see cref="joystick"/> on each call, so it stays as
    /// synchronous as the singleton read it replaces and copes with the field being resolved (or not
    /// found at all, on a scene with no touch controls) independently of when this was bound.
    /// </summary>
    private Vector2 ReadLegacyTouchMovement()
    {
        return joystick != null
            ? new Vector2(joystick.Horizontal, joystick.Vertical)
            : Vector2.zero;
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 32: the shrink reaction's camera, as a value rather than as
    /// <c>CameraManager</c> itself. <c>PlayerDamageReactions</c> used to read
    /// <c>CameraManager.instance.Cameras[0]</c> directly; it now receives this method (via
    /// <see cref="SpawnCoordinator.BindHumanDamageReactionCamera"/> -&gt;
    /// <c>PlayerController.BindDamageReactionCameraReader</c>) so <c>Level5.Player</c> never names
    /// <c>CameraManager</c>. Reads <see cref="CameraManager.instance"/> on each call, so it stays as
    /// live as the direct read it replaces - not moved into <c>PlayerController</c>, which would only
    /// trade one <c>Assembly-CSharp</c> dependency for another.
    ///
    /// Preserves the exact former camera-selection rule: <c>Cameras[0]</c>. Null-safe at every step,
    /// same as the read this replaces - an absent camera manager, an empty camera array, or a missing
    /// index-0 camera component all resolve to <c>null</c> rather than throwing, and the shrink
    /// reaction already treats a null camera as "skip the FOV change, reaction still proceeds."
    /// </summary>
    private Camera ReadPlayerDamageReactionCamera()
    {
        if (CameraManager.instance == null
            || CameraManager.instance.Cameras == null
            || CameraManager.instance.Cameras.Length == 0
            || CameraManager.instance.Cameras[0] == null)
        {
            return null;
        }

        return CameraManager.instance.Cameras[0].GetComponent<Camera>();
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 33: the idle-sniper runtime, as a live lookup rather than
    /// <c>PlayerController</c> reading <c>SniperManager.instance</c> itself.
    /// <c>PlayerController.checkIdleTimeForSniper</c> used to read that static directly; it now
    /// receives this method (via <see cref="SpawnCoordinator.BindHumanIdleSniperRuntime"/> -&gt;
    /// <c>PlayerController.BindIdleSniperRuntimeReader</c>) so <c>Level5.Player</c> never names
    /// <c>SniperManager</c>. Deliberately resolves <c>SniperManager.instance</c> fresh on every call
    /// rather than capturing it here - no sniper runtime needs to exist yet when this is bound, and
    /// the static may be assigned (or reassigned, or cleared on destroy) well after this scene's
    /// spawn pass.
    ///
    /// The null check is explicit and against the concrete <c>SniperManager</c> type, mirroring
    /// <see cref="ReadPlayerDamageReactionCamera"/> just above - not the bare
    /// <c>return SniperManager.instance;</c> an implicit interface conversion would allow. A destroyed
    /// Unity object compares equal to <c>null</c> only through <c>UnityEngine.Object</c>'s overloaded
    /// <c>==</c>, which is not selected once the value's static type is the
    /// <see cref="IPlayerIdleSniperRuntime"/> interface - so the check has to happen here, against the
    /// concrete type, before the conversion, rather than relying on <c>PlayerController</c>'s later
    /// <c>sniper == null</c> to catch it.
    /// </summary>
    private IPlayerIdleSniperRuntime ReadPlayerIdleSniperRuntime()
    {
        SniperManager instance = SniperManager.instance;
        return instance != null ? instance : null;
    }

    /// <summary>
    /// AUD-010 Phase 2b0: binds this scene's <see cref="BasketBallShotMade"/> - the made-shot trigger
    /// authored on the basketball_goal prefab/hierarchy, exactly one per gameplay scene - to the
    /// already-resolved <see cref="_rules"/>/<see cref="_modeId"/>, so it no longer has to read
    /// <c>MatchRuntime</c> itself. Not spawned by <see cref="SpawnCoordinator"/> (it is scene-authored
    /// on the hoop, not per-participant), so <c>FindAnyObjectByType</c> is the narrowest route that
    /// reliably reaches the one production instance - the same pattern already used for
    /// <see cref="LevelRuntimeContext"/> just above. Every current gameplay scene has exactly one, so a
    /// missing hoop is logged (not thrown/disabled) rather than assumed silently intentional - the same
    /// "should always be present, log if it isn't" shape <c>SpawnCoordinator.GiveBall</c> already uses
    /// for a basketball's <c>GameStats</c>/<c>BasketBallState</c>.
    /// </summary>
    private void BindBasketballShotMadeContext()
    {
        BasketBallShotMade shotMade = FindAnyObjectByType<BasketBallShotMade>();
        if (shotMade == null)
        {
            Debug.LogWarning("GameLevelManager could not find a BasketBallShotMade to bind match context to - any made shot in this scene will fail closed with no score.");
            return;
        }

        shotMade.BindMatchContext(_rules, _modeId);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 54: the composition adapter <see cref="SpawnCoordinator"/> now calls
    /// instead of reaching directly for legacy campaign selection itself. This class already reads
    /// <c>GameOptions</c> (it is an allowlisted consumer) and already constructs the coordinator, so it
    /// is the narrowest existing owner for this one lookup - not a new <c>GameOptions</c> consumer.
    ///
    /// Preserves the exact prior three-state semantics <c>SpawnCoordinator.ResolveParticipantPrefab</c>
    /// used to implement inline: no list, a negative index, or an out-of-range index all report "no
    /// campaign override available" (<c>false</c>) so the caller falls back to the normal Resources
    /// lookup; a valid entry reports <c>true</c> with that entry's authored <c>CpuPlayer</c>, which may
    /// itself legitimately be null - the caller must not treat that as "unavailable" and must not fall
    /// back. A malformed list entry (<c>levels[index] == null</c>) is left to throw exactly as the
    /// former inline read would have; no new guard is added for it here.
    /// </summary>
    private static bool TryResolveCampaignCpuPrefab(out GameObject prefab)
    {
        prefab = null;

        List<LevelSelected> levels = GameOptions.levelsList;
        int levelIndex = GameOptions.levelSelectedIndex;
        if (levels == null || levelIndex < 0 || levelIndex >= levels.Count)
        {
            return false;
        }

        prefab = levels[levelIndex].CpuPlayer;
        return true;
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 55: the composition adapter <see cref="SpawnCoordinator"/> now forwards to
    /// every spawned <see cref="PlayerAnimationEvents"/> instead of calling <c>ProjectilePool.Spawn</c>
    /// itself. <c>ProjectilePool</c> lives in loose <c>Assets/Scripts/projectile/</c> (<c>Assembly-CSharp</c>,
    /// not <c>Level5.Pooling</c>), so this is the narrowest existing owner for the call - not a new
    /// <c>ProjectilePool</c> consumer, this class already sits in the same assembly. Preserves the exact
    /// prefab/position/rotation arguments and pooled-spawn return value the former inline call had; no
    /// pooling behavior is touched.
    /// </summary>
    private static GameObject SpawnProjectileForAnimationEvents(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return ProjectilePool.Spawn(prefab, position, rotation);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 55: the composition adapter <see cref="SpawnCoordinator"/> now forwards to
    /// every spawned <see cref="PlayerAnimationEvents"/> instead of resolving
    /// <c>GameLevelManager.instance.AutoPlayer</c> itself. Reads the static <see cref="instance"/> field
    /// live on every call, exactly as the former direct <c>GameLevelManager.instance.AutoPlayer</c> read
    /// did - deliberately not a captured reference to <c>this</c>, so the check still observes the
    /// current manager if the static is ever replaced or cleared, matching pre-slice behavior exactly.
    /// </summary>
    private static bool HasAutoPlayerForAnimationEvents()
    {
        return instance != null && instance.AutoPlayer != null;
    }

    private float setTerrainHeight()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case Constants.SCENE_NAME_level_15_cocaine_island:
                return terrainHeight = 145;
            case Constants.SCENE_NAME_level_20_jacksonville:
                return terrainHeight = 200;
            default:
                return terrainHeight = 0;
        }
    }

    private void Start()
    {
        // return to this if n
        GameOptions.previousSceneName = Constants.SCENE_NAME_level_00_loading;
        if (numPlayers > 1)
        {
            isMultiplePlayersTotalPoints = true;
        }
        // analytic event
        if (!String.IsNullOrEmpty(GameOptions.levelSelectedName))
        {
            AnaylticsManager.LevelLoaded(GameOptions.levelSelectedName);
        }

        _locked = false;
        //set up player/basketball read only references for use in other classes
        PlayerIdentifier player1 = registry.GetBySlot(0);
        if (player1 != null && GameObject.FindWithTag("Player") != null)
        {
            _playerController1 = player1.GetComponent<PlayerController>();
            _playerAttackQueue = player1.GetComponent<PlayerAttackQueue>();
            _playerHealth = player1.GetComponentInChildren<PlayerHealth>();

            terrainHeight = player1.transform.position.y;
        }

        if (GameObject.FindWithTag("autoPlayer") != null)
        {
            _autoPlayer = GameObject.FindWithTag("autoPlayer");
        }

        ArenaBootstrap.Apply(_rules, MatchRuntime.HasConfiguration);
        _basketballRimVector = ArenaBootstrap.FindRimVector();

        // AUD-012 Phase 2b Slice 21: forwards the now-final rim vector and this manager's own live
        // IGroundHeightProvider to every registered human's PlayerController, replacing that
        // controller's former direct GameLevelManager.instance reads. Must run after both lines above:
        // the rim is not resolved until FindRimVector() returns.
        //
        // Null-conditional to match the null-safety the rest of Start() already has: the duplicate-
        // manager path (Awake's `instance != this` guard) returns before _spawnCoordinator is assigned,
        // and every other statement here survives that - ArenaBootstrap.Apply early-returns on the null
        // _rules it would see, and FindRimVector is a null-safe scene search. On the normal path the
        // coordinator is always assigned in Awake before spawning, so this can only skip where no level
        // was built at all.
        _spawnCoordinator?.BindHumanArenaContext(_basketballRimVector, this);
        _spawnCoordinator?.BindCpuArenaContext(_basketballRimVector, this);
    }

    /// <summary>
    /// Forwards the primary human's tracked <see cref="PlayerHealth"/>, character display name,
    /// resolved match rules and a live damage-display-text reader to the scene's
    /// <see cref="PlayerHealthBar"/> HUD (a scene-authored UI singleton, not a per-participant
    /// component, so it is not spawned or bound through <see cref="SpawnCoordinator"/>), replacing
    /// that HUD's former direct <c>GameLevelManager.instance</c>/<c>MatchRuntime</c> reads. A scene
    /// with no HUD instance, or no primary human, is left alone exactly as before (the HUD simply
    /// never activates).
    ///
    /// Code review, 2026-09-12: called from <see cref="Awake"/>, not <see cref="Start"/> - this HUD
    /// reads the bound context synchronously inside its own <c>Start()</c> to decide whether to
    /// activate itself, and Unity does not order <c>Start()</c> across independent components. Calling
    /// this from <c>Start()</c> raced <see cref="PlayerHealthBar.Start"/>: if the HUD's own
    /// <c>Start()</c> happened to run first, it saw an unbound context, deactivated its own
    /// GameObject, and this method's <c>FindAnyObjectByType</c> (active-only) then found nothing -
    /// leaving the HUD silently and permanently disabled for the match. <c>Awake()</c> is guaranteed
    /// to precede every <c>Start()</c> in the scene, closing that race. <paramref name="player1"/> is
    /// resolved from <see cref="registry"/> directly (equivalent to <see cref="Player1"/>) rather than
    /// the <c>Start()</c>-scoped local of the same name.
    ///
    /// The damage-display Text is bound as a live <see cref="Func{Text}"/>, not a captured reference:
    /// <c>PlayerController.DamageDisplayValueText</c> is only populated inside that controller's own
    /// <c>Start()</c> - resolving it fresh, at the point <see cref="PlayerHealthBar"/> actually
    /// displays a message (well after every <c>Start()</c> has run), matches the timing the original
    /// direct read had.
    /// </summary>
    private void BindPlayerHealthBarContext(PlayerIdentifier player1)
    {
        PlayerHealthBar healthBar = FindAnyObjectByType<PlayerHealthBar>();
        if (healthBar == null || player1 == null)
        {
            return;
        }

        PlayerHealth health = player1.GetComponentInChildren<PlayerHealth>();
        CharacterProfile profile = player1.GetComponent<CharacterProfile>();
        healthBar.BindPrimaryHumanContext(
            _rules,
            health,
            profile != null ? profile.PlayerDisplayName : null,
            ReadPrimaryHumanDamageDisplayText);
    }

    private Text ReadPrimaryHumanDamageDisplayText()
    {
        return _playerController1 != null ? _playerController1.DamageDisplayValueText : null;
    }

    private void Update()
    {
        if (controls == null || Pause.instance == null)
        {
            return;
        }

        //turn on : toggle run
        if (Controls.Other.change.enabled
            && Controls.Other.toggle_run_keyboard.triggered
            && !_locked
            && !Pause.instance.Paused)
        {
            _locked = true;
            if (PlayerController1 != null)
            {
                PlayerController1.ToggleRun();
            }
            _locked = false;
        }

        //turn off stats
        if (Controls.Other.change.enabled
            && Controls.Other.toggle_stats_keyboard.triggered
            && !_locked
            && !Pause.instance.Paused)
        {
            _locked = true;
            if (BasketBall.instance != null)
            {
                BasketBall.instance.toggleUiStats();
            }
            _locked = false;
        }
    }

    public List<PlayerIdentifier> getSortedGameStatsList()
    {
        List<PlayerIdentifier> sorted = players
            .Where(x => x != null && x.gameStats != null)
            .OrderByDescending(x => x.gameStats.Stats.TotalPoints)
            .ToList();
        if (isMultiplePlayersTotalPoints)
        {
            currentHighScoreTotalPoints = sorted.Count > 0 ? sorted[0].gameStats.Stats.TotalPoints : 0;
        }
        return sorted;
    }

    // ============================  facade  ==============================
    // The accessors the rest of the game already calls. Player1..Player4 are views onto the one
    // registry list rather than four separate fields, so they cannot drift out of step with it.

    /// <summary>The participants in the scene. Owned by <see cref="PlayerRegistry"/>.</summary>
    public List<PlayerIdentifier> players => registry.MutableParticipants;

    public PlayerRegistry Registry => registry;

    /// <summary>The resolved rules this scene is being played under.</summary>
    public ResolvedMatchRules Rules => _rules;

    /// <summary>Who should be playing, as validated at launch.</summary>
    public PlayerRoster Roster => _roster;

    public PlayerIdentifier Player1 => registry.GetBySlot(0);
    public PlayerIdentifier Player2 => registry.GetBySlot(1);
    public PlayerIdentifier Player3 => registry.GetBySlot(2);
    public PlayerIdentifier Player4 => registry.GetBySlot(3);
    public PlayerController PlayerController1 => _playerController1;
    public Animator Anim { get; private set; }
    public bool GameOver { get; set; }
    public PlayerControls Controls { get => controls; set => controls = value; }
    public FloatingJoystick Joystick { get => joystick; }
    public Vector3 BasketballRimVector { get => _basketballRimVector; set => _basketballRimVector = value; }
    public PlayerAttackQueue PlayerAttackQueue { get => _playerAttackQueue; set => _playerAttackQueue = value; }
    public PlayerHealth PlayerHealth { get => _playerHealth; set => _playerHealth = value; }
    public GameObject AutoPlayer { get => _autoPlayer; set => _autoPlayer = value; }
    public float TerrainHeight { get => terrainHeight; }
    public GameObject PlayerSpawnLocation => _spawnLocations != null ? _spawnLocations.Player1 : null;

    // ======================= IGroundHeightProvider (AUD-010 Phase 1c) =======================

    /// <summary>
    /// Exposes the existing <see cref="TerrainHeight"/> value through the basketball-domain contract,
    /// explicitly so this does not become a second public surface for the same value. Live: reads
    /// <c>terrainHeight</c> on every access, the same field <see cref="Start"/> updates from the
    /// primary participant's actual Y after spawning.
    /// </summary>
    float IGroundHeightProvider.GroundHeight => terrainHeight;

    // ======================= IPlayerMatchRuntime (AUD-012 Phase 2b Slice 37) =======================

    /// <summary>
    /// Explicit, live forwarding boundary over <c>MatchRuntime</c> for <see cref="PlayerController"/>
    /// (bound through <see cref="SpawnCoordinator.BindHumanMatchRuntime"/>), so that controller no
    /// longer names <c>MatchRuntime</c> directly. Deliberately forwards to the static on every call
    /// rather than answering from <see cref="_rules"/>/<see cref="_roster"/>: this class's own snapshot
    /// fields serve its own facade responsibilities, but <c>PlayerController</c>'s former direct reads
    /// had different point-of-use semantics (a directly entered/unconfigured scene's legacy-global
    /// fallback, re-evaluated live) that this boundary must preserve exactly. <c>MatchRuntime</c> stays
    /// the only owner of that compatibility translation.
    /// </summary>
    ResolvedMatchRules IPlayerMatchRuntime.Rules => MatchRuntime.Rules;

    bool IPlayerMatchRuntime.CustomCamera => MatchRuntime.CustomCamera;

    bool IPlayerMatchRuntime.LevelHasSevenPointers => MatchRuntime.LevelHasSevenPointers;

    int IPlayerMatchRuntime.LocalInputSlotFor(int playerId) => MatchRuntime.LocalInputSlotFor(playerId);
}
