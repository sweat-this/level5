
using Assets.Scripts.database;
using Mono.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class DBConnector : MonoBehaviour
{
    private String databaseNamePath = "/level5.db";
    private String filepath;

    const String verifyDatabaseSqlQuery = "SELECT name FROM sqlite_master WHERE type='table';";

    private const int currentDatabaseAppVersion = 7; // 3/25/21

    Text messageText;

    bool databaseCreated = false;
    public bool DatabaseCreated { get => databaseCreated; }

    DBHelper dbHelper;
    public static DBConnector instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Awake()
    {
        // keep Database object persistent
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        filepath = Application.persistentDataPath + databaseNamePath;

        dbHelper = gameObject.GetComponent<DBHelper>();
    }

    void Start()
    {
        // create database / add tables if not exist
        if (File.Exists(filepath))
        //|| !Application.version.Equals(getDatabaseVersion()))// && integrityCheck())
        {
            try
            {
                StartCoroutine(createDatabase());
            }
            catch (Exception e)
            {
                dbHelper.DatabaseLocked = false;
                Debug.Log("ERROR : " + e);
                return;
            }
        }
        // if database doesnt exist
        if (!File.Exists(filepath))
        {
            try
            {
                SqliteConnection.CreateFile(filepath);
                StartCoroutine(dropDatabase());
                StartCoroutine(createDatabase());
            }
            catch (Exception e)
            {
                dbHelper.DatabaseLocked = false;
                Debug.Log("ERROR : " + e);
                return;
            }
        }
        //if (getDatabaseVersion() < dbHelper.CurrentDatabaseAppVersion)
        //{
        //    //StartCoroutine(dropDatabase());
        //    //StartCoroutine(createDatabase());
        //    StartCoroutine(dbHelper.UpgradeDatabaseToVersion3());
        //    StartCoroutine(setDatabaseVersion());
        //}
    }

    // ensures a table has every column the current app version expects.
    // lets users who upgrade the app (rather than reinstalling) keep using their existing
    // sqlite file even after new columns were added to a table's schema.
    private static void EnsureTableColumns(IDbConnection dbconn, string tableName, KeyValuePair<string, string>[] expectedColumns)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (IDbCommand pragmaCmd = dbconn.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info(" + tableName + ")";
            using (IDataReader pragmaReader = pragmaCmd.ExecuteReader())
            {
                int nameIndex = pragmaReader.GetOrdinal("name");
                while (pragmaReader.Read())
                {
                    existingColumns.Add(pragmaReader.GetString(nameIndex));
                }
            }
        }

        foreach (KeyValuePair<string, string> column in expectedColumns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            using (IDbCommand alterCmd = dbconn.CreateCommand())
            {
                alterCmd.CommandText = "ALTER TABLE " + tableName + " ADD COLUMN " + column.Key + " " + column.Value;
                alterCmd.ExecuteNonQuery();
            }
        }
    }

    private static readonly KeyValuePair<string, string>[] HighScoresExpectedColumns = new[]
    {
        new KeyValuePair<string, string>("scoreidUnique", "TEXT"),
        new KeyValuePair<string, string>("modeid", "INTEGER"),
        new KeyValuePair<string, string>("characterid", "INTEGER"),
        new KeyValuePair<string, string>("character", "TEXT"),
        new KeyValuePair<string, string>("levelid", "INTEGER"),
        new KeyValuePair<string, string>("level", "TEXT"),
        new KeyValuePair<string, string>("os", "TEXT"),
        new KeyValuePair<string, string>("version", "TEXT"),
        new KeyValuePair<string, string>("date", "TEXT"),
        new KeyValuePair<string, string>("time", "REAL"),
        new KeyValuePair<string, string>("totalPoints", "INTEGER"),
        new KeyValuePair<string, string>("longestShot", "REAL"),
        new KeyValuePair<string, string>("totalDistance", "REAL"),
        new KeyValuePair<string, string>("maxShotMade", "INTEGER"),
        new KeyValuePair<string, string>("maxShotAtt", "INTEGER"),
        new KeyValuePair<string, string>("consecutiveShots", "INTEGER"),
        new KeyValuePair<string, string>("trafficEnabled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("hardcoreEnabled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("enemiesEnabled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("enemiesKilled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("platform", "TEXT"),
        new KeyValuePair<string, string>("device", "TEXT"),
        new KeyValuePair<string, string>("ipaddress", "TEXT"),
        new KeyValuePair<string, string>("twoMade", "INTEGER"),
        new KeyValuePair<string, string>("twoAtt", "INTEGER"),
        new KeyValuePair<string, string>("threeMade", "INTEGER"),
        new KeyValuePair<string, string>("threeAtt", "INTEGER"),
        new KeyValuePair<string, string>("fourMade", "INTEGER"),
        new KeyValuePair<string, string>("fourAtt", "INTEGER"),
        new KeyValuePair<string, string>("sevenMade", "INTEGER"),
        new KeyValuePair<string, string>("sevenAtt", "INTEGER"),
        new KeyValuePair<string, string>("bonusPoints", "INTEGER"),
        new KeyValuePair<string, string>("moneyBallMade", "INTEGER"),
        new KeyValuePair<string, string>("moneyBallAtt", "INTEGER"),
        new KeyValuePair<string, string>("submittedToApi", "INTEGER"),
        new KeyValuePair<string, string>("userName", "TEXT DEFAULT NULL"),
        new KeyValuePair<string, string>("sniperEnabled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("sniperMode", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("sniperModeName", "TEXT DEFAULT 'none'"),
        new KeyValuePair<string, string>("sniperHits", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("sniperShots", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p1TotalPoints", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p2TotalPoints", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p3TotalPoints", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p4TotalPoints", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("first", "TEXT DEFAULT NULL"),
        new KeyValuePair<string, string>("second", "TEXT DEFAULT NULL"),
        new KeyValuePair<string, string>("third", "TEXT DEFAULT NULL"),
        new KeyValuePair<string, string>("fourth", "TEXT DEFAULT NULL"),
        new KeyValuePair<string, string>("p1IsCpu", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p2IsCpu", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p3IsCpu", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("p4IsCpu", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("numPlayers", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("difficulty", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("campaignWins", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("campaignLosses", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("campaignTies", "INTEGER DEFAULT 0"),
    };

    private static readonly KeyValuePair<string, string>[] AllTimeStatsExpectedColumns = new[]
    {
        new KeyValuePair<string, string>("twoMade", "INTEGER"),
        new KeyValuePair<string, string>("twoAtt", "INTEGER"),
        new KeyValuePair<string, string>("threeMade", "INTEGER"),
        new KeyValuePair<string, string>("threeAtt", "INTEGER"),
        new KeyValuePair<string, string>("fourMade", "INTEGER"),
        new KeyValuePair<string, string>("fourAtt", "INTEGER"),
        new KeyValuePair<string, string>("sevenMade", "INTEGER"),
        new KeyValuePair<string, string>("sevenAtt", "INTEGER"),
        new KeyValuePair<string, string>("moneyBallMade", "INTEGER"),
        new KeyValuePair<string, string>("moneyBallAtt", "INTEGER"),
        new KeyValuePair<string, string>("totalPoints", "INTEGER"),
        new KeyValuePair<string, string>("totalDistance", "REAL"),
        new KeyValuePair<string, string>("longestShot", "REAL"),
        new KeyValuePair<string, string>("timePlayed", "REAL"),
        new KeyValuePair<string, string>("enemiesKilled", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("sniperHits", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("sniperShots", "INTEGER DEFAULT 0"),
    };

    private static readonly KeyValuePair<string, string>[] CharacterProfileExpectedColumns = new[]
    {
        new KeyValuePair<string, string>("accountId", "TEXT NOT NULL DEFAULT 'legacy'"),
        new KeyValuePair<string, string>("charid", "INTEGER"),
        new KeyValuePair<string, string>("playerName", "TEXT"),
        new KeyValuePair<string, string>("objectName", "TEXT"),
        new KeyValuePair<string, string>("accuracy2", "INTEGER"),
        new KeyValuePair<string, string>("accuracy3", "INTEGER"),
        new KeyValuePair<string, string>("accuracy4", "INTEGER"),
        new KeyValuePair<string, string>("accuracy7", "INTEGER"),
        new KeyValuePair<string, string>("jump", "float"),
        new KeyValuePair<string, string>("speed", "float"),
        new KeyValuePair<string, string>("runSpeed", "float"),
        new KeyValuePair<string, string>("runSpeedHasBall", "float"),
        new KeyValuePair<string, string>("luck", "INTEGER"),
        new KeyValuePair<string, string>("shootAngle", "INTEGER"),
        new KeyValuePair<string, string>("experience", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("level", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("pointsAvailable", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("pointsUsed", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("range", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("release", "INTEGER DEFAULT 0"),
        new KeyValuePair<string, string>("isLocked", "INTEGER DEFAULT 0"),
    };

    // NOTE: 'password' is intentionally still ensured here so old rows keep a stable schema;
    // the app no longer writes a value into it (see DBHelper.InsertUserCoroutine) and any
    // previously-stored plaintext password is scrubbed in createDatabase().
    private static readonly KeyValuePair<string, string>[] UserExpectedColumns = new[]
    {
        new KeyValuePair<string, string>("firstname", "TEXT"),
        new KeyValuePair<string, string>("lastname", "TEXT"),
        new KeyValuePair<string, string>("email", "TEXT"),
        new KeyValuePair<string, string>("ipaddress", "TEXT"),
        new KeyValuePair<string, string>("signupdate", "TEXT"),
        new KeyValuePair<string, string>("lastlogin", "TEXT"),
        new KeyValuePair<string, string>("password", "TEXT"),
        new KeyValuePair<string, string>("bearerToken", "TEXT"),
    };

    // Single source of truth for each table's CREATE TABLE statement. These used to be defined a
    // second time in standalone createTableUser()/createTableAllTimeStats() methods (both dead -
    // grepped, never called - now deleted) and a third time, with a real schema drift
    // (playerName/objectName NOT NULL), in CreateTableCharacterProfile() below. createDatabase()
    // runs first for every install, so that NOT NULL version's own CREATE TABLE IF NOT EXISTS was
    // a permanent no-op - dead intent, not dead code. Standardized on the constraint that has
    // actually been live (no NOT NULL), since nothing has ever enforced the stricter one.
    private static readonly string HighScoresCreateTableSql =
        $"CREATE TABLE if not exists {Constants.LOCAL_DATABASE_tableName_highscores}(" +
        "scoreid   INTEGER PRIMARY KEY AUTOINCREMENT," +
        "scoreidUnique   TEXT," +
        "modeid    INTEGER, " +
        "characterid   INTEGER, " +
        "character   TEXT, " +
        "levelid   INTEGER, " +
        "level    TEXT, " +
        "os    TEXT, " +
        "version   TEXT, " +
        "date  TEXT, " +
        "time  REAL, " +
        "totalPoints   INTEGER, " +
        "longestShot   REAL, " +
        "totalDistance REAL, " +
        "maxShotMade   INTEGER, " +
        "maxShotAtt    INTEGER, " +
        "consecutiveShots   INTEGER," +
        "trafficEnabled	INTEGER DEFAULT 0," +
        "hardcoreEnabled INTEGER DEFAULT 0, " +
        "enemiesEnabled INTEGER DEFAULT 0, " +
        "enemiesKilled INTEGER DEFAULT 0," +
        "platform    TEXT," +
        "device    TEXT," +
        "ipaddress   TEXT," +
        "twoMade   INTEGER, " +
        "twoAtt    INTEGER, " +
        "threeMade INTEGER, " +
        "threeAtt  INTEGER, " +
        "fourMade  INTEGER, " +
        "fourAtt   INTEGER, " +
        "sevenMade INTEGER, " +
        "sevenAtt  INTEGER, " +
        "bonusPoints  INTEGER, " +
        "moneyBallMade  INTEGER, " +
        "moneyBallAtt  INTEGER, " +
        "submittedToApi  INTEGER, " +
        "userName  TEXT DEFAULT NULL, " +
        "sniperEnabled  INTEGER DEFAULT 0, " +
        "sniperMode  INTEGER DEFAULT 0, " +
        "sniperModeName  TEXT DEFAULT 'none', " +
        "sniperHits  INTEGER DEFAULT 0, " +
        "sniperShots  INTEGER DEFAULT 0," +
        "p1TotalPoints INTEGER DEFAULT 0," +
        "p2TotalPoints INTEGER DEFAULT 0," +
        "p3TotalPoints INTEGER DEFAULT 0," +
        "p4TotalPoints INTEGER DEFAULT 0," +
        "first  TEXT DEFAULT NULL, " +
        "second  TEXT DEFAULT NULL, " +
        "third  TEXT DEFAULT NULL, " +
        "fourth  TEXT DEFAULT NULL, " +
        "p1IsCpu  INTEGER DEFAULT 0," +
        "p2IsCpu  INTEGER DEFAULT 0," +
        "p3IsCpu  INTEGER DEFAULT 0," +
        "p4IsCpu  INTEGER DEFAULT 0," +
        "numPlayers  INTEGER DEFAULT 0," +
        "difficulty  INTEGER DEFAULT 0," +
        "campaignWins  INTEGER DEFAULT 0," +
        "campaignLosses  INTEGER DEFAULT 0," +
        "campaignTies  INTEGER DEFAULT 0);";

    private static readonly string AllTimeStatsCreateTableSql =
        $"CREATE TABLE if not exists {Constants.LOCAL_DATABASE_tableName_allTimeStats}(" +
        "userid INTEGER UNIQUE," +
        "twoMade   INTEGER, " +
        "twoAtt    INTEGER, " +
        "threeMade INTEGER, " +
        "threeAtt  INTEGER, " +
        "fourMade  INTEGER, " +
        "fourAtt   INTEGER, " +
        "sevenMade INTEGER, " +
        "sevenAtt  INTEGER, " +
        "moneyBallMade INTEGER, " +
        "moneyBallAtt  INTEGER, " +
        "totalPoints  INTEGER, " +
        "totalDistance REAL, " +
        "longestShot REAL, " +
        "timePlayed   REAL," +
        "enemiesKilled INTEGER DEFAULT 0," +
        "sniperHits INTEGER DEFAULT 0," +
        "sniperShots INTEGER DEFAULT 0); ";

    private static readonly string CharacterProfileCreateTableSql =
        $"CREATE TABLE if not exists {Constants.LOCAL_DATABASE_tableName_characterProfile}(" +
        "id   INTEGER PRIMARY KEY, " +
        "accountId TEXT NOT NULL DEFAULT 'legacy', " +
        "charid   INTEGER, " +
        "playerName   TEXT," +
        "objectName   TEXT," +
        "accuracy2   INTEGER," +
        "accuracy3   INTEGER," +
        "accuracy4   INTEGER," +
        "accuracy7   INTEGER," +
        "jump   float," +
        "speed   float," +
        "runSpeed   float," +
        "runSpeedHasBall   float," +
        "luck   INTEGER," +
        "shootAngle   INTEGER," +
        "experience   INTEGER DEFAULT 0," +
        "level   INTEGER DEFAULT 0," +
        "pointsAvailable   INTEGER DEFAULT 0," +
        "pointsUsed   INTEGER DEFAULT 0," +
        "range   INTEGER DEFAULT 0," +
        "release   INTEGER DEFAULT 0," +
        "isLocked   INTEGER DEFAULT 0);";

    private static readonly string UserCreateTableSql =
        $"CREATE TABLE if not exists {Constants.LOCAL_DATABASE_tableName_user}( " +
        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
        "userid INTEGER UNIQUE," +
        "username  TEXT UNIQUE, " +
        "firstname TEXT, " +
        "lastname  TEXT, " +
        "email TEXT, " +
        "ipaddress TEXT, " +
        "signupdate TEXT, " +
        "lastlogin TEXT, " +
        "password TEXT, " +
        "bearerToken TEXT);";

    private static readonly string CheerleaderProfileCreateTableSql =
        $"CREATE TABLE if not exists {Constants.LOCAL_DATABASE_tableName_cheerleaderProfile}(" +
        "cid   INTEGER PRIMARY KEY, " +
        "name   TEXT NOT NULL," +
        "objectName   TEXT NOT NULL," +
        "unlockText   TEXT NOT NULL," +
        "islocked  INTEGER DEFAULT 0);";

    // Runs on DBHelper's shared connection without acquiring DatabaseLocked itself - safe because
    // its only caller, createDatabase(), already holds the lock for the whole call chain this
    // method is part of. Adding a check-first guard here would be wrong, not just redundant: it
    // would see the lock its own caller is holding and refuse to run.
    private void VerifyDatabase()
    {
        string version = "";

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = verifyDatabaseSqlQuery;
                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            version = reader.GetString(0);
                        }
                    }
                }
            }

            databaseCreated = true;
            dbHelper.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            dbHelper.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    //private int getDatabaseVersion()
    //{
    //    int value = 0;
    //    try
    //    {
    //        string sqlQuery = "PRAGMA user_version";
    //        IDbConnection dbconn;
    //        dbconn = (IDbConnection)new SqliteConnection(connection);
    //        dbconn.Open(); //Open connection to the database.
    //        IDbCommand dbcmd = dbconn.CreateCommand();

    //        dbcmd.CommandText = sqlQuery;
    //        IDataReader reader = dbcmd.ExecuteReader();

    //        while (reader.Read())
    //        {
    //            value = reader.GetInt32(0);
    //        }
    //        reader.Close();
    //        reader = null;
    //        dbcmd.Dispose();
    //        dbcmd = null;
    //        dbconn.Close();
    //        dbconn = null;
    //    }
    //    catch (Exception e)
    //    {
    //        dbHelper.DatabaseLocked = false;
    //        Debug.Log("ERROR : " + e);
    //        return 0;
    //    }

    //    return value;
    //}

    //public IEnumerator setDatabaseVersion()
    //{
    //    yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
    //    try
    //    {
    //        dbHelper.DatabaseLocked = true;
    //        dbconn = new SqliteConnection(connection);
    //        dbconn.Open();
    //        dbcmd = dbconn.CreateCommand();

    //        string sqlQuery = "PRAGMA main.user_version = '" + currentDatabaseAppVersion + "'";

    //        dbcmd.CommandText = sqlQuery;
    //        dbcmd.ExecuteScalar();

    //        dbcmd.Dispose();
    //        dbcmd = null;
    //        dbconn.Close();
    //        dbconn = null;

    //        dbHelper.DatabaseLocked = false;
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.Log("ERROR : " + e);
    //        dbHelper.DatabaseLocked = false;
    //    }
    //}

    // ============================ Save stats ===============================
    public bool savePlayerGameStats(HighScoreModel dbHighScoreModel)
    {
        return dbHelper != null && dbHelper.InsertGameScore(dbHighScoreModel);
    }

    public bool savePlayerProfileProgression(float expGained)
    {
        return savePlayerProfileProgression(expGained, GameOptions.characterId);
    }

    public bool savePlayerProfileProgression(float expGained, int characterId)
    {
        return dbHelper.UpdatePlayerProfileProgression(expGained, characterId);
    }

    public ProgressionApplyStatus ApplyProgressionResult(
        string resultId,
        string accountId,
        float experienceGained,
        int characterId,
        out ProgressionSnapshot snapshot)
    {
        snapshot = null;
        return dbHelper == null
            ? ProgressionApplyStatus.Failed
            : dbHelper.ApplyProgressionResult(
                resultId,
                accountId,
                experienceGained,
                characterId,
                out snapshot);
    }

    public List<ProgressionSnapshot> GetPendingProgressionProjections(string accountId)
    {
        return dbHelper == null
            ? new List<ProgressionSnapshot>()
            : dbHelper.GetPendingProgressionProjections(accountId);
    }

    public bool MarkProgressionProjectionApplied(string resultId)
    {
        return dbHelper != null && dbHelper.MarkProgressionProjectionApplied(resultId);
    }

    public bool savePlayerAllTimeStats(GameStats stats)
    {
        return stats != null && savePlayerAllTimeStats(AllTimeStatsSnapshot.From(MatchSession.EnsureCurrentMatch(), stats));
    }

    public bool savePlayerAllTimeStats(AllTimeStatsSnapshot stats)
    {
        return dbHelper != null && stats != null && dbHelper.UpdateAllTimeStats(stats);
    }

    // =========================================================================

    // create tables if not created
    IEnumerator createDatabase()
    {
        yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
        dbHelper.DatabaseLocked = true;
        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;

                string sqlQuery =
                    HighScoresCreateTableSql +
                    "DROP TABLE if exists Achievements; " +
                    AllTimeStatsCreateTableSql +
                    CharacterProfileCreateTableSql +
                    UserCreateTableSql;

                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = sqlQuery;
                    dbcmd.ExecuteNonQuery();
                }

                // bring pre-existing local databases (from older app versions) up to date with
                // any columns added since they were first created - CREATE TABLE IF NOT EXISTS
                // above does nothing for a table that already exists but is missing new columns.
                EnsureTableColumns(dbconn, Constants.LOCAL_DATABASE_tableName_highscores, HighScoresExpectedColumns);
                EnsureTableColumns(dbconn, Constants.LOCAL_DATABASE_tableName_allTimeStats, AllTimeStatsExpectedColumns);
                EnsureTableColumns(dbconn, Constants.LOCAL_DATABASE_tableName_characterProfile, CharacterProfileExpectedColumns);
                EnsureTableColumns(dbconn, Constants.LOCAL_DATABASE_tableName_user, UserExpectedColumns);

                // local password comparison is never performed (login is authenticated against
                // the server); scrub any plaintext password persisted by older app versions.
                //
                // The bearer token gets the same treatment, and for a stronger reason: a password
                // is useless here without the server agreeing to it, but a token IS the credential
                // - anything holding one can act as that account until it expires. Older builds
                // wrote tokens into this column, so an upgraded install can still be carrying one
                // at rest. Nothing has written it for some time and nothing reads it back into a
                // session, so clearing it costs nothing and closes the exposure on every device
                // that launches this build.
                using (IDbCommand scrubCmd = dbconn.CreateCommand())
                {
                    scrubCmd.CommandText =
                        $"UPDATE {Constants.LOCAL_DATABASE_tableName_user} SET password = NULL WHERE password IS NOT NULL";
                    scrubCmd.ExecuteNonQuery();
                }

                using (IDbCommand scrubTokenCmd = dbconn.CreateCommand())
                {
                    scrubTokenCmd.CommandText =
                        $"UPDATE {Constants.LOCAL_DATABASE_tableName_user} SET bearerToken = NULL WHERE bearerToken IS NOT NULL";
                    scrubTokenCmd.ExecuteNonQuery();
                }
            }

            VerifyDatabase();

        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
            dbHelper.DatabaseLocked = false;
            //return;
        }
    }

    IEnumerator dropDatabase()
    {
        yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
        dbHelper.DatabaseLocked = true;

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // DROP TABLE [IF EXISTS] [schema_name.]table_name;
                    string sqlQuery =
                        //"DROP TABLE if exists AllTimeStats; " +
                        "DROP TABLE if exists Achievements; " +
                        //"DROP TABLE if exists CharacterProfile; " +
                        $"DROP TABLE if exists {Constants.LOCAL_DATABASE_tableName_cheerleaderProfile}; " +
                        $"DROP TABLE if exists {Constants.LOCAL_DATABASE_tableName_highscores}; ";
                    //"DROP TABLE if exists User; ";

                    dbcmd.CommandText = sqlQuery;
                    dbcmd.ExecuteNonQuery();
                }
            }

            dbHelper.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            dbHelper.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
            //return;
        }
    }

    public IEnumerator dropDatabaseTable(string tableName)
    {
        yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
        dbHelper.DatabaseLocked = true;

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    // DROP TABLE [IF EXISTS] [schema_name.]table_name;
                    dbcmd.CommandText = "DROP TABLE if exists " + tableName + ";";
                    dbcmd.ExecuteNonQuery();
                }
            }

            dbHelper.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            dbHelper.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }

    /// <summary>
    /// Runs on DBHelper's shared connection but deliberately does not check or acquire
    /// DatabaseLocked itself. A "busy" answer here would be indistinguishable from a genuine
    /// "table doesn't exist" to every caller, and returning it during a real lock-contention
    /// window could trigger unwanted re-seeding of existing data - worse than the caller waiting.
    /// LoadManager, the only current caller, waits for the lock to clear before calling this and
    /// the synchronous block around it, rather than this method guessing at what an unavailable
    /// answer should mean.
    /// </summary>
    public bool tableExists(string tableName)
    {
        int count = 0;
        string value = null;

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
                    dbcmd.Parameters.Add(new SqliteParameter("@tableName", tableName));

                    using (IDataReader reader = dbcmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            value = reader.GetString(0);
                            count++;
                        }
                    }
                }
            }

            // if correct table name is returned and at least 1 table names exists
            return count > 0 && value.Equals(tableName);
        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
            return false;
        }
    }

    public IEnumerator CreateTableCharacterProfile()
    {
        yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
        dbHelper.DatabaseLocked = true;

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = CharacterProfileCreateTableSql;
                    dbcmd.ExecuteNonQuery();
                }
            }

            dbHelper.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            dbHelper.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }

    public IEnumerator CreateTableCheerleaderProfile()
    {
        yield return new WaitUntil(() => !dbHelper.DatabaseLocked);
        dbHelper.DatabaseLocked = true;

        try
        {
            {
                IDbConnection dbconn = dbHelper.Connection;
                using (IDbCommand dbcmd = dbconn.CreateCommand())
                {
                    dbcmd.CommandText = CheerleaderProfileCreateTableSql;
                    dbcmd.ExecuteNonQuery();
                }
            }

            dbHelper.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            dbHelper.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }
}
