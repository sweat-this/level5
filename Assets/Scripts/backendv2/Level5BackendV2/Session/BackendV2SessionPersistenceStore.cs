using System;
using System.IO;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Persists the Backend V2 refresh token to local disk so a player is not signed out of
    /// correspondence every time the app restarts.
    ///
    /// Plaintext JSON via <c>AtomicFile</c> - the same storage convention this project already gives
    /// every other piece of local save data (<c>CharacterProgressStore</c>,
    /// <c>FileVersusSeriesRepository</c>, <c>PendingProgressionStore</c>), with no additional
    /// OS-keychain/secure-storage layer. A refresh token is a bearer credential: anyone with read
    /// access to this file or the device can act as this account until the token is revoked or
    /// expires. This is a deliberate, documented tradeoff (see docs/backend-v2-client.md), not an
    /// oversight.
    /// </summary>
    public static class BackendV2SessionPersistenceStore
    {
        private const string FileName = "backendv2_session.json";

        public static void Save(BackendV2Session session)
        {
            if (session == null)
            {
                Clear();
                return;
            }

            PersistedSession data = new PersistedSession
            {
                accessToken = session.AccessToken,
                expiresAtUnixSeconds = session.ExpiresAt.ToUnixTimeSeconds(),
                playerId = session.PlayerId.ToString(),
                refreshToken = session.RefreshToken,
                refreshTokenExpiresAtUnixSeconds = session.RefreshTokenExpiresAt.ToUnixTimeSeconds(),
            };

            try
            {
                AtomicFile.WriteAllText(GetPath(), JsonUtility.ToJson(data));
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not persist the Backend V2 session: " + exception);
            }
        }

        /// <summary>Loads a previously-saved session. Returns false, and clears the file, when there
        /// is nothing usable - no file, unparsable contents, or a refresh token already past its own
        /// expiry (handing back a session that can only fail a refresh serves no one).</summary>
        public static bool TryLoad(out BackendV2Session session)
        {
            session = null;
            if (!AtomicFile.TryReadAllText(GetPath(), IsValid, out string json))
            {
                return false;
            }

            try
            {
                PersistedSession data = JsonUtility.FromJson<PersistedSession>(json);
                if (data == null
                    || string.IsNullOrEmpty(data.refreshToken)
                    || !Guid.TryParse(data.playerId, out Guid playerId))
                {
                    // Structurally valid JSON but semantically unusable - same as an expired refresh
                    // token below, this can only ever fail again, so clear it now rather than leaving
                    // a file that fails to restore on every future launch forever without ever
                    // self-healing.
                    Clear();
                    return false;
                }

                DateTimeOffset refreshExpiresAt = DateTimeOffset.FromUnixTimeSeconds(data.refreshTokenExpiresAtUnixSeconds);
                if (refreshExpiresAt <= DateTimeOffset.UtcNow)
                {
                    Clear();
                    return false;
                }

                session = new BackendV2Session(
                    data.accessToken,
                    DateTimeOffset.FromUnixTimeSeconds(data.expiresAtUnixSeconds),
                    playerId,
                    data.refreshToken,
                    refreshExpiresAt);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not read the persisted Backend V2 session: " + exception);
                return false;
            }
        }

        public static void Clear()
        {
            try
            {
                string path = GetPath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // AtomicFile.WriteAllText also writes a "<path>.bak" copy on every save, and
                // AtomicFile.TryReadAllText falls back to reading it when the primary file is
                // missing - deleting only the primary here would leave TryLoad still finding a
                // stale session through that fallback.
                string backupPath = path + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not clear the persisted Backend V2 session: " + exception);
            }
        }

        private static bool IsValid(string json)
        {
            try
            {
                return JsonUtility.FromJson<PersistedSession>(json) != null;
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

        [Serializable]
        private sealed class PersistedSession
        {
            public string accessToken;
            public long expiresAtUnixSeconds;
            public string playerId;
            public string refreshToken;
            public long refreshTokenExpiresAtUnixSeconds;
        }
    }
}
