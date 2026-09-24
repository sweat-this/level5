using System.IO;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Confirms <c>GameRules.SaveMatchResults</c> and <c>EndRoundMenuManager.saveGame</c> call
    /// <see cref="BackendV2MatchResultSubmission.TryQueue"/> exactly once each, and only after the
    /// score they pass it has already become locally durable - the one property a behavioral test
    /// cannot reach without standing up a full gameplay scene (<c>GameLevelManager</c>,
    /// <c>DBConnector</c>, a loaded match), which is out of proportion to what needs verifying here.
    /// <see cref="Level5BackendV2MatchResultSubmissionTests"/> covers everything
    /// <see cref="BackendV2MatchResultSubmission"/> itself does once called.
    /// </summary>
    public class Level5BackendV2MatchResultWiringTests
    {
        private static readonly string GameRulesPath = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "game manager", "GameRules.cs");

        private static readonly string EndRoundMenuManagerPath = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_end_round", "EndRoundMenuManager.cs");

        [Test]
        public void GameRulesQueuesTheOrdinaryResultExactlyOnceAfterLocalDurabilitySucceeds()
        {
            string text = File.ReadAllText(GameRulesPath);
            const string call = "BackendV2MatchResultSubmission.TryQueue(user);";

            int guardIndex = text.IndexOf(
                "gameModeId != Modes.FreePlay && gameModeId != Modes.BeatThaComputahs");
            int durableIndex = text.IndexOf(
                "matchScoreSaveCompleted = savedLocally || PendingMatchPersistenceStore.QueueScore(user);");
            int callIndex = text.IndexOf(call);
            int secondCallIndex = text.IndexOf(call, callIndex + 1);

            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0), "the FreePlay/BeatThaComputahs exclusion must still exist");
            Assert.That(callIndex, Is.GreaterThanOrEqualTo(0), "GameRules must queue the ordinary V2 result");
            Assert.That(secondCallIndex, Is.EqualTo(-1), "must be called exactly once");
            Assert.That(callIndex, Is.GreaterThan(guardIndex),
                "the V2 queue call must be reachable only within the FreePlay/BeatThaComputahs-excluded branch");
            Assert.That(callIndex, Is.GreaterThan(durableIndex),
                "the V2 queue call must come after the score is made locally durable, never before");
        }

        [Test]
        public void EndRoundMenuManagerQueuesTheCampaignAggregateExactlyOnceAfterLocalDurabilitySucceeds()
        {
            string text = File.ReadAllText(EndRoundMenuManagerPath);
            const string call = "BackendV2MatchResultSubmission.TryQueue(user);";

            int earlyReturnIndex = text.IndexOf("if (!isGameSaved)");
            int callIndex = text.IndexOf(call);
            int secondCallIndex = text.IndexOf(call, callIndex + 1);

            Assert.That(callIndex, Is.GreaterThanOrEqualTo(0), "EndRoundMenuManager must queue the campaign aggregate result");
            Assert.That(secondCallIndex, Is.EqualTo(-1), "must be called exactly once");
            Assert.That(callIndex, Is.GreaterThan(earlyReturnIndex),
                "the V2 queue call must come after the isGameSaved early-return, never before local durability succeeds");
        }
    }
}
