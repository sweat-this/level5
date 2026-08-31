using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// AUD-092 Phase 6A: the permanent runtime dynamic-text view for the Start menu
/// (<c>level_00_start</c>) - the TextMeshProUGUI counterpart of the subset of
/// <see cref="StartMenuUiObjects"/>' legacy <c>Text</c> fields that <c>StartManager</c>,
/// <c>PlayerSelectView</c> and <c>CpuSlotBinding</c> actually write <c>.text</c> into at runtime (27
/// of StartMenuUiObjects' 41 legacy Text fields - the remaining 14 are static labels or unbound and
/// stay legacy Text for Phase 6B).
///
/// Composed under <see cref="StartMenuUiObjects.TextUi"/> rather than a second singleton, so runtime
/// code has exactly one root (<see cref="StartMenuUiObjects.instance"/>) to resolve Start menu UI
/// from. The private serialized field names mirror StartMenuUiObjects' own legacy field names 1:1,
/// preserving the authoritative field-to-GameObject mapping the AUD-092 Phase 6A migration captured
/// from the production scene's prefab-instance property modifications before converting each Text in
/// place - only the public accessor names are shortened/concern-grouped, since those are new API
/// surface with no serialized identity to preserve.
/// </summary>
public class StartMenuTextUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_Text header_username;
    [SerializeField] private TMP_Text header_version;
    [SerializeField] private TMP_Text header_latestVersion;
    [SerializeField] private TMP_Text column1_subgroup_column2_num_players_selected_name_text;
    [SerializeField] private TMP_Text column1_subgroup_column2_player_select_name_text;
    [SerializeField] private TMP_Text column1_subgroup_column2_friend_selected_name_text;
    [SerializeField] private TMP_Text column1_subgroup_column2_level_selected_name_text;
    [SerializeField] private TMP_Text column1_subgroup_column2_mode_selected_name_text;
    [SerializeField] private TMP_Text column2_level_tab_level_selected_name;
    [SerializeField] private TMP_Text column2_level_tab_level_selected_info;
    [SerializeField] private TMP_Text column2_mode_tab_mode_selected_name;
    [SerializeField] private TMP_Text column2_mode_tab_mode_selected_description;
    [SerializeField] private TMP_Text column2_options_tab_traffic_select_option_text;
    [SerializeField] private TMP_Text column2_options_tab_hardcore_select_option_text;
    [SerializeField] private TMP_Text column2_options_tab_enemy_select_option_text;
    [SerializeField] private TMP_Text column2_options_tab_sniper_select_option_text;
    [SerializeField] private TMP_Text column2_options_tab_difficulty_select_option_text;
    [SerializeField] private TMP_Text column2_options_tab_difficulty_select_description_text;
    [SerializeField] private TMP_Text column2_options_tab_obstacle_select_option_text;
    [SerializeField] private TMP_Text column3_player_selected_stats_numbers_text;
    [SerializeField] private TMP_Text column3_player_selected_progression_stats_text;
    [SerializeField] private TMP_Text column3_player_selected_progression_update_points_text;
    [SerializeField] private TMP_Text column3_friend_selected_stats_numbers_text;
    [SerializeField] private TMP_Text column4_cpu_selected_stats_numbers_text;
    [SerializeField] private TMP_Text column4_cpu1_name_text;
    [SerializeField] private TMP_Text column4_cpu2_name_text;
    [SerializeField] private TMP_Text column4_cpu3_name_text;

    public TMP_Text HeaderUsername => header_username;
    public TMP_Text HeaderVersion => header_version;
    public TMP_Text HeaderLatestVersion => header_latestVersion;
    public TMP_Text NumPlayersSelectedName => column1_subgroup_column2_num_players_selected_name_text;
    public TMP_Text PlayerSelectedName => column1_subgroup_column2_player_select_name_text;
    public TMP_Text FriendSelectedName => column1_subgroup_column2_friend_selected_name_text;
    public TMP_Text LevelSelectedNameSummary => column1_subgroup_column2_level_selected_name_text;
    public TMP_Text LevelSelectedNameDetail => column2_level_tab_level_selected_name;
    public TMP_Text LevelSelectedInfo => column2_level_tab_level_selected_info;
    public TMP_Text ModeSelectedNameSummary => column1_subgroup_column2_mode_selected_name_text;
    public TMP_Text ModeSelectedNameDetail => column2_mode_tab_mode_selected_name;
    public TMP_Text ModeSelectedDescription => column2_mode_tab_mode_selected_description;
    public TMP_Text TrafficOption => column2_options_tab_traffic_select_option_text;
    public TMP_Text HardcoreOption => column2_options_tab_hardcore_select_option_text;
    public TMP_Text EnemyOption => column2_options_tab_enemy_select_option_text;
    public TMP_Text SniperOption => column2_options_tab_sniper_select_option_text;
    public TMP_Text DifficultyOption => column2_options_tab_difficulty_select_option_text;
    public TMP_Text DifficultyDescription => column2_options_tab_difficulty_select_description_text;
    public TMP_Text ObstacleOption => column2_options_tab_obstacle_select_option_text;
    public TMP_Text PlayerStatsNumbers => column3_player_selected_stats_numbers_text;
    public TMP_Text PlayerProgressionStats => column3_player_selected_progression_stats_text;
    public TMP_Text PlayerProgressionUpdatePoints => column3_player_selected_progression_update_points_text;
    public TMP_Text FriendStatsNumbers => column3_friend_selected_stats_numbers_text;
    public TMP_Text FocusedCpuStatsNumbers => column4_cpu_selected_stats_numbers_text;
    public TMP_Text Cpu1Name => column4_cpu1_name_text;
    public TMP_Text Cpu2Name => column4_cpu2_name_text;
    public TMP_Text Cpu3Name => column4_cpu3_name_text;

    /// <summary>
    /// True once every one of the 27 serialized TMP_Text fields above resolves. Reflection-driven
    /// (rather than 27 hand-maintained <c>if (x == null) missing.Add(...)</c> lines) so this stays
    /// correct automatically as fields are added/renamed here, instead of needing a matching manual
    /// edit to this method every time.
    /// </summary>
    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        foreach (FieldInfo field in typeof(StartMenuTextUiObjects).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.FieldType != typeof(TMP_Text))
            {
                continue;
            }

            if ((TMP_Text)field.GetValue(this) == null)
            {
                missing.Add("StartMenuTextUiObjects." + field.Name);
            }
        }

        return missing.Count == before;
    }
}
