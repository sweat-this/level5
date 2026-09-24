using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Level5.BackendV2
{
    public enum PendingMatchResultEnqueueResult
    {
        /// <summary>A new entry was queued.</summary>
        Added,

        /// <summary>The exact same (owner, clientResultId, payload) was already queued - a no-op,
        /// not an error.</summary>
        AlreadyQueued,

        /// <summary>A different payload is already queued under this (owner, clientResultId) key.
        /// This should never happen locally; the existing entry is left untouched and this is
        /// surfaced as a local integrity error rather than silently choosing one payload.</summary>
        ConflictingPayload,

        /// <summary>The request was invalid, or the queue file could not be read/written.</summary>
        Failed
    }

    /// <summary>
    /// Durable local queue for general Backend V2 match-result submissions, keyed by
    /// <c>(OwnerPlayerId, ClientResultId)</c>.
    ///
    /// Deliberately its own file, never <c>pending-match-persistence.json</c>: that file owns local
    /// SQLite recovery, a different concern with a different owner and a different serializer
    /// (<c>JsonUtility</c>, which cannot represent <see cref="SubmitMatchResultDto"/>'s
    /// <c>Dictionary&lt;string,double&gt;</c> metrics). This store uses <see cref="BackendV2Json"/>
    /// (Newtonsoft) instead, the same serializer every other Backend V2 wire DTO already uses, and
    /// <see cref="AtomicFile"/> for the same crash-safe write/primary-backup-recovery read behavior
    /// <c>BackendV2SessionPersistenceStore</c> and <c>PendingMatchPersistenceStore</c> already rely on.
    /// </summary>
    public static class PendingMatchResultStore
    {
        private const string FileName = "backendv2_pending_match_results.json";

        public static PendingMatchResultEnqueueResult Enqueue(Guid ownerPlayerId, SubmitMatchResultDto request)
        {
            if (request == null || request.ClientResultId == Guid.Empty)
            {
                return PendingMatchResultEnqueueResult.Failed;
            }

            PendingMatchResultEnqueueResult outcome = PendingMatchResultEnqueueResult.Failed;
            bool saved = Update(data =>
            {
                PendingMatchResult existing = data.Entries.Find(entry =>
                    entry != null && entry.OwnerPlayerId == ownerPlayerId
                    && entry.Request != null && entry.Request.ClientResultId == request.ClientResultId);

                if (existing == null)
                {
                    data.Entries.Add(new PendingMatchResult(ownerPlayerId, request));
                    outcome = PendingMatchResultEnqueueResult.Added;
                    return;
                }

                if (existing.Request.IsEquivalentTo(request))
                {
                    outcome = PendingMatchResultEnqueueResult.AlreadyQueued;
                    return;
                }

                Debug.LogError(
                    "PendingMatchResultStore refused to enqueue a match result: clientResultId "
                    + request.ClientResultId + " is already queued for player " + ownerPlayerId
                    + " with a different payload. The existing queued entry was left untouched.");
                outcome = PendingMatchResultEnqueueResult.ConflictingPayload;
            });

            return saved ? outcome : PendingMatchResultEnqueueResult.Failed;
        }

        /// <summary>Every entry currently owned by <paramref name="ownerPlayerId"/> that has not
        /// been marked as a definitive failure - fresh from disk on every call, so a caller can use
        /// this both for an initial drain pass and to detect entries added since.</summary>
        public static List<PendingMatchResult> GetRetryable(Guid ownerPlayerId)
        {
            PendingMatchResultData data = Load();
            List<PendingMatchResult> retryable = new List<PendingMatchResult>();
            foreach (PendingMatchResult entry in data.Entries)
            {
                if (entry != null && entry.OwnerPlayerId == ownerPlayerId && !entry.DefinitiveFailure)
                {
                    retryable.Add(entry);
                }
            }

            return retryable;
        }

        public static bool Remove(Guid ownerPlayerId, Guid clientResultId)
        {
            return Update(data =>
            {
                data.Entries.RemoveAll(entry =>
                    entry != null && entry.OwnerPlayerId == ownerPlayerId
                    && entry.Request != null && entry.Request.ClientResultId == clientResultId);
            });
        }

        public static bool MarkDefinitiveFailure(Guid ownerPlayerId, Guid clientResultId)
        {
            return Update(data =>
            {
                PendingMatchResult entry = data.Entries.Find(value =>
                    value != null && value.OwnerPlayerId == ownerPlayerId
                    && value.Request != null && value.Request.ClientResultId == clientResultId);
                if (entry != null)
                {
                    entry.DefinitiveFailure = true;
                }
            });
        }

        /// <summary>Deletes the queue file (primary and <c>AtomicFile</c> backup copy). Test/teardown
        /// use only - production code never needs to wipe the whole queue.</summary>
        public static void Clear()
        {
            try
            {
                string path = GetPath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string backupPath = path + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not clear the pending match result queue: " + exception);
            }
        }

        private static bool Update(Action<PendingMatchResultData> update)
        {
            try
            {
                PendingMatchResultData data = Load();
                update(data);
                return Save(data);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not update the pending match result queue: " + exception);
                return false;
            }
        }

        private static PendingMatchResultData Load()
        {
            string path = GetPath();
            if (!AtomicFile.TryReadAllText(path, IsValid, out string json))
            {
                return new PendingMatchResultData();
            }

            if (!BackendV2Json.TryDeserialize(json, out PendingMatchResultData data) || data == null)
            {
                return new PendingMatchResultData();
            }

            data.Normalize();
            return data;
        }

        private static bool Save(PendingMatchResultData data)
        {
            try
            {
                data.Normalize();
                AtomicFile.WriteAllText(GetPath(), BackendV2Json.Serialize(data));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not save the pending match result queue: " + exception);
                return false;
            }
        }

        private static bool IsValid(string json)
        {
            return BackendV2Json.TryDeserialize(json, out PendingMatchResultData data) && data != null;
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }

    public sealed class PendingMatchResultData
    {
        public List<PendingMatchResult> Entries = new List<PendingMatchResult>();

        public void Normalize()
        {
            Entries ??= new List<PendingMatchResult>();
        }
    }
}
