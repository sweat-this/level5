using System;
using System.IO;
using Level5.BackendV2.Tests;
using Mono.Data.Sqlite;
using UnityEngine;

/// <summary>
/// Spins up a real <see cref="DBConnector"/>/<see cref="DBHelper"/> pair against a throwaway SQLite
/// file instead of <see cref="Application.persistentDataPath"/>, so EditMode tests can exercise real
/// schema creation and queries without ever touching the developer's actual local-profile database.
///
/// No prior test in this suite opened live SQLite - every existing DB-adjacent test either leaves
/// <see cref="DBHelper.instance"/> null to exercise a null-guard, or scans source text for the
/// absence of a DB reference. This harness relies on two internal, test-only seams added alongside it
/// (<see cref="DBHelper.ConfigureForTests"/>, <see cref="DBConnector.ConfigureForTests"/>, and
/// widening <see cref="DBConnector.createDatabase"/> from private to internal) rather than mocking
/// SQLite itself - the schema/migration SQL in <see cref="DBConnector"/> is exactly what should run.
///
/// <see cref="DBConnector.instance"/>/<see cref="DBHelper.instance"/> are process-wide singletons whose
/// own Awake() destroys a second instance outright if one is already registered - and other test
/// fixtures in this suite (e.g. AccountTextMeshProMigrationTests) open real scenes containing their own
/// "database" GameObject with live DBConnector/DBHelper components, which can leave a stale `instance`
/// registered for the rest of the same batch test run. Create()/Destroy() save and restore whatever was
/// registered before/after this harness runs, the same discipline GameOptionsSnapshot uses for other
/// process-wide statics, so this harness's fresh components are never silently destroyed by that guard
/// and other tests get back whatever they had before.
/// </summary>
internal static class DBHelperTestHarness
{
    internal readonly struct Session
    {
        internal Session(
            GameObject gameObject,
            DBConnector connector,
            DBHelper helper,
            string dbPath,
            DBConnector previousConnectorInstance,
            DBHelper previousHelperInstance)
        {
            GameObject = gameObject;
            Connector = connector;
            Helper = helper;
            DbPath = dbPath;
            PreviousConnectorInstance = previousConnectorInstance;
            PreviousHelperInstance = previousHelperInstance;
        }

        internal GameObject GameObject { get; }
        internal DBConnector Connector { get; }
        internal DBHelper Helper { get; }
        internal string DbPath { get; }
        internal DBConnector PreviousConnectorInstance { get; }
        internal DBHelper PreviousHelperInstance { get; }
    }

    internal static Session Create()
    {
        DBConnector previousConnectorInstance = DBConnector.instance;
        DBHelper previousHelperInstance = DBHelper.instance;

        // Cleared (not just overwritten) before AddComponent below - Awake() destroys the new
        // GameObject outright if it finds a pre-existing `instance` that isn't itself.
        DBConnector.instance = null;
        DBHelper.instance = null;

        try
        {
            string dbPath = Path.Combine(Path.GetTempPath(), "level5-test-" + Guid.NewGuid().ToString("N") + ".db");

            GameObject gameObject = new GameObject("DBHelperTestHarness");
            DBHelper helper = gameObject.AddComponent<DBHelper>();
            DBConnector connector = gameObject.AddComponent<DBConnector>();

            helper.ConfigureForTests(dbPath);
            connector.ConfigureForTests(dbPath, helper);

            SqliteConnection.CreateFile(dbPath);
            CoroutineTestRunner.RunToCompletion(connector.createDatabase());

            return new Session(gameObject, connector, helper, dbPath, previousConnectorInstance, previousHelperInstance);
        }
        catch
        {
            // A failure anywhere above (schema creation, file creation, ...) must not strand the real
            // pre-existing singletons cleared to null above - a test whose [SetUp] throws here still
            // runs [TearDown], and TearDown must restore what was actually there before this call, not
            // a default(Session)'s null previous-instance fields.
            DBConnector.instance = previousConnectorInstance;
            DBHelper.instance = previousHelperInstance;
            throw;
        }
    }

    internal static void Destroy(Session session)
    {
        if (session.GameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(session.GameObject);
        }

        DeleteFileBestEffort(session.DbPath);

        DBConnector.instance = session.PreviousConnectorInstance;
        DBHelper.instance = session.PreviousHelperInstance;
    }

    /// <summary>
    /// DBHelper.OnDestroy() closes/disposes the shared SqliteConnection synchronously, but the
    /// underlying native file handle has been observed to release a moment later than that call
    /// returns - a bare File.Delete right after DestroyImmediate can still find it locked. Retrying
    /// after a GC pass (which finalizes anything the provider itself left pending) clears it every
    /// time seen in practice; this is temp-file housekeeping, not correctness, so a persistent failure
    /// is logged rather than failing the test.
    /// </summary>
    private static void DeleteFileBestEffort(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        Debug.LogWarning("DBHelperTestHarness: could not delete temp database file " + path);
    }
}
