using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartMenuUiObjects : MonoBehaviour
{
    [SerializeField] public Text header_username;
    [SerializeField] public Text header_version;
    [SerializeField] public Text header_latestVersion;

    [SerializeField] public GameObject column1_subgroup_column1_num_players_select;
    [SerializeField] public GameObject column1_subgroup_column1_player_select;
    [SerializeField] public GameObject column1_subgroup_column1_cpu_select;
    [SerializeField] public GameObject column1_subgroup_column1_friend_select;
    [SerializeField] public GameObject column1_subgroup_column1_level_select;
    [SerializeField] public GameObject column1_subgroup_column1_mode_select;
    [SerializeField] public GameObject column1_subgroup_column1_options_select;

    [SerializeField] public Button column1_subgroup_column2_num_players_selected_name_button;
    [SerializeField] public Button column1_subgroup_column2_player_select_name_button;
    [SerializeField] public Button column1_subgroup_column2_cpu_selected_name_button;
    [SerializeField] public Button column1_subgroup_column2_friend_selected_name_button;
    [SerializeField] public Button column1_subgroup_column2_level_selected_name_button;
    [SerializeField] public Button column1_subgroup_column2_mode_selected_name_button;
    [SerializeField] public Button column1_subgroup_column2_options_selected_name_button;

    [SerializeField] public GameObject column1_subgroup_column2;
    [SerializeField] public Text column1_subgroup_column2_num_players_selected_name_text;
    [SerializeField] public Text column1_subgroup_column2_player_select_name_text;
    [SerializeField] public Text column1_subgroup_column2_cpu_selected_name_text;
    [SerializeField] public Text column1_subgroup_column2_friend_selected_name_text;
    [SerializeField] public Text column1_subgroup_column2_level_selected_name_text;
    [SerializeField] public Text column1_subgroup_column2_mode_selected_name_text;
    [SerializeField] public Text column1_subgroup_column2_options_selected_name_text;

    [SerializeField] public GameObject column2;
    [SerializeField] public GameObject column2_players_tab;
    [SerializeField] public GameObject column2_players_tab_lock;
    [SerializeField] public GameObject column2_cpu_tab;
    [SerializeField] public GameObject column2_friend_tab;
    [SerializeField] public GameObject column2_level_tab;
    [SerializeField] public GameObject column2_mode_tab;
    [SerializeField] public GameObject column2_options_tab;

    [SerializeField] public Image column2_players_tab_player_selected_image;
    [SerializeField] public Image column2_friend_tab_friend_selected_image;
    [SerializeField] public Image column2_level_tab_level_selected_image;
    [SerializeField] public Text column2_level_tab_level_selected_name;
    [SerializeField] public Text column2_level_tab_level_selected_info;
    [SerializeField] public Text column2_mode_tab_mode_selected_name;
    [SerializeField] public Text column2_mode_tab_mode_selected_description;

    [SerializeField] public Button column2_options_tab_traffic_select_button;
    [SerializeField] public Button column2_options_tab_hardcore_select_button;
    [SerializeField] public Button column2_options_tab_enemy_select_button;
    [SerializeField] public Button column2_options_tab_sniper_select_button;
    [SerializeField] public Button column2_options_tab_obstacles_select_button;
    [SerializeField] public Button column2_options_tab_difficulty_select_button;

    [SerializeField] public Text column2_options_tab_traffic_select_text;
    [SerializeField] public Text column2_options_tab_traffic_select_option_text;   
    [SerializeField] public Text column2_options_tab_hardcore_select_text;
    [SerializeField] public Text column2_options_tab_hardcore_select_option_text;
    [SerializeField] public Text column2_options_tab_enemy_select_text;
    [SerializeField] public Text column2_options_tab_enemy_select_option_text;
    [SerializeField] public Text column2_options_tab_sniper_select_text;
    [SerializeField] public Text column2_options_tab_sniper_select_option_text;
    [SerializeField] public Text column2_options_tab_obstacles_select_text;
    [SerializeField] public Text column2_options_tab_obstacle_select_option_text;
    [SerializeField] public Text column2_options_tab_difficulty_select_text;
    [SerializeField] public Text column2_options_tab_difficulty_select_option_text;
    [SerializeField] public Text column2_options_tab_difficulty_select_description_text;

    [SerializeField] public GameObject column3;
    [SerializeField] public GameObject column3_friend_selected_stats_category;
    [SerializeField] public GameObject column3_friend_selected_stats_numbers;
    [SerializeField] public GameObject column3_level_selected_info;
    [SerializeField] public GameObject column3_player_stats;
    //[SerializeField] public GameObject column3_player_progression;
    //[SerializeField] public GameObject column3_player_selected_stats_category;
    [SerializeField] public Text column3_player_selected_stats_category_text;
    [SerializeField] public Text column3_player_selected_stats_numbers_text;
    [SerializeField] public Text column3_player_selected_progression_text;
    [SerializeField] public Text column3_player_selected_progression_stats_text;
    [SerializeField] public Text column3_friend_selected_stats_category_text;
    [SerializeField] public Text column3_friend_selected_stats_numbers_text;
    [SerializeField] public Text column3_player_selected_progression_update_points_text;
    [SerializeField] public Text column3_level_selected_name_text;
    [SerializeField] public Text column3_level_selected_description_text;

    [SerializeField] public GameObject column4;
    [SerializeField] public Text column4_cpu1_name_text;
    [SerializeField] public Image column4_cpu1_image;
    [SerializeField] public GameObject column4_cpu1_button;
    [SerializeField] public Text column4_cpu2_name_text;
    [SerializeField] public Image column4_cpu2_image;
    [SerializeField] public GameObject column4_cpu2_button;
    [SerializeField] public Text column4_cpu3_name_text;
    [SerializeField] public Image column4_cpu3_image;
    [SerializeField] public GameObject column4_cpu3_button;
    [SerializeField] public Text column4_cpu_selected_stats_numbers_text;
    [SerializeField] public Text column4_cpu_selected_stats_category_text;

    //footer
    public const string startButtonName = "press_start";
    public const string statsMenuButtonName = "stats_menu";
    public const string quitButtonName = "quit_game";
    public const string optionsMenuButtonName = "options_menu";
    public const string creditsMenuButtonName = "credits_menu";
    public const string updateMenuButtonName = "update_menu";
    public const string accountMenuButtonName = "account_menu";
    public const string updatePointsAvailable = "update_points_available";

    public static StartMenuUiObjects instance;

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

    private void Awake()
    {
        instance = this;
    }
}
