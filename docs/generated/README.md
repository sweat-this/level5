# Generated Documentation Data

This directory is reserved for deterministic, reviewable data exported from Unity-authored binary prefab and scene configuration.

## Level 5 authored game data

`Level5DocumentationExporter` writes:

```text
docs/generated/level5-authored-game-data.json
```

The generated JSON is intended to be committed when refreshed so Codex and repository audits can review authored values without reverse-engineering Unity binary assets.

### Unity menu

In the Unity editor:

```text
Level 5 > Documentation > Export Authored Game Data
```

The exporter refuses to run while entering/running Play Mode or while an open scene has unsaved changes. It temporarily opens authored scenes to inspect scene-level configuration and restores the original scene setup afterward.

### Batch mode

The exporter also exposes a static command-line entry point:

```powershell
Unity.exe `
  -batchmode `
  -quit `
  -projectPath <path-to-level5> `
  -executeMethod Level5DocumentationExporter.ExportFromCommandLine `
  -logFile -
```

Use the project's supported Unity editor version.

## Exported domains

Schema version 1 includes:

- current character-selection prefabs;
- fallback/default character profiles;
- level-selection prefabs;
- mode-selection prefabs;
- friend/cheerleader selection prefabs;
- enemy prefabs and health components;
- bodyguard prefabs and health components;
- NavMesh vehicle prefabs;
- non-NavMesh special vehicle prefabs;
- any prefab under `Assets/Resources/Prefabs` carrying a `RacingVehicleProfile`;
- scene `EnemySpawner` serialized configuration;
- scene `TrafficManager` serialized configuration;
- scene `RacingVehicleProfile` configuration.

Each exported component contains Unity serialized property paths, property types, and normalized string values. Object references are represented by asset paths when persistent, or by `scene:<hierarchy path>` for scene objects.

## Determinism and provenance

The export intentionally omits a generation timestamp. Collections, scene components, component fields, and findings are sorted deterministically.

Every record retains its source prefab/scene path. The generated file is evidence of authored implementation state, not automatic narrative canon.

## Refresh policy

Regenerate the file whenever changes alter any exported authored data. Review the JSON diff like source code.

Do not manually edit the generated JSON to make documentation look cleaner. Fix the authored Unity data or the exporter, then regenerate.
