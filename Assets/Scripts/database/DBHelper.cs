using Assets.Scripts.database;
using Mono.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.UI;

public class DBHelper : MonoBehaviour
{
    private String connection;
    private String databaseNamePath = "/level5.db";
    private String filepath;

    //private int currentDatabaseAppVersion = 8;
    //bool databaseSuccessfullyUpgraded = true;

    [SerializeField]
    bool databaseLocked = false;

    Text message;

    public static DBHelper instance;

    private static string RequireSqlIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SQL identifier cannot be empty.", parameterName);
        }

        if (!IsSqlIdentifierStart(value[0]))
        {
            throw new ArgumentException("SQL identifier must start with a letter or underscore.", parameterName);
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsSqlIdentifierPart(value[i]))
            {
                throw new ArgumentException("SQL identifier contains invalid characters.", parameterName);
            }
        }

        return value;
    }

    private static bool IsSqlIdentifierStart(char value)
    {
        return value == '_' || (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
    }

    private static bool IsSqlIdentifierPart(char value)
    {
        return IsSqlIdentifierStart(value) || (value >= '0' && value <= '9');
    }

    private static string RequireSqlSortOrder(string value)
    {
        string normalized = value == null ? string.Empty : value.Trim().ToUpperInvariant();
        if (normalized == "ASC" || normalized == "DESC")
        {
            return normalized;
        }

        throw new ArgumentException("SQL sort order must be ASC or DESC.", nameof(value));
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        filepath = Application.persistentDataPath + databaseNamePath;
        connection = "Data source=" + filepath; //Path to database
    }

    private void Start()
    {
        if (GameObject.Find("messageDisplay") != null)
        {
            message = GameObject.Find("messageDisplay").GetComponent<Text>();
        }
    }

    // check if specified table is emoty
    public bool isTableEmpty(String tableName)
    {
        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            int count = 0;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open();
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = "SELECT count(*) FROM " + tableName;
                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            count = reader.GetInt32(0);
                        }
                    }
                }
            }

            return count == 0;
        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
            return false;
        }
    }

    // insert current game's stats and score
    public void InsertGameScore(HighScoreModel stats)
    {
        databaseLocked = true;
        try
        {
            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // todo : add p1-p4 toal points/1ST-4TH PLACE/winnIsCpu to query and insert, then into create database
                    string sqlQuery1 =
                       "INSERT INTO HighScores( scoreidUnique, modeid, characterid, character, levelid, level, os, version ,date, time, " +
                       " totalPoints, longestShot, totalDistance, maxShotMade, maxShotAtt, consecutiveShots, trafficEnabled, " +
                       "hardcoreEnabled, enemiesEnabled, enemiesKilled, platform, device, ipaddress, twoMade, twoAtt, threeMade, threeAtt, " +
                       "fourMade, fourAtt, sevenMade, sevenAtt, bonusPoints, moneyBallMade, moneyBallAtt, userName, sniperEnabled, sniperMode, sniperModeName," +
                       "sniperHits, sniperShots, p1TotalPoints,p2TotalPoints,p3TotalPoints,p4TotalPoints,first,second,third,fourth,p1IsCpu,p2IsCpu,p3IsCpu,p4IsCpu,numPlayers,difficulty," +
                       "campaignWins, campaignLosses,CampaignTies)  " +
                       "Values(@scoreidUnique, @modeid, @characterid, @character, @levelid, @level, @os, @version, @date, @time, " +
                       "@totalPoints, @longestShot, @totalDistance, @maxShotMade, @maxShotAtt, @consecutiveShots, @trafficEnabled, " +
                       "@hardcoreEnabled, @enemiesEnabled, @enemiesKilled, @platform, @device, @ipaddress, @twoMade, @twoAtt, @threeMade, @threeAtt, " +
                       "@fourMade, @fourAtt, @sevenMade, @sevenAtt, @bonusPoints, @moneyBallMade, @moneyBallAtt, @userName, @sniperEnabled, @sniperMode, @sniperModeName, " +
                       "@sniperHits, @sniperShots, @p1TotalPoints, @p2TotalPoints, @p3TotalPoints, @p4TotalPoints, @first, @second, @third, @fourth, @p1IsCpu, @p2IsCpu, @p3IsCpu, @p4IsCpu, @numPlayers, @difficulty, " +
                       "@campaignWins, @campaignLosses, @campaignTies)";

                    dbcmd.CommandText = sqlQuery1;
                    dbcmd.Parameters.Add(new SqliteParameter("@scoreidUnique", stats.Scoreid));
                    dbcmd.Parameters.Add(new SqliteParameter("@modeid", stats.Modeid));
                    dbcmd.Parameters.Add(new SqliteParameter("@characterid", stats.Characterid));
                    dbcmd.Parameters.Add(new SqliteParameter("@character", stats.Character));
                    dbcmd.Parameters.Add(new SqliteParameter("@levelid", stats.Levelid));
                    dbcmd.Parameters.Add(new SqliteParameter("@level", stats.Level));
                    dbcmd.Parameters.Add(new SqliteParameter("@os", stats.Os));
                    dbcmd.Parameters.Add(new SqliteParameter("@version", stats.Version));
                    dbcmd.Parameters.Add(new SqliteParameter("@date", stats.Date));
                    dbcmd.Parameters.Add(new SqliteParameter("@time", stats.Time));
                    dbcmd.Parameters.Add(new SqliteParameter("@totalPoints", stats.TotalPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@longestShot", stats.LongestShot));
                    dbcmd.Parameters.Add(new SqliteParameter("@totalDistance", stats.TotalDistance));
                    dbcmd.Parameters.Add(new SqliteParameter("@maxShotMade", stats.MaxShotMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@maxShotAtt", stats.MaxShotAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@consecutiveShots", stats.ConsecutiveShots));
                    dbcmd.Parameters.Add(new SqliteParameter("@trafficEnabled", stats.TrafficEnabled));
                    dbcmd.Parameters.Add(new SqliteParameter("@hardcoreEnabled", stats.HardcoreEnabled));
                    dbcmd.Parameters.Add(new SqliteParameter("@enemiesEnabled", stats.EnemiesEnabled));
                    dbcmd.Parameters.Add(new SqliteParameter("@enemiesKilled", stats.EnemiesKilled));
                    dbcmd.Parameters.Add(new SqliteParameter("@platform", stats.Platform));
                    dbcmd.Parameters.Add(new SqliteParameter("@device", stats.Device));
                    dbcmd.Parameters.Add(new SqliteParameter("@ipaddress", stats.Ipaddress));
                    dbcmd.Parameters.Add(new SqliteParameter("@twoMade", stats.TwoMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@twoAtt", stats.TwoAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@threeMade", stats.ThreeMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@threeAtt", stats.ThreeAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@fourMade", stats.FourMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@fourAtt", stats.FourAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@sevenMade", stats.SevenMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@sevenAtt", stats.SevenAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@bonusPoints", stats.BonusPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@moneyBallMade", stats.MoneyBallMade));
                    dbcmd.Parameters.Add(new SqliteParameter("@moneyBallAtt", stats.MoneyBallAtt));
                    dbcmd.Parameters.Add(new SqliteParameter("@userName", stats.UserName));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperEnabled", stats.SniperEnabled));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperMode", stats.SniperMode));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperModeName", stats.SniperModeName));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperHits", stats.Sniperhits));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperShots", stats.SniperShots));
                    dbcmd.Parameters.Add(new SqliteParameter("@p1TotalPoints", stats.p1TotalPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@p2TotalPoints", stats.p2TotalPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@p3TotalPoints", stats.p3TotalPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@p4TotalPoints", stats.p4TotalPoints));
                    dbcmd.Parameters.Add(new SqliteParameter("@first", stats.firstPlace));
                    dbcmd.Parameters.Add(new SqliteParameter("@second", stats.secondPlace));
                    dbcmd.Parameters.Add(new SqliteParameter("@third", stats.thirdPlace));
                    dbcmd.Parameters.Add(new SqliteParameter("@fourth", stats.fourthPlace));
                    dbcmd.Parameters.Add(new SqliteParameter("@p1IsCpu", stats.p1IsCpu));
                    dbcmd.Parameters.Add(new SqliteParameter("@p2IsCpu", stats.p2IsCpu));
                    dbcmd.Parameters.Add(new SqliteParameter("@p3IsCpu", stats.p3IsCpu));
                    dbcmd.Parameters.Add(new SqliteParameter("@p4IsCpu", stats.p4IsCpu));
                    dbcmd.Parameters.Add(new SqliteParameter("@numPlayers", GameOptions.numPlayers));
                    dbcmd.Parameters.Add(new SqliteParameter("@difficulty", stats.Difficulty));
                    dbcmd.Parameters.Add(new SqliteParameter("@campaignWins", stats.campaignWins));
                    dbcmd.Parameters.Add(new SqliteParameter("@campaignLosses", stats.campaignLosses));
                    dbcmd.Parameters.Add(new SqliteParameter("@campaignTies", stats.campaignTies));

                    dbcmd.ExecuteNonQuery();
                }
            }

            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    // add default cheerleader data from PREFABS to DATABASE
    public IEnumerator InsertCheerleaderProfile(List<CheerleaderProfile> cheerleaderSelectedData)
    {
        yield return new WaitUntil(() => !databaseLocked);
        try
        {
            databaseLocked = true;
            var dbconn = new SqliteConnection(connection);
            using (dbconn)
            {
                dbconn.Open(); //Open connection to the database.
                using (SqliteTransaction tr = dbconn.BeginTransaction())
                {
                    using (SqliteCommand cmd = dbconn.CreateCommand())
                    {
                        cmd.Transaction = tr;
                        foreach (CheerleaderProfile ch in cheerleaderSelectedData)
                        {
                            string sqlQuery =
                            "Insert INTO "
                            + Constants.LOCAL_DATABASE_tableName_cheerleaderProfile + " ( cid, name, objectName, unlockText, isLocked) "
                            + " Values(@cid, @name, @objectName, @unlockText, @isLocked)";

                            cmd.CommandText = sqlQuery;
                            cmd.Parameters.Clear();
                            cmd.Parameters.Add(new SqliteParameter("@cid", ch.CheerleaderId));
                            cmd.Parameters.Add(new SqliteParameter("@name", ch.CheerleaderDisplayName));
                            cmd.Parameters.Add(new SqliteParameter("@objectName", ch.CheerleaderObjectName));
                            cmd.Parameters.Add(new SqliteParameter("@unlockText", ch.UnlockCharacterText));
                            cmd.Parameters.Add(new SqliteParameter("@isLocked", Convert.ToInt32(ch.IsLocked)));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tr.Commit();
                }
                dbconn.Close();
            }
            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            //return;
        }
    }

    // add experience gained to database
    internal void UpdatePlayerProfileProgression(float expGained)
    {
        try
        {
            int prevLevel = PlayerData.instance.CurrentExperience / 3000;
            int currentLevel = ((int)((PlayerData.instance.CurrentExperience + expGained) / 3000));

            int updatePointsAvailable = PlayerData.instance.UpdatePointsAvailable;
            int updatePointsUsed = PlayerData.instance.UpdatePointsUsed;

            int counter = currentLevel - prevLevel;
            // check for levels gained. for loop in case of gaining multiple levels
            if (currentLevel > prevLevel)
            {
                for (int i = 0; i < counter; i++)
                {
                    PlayerData.instance.UpdatePointsAvailable++;
                }
            }

            // if used points is too much
            if ((updatePointsUsed + updatePointsAvailable) > currentLevel)
            {
                updatePointsUsed = currentLevel;
                updatePointsAvailable = 0;
            }
            // if used points is not enough
            if ((updatePointsUsed + updatePointsAvailable) < currentLevel)
            {
                updatePointsAvailable = currentLevel - (updatePointsUsed + updatePointsAvailable);
            }

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    string sqlQuery1 =
                       "UPDATE " + Constants.LOCAL_DATABASE_tableName_characterProfile
                       + " SET experience = @experience"
                       + ", level = @level"
                       + ", pointsAvailable = @pointsAvailable"
                       + ", pointsUsed = @pointsUsed"
                       + " WHERE charid = @charid";

                    dbcmd.CommandText = sqlQuery1;
                    dbcmd.Parameters.Add(new SqliteParameter("@experience", PlayerData.instance.CurrentExperience + expGained));
                    dbcmd.Parameters.Add(new SqliteParameter("@level", currentLevel));
                    dbcmd.Parameters.Add(new SqliteParameter("@pointsAvailable", updatePointsAvailable));
                    dbcmd.Parameters.Add(new SqliteParameter("@pointsUsed", updatePointsUsed));
                    dbcmd.Parameters.Add(new SqliteParameter("@charid", GameOptions.characterId));

                    dbcmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    // insert default Player profiles
    public IEnumerator InsertCharacterProfile(List<CharacterProfile> shooterProfileList)
    {
        yield return new WaitUntil(() => !databaseLocked);
        try
        {
            databaseLocked = true;
            var dbconn = new SqliteConnection(connection);
            using (dbconn)
            {
                dbconn.Open(); //Open connection to the database.
                using (SqliteTransaction tr = dbconn.BeginTransaction())
                {
                    using (SqliteCommand cmd = dbconn.CreateCommand())
                    {
                        cmd.Transaction = tr;
                        foreach (CharacterProfile shooter in shooterProfileList)
                        {
                            string sqlQuery =
                            "Insert INTO "
                            + Constants.LOCAL_DATABASE_tableName_characterProfile + " ( charid, playerName, objectName, accuracy2, accuracy3, accuracy4, accuracy7, jump, " +
                            "speed, runSpeed, runSpeedHasBall, luck, shootAngle, experience, level, pointsAvailable, pointsUsed, range, release, isLocked) "
                            + " Values(@charid, @playerName, @objectName, @accuracy2, @accuracy3, @accuracy4, @accuracy7, @jump, " +
                            "@speed, @runSpeed, @runSpeedHasBall, @luck, @shootAngle, @experience, @level, @pointsAvailable, @pointsUsed, @range, @release, @isLocked)";

                            cmd.CommandText = sqlQuery;
                            cmd.Parameters.Clear();
                            cmd.Parameters.Add(new SqliteParameter("@charid", shooter.PlayerId));
                            cmd.Parameters.Add(new SqliteParameter("@playerName", shooter.PlayerDisplayName));
                            cmd.Parameters.Add(new SqliteParameter("@objectName", shooter.PlayerObjectName));
                            cmd.Parameters.Add(new SqliteParameter("@accuracy2", shooter.Accuracy2Pt));
                            cmd.Parameters.Add(new SqliteParameter("@accuracy3", shooter.Accuracy3Pt));
                            cmd.Parameters.Add(new SqliteParameter("@accuracy4", shooter.Accuracy4Pt));
                            cmd.Parameters.Add(new SqliteParameter("@accuracy7", shooter.Accuracy7Pt));
                            cmd.Parameters.Add(new SqliteParameter("@jump", shooter.JumpForce));
                            cmd.Parameters.Add(new SqliteParameter("@speed", shooter.Speed));
                            cmd.Parameters.Add(new SqliteParameter("@runSpeed", shooter.RunSpeed));
                            cmd.Parameters.Add(new SqliteParameter("@runSpeedHasBall", shooter.RunSpeedHasBall));
                            cmd.Parameters.Add(new SqliteParameter("@luck", shooter.Luck));
                            cmd.Parameters.Add(new SqliteParameter("@shootAngle", shooter.ShootAngle));
                            cmd.Parameters.Add(new SqliteParameter("@experience", shooter.Experience));
                            cmd.Parameters.Add(new SqliteParameter("@level", shooter.Level));
                            cmd.Parameters.Add(new SqliteParameter("@pointsAvailable", shooter.PointsAvailable));
                            cmd.Parameters.Add(new SqliteParameter("@pointsUsed", shooter.PointsUsed));
                            cmd.Parameters.Add(new SqliteParameter("@range", shooter.Range));
                            cmd.Parameters.Add(new SqliteParameter("@release", shooter.Release));
                            cmd.Parameters.Add(new SqliteParameter("@isLocked", Convert.ToInt32(shooter.IsLocked)));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tr.Commit();
                }
                dbconn.Close();
            }
            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }

    // insert a specific character to database. Example, new character added to game,
    // this will update Database with new character info
    public void InsertCharacterProfile(CharacterProfile character)
    {
        try
        {
            databaseLocked = true;
            var dbconn = new SqliteConnection(connection);
            using (dbconn)
            {
                dbconn.Open(); //Open connection to the database.
                using (SqliteTransaction tr = dbconn.BeginTransaction())
                {
                    using (SqliteCommand cmd = dbconn.CreateCommand())
                    {
                        cmd.Transaction = tr;

                        string sqlQuery =
                        "Insert INTO "
                        + Constants.LOCAL_DATABASE_tableName_characterProfile + " ( charid, playerName, objectName, accuracy2, accuracy3, accuracy4, accuracy7, jump, " +
                        "speed, runSpeed, runSpeedHasBall, luck, shootAngle, experience, level, pointsAvailable, pointsUsed, range, release, islocked) "
                        + " Values(@charid, @playerName, @objectName, @accuracy2, @accuracy3, @accuracy4, @accuracy7, @jump, " +
                        "@speed, @runSpeed, @runSpeedHasBall, @luck, @shootAngle, @experience, @level, @pointsAvailable, @pointsUsed, @range, @release, @isLocked)";

                        cmd.CommandText = sqlQuery;
                        cmd.Parameters.Add(new SqliteParameter("@charid", character.PlayerId));
                        cmd.Parameters.Add(new SqliteParameter("@playerName", character.PlayerDisplayName));
                        cmd.Parameters.Add(new SqliteParameter("@objectName", character.PlayerObjectName));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy2", character.Accuracy2Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy3", character.Accuracy3Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy4", character.Accuracy4Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy7", character.Accuracy7Pt));
                        cmd.Parameters.Add(new SqliteParameter("@jump", character.JumpForce));
                        cmd.Parameters.Add(new SqliteParameter("@speed", character.Speed));
                        cmd.Parameters.Add(new SqliteParameter("@runSpeed", character.RunSpeed));
                        cmd.Parameters.Add(new SqliteParameter("@runSpeedHasBall", character.RunSpeedHasBall));
                        cmd.Parameters.Add(new SqliteParameter("@luck", character.Luck));
                        cmd.Parameters.Add(new SqliteParameter("@shootAngle", character.ShootAngle));
                        cmd.Parameters.Add(new SqliteParameter("@experience", character.Experience));
                        cmd.Parameters.Add(new SqliteParameter("@level", character.Level));
                        cmd.Parameters.Add(new SqliteParameter("@pointsAvailable", character.PointsAvailable));
                        cmd.Parameters.Add(new SqliteParameter("@pointsUsed", character.PointsUsed));
                        cmd.Parameters.Add(new SqliteParameter("@range", character.Range));
                        cmd.Parameters.Add(new SqliteParameter("@release", character.Release));
                        cmd.Parameters.Add(new SqliteParameter("@isLocked", Convert.ToInt32(character.IsLocked)));
                        cmd.ExecuteNonQuery();
                    }
                    tr.Commit();
                }
                dbconn.Close();
            }
            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    // update a character profile.
    // used in Progression scene on Save progress
    public void UpdateCharacterProfile(CharacterProfile character)
    {
        try
        {
            databaseLocked = true;
            var dbconn = new SqliteConnection(connection);
            using (dbconn)
            {
                dbconn.Open(); //Open connection to the database.
                using (SqliteTransaction tr = dbconn.BeginTransaction())
                {
                    using (SqliteCommand cmd = dbconn.CreateCommand())
                    {
                        cmd.Transaction = tr;

                        string sqlQuery =
                        "Update " + Constants.LOCAL_DATABASE_tableName_characterProfile
                        + " SET accuracy2 = @accuracy2"
                        + ", accuracy3 = @accuracy3"
                        + ", accuracy4 = @accuracy4"
                        + ", accuracy7 = @accuracy7"
                        + ", range = @range"
                        + ", release = @release"
                        + ", luck = @luck"
                        + ", pointsAvailable = @pointsAvailable"
                        + ", pointsUsed = @pointsUsed"
                        + " WHERE charid = @charid";
                        //+ " AND userid = "+ GameOptions.userid;

                        cmd.CommandText = sqlQuery;
                        cmd.Parameters.Add(new SqliteParameter("@accuracy2", character.Accuracy2Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy3", character.Accuracy3Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy4", character.Accuracy4Pt));
                        cmd.Parameters.Add(new SqliteParameter("@accuracy7", character.Accuracy7Pt));
                        cmd.Parameters.Add(new SqliteParameter("@range", character.Range));
                        cmd.Parameters.Add(new SqliteParameter("@release", character.Release));
                        cmd.Parameters.Add(new SqliteParameter("@luck", character.Luck));
                        cmd.Parameters.Add(new SqliteParameter("@pointsAvailable", character.PointsAvailable));
                        cmd.Parameters.Add(new SqliteParameter("@pointsUsed", character.PointsUsed));
                        cmd.Parameters.Add(new SqliteParameter("@charid", character.PlayerId));
                        cmd.ExecuteNonQuery();

                    }
                    tr.Commit();
                }
                dbconn.Close();
            }
            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }
    // insert a specific cheerleader to database. Example, new cheerleader added to game,
    // this will update Database with new cheerleader info
    public void InsertCheerleaderProfile(CheerleaderProfile cheerleader)
    {
        try
        {
            databaseLocked = true;
            var dbconn = new SqliteConnection(connection);
            using (dbconn)
            {
                dbconn.Open(); //Open connection to the database.
                using (SqliteTransaction tr = dbconn.BeginTransaction())
                {
                    using (SqliteCommand cmd = dbconn.CreateCommand())
                    {
                        cmd.Transaction = tr;

                        string sqlQuery =
                        "Insert INTO "
                        + Constants.LOCAL_DATABASE_tableName_cheerleaderProfile + " ( cid, name, objectName, unlockText, isLocked ) "
                        + " Values(@cid, @name, @objectName, @unlockText, @isLocked)";

                        cmd.CommandText = sqlQuery;
                        cmd.Parameters.Add(new SqliteParameter("@cid", cheerleader.CheerleaderId));
                        cmd.Parameters.Add(new SqliteParameter("@name", cheerleader.CheerleaderDisplayName));
                        cmd.Parameters.Add(new SqliteParameter("@objectName", cheerleader.CheerleaderObjectName));
                        cmd.Parameters.Add(new SqliteParameter("@unlockText", cheerleader.UnlockCharacterText));
                        cmd.Parameters.Add(new SqliteParameter("@isLocked", Convert.ToInt32(cheerleader.IsLocked)));
                        cmd.ExecuteNonQuery();

                    }
                    tr.Commit();
                }
                dbconn.Close();
            }
            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    // get All time stats. Used to update all time stats after a game session
    internal GameStats getAllTimeStats()
    {
        try
        {
            GameStats prevStats = gameObject.AddComponent<GameStats>();

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_allTimeStats))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        dbcmd.CommandText = "Select * From " + Constants.LOCAL_DATABASE_tableName_allTimeStats;
                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                prevStats.TwoPointerMade = reader.GetInt32(1);
                                prevStats.TwoPointerAttempts = reader.GetInt32(2);
                                prevStats.ThreePointerMade = reader.GetInt32(3);
                                prevStats.ThreePointerAttempts = reader.GetInt32(4);
                                prevStats.FourPointerMade = reader.GetInt32(5);
                                prevStats.FourPointerAttempts = reader.GetInt32(6);
                                prevStats.SevenPointerMade = reader.GetInt32(7);
                                prevStats.SevenPointerAttempts = reader.GetInt32(8);
                                prevStats.MoneyBallMade = reader.GetInt32(9);
                                prevStats.MoneyBallAttempts = reader.GetInt32(10);
                                prevStats.TotalPoints = reader.GetInt32(11);
                                prevStats.TotalDistance = reader.GetFloat(12);
                                prevStats.LongestShotMade = reader.GetFloat(13);
                                prevStats.TimePlayed = reader.GetFloat(14);
                                if (reader.IsDBNull(15))
                                {
                                    prevStats.EnemiesKilled = 0;
                                }
                                else
                                {
                                    prevStats.EnemiesKilled = reader.GetInt32(15);
                                }
                                prevStats.SniperHits = reader.GetInt32(16);
                                prevStats.SniperShots = reader.GetInt32(17);
                            }
                        }
                    }
                }
            }

            Destroy(prevStats, 5);
            return prevStats;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return null;
        }
    }


    // get Character Data from Database
    public List<CharacterProfileRecord> getCharacterProfileStats(int userid)
    {
        List<CharacterProfileRecord> characterStats = new List<CharacterProfileRecord>();
        try
        {
            DatabaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_characterProfile))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        string sqlQuery = "Select charid, playerName, objectName, accuracy2, accuracy3, accuracy4, accuracy7, jump, speed,"
                            + "runSpeed, runSpeedHasBall, luck, shootAngle, experience, level, pointsAvailable, pointsUsed, range, release, isLocked"
                            + " From " + Constants.LOCAL_DATABASE_tableName_characterProfile;

                        dbcmd.CommandText = sqlQuery;
                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                CharacterProfileRecord temp = new CharacterProfileRecord
                                {
                                    PlayerId = reader.GetInt32(0),
                                    PlayerDisplayName = reader.GetString(1),
                                    PlayerObjectName = reader.GetString(2),
                                    Accuracy2Pt = reader.GetInt32(3),
                                    Accuracy3Pt = reader.GetInt32(4),
                                    Accuracy4Pt = reader.GetInt32(5),
                                    Accuracy7Pt = reader.GetInt32(6),
                                    JumpForce = reader.GetFloat(7),
                                    Speed = reader.GetFloat(8),
                                    RunSpeed = reader.GetFloat(9),
                                    RunSpeedHasBall = reader.GetFloat(10),
                                    Luck = reader.GetInt32(11),
                                    ShootAngle = reader.GetInt32(12),
                                    Experience = reader.GetInt32(13),
                                    Level = reader.GetInt32(14),
                                    PointsAvailable = reader.GetInt32(15),
                                    PointsUsed = reader.GetInt32(16),
                                    Range = reader.GetInt32(17),
                                    Release = reader.GetInt32(18),
                                    IsLocked = Convert.ToBoolean(reader.GetValue(19))
                                };
                                characterStats.Add(temp);
                            }
                        }
                    }
                }
            }

            databaseLocked = false;
            return characterStats;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return new List<CharacterProfileRecord>();
        }
    }

    // get cheerleader data from Database
    public List<CheerleaderProfileRecord> getCheerleaderProfileStats()
    {
        try
        {
            DatabaseLocked = true;
            List<CheerleaderProfileRecord> cheerleaderStats = new List<CheerleaderProfileRecord>();

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_cheerleaderProfile))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        dbcmd.CommandText = "Select cid, name, objectName, unlockText, isLocked "
                            + " From " + Constants.LOCAL_DATABASE_tableName_cheerleaderProfile;

                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                CheerleaderProfileRecord temp = new CheerleaderProfileRecord
                                {
                                    CheerleaderId = reader.GetInt32(0),
                                    CheerleaderDisplayName = reader.GetString(1),
                                    CheerleaderObjectName = reader.GetString(2),
                                    UnlockCharacterText = reader.GetString(3),
                                    IsLocked = Convert.ToBoolean(reader.GetInt32(4))
                                };

                                cheerleaderStats.Add(temp);
                            }
                        }
                    }
                }
            }

            databaseLocked = false;
            return cheerleaderStats;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return new List<CheerleaderProfileRecord>();
        }
    }

    // insert current game's stats and score
    public void InsertUser(UserModel user)
    {
        StartCoroutine(InsertUserCoroutine(user));
    }

    private IEnumerator InsertUserCoroutine(UserModel user)
    {
        yield return new WaitUntil(() => !databaseLocked);
        databaseLocked = true;
        try
        {
            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // local accounts are authenticated against the server (see AccountManager.LoginUserCoroutine /
                    // APIHelper.PostToken) - the password is never compared locally, so it is not persisted here.
                    string sqlQuery1 =
                       "INSERT INTO User(userid, username,firstname, lastname, email, ipaddress, signupdate, lastlogin)  " +
                       "Values(@userid, @username, @firstname, @lastname, @email, @ipaddress, @signupdate, @lastlogin)";

                    dbcmd.CommandText = sqlQuery1;
                    dbcmd.Parameters.Add(new SqliteParameter("@userid", user.Userid));
                    dbcmd.Parameters.Add(new SqliteParameter("@username", user.UserName));
                    dbcmd.Parameters.Add(new SqliteParameter("@firstname", user.FirstName));
                    dbcmd.Parameters.Add(new SqliteParameter("@lastname", user.LastName));
                    dbcmd.Parameters.Add(new SqliteParameter("@email", user.Email));
                    dbcmd.Parameters.Add(new SqliteParameter("@ipaddress", user.IpAddress));
                    dbcmd.Parameters.Add(new SqliteParameter("@signupdate", user.SignUpDate));
                    dbcmd.Parameters.Add(new SqliteParameter("@lastlogin", user.LastLogin));

                    dbcmd.ExecuteNonQuery();
                }
            }

            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            Debug.Log(e);
        }
    }

    // get user Data from Database
    public List<UserModel> getUserProfileStats()
    {
        List<UserModel> userModel = new List<UserModel>();
        try
        {
            DatabaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_user))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        string sqlQuery = "Select userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin, password,"
                            + "bearerToken"
                            + " From " + Constants.LOCAL_DATABASE_tableName_user
                            + " ORDER BY lastlogin ASC";
                        dbcmd.CommandText = sqlQuery;

                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UserModel temp = new UserModel();

                                temp.Userid = reader.GetInt32(0);
                                temp.UserName = reader.GetString(1);
                                temp.FirstName = reader.GetString(2);
                                temp.LastName = reader.GetString(3);
                                temp.Email = reader.GetString(4);
                                temp.IpAddress = reader.GetString(5);
                                temp.SignUpDate = reader.GetString(6);
                                temp.LastLogin = reader.GetString(7);
                                // password is no longer persisted locally (see InsertUserCoroutine) - column is
                                // kept for schema stability but will typically be NULL going forward.
                                temp.Password = reader.IsDBNull(8) ? "" : reader.GetString(8);
                                if (reader.IsDBNull(9))
                                {
                                    temp.BearerToken = "";
                                }
                                else
                                {
                                    temp.BearerToken = reader.GetString(9);
                                }
                                userModel.Add(temp);
                            }
                        }
                    }
                }
            }

            databaseLocked = false;
            return userModel;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return new List<UserModel>();
        }
    }

    public bool localUserExists(UserModel user)
    {
        int count = 0;
        try
        {
            DatabaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_user))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        dbcmd.CommandText = "Select * From " + Constants.LOCAL_DATABASE_tableName_user + " WHERE username = @username";
                        dbcmd.Parameters.Add(new SqliteParameter("@username", user.UserName));

                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                count++;
                            }
                        }
                    }
                }
            }

            databaseLocked = false;
            return count > 0;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return false;
        }
    }

    // update all time stats. deltas from the current session are added atomically in SQL so
    // concurrent/overlapping calls can't lose an update the way a read-modify-write in C# could.
    internal void UpdateAllTimeStats(GameStats stats)
    {
        try
        {
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    string sqlQuery;

                    if (isTableEmpty(Constants.LOCAL_DATABASE_tableName_allTimeStats))
                    {
                        sqlQuery =
                       "Insert INTO " + Constants.LOCAL_DATABASE_tableName_allTimeStats + " ( twoMade, twoAtt, threeMade, threeAtt, fourMade, FourAtt, sevenMade, " +
                       "sevenAtt, totalPoints, moneyBallMade, moneyBallAtt, totalDistance, timePlayed, longestShot, enemiesKilled, sniperHits, sniperShots)  " +
                       "Values(@twoMade, @twoAtt, @threeMade, @threeAtt, @fourMade, @fourAtt, @sevenMade, @sevenAtt, @totalPoints, " +
                       "@moneyBallMade, @moneyBallAtt, @totalDistance, @timePlayed, @longestShot, @enemiesKilled, @sniperHits, @sniperShots)";

                        dbcmd.CommandText = sqlQuery;
                        dbcmd.Parameters.Add(new SqliteParameter("@twoMade", stats.TwoPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@twoAtt", stats.TwoPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@threeMade", stats.ThreePointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@threeAtt", stats.ThreePointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@fourMade", stats.FourPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@fourAtt", stats.FourPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@sevenMade", stats.SevenPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@sevenAtt", stats.SevenPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@totalPoints", stats.TotalPoints));
                        dbcmd.Parameters.Add(new SqliteParameter("@moneyBallMade", stats.MoneyBallMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@moneyBallAtt", stats.MoneyBallAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@totalDistance", stats.TotalDistance));
                        dbcmd.Parameters.Add(new SqliteParameter("@timePlayed", stats.TimePlayed));
                        dbcmd.Parameters.Add(new SqliteParameter("@longestShot", stats.LongestShotMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@enemiesKilled", stats.EnemiesKilled));
                        dbcmd.Parameters.Add(new SqliteParameter("@sniperHits", stats.SniperHits));
                        dbcmd.Parameters.Add(new SqliteParameter("@sniperShots", stats.SniperShots));
                    }
                    else
                    {
                        // additive counters are incremented in place; longestShot is a personal-best style
                        // stat so it's raised to the new value only if the new value is larger.
                        sqlQuery =
                       "Update " + Constants.LOCAL_DATABASE_tableName_allTimeStats +
                       " SET" +
                       " twoMade = twoMade + @twoMade" +
                       ", twoAtt = twoAtt + @twoAtt" +
                       ", threeMade = threeMade + @threeMade" +
                       ", threeAtt = threeAtt + @threeAtt" +
                       ", fourMade = fourMade + @fourMade" +
                       ", FourAtt = FourAtt + @fourAtt" +
                       ", sevenMade = sevenMade + @sevenMade" +
                       ", sevenAtt = sevenAtt + @sevenAtt" +
                       ", moneyBallMade = moneyBallMade + @moneyBallMade" +
                       ", moneyBallAtt = moneyBallAtt + @moneyBallAtt" +
                       ", totalPoints = totalPoints + @totalPoints" +
                       ", totalDistance = totalDistance + @totalDistance" +
                       ", timePlayed = timePlayed + @timePlayed" +
                       ", longestShot = MAX(longestShot, @longestShot)" +
                       ", enemiesKilled = enemiesKilled + @enemiesKilled" +
                       ", sniperHits = sniperHits + @sniperHits" +
                       ", sniperShots = sniperShots + @sniperShots" +
                       " WHERE ROWID = 1 ";

                        dbcmd.CommandText = sqlQuery;
                        dbcmd.Parameters.Add(new SqliteParameter("@twoMade", stats.TwoPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@twoAtt", stats.TwoPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@threeMade", stats.ThreePointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@threeAtt", stats.ThreePointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@fourMade", stats.FourPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@fourAtt", stats.FourPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@sevenMade", stats.SevenPointerMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@sevenAtt", stats.SevenPointerAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@moneyBallMade", stats.MoneyBallMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@moneyBallAtt", stats.MoneyBallAttempts));
                        dbcmd.Parameters.Add(new SqliteParameter("@totalPoints", stats.TotalPoints));
                        dbcmd.Parameters.Add(new SqliteParameter("@totalDistance", stats.TotalDistance));
                        dbcmd.Parameters.Add(new SqliteParameter("@timePlayed", stats.TimePlayed));
                        dbcmd.Parameters.Add(new SqliteParameter("@longestShot", stats.LongestShotMade));
                        dbcmd.Parameters.Add(new SqliteParameter("@enemiesKilled", stats.EnemiesKilled));
                        dbcmd.Parameters.Add(new SqliteParameter("@sniperHits", stats.SniperHits));
                        dbcmd.Parameters.Add(new SqliteParameter("@sniperShots", stats.SniperShots));
                    }

                    dbcmd.ExecuteNonQuery();
                }
            }

            DatabaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    // return int from specified table by field and userid
    public int getIntValueFromTableByFieldAndCharId(String tableName, String field, int charid)
    {
        int value = 0;
        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = "SELECT " + field + " FROM " + tableName + " WHERE charid = @charid";
                    dbcmd.Parameters.Add(new SqliteParameter("@charid", charid));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetInt32(0);
                        }
                    }
                }
            }

            DatabaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }

    // ***************************** get values by MODE ID *******************************************
    // return string from specified table by field and userid
    public int getIntValueHighScoreFromTableByFieldAndModeId(String tableName, String field, int modeid, String order, int hardcore)
    {
        int value = 0;

        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            order = RequireSqlSortOrder(order);
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // get all all values sort DESC, return top 1
                    dbcmd.CommandText = "SELECT " + field + " FROM " + tableName
                        + " WHERE modeid = @modeid AND hardcoreEnabled = @hardcore ORDER BY " + field + "  " + order + "  LIMIT 1";
                    dbcmd.Parameters.Add(new SqliteParameter("@modeid", modeid));
                    dbcmd.Parameters.Add(new SqliteParameter("@hardcore", hardcore));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // null check
                            if (reader.IsDBNull(0))
                            {
                                value = 0;
                            }
                            else
                            {
                                value = reader.GetInt32(0);
                            }
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }

    public List<StatsTableHighScoreRow> getListOfHighScoreRowsFromTableByModeIdAndField(string field,
        int modeid,
        bool hardcoreValue,
        bool trafficValue,
        bool enemiesValue,
        bool sniperValue,
        int pageNumber)
    {
        List<StatsTableHighScoreRow> listOfValues = new List<StatsTableHighScoreRow>();

        string score; // store as string, more effcient that wrting 3 versions of the function
        string character;
        string level;
        string date;
        string hardcore = "";
        float time;
        string username;
        int hardcoreEnabled = 0;
        int trafficEnabled = 0;
        int enemiesEnabled = 0;
        int sniperEnabled = 0;

        int pageNumberOffset = pageNumber * 10;

        try
        {
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    string sqlQuery = BuildSqlQueryForGetHighScoreRows(field, modeid, hardcoreValue, trafficValue, enemiesValue, sniperValue);

                    dbcmd.CommandText = sqlQuery;
                    dbcmd.Parameters.Add(new SqliteParameter("@modeid", modeid));
                    dbcmd.Parameters.Add(new SqliteParameter("@offset", pageNumberOffset));
                    dbcmd.Parameters.Add(new SqliteParameter("@hardcoreEnabled", Convert.ToInt32(hardcoreValue)));
                    dbcmd.Parameters.Add(new SqliteParameter("@trafficEnabled", Convert.ToInt32(trafficValue)));
                    dbcmd.Parameters.Add(new SqliteParameter("@enemiesEnabled", Convert.ToInt32(enemiesValue)));
                    dbcmd.Parameters.Add(new SqliteParameter("@sniperEnabled", Convert.ToInt32(sniperValue)));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // game modes that require float values
                            if ((modeid > 4 && modeid < 14) || modeid == 25 || modeid == 99)
                            {
                                score = reader.GetFloat(0).ToString();
                            }
                            else
                            {
                                score = reader.GetInt32(0).ToString();
                            }
                            character = reader.GetString(1);
                            level = reader.GetString(2);
                            date = reader.GetString(3);
                            time = reader.GetFloat(4);
                            // if filters selected
                            if (hardcoreValue || trafficValue || enemiesValue || sniperValue)
                            {
                                // null check
                                if (reader.IsDBNull(5))
                                {
                                    hardcoreEnabled = 0;
                                }
                                else
                                {
                                    hardcoreEnabled = reader.GetInt32(5);
                                }
                                // null check
                                if (reader.IsDBNull(6))
                                {
                                    trafficEnabled = 0;
                                }
                                else
                                {
                                    trafficEnabled = reader.GetInt32(6);
                                }
                                // null check
                                if (reader.IsDBNull(7))
                                {
                                    enemiesEnabled = 0;
                                }
                                else
                                {
                                    enemiesEnabled = reader.GetInt32(7);
                                }
                                // null check
                                if (reader.IsDBNull(8))
                                {
                                    sniperEnabled = 0;
                                }
                                else
                                {
                                    sniperEnabled = reader.GetInt32(8);
                                }
                                username = reader.GetString(9);
                            }
                            // filters not selected
                            else
                            {
                                username = reader.GetString(5);
                            }

                            StatsTableHighScoreRow row = gameObject.AddComponent<StatsTableHighScoreRow>();
                            row.setRowValues(score, character, level, date, hardcore, username);

                            listOfValues.Add(row);
                            Destroy(row);
                        }
                    }
                }
            }

            // if less than 10 values in list, add empty values
            if (listOfValues.Count < 10)
            {
                int numToAdd = 10 - listOfValues.Count;
                for (int i = 0; i < numToAdd; i++)
                {
                    StatsTableHighScoreRow row = gameObject.AddComponent<StatsTableHighScoreRow>();
                    row.setRowValues("", "", "", "", "", "");
                    listOfValues.Add(row);
                    Destroy(row);
                }
            }

            databaseLocked = false;
            return listOfValues;
        }
        catch (Exception e)
        {
            Debug.Log(" ERROR : " + e);
            databaseLocked = false;
            return listOfValues;
        }
    }

    private static string BuildSqlQueryForGetHighScoreRows(string field, int modeid, bool hardcoreValue, bool trafficValue, bool enemiesValue, bool sniperValue)
    {
        field = RequireSqlIdentifier(field, nameof(field));
        string sqlQuery;
        // if no filter selected, return all
        if (!hardcoreValue && !trafficValue && !enemiesValue && !sniperValue)
        {
            // game modes that require float values/ low time as high score
            if (((modeid > 4 && modeid < 14) || modeid == 25) && modeid != 6 && modeid != 99)
            {
                sqlQuery = "SELECT  " + field + ", character, level, date, time, userName FROM HighScores  WHERE modeid = @modeid"
                    + " ORDER BY "
                    + field + " ASC,time ASC LIMIT 10 OFFSET @offset";
            }
            else
            {
                sqlQuery = "SELECT  " + field + ", character, level, date, time, userName FROM HighScores  WHERE modeid = @modeid"
                    + " ORDER BY "
                    + field + " DESC, time ASC LIMIT 10 OFFSET @offset";
            }
        }
        // filters selected, filter results
        else
        {
            // game modes that require float values/ low time as high score
            if (((modeid > 4 && modeid < 14) || modeid == 25) && modeid != 6 && modeid != 99)
            {
                sqlQuery = "SELECT  " + field + ", character, level, date, time, hardcoreEnabled, " +
                    "trafficEnabled, enemiesEnabled, sniperEnabled, userName FROM HighScores  WHERE modeid = @modeid"
                    + " AND hardcoreEnabled = @hardcoreEnabled"
                    + " AND trafficEnabled = @trafficEnabled"
                    + " AND enemiesEnabled = @enemiesEnabled"
                    + " AND sniperEnabled = @sniperEnabled"
                    + " ORDER BY "
                    + field + " ASC,time ASC LIMIT 10 OFFSET @offset";

            }
            else
            {
                sqlQuery = "SELECT  " + field + ", character, level, date, time, hardcoreEnabled," +
                    "trafficEnabled, enemiesEnabled, sniperEnabled, userName FROM HighScores  WHERE modeid = @modeid"
                    + " AND hardcoreEnabled = @hardcoreEnabled"
                    + " AND trafficEnabled = @trafficEnabled"
                    + " AND enemiesEnabled = @enemiesEnabled"
                    + " AND sniperEnabled = @sniperEnabled"
                    + " ORDER BY "
                    + field + " DESC, time ASC LIMIT 10 OFFSET @offset";
            }
        }
        return sqlQuery;
    }

    public int getNumberOfResults(string field, int modeid, bool hardcoreValue, int pageNumber)
    {
        int rowCount = 0;

        try
        {
            field = RequireSqlIdentifier(field, nameof(field));
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    string sqlQuery;
                    if (hardcoreValue)
                    {
                        sqlQuery = "SELECT Count(*) FROM HighScores  WHERE modeid = @modeid"
                                + " AND hardcoreEnabled = 1 ORDER BY " + field;
                    }
                    else
                    {
                        sqlQuery = "SELECT Count(*) FROM HighScores  WHERE modeid = @modeid"
                                + " AND hardcoreEnabled = 0 ORDER BY " + field;
                    }

                    dbcmd.CommandText = sqlQuery;
                    dbcmd.Parameters.Add(new SqliteParameter("@modeid", modeid));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rowCount = reader.GetInt32(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return rowCount;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return rowCount;
        }
    }


    //============================== get all time stats ===================================================
    public float getFloatValueAllTimeFromTableByField(String tableName, String field)
    {
        float value = 0;

        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = "SELECT " + field + " FROM " + tableName;

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetFloat(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }
    public int getIntValueAllTimeFromTableByField(String tableName, String field)
    {
        int value = 0;

        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // get all all values sort DESC, return top 1
                    dbcmd.CommandText = "SELECT " + field + " FROM " + tableName;

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetInt32(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            Debug.Log(" value : " + value);
            return value;
        }
    }
    //====================================================================================================
    public float getFloatValueHighScoreFromTableByFieldAndModeId(String tableName, String field, int modeid, String order, int hardcore)
    {
        float value = 0;

        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            order = RequireSqlSortOrder(order);
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // get all all values sort DESC, return top 1
                    dbcmd.CommandText = "SELECT " + field + " FROM " + tableName
                        + " WHERE modeid = @modeid AND hardcoreEnabled = @hardcore ORDER BY " + field + " " + order + " LIMIT 1";
                    dbcmd.Parameters.Add(new SqliteParameter("@modeid", modeid));
                    dbcmd.Parameters.Add(new SqliteParameter("@hardcore", hardcore));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetFloat(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }

    //====================================================================================================
    public int getMostConsecutiveShots()
    {
        int value = 0;

        try
        {
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // get all all values sort DESC, return top 1
                    dbcmd.CommandText = "SELECT consecutiveShots from HighScores ORDER BY consecutiveShots DESC LIMIT 1";

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetInt32(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }


    //====================================================================================================
    public float getLongestShotMadeShots()
    {
        float value = 0;

        try
        {
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // get all all values sort DESC, return top 1
                    dbcmd.CommandText = "SELECT longestShot from HighScores ORDER BY longestShot DESC LIMIT 1";

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetFloat(0);
                        }
                    }
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }

    // return string from specified table by field and userid
    public float updateFloatValueByTableAndField(String tableName, String field, float value)
    {
        try
        {
            tableName = RequireSqlIdentifier(tableName, nameof(tableName));
            field = RequireSqlIdentifier(field, nameof(field));
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // if entry is NOT in list of stats
                    dbcmd.CommandText = "UPDATE " + tableName + " SET " + field + " = @value";
                    dbcmd.Parameters.Add(new SqliteParameter("@value", value));

                    dbcmd.ExecuteNonQuery();
                }
            }

            databaseLocked = false;
            return value;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
            return value;
        }
    }

    public void deleteLocalUser(string username)
    {
        try
        {
            databaseLocked = true;

            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = "DELETE FROM User Where username = @username";
                    dbcmd.Parameters.Add(new SqliteParameter("@username", username));

                    dbcmd.ExecuteNonQuery();
                }
            }

            databaseLocked = false;
        }
        catch (Exception e)
        {
            databaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }

    //public void alterTableAddColumn(string tableName, string columnName, string type)
    //{
    //    try
    //    {
    //        databaseLocked = true;
    //        if (!doesColumnExist(tableName, columnName))
    //        {
    //            IDbConnection dbconn;
    //            dbconn = (IDbConnection)new SqliteConnection(connection);
    //            dbconn.Open(); //Open connection to the database.
    //            IDbCommand dbcmd = dbconn.CreateCommand();

    //            string sqlQuery = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + type + " NOT NULL DEFAULT none;";
    //            dbcmd.CommandText = sqlQuery;

    //            IDataReader reader = dbcmd.ExecuteReader();

    //            reader.Close();
    //            reader = null;
    //            dbcmd.Dispose();
    //            dbcmd = null;
    //            dbconn.Close();
    //            dbconn = null;
    //        }
    //    }
    //    catch (Exception e)
    //    {
    //        databaseSuccessfullyUpgraded = false;
    //        Debug.Log("database upgrade to version " + currentDatabaseAppVersion + " failed");
    //        Debug.Log("ERROR : " + e);
    //        databaseLocked = false;
    //        return;
    //    }
    //}

    //public bool doesColumnExist(string tableName, string columnName)
    //{
    //    try
    //    {
    //        databaseLocked = true;

    //        IDbConnection dbconn;
    //        dbconn = (IDbConnection)new SqliteConnection(connection);
    //        dbconn.Open(); //Open connection to the database.
    //        IDbCommand dbcmd = dbconn.CreateCommand();

    //        string sqlQueryCheckForColumn = "PRAGMA table_info(" + tableName + ")";

    //        dbcmd.CommandText = sqlQueryCheckForColumn;
    //        IDataReader reader = dbcmd.ExecuteReader();

    //        int nameIndex = reader.GetOrdinal("Name");

    //        while (reader.Read())
    //        {
    //            if (reader.GetString(nameIndex).Equals(columnName))
    //            {
    //                //Debug.Log("column : " + columnName + " found");
    //                return true;
    //            }
    //        }

    //        reader.Close();
    //        reader = null;
    //        dbcmd.Dispose();
    //        dbcmd = null;
    //        dbconn.Close();
    //        dbconn = null;

    //        databaseLocked = false;
    //    }
    //    catch
    //    {
    //        databaseLocked = false;
    //        return false;
    //    }
    //    return false;
    //}

    // insert current game's stats and score
    public void setGameScoreSubmitted(string scoreid, bool value)
    {
        databaseLocked = true;
        int submittedValue = 0;
        if (value)
        {
            submittedValue = 1;
        }
        try
        {
            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // if entry is NOT in list of stats
                    dbcmd.CommandText = "UPDATE " + Constants.LOCAL_DATABASE_tableName_highscores + " SET submittedToApi = @submittedToApi"
                        + " WHERE scoreidUnique = @scoreidUnique";

                    dbcmd.Parameters.Add(new SqliteParameter("@submittedToApi", submittedValue));
                    dbcmd.Parameters.Add(new SqliteParameter("@scoreidUnique", scoreid));

                    dbcmd.ExecuteNonQuery();
                }
            }

            databaseLocked = false;
        }
        catch (Exception e)
        {
            DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }

    public List<HighScoreModel> getUnsubmittedHighScoreFromDatabase()
    {
        List<HighScoreModel> highscores = new List<HighScoreModel>();
        databaseLocked = true;
        try
        {
            using (IDbConnection dbconn = new SqliteConnection(connection))
            {
                dbconn.Open(); //Open connection to the database.

                if (!isTableEmpty(Constants.LOCAL_DATABASE_tableName_highscores))
                {
                    using (IDbCommand dbcmd = dbconn.CreateCommand())
                    {
                        dbcmd.CommandText = "Select  * From " + Constants.LOCAL_DATABASE_tableName_highscores
                            + " WHERE submittedToApi = 0 "
                            + " AND modeid != 99";

                        using (IDataReader reader = dbcmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                HighScoreModel highscore = new HighScoreModel();

                                if (reader.IsDBNull(0)) { highscore.Id = 0; }
                                else { highscore.Id = reader.GetInt32(0); }
                                highscore.Scoreid = reader.GetString(1);
                                highscore.Modeid = reader.GetInt32(2);
                                highscore.Characterid = reader.GetInt32(3);
                                highscore.Character = reader.GetString(4);
                                highscore.Levelid = reader.GetInt32(5);
                                highscore.Level = reader.GetString(6);
                                highscore.Os = reader.GetString(7);
                                highscore.Version = reader.GetString(8);
                                highscore.Date = reader.GetString(9);
                                highscore.Time = reader.GetFloat(10);
                                highscore.TotalPoints = reader.GetInt32(11);
                                highscore.LongestShot = reader.GetFloat(12);
                                highscore.TotalDistance = reader.GetFloat(13);
                                highscore.MaxShotMade = reader.GetInt32(14);
                                highscore.MaxShotAtt = reader.GetInt32(15);
                                highscore.ConsecutiveShots = reader.GetInt32(16);
                                highscore.TrafficEnabled = reader.GetInt32(17);
                                highscore.HardcoreEnabled = reader.GetInt32(18);
                                highscore.EnemiesEnabled = reader.GetInt32(19);
                                highscore.EnemiesKilled = reader.GetInt32(20);
                                highscore.Platform = reader.GetString(21);
                                highscore.Device = reader.GetString(22);
                                highscore.Ipaddress = reader.GetString(23);
                                highscore.TwoMade = reader.GetInt32(24);
                                highscore.TwoAtt = reader.GetInt32(25);
                                highscore.ThreeMade = reader.GetInt32(26);
                                highscore.ThreeAtt = reader.GetInt32(27);
                                highscore.FourMade = reader.GetInt32(28);
                                highscore.FourAtt = reader.GetInt32(29);
                                highscore.SevenMade = reader.GetInt32(30);
                                highscore.SevenAtt = reader.GetInt32(31);
                                highscore.BonusPoints = reader.GetInt32(32);
                                highscore.MoneyBallMade = reader.GetInt32(33);
                                highscore.MoneyBallAtt = reader.GetInt32(34);
                                highscore.UserName = reader.GetString(36).ToString();
                                highscore.SniperEnabled = reader.GetInt32(37);
                                highscore.SniperMode = reader.GetInt32(38);
                                highscore.SniperModeName = reader.GetString(39);
                                highscore.Sniperhits = reader.GetInt32(40);
                                highscore.SniperShots = reader.GetInt32(41);
                                highscore.p1TotalPoints = reader.GetInt32(42);
                                highscore.p2TotalPoints = reader.GetInt32(43);
                                highscore.p3TotalPoints = reader.GetInt32(44);
                                highscore.p4TotalPoints = reader.GetInt32(45);
                                highscore.firstPlace = reader.GetString(46);
                                highscore.secondPlace = reader.GetString(47);
                                highscore.thirdPlace = reader.GetString(48);
                                highscore.fourthPlace = reader.GetString(49);
                                highscore.p1IsCpu = reader.GetInt32(50);
                                highscore.p2IsCpu = reader.GetInt32(51);
                                highscore.p3IsCpu = reader.GetInt32(52);
                                highscore.p4IsCpu = reader.GetInt32(53);
                                highscore.numPlayers = reader.GetInt32(54);
                                highscore.Difficulty = reader.GetInt32(55);

                                // if username empty on unsubmitted score
                                // but user logged in [gameoptions.username != null/empty
                                // add logged in username to score and submit
                                if ((string.IsNullOrEmpty(highscore.UserName) || string.IsNullOrWhiteSpace(highscore.UserName))
                                    && (!string.IsNullOrWhiteSpace(GameOptions.userName) || !string.IsNullOrEmpty(GameOptions.userName)))
                                {
                                    highscore.UserName = GameOptions.userName;
                                    highscores.Add(highscore);
                                }
                                // if username != null or empty, add to list
                                // this will catch if user has logged in
                                if (!string.IsNullOrEmpty(highscore.UserName) || !string.IsNullOrWhiteSpace(highscore.UserName))
                                {
                                    highscores.Add(highscore);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log("EXCEPTION : " + e);
            DatabaseLocked = false;
            return null;
        }
        databaseLocked = false;
        return highscores;
    }

    public bool DatabaseLocked { get => databaseLocked; set => databaseLocked = value; }
}

public class CharacterProfileRecord
{
    public int PlayerId { get; set; }
    public string PlayerDisplayName { get; set; }
    public string PlayerObjectName { get; set; }
    public float Accuracy2Pt { get; set; }
    public float Accuracy3Pt { get; set; }
    public float Accuracy4Pt { get; set; }
    public float Accuracy7Pt { get; set; }
    public float JumpForce { get; set; }
    public float Speed { get; set; }
    public float RunSpeed { get; set; }
    public float RunSpeedHasBall { get; set; }
    public int Luck { get; set; }
    public int ShootAngle { get; set; }
    public int Experience { get; set; }
    public int Level { get; set; }
    public int PointsAvailable { get; set; }
    public int PointsUsed { get; set; }
    public int Range { get; set; }
    public int Release { get; set; }
    public bool IsLocked { get; set; }

    public static CharacterProfileRecord FromProfile(CharacterProfile profile)
    {
        return new CharacterProfileRecord
        {
            PlayerId = profile.PlayerId,
            PlayerDisplayName = profile.PlayerDisplayName,
            PlayerObjectName = profile.PlayerObjectName,
            Accuracy2Pt = profile.Accuracy2Pt,
            Accuracy3Pt = profile.Accuracy3Pt,
            Accuracy4Pt = profile.Accuracy4Pt,
            Accuracy7Pt = profile.Accuracy7Pt,
            JumpForce = profile.JumpForce,
            Speed = profile.Speed,
            RunSpeed = profile.RunSpeed,
            RunSpeedHasBall = profile.RunSpeedHasBall,
            Luck = profile.Luck,
            ShootAngle = profile.ShootAngle,
            Experience = profile.Experience,
            Level = profile.Level,
            PointsAvailable = profile.PointsAvailable,
            PointsUsed = profile.PointsUsed,
            Range = profile.Range,
            Release = profile.Release,
            IsLocked = profile.IsLocked
        };
    }
}

public class CheerleaderProfileRecord
{
    public int CheerleaderId { get; set; }
    public string CheerleaderDisplayName { get; set; }
    public string CheerleaderObjectName { get; set; }
    public string UnlockCharacterText { get; set; }
    public bool IsLocked { get; set; }

    public static CheerleaderProfileRecord FromProfile(CheerleaderProfile profile)
    {
        return new CheerleaderProfileRecord
        {
            CheerleaderId = profile.CheerleaderId,
            CheerleaderDisplayName = profile.CheerleaderDisplayName,
            CheerleaderObjectName = profile.CheerleaderObjectName,
            UnlockCharacterText = profile.UnlockCharacterText,
            IsLocked = profile.IsLocked
        };
    }
}
