using System;
using Assets.Scripts.database;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="BackendV2MatchResultAdapter"/>: the exact <see cref="HighScoreModel"/> -&gt;
    /// <see cref="SubmitMatchResultDto"/> field mapping this issue specifies, including the two
    /// fields whose local name does not match the wire name
    /// (<c>MaxShotMade</c>/<c>ShotsMade</c>, <c>ConsecutiveShots</c>/<c>LongestStreak</c>) and GUID
    /// reuse/validation of <c>Scoreid</c>.
    /// </summary>
    public class Level5BackendV2MatchResultAdapterTests
    {
        private static HighScoreModel ValidScore()
        {
            return new HighScoreModel
            {
                Scoreid = "9c1e2d3f4a5b4c6d8e7f0a1b2c3d4e5f",
                Modeid = 3,
                Levelid = 7,
                Characterid = 12,
                Version = "1.4.2",
                Platform = "Handheld",
                TotalPoints = 120,
                MaxShotMade = 18,
                TotalDistance = 342.5f,
                Time = 95.2f,
                ConsecutiveShots = 6,
                EnemiesKilled = 4,
                HardcoreEnabled = 0,
                TrafficEnabled = 1,
                EnemiesEnabled = 1,
                SniperEnabled = 0,
            };
        }

        [Test]
        public void TryAdaptReusesScoreidAsTheClientResultIdGuid()
        {
            HighScoreModel score = ValidScore();

            bool adapted = BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(adapted, Is.True);
            Assert.That(request.ClientResultId, Is.EqualTo(Guid.Parse(score.Scoreid)));
        }

        [Test]
        public void TryAdaptMapsModeLevelVersionAndPlatformDirectly()
        {
            HighScoreModel score = ValidScore();

            BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(request.ModeId, Is.EqualTo(score.Modeid));
            Assert.That(request.LevelId, Is.EqualTo(score.Levelid));
            Assert.That(request.ClientVersion, Is.EqualTo(score.Version));
            Assert.That(request.Platform, Is.EqualTo(score.Platform));
        }

        [Test]
        public void TryAdaptRepresentsCharacterIdAsAnInvariantCultureString()
        {
            HighScoreModel score = ValidScore();
            score.Characterid = 12;

            BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(request.CharacterId, Is.EqualTo("12"));
        }

        [Test]
        public void TryAdaptMapsAllSixMetrics()
        {
            HighScoreModel score = ValidScore();

            BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(request.Metrics["TotalPoints"], Is.EqualTo(120.0));
            Assert.That(request.Metrics["ShotsMade"], Is.EqualTo(18.0), "MaxShotMade -> ShotsMade");
            Assert.That(request.Metrics["TotalDistance"], Is.EqualTo(342.5).Within(0.0001));
            Assert.That(request.Metrics["CompletionTimeSeconds"], Is.EqualTo(95.2).Within(0.0001), "Time -> CompletionTimeSeconds");
            Assert.That(request.Metrics["LongestStreak"], Is.EqualTo(6.0), "ConsecutiveShots -> LongestStreak");
            Assert.That(request.Metrics["EnemiesKilled"], Is.EqualTo(4.0));
            Assert.That(request.Metrics, Has.Count.EqualTo(6));
        }

        [Test]
        public void TryAdaptMapsAllFourModifiersFromNonZeroFlags()
        {
            HighScoreModel score = ValidScore();
            score.HardcoreEnabled = 1;
            score.TrafficEnabled = 0;
            score.EnemiesEnabled = 1;
            score.SniperEnabled = 1;

            BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(request.Modifiers.Hardcore, Is.True);
            Assert.That(request.Modifiers.TrafficEnabled, Is.False);
            Assert.That(request.Modifiers.EnemiesEnabled, Is.True);
            Assert.That(request.Modifiers.SniperEnabled, Is.True);
        }

        [Test]
        public void TryAdaptReturnsFalseForAMalformedScoreid()
        {
            HighScoreModel score = ValidScore();
            score.Scoreid = "not-a-guid";

            bool adapted = BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(adapted, Is.False);
            Assert.That(request, Is.Null);
        }

        [Test]
        public void TryAdaptReturnsFalseForANullScoreid()
        {
            HighScoreModel score = ValidScore();
            score.Scoreid = null;

            bool adapted = BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request);

            Assert.That(adapted, Is.False);
            Assert.That(request, Is.Null);
        }

        [Test]
        public void TryAdaptReturnsFalseForANullScore()
        {
            bool adapted = BackendV2MatchResultAdapter.TryAdapt(null, out SubmitMatchResultDto request);

            Assert.That(adapted, Is.False);
            Assert.That(request, Is.Null);
        }
    }
}
