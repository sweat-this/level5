using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Level5.BackendV2
{
    public enum PendingRemoteAttemptResultEnqueueResult
    {
        /// <summary>A new entry was durably persisted.</summary>
        Added,

        /// <summary>The exact same (owner, attemptId, context, metrics) was already persisted - a
        /// no-op, not an error.</summary>
        AlreadyQueued,

        /// <summary>A different payload is already persisted under this (owner, attemptId) key. This
        /// should never happen locally; the existing entry is left untouched and this is surfaced as
        /// a local integrity error rather than silently choosing one payload.</summary>
        ConflictingPayload,

        /// <summary>The request was invalid, or the file could not be read/written.</summary>
        Failed
    }

    /// <summary>
    /// Durable local store for a completed remote-correspondence-attempt result, persisted before its
    /// first <c>CompleteAttempt</c> submission so an application termination between that persist and
    /// a definitive server response can never lose it. Keyed by <c>(OwnerPlayerId, AttemptId)</c>,
    /// where <c>OwnerPlayerId</c> is always the server-issued <see cref="RemoteAttemptContext.PlayerId"/>
    /// - never local/V1 identity or whichever player happens to be logged in later.
    ///
    /// Deliberately its own file (<c>backendv2_pending_remote_attempt_results.json</c>), never
    /// <c>backendv2_pending_match_results.json</c>: a correspondence attempt result is not a general
    /// match result (see <c>RemoteAttemptResultSubmitter</c>'s own doc comment on why they must not
    /// share a delivery pipeline), and the two must be free to evolve independently. Uses the exact
    /// same proven primitives <see cref="PendingMatchResultStore"/> already relies on -
    /// <see cref="BackendV2Json"/> (Newtonsoft) for a <see cref="Dictionary{TKey,TValue}"/>-shaped
    /// payload <c>JsonUtility</c> cannot represent, and <see cref="AtomicFile"/> for the same
    /// crash-safe write/primary-backup-recovery read behavior every other Backend V2 local store uses.
    /// </summary>
    public static class PendingRemoteAttemptResultStore
    {
        private const string FileName = "backendv2_pending_remote_attempt_results.json";

        public static PendingRemoteAttemptResultEnqueueResult Enqueue(
            Guid ownerPlayerId, RemoteAttemptContext context, IReadOnlyDictionary<string, double> metrics)
        {
            if (ownerPlayerId == Guid.Empty || context == null || context.AttemptId == Guid.Empty)
            {
                return PendingRemoteAttemptResultEnqueueResult.Failed;
            }

            PendingRemoteAttemptResultEntry candidate =
                PendingRemoteAttemptResultEntry.From(ownerPlayerId, context, metrics);

            PendingRemoteAttemptResultEnqueueResult outcome = PendingRemoteAttemptResultEnqueueResult.Failed;
            bool saved = Update(data =>
            {
                PendingRemoteAttemptResultEntry existing = data.Entries.Find(entry =>
                    entry != null && entry.OwnerPlayerId == ownerPlayerId
                    && entry.Context != null && entry.Context.AttemptId == context.AttemptId);

                if (existing == null)
                {
                    data.Entries.Add(candidate);
                    outcome = PendingRemoteAttemptResultEnqueueResult.Added;
                    return;
                }

                if (existing.IsEquivalentTo(candidate))
                {
                    outcome = PendingRemoteAttemptResultEnqueueResult.AlreadyQueued;
                    return;
                }

                Debug.LogError(
                    "PendingRemoteAttemptResultStore refused to enqueue a remote attempt result: attemptId "
                    + context.AttemptId + " is already persisted for player " + ownerPlayerId
                    + " with a different payload. The existing persisted entry was left untouched.");
                outcome = PendingRemoteAttemptResultEnqueueResult.ConflictingPayload;
            });

            return saved ? outcome : PendingRemoteAttemptResultEnqueueResult.Failed;
        }

        /// <summary>The pending entry owned by <paramref name="ownerPlayerId"/>, if any - fresh from
        /// disk on every call, so a caller never needs its own cache-invalidation logic across a
        /// Backend V2 account switch. At most one is expected in practice (<c>RemoteAttemptLauncher</c>
        /// refuses to start a new attempt while one is pending), but this does not itself enforce
        /// that; it returns the first match.</summary>
        public static bool TryGetForOwner(Guid ownerPlayerId, out PendingRemoteAttemptResultEntry entry)
        {
            PendingRemoteAttemptResultData data = Load();
            entry = data.Entries.Find(value => value != null && value.OwnerPlayerId == ownerPlayerId);
            return entry != null;
        }

        public static bool Remove(Guid ownerPlayerId, Guid attemptId)
        {
            return Update(data =>
            {
                data.Entries.RemoveAll(entry =>
                    entry != null && entry.OwnerPlayerId == ownerPlayerId
                    && entry.Context != null && entry.Context.AttemptId == attemptId);
            });
        }

        /// <summary>Deletes the store file (primary and <c>AtomicFile</c> backup copy). Test/teardown
        /// use only - production code never needs to wipe every player's pending results.</summary>
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
                Debug.LogError("Could not clear the pending remote attempt result store: " + exception);
            }
        }

        private static bool Update(Action<PendingRemoteAttemptResultData> update)
        {
            try
            {
                PendingRemoteAttemptResultData data = Load();
                update(data);
                if (Save(data))
                {
                    return true;
                }

                // A mutation here (in particular the very first Enqueue for an attempt, right after a
                // match ends) is a one-shot opportunity: once TrySubmit has moved on, nothing calls
                // back in to retry the persist itself (RemoteAttemptResultSubmitter refuses to submit
                // at all when this fails - see its own doc comment), so a write that silently fails
                // here would permanently and silently drop the chance to durably record this attempt's
                // result before the first network submission. One immediate retry of the write only
                // (not update(data) again, which already mutated data once) is enough to ride out a
                // one-shot transient local I/O failure without looping indefinitely.
                return Save(data);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not update the pending remote attempt result store: " + exception);
                return false;
            }
        }

        private static PendingRemoteAttemptResultData Load()
        {
            string path = GetPath();
            if (!AtomicFile.TryReadAllText(path, IsValid, out string json))
            {
                return new PendingRemoteAttemptResultData();
            }

            if (!BackendV2Json.TryDeserialize(json, out PendingRemoteAttemptResultData data) || data == null)
            {
                return new PendingRemoteAttemptResultData();
            }

            data.Normalize();
            return data;
        }

        private static bool Save(PendingRemoteAttemptResultData data)
        {
            try
            {
                data.Normalize();
                AtomicFile.WriteAllText(GetPath(), BackendV2Json.Serialize(data));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not save the pending remote attempt result store: " + exception);
                return false;
            }
        }

        private static bool IsValid(string json)
        {
            return BackendV2Json.TryDeserialize(json, out PendingRemoteAttemptResultData data) && data != null;
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }

    public sealed class PendingRemoteAttemptResultData
    {
        public List<PendingRemoteAttemptResultEntry> Entries = new List<PendingRemoteAttemptResultEntry>();

        public void Normalize()
        {
            Entries ??= new List<PendingRemoteAttemptResultEntry>();
        }
    }
}
