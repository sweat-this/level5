using Assets.Scripts.database;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PendingMatchPersistenceStore
{
    private const string FileName = "pending-match-persistence.json";

    public static bool QueueScore(HighScoreModel score)
    {
        if (score == null || string.IsNullOrEmpty(score.Scoreid))
        {
            return false;
        }

        return Update(data =>
        {
            if (!data.scores.Exists(value => value != null && value.Scoreid == score.Scoreid))
            {
                data.scores.Add(score);
            }
        });
    }

    public static bool QueueAllTime(string resultId, GameStats stats)
    {
        if (string.IsNullOrEmpty(resultId) || stats == null)
        {
            return false;
        }

        return Update(data =>
        {
            if (!data.allTime.Exists(value => value != null && value.resultId == resultId))
            {
                data.allTime.Add(AllTimeStatsSnapshot.From(resultId, stats));
            }
        });
    }

    public static bool Repair()
    {
        if (DBConnector.instance == null)
        {
            return false;
        }

        PendingMatchPersistenceData data = Load();
        for (int index = data.scores.Count - 1; index >= 0; index--)
        {
            HighScoreModel score = data.scores[index];
            if (score == null)
            {
                data.scores.RemoveAt(index);
                continue;
            }

            if (!DBConnector.instance.savePlayerGameStats(score))
            {
                continue;
            }

            DBHelper.instance?.setGameScoreSubmitted(score.Scoreid, false);
            data.scores.RemoveAt(index);
        }
        data.allTime.RemoveAll(snapshot => snapshot == null || DBConnector.instance.savePlayerAllTimeStats(snapshot));
        return Save(data);
    }

    private static bool Update(Action<PendingMatchPersistenceData> update)
    {
        try
        {
            PendingMatchPersistenceData data = Load();
            update(data);
            return Save(data);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not queue pending match persistence: " + exception);
            return false;
        }
    }

    private static PendingMatchPersistenceData Load()
    {
        string path = GetPath();
        if (!AtomicFile.TryReadAllText(path, IsValid, out string json))
        {
            return new PendingMatchPersistenceData();
        }

        PendingMatchPersistenceData data = JsonUtility.FromJson<PendingMatchPersistenceData>(json)
            ?? new PendingMatchPersistenceData();
        data.Normalize();
        return data;
    }

    private static bool Save(PendingMatchPersistenceData data)
    {
        try
        {
            data.Normalize();
            AtomicFile.WriteAllText(GetPath(), JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not save pending match persistence: " + exception);
            return false;
        }
    }

    private static bool IsValid(string json)
    {
        try
        {
            return JsonUtility.FromJson<PendingMatchPersistenceData>(json) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetPath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }
}

[Serializable]
public class PendingMatchPersistenceData
{
    public List<HighScoreModel> scores = new List<HighScoreModel>();
    public List<AllTimeStatsSnapshot> allTime = new List<AllTimeStatsSnapshot>();

    public void Normalize()
    {
        scores ??= new List<HighScoreModel>();
        allTime ??= new List<AllTimeStatsSnapshot>();
    }
}

[Serializable]
public class AllTimeStatsSnapshot
{
    public string resultId;
    public int twoMade;
    public int twoAtt;
    public int threeMade;
    public int threeAtt;
    public int fourMade;
    public int fourAtt;
    public int sevenMade;
    public int sevenAtt;
    public int moneyBallMade;
    public int moneyBallAtt;
    public int totalPoints;
    public float totalDistance;
    public float timePlayed;
    public float longestShot;
    public int enemiesKilled;
    public int sniperHits;
    public int sniperShots;

    public static AllTimeStatsSnapshot From(string resultId, GameStats stats)
    {
        return new AllTimeStatsSnapshot
        {
            resultId = resultId,
            twoMade = stats.TwoPointerMade,
            twoAtt = stats.TwoPointerAttempts,
            threeMade = stats.ThreePointerMade,
            threeAtt = stats.ThreePointerAttempts,
            fourMade = stats.FourPointerMade,
            fourAtt = stats.FourPointerAttempts,
            sevenMade = stats.SevenPointerMade,
            sevenAtt = stats.SevenPointerAttempts,
            moneyBallMade = stats.MoneyBallMade,
            moneyBallAtt = stats.MoneyBallAttempts,
            totalPoints = stats.TotalPoints,
            totalDistance = stats.TotalDistance,
            timePlayed = stats.TimePlayed,
            longestShot = stats.LongestShotMade,
            enemiesKilled = stats.EnemiesKilled,
            sniperHits = stats.SniperHits,
            sniperShots = stats.SniperShots
        };
    }
}
