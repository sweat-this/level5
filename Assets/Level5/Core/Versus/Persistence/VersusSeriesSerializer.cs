using System;
using System.Collections.Generic;
using System.Globalization;
using Level5.Core.Match;
using UnityEngine;

namespace Level5.Core.Versus.Persistence
{
    /// <summary>
    /// Converts a series to and from its stored form.
    ///
    /// It lives here rather than in the repository so that every repository - the in-memory one, the
    /// file-backed one, a remote one later - stores the same bytes. A store that invented its own
    /// mapping would be a second definition of what a series is.
    ///
    /// Reading is deliberately forgiving about unknown names and strict about missing structure: an
    /// enum member this build does not have falls back to a documented default, but a series whose
    /// game count disagrees with its snapshot is refused, because that is corruption rather than
    /// age.
    /// </summary>
    public static class VersusSeriesSerializer
    {
        private const string TimeFormat = "o";

        public static string ToJson(VersusSeries series, bool archived = false, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(ToDocument(series, archived), prettyPrint);
        }

        /// <summary>Reads a series back. Returns null for empty input; throws for corrupt input.</summary>
        public static VersusSeries FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            VersusSeriesDocument document = JsonUtility.FromJson<VersusSeriesDocument>(json);
            return document == null ? null : FromDocument(document);
        }

        /// <summary>Reads only the listable facts, without rebuilding the series.</summary>
        public static SeriesSummary SummaryFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            VersusSeriesDocument document = JsonUtility.FromJson<VersusSeriesDocument>(json);
            return document == null ? null : ToSummary(document);
        }

        public static VersusSeriesDocument ToDocument(VersusSeries series, bool archived = false)
        {
            if (series == null)
            {
                throw new VersusDomainException("there is no series to store");
            }

            SeriesSnapshot snapshot = series.Snapshot;
            VersusSeriesDocument document = new VersusSeriesDocument
            {
                documentVersion = 1,
                seriesId = series.Id.Value,
                status = series.Status.ToString(),
                mode = series.Mode.ToString(),
                informationPolicy = snapshot.InformationPolicy.ToString(),
                gameCount = snapshot.GameCount,
                alternatesFirstAttempt = snapshot.AlternatesFirstAttempt,
                snapshotFormatVersion = snapshot.FormatVersion,
                createdAtUtc = ToStoredTime(series.CreatedAtUtc),
                archived = archived,
                first = ToDocument(series.Participants.First),
                second = ToDocument(series.Participants.Second),
                rulesets = new VersusRulesetDocument[snapshot.GameCount],
                games = new VersusGameDocument[series.Games.Count],
                result = ToDocument(series.Result)
            };

            for (int index = 0; index < snapshot.GameCount; index++)
            {
                document.rulesets[index] = ToDocument(snapshot.GameAt(index));
            }

            for (int index = 0; index < series.Games.Count; index++)
            {
                document.games[index] = ToDocument(series.Games[index]);
            }

            return document;
        }

        public static VersusSeries FromDocument(VersusSeriesDocument document)
        {
            if (document == null)
            {
                throw new VersusDomainException("there is no series document to read");
            }

            if (document.rulesets == null || document.rulesets.Length == 0)
            {
                throw new VersusDomainException(
                    $"series '{document.seriesId}' was stored without its frozen rules, so it cannot be scored");
            }

            List<CompetitiveRuleset> rulesets = new List<CompetitiveRuleset>(document.rulesets.Length);
            foreach (VersusRulesetDocument stored in document.rulesets)
            {
                rulesets.Add(FromDocument(stored));
            }

            SeriesSnapshot snapshot = new SeriesSnapshot(
                SeriesFormat.FromGameCount(document.gameCount > 0 ? document.gameCount : rulesets.Count),
                rulesets,
                ParseEnum(document.informationPolicy, InformationPolicy.SealedAttempt),
                document.alternatesFirstAttempt,
                document.snapshotFormatVersion > 0 ? document.snapshotFormatVersion : SeriesSnapshot.CurrentFormatVersion);

            VersusParticipants participants = new VersusParticipants(
                FromDocument(document.first),
                FromDocument(document.second));

            List<VersusGame> games = new List<VersusGame>();
            if (document.games != null)
            {
                foreach (VersusGameDocument stored in document.games)
                {
                    games.Add(FromDocument(stored, snapshot));
                }
            }

            return VersusSeries.Restore(
                new SeriesId(document.seriesId),
                snapshot,
                participants,
                ParseEnum(document.mode, VersusMode.LocalAlternating),
                ParseEnum(document.status, SeriesStatus.Active),
                FromStoredTime(document.createdAtUtc) ?? DateTime.MinValue,
                FromDocument(document.result, participants),
                games);
        }

        public static SeriesSummary ToSummary(VersusSeriesDocument document)
        {
            int firstWins = 0;
            int secondWins = 0;
            int currentGameNumber = 0;
            string firstId = document.first == null ? string.Empty : document.first.participantId;

            if (document.games != null)
            {
                foreach (VersusGameDocument game in document.games)
                {
                    if (game == null)
                    {
                        continue;
                    }

                    if (ParseEnum(game.status, VersusGameStatus.Pending) == VersusGameStatus.Active)
                    {
                        currentGameNumber = game.index + 1;
                    }

                    if (game.result == null || string.IsNullOrEmpty(game.result.winnerId))
                    {
                        continue;
                    }

                    if (game.result.winnerId == firstId)
                    {
                        firstWins++;
                    }
                    else
                    {
                        secondWins++;
                    }
                }
            }

            return new SeriesSummary(
                new SeriesId(document.seriesId),
                ParseEnum(document.status, SeriesStatus.Active),
                ParseEnum(document.mode, VersusMode.LocalAlternating),
                SeriesFormat.FromGameCount(document.gameCount > 0 ? document.gameCount : 1),
                new ParticipantId(firstId),
                document.first == null ? string.Empty : document.first.displayName,
                new ParticipantId(document.second == null ? string.Empty : document.second.participantId),
                document.second == null ? string.Empty : document.second.displayName,
                firstWins,
                secondWins,
                currentGameNumber,
                FromStoredTime(document.createdAtUtc) ?? DateTime.MinValue,
                document.result == null ? null : FromStoredTime(document.result.completedAtUtc),
                document.archived);
        }

        // ---- participants ---------------------------------------------------------------------

        private static VersusParticipantDocument ToDocument(MatchParticipant participant)
        {
            return new VersusParticipantDocument
            {
                participantId = participant.Id.Value,
                displayName = participant.DisplayName,
                kind = participant.Kind.ToString()
            };
        }

        private static MatchParticipant FromDocument(VersusParticipantDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.participantId))
            {
                throw new VersusDomainException("a stored series is missing one of its participants");
            }

            return new MatchParticipant(
                new ParticipantId(document.participantId),
                document.displayName,
                ParseEnum(document.kind, ParticipantKind.LocalHuman));
        }

        // ---- rulesets -------------------------------------------------------------------------

        private static VersusRulesetDocument ToDocument(CompetitiveRuleset ruleset)
        {
            List<string> capabilities = new List<string>();
            foreach (VersusCapability capability in AllCapabilities)
            {
                if (ruleset.Supports(capability))
                {
                    capabilities.Add(capability.ToString());
                }
            }

            VersusComparisonKeyDocument[] keys = new VersusComparisonKeyDocument[ruleset.ComparisonKeys.Count];
            for (int index = 0; index < keys.Length; index++)
            {
                ComparisonKey key = ruleset.ComparisonKeys[index];
                keys[index] = new VersusComparisonKeyDocument
                {
                    metric = key.Metric.ToString(),
                    direction = key.Direction.ToString()
                };
            }

            return new VersusRulesetDocument
            {
                rulesetId = ruleset.Id.Value,
                version = ruleset.Version,
                minimumCompatibleVersion = ruleset.MinimumCompatibleVersion,
                modeId = GameModeIds.ToInt(ruleset.ModeId),
                displayName = ruleset.DisplayName,
                capabilities = capabilities.ToArray(),
                comparisonKeys = keys
            };
        }

        private static CompetitiveRuleset FromDocument(VersusRulesetDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.rulesetId))
            {
                throw new VersusDomainException("a stored series is missing the rules for one of its games");
            }

            VersusCapability capabilities = VersusCapability.None;
            if (document.capabilities != null)
            {
                foreach (string name in document.capabilities)
                {
                    capabilities |= ParseEnum(name, VersusCapability.None);
                }
            }

            List<ComparisonKey> keys = new List<ComparisonKey>();
            if (document.comparisonKeys != null)
            {
                foreach (VersusComparisonKeyDocument key in document.comparisonKeys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    keys.Add(new ComparisonKey(
                        ParseEnum(key.metric, AttemptMetric.Score),
                        ParseEnum(key.direction, MetricDirection.HigherWins)));
                }
            }

            if (keys.Count == 0)
            {
                throw new VersusDomainException(
                    $"stored ruleset '{document.rulesetId}' has no comparison keys, so it cannot decide a winner");
            }

            return new CompetitiveRuleset(
                new RulesetId(document.rulesetId),
                document.version,
                GameModeIds.FromInt(document.modeId),
                capabilities,
                keys,
                document.minimumCompatibleVersion < 1 ? 1 : document.minimumCompatibleVersion,
                document.displayName);
        }

        // ---- games and attempts ---------------------------------------------------------------

        private static VersusGameDocument ToDocument(VersusGame game)
        {
            IReadOnlyList<Attempt> attempts = game.AttemptsForPersistence;
            VersusAttemptDocument[] stored = new VersusAttemptDocument[attempts.Count];
            for (int index = 0; index < attempts.Count; index++)
            {
                stored[index] = ToDocument(attempts[index]);
            }

            return new VersusGameDocument
            {
                index = game.Index,
                status = game.Status.ToString(),
                firstAttemptParticipantIndex = game.FirstAttemptParticipantIndex,
                attempts = stored,
                result = ToDocument(game.Result)
            };
        }

        private static VersusGame FromDocument(VersusGameDocument document, SeriesSnapshot snapshot)
        {
            if (document == null)
            {
                throw new VersusDomainException("a stored series contains an empty game");
            }

            List<Attempt> attempts = new List<Attempt>();
            if (document.attempts != null)
            {
                foreach (VersusAttemptDocument stored in document.attempts)
                {
                    if (stored != null)
                    {
                        attempts.Add(FromDocument(stored));
                    }
                }
            }

            return VersusGame.Restore(
                document.index,
                snapshot.GameAt(document.index),
                snapshot.InformationPolicy,
                document.firstAttemptParticipantIndex,
                ParseEnum(document.status, VersusGameStatus.Pending),
                FromDocument(document.result),
                attempts);
        }

        private static VersusAttemptDocument ToDocument(Attempt attempt)
        {
            return new VersusAttemptDocument
            {
                attemptId = attempt.Id.Value,
                participantId = attempt.ParticipantId.Value,
                gameIndex = attempt.GameIndex,
                rulesetId = attempt.RulesetId.Value,
                rulesetVersion = attempt.RulesetVersion,
                state = attempt.State.ToString(),
                issuedAtUtc = ToStoredTime(attempt.IssuedAtUtc),
                startedAtUtc = ToStoredTime(attempt.StartedAtUtc),
                completedAtUtc = ToStoredTime(attempt.CompletedAtUtc),
                result = ToDocument(attempt.Result)
            };
        }

        private static Attempt FromDocument(VersusAttemptDocument document)
        {
            return Attempt.Restore(
                new AttemptId(document.attemptId),
                new ParticipantId(document.participantId),
                document.gameIndex,
                new RulesetId(document.rulesetId),
                document.rulesetVersion,
                ParseEnum(document.state, AttemptState.Created),
                FromStoredTime(document.issuedAtUtc) ?? DateTime.MinValue,
                FromStoredTime(document.startedAtUtc),
                FromStoredTime(document.completedAtUtc),
                FromDocument(document.result));
        }

        private static VersusAttemptResultDocument ToDocument(AttemptResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new VersusAttemptResultDocument
            {
                rulesetId = result.RulesetId.Value,
                rulesetVersion = result.RulesetVersion,
                metrics = result.ToArray()
            };
        }

        private static AttemptResult FromDocument(VersusAttemptResultDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.rulesetId))
            {
                return null;
            }

            return AttemptResult.FromValues(
                new RulesetId(document.rulesetId),
                document.rulesetVersion,
                document.metrics);
        }

        // ---- results --------------------------------------------------------------------------

        private static VersusGameResultDocument ToDocument(GameResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new VersusGameResultDocument
            {
                kind = result.Kind.ToString(),
                winnerId = result.WinnerId.Value,
                resolvedAtUtc = ToStoredTime(result.ResolvedAtUtc)
            };
        }

        private static GameResult FromDocument(VersusGameResultDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.kind))
            {
                return null;
            }

            return new GameResult(
                ParseEnum(document.kind, GameOutcomeKind.Decided),
                new ParticipantId(document.winnerId),
                FromStoredTime(document.resolvedAtUtc) ?? DateTime.MinValue);
        }

        private static VersusSeriesResultDocument ToDocument(SeriesResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new VersusSeriesResultDocument
            {
                kind = result.Kind.ToString(),
                winnerId = result.WinnerId.Value,
                firstWins = result.Score.FirstWins,
                secondWins = result.Score.SecondWins,
                draws = result.Score.Draws,
                completedAtUtc = ToStoredTime(result.CompletedAtUtc)
            };
        }

        private static SeriesResult FromDocument(VersusSeriesResultDocument document, VersusParticipants participants)
        {
            if (document == null || string.IsNullOrEmpty(document.kind))
            {
                return null;
            }

            ParticipantId winner = new ParticipantId(document.winnerId);
            if (winner.HasValue && !participants.Contains(winner))
            {
                throw new VersusDomainException(
                    $"a stored series names '{winner}' as its winner, who is not one of its participants");
            }

            return new SeriesResult(
                ParseEnum(document.kind, SeriesOutcomeKind.Decided),
                winner,
                new SeriesScore(document.firstWins, document.secondWins, document.draws),
                FromStoredTime(document.completedAtUtc) ?? DateTime.MinValue);
        }

        // ---- primitives -----------------------------------------------------------------------

        private static readonly VersusCapability[] AllCapabilities =
        {
            VersusCapability.LocalSimultaneous,
            VersusCapability.LocalAlternating,
            VersusCapability.Asynchronous,
            VersusCapability.OnlineRealtime
        };

        private static string ToStoredTime(DateTime? value)
        {
            return value.HasValue ? ToStoredTime(value.Value) : string.Empty;
        }

        private static string ToStoredTime(DateTime value)
        {
            return value.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
        }

        private static DateTime? FromStoredTime(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            // RoundtripKind alone. The stored form is the "o" format, which ends in Z for a UTC
            // time, so the kind comes back from the text itself - and RoundtripKind may not be
            // combined with AdjustToUniversal, which throws rather than being ignored.
            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed))
            {
                // A document hand-edited without its Z still has to come back as UTC: every time
                // this domain writes is UTC, and a local-kind time would drift by the offset.
                return parsed.Kind == DateTimeKind.Utc
                    ? parsed
                    : DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);
            }

            return null;
        }

        /// <summary>
        /// Reads an enum by name, falling back rather than throwing.
        ///
        /// A name this build does not know means the document came from a build that had a member
        /// this one does not. Falling back to the documented default keeps the rest of the series
        /// readable; refusing the whole document would lose a competition over one unknown word.
        /// </summary>
        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            return Enum.TryParse(value, false, out TEnum parsed) ? parsed : fallback;
        }
    }
}
