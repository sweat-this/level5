using System.Reflection;
using Assets.Scripts.restapi;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Regression guard for retiring the legacy V1 remote score/leaderboard transport in favor of
    /// Backend V2 MatchResults/Leaderboards (see docs/persistence-boundaries.md). Reflection is used
    /// instead of a source-text scan (contrast <see cref="Level5BackendV2MatchResultWiringTests"/>) so
    /// this cannot be defeated by a comment or a doc reference still naming these symbols - if any of
    /// them exist at all, production code could call them again. Scoped only to the retired score
    /// surface; every other <c>APIHelper</c>/<c>DBHelper</c> method is untouched and out of scope.
    /// </summary>
    public class Level5V1ScoreTransportRetirementTests
    {
        private const BindingFlags AnyDeclared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        [Test]
        public void ApiHelperNoLongerExposesTheRetiredScoreLeaderboardMethods()
        {
            string[] retired =
            {
                "PostHighscore",
                "PostUnsubmittedHighscores",
                "PutHighscore",
                "GetHighscoreByModeid",
                "GetHighscoreCountByModeid",
                "GetHighscoreByScoreid",
                "ScoreIdExists",
                "SetScoreSubmittedWhenAvailable",
            };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(APIHelper).GetMethod(name, AnyDeclared), Is.Null,
                    "APIHelper." + name + " is retired V1 score transport and must not exist.");
            }
        }

        [Test]
        public void ApiHelperNoLongerExposesTheRetiredHighScoreEndpointConstants()
        {
            string[] retired =
            {
                "API_ADDRESS_DEV_publicApiHighScores",
                "API_ADDRESS_DEV_publicApiHighScoresUnsubmitted",
                "API_ADDRESS_DEV_publicApiHighScoresByScoreid",
                "API_ADDRESS_DEV_publicApiHighScoresByModeid",
                "API_ADDRESS_DEV_publicApiHighScoresCountByModeid",
                "API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayAll",
                "API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayFiltered",
                "API_ADDRESS_DEV_publicApiHighScoresByPlatform",
            };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(Constants).GetField(name, AnyDeclared), Is.Null,
                    "Constants." + name + " is a retired V1 high-score endpoint and must not exist.");
            }
        }

        [Test]
        public void DbHelperNoLongerExposesTheRetiredSubmittedStateMethods()
        {
            string[] retired = { "setGameScoreSubmitted", "getUnsubmittedHighScoreFromDatabase" };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(DBHelper).GetMethod(name, AnyDeclared), Is.Null,
                    "DBHelper." + name + " is retired V1 score transport and must not exist. Note: "
                        + "the underlying 'submittedToApi' SQLite column is intentionally kept as inert "
                        + "schema compatibility - only its reader/writer methods are retired.");
            }
        }

        [Test]
        public void StatsManagerNoLongerExposesTheLegacySubmitScoresWorkflow()
        {
            string[] retired = { "submitUnsubmittedScores", "SubmitUnsubmittedScoresCoroutine", "getUnsubmittedHighscores" };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(StatsManager).GetMethod(name, AnyDeclared), Is.Null,
                    "StatsManager." + name + " is the retired V1 manual submit workflow and must not exist.");
            }
        }

        [Test]
        public void StatsUiObjectsNoLongerExposesTheLegacySubmitScoresFields()
        {
            string[] retired = { "SubmittedHighscoresText", "NumUnsubmittedHighscoresText" };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(StatsUiObjects).GetProperty(name, AnyDeclared), Is.Null,
                    "StatsUiObjects." + name + " backed the retired Stats submit-scores UI and must not exist.");
            }
        }
    }
}
