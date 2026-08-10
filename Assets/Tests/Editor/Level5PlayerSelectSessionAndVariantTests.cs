using NUnit.Framework;

/// <summary>
/// Edit-mode tests for the two small Unity/menu-layer seams: <see cref="PlayerSelectionSession"/>
/// (stable-ID session memory that replaced the GameOptions player/CPU indices) and
/// <see cref="LegacyCharacterVariantResolver"/> (the quarantined Wizard-of-Boat special case).
/// </summary>
public class Level5PlayerSelectSessionAndVariantTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerSelectionSession.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSelectionSession.Clear();
    }

    [Test]
    public void SessionStartsWithNothingRemembered()
    {
        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.Null);
        for (int slot = 0; slot < Level5.Core.PlayerSelection.PlayerSelectionState.CpuSlotCount; slot++)
        {
            Assert.That(PlayerSelectionSession.GetCpu(slot), Is.Null);
        }
    }

    [Test]
    public void SessionRemembersPrimaryAndCpuSlotsIndependently()
    {
        PlayerSelectionSession.RememberPrimary(7);
        PlayerSelectionSession.RememberCpu(0, 10);
        PlayerSelectionSession.RememberCpu(2, 12);

        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.EqualTo(7));
        Assert.That(PlayerSelectionSession.GetCpu(0), Is.EqualTo(10));
        Assert.That(PlayerSelectionSession.GetCpu(1), Is.Null);
        Assert.That(PlayerSelectionSession.GetCpu(2), Is.EqualTo(12));
    }

    [Test]
    public void ClearResetsEverything()
    {
        PlayerSelectionSession.RememberPrimary(1);
        PlayerSelectionSession.RememberCpu(0, 2);

        PlayerSelectionSession.Clear();

        Assert.That(PlayerSelectionSession.PrimaryCharacterId, Is.Null);
        Assert.That(PlayerSelectionSession.GetCpu(0), Is.Null);
    }

    [Test]
    public void NonWizardOfBoatCharactersGetNoVariantOverride()
    {
        Level5.Core.PlayerSelection.CharacterSelectOption option = new Level5.Core.PlayerSelection.CharacterSelectOption(
            1, "Dr. Blood", "drblood", true, true, true, Level5.Core.PlayerSelection.CharacterSelectStats.Empty);

        Assert.That(LegacyCharacterVariantResolver.ResolveObjectName(option), Is.Null);
    }

    [Test]
    public void WizardOfBoatResolvesToOneOfTheTwoKnownRuntimeVariants()
    {
        Level5.Core.PlayerSelection.CharacterSelectOption option = new Level5.Core.PlayerSelection.CharacterSelectOption(
            1, "Wizard of Boat", "wizardofboat", true, true, true, Level5.Core.PlayerSelection.CharacterSelectStats.Empty);

        for (int i = 0; i < 30; i++)
        {
            string variant = LegacyCharacterVariantResolver.ResolveObjectName(option);
            Assert.That(variant == "wob1" || variant == "wob2", Is.True, "unexpected variant: " + variant);
        }
    }
}
