using System;
using System.Collections.Generic;
using System.Linq;
using Level5.Core.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

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
public class GameLevelManager : MonoBehaviour
{
    public bool isMultiplePlayersTotalPoints;
    public int currentHighScoreTotalPoints;
    public int numPlayers;

    private readonly PlayerRegistry registry = new PlayerRegistry();

    [SerializeField]
    private GameObject _autoPlayer;
    [SerializeField]
    private PlayerController _playerController1;
    [SerializeField]
    private PlayerController _playerController2;
    [SerializeField]
    private AutoPlayerController _autoPlayerController;
    private CharacterProfile _characterProfile;
    private PlayerHealth _playerHealth;
    [SerializeField]
    private PlayerAttackQueue _playerAttackQueue;
    private GameStats _gameStats;

    private SpawnCoordinator.SpawnLocations _spawnLocations;
    private SpawnCoordinator _spawnCoordinator;
    private ResolvedMatchRules _rules;
    private PlayerRoster _roster;

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
        numPlayers = Mathf.Max(1, _roster.Count);
        MatchSession.EnsureCurrentMatch();

        registry.Clear();

        // The scene's composition root. Created here while no gameplay scene carries the component
        // yet, so new systems have somewhere to ask for the configuration and the registry instead
        // of adding another singleton. A scene that gains one of its own is used as-is.
        LevelRuntimeContext context = FindAnyObjectByType<LevelRuntimeContext>()
            ?? gameObject.AddComponent<LevelRuntimeContext>();
        context.AdoptPlayerRegistry(registry);

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

        _spawnCoordinator = new SpawnCoordinator(_spawnLocations, registry, _rules, _roster, MatchRuntime.ModeId);

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

        _spawnCoordinator.SpawnCheerleader(MatchRuntime.Cheerleader.ObjectName, terrainHeight);

        ArenaBootstrap.HideDuplicateCharacterActors(MatchRuntime.PrimaryCharacterObjectName, _rules.TrafficEnabled);
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
            _characterProfile = player1.GetComponent<CharacterProfile>();
            _playerAttackQueue = player1.GetComponent<PlayerAttackQueue>();
            _playerHealth = player1.GetComponentInChildren<PlayerHealth>();

            terrainHeight = player1.transform.position.y;
        }

        if (GameObject.FindWithTag("autoPlayer") != null)
        {
            _autoPlayer = GameObject.FindWithTag("autoPlayer");

            // CPU-6: the `_autoPlayerController.isCPU = true` that used to follow is gone with the
            // field. It was the only writer of a value nothing read - PlayerIdentifier.isCpu on
            // this same GameObject already says the same thing, and is what callers use.
            _autoPlayerController = _autoPlayer.GetComponent<PlayerIdentifier>().isDefensivePlayer ? null : _autoPlayer.GetComponent<AutoPlayerController>();
        }

        ArenaBootstrap.Apply(_rules, MatchRuntime.HasConfiguration);
        _basketballRimVector = ArenaBootstrap.FindRimVector();
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
            .OrderByDescending(x => x.gameStats.TotalPoints)
            .ToList();
        if (isMultiplePlayersTotalPoints)
        {
            currentHighScoreTotalPoints = sorted.Count > 0 ? sorted[0].gameStats.TotalPoints : 0;
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
    public PlayerController PlayerController2 => _playerController2;
    public Animator Anim { get; private set; }
    public bool GameOver { get; set; }
    public PlayerControls Controls { get => controls; set => controls = value; }
    public FloatingJoystick Joystick { get => joystick; }
    public Vector3 BasketballRimVector { get => _basketballRimVector; set => _basketballRimVector = value; }
    public PlayerAttackQueue PlayerAttackQueue { get => _playerAttackQueue; set => _playerAttackQueue = value; }
    public PlayerHealth PlayerHealth { get => _playerHealth; set => _playerHealth = value; }
    public GameObject AutoPlayer { get => _autoPlayer; set => _autoPlayer = value; }
    public GameStats GameStats { get => _gameStats; set => _gameStats = value; }
    public float TerrainHeight { get => terrainHeight; }
    public GameObject PlayerSpawnLocation => _spawnLocations != null ? _spawnLocations.Player1 : null;
}
