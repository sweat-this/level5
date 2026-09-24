using NUnit.Framework;

/// <summary>
/// Backend V2 leaderboard cutover: proves the Stats screen's four-toggle filter semantics translate
/// to Backend V2 query values exactly as specified - all-off omits every parameter, any-on sends the
/// exact four-value combination (including explicit false), and there is no legacy
/// modes-20-22-force-enemies override.
/// </summary>
public class OnlineLeaderboardFilterTranslatorTests
{
    [Test]
    public void AllTogglesOffOmitsEveryFilterParameter()
    {
        OnlineLeaderboardFilterQuery query = OnlineLeaderboardFilterTranslator.Translate(
            hardcore: false, traffic: false, enemies: false, sniper: false);

        Assert.That(query.Hardcore, Is.Null);
        Assert.That(query.Traffic, Is.Null);
        Assert.That(query.Enemies, Is.Null);
        Assert.That(query.Sniper, Is.Null);
    }

    [Test]
    public void OneToggleOnSendsAllFourExactValues()
    {
        OnlineLeaderboardFilterQuery query = OnlineLeaderboardFilterTranslator.Translate(
            hardcore: true, traffic: false, enemies: false, sniper: false);

        Assert.That(query.Hardcore, Is.EqualTo(true));
        Assert.That(query.Traffic, Is.EqualTo(false));
        Assert.That(query.Enemies, Is.EqualTo(false));
        Assert.That(query.Sniper, Is.EqualTo(false));
    }

    [Test]
    public void EveryFilterOnSendsAllFourAsTrue()
    {
        OnlineLeaderboardFilterQuery query = OnlineLeaderboardFilterTranslator.Translate(
            hardcore: true, traffic: true, enemies: true, sniper: true);

        Assert.That(query.Hardcore, Is.EqualTo(true));
        Assert.That(query.Traffic, Is.EqualTo(true));
        Assert.That(query.Enemies, Is.EqualTo(true));
        Assert.That(query.Sniper, Is.EqualTo(true));
    }

    [Test]
    public void OnlyTrafficOnStillSendsHardcoreEnemiesSniperAsExplicitFalse()
    {
        OnlineLeaderboardFilterQuery query = OnlineLeaderboardFilterTranslator.Translate(
            hardcore: false, traffic: true, enemies: false, sniper: false);

        Assert.That(query.Hardcore, Is.EqualTo(false));
        Assert.That(query.Traffic, Is.EqualTo(true));
        Assert.That(query.Enemies, Is.EqualTo(false));
        Assert.That(query.Sniper, Is.EqualTo(false));
    }

    /// <summary>
    /// Legacy APIHelper.GetHighscoreByModeid/GetHighscoreCountByModeid force enemies=1 for modes
    /// 20-22 (Assets/Scripts/restapi/APIHelper.cs). Backend V2 owns leaderboard filter policy now;
    /// the translator has no mode parameter at all, so it cannot special-case any mode - proven here
    /// by exercising a mode-20-22-shaped scenario (all filters off) and confirming enemies is still
    /// omitted (null), never forced to true.
    /// </summary>
    [Test]
    public void NoModeParameterExistsSoNoModeCanForceAnEnemiesOverride()
    {
        OnlineLeaderboardFilterQuery query = OnlineLeaderboardFilterTranslator.Translate(
            hardcore: false, traffic: false, enemies: false, sniper: false);

        Assert.That(query.Enemies, Is.Null, "enemies must be omitted, never forced true, regardless of mode");
    }
}
