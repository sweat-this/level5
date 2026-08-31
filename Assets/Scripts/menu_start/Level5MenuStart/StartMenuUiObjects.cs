using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartMenuUiObjects : MonoBehaviour
{
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

    [SerializeField] public Button column2_options_tab_traffic_select_button;
    [SerializeField] public Button column2_options_tab_hardcore_select_button;
    [SerializeField] public Button column2_options_tab_enemy_select_button;
    [SerializeField] public Button column2_options_tab_sniper_select_button;
    [SerializeField] public Button column2_options_tab_obstacles_select_button;
    [SerializeField] public Button column2_options_tab_difficulty_select_button;

    [SerializeField] public GameObject column3;
    [SerializeField] public GameObject column3_friend_selected_stats_category;
    [SerializeField] public GameObject column3_friend_selected_stats_numbers;
    [SerializeField] public GameObject column3_level_selected_info;
    [SerializeField] public GameObject column3_player_stats;
    //[SerializeField] public GameObject column3_player_progression;
    //[SerializeField] public GameObject column3_player_selected_stats_category;

    [SerializeField] public GameObject column4;
    [SerializeField] public Image column4_cpu1_image;
    [SerializeField] public GameObject column4_cpu1_button;
    [SerializeField] public Image column4_cpu2_image;
    [SerializeField] public GameObject column4_cpu2_button;
    [SerializeField] public Image column4_cpu3_image;
    [SerializeField] public GameObject column4_cpu3_button;

    /// <summary>
    /// AUD-092 Phase 6A/6B: the permanent TMP counterpart of the 27 legacy Text fields this class used
    /// to carry that runtime code actually writes <c>.text</c> into (Phase 6A). The other 14 legacy Text
    /// fields this class used to carry were pure static/unbound labels nothing ever wrote <c>.text</c>
    /// into; Phase 6B converted their backing Text components to TextMeshProUGUI in place and removed
    /// the fields entirely rather than adding them here - static presentation needs no runtime view
    /// binding. Composed here (not a second singleton) so <see cref="instance"/> stays the one root the
    /// rest of the Start menu resolves UI from - see <see cref="StartMenuTextUiObjects"/>'s own doc
    /// comment.
    /// </summary>
    [SerializeField] private StartMenuTextUiObjects textUi;
    public StartMenuTextUiObjects TextUi => textUi;

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
