namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Representative Backend V2 wire JSON, hand-derived from the shipped controller DTOs
    /// (Level5Backend/v2/src/Level5.Api/Controllers/*.cs) - camelCase, exactly as ASP.NET Core's
    /// default System.Text.Json serializer would emit them.
    /// </summary>
    public static class BackendV2Fixtures
    {
        public const string TokenResponse = @"{
            ""accessToken"": ""eyJhbGciOiJIUzI1NiJ9.fake.token"",
            ""expiresAt"": ""2026-09-17T12:15:00+00:00"",
            ""playerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
            ""refreshToken"": ""r-8f14e45f-ceea-467e"",
            ""refreshTokenExpiresAt"": ""2026-10-17T12:00:00+00:00""
        }";

        public const string ProblemDetailsNotFound = @"{
            ""type"": ""https://level5.game/errors/not_found"",
            ""title"": ""The series was not found."",
            ""status"": 404,
            ""code"": ""not_found"",
            ""traceId"": ""00-trace-1234-00""
        }";

        public const string ProblemDetailsConflict = @"{
            ""type"": ""https://level5.game/errors/conflicting_attempt_result"",
            ""title"": ""The attempt already has a different result."",
            ""status"": 409,
            ""code"": ""conflicting_attempt_result"",
            ""traceId"": ""00-trace-5678-00""
        }";

        public const string PlayerProfile = @"{
            ""playerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
            ""displayName"": ""Ada"",
            ""tag"": ""ADA#1234""
        }";

        public const string FriendSummary = @"[{
            ""playerId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
            ""displayName"": ""Grace"",
            ""tag"": ""GRACE#5678"",
            ""friendsSince"": ""2026-08-01T00:00:00+00:00""
        }]";

        public const string FriendRequest = @"{
            ""id"": ""3b12b2c7-3f3e-4a0e-9c2f-8a7c2b1d4e5f"",
            ""fromPlayerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
            ""toPlayerId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
            ""status"": ""Pending""
        }";

        public const string SeriesDetail = @"{
            ""id"": ""4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a"",
            ""challengerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
            ""opponentId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
            ""status"": ""Active"",
            ""totalGames"": 3,
            ""gamesToWin"": 2,
            ""currentGameNumber"": 1,
            ""revision"": 4,
            ""winnerId"": null,
            ""createdAt"": ""2026-09-01T00:00:00+00:00"",
            ""completedAt"": null,
            ""rules"": {
                ""competitionProtocolVersion"": 1,
                ""rulesetId"": ""most-points"",
                ""rulesetVersion"": 1,
                ""minimumCompatibleVersion"": 1,
                ""modeId"": ""most-points"",
                ""informationPolicy"": ""SealedAttempt"",
                ""alternatesFirstAttempt"": true,
                ""comparisonKeys"": [{ ""metric"": ""Score"", ""direction"": ""HigherWins"" }]
            },
            ""games"": [
                { ""gameNumber"": 1, ""yourAttempt"": null, ""opponentAttempt"": null }
            ]
        }";

        public const string SeriesSummaryPage = @"{
            ""items"": [{
                ""id"": ""4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a"",
                ""challengerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
                ""opponentId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
                ""status"": ""Active"",
                ""currentGameNumber"": 1,
                ""totalGames"": 3,
                ""revision"": 4,
                ""createdAt"": ""2026-09-01T00:00:00+00:00""
            }],
            ""limit"": 20,
            ""nextCursor"": ""opaque-cursor-token""
        }";

        public const string AttemptDescriptor = @"{
            ""seriesId"": ""4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a"",
            ""attemptId"": ""5d34d4e9-5151-4c2f-9e4f-0a9e4d3f6a7b"",
            ""gameNumber"": 1,
            ""playerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
            ""competitionProtocolVersion"": 1,
            ""rulesetId"": ""most-points"",
            ""rulesetVersion"": 1,
            ""minimumCompatibleVersion"": 1,
            ""modeId"": ""most-points"",
            ""informationPolicy"": ""SealedAttempt"",
            ""totalGames"": 3,
            ""gamesToWin"": 2,
            ""comparisonKeys"": [{ ""metric"": ""Score"", ""direction"": ""HigherWins"" }],
            ""requiredResultMetrics"": [""Score""]
        }";

        public const string CompleteAttemptRequestBody = @"{ ""metrics"": { ""Score"": 42.0 } }";
    }
}
