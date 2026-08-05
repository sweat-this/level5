using System;
using System.IO;
using NUnit.Framework;

public class Level5CoreTests
{
    private bool player1WasCpu;
    private bool player2WasCpu;
    private bool player3WasCpu;
    private bool player4WasCpu;
    private int playerCount;
    private int cpuPlayerCount;

    [SetUp]
    public void SetUp()
    {
        player1WasCpu = GameOptions.player1IsCpu;
        player2WasCpu = GameOptions.player2IsCpu;
        player3WasCpu = GameOptions.player3IsCpu;
        player4WasCpu = GameOptions.player4IsCpu;
        playerCount = GameOptions.numPlayers;
        cpuPlayerCount = GameOptions.numCpuPlayers;
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.player1IsCpu = player1WasCpu;
        GameOptions.player2IsCpu = player2WasCpu;
        GameOptions.player3IsCpu = player3WasCpu;
        GameOptions.player4IsCpu = player4WasCpu;
        GameOptions.numPlayers = playerCount;
        GameOptions.numCpuPlayers = cpuPlayerCount;
    }

    [Test]
    public void HumanInputSlotsIgnoreCpuPlayers()
    {
        GameOptions.player1IsCpu = false;
        GameOptions.player2IsCpu = true;
        GameOptions.player3IsCpu = false;
        GameOptions.player4IsCpu = false;

        Assert.That(GameOptions.GetHumanPlayerInputSlot(0), Is.EqualTo(0));
        Assert.That(GameOptions.GetHumanPlayerInputSlot(1), Is.EqualTo(-1));
        Assert.That(GameOptions.GetHumanPlayerInputSlot(2), Is.EqualTo(1));
        Assert.That(GameOptions.GetHumanPlayerInputSlot(3), Is.EqualTo(2));
    }

    [Test]
    public void CompactedCpuRosterAssignsCpuRolesByRosterPosition()
    {
        GameOptions.ConfigureSingleHumanRoster(2);

        Assert.That(GameOptions.numPlayers, Is.EqualTo(2));
        Assert.That(GameOptions.numCpuPlayers, Is.EqualTo(1));
        Assert.That(GameOptions.player1IsCpu, Is.False);
        Assert.That(GameOptions.player2IsCpu, Is.True);
        Assert.That(GameOptions.player3IsCpu, Is.False);
        Assert.That(GameOptions.player4IsCpu, Is.False);
    }

    [Test]
    public void LockdownRosterCountsItsImplicitCpu()
    {
        GameOptions.ConfigureSingleHumanRoster(1, true);

        Assert.That(GameOptions.numPlayers, Is.EqualTo(1));
        Assert.That(GameOptions.numCpuPlayers, Is.EqualTo(1));
        Assert.That(GameOptions.player2IsCpu, Is.True);
    }

    [Test]
    public void ResultIdsAreUniqueAndKeepTheirPrefix()
    {
        string first = ProgressionService.CreateResultId("round");
        string second = ProgressionService.CreateResultId("round");

        Assert.That(first, Does.StartWith("round-"));
        Assert.That(second, Does.StartWith("round-"));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void MatchSessionRotatesResultIdForEachGameplayLoad()
    {
        string previous = GameOptions.matchResultId;
        try
        {
            string first = MatchSession.BeginNewMatch();
            string second = MatchSession.BeginNewMatch();

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(MatchSession.EnsureCurrentMatch(), Is.EqualTo(second));
        }
        finally
        {
            GameOptions.matchResultId = previous;
        }
    }

    [TestCase(false, false, false, 2, CampaignNextAction.Advance)]
    [TestCase(true, false, false, 2, CampaignNextAction.Complete)]
    [TestCase(true, true, false, 1, CampaignNextAction.Retry)]
    [TestCase(true, true, false, 0, CampaignNextAction.EndRun)]
    [TestCase(false, true, false, 1, CampaignNextAction.Retry)]
    [TestCase(false, true, false, 0, CampaignNextAction.EndRun)]
    [TestCase(false, false, true, 0, CampaignNextAction.Retry)]
    [TestCase(true, true, true, 0, CampaignNextAction.Retry)]
    public void CampaignDecisionUsesOutcomeBeforeFinalLevel(
        bool finalLevel,
        bool winnerIsCpu,
        bool tie,
        int continuesRemaining,
        CampaignNextAction expected)
    {
        Assert.That(
            CampaignRoundDecision.Decide(finalLevel, winnerIsCpu, tie, continuesRemaining),
            Is.EqualTo(expected));
    }

    [Test]
    public void AtomicFilePreservesPreviousVersionAsBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "level5-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "save.json");

        try
        {
            AtomicFile.WriteAllText(path, "first");
            AtomicFile.WriteAllText(path, "second");

            Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("first"));

            File.Delete(path);
            Assert.That(AtomicFile.TryReadAllText(path, out string recovered), Is.True);
            Assert.That(recovered, Is.EqualTo("first"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Test]
    public void AtomicFileUsesValidBackupWhenPrimaryIsMalformed()
    {
        string directory = Path.Combine(Path.GetTempPath(), "level5-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "save.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "malformed");
            File.WriteAllText(path + ".bak", "valid");

            bool loaded = AtomicFile.TryReadAllText(path, value => value == "valid", out string recovered);

            Assert.That(loaded, Is.True);
            Assert.That(recovered, Is.EqualTo("valid"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
