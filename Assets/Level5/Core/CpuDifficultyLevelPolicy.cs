using System;

namespace Level5.Core
{
    /// <summary>
    /// The Hardcore CPU level bump (#71), extracted unchanged from the old
    /// <c>CharacterProfile.intializeCpuShooterStats</c> so it is a pure, deterministic calculation
    /// instead of something that reached for <c>GameLevelManager.instance</c> and mutated
    /// <c>CharacterProfile.Level</c> in place.
    ///
    /// <paramref name="baseCpuLevel"/> must be the CPU's own pre-Hardcore level, not a previously
    /// boosted one - calling this twice with the same base and primary inputs must return the same
    /// answer, never a compounding one.
    /// </summary>
    public static class CpuDifficultyLevelPolicy
    {
        public static int Resolve(int baseCpuLevel, int primaryHumanLevel, bool hardcore)
        {
            if (!hardcore)
            {
                return baseCpuLevel;
            }

            return Math.Max(baseCpuLevel, primaryHumanLevel) + 10;
        }
    }
}
