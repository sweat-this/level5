using System;
using UnityEngine;

[Serializable]
public class LevelPreset
{
    public int LevelId { get; private set; }
    public string LevelDisplayName { get; private set; }
    public string LevelInfo { get; private set; }
    public string LevelObjectName { get; private set; }
    public string LevelDescription { get; private set; }
    public bool LevelRequiresTimeOfDay { get; private set; }
    public bool LevelHasTraffic { get; private set; }
    public bool LevelHasWeather { get; private set; }
    public bool LevelHasSevenPointers { get; private set; }
    public bool IsFightingLevel { get; private set; }
    public bool IsShootingLevel { get; private set; }
    public bool IsBattleRoyalLevel { get; private set; }
    public bool IsCageMatchLevel { get; private set; }
    public bool CustomCamera { get; private set; }
    public bool IsSelectable { get; private set; }
    public bool IsLocked { get; private set; }
    public GameObject CpuPlayer { get; private set; }

    public static LevelPreset FromLevelSelected(LevelSelected level)
    {
        if (level == null)
        {
            return null;
        }

        return new LevelPreset
        {
            LevelId = level.LevelId,
            LevelDisplayName = level.LevelDisplayName,
            LevelInfo = level.LevelInfo,
            LevelObjectName = level.LevelObjectName,
            LevelDescription = level.LevelDescription,
            LevelRequiresTimeOfDay = level.LevelRequiresTimeOfDay,
            LevelHasTraffic = level.LevelHasTraffic,
            LevelHasWeather = level.LevelHasWeather,
            LevelHasSevenPointers = level.LevelHasSevenPointers,
            IsFightingLevel = level.IsFightingLevel,
            IsShootingLevel = level.IsShootingLevel,
            IsBattleRoyalLevel = level.IsBattleRoyalLevel,
            IsCageMatchLevel = level.IsCageMatchLevel,
            CustomCamera = level.CustomCamera,
            IsSelectable = level.IsSelectable,
            IsLocked = level.IsLocked,
            CpuPlayer = level.CpuPlayer
        };
    }
}
