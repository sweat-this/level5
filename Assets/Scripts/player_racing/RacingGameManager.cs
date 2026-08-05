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
    //private AutoPlayerController _autoPlayerController;
    [SerializeField]
    private RacingVehicleProfile _characterProfile;
    //private PlayerHealth _playerHealth;
    //[SerializeField]
    //private PlayerAttackQueue _playerAttackQueue;
    //[SerializeField]
    private GameStats _gameStats;

    //spawn locations
    private GameObject _basketballSpawnLocation;
    private GameObject _playerSpawnLocation;

    [SerializeField]
    private GameObject cinderBlockPrefab;

    PlayerControls controls;
    FloatingJoystick joystick;

    float terrainHeight;

    const string basketBallPrefabPath = "Prefabs/basketball/basketball";
    const string basketBallPrefabAutoPath = "Prefabs/basketball/basketballAuto";

    public static RacingGameManager instance;
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
        //_basketballSpawnLocation = GameObject.Find("ball_spawn_location");
        //_cheerleaderSpawnLocation = GameObject.Find("cheerleader_spawn_location");

        //// if player doesnt exists, spawn default Player
        //checkPlayerPrefabExists();
        //// if basketball doesnt exists
        //checkBasketballPrefabExists();
        //// cheerleader doesnt exists
        //checkCheerleaderPrefabExists();
        ////// check if player is npc in scene
        //checkPlayerIsNpcInScene();
        //// get necessary references to objects
        //Basketball = GameObject.FindGameObjectWithTag("basketball").GetComponent<BasketBall>();
        //_basketballRimVector = GameObject.Find("rim").transform.position;
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

        //disable lighting if necessary
        // something like if gameoptions.lightingenabled

        //Light[] lights = GameObject.FindObjectsOfType<Light>();
        //if(lights.Length > 0)
        //{
        //    foreach (Light light in lights)
        //    {
        //        //Debug.Log("disable : " + light.name);
        //        light.enabled = false;
        //    }
        //    RenderSettings.ambientLight = Color.white;
        //}

        _locked = false;
        //set up player/basketball read only references for use in other classes
        if (GameObject.FindWithTag("Player") != null)
        {
            _player = GameObject.FindWithTag("Player");
            _playerController = _player.GetComponent<RacingVehicleController>();
            _characterProfile = _player.GetComponent<RacingVehicleProfile>();
            //_playerAttackQueue = _player.GetComponent<PlayerAttackQueue>();
            //_playerHealth = _player.GetComponentInChildren<PlayerHealth>();
            Anim = Player.GetComponentInChildren<Animator>();

            //terrainHeight = Terrain.activeTerrain.SampleHeight(transform.position);
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
        //if (GameObject.FindWithTag("autoPlayer") != null)
        //{
        //    Debug.Log("auto player");
        //    _autoPlayer = GameObject.FindWithTag("autoPlayer");
        //    _autoPlayerController = _autoPlayer.GetComponent<AutoPlayerController>();
        //    isCPUplayer = true;
        //}
        //else
        //{
        //    isCPUplayer = false;
        //}

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

    //private void checkPlayerIsNpcInScene()
    //{
    //    //Debug.Log("checkPlayerIsNpcInScene() : traffic : "+ GameOptions.trafficEnabled);
    //    // check if player is 'a vehicle' ex. Sam on skateboard, Rad Tony on horse
    //    if (GameOptions.trafficEnabled)
    //    {
    //        _vehicleObjects = GameObject.FindGameObjectsWithTag("vehicle");

    //        // check if player is vehicle in scene
    //        foreach (var veh in _vehicleObjects)
    //        {
    //            //Debug.Log("veh  : " + veh.name);
    //            //Debug.Log("GameOptions.playerObjectName : " + GameOptions.playerObjectName);
    //            if (!string.IsNullOrEmpty(GameOptions.characterObjectName) && veh.name.Contains(GameOptions.characterObjectName))
    //            {
    //                //Debug.Log("disable veh  : " + veh.name);
    //                veh.SetActive(false);
    //            }
    //        }
    //    }
    //    // check if player is an npc, ex. ??? on slab
    //    _npcObjects = GameObject.FindGameObjectsWithTag("auto_npc");
    //    foreach (var npc in _npcObjects)
    //    {
    //        if (!string.IsNullOrEmpty(GameOptions.characterObjectName) && npc.name.Contains(GameOptions.characterObjectName))
    //        {
    //            //Debug.Log("disable npc  : " + npc.name);
    //            npc.SetActive(false);
    //        }
    //    }
    //}

    //private void checkCheerleaderPrefabExists()
    //{
    //    if (GameObject.FindWithTag("cheerleader") == null
    //        && GameOptions.cheerleaderObjectName != null)
    //    {
    //        string cheerleaderPrefabPath = "Prefabs/characters/cheerleaders/cheerleader_" + GameOptions.cheerleaderObjectName;
    //        _cheerleaderClone = Resources.Load(cheerleaderPrefabPath) as GameObject;

    //        if (_cheerleaderClone != null)
    //        {
    //            Instantiate(_cheerleaderClone, _cheerleaderSpawnLocation.transform.position, Quaternion.identity);
    //        }
    //    }
    //}

    //private void checkBasketballPrefabExists()
    //{
    //    if (GameObject.FindWithTag("basketball") == null && !isCPUplayer)
    //    {
    //        _basketballPrefab = Resources.Load(basketBallPrefabPath) as GameObject;
    //    }
    //    if (GameObject.FindWithTag("basketball") == null && isCPUplayer)
    //    {
    //        _basketballPrefab = Resources.Load(basketBallPrefabAutoPath) as GameObject;
    //    }
    //    Instantiate(_basketballPrefab, _basketballSpawnLocation.transform.position, Quaternion.identity);
    //}

    //private void checkPlayerPrefabExists()
    //{
    //    // if player selected is not null / player not selected
    //    if (!string.IsNullOrEmpty(GameOptions.characterObjectName))
    //    {
    //        string playerPrefabPath = "Prefabs/characters/players/player_" + GameOptions.characterObjectName;
    //        _playerClone = Resources.Load(playerPrefabPath) as GameObject;
    //        //Debug.Log("load prefab");y analyticsvalidotr not working

    //    }

    //    //Debug.Log("GameObject.FindWithTag(Player) : " + GameObject.FindWithTag("Player"));
    //    // if no player, spawn player
    //    if (GameObject.FindWithTag("Player") == null )//&& GameObject.FindWithTag("autoPlayer") == null)
    //    {
    //        if (_playerClone != null)
    //        {
    //            Instantiate(_playerClone, _playerSpawnLocation.transform.position, Quaternion.identity);
    //            //Debug.Log("player spawned : " + _playerClone.name);
    //        }
    //        else
    //        {
    //            // spawn default character
    //            string playerPrefabPath = "Prefabs/characters/players/player_drblood";
    //            //Debug.Log("playerPrefabPath : " + playerPrefabPath);
    //            _playerClone = Resources.Load(playerPrefabPath) as GameObject;
    //            //Debug.Log("_playerClone : " + _playerClone);
    //            Instantiate(_playerClone, _playerSpawnLocation.transform.position, Quaternion.identity);
    //        }
    //    }
    //}

    //private string GetCurrentSceneName()
    //{
    //    return SceneManager.GetActiveScene().name;
    //}

    //private void Quit()
    //{
    //    Application.Quit();
    //}

    public GameObject Player => _player;

    public RacingVehicleController PlayerController => _playerController;
    //public AutoPlayerController AutoPlayerController => _autoPlayerController;
    public Animator Anim { get; private set; }
    public bool GameOver { get; set; }
    public PlayerControls Controls { get => controls; set => controls = value; }
    public FloatingJoystick Joystick { get => joystick; }
    //public BasketBall Basketball { get => _basketball; set => _basketball = value; }
    ////public GameObject BasketballObject { get => _basketballObject; set => _basketballObject = value; }
    //public Vector3 BasketballRimVector { get => _basketballRimVector; set => _basketballRimVector = value; }
    public RacingVehicleProfile CharacterProfile { get => _characterProfile; set => _characterProfile = value; }
    //public PlayerAttackQueue PlayerAttackQueue { get => _playerAttackQueue; set => _playerAttackQueue = value; }
    //public PlayerHealth PlayerHealth { get => _playerHealth; set => _playerHealth = value; }
    public bool IsAutoPlayer { get => isAutoPlayer; set => isAutoPlayer = value; }
    public GameObject AutoPlayer { get => _autoPlayer; set => _autoPlayer = value; }
    public GameStats GameStats { get => _gameStats; set => _gameStats = value; }
    public float TerrainHeight { get => terrainHeight;}
    public GameObject CinderBlockPrefab { get => cinderBlockPrefab;  }
}
