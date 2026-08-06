# Legacy (not compiled)

Unity ignores any folder whose name ends in `~`, so nothing here is imported as an asset or
compiled into `Assembly-CSharp`. The files stay in git for reference.

## Contents

- `StartManager_original.cs` - the pre-rewrite start menu manager, superseded by
  `Assets/Scripts/menu_start/StartManager.cs`. It had no code references and no scene or prefab
  referenced its GUID, but it was still compiling into player builds and was reachable by any
  `GameObject.Find`/`GetComponent` lookup (audit AUD-033).

To bring a file back, move it out of this folder and let Unity regenerate its `.meta`.
