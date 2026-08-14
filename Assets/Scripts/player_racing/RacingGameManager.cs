using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingGameManager : MonoBehaviour
{
    // this is to keep a reference to player in game manager 
    // that can be retrieved across all scripts
    [SerializeField]
    private GameObject _player;
    private GameObject _autoPlayer;
    [SerializeField]
    private RacingVehicleController _playerController;
    [SerializeField]
    private RacingVehicleProfile _characterProfile;
    private GameStats _gameStats;

    //spawn locations
    private GameObject _playerSpawnLocation;

    [SerializeField]
    private GameObject cinderBlockPrefab;

    PlayerControls controls;
    FloatingJoystick joystick;

    float terrainHeight;

    public static RacingGameManager instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    private bool _locked;

    bool isAutoPlayer;

    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
        PlayerControlsProvider.EnableGameplayMaps();
        PlayerControlsProvider.EnableOtherMaps();
        //controls.PlayerTouch.Enable();
    }
    private void OnDisable()
    {
        PlayerControlsProvider.DisableOtherMaps();
        PlayerControlsProvider.DisableGameplayMaps();
        //controls.PlayerTouch.Disable();
    }

    private void Awake()
    {
        instance = this;
        // mapped controls
        controls = PlayerControlsProvider.Controls;

        //ui touch controls
        if (GameObject.FindGameObjectWithTag("joystick") != null)
        {
            joystick = GameObject.FindGameObjectWithTag("joystick").GetComponentInChildren<FloatingJoystick>();
        }
        _gameStats = GetComponent<GameStats>();

        // spawn locations
        _playerSpawnLocation = GameObject.Find("player_spawn_location");
    }

    private void Start()
    {
        // return to this if n
        GameOptions.previousSceneName = Constants.SCENE_NAME_level_00_loading;

        // analytic event
        if (!String.IsNullOrEmpty(GameOptions.levelSelectedName))
        {
            AnaylticsManager.LevelLoaded(GameOptions.levelSelectedName);
        }

        _locked = false;
        //set up player/basketball read only references for use in other classes
        if (GameObject.FindWithTag("Player") != null)
        {
            _player = GameObject.FindWithTag("Player");
            _playerController = _player.GetComponent<RacingVehicleController>();
            _characterProfile = _player.GetComponent<RacingVehicleProfile>();
            Anim = Player.GetComponentInChildren<Animator>();

            terrainHeight = Player.transform.position.y;
        }
        else
        {
            if(SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_15_cocaine_island)
            {
                terrainHeight = 145;
            }
            else
            {
                terrainHeight = 0;
            }
        }
        // if shot clock is present, set shot clock camera to Camera.Main because it uses worldspace
        // instead of an overlay. this is for a slight performance increase
        if (GameObject.Find("shot_clock") != null)
        {
            GameObject.Find("shot_clock").GetComponent<Canvas>().worldCamera = Camera.main;
        }
    }


    private void Update()
    {
        //turn on : toggle run
        if (Controls.Other.change.enabled
            && Controls.Other.toggle_run_keyboard.triggered
            && !_locked
            && !Pause.instance.Paused)
        {
            _locked = true;
            PlayerController.ToggleRun();
            _locked = false;
        }

        //turn off stats
        if (Controls.Other.change.enabled
            && Controls.Other.toggle_stats_keyboard.triggered
            && !_locked
            && !Pause.instance.Paused)
        {
            _locked = true;
            BasketBall.instance.toggleUiStats();
            _locked = false;
        }
    }

    public GameObject Player => _player;

    public RacingVehicleController PlayerController => _playerController;
    public Animator Anim { get; private set; }
    public bool GameOver { get; set; }
    public PlayerControls Controls { get => controls; set => controls = value; }
    public FloatingJoystick Joystick { get => joystick; }
    public RacingVehicleProfile CharacterProfile { get => _characterProfile; set => _characterProfile = value; }
    public bool IsAutoPlayer { get => isAutoPlayer; set => isAutoPlayer = value; }
    public GameObject AutoPlayer { get => _autoPlayer; set => _autoPlayer = value; }
    public GameStats GameStats { get => _gameStats; set => _gameStats = value; }
    public float TerrainHeight { get => terrainHeight;}
    public GameObject CinderBlockPrefab { get => cinderBlockPrefab;  }
}
