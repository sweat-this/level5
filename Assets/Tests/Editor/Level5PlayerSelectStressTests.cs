using System;
using System.Collections.Generic;
using System.Linq;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using NUnit.Framework;

/// <summary>
/// Stress/fuzz coverage for the player-selection core, on top of the example-based tests in
/// <see cref="Level5PlayerSelectionCoreTests"/>. These drive thousands of randomized operations
/// against varying catalog shapes and assert the invariants that must hold regardless of the
/// exact sequence: selection always resolves to a real catalog entry (or stays empty), the
/// participant count never exceeds the roster cap, and conversion never throws or mutates state
/// it should not.
/// </summary>
public class Level5PlayerSelectStressTests
{
    private static CharacterSelectOption Option(int id, bool isShooter = true, bool isFighter = false, bool isUnlocked = true)
    {
        return new CharacterSelectOption(id, "char" + id, "obj" + id, isShooter, isFighter, isUnlocked, CharacterSelectStats.Empty);
    }

    private static List<CharacterSelectOption> BuildCatalog(int size, Random random)
    {
        List<CharacterSelectOption> catalog = new List<CharacterSelectOption>(size);
        for (int i = 0; i < size; i++)
        {
            // Roughly a third fighters, a third shooters, a third both, a random few locked -
            // enough variety that capability filtering and lock validation both get exercised.
            bool isShooter = random.Next(3) != 0;
            bool isFighter = random.Next(3) != 1;
            bool isUnlocked = random.Next(5) != 0;
            catalog.Add(Option(i + 1, isShooter, isFighter, isUnlocked));
        }

        return catalog;
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(11)]
    public void RandomizedCyclingAlwaysResolvesToARealCatalogEntry(int catalogSize)
    {
        Random random = new Random(1000 + catalogSize);
        List<CharacterSelectOption> catalog = BuildCatalog(catalogSize, random);
        List<CharacterSelectOption> cpuCatalog = BuildCatalog(catalogSize, random);
        GameModeDefinition[] modes =
        {
            null,
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Mode(GameModeId.BashUpSomeNerds, enemiesOnly: true, requiresBasketball: false),
        };
        MatchModifiers[] modifierSets = { null, MatchModifiers.Default, MatchModifiers.Default.With(enemies: true) };

        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);

        for (int i = 0; i < 5000; i++)
        {
            int step = random.Next(-3, 4);
            GameModeDefinition mode = modes[random.Next(modes.Length)];
            MatchModifiers modifiers = modifierSets[random.Next(modifierSets.Length)];

            controller.CyclePrimary(catalog, mode, modifiers, step);

            int slot = random.Next(PlayerSelectionState.CpuSlotCount);
            switch (random.Next(4))
            {
                case 0:
                    controller.CycleCpuSlot(cpuCatalog, slot, step == 0 ? 1 : step);
                    break;
                case 1:
                    controller.ActivateCpuSlot(slot, cpuCatalog);
                    break;
                case 2:
                    controller.DeactivateCpuSlot(slot);
                    break;
                default:
                    controller.ReconcileRequiredCpu(mode, cpuCatalog);
                    break;
            }

            // Invariant 1: the primary, once set, always names a real catalog entry.
            if (state.PrimaryCharacterId.HasValue)
            {
                Assert.That(catalog.Any(o => o.CharacterId == state.PrimaryCharacterId.Value), Is.True,
                    $"iteration {i}: primary id {state.PrimaryCharacterId} is not in the catalog");
            }

            // Invariant 2: every active CPU slot names a real CPU-catalog entry.
            foreach (PlayerSelectionSlot cpuSlot in state.CpuSlots)
            {
                if (cpuSlot.IsActive)
                {
                    Assert.That(cpuCatalog.Any(o => o.CharacterId == cpuSlot.CharacterId.Value), Is.True,
                        $"iteration {i}: cpu slot names id {cpuSlot.CharacterId} which is not in the cpu catalog");
                }
            }

            // Invariant 3: participant count never exceeds the roster cap.
            Assert.That(controller.ParticipantCount, Is.LessThanOrEqualTo(PlayerRoster.MaxSlots));

            // Invariant 4: building a roster from whatever state resulted never throws, and on
            // success produces a dense, correctly-typed roster no larger than the participant count.
            controller.EnsurePrimarySelected(catalog);
            PlayerRosterBuildResult result = controller.TryBuildRoster(catalog, cpuCatalog, mode);
            if (result.Succeeded)
            {
                Assert.That(result.Roster.Count, Is.LessThanOrEqualTo(PlayerRoster.MaxSlots));
                Assert.That(result.Roster.Players[0].ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
                for (int p = 0; p < result.Roster.Count; p++)
                {
                    Assert.That(result.Roster.Players[p].SlotId, Is.EqualTo(p));
                }
            }
        }
    }

    [Test]
    public void RandomizedCyclingNeverThrowsWithAnEmptyCpuCatalog()
    {
        // Every CPU slot operation must degrade to "stay inactive" rather than throw when there is
        // nothing to offer - the shape a brand new account with no unlocked CPUs would produce.
        Random random = new Random(42);
        // Unlocked explicitly: this test is about CPU-slot behavior with nothing to offer, not
        // about lock validation, so the primary must not randomly fail ValidateLaunch here.
        List<CharacterSelectOption> catalog = new List<CharacterSelectOption> { Option(1, isUnlocked: true) };
        List<CharacterSelectOption> emptyCpuCatalog = new List<CharacterSelectOption>();
        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = 1 };
        PlayerSelectionController controller = new PlayerSelectionController(state);

        for (int i = 0; i < 500; i++)
        {
            int slot = random.Next(PlayerSelectionState.CpuSlotCount);
            Assert.DoesNotThrow(() => controller.CycleCpuSlot(emptyCpuCatalog, slot, random.Next(-2, 3)));
            Assert.DoesNotThrow(() => controller.ActivateCpuSlot(slot, emptyCpuCatalog));
        }

        foreach (PlayerSelectionSlot cpuSlot in state.CpuSlots)
        {
            Assert.That(cpuSlot.IsActive, Is.False);
        }

        PlayerRosterBuildResult result = controller.TryBuildRoster(catalog, emptyCpuCatalog, null);
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Roster.Count, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedReconciliationAndBuildNeverMutatesStateInsideTryBuildRoster()
    {
        Random random = new Random(7);
        List<CharacterSelectOption> catalog = BuildCatalog(4, random);
        List<CharacterSelectOption> cpuCatalog = BuildCatalog(4, random);
        GameModeDefinition requiresCpu = TestDefinitions.Mode(GameModeId.TotalPoints, requiresCpuOpponent: true);
        PlayerSelectionState state = new PlayerSelectionState();
        PlayerSelectionController controller = new PlayerSelectionController(state);
        controller.EnsurePrimarySelected(catalog);

        for (int i = 0; i < 200; i++)
        {
            int? primaryBefore = state.PrimaryCharacterId;
            int?[] cpuBefore = state.CpuSlots.Select(s => s.CharacterId).ToArray();

            controller.TryBuildRoster(catalog, cpuCatalog, requiresCpu);

            Assert.That(state.PrimaryCharacterId, Is.EqualTo(primaryBefore), "TryBuildRoster must not change the primary selection");
            CollectionAssert.AreEqual(cpuBefore, state.CpuSlots.Select(s => s.CharacterId).ToArray(), "TryBuildRoster must not change CPU slots");
        }
    }

    // ---- IndexMath ------------------------------------------------------------------------------

    [TestCase(0, 5, 0)]
    [TestCase(4, 5, 4)]
    [TestCase(5, 5, 0)]
    [TestCase(-1, 5, 4)]
    [TestCase(-5, 5, 0)]
    [TestCase(-6, 5, 4)]
    [TestCase(12, 5, 2)]
    [TestCase(-12, 5, 3)]
    [TestCase(0, 1, 0)]
    [TestCase(99, 1, 0)]
    [TestCase(-99, 1, 0)]
    [TestCase(3, 0, 0)]
    [TestCase(3, -2, 0)]
    public void IndexMathWrapMatchesExpectedValueForBoundaryInputs(int value, int count, int expected)
    {
        Assert.That(IndexMath.Wrap(value, count), Is.EqualTo(expected));
    }

    [Test]
    public void IndexMathWrapIsAlwaysInRangeForRandomizedInputs()
    {
        Random random = new Random(2026);
        for (int i = 0; i < 10000; i++)
        {
            int count = random.Next(1, 50);
            int value = random.Next(-100000, 100000);

            int wrapped = IndexMath.Wrap(value, count);

            Assert.That(wrapped, Is.GreaterThanOrEqualTo(0));
            Assert.That(wrapped, Is.LessThan(count));
            Assert.That(((wrapped - value) % count + count) % count, Is.EqualTo(0), "wrapped value must be congruent to the input modulo count");
        }
    }

    // ---- session --------------------------------------------------------------------------------

    [Test]
    public void SessionSurvivesRapidRandomizedRememberAndClearCycles()
    {
        Random random = new Random(99);
        PlayerSelectionSession.Clear();

        for (int i = 0; i < 2000; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    PlayerSelectionSession.RememberPrimary(random.Next(1, 100));
                    break;
                case 1:
                    PlayerSelectionSession.RememberCpu(random.Next(PlayerSelectionState.CpuSlotCount), random.Next(1, 100));
                    break;
                case 2:
                    PlayerSelectionSession.RememberCpu(random.Next(PlayerSelectionState.CpuSlotCount), null);
                    break;
                default:
                    PlayerSelectionSession.Clear();
                    break;
            }
        }

        Assert.DoesNotThrow(() => PlayerSelectionSession.Clear());
    }
}
