using System;
using Assets.Scripts.database;
using Level5.Core.Match;
using NUnit.Framework;

public class Level5CapturedScorePersistenceTests
{
    [TestCase(1, 4)]
    [TestCase(4, 1)]
    public void SqlitePersistsCapturedParticipantCountAfterCurrentRosterChanges(int capturedCount, int laterCount)
    {
        var options = GameOptionsSnapshot.Capture();
        MatchConfiguration previousMatch = ActiveMatch.Configuration;
        DBHelperTestHarness.Session database = DBHelperTestHarness.Create();
        try
        {
            ActiveMatch.Clear();
            GameOptions.numPlayers = capturedCount;
            var captured = new HighScoreModel
            {
                Scoreid = Guid.NewGuid().ToString("N"),
                numPlayers = capturedCount
            };
            GameOptions.numPlayers = laterCount;

            Assert.That(database.Connector.savePlayerGameStats(captured), Is.True);
            // A retry must preserve the same row and captured count too.
            Assert.That(database.Connector.savePlayerGameStats(captured), Is.True);
            using (var command = database.Helper.Connection.CreateCommand())
            {
                command.CommandText = "SELECT numPlayers FROM HighScores WHERE scoreidUnique = @id";
                command.Parameters.AddWithValue("@id", captured.Scoreid);
                Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(capturedCount));
                command.CommandText = "SELECT COUNT(*) FROM HighScores WHERE scoreidUnique = @id";
                Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1));
            }
        }
        finally
        {
            DBHelperTestHarness.Destroy(database);
            options.Restore();
            if (previousMatch != null) ActiveMatch.Begin(previousMatch);
            else ActiveMatch.Clear();
        }
    }
}
