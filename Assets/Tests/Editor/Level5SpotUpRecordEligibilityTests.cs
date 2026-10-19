using Level5.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Assets.Scripts.database;
using Level5.BackendV2;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class Level5SpotUpRecordEligibilityTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
    private DBHelperTestHarness.Session database;
    private GameOptionsSnapshot options;
    private GameRules previousRules;
    private MatchController previousController;
    private GameLevelManager previousManager;
    private BackendV2Session previousSession;
    private GameRules rules;
    private GameStats stats;
    private Guid owner;

    [SetUp]
    public void SetUp()
    {
        options = GameOptionsSnapshot.Capture();
        previousRules = GameRules.instance;
        previousController = MatchController.instance;
        previousManager = GameLevelManager.instance;
        previousSession = BackendV2SessionStore.Current;
        GameRules.instance = null;
        MatchController.instance = null;
        GameLevelManager.instance = null;
        ActiveMatch.Clear();
        ActiveRemoteAttempt.Clear();
        ActiveVersusAttempt.Clear();
        foreach (string name in new[] { "pending-match-persistence.json", "backendv2_pending_match_results.json" })
        {
            foreach (string suffix in new[] { "", ".bak" })
            {
                string path = Path.Combine(Application.persistentDataPath, name + suffix);
                files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                if (File.Exists(path)) File.Delete(path);
            }
        }
        database = DBHelperTestHarness.Create();
        DBConnector.instance = database.Connector;
        DBHelper.instance = database.Helper;
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = "spot-up-record-test-" + Guid.NewGuid().ToString("N");
        owner = Guid.NewGuid();
        BackendV2SessionStore.Set(new BackendV2Session("test-access", DateTimeOffset.UtcNow.AddHours(1),
            owner, "test-refresh", DateTimeOffset.UtcNow.AddDays(1)));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject obj in objects) UnityEngine.Object.DestroyImmediate(obj);
        objects.Clear();
        DBHelperTestHarness.Destroy(database);
        GameRules.instance = previousRules;
        MatchController.instance = previousController;
        GameLevelManager.instance = previousManager;
        BackendV2SessionStore.Clear();
        if (previousSession != null) BackendV2SessionStore.Set(previousSession);
        ActiveMatch.Clear();
        ActiveRemoteAttempt.Clear();
        ActiveVersusAttempt.Clear();
        options.Restore();
        foreach (var file in files)
        {
            if (file.Value == null)
            {
                if (File.Exists(file.Key)) File.Delete(file.Key);
            }
            else File.WriteAllBytes(file.Key, file.Value);
        }
        files.Clear();
    }

    private GameObject Spawn(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        return obj;
    }

    private static object Invoke(object target, string method, params object[] arguments)
        => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);

    private static T Read<T>(object target, string field)
        => (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);

    private void Begin(GameModeId mode, bool initialize = true, int markerCount = 2)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{(int)mode:00}.prefab");
        Assert.That(source, Is.Not.Null);
        var definition = GameModeDefinitionFactory.Create(source.GetComponent<StartScreenModeSelected>());
        var level = TestDefinitions.Level(1, ArenaCapability.Basketball | ArenaCapability.SevenPointLine);
        var roster = TestDefinitions.SoloRoster();
        var resolved = MatchConfigurationBuilder.Resolve(definition, level, roster, MatchModifiers.Default);
        ActiveMatch.Begin(new MatchConfiguration(definition, level, roster, MatchModifiers.Default,
            resolved, CheerleaderSelection.None, "Spot-Up records regression"));
        MatchSession.BeginNewMatch();
        MatchController controller = Spawn("controller").AddComponent<MatchController>();
        Invoke(controller, "Awake"); // Unity does not run ordinary Awake automatically in EditMode.
        for (int index = 0; index < markerCount; index++)
        {
            GameObject obj = Spawn("required-marker-" + index);
            obj.tag = "shot_marker";
            var marker = obj.AddComponent<BasketBallShotMarker>();
            marker.shotTypeThree = resolved.RequiresShotMarkers3s;
            marker.shotTypeFour = !marker.shotTypeThree && resolved.RequiresShotMarkers4s;
            marker.shotTypeSeven = !marker.shotTypeThree && !marker.shotTypeFour;
        }
        LogAssert.Expect(LogType.Error, "GameRules could not bind money-ball state to any basketball: no participant registry.");
        rules = Spawn("rules").AddComponent<GameRules>();
        Invoke(rules, "Awake"); // Actual Awake subscribes to actual controller.
        rules.GameModeId = (int)mode;
        rules.BasketBallShotMarkersList = new List<BasketBallShotMarker>();
        stats = Spawn("stats").AddComponent<GameStats>();
        stats.BindMatchRules(resolved);
        stats.TotalPoints = 21;
        stats.SevenPointerMade = 3;
        stats.SevenPointerAttempts = 5;
        stats.TimePlayed = 12.5f;
        if (initialize) Invoke(rules, "SetPositionMarkers");
    }

    private void ClearCourse()
    {
        IShotMarkerSession session = rules;
        while (session.MarkersRemaining > 0) session.RecordMarkerCompleted();
        session.RequestMatchEnd(); // Production compatibility path accepts Unknown.
    }

    private HighScoreModel Score() => new HighScoreModel
    {
        Scoreid = Guid.NewGuid().ToString("N"), Modeid = rules.GameModeId, Levelid = 1,
        Characterid = 1, Version = "regression", Platform = "Desktop", Time = stats.TimePlayed,
        TotalPoints = stats.TotalPoints, SevenMade = stats.SevenPointerMade, SevenAtt = stats.SevenPointerAttempts
    };

    private bool Save(HighScoreModel score) => (bool)Invoke(rules, "SaveMatchResults", score, stats);

    private int Scalar(string sql)
    {
        using (var command = database.Helper.Connection.CreateCommand())
        {
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    private static PendingMatchPersistenceData Pending()
        => (PendingMatchPersistenceData)typeof(PendingMatchPersistenceStore)
            .GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

    [TestCase(GameModeId.SpotUp3s)]
    [TestCase(GameModeId.SpotUp4s)]
    [TestCase(GameModeId.SpotUpAll)]
    [TestCase(GameModeId.SpotUp7s)]
    public void CompletedCourseSavesExactlyOneTimeRecordAndQueuesForTheAuthenticatedPlayer(GameModeId mode)
    {
        Begin(mode);
        ClearCourse();
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.Unknown));
        HighScoreModel score = Score();
        Assert.That(Save(score), Is.True);
        Assert.That(Save(Score()), Is.True); // A retry with a fresh DTO must not write twice.
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores WHERE time > 0"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT totalPoints FROM AllTimeStats"), Is.EqualTo(21));
        Assert.That(Pending().scores, Is.Empty);
        var queued = PendingMatchResultStore.GetRetryable(owner);
        Assert.That(queued, Has.Count.EqualTo(1));
        Assert.That(queued[0].Request.ClientResultId, Is.EqualTo(Guid.Parse(score.Scoreid)));
        Assert.That(queued[0].OwnerPlayerId, Is.EqualTo(owner));
    }

    [TestCase(GameModeId.SpotUp3s, MatchEndCause.PlayerDied)]
    [TestCase(GameModeId.SpotUp4s, MatchEndCause.PlayerDied)]
    [TestCase(GameModeId.SpotUpAll, MatchEndCause.PlayerDied)]
    [TestCase(GameModeId.SpotUp7s, MatchEndCause.PlayerDied)]
    [TestCase(GameModeId.SpotUp7s, MatchEndCause.TimeExpired)]
    [TestCase(GameModeId.SpotUp7s, MatchEndCause.Unknown)]
    public void IncompleteCourseNeverSavesOrQueuesAScoreButSavesStats(GameModeId mode, MatchEndCause cause)
    {
        Begin(mode);
        Assert.That(rules.RequestEnd(new MatchEndReason(cause)), Is.True);
        ClearCourse(); // Late callbacks cannot turn the failed attempt into a completion.
        Assert.That(rules.RequestEnd(MatchEndReason.ObjectiveComplete), Is.False);
        Assert.That(rules.RequestEnd(MatchEndReason.TimeExpired), Is.False);
        Assert.That(rules.RequestEnd(MatchEndReason.PlayerDied), Is.False);
        Assert.That(Save(Score()), Is.True);
        Assert.That(Save(Score()), Is.True);
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(cause));
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.Zero);
        Assert.That(Pending().scores, Is.Empty);
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
        Assert.That(Scalar("SELECT totalPoints FROM AllTimeStats"), Is.EqualTo(21));
        Assert.That(Read<bool>(rules, "matchScoreSaveCompleted"), Is.True);
    }

    [TestCase(false, 2)]
    [TestCase(true, 0)]
    public void MissingOrEmptyInitializedCourseCannotQualify(bool initialize, int count)
    {
        Begin(GameModeId.SpotUp7s, initialize, count);
        rules.RequestGameOver();
        Assert.That(Save(Score()), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.Zero);
        Assert.That(Pending().scores, Is.Empty);
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
    }

    [Test]
    public void ExplicitObjectiveCompleteAlsoQualifiesForAClearedInitializedCourse()
    {
        Begin(GameModeId.SpotUp7s);
        rules.MarkersRemaining = 0;
        Assert.That(rules.RequestEnd(MatchEndReason.ObjectiveComplete), Is.True);
        Assert.That(Save(Score()), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(1));
    }

    [TestCase(MatchEndCause.PlayerDied)]
    [TestCase(MatchEndCause.TimeExpired)]
    [TestCase(MatchEndCause.Abandoned)]
    [TestCase(MatchEndCause.LastPlayerStanding)]
    [TestCase(MatchEndCause.CampaignRoundComplete)]
    public void UnsuccessfulCauseWinsEvenIfMarkersJustCleared(MatchEndCause cause)
    {
        Begin(GameModeId.SpotUp7s);
        rules.MarkersRemaining = 0;
        // Use the lifecycle owner directly to prove the capture is at its event boundary.
        Assert.That(MatchController.instance.RequestEnd(cause), Is.True);
        rules.RequestGameOver();
        Assert.That(Save(Score()), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.Zero);
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
    }

    [TestCase(MatchEndCause.PlayerDied)]
    [TestCase(MatchEndCause.TimeExpired)]
    public void CompletionWinsAgainstLaterFailureAndMarkerChanges(MatchEndCause laterCause)
    {
        Begin(GameModeId.SpotUp7s);
        ClearCourse();
        rules.MarkersRemaining = 2;
        Assert.That(rules.RequestEnd(new MatchEndReason(laterCause)), Is.False);
        Assert.That(Save(Score()), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(1));
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.Unknown));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SqliteFailureQueuesOnlyEligibleScoresAndAlwaysQueuesStats(bool completed)
    {
        Begin(GameModeId.SpotUp7s);
        if (completed) ClearCourse();
        else rules.RequestEnd(MatchEndReason.TimeExpired);
        database.Helper.DatabaseLocked = true; // Real SQLite save refuses; existing durable fallback runs.
        try
        {
            Assert.That(Save(Score()), Is.True);
            Assert.That(Save(Score()), Is.True);
            Assert.That(Pending().scores, Has.Count.EqualTo(completed ? 1 : 0));
            Assert.That(Pending().allTime, Has.Count.EqualTo(1));
            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(completed ? 1 : 0));
        }
        finally { database.Helper.DatabaseLocked = false; }
        Assert.That(PendingMatchPersistenceStore.Repair(), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(completed ? 1 : 0));
        Assert.That(Scalar("SELECT totalPoints FROM AllTimeStats"), Is.EqualTo(21));
    }

    [Test]
    public void NoSessionAtDurabilityDoesNotRetroactivelyQueueOnRetry()
    {
        Begin(GameModeId.SpotUp7s);
        ClearCourse();
        BackendV2Session session = BackendV2SessionStore.Current;
        BackendV2SessionStore.Clear();
        Assert.That(Save(Score()), Is.True);
        BackendV2SessionStore.Set(session);
        Assert.That(Save(Score()), Is.True);
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ScoreEligibilityDoesNotChangeProgressionAwardsOrIdempotency(bool completed)
    {
        Begin(GameModeId.SpotUp7s);
        string account = CharacterProgressAccountId.GetCurrent();
        using (var command = database.Helper.Connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO CharacterProfile (accountId, charid, experience, level) VALUES (@account, 1, 0, 0)";
            command.Parameters.AddWithValue("@account", account);
            command.ExecuteNonQuery();
        }
        if (completed) ClearCourse();
        else rules.RequestEnd(MatchEndReason.PlayerDied);
        Assert.That(Save(Score()), Is.True);
        int expected = (int)stats.getExperienceGainedFromSession();
        Assert.That(expected, Is.GreaterThan(0));
        Assert.That(Invoke(rules, "ApplyMatchProgressionResult", stats), Is.True);
        Assert.That(Invoke(rules, "ApplyMatchProgressionResult", stats), Is.True);
        Assert.That(Scalar("SELECT experience FROM CharacterProfile WHERE charid = 1"), Is.EqualTo(expected));
        Assert.That(Scalar("SELECT COUNT(*) FROM ProgressionResultLedger"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(completed ? 1 : 0));
        PendingProgressionStore.DeleteAccountFiles(account);
    }

    [TestCase(GameModeId.TotalPoints, 1)]
    [TestCase(GameModeId.SevenPointContest, 1)]
    [TestCase(GameModeId.FreePlay, 0)]
    [TestCase(GameModeId.BeatThaComputahs, 0)]
    public void NeighboringModesKeepExistingOrdinaryScoreAndStatsBehavior(GameModeId mode, int scores)
    {
        Begin(mode);
        rules.RequestEnd(MatchEndReason.PlayerDied);
        Assert.That(Save(Score()), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM HighScores"), Is.EqualTo(scores));
        Assert.That(Scalar("SELECT totalPoints FROM AllTimeStats"), Is.EqualTo(21));
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(scores));
    }
}
