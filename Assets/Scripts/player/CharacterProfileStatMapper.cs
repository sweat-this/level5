public static class CharacterProfileStatMapper
{
    public static void Apply(CharacterProfile profile, RuntimeCharacterStats runtimeStats)
    {
        if (profile == null || runtimeStats == null || runtimeStats.stats == null)
        {
            return;
        }

        CharacterStats stats = runtimeStats.stats;
        profile.PlayerId = runtimeStats.legacyPlayerId;
        profile.PlayerDisplayName = runtimeStats.displayName;
        profile.Accuracy2Pt = stats.accuracy2Pt;
        profile.Accuracy3Pt = stats.accuracy3Pt;
        profile.Accuracy4Pt = stats.accuracy4Pt;
        profile.Accuracy7Pt = stats.accuracy7Pt;
        profile.JumpForce = stats.jumpForce;
        profile.Speed = stats.speed;
        profile.RunSpeed = stats.runSpeed;
        profile.RunSpeedHasBall = stats.runSpeedHasBall;
        profile.Range = stats.range;
        profile.Release = stats.release;
        profile.Luck = stats.luck;
        profile.ShootAngle = stats.shootAngle;
        profile.Experience = runtimeStats.experience;
        profile.Level = runtimeStats.level;
        profile.PointsUsed = runtimeStats.pointsSpent;
        profile.IsLocked = !runtimeStats.unlocked;
    }
}
