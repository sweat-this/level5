namespace Level5.Core
{
    /// <summary>
    /// Live ground-height value for a basketball's no-active-Terrain drop-shadow fallback.
    ///
    /// AUD-010 Phase 1c: <see cref="BasketBall"/> reads this instead of
    /// <c>GameLevelManager.instance.TerrainHeight</c> directly. <c>GameLevelManager</c> implements it
    /// over its existing <c>terrainHeight</c> field, which changes after spawn time (its own
    /// <c>Start()</c> updates it from the primary participant's actual Y) - so this must be read live
    /// at the point of use, never cached at bind time.
    /// </summary>
    public interface IGroundHeightProvider
    {
        float GroundHeight { get; }
    }
}
