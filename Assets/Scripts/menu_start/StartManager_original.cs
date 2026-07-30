using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = System.Random;

namespace Assets.Scripts.menu_start
{
    public class StartManager_original : MonoBehaviour
    {

        [SerializeField]
        public string currentHighlightedButton;
        //list of all shooter profiles with player data
        [SerializeField]
        private List<CharacterProfile> playerSelectedData;
        [SerializeField]
        private List<CharacterProfile> cpuPlayerSelectedData;
        // list off friend profile data
        [SerializeField]
        private List<CheerleaderProfile> friendSelectedData;
        // list off level  data
        [SerializeField]
        private List<LevelSelected> levelSelectedData;
        //mode selected objects
        [SerializeField]
        private List<StartScreenModeSelected> modeSelectedData;

        //private Text playerSelectUnlockText;

        [SerializeField]
        private Text friendSelectUnlockText;

        // option select buttons, this will be disabled with touch input
        Button numPlayersSelectButton;
        Button levelSelectButton;
        Button trafficSelectButton;
        Button hardcoreSelectButton;
        Button enemySelectButton;
        Button sniperSelectButton;
        Button difficultySelectButton;
        Button obstacleSelectButton;
        Button playerSelectButton;
        Button friendSelectButton;
        Button modeSelectButton;

        //player selected display
        private Text playerSelectOptionText;
        private Image playerSelectOptionImage;
        private Text playerSelectOptionStatsText;
        private Text playerSelectCategoryStatsText;
        private Text playerProgressionCategoryText;
        private Text playerProgressionStatsText;
        [SerializeField]
        private Text playerProgressionUpdatePointsText;

        // num player select display
        private Text numPlayersSelectOptionText;

        // level select display
        private Text levelSelectOptionText;
        private Text levelSelectOptionDescriptionText;

        //friend selected display
        private Text friendSelectOptionText;
        private Image friendSelectOptionImage;

        //mode selected display
        private Text modeSelectOptionText;
        private Text modeSelectOptionNameText;
        private Text ModeSelectOptionDescriptionText;

        //selectable option text
        [SerializeField]
        private Text trafficSelectOptionText;
        [SerializeField]
        private Text hardcoreSelectOptionText;
        [SerializeField]
        private Text enemySelectOptionText;
        [SerializeField]
        private Text sniperSelectOptionText;
        [SerializeField]
        private Text difficultySelectOptionText;
        [SerializeField]
        private Text difficultySelectOptionDescriptionText;
        [SerializeField]
        private Text obstacleSelectOptionText;

        //version text
        private Text versionText;
        private Text latestVersionText;
        private Text userNameText;

        //const object names
        public const string startButtonName = "press_start";
        public const string statsMenuButtonName = "stats_menu";
        public const string quitButtonName = "quit_game";
        public const string optionsMenuButtonName = "options_menu";
        public const string creditsMenuButtonName = "credits_menu";
        public const string updateMenuButtonName = "update_menu";
        public const string accountMenuButtonName = "account_menu";
        public const string updatePointsAvailable = "update_points_available";

        public const string playerSelectButtonName = "player_select";
        public const string playerSelectOptionButtonName = "player_selected_name";
        public const string playerSelectStatsObjectName = "player_selected_stats_numbers";
        public const string playerSelectImageObjectName = "player_selected_image";
        //public const string playerSelectUnlockObjectName = "player_selected_unlock";
        //public const string playerSelectIsLockedObjectName = "player_selected_lock_texture";
        public const string playerSelectStatsCategoryName = "player_selected_stats_category";

        public const string playerProgressionName = "player_progression";
        public const string playerProgressionStatsName = "player_progression_stats";

        public const string cpuSelectButtonName = "cpu_select";
        public const string cpuSelectOptionButtonName = "cpu_selected_name";
        //friend objects
        public const string friendSelectButtonName = "friend_select";
        public const string friendSelectOptionButtonName = "friend_selected_name";
        public const string friendSelectImageObjectName = "friend_selected_image";
        public const string friendSelectUnlockObjectName = "friend_selected_unlock";
        public const string friendSelectIsLockedObjectName = "friend_selected_lock_texture";

        //level objects
        public const string levelSelectButtonName = "level_select";
        public const string levelSelectOptionButtonName = "level_selected_name";

        //level objects
        public const string numPlayersSelectButtonName = "num_players_select";
        public const string numPlayersSelectOptionButtonName = "num_players_selected_name";

        //mode objects
        public const string modeSelectButtonName = "mode_select";
        public const string modeSelectOptionButtonName = "mode_selected_name";
        public const string modeSelectDescriptionObjectName = "mode_selected_description";

        //traffic objects
        public const string trafficSelectButtonName = "traffic_select";
        public const string trafficSelectOptionName = "traffic_select_option";

        //hardcore mode
        public const string hardcoreSelectButtonName = "hardcore_select";
        public const string hardcoreSelectOptionName = "hardcore_select_option";

        //hardcore mode
        public const string enemySelectButtonName = "enemy_select";
        public const string enemySelectOptionName = "enemy_select_option";

        //sniper
        public const string sniperSelectButtonName = "sniper_select";
        public const string sniperSelectOptionName = "sniper_select_option";
        //difficulty
        public const string difficultySelectButtonName = "difficulty_select";
        public const string difficultySelectOptionName = "difficulty_select_option";
        public const string difficultySelectDescriptionName = "difficulty_selected_description";
        //obstacle
        public const string obstacleSelectButtonName = "obstacle_select";
        public const string obstacleSelectOptionName = "obstacle_select_option";
        //options
        public const string optionsSelectButtonName = "options_select";
        public const string optionsSelectOptionName = "options_selected_name";

        public const string Cpu1SelectOptionName = "cpu1_button";
        public const string Cpu2SelectOptionName = "cpu2_button";
        public const string Cpu3SelectOptionName = "cpu3_button";

        [SerializeField]
        private bool trafficEnabled;
        [SerializeField]
        private bool hardcoreEnabled;
        [SerializeField]
        private bool enemiesEnabled;
        [SerializeField]
        private bool obstaclesEnabled;
        [SerializeField]
        private int difficultySelected;
        [SerializeField]
        private bool sniperEnabled;
        [SerializeField]
        public int playerSelectedIndex;
        public int levelSelectedIndex;
        public int modeSelectedIndex;
        public int friendSelectedIndex;
        public int cpu1SelectedIndex;
        public int cpu2SelectedIndex;
        public int cpu3SelectedIndex;

        //private int numOfPlayers; //testing with 1

        private PlayerControls controls;

        [SerializeField]
        public static StartManager_original instance;

        bool buttonPressed = false;
        bool dataLoaded = false;

        private void OnEnable()
        {
            controls = PlayerControlsProvider.Controls;
            PlayerControlsProvider.EnableMenuMaps();
        }
        private void OnDisable()
        {
            PlayerControlsProvider.DisableMenuMaps();
        }

        //private Text gameModeSelectText;
        void Awake()
        {
            instance = this;
            StartCoroutine(getLoadedData());
            controls = PlayerControlsProvider.Controls;
            // find all button / text / etc and assign to variables
            StartCoroutine(GetUiObjectReferences());

            //default index for player selected
            playerSelectedIndex = GameOptions.playerSelectedIndex;
            friendSelectedIndex = GameOptions.cheerleaderSelectedIndex;
            levelSelectedIndex = GameOptions.levelSelectedIndex;
            modeSelectedIndex = GameOptions.modeSelectedIndex;
            trafficEnabled = GameOptions.trafficEnabled;
            hardcoreEnabled = GameOptions.hardcoreModeEnabled;
            difficultySelected = 1;
            obstaclesEnabled = GameOptions.obstaclesEnabled;

            // update experience and levels
            // recommended here because experience will be gained after every game played
            StartCoroutine(UpdateLevelAndExperienceFromDatabase());

            // diable nav buttons if mobile
#if UNITY_ANDROID && !UNITY_EDITOR
            disableButtonsNotUsedForTouchInput();
#endif
            //StartCoroutine(InitializeDisplay());
        }

        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(InitializeDisplay());
            StartCoroutine(SetVersion());
            AnaylticsManager.MenuStartLoaded();
        }


        // Update is called once per frame
        void Update()
        {
            // check for some button not selected
            if (EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject); // + "_description";
                }
                currentHighlightedButton = EventSystem.current.currentSelectedGameObject.name; // + "_description";
            }
            // if player highlighted, display player
            if ((currentHighlightedButton.Equals(numPlayersSelectButtonName) || currentHighlightedButton.Equals(numPlayersSelectOptionButtonName))
                && dataLoaded)
            {
                initializeNumPlayersDisplay();
            }
            // if player highlighted, display player
            if ((currentHighlightedButton.Equals(playerSelectButtonName) || currentHighlightedButton.Equals(playerSelectOptionButtonName))
                && dataLoaded)
            {
                initializePlayerDisplay();
            }
            // friend
            if (currentHighlightedButton.Equals(friendSelectButtonName) || currentHighlightedButton.Equals(FriendSelectOptionButtonName))
            {
                initializefriendDisplay();
            }
            if (currentHighlightedButton.Equals(levelSelectButtonName) || currentHighlightedButton.Equals(levelSelectOptionButtonName))
            {
                initializeLevelDisplay();
            }
            if (currentHighlightedButton.Equals(modeSelectButtonName) || currentHighlightedButton.Equals(modeSelectOptionButtonName))
            {
                initializeModeDisplay();
            }
            if (currentHighlightedButton.Equals(cpuSelectButtonName) || currentHighlightedButton.Equals(cpuSelectOptionButtonName))
            {
                initializeCpuDisplay();
            }
            if (currentHighlightedButton.Equals(Cpu1SelectOptionName)) { setCpuPlayer1(); }
            if (currentHighlightedButton.Equals(Cpu2SelectOptionName)) { setCpuPlayer2(); }
            if (currentHighlightedButton.Equals(Cpu3SelectOptionName)) { setCpuPlayer3(); }
            if (currentHighlightedButton.Equals(levelSelectButtonName) || currentHighlightedButton.Equals(levelSelectOptionButtonName))
            {
                initializeLevelDisplay();
            }
            if (currentHighlightedButton.Equals(modeSelectButtonName) || currentHighlightedButton.Equals(modeSelectOptionButtonName))
            {
                initializeModeDisplay();
            }
            if (currentHighlightedButton.Equals(optionsSelectButtonName) || currentHighlightedButton.Equals(optionsSelectOptionName))
            {
                initializeOptionsDisplay();
            }
            // ================================== footer buttons =====================================================================
            // start button | start game
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(startButtonName))
            {
                loadGame();
            }
            // quit button | quit game
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(quitButtonName))
            {
                Application.Quit();
            }
            // stats menu button | load stats menu
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(statsMenuButtonName))
            {
                loadMenu(Constants.SCENE_NAME_level_00_stats);
            }

            // update menu button | load update menu
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(updateMenuButtonName))
            {
                GameOptions.playerSelectedIndex = playerSelectedIndex;
                loadMenu(Constants.SCENE_NAME_level_00_progression);
            }
            // options menu button | load options menu
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(optionsMenuButtonName))
            {
                loadMenu(Constants.SCENE_NAME_level_00_options);
            }
            // credits menu button | load credits menu
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(creditsMenuButtonName))
            {
                loadMenu(Constants.SCENE_NAME_level_00_credits);
            }

            // account menu button | load account menu
            if (PlayerControlsProvider.MenuSubmitTriggered
                && currentHighlightedButton.Equals(accountMenuButtonName))
            {
                loadMenu(Constants.SCENE_NAME_level_00_account);
            }

            // ================================== navigation =====================================================================
            // up, option select
            if (controls.UINavigation.Up.triggered && !buttonPressed
                && !currentHighlightedButton.Equals(numPlayersSelectOptionButtonName)
                && !currentHighlightedButton.Equals(playerSelectOptionButtonName)
                && !currentHighlightedButton.Equals(levelSelectOptionButtonName)
                && !currentHighlightedButton.Equals(modeSelectOptionButtonName)
                && !currentHighlightedButton.Equals(trafficSelectOptionName)
                && !currentHighlightedButton.Equals(FriendSelectOptionButtonName)
                && !currentHighlightedButton.Equals(hardcoreSelectOptionName)
                && !currentHighlightedButton.Equals(enemySelectOptionName)
                && !currentHighlightedButton.Equals(difficultySelectOptionName)
                && !currentHighlightedButton.Equals(obstacleSelectOptionName)
                && !currentHighlightedButton.Equals(SniperSelectOptionName)
                && !currentHighlightedButton.Equals(cpuSelectOptionButtonName)
                && !currentHighlightedButton.Equals(optionsSelectOptionName))
            {
                buttonPressed = true;
                EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
                    .GetComponent<Button>().FindSelectableOnUp().gameObject);
                buttonPressed = false;
            }

            // down, option select
            if (controls.UINavigation.Down.triggered && !buttonPressed
                && !currentHighlightedButton.Equals(numPlayersSelectOptionButtonName)
                && !currentHighlightedButton.Equals(playerSelectOptionButtonName)
                && !currentHighlightedButton.Equals(levelSelectOptionButtonName)
                && !currentHighlightedButton.Equals(modeSelectOptionButtonName)
                && !currentHighlightedButton.Equals(FriendSelectOptionButtonName)
                && !currentHighlightedButton.Equals(trafficSelectOptionName)
                && !currentHighlightedButton.Equals(hardcoreSelectOptionName)
                && !currentHighlightedButton.Equals(difficultySelectOptionName)
                && !currentHighlightedButton.Equals(enemySelectOptionName)
                && !currentHighlightedButton.Equals(obstacleSelectOptionName)
                && !currentHighlightedButton.Equals(cpuSelectOptionButtonName)
                && !currentHighlightedButton.Equals(SniperSelectOptionName)
                && !currentHighlightedButton.Equals(optionsSelectOptionName))
            {
                buttonPressed = true;
                EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
                    .GetComponent<Button>().FindSelectableOnDown().gameObject);
                buttonPressed = false;
            }

            // right, go to change options
            if (controls.UINavigation.Right.triggered
                && EventSystem.current.currentSelectedGameObject.GetComponent<Button>()
                .FindSelectableOnRight() != null)
            {
                EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
                    .GetComponent<Button>().FindSelectableOnRight().gameObject);
            }

            // left, return to option select
            if (controls.UINavigation.Left.triggered)
            {
                // check if button exists. if no selectable on left, throws null object exception
                if (EventSystem.current.currentSelectedGameObject.GetComponent<Button>().FindSelectableOnLeft() != null)
                {
                    EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
                        .GetComponent<Button>().FindSelectableOnLeft().gameObject);
                }
            }

            // ================================== change options =============================================================
            // up, change options
            if (controls.UINavigation.Up.triggered && !buttonPressed)
            {
                buttonPressed = true;
                try
                {
                    if (currentHighlightedButton.Equals(numPlayersSelectOptionButtonName))
                    {
                        changeSelectedNumPlayersUp();
                        initializeNumPlayersDisplay();
                    }
                    if (currentHighlightedButton.Equals(playerSelectOptionButtonName))
                    {
                        changeSelectedPlayerUp();
                        initializePlayerDisplay();
                    }
                    if (currentHighlightedButton.Equals(levelSelectOptionButtonName))
                    {
                        Debug.Log("change level");
                        changeSelectedLevelUp();
                        initializeLevelDisplay();
                    }
                    if (currentHighlightedButton.Equals(modeSelectOptionButtonName))
                    {
                        changeSelectedModeUp();
                        initializeModeDisplay();
                    }
                    if (currentHighlightedButton.Equals(FriendSelectOptionButtonName))
                    {
                        changeSelectedfriendUp();
                        initializefriendDisplay();
                    }
                    if (currentHighlightedButton.Equals(optionsSelectOptionName))
                    {
                        Debug.Log("option up");
                        //changeSelectedfriendUp();
                        //initializefriendDisplay();
                    }
                    if (currentHighlightedButton.Equals(trafficSelectOptionName))
                    {
                        // disabled for now. default : OFF
                        changeSelectedTrafficOption();
                        initializeTrafficOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(hardcoreSelectOptionName))
                    {
                        changeSelectedHardcoreOption();
                        initializeHardcoreOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(enemySelectOptionName))
                    {
                        changeSelectedEnemiesOption();
                        initializeEnemyOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(SniperSelectOptionName))
                    {
                        changeSelectedSniperOption();
                        initializeSniperOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(difficultySelectOptionName))
                    {
                        changeSelectedDifficultyOption(difficultySelected);
                        initializeDifficultyOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(ObstacleSelectOptionName))
                    {
                        changeSelectedObstacleOption();
                        initializeObstacleOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(Cpu1SelectOptionName)
                        || currentHighlightedButton.Equals(Cpu2SelectOptionName)
                        || currentHighlightedButton.Equals(Cpu3SelectOptionName))
                    {
                        changeSelectedCpuOptionUp(currentHighlightedButton);
                    }
                }
                catch
                {
                    return;
                }
                buttonPressed = false;
            }
            // down, change option
            if (controls.UINavigation.Down.triggered && !buttonPressed)
            {
                buttonPressed = true;
                try
                {
                    if (currentHighlightedButton.Equals(numPlayersSelectOptionButtonName))
                    {
                        changeSelectedNumPlayersDown();
                        initializeNumPlayersDisplay();
                    }
                    if (currentHighlightedButton.Equals(playerSelectOptionButtonName))
                    {
                        changeSelectedPlayerDown();
                        initializePlayerDisplay();
                    }
                    if (currentHighlightedButton.Equals(levelSelectOptionButtonName))
                    {
                        Debug.Log("change level");
                        changeSelectedLevelDown();
                        initializeLevelDisplay();
                    }
                    if (currentHighlightedButton.Equals(modeSelectOptionButtonName))
                    {
                        changeSelectedModeDown();
                        initializeModeDisplay();
                    }
                    if (currentHighlightedButton.Equals(FriendSelectOptionButtonName))
                    {
                        changeSelectedfriendDown();
                        initializefriendDisplay();
                    }
                    if (currentHighlightedButton.Equals(optionsSelectOptionName))
                    {
                        Debug.Log("option down");
                        //changeSelectedfriendUp();
                        //initializefriendDisplay();
                    }
                    if (currentHighlightedButton.Equals(trafficSelectOptionName))
                    {
                        changeSelectedTrafficOption();
                        initializeTrafficOptionDisplay();

                    }
                    if (currentHighlightedButton.Equals(hardcoreSelectOptionName))
                    {
                        changeSelectedHardcoreOption();
                        initializeHardcoreOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(enemySelectOptionName))
                    {
                        changeSelectedEnemiesOption();
                        initializeEnemyOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(SniperSelectOptionName))
                    {
                        changeSelectedSniperOption();
                        initializeSniperOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(difficultySelectOptionName))
                    {
                        changeSelectedDifficultyOption(difficultySelected);
                        initializeDifficultyOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(ObstacleSelectOptionName))
                    {
                        changeSelectedObstacleOption();
                        initializeObstacleOptionDisplay();
                    }
                    if (currentHighlightedButton.Equals(Cpu1SelectOptionName)
                        || currentHighlightedButton.Equals(Cpu2SelectOptionName)
                        || currentHighlightedButton.Equals(Cpu3SelectOptionName))
                    {
                        changeSelectedCpuOptionDown(currentHighlightedButton);
                    }
                }
                catch
                {
                    return;
                }
                buttonPressed = false;
            }
        }

        private void changeSelectedCpuOptionDown(string currentHighlightedButton)
        {
            switch (currentHighlightedButton)
            {
                case Cpu1SelectOptionName:
                    if (cpu1SelectedIndex == 0)
                    {
                        cpu1SelectedIndex = cpuPlayerSelectedData.Count - 1;
                    }
                    else
                    {
                        cpu1SelectedIndex--;
                    }
                    setCpuPlayer1();
                    break;
                case Cpu2SelectOptionName:
                    if (cpu2SelectedIndex == 0)
                    {
                        cpu2SelectedIndex = cpuPlayerSelectedData.Count - 1;
                    }
                    else
                    {
                        cpu2SelectedIndex--;
                    }
                    setCpuPlayer2();
                    break;
                case Cpu3SelectOptionName:
                    if (cpu3SelectedIndex == 0)
                    {
                        cpu3SelectedIndex = cpuPlayerSelectedData.Count - 1;
                    }
                    else
                    {
                        cpu3SelectedIndex--;
                    }
                    setCpuPlayer3();
                    break;
            }
        }
        private void changeSelectedCpuOptionUp(string currentHighlightedButton)
        {
            switch (currentHighlightedButton)
            {
                case Cpu1SelectOptionName:
                    if (cpu1SelectedIndex < cpuPlayerSelectedData.Count - 1)
                    {
                        cpu1SelectedIndex++;
                    }
                    else
                    {
                        cpu1SelectedIndex = 0;
                    }
                    setCpuPlayer1();
                    break;
                case Cpu2SelectOptionName:
                    if (cpu2SelectedIndex < cpuPlayerSelectedData.Count - 1)
                    {
                        cpu2SelectedIndex++;
                    }
                    else
                    {
                        cpu2SelectedIndex = 0;
                    }
                    setCpuPlayer2();
                    break;
                case Cpu3SelectOptionName:
                    if (cpu3SelectedIndex < cpuPlayerSelectedData.Count - 1)
                    {
                        cpu3SelectedIndex++;
                    }
                    else
                    {
                        cpu3SelectedIndex = 0;
                    }
                    setCpuPlayer3();
                    break;
            }
        }


        IEnumerator UpdateLevelAndExperienceFromDatabase()
        {
            yield return new WaitUntil(() => dataLoaded);

            foreach (CharacterProfile s in playerSelectedData)
            {
                s.Experience = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "experience", s.PlayerId);
                s.Level = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "level", s.PlayerId);
            }
        }

        IEnumerator getLoadedData()
        {
            if (LoadedData.instance != null)
            {
                yield return new WaitUntil(() => LoadedData.instance.PlayerSelectedData != null);
                playerSelectedData = LoadedData.instance.PlayerSelectedData;

                yield return new WaitUntil(() => LoadedData.instance.CpuPlayerSelectedData != null);
                cpuPlayerSelectedData = LoadedData.instance.CpuPlayerSelectedData;

                yield return new WaitUntil(() => LoadedData.instance.CheerleaderSelectedData != null);
                friendSelectedData = LoadedData.instance.CheerleaderSelectedData;

                yield return new WaitUntil(() => LoadedData.instance.LevelSelectedData != null);
                levelSelectedData = LoadedData.instance.LevelSelectedData;

                yield return new WaitUntil(() => LoadedData.instance.ModeSelectedData != null);
                modeSelectedData = LoadedData.instance.ModeSelectedData;

                if (playerSelectedData != null
                    && friendSelectedData != null
                    && levelSelectedData != null
                    && modeSelectedData != null)
                {
                    dataLoaded = true;
                }
            }
            else
            {
                SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
            }
        }

        IEnumerator InitializeDisplay()
        {
            yield return new WaitUntil(() => dataLoaded);
            // display default data
            initializeNumPlayersDisplay();
            initializefriendDisplay();
            initializeCpuDisplay();
            initializeLevelDisplay();

            initializeModeDisplay();
            initializeEnemyOptionDisplay();
            initializeTrafficOptionDisplay();
            initializeHardcoreOptionDisplay();
            initializeSniperOptionDisplay();
            initializeDifficultyOptionDisplay();
            initializeObstacleOptionDisplay();
            initializePlayerDisplay();
            initializeCpuPlayerDisplay();
            setInitialGameOptions();
        }

        private void initializeCpuDisplay()
        {
            //Debug.Log("initializeCpuDisplay");
            disableMenuObjects("cpu_tab");
            enableMenuObjects("cpu_tab");

        }

        private IEnumerator SetVersion()
        {
            yield return new WaitUntil(() => StartMenuUiObjects.instance != null);

            Debug.Log(GameOptions.userName);
            if (APIHelper.BearerToken != null && !string.IsNullOrEmpty(GameOptions.userName))
            {
                userNameText.text = "username : " + GameOptions.userName + " connected";
            }
            if (APIHelper.BearerToken == null || string.IsNullOrEmpty(GameOptions.userName))
            {
                userNameText.text = "username : " + GameOptions.userName + " disconnected";
            }
            versionText.text = "current version: " + Application.version;
            yield return new WaitUntil(() => !APIHelper.ApiLocked);
            if (UtilityFunctions.IsConnectedToInternet())
            {
                latestVersionText.text = "latest version: " + APIHelper.GetLatestBuildVersion();
            }
            else
            {
                latestVersionText.text = "latest version: " + "No Internet";
            }
        }

        // ============================  get UI buttons / text references ==============================
        private IEnumerator GetUiObjectReferences()
        {
            yield return new WaitUntil(() => StartMenuUiObjects.instance != null);

            // buttons to disable for touch input
            levelSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_level_selected_name_button;
            trafficSelectButton = StartMenuUiObjects.instance.column2_options_tab_traffic_select_button;
            hardcoreSelectButton = StartMenuUiObjects.instance.column2_options_tab_hardcore_select_button;
            enemySelectButton = StartMenuUiObjects.instance.column2_options_tab_enemy_select_button;
            sniperSelectButton = StartMenuUiObjects.instance.column2_options_tab_sniper_select_button;
            difficultySelectButton = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_button;
            obstacleSelectButton = StartMenuUiObjects.instance.column2_options_tab_obstacles_select_button;
            playerSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_player_select_name_button;
            friendSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_friend_selected_name_button;
            modeSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_mode_selected_name_button;

            // player object with lock texture and unlock text
            playerSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_player_select_name_text;
            playerSelectOptionStatsText = StartMenuUiObjects.instance.column3_player_selected_stats_numbers_text;
            playerSelectOptionImage = StartMenuUiObjects.instance.column2_players_tab_player_selected_image;
            playerSelectCategoryStatsText = StartMenuUiObjects.instance.column3_player_selected_stats_category_text;
            playerProgressionStatsText = StartMenuUiObjects.instance.column3_player_selected_progression_stats_text;
            playerProgressionCategoryText = StartMenuUiObjects.instance.column3_player_selected_progression_text;
            playerProgressionUpdatePointsText = StartMenuUiObjects.instance.column3_player_selected_progression_update_points_text;

            // friend object with lock texture and unlock text
            friendSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_friend_selected_name_text;
            friendSelectOptionImage = StartMenuUiObjects.instance.column2_friend_tab_friend_selected_image;

            // options selection text
            trafficSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_traffic_select_option_text;
            hardcoreSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_hardcore_select_option_text;
            enemySelectOptionText = StartMenuUiObjects.instance.column2_options_tab_enemy_select_option_text;
            sniperSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_sniper_select_option_text;
            difficultySelectOptionText = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_option_text;
            obstacleSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_obstacle_select_option_text;

            //version
            versionText = StartMenuUiObjects.instance.header_version;
            latestVersionText = StartMenuUiObjects.instance.header_latestVersion;
            userNameText = StartMenuUiObjects.instance.header_username;
        }

        private void setInitialGameOptions()
        {
            GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;
            //List<string> names = new List<string>();
            //List<int> ids = new List<int>();
            //for (int i = 0; i < GameOptions.numPlayers; i++)
            //{
            //    names.Insert(i, playerSelectedData[playerSelectedIndex].PlayerObjectName);
            //    ids.Insert(i, playerSelectedData[playerSelectedIndex].PlayerId);

            //    Debug.Log(names[i]);
            //    Debug.Log(ids[i]);
            //}
            //GameOptions.characterObjectNames = names;
            //GameOptions.playerIds = ids;

            //GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;

            GameOptions.levelSelected = levelSelectedData[levelSelectedIndex].LevelObjectName;
            GameOptions.gameModeSelectedName = modeSelectedData[modeSelectedIndex].ModeObjectName;
            GameOptions.gameModeSelectedId = modeSelectedData[modeSelectedIndex].ModeId;

            GameOptions.gameModeRequiresCounter = modeSelectedData[modeSelectedIndex].ModeRequiresCounter;
            GameOptions.gameModeRequiresCountDown = modeSelectedData[modeSelectedIndex].ModeRequiresCountDown;
        }

        public string getRandomWizardOfBoat()
        {

            Random random = new Random();
            int randNum = random.Next(1, 100);

            if (randNum > 50)
            {
                return "wob1";
            }
            else
            {
                return "wob2";
            }
        }

        public void disableButtonsNotUsedForTouchInput()
        {
            levelSelectButton.enabled = false;
            trafficSelectButton.enabled = false;
            playerSelectButton.enabled = false;
            friendSelectButton.enabled = false;
            modeSelectButton.enabled = false;
        }

        public void enableButtonsNotUsedForTouchInput()
        {
            Debug.Log("enable buttons");
            levelSelectButton.enabled = true;
            trafficSelectButton.enabled = true;
            playerSelectButton.enabled = true;
            friendSelectButton.enabled = true;
            modeSelectButton.enabled = true;
        }

        public void changeSelectedTrafficOption()
        {
            trafficEnabled = !trafficEnabled;
        }

        public void changeSelectedHardcoreOption()
        {
            hardcoreEnabled = !hardcoreEnabled;
        }

        public void changeSelectedEnemiesOption()
        {
            enemiesEnabled = !enemiesEnabled;
        }

        public void changeSelectedSniperOption()
        {
            sniperEnabled = !sniperEnabled;
        }

        public void changeSelectedObstacleOption()
        {
            obstaclesEnabled = !obstaclesEnabled;
        }

        public void changeSelectedDifficultyOption(int currentDifficulty)
        {
            int maxDifficulty = 2;
            if (currentDifficulty == maxDifficulty)
            {
                currentDifficulty = 0;
            }
            else
            {
                currentDifficulty++;
            }
            difficultySelected = currentDifficulty;
        }

        // ============================  Initialize displays ==============================

        public void initializeTrafficOptionDisplay()
        {
            if (trafficEnabled)
            {
                trafficSelectOptionText.text = "ON";
            }
            if (!trafficEnabled)
            {
                trafficSelectOptionText.text = "OFF";
            }
        }

        public void initializeHardcoreOptionDisplay()
        {
            if (hardcoreEnabled)
            {
                hardcoreSelectOptionText.text = "ON";
            }
            if (!hardcoreEnabled)
            {
                hardcoreSelectOptionText.text = "OFF";
            }
        }
        public void initializeEnemyOptionDisplay()
        {
            if (enemiesEnabled)
            {
                enemySelectOptionText.text = "ON";
            }
            if (!enemiesEnabled)
            {
                enemySelectOptionText.text = "OFF";
            }
        }
        public void initializeSniperOptionDisplay()
        {
            if (sniperEnabled)
            {
                sniperSelectOptionText.text = "ON";
            }
            if (!sniperEnabled)
            {
                sniperSelectOptionText.text = "OFF";
            }
        }
        public void initializeDifficultyOptionDisplay()
        {
            difficultySelectOptionDescriptionText = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_description_text;
            if (difficultySelected == 0)
            {
                difficultySelectOptionText.text = "easy";
                difficultySelectOptionDescriptionText.text = "max stats | 0.5x experience";
            }
            if (difficultySelected == 1)
            {
                difficultySelectOptionText.text = "normal";
                difficultySelectOptionDescriptionText.text = "basic stats | 1x experience";
            }
            if (difficultySelected == 2)
            {
                difficultySelectOptionText.text = "hardcore";
                difficultySelectOptionDescriptionText.text = "basic stats | 1.5x experience";
            }
        }
        public void initializeObstacleOptionDisplay()
        {
            if (obstaclesEnabled)
            {
                obstacleSelectOptionText.text = "ON";
            }
            if (!obstaclesEnabled)
            {
                obstacleSelectOptionText.text = "OFF";
            }
        }

        public void initializeLevelDisplay()
        {
            disableMenuObjects("level_tab");
            enableMenuObjects("level_tab");

            // NOTE : add level column 2 refs to change text/descr
            // add descritpion to startmenu levle objects
            StartMenuUiObjects.instance.column1_subgroup_column2_level_selected_name_text.text
                = StartMenuUiObjects.instance.column2_level_tab_level_selected_name.text
                = levelSelectedData[levelSelectedIndex].LevelDisplayName;
            StartMenuUiObjects.instance.column2_level_tab_level_selected_info.text = levelSelectedData[levelSelectedIndex].LevelInfo;

            GameOptions.levelSelected = levelSelectedData[levelSelectedIndex].LevelObjectName;
        }

        public void initializeNumPlayersDisplay()
        {
            numPlayersSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_num_players_selected_name_text;
            numPlayersSelectOptionText.text = GameOptions.numPlayers.ToString();
        }

        public void initializefriendDisplay()
        {
            try
            {
                disableMenuObjects("friend_tab");
                StartMenuUiObjects.instance.column2_friend_tab.SetActive(true);

                friendSelectOptionText.text = friendSelectedData[friendSelectedIndex].CheerleaderDisplayName;
                friendSelectOptionImage.sprite = friendSelectedData[friendSelectedIndex].CheerleaderPortrait;


                friendSelectOptionText = GameObject.Find(FriendSelectOptionButtonName).GetComponent<Text>();
                friendSelectOptionText.text = friendSelectedData[friendSelectedIndex].CheerleaderDisplayName;
                GameOptions.cheerleaderObjectName = friendSelectedData[friendSelectedIndex].CheerleaderObjectName;
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
        }
        public void initializeOptionsDisplay()
        {
            try
            {
                Debug.Log("initializeOptionsDisplay");
                disableMenuObjects("options_tab");
                enableMenuObjects("options_tab");
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
        }

        public void initializeModeDisplay()
        {
            disableMenuObjects("mode_tab");
            enableMenuObjects("mode_tab");

            modeSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_mode_selected_name_text;
            modeSelectOptionText.text = modeSelectedData[modeSelectedIndex].ModeDisplayName;

            modeSelectOptionNameText = StartMenuUiObjects.instance.column2_mode_tab_mode_selected_name;
            modeSelectOptionNameText.text = modeSelectedData[modeSelectedIndex].ModeDisplayName;

            ModeSelectOptionDescriptionText = StartMenuUiObjects.instance.column2_mode_tab_mode_selected_description;
            ModeSelectOptionDescriptionText.text = modeSelectedData[modeSelectedIndex].ModeDescription;
        }
        void disableMenuObjects(string activeMenu)
        {
            if (!activeMenu.ToLower().Equals("players_tab"))
            {
                StartMenuUiObjects.instance.column2_players_tab.SetActive(false);
                StartMenuUiObjects.instance.column3.SetActive(false);
                StartMenuUiObjects.instance.column2_players_tab.SetActive(false);
            }
            if (!activeMenu.ToLower().Equals("cpu_tab"))
            {
                StartMenuUiObjects.instance.column4.SetActive(false);
                StartMenuUiObjects.instance.column2.SetActive(true);
                StartMenuUiObjects.instance.column1_subgroup_column2.SetActive(true);
            }
            if (!activeMenu.ToLower().Equals("friend_tab"))
            {
                StartMenuUiObjects.instance.column2_friend_tab.SetActive(false);
            }
            if (!activeMenu.ToLower().Equals("level_tab"))
            {
                StartMenuUiObjects.instance.column2_level_tab.SetActive(false);
            }
            if (!activeMenu.ToLower().Equals("mode_tab"))
            {
                StartMenuUiObjects.instance.column2_mode_tab.SetActive(false);
            }
            if (!activeMenu.ToLower().Equals("options_tab"))
            {
                StartMenuUiObjects.instance.column2_options_tab.SetActive(false);
            }
            if (activeMenu.ToLower().Equals("cpu_tab"))
            {
                StartMenuUiObjects.instance.column2.SetActive(false);
                StartMenuUiObjects.instance.column1_subgroup_column2.SetActive(false);
            }
        }
        void enableMenuObjects(string activeMenu)
        {
            if (activeMenu.ToLower().Equals("players_tab"))
            {
                StartMenuUiObjects.instance.column1_subgroup_column2.SetActive(true);
                StartMenuUiObjects.instance.column2.SetActive(true);
                StartMenuUiObjects.instance.column2_players_tab.SetActive(true);
                StartMenuUiObjects.instance.column3.SetActive(true);
                StartMenuUiObjects.instance.column3_player_stats.SetActive(true);
            }

            if (activeMenu.ToLower().Equals("cpu_tab"))
            {
                StartMenuUiObjects.instance.column4.SetActive(true);
                // cpu player display
                initializeCpuPlayerDisplay();
            }

            //if (activeMenu.ToLower().Equals("friend_tab"))
            //{
            //    StartMenuUiObjects.instance.column2_friend_tab.SetActive(false);
            //    StartMenuUiObjects.instance.column4.SetActive(false);
            //    StartMenuUiObjects.instance.column3.SetActive(false);

            //}
            if (activeMenu.ToLower().Equals("level_tab"))
            {
                StartMenuUiObjects.instance.column2_level_tab.SetActive(true);
            }
            if (activeMenu.ToLower().Equals("mode_tab"))
            {
                StartMenuUiObjects.instance.column2_mode_tab.SetActive(true);

            }
            if (activeMenu.ToLower().Equals("options_tab"))
            {
                StartMenuUiObjects.instance.column2_options_tab.SetActive(true);
            }
        }

        public void initializePlayerDisplay()
        {
            try
            {
                disableMenuObjects("players_tab");
                enableMenuObjects("players_tab");

                playerSelectOptionText.text = playerSelectedData[playerSelectedIndex].PlayerDisplayName;
                playerSelectOptionImage.sprite = playerSelectedData[playerSelectedIndex].PlayerPortrait;

                playerSelectOptionStatsText.text = // playerSelectedData[playerSelectedIndex].Accuracy2Pt.ToString("F0") + "\n"
                    playerSelectedData[playerSelectedIndex].Accuracy3Pt.ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Accuracy4Pt.ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Accuracy7Pt.ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Release.ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Range.ToString("F0") + " ft\n"
                    + playerSelectedData[playerSelectedIndex].calculateSpeedToPercent().ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].calculateJumpValueToPercent().ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Luck.ToString("F0");

                playerSelectedData[playerSelectedIndex].Level =
                    playerSelectedData[playerSelectedIndex].Experience / 3000;
                int nextlvl = (playerSelectedData[playerSelectedIndex].Level + 1) * 3000 - playerSelectedData[playerSelectedIndex].Experience;

                playerProgressionStatsText.text = playerSelectedData[playerSelectedIndex].Level.ToString("F0") + "\n"
                    + playerSelectedData[playerSelectedIndex].Experience.ToString("F0") + "\n"
                    + nextlvl.ToString("F0") + "\n";

                // player points avaiable for upgrade
                if (playerSelectedData[playerSelectedIndex].PointsAvailable > 0)
                {
                    playerProgressionUpdatePointsText.text = "+" + playerSelectedData[playerSelectedIndex].PointsAvailable.ToString();
                }
                else
                {
                    playerProgressionUpdatePointsText.text = "";
                }
                GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;

            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
        }

        // ============================  footer options activate - load scene/stats/quit/etc ==============================
        public void loadGame()
        {
            // tells character profile to load profile from LoadedData.instance
            GameOptions.gameModeHasBeenSelected = true;

            // update game options for game mode
            setGameOptions();

            // i create the string this way so that i can have a description of the level so i know what im opening
            string sceneName = GameOptions.levelSelected + "_" + levelSelectedData[levelSelectedIndex].LevelDescription;

            // if player not locked, friend not locked, mode contains 'free', mode not aracde mode
            if (modeSelectedData[modeSelectedIndex].ModeDisplayName.ToLower().Contains("free")
                || !modeSelectedData[modeSelectedIndex].ModeDisplayName.ToLower().Contains("arcade"))
            {
                // load player progression info
                PlayerData.instance.CurrentExperience = playerSelectedData[playerSelectedIndex].Experience;
                PlayerData.instance.CurrentLevel = playerSelectedData[playerSelectedIndex].Level;
                PlayerData.instance.UpdatePointsAvailable = playerSelectedData[playerSelectedIndex].PointsAvailable;
                PlayerData.instance.UpdatePointsUsed = playerSelectedData[playerSelectedIndex].PointsUsed;
            }
            SceneManager.LoadScene(sceneName);
        }

        public void loadMenu(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        // ============================  set game options ==============================
        // this is necessary for setting Game Rules on game manager
        private void setGameOptions()
        {
            GameOptions.characterId = playerSelectedData[playerSelectedIndex].PlayerId;
            GameOptions.characterDisplayName = playerSelectedData[playerSelectedIndex].PlayerDisplayName;

            // if Wizard of Boat selected, randomly choose which one to spawn
            if (playerSelectedData[playerSelectedIndex].PlayerDisplayName.ToLower().Contains("boat"))
            {
                GameOptions.characterObjectName = getRandomWizardOfBoat();
            }
            else
            {
                GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;
            }

            GameOptions.levelSelected = levelSelectedData[levelSelectedIndex].LevelObjectName;
            GameOptions.levelId = levelSelectedData[levelSelectedIndex].LevelId;
            GameOptions.levelDisplayName = levelSelectedData[levelSelectedIndex].LevelDisplayName;
            GameOptions.levelRequiresTimeOfDay = levelSelectedData[levelSelectedIndex].LevelRequiresTimeOfDay;

            GameOptions.gameModeSelectedId = modeSelectedData[modeSelectedIndex].ModeId;
            GameOptions.gameModeSelectedName = modeSelectedData[modeSelectedIndex].ModeDisplayName;

            GameOptions.gameModeRequiresCountDown = modeSelectedData[modeSelectedIndex].ModeRequiresCountDown;
            GameOptions.gameModeRequiresCounter = modeSelectedData[modeSelectedIndex].ModeRequiresCounter;

            GameOptions.gameModeRequiresShotMarkers3s = modeSelectedData[modeSelectedIndex].ModeRequiresShotMarkers3S;
            GameOptions.gameModeRequiresShotMarkers4s = modeSelectedData[modeSelectedIndex].ModeRequiresShotMarkers4S;

            GameOptions.gameModeThreePointContest = modeSelectedData[modeSelectedIndex].GameModeThreePointContest;
            GameOptions.gameModeFourPointContest = modeSelectedData[modeSelectedIndex].GameModeFourPointContest;
            GameOptions.gameModeSevenPointContest = modeSelectedData[modeSelectedIndex].GameModeSevenPointContest;
            GameOptions.gameModeAllPointContest = modeSelectedData[modeSelectedIndex].GameModeAllPointContest;

            // check if game mode requires timer that is not 120
            if (modeSelectedData[modeSelectedIndex].CustomTimer > 0)
            {
                GameOptions.customTimer = modeSelectedData[modeSelectedIndex].CustomTimer;
            }
            else
            {
                GameOptions.customTimer = 0;
            }

            GameOptions.gameModeRequiresMoneyBall = modeSelectedData[modeSelectedIndex].ModeRequiresMoneyBall;
            GameOptions.gameModeRequiresConsecutiveShot = modeSelectedData[modeSelectedIndex].ModeRequiresConsecutiveShots;

            GameOptions.cheerleaderDisplayName = friendSelectedData[friendSelectedIndex].CheerleaderDisplayName;
            GameOptions.cheerleaderId = friendSelectedData[friendSelectedIndex].CheerleaderId;
            GameOptions.cheerleaderObjectName = friendSelectedData[friendSelectedIndex].CheerleaderObjectName;

            //GameOptions.trafficEnabled = trafficEnabled;

            GameOptions.applicationVersion = Application.version;
            GameOptions.operatingSystemVersion = SystemInfo.operatingSystem;

            // send current selected options to game options for next load on start manager
            GameOptions.playerSelectedIndex = playerSelectedIndex;
            GameOptions.cheerleaderSelectedIndex = friendSelectedIndex;
            GameOptions.levelSelectedIndex = levelSelectedIndex;
            GameOptions.modeSelectedIndex = modeSelectedIndex;
            GameOptions.trafficEnabled = trafficEnabled;
            GameOptions.enemiesEnabled = enemiesEnabled;
            GameOptions.sniperEnabled = sniperEnabled;

            GameOptions.arcadeModeEnabled = modeSelectedData[modeSelectedIndex].ArcadeModeActive;
            GameOptions.EnemiesOnlyEnabled = modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled;

            GameOptions.levelRequiresWeather = levelSelectedData[levelSelectedIndex].LevelHasWeather;
            GameOptions.levelHasSevenPointers = levelSelectedData[levelSelectedIndex].LevelHasSevenPointers;

            GameOptions.difficultySelected = difficultySelected;
            if (difficultySelected == 2) { hardcoreEnabled = true; }
            GameOptions.hardcoreModeEnabled = hardcoreEnabled;

            GameOptions.obstaclesEnabled = obstaclesEnabled;
            GameOptions.battleRoyalEnabled = modeSelectedData[modeSelectedIndex].IsBattleRoyal;
            GameOptions.cageMatchEnabled = modeSelectedData[modeSelectedIndex].IsCageMatch;

            GameOptions.gameModeRequiresPlayerSurvive = modeSelectedData[modeSelectedIndex].GameModeRequiresPlayerSurvive;

            // if enemies only mode, enable enemies whether it was selected or not
            if (GameOptions.EnemiesOnlyEnabled || GameOptions.battleRoyalEnabled)
            {
                GameOptions.enemiesEnabled = true;
            }

            if (!levelSelectedData[levelSelectedIndex].LevelHasTraffic)
            {
                GameOptions.trafficEnabled = false;
            }

            GameOptions.customCamera = levelSelectedData[levelSelectedIndex].CustomCamera;

            GameOptions.characterObjectNames = new List<string>
        {
            playerSelectedData[playerSelectedIndex].PlayerObjectName,
            cpuPlayerSelectedData[cpu1SelectedIndex].PlayerObjectName,
            cpuPlayerSelectedData[cpu2SelectedIndex].PlayerObjectName,
            cpuPlayerSelectedData[cpu3SelectedIndex].PlayerObjectName,

        };
            //if (modeSelectedData[modeSelectedIndex].ModeId == 21)
            //{
            //    levelSelectedIndex = 15;
            //}

            // load hardcore mode highscores (for ui display) for game mode if hardcore mode enabled
            //Debug.Log("hardcore enabled : "+ GameOptions.hardcoreModeEnabled);
            PlayerData.instance.loadStatsFromDatabase();
        }

        // ============================  message display ==============================
        // used in this context to display if item is locked

        public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
            messageText.text = "";
        }

        // ============================  navigation functions ==============================

        public void changeSelectedNumPlayersUp()
        {
            // if default index (first in list), go to end of list
            if (GameOptions.numPlayers == 4)
            {
                GameOptions.numPlayers = 1;
            }
            else
            {
                GameOptions.numPlayers++;
            }
        }
        public void changeSelectedNumPlayersDown()
        {
            // if default index (first in list), go to end of list
            if (GameOptions.numPlayers == 1)
            {
                GameOptions.numPlayers = 4;
            }
            else
            {
                GameOptions.numPlayers--;
            }
        }

        public void changeSelectedPlayerUp()
        {
            // if default index (first in list), go to end of list
            if (playerSelectedIndex == 0)
            {
                playerSelectedIndex = playerSelectedData.Count - 1;
            }
            else
            {
                // if not first index, decrement
                playerSelectedIndex--;
            }
            // check for fighting modes + if player is fighter
            if (!playerSelectedData[playerSelectedIndex].IsFighter
                && (modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled
                || enemiesEnabled))
            {
                //Debug.Log("player not fighter : " + playerSelectedData[playerSelectedIndex].PlayerObjectName);
                changeSelectedPlayerUp();
            }
            // check for shooting modes + if player is fighter
            // check for shooting modes + if player is fighter
            if (!playerSelectedData[playerSelectedIndex].IsShooter
                && !modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled
                && !enemiesEnabled)
            {
                //Debug.Log("player not shooter : " + playerSelectedData[playerSelectedIndex].PlayerObjectName);
                changeSelectedPlayerUp();
            }
            GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;
        }
        public void changeSelectedPlayerDown()
        {
            // if default index (first in list
            if (playerSelectedIndex == playerSelectedData.Count - 1)
            {
                playerSelectedIndex = 0;
            }
            else
            {
                //if not first index, increment
                playerSelectedIndex++;
            }
            // check for fighting modes + if player is fighter
            if (!playerSelectedData[playerSelectedIndex].IsFighter
                && (modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled
                || enemiesEnabled))
            {
                //Debug.Log("player not fighter : " + playerSelectedData[playerSelectedIndex].PlayerObjectName);
                changeSelectedPlayerDown();
            }
            // check for shooting modes + if player is fighter
            if (!playerSelectedData[playerSelectedIndex].IsShooter
                && !modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled
                && !enemiesEnabled)
            {
                //Debug.Log("player not shooter : " + playerSelectedData[playerSelectedIndex].PlayerObjectName);
                changeSelectedPlayerDown();
            }
            GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;
        }

        public void changeSelectedfriendUp()
        {
            // if default index (first in list
            if (friendSelectedIndex == 0)
            {
                friendSelectedIndex = friendSelectedData.Count - 1;
            }
            else
            {
                //if not first index, increment
                friendSelectedIndex--;
            }
            GameOptions.cheerleaderObjectName = friendSelectedData[friendSelectedIndex].CheerleaderObjectName;
        }

        public void changeSelectedfriendDown()
        {
            // if default index (first in list
            if (friendSelectedIndex == friendSelectedData.Count - 1)
            {
                friendSelectedIndex = 0;
            }
            else
            {
                //if not first index, increment
                friendSelectedIndex++;
            }
            GameOptions.cheerleaderObjectName = friendSelectedData[friendSelectedIndex].CheerleaderObjectName;
        }

        public void changeSelectedLevelUp()
        {
            Debug.Log("change level");
            // if default index (first in list), go to end of list
            if (levelSelectedIndex == 0)
            {
                levelSelectedIndex = levelSelectedData.Count - 1;
            }
            else
            {
                // if not first index, decrement
                levelSelectedIndex--;
            }
            if (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            {
                changeSelectedLevelUp();
            }

            // if mode is shooting, level is not
            if (!modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled && !levelSelectedData[levelSelectedIndex].IsShootingLevel
                // mode has enemies, level isnt a fighting level
                || modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled && !levelSelectedData[levelSelectedIndex].IsFightingLevel
                // battle royal mode, level isnt battle royal level
                || modeSelectedData[modeSelectedIndex].IsBattleRoyal && !levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel
                // not battle royal mode, level is battle royal
                || !modeSelectedData[modeSelectedIndex].IsBattleRoyal && levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel)
            //// mode is cage match, level is not cage match
            //|| (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            ////mode is not cage match, level is cage match
            //|| (!modeSelectedData[modeSelectedIndex].IsCageMatch && levelSelectedData[levelSelectedIndex].IsCageMatchLevel))
            {
                changeSelectedLevelUp();
            }

            initializeLevelDisplay();
            initializeModeDisplay();
        }
        public void changeSelectedLevelDown()
        {
            // if default index (first in list
            if (levelSelectedIndex == levelSelectedData.Count - 1)
            {
                levelSelectedIndex = 0;
            }
            else
            {
                //if not first index, increment
                levelSelectedIndex++;
            }
            if (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            {
                changeSelectedLevelDown();
            }
            // if mode is shooting, level is not
            if (!modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled && !levelSelectedData[levelSelectedIndex].IsShootingLevel
                // mode has enemies, level isnt a fighting level
                || modeSelectedData[modeSelectedIndex].EnemiesOnlyEnabled && !levelSelectedData[levelSelectedIndex].IsFightingLevel
                // battle royal mode, level isnt battle royal level
                || modeSelectedData[modeSelectedIndex].IsBattleRoyal && !levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel
                // not battle royal mode, level is battle royal
                || !modeSelectedData[modeSelectedIndex].IsBattleRoyal && levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel)
            //// mode is cage match, level is not cage match
            //|| (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            ////mode is not cage match, level is cage match
            //|| (!modeSelectedData[modeSelectedIndex].IsCageMatch && levelSelectedData[levelSelectedIndex].IsCageMatchLevel))
            {
                changeSelectedLevelDown();
            }

            GameOptions.levelSelected = levelSelectedData[levelSelectedIndex].LevelObjectName;
            initializeLevelDisplay();
            initializeModeDisplay();

        }
        public void changeSelectedModeUp()
        {
            // if default index (first in list), go to end of list
            if (modeSelectedIndex == 0)
            {
                modeSelectedIndex = modeSelectedData.Count - 1;
            }
            else
            {
                // if not first index, decrement
                modeSelectedIndex--;
            }
            // mode is not battle royal, level is not
            if (modeSelectedData[modeSelectedIndex].IsBattleRoyal
                && !levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel)
            {
                changeSelectedLevelUp();
            }
            if (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            {
                changeSelectedLevelUp();
            }
            GameOptions.gameModeSelectedId = modeSelectedData[modeSelectedIndex].ModeId;
            GameOptions.gameModeSelectedName = modeSelectedData[modeSelectedIndex].ModeDisplayName;
            initializeModeDisplay();
            initializeLevelDisplay();
        }

        public void changeSelectedModeDown()
        {
            // if default index (first in list
            if (modeSelectedIndex == modeSelectedData.Count - 1)
            {
                modeSelectedIndex = 0;
            }
            else
            {
                //if not first index, increment
                modeSelectedIndex++;
            }
            if (modeSelectedData[modeSelectedIndex].IsBattleRoyal
                && !levelSelectedData[levelSelectedIndex].IsBattleRoyalLevel)
            {
                changeSelectedLevelDown();
            }
            if (modeSelectedData[modeSelectedIndex].IsCageMatch && !levelSelectedData[levelSelectedIndex].IsCageMatchLevel)
            {
                changeSelectedLevelUp();
            }
            GameOptions.gameModeSelectedId = modeSelectedData[modeSelectedIndex].ModeId;
            GameOptions.gameModeSelectedName = modeSelectedData[modeSelectedIndex].ModeDisplayName;
            initializeModeDisplay();
            initializeLevelDisplay();
        }
        private void initializeCpuPlayerDisplay()
        {
            if (StartMenuUiObjects.instance.column4.activeSelf)
            {
                setCpuPlayer1();
                setCpuPlayer2();
                setCpuPlayer3();
            }
        }
        public void setCpuPlayer(int cpuPlayerIndex)
        {
            StartMenuUiObjects.instance.column4_cpu1_image.sprite = cpuPlayerSelectedData[cpuPlayerIndex].PlayerPortrait;
            StartMenuUiObjects.instance.column4_cpu1_name_text.text = cpuPlayerSelectedData[cpuPlayerIndex].PlayerDisplayName;
            if (cpuPlayerSelectedData[cpu1SelectedIndex].PlayerId != 0)
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text =
                        cpuPlayerSelectedData[cpuPlayerIndex].Accuracy3Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Accuracy4Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Accuracy7Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Release.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Range.ToString("F0") + " ft\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].calculateSpeedToPercent().ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].calculateJumpValueToPercent().ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Luck.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Clutch.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpuPlayerIndex].Level.ToString("F0");
            }
        }
        public void setCpuPlayer1()
        {
            StartMenuUiObjects.instance.column4_cpu1_image.sprite = cpuPlayerSelectedData[cpu1SelectedIndex].PlayerPortrait;
            StartMenuUiObjects.instance.column4_cpu1_name_text.text = cpuPlayerSelectedData[cpu1SelectedIndex].PlayerDisplayName;
            if (cpuPlayerSelectedData[cpu1SelectedIndex].PlayerId != 0)
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text =
                        cpuPlayerSelectedData[cpu1SelectedIndex].Accuracy3Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Accuracy4Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Accuracy7Pt.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Release.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Range.ToString("F0") + " ft\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].calculateSpeedToPercent().ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].calculateJumpValueToPercent().ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Luck.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Clutch.ToString("F0") + "\n"
                        + cpuPlayerSelectedData[cpu1SelectedIndex].Level.ToString("F0");
            }
            else
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text = "";
            }
        }
        public void setCpuPlayer2()
        {
            StartMenuUiObjects.instance.column4_cpu2_image.sprite = cpuPlayerSelectedData[cpu2SelectedIndex].PlayerPortrait;
            StartMenuUiObjects.instance.column4_cpu2_name_text.text = cpuPlayerSelectedData[cpu2SelectedIndex].PlayerDisplayName;
            if (cpuPlayerSelectedData[cpu2SelectedIndex].PlayerId != 0)
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text =
                    cpuPlayerSelectedData[cpu2SelectedIndex].Accuracy3Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Accuracy4Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Accuracy7Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Release.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Range.ToString("F0") + " ft\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].calculateSpeedToPercent().ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].calculateJumpValueToPercent().ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Luck.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Clutch.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu2SelectedIndex].Level.ToString("F0");
            }
            else
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text = "";
            }
        }
        public void setCpuPlayer3()
        {
            StartMenuUiObjects.instance.column4_cpu3_image.sprite = cpuPlayerSelectedData[cpu3SelectedIndex].PlayerPortrait;
            StartMenuUiObjects.instance.column4_cpu3_name_text.text = cpuPlayerSelectedData[cpu3SelectedIndex].PlayerDisplayName;
            if (cpuPlayerSelectedData[cpu3SelectedIndex].PlayerId != 0)
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text =
                    cpuPlayerSelectedData[cpu3SelectedIndex].Accuracy3Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Accuracy4Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Accuracy7Pt.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Release.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Range.ToString("F0") + " ft\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].calculateSpeedToPercent().ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].calculateJumpValueToPercent().ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Luck.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Clutch.ToString("F0") + "\n"
                    + cpuPlayerSelectedData[cpu3SelectedIndex].Level.ToString("F0");
            }
            else
            {
                StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text.text = "";
            }
        }
        // ============================  public var references  ==============================
        // dont think some of these are used, keep an eye on this on refactor
        // button names
        public static string FriendSelectOptionButtonName => friendSelectOptionButtonName;
        public static string SniperSelectOptionName => sniperSelectOptionName;
        public static string ObstacleSelectOptionName => obstacleSelectOptionName;
    }
}
