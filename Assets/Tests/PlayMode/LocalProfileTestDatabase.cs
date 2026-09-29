#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A throwaway SQLite database standing in for <c>Application.persistentDataPath/level5.db</c> while a
/// PlayMode test drives the real account scenes, so no test ever reads or writes the developer's real
/// local profiles.
///
/// The account scenes each carry their own <c>database</c> prefab (DBConnector + DBHelper). Both
/// singletons destroy a second instance in their own <c>Awake</c>, so registering this helper's
/// components first makes every scene's prefab instance discard itself and leaves the scenes talking to
/// this file - the same mechanism the EditMode <c>DBHelperTestHarness</c> relies on, driven through
/// reflection here because <c>DBHelper</c>/<c>DBConnector</c> live in Unity's implicit default assembly
/// which a named assembly definition (this test assembly) can never reference.
///
/// <see cref="Close"/> followed by <see cref="Open"/> is the in-process equivalent of an app restart
/// against a database that already holds profiles: the components, their connection and every cached
/// reference are discarded and rebuilt against the same file, so only what SQLite durably committed
/// survives.
/// </summary>
internal sealed class LocalProfileTestDatabase
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type HelperType = FindType("DBHelper");
    private static readonly Type ConnectorType = FindType("DBConnector");

    private GameObject gameObject;
    private Component helper;
    private Component connector;
    private object previousConnectorInstance;
    private object previousHelperInstance;

    internal LocalProfileTestDatabase()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "level5-playmode-profile-" + Guid.NewGuid().ToString("N") + ".db");
    }

    internal string Path { get; }

    internal bool IsOpen => gameObject != null;

    private static Type FindType(string name)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null)
            {
                return type;
            }
        }

        Assert.Fail("Could not resolve production type " + name);
        return null;
    }

    private static FieldInfo InstanceField(Type type) => type.GetField("instance", BindingFlags.Public | BindingFlags.Static);

    /// <summary>
    /// Registers fresh DBConnector/DBHelper components against <see cref="Path"/> and waits for the
    /// schema to be created (creating the file first if it does not exist yet).
    /// </summary>
    internal IEnumerator Open()
    {
        Assert.That(IsOpen, Is.False, "the test database is already open");

        previousConnectorInstance = InstanceField(ConnectorType).GetValue(null);
        previousHelperInstance = InstanceField(HelperType).GetValue(null);
        // Cleared (not just overwritten): each component's Awake destroys its own GameObject when it
        // finds a different, still-registered instance.
        InstanceField(ConnectorType).SetValue(null, null);
        InstanceField(HelperType).SetValue(null, null);

        gameObject = new GameObject("LocalProfileTestDatabase");
        helper = gameObject.AddComponent(HelperType);
        connector = gameObject.AddComponent(ConnectorType);

        // Both are internal test seams. Awake has already run by now (pointing at the real
        // persistentDataPath) but nothing has touched the database yet - DBConnector.Start, which
        // creates/migrates the schema, runs next frame, after this redirect.
        HelperType.GetMethod("ConfigureForTests", AnyInstance).Invoke(helper, new object[] { Path });
        ConnectorType.GetMethod("ConfigureForTests", AnyInstance).Invoke(connector, new object[] { Path, helper });

        float deadline = Time.realtimeSinceStartup + 20f;
        while (!(bool)ConnectorType.GetProperty("DatabaseCreated").GetValue(connector) || DatabaseLocked)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "the test database was not created in time");
            yield return null;
        }
    }

    /// <summary>
    /// Discards the components and their SQLite connection but keeps the file, restoring whatever
    /// singletons were registered before <see cref="Open"/>.
    /// </summary>
    internal void Close()
    {
        if (gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            gameObject = null;
            helper = null;
            connector = null;
        }

        InstanceField(ConnectorType).SetValue(null, previousConnectorInstance);
        InstanceField(HelperType).SetValue(null, previousHelperInstance);
    }

    /// <summary>Closes the database and deletes its file (best effort - never fails a test).</summary>
    internal void Dispose()
    {
        Close();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }

                return;
            }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        Debug.LogWarning("LocalProfileTestDatabase: could not delete temp database file " + Path);
    }

    internal bool DatabaseLocked => (bool)HelperType.GetProperty("DatabaseLocked").GetValue(helper);

    private IDbConnection Connection => (IDbConnection)HelperType.GetProperty("Connection", AnyInstance).GetValue(helper);

    internal bool HasCharacterProfilesForAccount(string accountId)
    {
        return (bool)HelperType.GetMethod("HasCharacterProfilesForAccount").Invoke(helper, new object[] { accountId });
    }

    internal bool EnsureCharacterProfilesForAccount(string accountId)
    {
        return (bool)HelperType.GetMethod("EnsureCharacterProfilesForAccount").Invoke(helper, new object[] { accountId });
    }

    /// <summary>Reads a scope's character progression through the production query the loading scene uses.</summary>
    internal int ReadCharacterProfileCountThroughProduction(int userid)
    {
        object records = HelperType.GetMethod("getCharacterProfileStats").Invoke(helper, new object[] { userid });
        return ((System.Collections.ICollection)records).Count;
    }

    internal void SeedCharacterProfile(string accountId, int charid)
    {
        using (IDbCommand command = Connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO CharacterProfile (accountId, charid, playerName, objectName, accuracy2, accuracy3, accuracy4, accuracy7, "
                + "jump, speed, runSpeed, runSpeedHasBall, luck, shootAngle, experience, level, pointsAvailable, pointsUsed, range, "
                + "release, isLocked) VALUES (@accountId, @charid, 'name', 'object', 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 10, 1, 0, 0, 1, 1, 0)";
            AddParameter(command, "@accountId", accountId);
            AddParameter(command, "@charid", charid);
            command.ExecuteNonQuery();
        }
    }

    internal int CountRows(string table, string whereColumn = null, object whereValue = null)
    {
        using (IDbCommand command = Connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM " + table
                + (whereColumn == null ? string.Empty : " WHERE " + whereColumn + " = @value");
            if (whereColumn != null)
            {
                AddParameter(command, "@value", whereValue);
            }

            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    internal List<KeyValuePair<int, string>> ReadUsers()
    {
        List<KeyValuePair<int, string>> users = new List<KeyValuePair<int, string>>();
        using (IDbCommand command = Connection.CreateCommand())
        {
            command.CommandText = "SELECT userid, username FROM User ORDER BY userid";
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    users.Add(new KeyValuePair<int, string>(reader.GetInt32(0), reader.GetString(1)));
                }
            }
        }

        return users;
    }

    /// <summary>Inserts a User row the way a V1-era database would already hold one (server-assigned id).</summary>
    internal void SeedUser(int userid, string username, DateTime lastLoginUtc)
    {
        using (IDbCommand command = Connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO User(userid, username, firstname, lastname, email, ipaddress, signupdate, lastlogin) "
                + "VALUES (@userid, @username, '', '', '', '', @stamp, @stamp)";
            AddParameter(command, "@userid", userid);
            AddParameter(command, "@username", username);
            AddParameter(command, "@stamp", lastLoginUtc.ToString("o"));
            command.ExecuteNonQuery();
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
#endif
