using Level5.Core;
using NUnit.Framework;

/// <summary>
/// Direct regression guard for <see cref="CharacterProgressAccountId.GetCurrent"/>'s exact mapping -
/// the thing every local-profile/guest selection ultimately reads to pick a save scope. No SQLite
/// involved (unlike <see cref="Level5LocalProfileAllocationTests"/>, which shares this concern only
/// incidentally via <c>NewlyCreatedProfileGetsItsOwnCharacterProgressionAccountScope</c>): this fixture
/// touches only the static <see cref="LocalAccountIdentity"/> fields the method reads, so it needs no
/// database harness at all.
/// </summary>
public class Level5CharacterProgressAccountIdTests
{
    private int previousUserId;
    private string previousUserName;

    [SetUp]
    public void SetUp()
    {
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
    }

    [TearDown]
    public void TearDown()
    {
        LocalAccountIdentity.UserId = previousUserId;
        LocalAccountIdentity.UserName = previousUserName;
    }

    [Test]
    public void PositiveUserIdResolvesToItsExactNumericStringWithNoConversion()
    {
        LocalAccountIdentity.UserId = 123;
        LocalAccountIdentity.UserName = "Patrick";

        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo("123"));
    }

    /// <summary>
    /// The reserved guest id (<see cref="UserAccountManager.GuestUserid"/>) is itself a positive int,
    /// so guest selection resolves through the same branch as any other positive UserId - there is no
    /// separate guest conversion. This pins the current value rather than re-deriving it, so a change
    /// to the reserved id would be caught here.
    /// </summary>
    [Test]
    public void GuestUserIdResolvesToItsOwnExistingScopeWithNoConversion()
    {
        LocalAccountIdentity.UserId = UserAccountManager.GuestUserid;
        LocalAccountIdentity.UserName = UserAccountManager.GuestUsername;

        Assert.That(
            CharacterProgressAccountId.GetCurrent(),
            Is.EqualTo(UserAccountManager.GuestUserid.ToString()));
    }

    [Test]
    public void NonPositiveUserIdFallsBackToUserName()
    {
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = "someone";

        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo("someone"));
    }

    [Test]
    public void NoUserIdOrUserNameFallsBackToTheLiteralGuestScope()
    {
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = null;

        Assert.That(CharacterProgressAccountId.GetCurrent(), Is.EqualTo("guest"));
    }
}
