using System.Collections.Generic;
using System.Linq;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using NUnit.Framework;

/// <summary>
/// Pure edit-mode tests for the player-selection core: stable-ID selection, cycling, CPU draft
/// slots, required-opponent reconciliation, lock validation and roster conversion. None of this
/// needs a scene - that is the point of moving it out of StartManager.
/// </summary>
public class Level5PlayerSelectionCoreTests
{
    private static CharacterSelectOption Option(
        int id,
        string name = null,
        bool isShooter = true,
        bool isFighter = false,
        bool isUnlocked = true)
    {
        return new CharacterSelectOption(id, name ?? ("char" + id), "obj" + id, isShooter, isFighter, isUnlocked, CharacterSelectStats.Empty);
    }

    private static List<CharacterSelectOption> Catalog(params CharacterSelectOption[] options)
    {
        return new List<CharacterSelectOption>(options);
    }

    // ---- selection state --------------------------------------------------------------------

    [Test]
    public void DefaultStateHasNoPrimaryAndThreeInactiveCpuSlots()
    {
        PlayerSelectionState state = new PlayerSelectionState();

        Assert.That(state.PrimaryCharacterId, Is.Null);
        Assert.That(state.CpuSlots.Count, Is.EqualTo(3));
        Assert.That(state.ParticipantCount, Is.EqualTo(1));
        foreach (PlayerSelectionSlot slot in state.CpuSlots)
        {
            Assert.That(slot.IsActive, Is.False);
        }
    }

    [Test]
    public void EnsurePrimarySelectedKeepsAValidRememberedId()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 2 };
        PlayerSelectionController controller = new PlayerSelectionController(state);

        controller.EnsurePrimarySelected(Catalog(Option(1), Option(2), Option(3)));

        Assert.That(state.PrimaryCharacterId, Is.EqualTo(2));
    }

    [Test]
    public void EnsurePrimarySelectedFallsBackToFirstCatalogEntryWhenMissing()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 99 };
        PlayerSelectionController controller = new PlayerSelectionController(state);

        controller.EnsurePrimarySelected(Catalog(Option(1), Option(2)));

        Assert.That(state.PrimaryCharacterId, Is.EqualTo(1));
    }

    [Test]
    public void EnsurePrimarySelectedFallsBackWhenNothingRemembered()
    {
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);

        controller.EnsurePrimarySelected(Catalog(Option(5), Option(6)));

        Assert.That(state.PrimaryCharacterId, Is.EqualTo(5));
    }

    [Test]
    public void CyclePrimaryWrapsForwardAndBackward()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> catalog = Catalog(Option(1), Option(2), Option(3));

        controller.CyclePrimary(catalog, null, null, 1);
        Assert.That(state.PrimaryCharacterId, Is.EqualTo(2));

        controller.CyclePrimary(catalog, null, null, 1);
        controller.CyclePrimary(catalog, null, null, 1);
        Assert.That(state.PrimaryCharacterId, Is.EqualTo(1), "stepping past the end should wrap to the first entry");

        controller.CyclePrimary(catalog, null, null, -1);
        Assert.That(state.PrimaryCharacterId, Is.EqualTo(3), "stepping before the start should wrap to the last entry");
    }

    [Test]
    public void CyclePrimarySkipsCharactersThatCannotPlayTheCurrentContext()
    {
        // Fighting context: only index 2 (a fighter) qualifies, everything else is a shooter.
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> catalog = Catalog(
            Option(1, isShooter: true, isFighter: false),
            Option(2, isShooter: true, isFighter: false),
            Option(3, isShooter: false, isFighter: true));
        GameModeDefinition fightingMode = TestDefinitions.Mode(GameModeId.BashUpSomeNerds, enemiesOnly: true, requiresBasketball: false);

        controller.CyclePrimary(catalog, fightingMode, MatchModifiers.Default, 1);

        Assert.That(state.PrimaryCharacterId, Is.EqualTo(3));
    }

    [Test]
    public void CyclePrimaryDoesNotSkipLockedCharacters()
    {
        // Locked characters remain browseable while cycling - only capability filters cycling.
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> catalog = Catalog(Option(1), Option(2, isUnlocked: false));

        controller.CyclePrimary(catalog, null, null, 1);

        Assert.That(state.PrimaryCharacterId, Is.EqualTo(2));
    }

    [Test]
    public void CpuSlotCyclesThroughNoneAndEveryCatalogEntry()
    {
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> cpuCatalog = Catalog(Option(10), Option(11));

        Assert.That(state.CpuSlots[0].IsActive, Is.False);

        controller.CycleCpuSlot(cpuCatalog, 0, 1);
        Assert.That(state.CpuSlots[0].CharacterId, Is.EqualTo(10));

        controller.CycleCpuSlot(cpuCatalog, 0, 1);
        Assert.That(state.CpuSlots[0].CharacterId, Is.EqualTo(11));

        controller.CycleCpuSlot(cpuCatalog, 0, 1);
        Assert.That(state.CpuSlots[0].IsActive, Is.False, "cycling past the last option returns to none");

        controller.CycleCpuSlot(cpuCatalog, 0, -1);
        Assert.That(state.CpuSlots[0].CharacterId, Is.EqualTo(11), "cycling backward from none reaches the last option");
    }

    [Test]
    public void CpuNoneNeverBecomesARosterParticipant()
    {
        // The catalog adapter never includes the legacy id-0 "none" record; a slot with no
        // catalog entries to offer simply stays inactive.
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);

        controller.CycleCpuSlot(new List<CharacterSelectOption>(), 0, 1);

        Assert.That(state.CpuSlots[0].IsActive, Is.False);
    }

    [TestCase(0, 1)]
    [TestCase(1, 2)]
    [TestCase(2, 3)]
    [TestCase(3, 4)]
    public void ParticipantCountTracksActiveCpuSlots(int activeCpuSlots, int expectedParticipants)
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> cpuCatalog = Catalog(Option(10), Option(11), Option(12));

        for (int slot = 0; slot < activeCpuSlots; slot++)
        {
            controller.ActivateCpuSlot(slot, cpuCatalog);
        }

        Assert.That(controller.ParticipantCount, Is.EqualTo(expectedParticipants));
        Assert.That(controller.ParticipantCount, Is.LessThanOrEqualTo(PlayerRoster.MaxSlots));
    }

    [Test]
    public void DeactivateCpuSlotClearsIt()
    {
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);
        controller.ActivateCpuSlot(1, Catalog(Option(10)));

        controller.DeactivateCpuSlot(1);

        Assert.That(state.CpuSlots[1].IsActive, Is.False);
    }

    // ---- availability / lock ------------------------------------------------------------------

    [Test]
    public void UnlockedPrimaryBuildsSuccessfully()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> catalog = Catalog(Option(1, isUnlocked: true));

        PlayerSelectValidation validation = controller.ValidateLaunch(catalog);

        Assert.That(validation.IsValid, Is.True);
    }

    [Test]
    public void LockedPrimaryRemainsSelectableButLaunchFails()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> catalog = Catalog(Option(1, isUnlocked: false));

        // Browsing/selecting the locked character is unaffected...
        Assert.That(state.PrimaryCharacterId, Is.EqualTo(1));

        // ...but launch is explicitly refused.
        PlayerSelectValidation validation = controller.ValidateLaunch(catalog);
        Assert.That(validation.IsValid, Is.False);

        PlayerRosterBuildResult build = controller.TryBuildRoster(catalog, new List<CharacterSelectOption>(), null);
        Assert.That(build.Succeeded, Is.False);
    }

    [Test]
    public void MissingPrimarySelectionFailsValidationExplicitly()
    {
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);

        PlayerSelectValidation validation = controller.ValidateLaunch(new List<CharacterSelectOption>());

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Reason, Is.Not.Empty);
    }

    // ---- required-CPU reconciliation and implicit defenders -----------------------------------

    [Test]
    public void ReconcileRequiredCpuActivatesFirstRealCpuWhenNoneIsActive()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints, requiresCpuOpponent: true);
        List<CharacterSelectOption> cpuCatalog = Catalog(Option(10), Option(11));

        bool changed = controller.ReconcileRequiredCpu(mode, cpuCatalog);

        Assert.That(changed, Is.True);
        Assert.That(state.CpuSlots[0].CharacterId, Is.EqualTo(10));
        Assert.That(controller.ParticipantCount, Is.EqualTo(2), "the visible participant count must agree with what will launch");
    }

    [Test]
    public void ReconcileRequiredCpuDoesNothingWhenACpuIsAlreadyActive()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints, requiresCpuOpponent: true);
        List<CharacterSelectOption> cpuCatalog = Catalog(Option(10), Option(11));
        controller.ActivateCpuSlot(1, Catalog(Option(11)));

        bool changed = controller.ReconcileRequiredCpu(mode, cpuCatalog);

        Assert.That(changed, Is.False);
        Assert.That(state.CpuSlots[0].IsActive, Is.False);
        Assert.That(state.CpuSlots[1].CharacterId, Is.EqualTo(11));
    }

    [Test]
    public void ReconcileRequiredCpuDoesNothingForModesThatDoNotRequireOne()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints, requiresCpuOpponent: false);

        bool changed = controller.ReconcileRequiredCpu(mode, Catalog(Option(10)));

        Assert.That(changed, Is.False);
        Assert.That(controller.ParticipantCount, Is.EqualTo(1));
    }

    [Test]
    public void ImplicitDefenderModeOmitsDraftCpusFromTheBuiltRoster()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        controller.ActivateCpuSlot(0, Catalog(Option(10)));
        GameModeDefinition lockdown = TestDefinitions.Mode(GameModeId.Lockdown, addsImplicitDefender: true, maxPlayers: 1);

        PlayerRosterBuildResult result = controller.TryBuildRoster(Catalog(Option(1)), Catalog(Option(10)), lockdown);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Roster.Count, Is.EqualTo(1), "Lockdown brings its own defender; the authored CPU draft must not enter the roster");
    }

    [Test]
    public void BuildRosterDoesNotMutateSelectionState()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints, requiresCpuOpponent: true);

        // No CPU active and the mode requires one - TryBuildRoster must not silently add it.
        PlayerRosterBuildResult result = controller.TryBuildRoster(Catalog(Option(1)), Catalog(Option(10)), mode);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Roster.Count, Is.EqualTo(1), "building a roster must not mutate the draft to satisfy a required opponent");
        Assert.That(state.CpuSlots[0].IsActive, Is.False);
    }

    // ---- roster conversion ---------------------------------------------------------------------

    [Test]
    public void RosterFromOneHumanOnly()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);

        PlayerRosterBuildResult result = controller.TryBuildRoster(Catalog(Option(1, "Hero")), new List<CharacterSelectOption>(), null);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Roster.Count, Is.EqualTo(1));
        Assert.That(result.Roster.Players[0].ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(result.Roster.Players[0].Character.DisplayName, Is.EqualTo("Hero"));
    }

    [Test]
    public void RosterFromOneHumanAndOneCpu()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        controller.ActivateCpuSlot(0, Catalog(Option(10)));

        PlayerRosterBuildResult result = controller.TryBuildRoster(Catalog(Option(1)), Catalog(Option(10)), null);

        Assert.That(result.Roster.Count, Is.EqualTo(2));
        Assert.That(result.Roster.Players[0].ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(result.Roster.Players[1].ControlType, Is.EqualTo(PlayerControlType.Cpu));
    }

    [Test]
    public void RosterFromOneHumanAndThreeCpusIsDenseAndOrdered()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        List<CharacterSelectOption> cpuCatalog = Catalog(Option(10), Option(11), Option(12));
        controller.ActivateCpuSlot(0, Catalog(Option(10)));
        controller.ActivateCpuSlot(1, Catalog(Option(11)));
        controller.ActivateCpuSlot(2, Catalog(Option(12)));

        PlayerRosterBuildResult result = controller.TryBuildRoster(Catalog(Option(1)), cpuCatalog, null);

        Assert.That(result.Roster.Count, Is.EqualTo(4));
        for (int i = 0; i < result.Roster.Count; i++)
        {
            Assert.That(result.Roster.Players[i].SlotId, Is.EqualTo(i));
        }

        Assert.That(result.Roster.Players[1].Character.CharacterId, Is.EqualTo(10));
        Assert.That(result.Roster.Players[2].Character.CharacterId, Is.EqualTo(11));
        Assert.That(result.Roster.Players[3].Character.CharacterId, Is.EqualTo(12));
    }

    [Test]
    public void WizardOfBoatVariantResolverIsInvokedAtConversionAndOnlyProducesAllowedVariants()
    {
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);
        CharacterSelectOption wizard = Option(1, "Wizard of Boat");

        for (int i = 0; i < 20; i++)
        {
            PlayerRosterBuildResult result = controller.TryBuildRoster(
                Catalog(wizard),
                new List<CharacterSelectOption>(),
                null,
                LegacyCharacterVariantResolverStub.Resolve);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(new[] { "wob1", "wob2" }.Contains(result.Roster.Players[0].Character.ObjectName), Is.True);
        }
    }

    /// <summary>A stand-in for the real Unity-Random-backed resolver, so this pure test stays Unity-free.</summary>
    private static class LegacyCharacterVariantResolverStub
    {
        private static readonly System.Random Random = new System.Random();

        public static string Resolve(CharacterSelectOption primary)
        {
            if (primary == null || !primary.DisplayName.ToLowerInvariant().Contains("boat"))
            {
                return null;
            }

            return Random.Next(1, 100) > 50 ? "wob1" : "wob2";
        }
    }
}
