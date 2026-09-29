using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Source-order guards for the match-local gameplay device plan (two-human local input foundation).
///
/// The behaviour is covered end to end in PlayMode; these pin the structural facts a refactor could
/// silently break without failing any gameplay test on a machine that happens to have the right
/// devices attached:
///
/// - the plan is composed by <c>GameLevelManager.Awake</c> from the roster, before any participant is
///   spawned (so no <c>PlayerController.Start()</c> can beat it);
/// - a launch source that can build a multi-human roster (the start menu, and the campaign round
///   advance that reuses a roster) asks the device preflight before it commits the match;
/// - <c>PlayerControlsProvider</c> no longer indexes <c>Gamepad.all</c> per acquisition;
/// - <c>Level5.Core</c> stays free of Input System types, so a roster only ever says "local input
///   slot N", never which device that is.
/// </summary>
public class Level5GameplayDevicePlanCompositionGuardTests
{
    private static string ProjectPath(params string[] parts)
    {
        return Path.Combine(new[] { Directory.GetCurrentDirectory() }.Concat(parts).ToArray());
    }

    private static string Read(params string[] parts)
    {
        return File.ReadAllText(ProjectPath(parts));
    }

    [Test]
    public void GameLevelManagerConfiguresTheDevicePlanFromTheRosterBeforeSpawningAnyone()
    {
        string source = Read("Assets", "Scripts", "game manager", "GameLevelManager.cs");

        int configure = source.IndexOf("PlayerControlsProvider.TryConfigureGameplayDevicePlan(_roster.LocalHumanCount");
        int spawn = source.IndexOf("_spawnCoordinator.SpawnPlayers()");

        Assert.That(configure, Is.GreaterThanOrEqualTo(0), "GameLevelManager.Awake must configure the plan from _roster.LocalHumanCount");
        Assert.That(spawn, Is.GreaterThanOrEqualTo(0));
        Assert.That(configure, Is.LessThan(spawn), "the plan must exist before any human PlayerController can Start");
    }

    [Test]
    public void GameLevelManagerClearsThePlanWhenItsMatchEnds()
    {
        string source = Read("Assets", "Scripts", "game manager", "GameLevelManager.cs");

        Assert.That(source, Does.Contain("PlayerControlsProvider.ClearGameplayDevicePlan()"));
    }

    [Test]
    public void TheStartMenuLaunchAsksTheDevicePreflightBeforeCommittingTheMatch()
    {
        string source = Read("Assets", "Scripts", "menu_start", "StartManager.cs");

        int preflight = source.IndexOf("PlayerControlsProvider.TryPreflightGameplayDevices(roster.LocalHumanCount");
        int begin = source.IndexOf("ActiveMatch.Begin(configuration)");

        Assert.That(preflight, Is.GreaterThanOrEqualTo(0));
        Assert.That(begin, Is.GreaterThanOrEqualTo(0));
        Assert.That(preflight, Is.LessThan(begin), "an unseatable roster must be refused before the match becomes current");
    }

    [Test]
    public void TheCampaignRoundAdvanceAsksTheDevicePreflightBeforeCommittingTheNextRound()
    {
        string source = Read("Assets", "Scripts", "menu_end_round", "EndRoundMenuManager.cs");

        int preflight = source.IndexOf("PlayerControlsProvider.TryPreflightGameplayDevices(current.Roster.LocalHumanCount");
        int apply = source.IndexOf("ApplyLevelSelection(targetLevelIndex)");

        Assert.That(preflight, Is.GreaterThanOrEqualTo(0));
        Assert.That(apply, Is.GreaterThanOrEqualTo(0));
        Assert.That(preflight, Is.LessThan(apply), "a roster the devices can no longer seat must be refused before the next round begins");
    }

    [Test]
    public void PlayerControlsProviderNoLongerResolvesGamepadsByIndexPerAcquisition()
    {
        string source = Read("Assets", "Scripts", "input", "Level5Input", "PlayerControlsProvider.cs");

        Assert.That(source, Does.Not.Contain("Gamepad.all["), "gamepad ownership comes from the captured plan");
        Assert.That(source, Does.Not.Contain("Keyboard.current"), "device capture belongs to LocalGameplayDeviceAvailability");
    }

    [Test]
    public void TheCoreMatchAssemblyDoesNotReferenceTheInputSystem()
    {
        string[] offenders = Directory
            .GetFiles(ProjectPath("Assets", "Level5", "Core"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("UnityEngine.InputSystem")
                || File.ReadAllText(path).Contains("Gamepad.all")
                || File.ReadAllText(path).Contains("Keyboard.current"))
            .ToArray();

        Assert.That(offenders, Is.Empty, "Level5.Core must stay framework-independent: " + string.Join(", ", offenders));

        string asmdef = Read("Assets", "Level5", "Core", "Level5.Core.asmdef");
        Assert.That(asmdef, Does.Not.Contain("Unity.InputSystem"));
    }
}
