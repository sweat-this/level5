using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.database;
using Level5.BackendV2;
using Level5.Core.Match;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Level5RangeShotRecordTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private DBHelperTestHarness.Session database;
    private GameOptionsSnapshot options;
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        options = GameOptionsSnapshot.Capture();
        database = DBHelperTestHarness.Create();
        DBHelper.instance = database.Helper;
        database.GameObject.tag = "database";
        root = new GameObject("range-record-test");
        root.SetActive(false); // No menu/PlayerData startup or live persistence.
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        DBHelperTestHarness.Destroy(database);
        options.Restore();
    }

    [TestCase(2, "threeMade")]
    [TestCase(3, "fourMade")]
    [TestCase(4, "sevenMade")]
    public void ProductionModeAuthorsQualifyingFieldAndPreservesRules(int id, string field)
    {
        StartScreenModeSelected source = Source(id);
        Assert.That(source.ModeId, Is.EqualTo(id));
        Assert.That(source.HighScoreField, Is.EqualTo(field));
        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        try
        {
            Assert.That(mode.RawModeId, Is.EqualTo(id));
            Assert.That(mode.HighScoreField, Is.EqualTo(field));
            Assert.That(mode.ClockMode, Is.EqualTo(MatchClockMode.Countdown));
            Assert.That(mode.CustomTimerSeconds, Is.Zero);
            Assert.That(mode.Objective, Is.EqualTo(MatchObjective.MakeCount));
            Assert.That(mode.ShotRule, Is.EqualTo(ShotRule.Any));
            Assert.That(mode.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.None));
            Assert.That(mode.CombatMode, Is.EqualTo(CombatMode.None));
            Assert.That(mode.RequiresBasketball, Is.True);
            Assert.That(mode.RequiresMoneyBall || mode.RequiresConsecutiveShots
                || mode.RequiresPlayerSurvive || mode.AllowsCpuShooters || mode.EnemiesOnly
                || mode.ArcadeMode || mode.RequiresCpuOpponent || mode.AddsImplicitDefender, Is.False);
            Assert.That(mode.MinPlayers, Is.EqualTo(1));
            Assert.That(mode.MaxPlayers, Is.EqualTo(PlayerRoster.MaxSlots));
            Assert.That(mode.Description, Is.EqualTo(source.ModeDescription));
        }
        finally { Object.DestroyImmediate(mode); }
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void SqliteRanksAndDisplaysQualifyingMakesWithTimeTieBreakAndHistoricalNull(int id)
    {
        string field = Source(id).HighScoreField;
        Insert(id, "wrong-range", 99, 2, 1);
        Insert(id, "qualifying-slow", 8, 8, 100);
        Insert(id, "qualifying-fast", 9, 8, 50);
        Insert(id, "zero", 199, 0, 0);
        Insert(id, "unknown", 999, 0, 0);
        Execute($"UPDATE HighScores SET {field} = NULL WHERE userName = 'unknown'");

        List<StatsTableHighScoreRow> rows = Rows(id);
        Assert.That(rows.Take(5).Select(row => row.UserName), Is.EqualTo(new[]
            { "qualifying-fast", "qualifying-slow", "wrong-range", "zero", "unknown" }));
        Assert.That(rows.Take(5).Select(row => row.Score), Is.EqualTo(new[] { "8", "8", "2", "0", "" }));
        Assert.That(database.Helper.getIntValueHighScoreFromTableByFieldAndModeId(
            "HighScores", field, id, "DESC", 0), Is.EqualTo(8));
        Assert.That(database.Helper.getNumberOfResults(field, id, false, false, false, false), Is.EqualTo(5));

        PlayerData data = root.AddComponent<PlayerData>();
        GameOptions.hardcoreModeEnabled = false;
        data.loadStatsFromDatabase();
        Assert.That(PersonalBest(data, id), Is.EqualTo(8));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void NullOnlyAndZeroOnlyHistoryDoNotInventSuccessfulCounts(int id)
    {
        string field = Source(id).HighScoreField;
        Insert(id, "unknown", 999, 0, 0);
        Execute($"UPDATE HighScores SET {field} = NULL");
        Assert.That(Rows(id)[0].Score, Is.Empty);
        PlayerData data = root.AddComponent<PlayerData>();
        GameOptions.hardcoreModeEnabled = false;
        data.loadStatsFromDatabase();
        Assert.That(PersonalBest(data, id), Is.Zero);
        Insert(id, "zero", 500, 0, 10);
        Assert.That(Rows(id).Take(2).Select(row => row.UserName), Is.EqualTo(new[] { "zero", "unknown" }));
        Assert.That(Rows(id)[0].Score, Is.EqualTo("0"));
        data.loadStatsFromDatabase();
        Assert.That(PersonalBest(data, id), Is.Zero);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void PersonalBestHardcoreAndLeaderboardFilterSemanticsRemainIndependent(int id)
    {
        Insert(id, "ordinary", 50, 5, 20);
        Insert(id, "hardcore", 60, 6, 20, 1);
        Insert(id, "all-filters", 70, 7, 20, 1, 1, 1, 1);
        Insert(id, "traffic-only", 80, 8, 20, 0, 1);
        Assert.That(Rows(id).Take(4).Select(row => row.UserName), Is.EqualTo(new[]
            { "traffic-only", "all-filters", "hardcore", "ordinary" }));
        Assert.That(Rows(id, true).First().UserName, Is.EqualTo("hardcore"));
        Assert.That(Rows(id, true, true, true, true).First().UserName, Is.EqualTo("all-filters"));
        Assert.That(Rows(id, false, true).First().UserName, Is.EqualTo("traffic-only"));
        Assert.That(database.Helper.getNumberOfResults(Source(id).HighScoreField, id, true, false, false, false), Is.EqualTo(1));
        PlayerData data = root.AddComponent<PlayerData>();
        GameOptions.hardcoreModeEnabled = false;
        data.loadStatsFromDatabase();
        Assert.That(PersonalBest(data, id), Is.EqualTo(8));
        GameOptions.hardcoreModeEnabled = true;
        data.loadStatsFromDatabase();
        Assert.That(PersonalBest(data, id), Is.EqualTo(7));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void StatsManagerUsesProductionModeSelectionAndBindsQualifyingSqliteRows(int id)
    {
        Insert(id, "wrong-range", 100, 1, 10);
        Insert(id, "qualifying", 8, 8, 20);
        StatsManager manager = root.AddComponent<StatsManager>();
        var modes = (List<StatsManager.mode>)typeof(StatsManager)
            .GetMethod("getModeSelectDataList", PrivateInstance).Invoke(manager, null);
        Set(manager, "modesList", modes);
        Set(manager, "currentModeSelectedIndex", modes.FindIndex(mode => mode.modeSelectedId == id));
        Set(manager, "localResultsPageNumber", 0);
        Set(manager, "modeSelectButtonText", Text("mode"));
        Set(manager, "pageNumberLocalSelectButtonText", Text("page"));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/stats/highScoreRow.prefab");
        var views = new List<GameObject>();
        for (int i = 0; i < 2; i++) views.Add(Object.Instantiate(prefab, root.transform));
        Set(manager, "highScoreRowsObjectsList", views);
        ReadRowsWithoutEditModeDestroyErrors(() => { manager.changeHighScoreDataDisplay(); return true; });
        StatsTableHighScoreRow first = views[0].GetComponent<StatsTableHighScoreRow>();
        Assert.That(first.UserName, Is.EqualTo("qualifying"));
        Assert.That(first.scoreLabel.text, Is.EqualTo("8"));
        Assert.That(views[1].GetComponent<StatsTableHighScoreRow>().scoreLabel.text, Is.EqualTo("1"));
        Assert.That(database.Helper.DatabaseLocked, Is.False);

        Insert(id, "filtered", 99, 9, 30, 1, 1);
        Set(manager, "hardcoreEnabled", true);
        Set(manager, "trafficEnabled", true);
        ReadRowsWithoutEditModeDestroyErrors(() => { manager.changeHighScoreDataDisplay(); return true; });
        Assert.That(first.UserName, Is.EqualTo("filtered"));
        Assert.That(first.scoreLabel.text, Is.EqualTo("9"));
        Assert.That(views[1].GetComponent<StatsTableHighScoreRow>().scoreLabel.text, Is.Empty);
        Assert.That(((TextMeshProUGUI)typeof(StatsManager).GetField("pageNumberLocalSelectButtonText", PrivateInstance)
            .GetValue(manager)).text, Is.EqualTo("page 1 / 1"));
    }

    [Test]
    public void DurableScoreKeepsAggregateAndIndependentRangeCountsAndOnlineMetric()
    {
        GameStats stats = root.AddComponent<GameStats>();
        stats.Stats.ShotMade = 20;
        stats.Stats.ThreePointerMade = 3;
        stats.Stats.FourPointerMade = 4;
        stats.Stats.SevenPointerMade = 7;
        PlayerIdentifier player = root.AddComponent<PlayerIdentifier>();
        player.gameStats = stats;
        HighScoreModel model = new HighScoreModel().convertBasketBallStatsToModel(new List<PlayerIdentifier> { player });
        Assert.That(model.MaxShotMade, Is.EqualTo(20));
        Assert.That(new[] { model.ThreeMade, model.FourMade, model.SevenMade }, Is.EqualTo(new[] { 3, 4, 7 }));
        // Seed a sentinel all-time row; local record insertion/reads must leave it alone.
        Execute("INSERT INTO AllTimeStats (threeMade, fourMade, sevenMade, totalPoints) VALUES (30, 40, 70, 1000)");
        model.Modeid = 2;
        model.UserName = "persisted";
        model.Character = "character";
        model.Level = "level";
        Assert.That(database.Helper.InsertGameScore(model), Is.True);
        using (var command = database.Helper.Connection.CreateCommand())
        {
            command.CommandText = "SELECT maxShotMade, threeMade, fourMade, sevenMade FROM HighScores";
            using (var reader = command.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True);
                Assert.That(Enumerable.Range(0, 4).Select(reader.GetInt32), Is.EqualTo(new[] { 20, 3, 4, 7 }));
            }
        }
        Assert.That(Rows(2)[0].Score, Is.EqualTo("3"));
        Assert.That(database.Helper.getIntValueAllTimeFromTableByField("AllTimeStats", "threeMade"), Is.EqualTo(30));
        Assert.That(database.Helper.getIntValueAllTimeFromTableByField("AllTimeStats", "fourMade"), Is.EqualTo(40));
        Assert.That(database.Helper.getIntValueAllTimeFromTableByField("AllTimeStats", "sevenMade"), Is.EqualTo(70));
        Assert.That(database.Helper.getIntValueAllTimeFromTableByField("AllTimeStats", "totalPoints"), Is.EqualTo(1000));
        Assert.That(BackendV2MatchResultAdapter.TryAdapt(model, out SubmitMatchResultDto request), Is.True);
        Assert.That(request.Metrics[MatchResultMetric.ShotsMade.ToString()], Is.EqualTo(20));
    }

    [TestCase(1, "totalPoints")]
    [TestCase(6, "totalDistance")]
    [TestCase(7, "time")]
    [TestCase(14, "consecutiveShots")]
    [TestCase(16, "totalPoints")]
    [TestCase(99, "longestShot")]
    public void UnrelatedModesKeepTheirAuthoredRankingFields(int id, string field)
    {
        Assert.That(Source(id).HighScoreField, Is.EqualTo(field));
        Insert(id, "higher", 100, 0, 20);
        Insert(id, "lower", 200, 0, 10);
        Execute($"UPDATE HighScores SET {field} = 7 WHERE userName = 'higher'");
        Execute($"UPDATE HighScores SET {field} = 3 WHERE userName = 'lower'");
        Assert.That(Rows(id).Take(2).Select(row => row.UserName), Is.EqualTo(id == 7
            ? new[] { "lower", "higher" } : new[] { "higher", "lower" }));
    }

    private static StartScreenModeSelected Source(int id)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{id:D2}.prefab");
        Assert.That(prefab, Is.Not.Null);
        return prefab.GetComponent<StartScreenModeSelected>();
    }

    private void Insert(int id, string name, int aggregate, int qualifying, float time,
        int hardcore = 0, int traffic = 0, int enemies = 0, int sniper = 0)
    {
        var score = new HighScoreModel
        {
            Scoreid = Guid.NewGuid().ToString(), Modeid = id, UserName = name,
            Character = "character", Level = "level", Date = "2026-10-12", Time = time,
            MaxShotMade = aggregate, ThreeMade = id == 2 ? qualifying : 0,
            FourMade = id == 3 ? qualifying : 0, SevenMade = id == 4 ? qualifying : 0,
            HardcoreEnabled = hardcore, TrafficEnabled = traffic, EnemiesEnabled = enemies, SniperEnabled = sniper
        };
        Assert.That(database.Helper.InsertGameScore(score), Is.True);
    }

    private List<StatsTableHighScoreRow> Rows(int id, bool hardcore = false, bool traffic = false,
        bool enemies = false, bool sniper = false)
    {
        return ReadRowsWithoutEditModeDestroyErrors(() => database.Helper
            .getListOfHighScoreRowsFromTableByModeIdAndField(Source(id).HighScoreField, id,
                hardcore, traffic, enemies, sniper, 0));
    }

    // DBHelper's existing data-only rows use deferred Destroy, which Unity diagnoses in EditMode.
    // Permit only that known engine diagnostic; query/projection errors must still fail the test.
    private static T ReadRowsWithoutEditModeDestroyErrors<T>(Func<T> read)
    {
        var unexpected = new List<string>();
        bool previous = LogAssert.ignoreFailingMessages;
        void Capture(string message, string stack, LogType type)
        {
            if (!message.StartsWith("DBHelperTestHarness: Destroy may not be called from edit mode", StringComparison.Ordinal)
                && (type == LogType.Error || type == LogType.Exception || message.Contains("ERROR :")))
                unexpected.Add(message);
        }
        Application.logMessageReceived += Capture;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            T result = read();
            Assert.That(unexpected, Is.Empty, string.Join("\n", unexpected));
            return result;
        }
        finally
        {
            LogAssert.ignoreFailingMessages = previous;
            Application.logMessageReceived -= Capture;
        }
    }

    private void Execute(string sql)
    {
        using (var command = database.Helper.Connection.CreateCommand())
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    private TextMeshProUGUI Text(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform);
        return child.AddComponent<TextMeshProUGUI>();
    }

    private static float PersonalBest(PlayerData data, int id)
    {
        return id == 2 ? data.ThreePointerMade : id == 3 ? data.FourPointerMade : data.SevenPointerMade;
    }

    private static void Set(object target, string name, object value)
    {
        typeof(StatsManager).GetField(name, PrivateInstance).SetValue(target, value);
    }
}
