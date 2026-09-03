namespace Level5.Core.Match
{
    /// <summary>
    /// Live mutable session state: whether the money ball is currently active.
    ///
    /// AUD-010 Phase 1c: <see cref="BasketballShotPipeline"/> reads this instead of
    /// <c>GameRules.instance.MoneyBallEnabled</c> directly. <c>GameRules</c> implements it over its
    /// existing <c>MoneyBallEnabled</c> property, which stays mutable session state - a player can
    /// toggle it while a shot is in the air, and the next qualifying shot must observe the change. A
    /// read-only boundary over that existing state, not a new owner of it - see docs/shot-lifecycle.md.
    /// </summary>
    public interface IMoneyBallState
    {
        bool MoneyBallEnabled { get; }
    }
}
