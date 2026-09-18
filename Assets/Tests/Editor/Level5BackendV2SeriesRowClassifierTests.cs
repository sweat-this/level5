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
                    YourAttempt = new AttemptViewDto { Id = Guid.NewGuid(), Status = "Pending", Result = null },
                    OpponentAttempt = null,
                },
            });

            Assert.That(SeriesRowClassifier.ClassifyTurn(series), Is.EqualTo(ActiveSeriesTurn.YourTurn));
        }

        [Test]
        public void NoRoundForTheCurrentGameNumber_DefaultsToYourTurn()
        {
            // Ambiguous/defensive case: defaulting to YourTurn is the safe direction, since starting
            // an attempt that is not actually available is safely refused server-side, whereas
            // wrongly defaulting to OpponentTurn would hide an actionable turn from the player.
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
        public void NullSeriesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => SeriesRowClassifier.ClassifyTurn(null));
        }
    }
}
