using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Backend V2 leaderboard cutover: <see cref="LeaderboardEntryPresentationMapper"/> converts a
    /// wire <see cref="LeaderboardEntryDto"/> into the Stats screen's existing row fields. Character
    /// and level name resolution are exercised via injected fake lookups rather than
    /// <c>LoadedData.instance</c>, so this needs no loaded scene.
    /// </summary>
    public class LeaderboardEntryPresentationMapperTests
    {
        private static LeaderboardEntryDto Entry(
            string displayName = "Ada",
            string characterId = "12",
            int levelId = 7,
            double value = 120.0,
            bool hardcore = false)
        {
            return new LeaderboardEntryDto
            {
                MatchResultId = Guid.NewGuid(),
                Player = new PlayerProfileResponseDto { PlayerId = Guid.NewGuid(), DisplayName = displayName, Tag = "ADA#1234" },
                CharacterId = characterId,
                LevelId = levelId,
                Value = value,
                CreatedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
                Modifiers = new MatchResultModifiersDto { Hardcore = hardcore }
            };
        }

        [Test]
        public void MapsDisplayNameIntoTheUserNameField()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(displayName: "Grace"), "TotalPoints", id => null, id => null);

            Assert.That(row.UserName, Is.EqualTo("Grace"));
        }

        [TestCase("TotalPoints")]
        [TestCase("ShotsMade")]
        [TestCase("LongestStreak")]
        [TestCase("EnemiesKilled")]
        public void CountLikeMetricsFormatAsPlainIntegers(string metric)
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(value: 42.0), metric, id => null, id => null);

            Assert.That(row.Score, Is.EqualTo("42"));
        }

        [TestCase("TotalDistance")]
        [TestCase("CompletionTimeSeconds")]
        public void ContinuousMetricsKeepConciseDecimalPrecision(string metric)
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(value: 95.23), metric, id => null, id => null);

            Assert.That(row.Score, Is.EqualTo("95.23"));
        }

        [Test]
        public void AnUnknownFutureMetricFormatsSafelyInsteadOfThrowing()
        {
            Assert.DoesNotThrow(() => LeaderboardEntryPresentationMapper.Map(
                Entry(value: 7.5), "SomeFutureMetric", id => null, id => null));
        }

        [Test]
        public void KnownCharacterIdResolvesThroughTheInjectedLookup()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(characterId: "12"), "TotalPoints", id => id == 12 ? "Skywalker" : null, id => null);

            Assert.That(row.Character, Is.EqualTo("Skywalker"));
        }

        [Test]
        public void UnknownCharacterIdFallsBackToTheRawServerValueRatherThanDroppingTheRow()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(characterId: "999"), "TotalPoints", id => null, id => null);

            Assert.That(row.Character, Is.EqualTo("999"));
        }

        [Test]
        public void NonNumericCharacterIdFallsBackToTheRawServerValue()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(characterId: "not-a-number"), "TotalPoints", id => "Should not be called", id => null);

            Assert.That(row.Character, Is.EqualTo("not-a-number"));
        }

        [Test]
        public void KnownLevelIdResolvesThroughTheInjectedLookup()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(levelId: 7), "TotalPoints", id => null, id => id == 7 ? "Downtown Court" : null);

            Assert.That(row.Level, Is.EqualTo("Downtown Court"));
        }

        [Test]
        public void UnknownLevelIdFallsBackToALevelIdLabel()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(levelId: 404), "TotalPoints", id => null, id => null);

            Assert.That(row.Level, Is.EqualTo("Level 404"));
        }

        [Test]
        public void ServerTimestampIsRenderedAsLocalDisplayTimeNotTheLegacyLocalDate()
        {
            DateTimeOffset createdAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
            LeaderboardEntryDto entry = Entry();
            entry.CreatedAt = createdAt;

            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                entry, "TotalPoints", id => null, id => null);

            Assert.That(row.Date, Is.EqualTo(createdAt.ToLocalTime().ToString()));
        }

        [Test]
        public void HardcoreTrueRendersOn()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(hardcore: true), "TotalPoints", id => null, id => null);

            Assert.That(row.HardcoreEnabled, Is.EqualTo("ON"));
        }

        [Test]
        public void HardcoreFalseRendersOff()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                Entry(hardcore: false), "TotalPoints", id => null, id => null);

            Assert.That(row.HardcoreEnabled, Is.EqualTo("OFF"));
        }

        [Test]
        public void ANullEntryMapsToAllBlankFieldsRatherThanThrowing()
        {
            LeaderboardRowPresentation row = LeaderboardEntryPresentationMapper.Map(
                null, "TotalPoints", id => null, id => null);

            Assert.That(row.UserName, Is.EqualTo(""));
            Assert.That(row.Score, Is.EqualTo(""));
            Assert.That(row.Character, Is.EqualTo(""));
            Assert.That(row.Level, Is.EqualTo(""));
            Assert.That(row.Date, Is.EqualTo(""));
            Assert.That(row.HardcoreEnabled, Is.EqualTo(""));
        }
    }
}
