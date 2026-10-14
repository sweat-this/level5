using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>Only explicit acknowledgement keys, bound to a Backend V2 player GUID.</summary>
    public sealed class CorrespondenceReadState
    {
        private readonly Guid player;
        private readonly string path;
        private HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);
        public string ErrorMessage { get; private set; }

        public CorrespondenceReadState(Guid player, string directory = null)
        {
            if (player == Guid.Empty) throw new ArgumentException("A player GUID is required.", nameof(player));
            this.player = player;
            path = Path.Combine(directory ?? Application.persistentDataPath, "backendv2-correspondence-read-" + player.ToString("N") + ".json");
            if (AtomicFile.TryReadAllText(path, IsValid, out string json))
                read = new HashSet<string>(JsonUtility.FromJson<Document>(json).keys, StringComparer.Ordinal);
        }

        public bool IsUnread(string key) => key != null && !read.Contains(key);

        public bool Acknowledge(string key)
        {
            if (!ValidKey(key)) return false;
            if (read.Contains(key)) return true;
            var updated = new HashSet<string>(read, StringComparer.Ordinal) { key };
            try
            {
                AtomicFile.WriteAllText(path, JsonUtility.ToJson(new Document
                { version = 1, playerId = player.ToString("N"), keys = updated.OrderBy(k => k, StringComparer.Ordinal).ToArray() }));
                read = updated;
                ErrorMessage = null;
                return true;
            }
            catch (IOException) { ErrorMessage = "could not save read acknowledgement; item remains unread"; }
            catch (UnauthorizedAccessException) { ErrorMessage = "could not save read acknowledgement; item remains unread"; }
            return false;
        }

        private bool IsValid(string json)
        {
            Document document = JsonUtility.FromJson<Document>(json);
            return document != null && document.version == 1 && document.playerId == player.ToString("N")
                && document.keys != null && document.keys.All(ValidKey);
        }

        private static bool ValidKey(string key)
        {
            if (key == null) return false;
            string[] parts = key.Split(':');
            if (parts.Length < 2 || !Guid.TryParseExact(parts[1], "N", out Guid id) || id == Guid.Empty) return false;
            return parts.Length == 2 && (parts[0] == "challenge" || parts[0] == "completed")
                || parts.Length == 3 && parts[0] == "turn" && int.TryParse(parts[2], out int game) && game > 0;
        }

        [Serializable]
        private sealed class Document
        {
            public int version;
            public string playerId;
            public string[] keys;
        }
    }
}
