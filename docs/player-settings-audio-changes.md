# Player settings and audio changed files

Exact implementation manifest against dev e594eaa07190cb766adce9ec47a28b886aad6523: **235 files**. Existing .meta files are unchanged. Only new assets/scripts/tests have new metadata. Audio content, packages, GameOptions, match/progression/account stores and project settings are unchanged.

| File | Rationale |
| --- | --- |
| `Assets/Level5/Core/PlayerSettings.cs` | Immutable normalized application-wide audio preferences and full-gain defaults. |
| `Assets/Level5/Core/PlayerSettings.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Resources/Audio.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Resources/Audio/PlayerAudio.mixer` | Minimal Master > Music/SFX hierarchy, three exposed gain parameters, 0 dB snapshot defaults. |
| `Assets/Resources/Audio/PlayerAudio.mixer.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Resources/Prefabs/SFX.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. Add serialized effects source cloned from the original acoustic properties; keep original source as Music. |
| `Assets/Resources/Prefabs/Weather Sytem.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/auto_players/auto_player_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketballAuto.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball_goal.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball_goal_circlek.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball_goal_slab.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball_goal_snow.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/basketball/basketball_goal_sudan.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/bodyguards/bodyguard_ian.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/Cameras_level11_forest.prefab` | Assign classified mixer output to 12 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/Cameras_with_weather.prefab` | Assign classified mixer output to 12 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/camera_perspective_on_goal 1.prefab` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/weather_cameras/weather_system_orthographic_1.prefab` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/weather_cameras/weather_system_orthographic_2.prefab` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/camera/weather_cameras/weather_system_perspective_1.prefab` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_amber.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_dallas.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_dan_russell.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_donut.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_jessica.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_old_real.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_probe_droid.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_toups.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_worker_parasite.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cheerleaders/cheerleader_wyld_stallyns.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_ak47.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_christy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_dallas.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_drblood_white.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_executioner.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_flash.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_ian.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_jcleger.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_johnny_dracula.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_kamille.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_oldreal.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_pony.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_rad_tony.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_raymond.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_rob.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_thom.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_wizard.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_wob1.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_woody.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players/cpu_player_zilla_baby.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/cpu_players_defense/cpu_player_defense_oldreal.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/npc_auto/npc_staff_auto.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/npc_auto/npc_werewolf_auto.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_ak47.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_ashley.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_baby.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_christy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_dallas.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_danrussell.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_dirtyike.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_donut.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_drblood_ghost.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_drblood_test.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_drblood_white.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_evelyn.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_executioner.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_flash.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_gremlin.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_harrycharles.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_hector.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_ian.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_jcleger.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_jimmy_ford.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_johnny_dracula.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_johnnyb.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_kamille.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_marion.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_meggadeth.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_miked.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_nickjagger.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_oldreal.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_pony.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_powerman.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_rad_tony.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_raymond.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_richardcharles.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_ricky.prefab` | Assign classified mixer output to 2 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_rob.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_robot.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_sam.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_sarah.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_thom.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_wizard.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_wob1.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_wob2.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_woody.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_yeti.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/characters/players/player_zilla_baby.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/critical/OptionManager.prefab` | Serialize labeled percentage sliders into three existing empty rows, activate them, preserve navigation and prevent footer overlap. |
| `Assets/Resources/Prefabs/critical/SFX.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. Add serialized effects source cloned from the original acoustic properties; keep original source as Music. |
| `Assets/Resources/Prefabs/critical/weather_system.prefab` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_alien_blue.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_alien_red.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_alien_white.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_drblood_white.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_evelyn.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_executioner.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_ian.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_james.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_ninja_black.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_ninja_red.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_ninja_white.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_nza.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_oldreal.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_pony.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_powerman.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_robot_hatchet.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_thom.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_wizard.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_woody.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/enemies/enemy_yeti.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/ghost.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_00_none.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_01_jessica.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_02_old_real.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_03_toups.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_04_dallas.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_05_parasite.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_06_amber.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_07_bill_ted.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_08_probe_droid.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_09_donut.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_10_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_11_dan_russell.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_default_objects/cheerleader_12_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_00_none.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_01_jessica.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_02_old_real.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_03_toups.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_04_dallas.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_05_parasite.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_06_amber.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_07_bill_ted.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_08_probe_droid.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_09_donut.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_10_drblood.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_11_dan_russell.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/menu_start/cheerleader_selected_object/cheerleader_12_werewolf.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/player_christy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_automatic_bullet.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_automatic_bullet_enemy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_bullet_enemy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_bullet_instantkill_enemy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_bullet_player.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_cigarette.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_dart_enemy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_flamethrower.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_impact_explosion.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_impact_ground.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_impact_player.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_impact_rabbit.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_laser_enemy.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_laser_enemy_disintegrate.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_laser_player.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_molotov.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_rabbit.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/projectile/projectile_rocket.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/sniper/Sniper.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/01_bandit.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/02_cadillac.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/03_cop_car.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/04_radtony_horse.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/05_lamborghini.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/06_lincoln.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/07_monte_carlo.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/08_robot_squad.prefab` | Assign classified mixer output to 21 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/09_semi_truck.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/10_skateboard_sam.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/11_speeder_bike.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/12_suburban.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/13_tank.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/14_tank_gold.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/15_three_wheeler_wob1.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/16_three_wheeler_wob2.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/17_van.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/18_winnebago.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/19_drblood_jetpack.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-navmesh/20_nza_squad.prefab` | Assign classified mixer output to 21 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/vehicles-no-navmesh/speedboat.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Resources/Prefabs/weather/Weather Sytem.prefab` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_03_snow.unity` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_10_time_jail.unity` | Assign classified mixer output to 61 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_11_forest.unity` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_14_dome.unity` | Assign classified mixer output to 1 original source(s); preserve all other source properties. Mirror the existing 0.893 volume override to effects. |
| `Assets/Scenes/level_15_cocaine_island.unity` | Assign classified mixer output to 26 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_16_boner_mountain.unity` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_17_rumble_pit.unity` | Assign classified mixer output to 6 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_18_aveb2.unity` | Assign classified mixer output to 5 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_21_shore.unity` | Assign classified mixer output to 5 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_22_rumble_pit_shooting.unity` | Assign classified mixer output to 1 original source(s); preserve all other source properties. |
| `Assets/Scenes/level_24_aveb_cemetary.unity` | Assign classified mixer output to 3 original source(s); preserve all other source properties. |
| `Assets/Scripts/SFX manager/Level5.Audio.asmdef` | Declare existing Core/Utility/Audio/JSON or Collections references needed by the touched owner/tests; no new package. |
| `Assets/Scripts/SFX manager/PlayerSettingsRuntime.cs` | Independent startup load, one Current authority, central mixer application and lifecycle save retries. |
| `Assets/Scripts/SFX manager/PlayerSettingsRuntime.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Scripts/SFX manager/SFXBB.cs` | Explicit independent playlist/effects sources, preserved public playback API, pause-safe playlist state. |
| `Assets/Scripts/Utility/Level5Utility/Level5.Utility.asmdef` | Declare existing Core/Utility/Audio/JSON or Collections references needed by the touched owner/tests; no new package. |
| `Assets/Scripts/Utility/Level5Utility/PlayerSettingsStore.cs` | Required-field JSON with AtomicFile primary/backup recovery and observable write failure. |
| `Assets/Scripts/Utility/Level5Utility/PlayerSettingsStore.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Scripts/menu_options/OptionsManager.cs` | Bind immediate apply/save callbacks with symmetric lifecycle and guarded initialization. |
| `Assets/Scripts/menu_options/OptionsUiObjects.cs` | Own/validate serialized sliders and labels; refresh values without notifying listeners. |
| `Assets/Sprites/levels/time_jail/prisoners.prefab` | Assign classified mixer output to 10 original source(s); preserve all other source properties. |
| `Assets/Tests/Editor/PlayerAudioCompositionTests.cs` | 11 cases covering gain conversion, hierarchy, exposed parameters, inventory routes/authored gains and serialized Options/SFX contracts. |
| `Assets/Tests/Editor/PlayerAudioCompositionTests.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Tests/Editor/PlayerSettingsPersistenceTests.cs` | 20 focused persistence cases including defaults, recovery, malformed values, both-invalid save and failed writes. |
| `Assets/Tests/Editor/PlayerSettingsPersistenceTests.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `Assets/Tests/PlayMode/Level5.PlayModeTests.asmdef` | Declare existing Core/Utility/Audio/JSON or Collections references needed by the touched owner/tests; no new package. |
| `Assets/Tests/PlayMode/PlayerAudioSettingsPlayModeTests.cs` | 4 graphics cases for actual Options input/lifecycle/layout, independent playback/pause, real scene routing, isolated restoration and capability-gated DSP acceptance. |
| `Assets/Tests/PlayMode/PlayerAudioSettingsPlayModeTests.cs.meta` | Required Unity metadata for a new asset, folder, script or test; preserve existing GUIDs. |
| `docs/README.md` | Link settings/audio audit, source inventory and file manifest. |
| `docs/persistence-boundaries.md` | Document application-wide settings independent of local profiles, Backend and progression. |
| `docs/player-audio-source-inventory.md` | List all 399 covered sources and four deferred test/demo sources with exact path, category and authored gain. |
| `docs/player-settings-audio-audit.md` | Current-state audit, risks, two pre-implementation reviews, diagrams, schema, compatibility and owner waiver. |
| `docs/player-settings-audio-changes.md` | This complete changed-file manifest and per-path rationale. |
| `docs/player-settings-audio-validation.md` | Exact commands/cases/totals, process exits, repaired failures, limitations and issue acceptance. |
