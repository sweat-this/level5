using System.Collections.Generic;
using Level5.Core.PlayerSelection;
using NUnit.Framework;

/// <summary>
/// Edit-mode tests for <see cref="RemoteCharacterSelectionResolver"/> (issue #179): resolving the
/// current local primary character for a remote correspondence launch through the exact same
/// stable-id fallback, launch validation and match-facing conversion normal player-select
/// initialization uses. <see cref="RemoteCharacterSelectionResolver.ResolveFromCatalog"/> is the
/// pure core exercised here - no <c>LoadedData</c>/scene is needed, matching
/// <c>Level5PlayerSelectionCoreTests</c>' own approach to the same underlying types.
/// </summary>
public class Level5RemoteCharacterSelectionResolverTests
{
    private LoadedData previousLoadedData;

    [SetUp]
    public void SetUp()
    {
        PlayerSelectionSession.Clear();
        previousLoadedData = LoadedData.instance;
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSelectionSession.Clear();
        LoadedData.instance = previousLoadedData;
    }

    private static CharacterSelectOption Option(
        int id,
        string name = null,
        string objectName = null,
        bool isShooter = true,
        bool isFighter = false,
        bool isUnlocked = true)
    {
        return new CharacterSelectOption(
            id, name ?? ("char" + id), objectName ?? ("obj" + id), isShooter, isFighter, isUnlocked,
            CharacterSelectStats.Empty);
    }

    private static List<CharacterSelectOption> Catalog(params CharacterSelectOption[] options)
    {
        return new List<CharacterSelectOption>(options);
    }

    [Test]
    public void AValidRememberedIdResolvesThatCharacter()
    {
        PlayerSelectionSession.RememberPrimary(2);

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(
            Catalog(Option(1), Option(2), Option(3)));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Character.CharacterId, Is.EqualTo(2));
    }

    [Test]
    public void TheResolvedCharacterSelectionPreservesTheOptionsFields()
    {
        PlayerSelectionSession.RememberPrimary(4);
        CharacterSelectOption option = Option(4, name: "Dr. Blood", objectName: "drblood", isShooter: true, isFighter: false);

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(Catalog(option));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Character.CharacterId, Is.EqualTo(4));
        Assert.That(result.Character.DisplayName, Is.EqualTo("Dr. Blood"));
        Assert.That(result.Character.ObjectName, Is.EqualTo("drblood"));
        Assert.That(result.Character.IsShooter, Is.True);
        Assert.That(result.Character.IsFighter, Is.False);
    }

    [Test]
    public void ANullRememberedIdUsesTheExistingFirstEntryFallback()
    {
        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.Null);

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(
            Catalog(Option(5), Option(6)));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Character.CharacterId, Is.EqualTo(5));
    }

    [Test]
    public void AStaleRememberedIdUsesTheSameFallback()
    {
        PlayerSelectionSession.RememberPrimary(99);

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(
            Catalog(Option(1), Option(2)));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Character.CharacterId, Is.EqualTo(1));
    }

    [Test]
    public void AFallbackUpdatesPlayerSelectionSession()
    {
        PlayerSelectionSession.RememberPrimary(99);

        RemoteCharacterSelectionResolver.ResolveFromCatalog(Catalog(Option(1), Option(2)));

        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.EqualTo(1));
    }

    [Test]
    public void AValidRememberedIdDoesNotRewriteTheSession()
    {
        PlayerSelectionSession.RememberPrimary(2);

        RemoteCharacterSelectionResolver.ResolveFromCatalog(Catalog(Option(1), Option(2), Option(3)));

        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.EqualTo(2));
    }

    [Test]
    public void ALockedSelectedCharacterIsRejectedThroughExistingLaunchValidation()
    {
        PlayerSelectionSession.RememberPrimary(1);

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(
            Catalog(Option(1, isUnlocked: false)));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("locked"));
    }

    [Test]
    public void ANullPrimaryCatalogFailsClearly()
    {
        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(null);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.Not.Empty);
    }

    [Test]
    public void AnEmptyPrimaryCatalogFailsClearly()
    {
        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(Catalog());

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.Not.Empty);
    }

    [Test]
    public void WizardOfBoatResolutionStillUsesOneOfTheExistingLaunchVariants()
    {
        PlayerSelectionSession.RememberPrimary(1);
        CharacterSelectOption wizardOfBoat = Option(1, name: "Wizard of Boat", objectName: "wizardofboat");

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveFromCatalog(Catalog(wizardOfBoat));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Character.ObjectName == "wob1" || result.Character.ObjectName == "wob2", Is.True,
            "unexpected object name: " + result.Character.ObjectName);
    }

    [Test]
    public void ResolveCurrentPrimaryFailsClearlyWhenNoDataIsLoaded()
    {
        // Explicitly cleared (rather than assumed) so this test does not depend on no other
        // fixture having left a LoadedData instance behind - SetUp/TearDown save and restore
        // whatever was there before, matching Level5CharacterProfileMatchContextTests' own
        // convention for this same static.
        LoadedData.instance = null;

        RemoteCharacterSelectionResult result = RemoteCharacterSelectionResolver.ResolveCurrentPrimary();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.Not.Empty);
    }
}
