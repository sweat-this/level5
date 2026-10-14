using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2SeriesRowClassifierTests
    {
        private static SeriesResponseDto Series(int currentGameNumber, List<GameRoundViewDto> games)
        {
            return new SeriesResponseDto
            {
                Id = Guid.NewGuid(),
                ChallengerId = Guid.NewGuid(),
                OpponentId = Guid.NewGuid(),
                Status = "Active",
                TotalGames = 3,
                GamesToWin = 2,
                CurrentGameNumber = currentGameNumber,
                Revision = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                Games = games,
                Rules = new FrozenRulesDto { InformationPolicy = "SealedAttempt" },
            };
        }

        [Test]
        public void NeitherPlayerHasAttemptedTheCurrentGameYet_IsYourTurn()
        {
            SeriesResponseDto series = Series(1, new List<GameRoundViewDto>
            {
                new GameRoundViewDto { GameNumber = 1, YourAttempt = null, OpponentAttempt = null },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void YourAttemptIsCompleted_IsOpponentTurn()
        {
            SeriesResponseDto series = Series(1, new List<GameRoundViewDto>
            {
                new GameRoundViewDto
                {
                    GameNumber = 1,
                    YourAttempt = new AttemptViewDto { Id = Guid.NewGuid(), Status = "Completed", Result = new Dictionary<string, double> { ["Score"] = 10 } },
                    OpponentAttempt = null,
                },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.OpponentTurn));
        }

        [Test]
        public void YourAttemptWasStartedButNotCompleted_StillYourTurn()
        {
            // e.g. the player quit mid-attempt and reconnected - the attempt exists server-side but
            // was never finished, so it is still their turn to finish it.
            SeriesResponseDto series = Series(1, new List<GameRoundViewDto>
            {
                new GameRoundViewDto
                {
                    GameNumber = 1,
                    YourAttempt = new AttemptViewDto { Id = Guid.NewGuid(), Status = "Started", Result = null },
                    OpponentAttempt = null,
                },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void NoRoundForTheCurrentGameNumber_IsAnUnstartedTurn()
        {
            // Backend creates the next round lazily; complete detail with no current round is authoritative.
            SeriesResponseDto series = Series(2, new List<GameRoundViewDto>
            {
                new GameRoundViewDto { GameNumber = 1, YourAttempt = null, OpponentAttempt = null },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void OpponentAttemptResultIsNeverReadByClassification()
        {
            // Sealed-result guard: an opponent attempt that is present but whose Result the server
            // has not disclosed must not affect classification at all - only YourAttempt matters.
            SeriesResponseDto series = Series(1, new List<GameRoundViewDto>
            {
                new GameRoundViewDto
                {
                    GameNumber = 1,
                    YourAttempt = null,
                    OpponentAttempt = new AttemptViewDto { Id = Guid.NewGuid(), Status = "Completed", Result = null },
                },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void MissingGamesOrPolicyRemainUnknown()
        {
            var series = Series(1, null);
            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.Unknown));
            series.Games = new List<GameRoundViewDto>(); series.Rules = null;
            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.Unknown));
        }

        [Test]
        public void OpenTargetResponderWaitsForPublicFirstMoverCompletion()
        {
            var series = Series(1, new List<GameRoundViewDto>());
            series.Rules.InformationPolicy = "OpenTarget";
            Assert.That(SeriesRowClassifier.ClassifyTurn(series, series.ChallengerId), Is.EqualTo(ActiveSeriesTurn.YourTurn));
            Assert.That(SeriesRowClassifier.ClassifyTurn(series, series.OpponentId), Is.EqualTo(ActiveSeriesTurn.OpponentTurn));
            series.Games.Add(new GameRoundViewDto { GameNumber = 1, OpponentAttempt = new AttemptViewDto { Status = "Completed", Result = null } });
            Assert.That(SeriesRowClassifier.ClassifyTurn(series, series.OpponentId), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void AlternatingFirstMoverIsAppliedToTheCurrentGame()
        {
            var series = Series(2, new List<GameRoundViewDto>());
            series.Rules.InformationPolicy = "OpenTarget"; series.Rules.AlternatesFirstAttempt = true;
            Assert.That(SeriesRowClassifier.ClassifyTurn(series, series.OpponentId), Is.EqualTo(ActiveSeriesTurn.YourTurn));
            Assert.That(SeriesRowClassifier.ClassifyTurn(series, series.ChallengerId), Is.EqualTo(ActiveSeriesTurn.OpponentTurn));
        }

        [Test]
        public void NullSeriesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => SeriesRowClassifier.ClassifyTurn(null));
        }
    }
}
