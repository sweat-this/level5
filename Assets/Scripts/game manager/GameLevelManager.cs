using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameLevelManager : MonoBehaviour
{
    public bool isMultiplePlayersTotalPoints;
    public int currentHighScoreTotalPoints;
    public int numPlayers;
    // this is to keep a reference to player in game manager 
    // that can be retrieved across all scripts
    [SerializeField]
    public List<PlayerIdentifier> players;
    [SerializeField]
    private PlayerIdentifier _player1;
    [SerializeField]
    private PlayerIdentifier _player2;
    [SerializeField]
    private PlayerIdentifier _player3;
    [SerializeField]
    private PlayerIdentifier _player4;
    [SerializeField]
    private PlayerIdentifier _basketball1;
    [SerializeField]
    private PlayerIdentifier _basketball2;
    [SerializeField]
    private PlayerIdentifier _basketball3;
    [SerializeField]
    private PlayerIdentifier _basketball4;
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
    [SerializeField]
    private List<GameStats> gameStatsList;
    private GameStats _gameStats;

    //BasketBall objects
    private GameObject _basketballPrefab;
    private int numBasketballs;
    private GameObject _basketballPrefabAuto;
    //private GameObject _cheerleaderPrefab;

    //spawn locations
    private GameObject _basketballSpawnLocation;
    public GameObject _playerSpawnLocation1;
    public GameObject _playerSpawnLocation2;
    public GameObject _playerSpawnLocation3;
    public GameObject _playerSpawnLocation4;
    [SerializeField]
    private GameObject _cheerleaderSpawnLocation;

    private Vector3 _basketballRimVector;

    private GameObject _playerClone1;
    private GameObject _playerClone2;
    private GameObject _playerClone3;
    private GameObject _playerClone4;
    private GameObject _cheerleaderClone;

    GameObject[] _npcObjects;
    GameObject[] _vehicleObjects;

    GameObject[] _Levels;

    private PlayerControls controls;
    FloatingJoystick joystick;

    float terrainHeight;

    public static GameLevelManager instance;
    private bool _locked;

    string playerPrefabPath1;
    string playerPrefabPath2;
    string playerPrefabPath3;
    string playerPrefabPath4;

    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
        PlayerControlsProvider.EnableOther();
    }
    private void OnDisable()
    {
        PlayerControlsProvider.DisableOther();
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

        //test 
        //GameOptions.numPlayers = 4;
        numPlayers = Mathf.Max(1, GameOptions.numPlayers);
        GameOptions.numPlayers = numPlayers;
        if (string.IsNullOrEmpty(GameOptions.matchResultId))
        {
            GameOptions.matchResultId = ProgressionService.CreateResultId("match");
        }
        EnsureCharacterObjectNames();
        if (players == null)
        {
            players = new List<PlayerIdentifier>();
        }
        else
        {
            players.Clear();
        }
        //Debug.Log("numPlayers : " + numPlayers);
        // spawn locations
        _playerSpawnLocation1 = GameObject.Find("player_spawn_location1");
        _playerSpawnLocation2 = GameObject.Find("player_spawn_location2");
        _playerSpawnLocation3 = GameObject.Find("player_spawn_location3");
        _playerSpawnLocation4 = GameObject.Find("player_spawn_location4");
        _basketballSpawnLocation = GameObject.Find("ball_spawn_location");
        _cheerleaderSpawnLocation = GameObject.Find("cheerleader_spawn_location");
        if (!HasRequiredSpawnLocations())
        {
            enabled = false;
            return;
        }
        //set terrain height
        terrainHeight = setTerrainHeight();

        //ui touch controls
        if (GameObject.FindGameObjectWithTag("joystick") != null)
        {
            joystick = GameObject.FindGameObjectWithTag("joystick").GetComponentInChildren<FloatingJoystick>();
        }

        // if basketball doesnt exists
        if (!GameOptions.gameModeRequiresBasketball && GameOptions.gameModeHasBeenSelected) { GameOptions.enemiesEnabled = true; }
        // if player doesnt exists, spawn default Player
        try
        {
            checkPlayerPrefabExists();
            checkBasketballPrefabExists();
        }
        catch (Exception exception)
        {
            Debug.LogError($"GameLevelManager could not initialize the level: {exception.Message}");
            enabled = false;
            return;
        }
        // cheerleader doesnt exists
        checkCheerleaderPrefabExists();
        //// check if player is npc in scene
        checkPlayerIsNpcInScene();
        //// get necessary references to objects
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

    private void EnsureCharacterObjectNames()
    {
        if (GameOptions.characterObjectNames == null)
        {
            GameOptions.characterObjectNames = new List<string>();
        }

        while (GameOptions.characterObjectNames.Count < numPlayers)
        {
            GameOptions.characterObjectNames.Add("drblood");
        }

        for (int i = 0; i < GameOptions.characterObjectNames.Count; i++)
        {
            if (string.IsNullOrEmpty(GameOptions.characterObjectNames[i]))
            {
                GameOptions.characterObjectNames[i] = "drblood";
            }
        }
    }

    private bool HasRequiredSpawnLocations()
    {
        if (_playerSpawnLocation1 == null || _basketballSpawnLocation == null)
        {
            Debug.LogError("GameLevelManager missing required player or basketball spawn locations.");
            return false;
        }

        if ((numPlayers > 1 || GameOptions.gameModeSelectedId == Modes.Lockdown) && _playerSpawnLocation2 == null)
        {
            Debug.LogError("GameLevelManager requires player_spawn_location2 for the selected mode.");
            return false;
        }

        if (numPlayers > 2 && _playerSpawnLocation3 == null)
        {
            Debug.LogError("GameLevelManager requires player_spawn_location3 for three-player games.");
            return false;
        }

        if (numPlayers > 3 && _playerSpawnLocation4 == null)
        {
            Debug.LogError("GameLevelManager requires player_spawn_location4 for four-player games.");
            return false;
        }

        return true;
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
        if (GameObject.FindWithTag("Player") != null)
        {
            _playerController1 = _player1.GetComponent<PlayerController>();
            _characterProfile = _player1.GetComponent<CharacterProfile>();
            _playerAttackQueue = _player1.GetComponent<PlayerAttackQueue>();
            _playerHealth = _player1.GetComponentInChildren<PlayerHealth>();
            _playerController1.isCPU = false;
            terrainHeight = Player1.transform.position.y;
        }
        //else
        //{
        //    if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_15_cocaine_island)
        //    {
        //        terrainHeight = 145;
        //    }
        //    if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_20_jacksonville)
        //    {
        //        terrainHeight = 200;
        //    }
        //    else
        //    {
        //        terrainHeight = 0;
        //    }
        //}
        if (GameObject.FindWithTag("autoPlayer") != null)
        {
            _autoPlayer = GameObject.FindWithTag("autoPlayer");

            _autoPlayerController = _autoPlayer.GetComponent<PlayerIdentifier>().isDefensivePlayer ? null : _autoPlayer.GetComponent<AutoPlayerController>();
            _playerHealth = _autoPlayer.GetComponentInChildren<PlayerHealth>();
            if (!_autoPlayer.GetComponent<PlayerIdentifier>().isDefensivePlayer)
            {
                _autoPlayerController.isCPU = true;
            }
        }

        // if shot clock is present, set shot clock camera to Camera.Main because it uses worldspace
        // instead of an overlay. this is for a slight performance increase
        if (GameObject.Find("shot_clock") != null)
        {
            GameObject.Find("shot_clock").GetComponent<Canvas>().worldCamera = Camera.main;
        }

        if (!GameOptions.gameModeHasBeenSelected && GameOptions.battleRoyalEnabled)
        {
            GameOptions.enemiesEnabled = true;
            GameObject basketballGoal = GameObject.Find("basketball_goal");
            if (basketballGoal != null)
            {
                basketballGoal.SetActive(false);
            }
        }
        GameObject basketball = GameObject.FindGameObjectWithTag("basketball");
        if (basketball != null)
        {
            _basketball1 = basketball.GetComponent<PlayerIdentifier>();
        }
        if (GameObject.Find("rim") != null)
        {
            Vector3 rim = GameObject.Find("rim").transform.position;
            _basketballRimVector = new Vector3(rim.x, 0, rim.z);
        }

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

    //public int getLeaderHighScore()
    //{
    //    List<PlayerIdentifier> sorted = players.OrderByDescending(x => x.gameStats.TotalPoints).ToList();
    //    return sorted[0].gameStats.TotalPoints;
    //}
    //static int sortByTotalPoints(GameStats g1, GameStats g2)
    //{
    //    return g1.TotalPoints.CompareTo(g2.TotalPoints);
    //}

    private void checkPlayerIsNpcInScene()
    {
        // check if player is 'a vehicle' ex. Sam on skateboard, Rad Tony on horse
        if (GameOptions.trafficEnabled)
        {
            _vehicleObjects = GameObject.FindGameObjectsWithTag("vehicle");

            // check if player is vehicle in scene
            foreach (var veh in _vehicleObjects)
            {
                if (!string.IsNullOrEmpty(GameOptions.characterObjectName) && veh.name.Contains(GameOptions.characterObjectName))
                {
                    //Debug.Log("disable veh  : " + veh.name);
                    veh.SetActive(false);
                }
            }
        }
        // check if player is an npc, ex. ??? on slab
        _npcObjects = GameObject.FindGameObjectsWithTag("auto_npc");
        foreach (var npc in _npcObjects)
        {
            if (!string.IsNullOrEmpty(GameOptions.characterObjectName) && npc.name.Contains(GameOptions.characterObjectName))
            {
                //Debug.Log("disable npc  : " + npc.name);
                npc.SetActive(false);
            }
        }
    }

    private void checkCheerleaderPrefabExists()
    {
        if (GameObject.FindWithTag("cheerleader") == null
            && GameOptions.cheerleaderObjectName != null
            && _cheerleaderSpawnLocation != null)
        {
            string cheerleaderPrefabPath = "Prefabs/characters/cheerleaders/cheerleader_" + GameOptions.cheerleaderObjectName;
            _cheerleaderClone = Resources.Load(cheerleaderPrefabPath) as GameObject;
            //Debug.Log(terrainHeight);
            _cheerleaderSpawnLocation.transform.position = new Vector3(_cheerleaderSpawnLocation.transform.position.x, terrainHeight, _cheerleaderSpawnLocation.transform.position.z);
            if (_cheerleaderClone != null)
            {
                Instantiate(_cheerleaderClone, _cheerleaderSpawnLocation.transform.position, Quaternion.identity);
            }
        }
    }

    private void checkBasketballPrefabExists()
    {
        if (players.Count == 0 || _basketballSpawnLocation == null)
        {
            Debug.LogError("Cannot spawn basketballs before players and basketball spawn location are initialized.");
            return;
        }

        numBasketballs = GameOptions.numPlayers;
        // get number of players. cpu and human
        // list of players and auto playera

        // spawn for each, basketball/bballauto
        _basketballPrefabAuto = Resources.Load(Constants.PREFAB_PATH_BASKETBALL_cpu) as GameObject;
        _basketballPrefab = Resources.Load(Constants.PREFAB_PATH_BASKETBALL_human) as GameObject;

        if (_basketballPrefab == null || _basketballPrefabAuto == null)
        {
            throw new InvalidOperationException("required human or CPU basketball prefab is missing");
        }

        GameObject go1 = Instantiate(_basketballPrefab, _basketballSpawnLocation.transform.position, Quaternion.identity);
        _basketball1 = go1.GetComponent<PlayerIdentifier>();
        _basketball1.setIds(_player1.pid, _player1.pid, _player1.pid, false);
        _basketball1.setBasketball(go1);
        players[0].setBasketball(go1);
        _basketball1.setPlayer(players[0].player);


        if (GameOptions.gameModeSelectedId == Modes.Lockdown)
        {
            numBasketballs = 1;
        }
        // player needs basketball reference
        // cpu player needs auto bball ref.
        if (numBasketballs > 1 && players.Count > 1 && GameOptions.gameModeAllowsCpuShooters)
        {
            if (GameOptions.player2IsCpu)
            {
                GameObject go2 = Instantiate(_basketballPrefabAuto, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball2 = go2.GetComponent<PlayerIdentifier>();
                _basketball2.setIds(_player2.pid, _player2.pid, _player2.pid, true);
                _basketball2.setAutoBasketball(go2);
                players[1].setAutoBasketball(go2);
                _basketball2.setAutoPlayer(players[1].autoPlayer);

            }
            else
            {
                GameObject go2 = Instantiate(_basketballPrefab, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball2 = go2.GetComponent<PlayerIdentifier>();
                _basketball2.setIds(_player2.pid, _player2.pid, _player2.pid, false);
                _basketball2.setBasketball(go2);
                players[1].setBasketball(go2);
                _basketball2.setPlayer(players[1].player);
            }
        }
        if (numBasketballs > 2 && players.Count > 2 && GameOptions.gameModeAllowsCpuShooters)
        {
            if (GameOptions.player3IsCpu)
            {
                GameObject go3 = Instantiate(_basketballPrefabAuto, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball3 = go3.GetComponent<PlayerIdentifier>();
                _basketball3.setIds(_player3.pid, _player3.pid, _player3.pid, true);
                _basketball3.setAutoBasketball(go3);
                players[2].setAutoBasketball(go3);
                _basketball3.setAutoPlayer(players[2].autoPlayer);
            }
            else
            {
                GameObject go3 = Instantiate(_basketballPrefab, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball3 = go3.GetComponent<PlayerIdentifier>();
                _basketball3.setIds(_player3.pid, _player3.pid, _player3.pid, false);
                _basketball3.setBasketball(go3);
                players[2].setBasketball(go3);
                _basketball3.setPlayer(players[2].player);
            }
        }
        if (numPlayers > 3 && players.Count > 3 && GameOptions.gameModeAllowsCpuShooters)
        {
            if (GameOptions.player4IsCpu)
            {
                GameObject go4 = Instantiate(_basketballPrefabAuto, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball4 = go4.GetComponent<PlayerIdentifier>();
                _basketball4.setIds(_player4.pid, _player4.pid, _player4.pid, true);
                _basketball4.setAutoBasketball(go4);
                players[3].setAutoBasketball(go4);
                _basketball4.setAutoPlayer(players[3].autoPlayer);
            }
            else
            {
                GameObject go4 = Instantiate(_basketballPrefab, _basketballSpawnLocation.transform.position, Quaternion.identity);
                _basketball4 = go4.GetComponent<PlayerIdentifier>();
                _basketball4.setIds(_player4.pid, _player4.pid, _player4.pid, false);
                _basketball4.setBasketball(go4);
                players[3].setBasketball(go4);
                _basketball4.setPlayer(players[3].player);
            }
        }
    }

    private void checkPlayerPrefabExists()
    {
        int pid = 0;
        if (GameOptions.characterObjectNames == null)
        {
            GameOptions.numPlayers = 1;
            playerPrefabPath1 = Constants.PREFAB_PATH_CHARACTER_human + "drblood";
        }
        else
        {
            playerPrefabPath1 = Constants.PREFAB_PATH_CHARACTER_human + GameOptions.characterObjectNames[0];
        }
        _playerClone1 = Resources.Load(playerPrefabPath1) as GameObject;
        if (_playerClone1 == null)
        {
            throw new InvalidOperationException($"player prefab not found at Resources/{playerPrefabPath1}");
        }
        GameObject go1 = Instantiate(_playerClone1, _playerSpawnLocation1.transform.position, Quaternion.identity);
        _player1 = go1.GetComponent<PlayerIdentifier>();
        _player1.setIds(pid, pid, pid, false);
        _player1.player = go1;
        _player1.setPlayer(_player1.player);

        players.Add(_player1);
        pid++;
        if (GameObject.FindWithTag("autoPlayer"))
        {
            GameObject go2 = GameObject.FindWithTag("autoPlayer");
            _player2 = go2.GetComponent<PlayerIdentifier>();
            _player2.setIds(pid, pid, pid, true);
            _player2.autoPlayer = go2;
            _player2.setAutoPlayer(_player2.autoPlayer);
            players.Add(_player2);
            pid++;
        }
        // if lockdown mode
        if (GameOptions.gameModeSelectedId == Modes.Lockdown)
        {
            playerPrefabPath2 = Constants.PREFAB_PATH_CHARACTER_DEFENSE_cpu + "oldreal";
            _playerClone2 = Resources.Load(playerPrefabPath2) as GameObject;

            GameObject go2 = Instantiate(_playerClone2, _playerSpawnLocation2.transform.position, Quaternion.identity);

            _player2 = go2.GetComponent<PlayerIdentifier>();
            _player2.setIds(pid, pid, pid, true);
            _player2.autoPlayer = go2;
            _player2.setAutoPlayer(_player2.autoPlayer);
            players.Add(_player2);
            pid++;
        }
        if (numPlayers > 1 && GameOptions.player2IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {

            if (GameOptions.gameModeSelectedId == Modes.BeatThaComputahs)
            {
                _playerClone2 = GameOptions.levelsList[GameOptions.levelSelectedIndex].CpuPlayer;
            }
            else
            {
                playerPrefabPath2 = Constants.PREFAB_PATH_CHARACTER_cpu + GameOptions.characterObjectNames[1];
                _playerClone2 = Resources.Load(playerPrefabPath2) as GameObject;

            }
            GameObject go2 = Instantiate(_playerClone2, _playerSpawnLocation2.transform.position, Quaternion.identity);

            _player2 = go2.GetComponent<PlayerIdentifier>();
            _player2.setIds(pid, pid, pid, true);
            _player2.autoPlayer = go2;
            _player2.setAutoPlayer(_player2.autoPlayer);
            players.Add(_player2);
            pid++;
        }
        if (numPlayers > 1 && !GameOptions.player2IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {
            playerPrefabPath2 = Constants.PREFAB_PATH_CHARACTER_human + GameOptions.characterObjectNames[1];
            _playerClone2 = Resources.Load(playerPrefabPath2) as GameObject;
            GameObject go2 = Instantiate(_playerClone2, _playerSpawnLocation2.transform.position, Quaternion.identity);

            _player2 = go2.GetComponent<PlayerIdentifier>();
            _player2.setIds(pid, pid, pid, false);
            _player2.player = go2;
            _player2.setPlayer(_player2.player);
            players.Add(_player2);
            pid++;
        }
        if (numPlayers > 2 && GameOptions.player3IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {
            playerPrefabPath3 = Constants.PREFAB_PATH_CHARACTER_cpu + GameOptions.characterObjectNames[2];
            _playerClone3 = Resources.Load(playerPrefabPath3) as GameObject;
            GameObject go3 = Instantiate(_playerClone3, _playerSpawnLocation3.transform.position, Quaternion.identity);

            _player3 = go3.GetComponent<PlayerIdentifier>();
            _player3.setIds(pid, pid, pid, true);
            _player3.autoPlayer = go3;
            _player3.setAutoPlayer(_player3.autoPlayer);
            players.Add(_player3);
            pid++;
        }
        if (numPlayers > 2 && !GameOptions.player3IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {
            playerPrefabPath3 = Constants.PREFAB_PATH_CHARACTER_human + GameOptions.characterObjectNames[2];
            _playerClone3 = Resources.Load(playerPrefabPath3) as GameObject;
            GameObject go3 = Instantiate(_playerClone3, _playerSpawnLocation3.transform.position, Quaternion.identity);

            _player3 = go3.GetComponent<PlayerIdentifier>();
            _player3.setIds(pid, pid, pid, false);
            _player3.player = go3;
            _player3.setPlayer(_player3.player);
            players.Add(_player3);
            pid++;
        }
        if (numPlayers > 3 && GameOptions.player4IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {
            playerPrefabPath4 = Constants.PREFAB_PATH_CHARACTER_cpu + GameOptions.characterObjectNames[3];
            _playerClone4 = Resources.Load(playerPrefabPath4) as GameObject;
            GameObject go4 = Instantiate(_playerClone4, _playerSpawnLocation4.transform.position, Quaternion.identity);
            _player4 = go4.GetComponent<PlayerIdentifier>();

            _player4.setIds(pid, pid, pid, true);
            _player4.autoPlayer = go4;
            _player4.setAutoPlayer(_player4.autoPlayer);
            players.Add(_player4);
            pid++;
        }
        if (numPlayers > 3 && !GameOptions.player4IsCpu && GameOptions.gameModeAllowsCpuShooters)
        {
            playerPrefabPath4 = Constants.PREFAB_PATH_CHARACTER_human + GameOptions.characterObjectNames[3];
            _playerClone4 = Resources.Load(playerPrefabPath4) as GameObject;
            GameObject go4 = Instantiate(_playerClone4, _playerSpawnLocation4.transform.position, Quaternion.identity);

            _player4 = go4.GetComponent<PlayerIdentifier>();
            _player4.setIds(pid, pid, pid, false);
            _player4.player = go4;
            _player4.setPlayer(_player4.player);
            players.Add(_player4);
            pid++;
        }
    }

    public PlayerIdentifier Player1 => _player1;
    public PlayerIdentifier Player2 => _player2;
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
    public GameObject PlayerSpawnLocation { get => _playerSpawnLocation1; set => _playerSpawnLocation1 = value; }
    public PlayerIdentifier Player3 { get => _player3; set => _player3 = value; }
    public PlayerIdentifier Player4 { get => _player4; set => _player4 = value; }
}
