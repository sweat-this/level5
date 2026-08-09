using System.Collections.Generic;

namespace Level5.Core.Versus.Persistence
{
    /// <summary>
    /// A repository that keeps series in memory - as serialized JSON, not as objects.
    ///
    /// Storing the strings is the point. A repository that handed back the same instance it was
    /// given would let a test pass while the real, file-backed path was silently unable to round-trip
    /// the data. Every read here goes through exactly the serializer the file store uses, so
    /// "restore the series and carry on" is proven, not assumed.
    ///
    /// Used by the edit-mode tests, and by the dev console when a run should not leave anything on
    /// disk.
    /// </summary>
    public sealed class InMemoryVersusSeriesRepository : IVersusSeriesRepository
    {
        private readonly Dictionary<string, string> documents = new Dictionary<string, string>();
        private readonly List<string> order = new List<string>();

        /// <summary>Makes the next <see cref="Save"/> fail, so retry paths can be tested.</summary>
        public bool FailNextSave { get; set; }

        /// <summary>How many series are stored, archived ones included.</summary>
        public int Count => documents.Count;

        public bool Save(VersusSeries series)
        {
            if (series == null)
            {
                return false;
            }

            if (FailNextSave)
            {
                FailNextSave = false;
                return false;
            }

            string key = series.Id.Value;
            bool archived = documents.TryGetValue(key, out string existing) && IsArchived(existing);

            if (!documents.ContainsKey(key))
            {
                order.Add(key);
            }

            documents[key] = VersusSeriesSerializer.ToJson(series, archived);
            return true;
        }

        public VersusSeries Load(SeriesId id)
        {
            return documents.TryGetValue(id.Value, out string json)
                ? VersusSeriesSerializer.FromJson(json)
                : null;
        }

        public bool Exists(SeriesId id)
        {
            return documents.ContainsKey(id.Value);
        }

        public IReadOnlyList<SeriesSummary> ListSummaries()
        {
            List<SeriesSummary> summaries = new List<SeriesSummary>(order.Count);
            foreach (string key in order)
            {
                if (documents.TryGetValue(key, out string json))
                {
                    SeriesSummary summary = VersusSeriesSerializer.SummaryFromJson(json);
                    if (summary != null)
                    {
                        summaries.Add(summary);
                    }
                }
            }

            return summaries;
        }

        public bool Delete(SeriesId id)
        {
            order.Remove(id.Value);
            return documents.Remove(id.Value);
        }

        public bool Archive(SeriesId id)
        {
            if (!documents.TryGetValue(id.Value, out string json))
            {
                return false;
            }

            VersusSeriesDocument document = UnityEngine.JsonUtility.FromJson<VersusSeriesDocument>(json);
            if (document == null)
            {
                return false;
            }

            document.archived = true;
            documents[id.Value] = UnityEngine.JsonUtility.ToJson(document);
            return true;
        }

        /// <summary>The stored bytes for a series. Tests use this to prove nothing leaks into them.</summary>
        public string RawDocument(SeriesId id)
        {
            return documents.TryGetValue(id.Value, out string json) ? json : null;
        }

        public void Clear()
        {
            documents.Clear();
            order.Clear();
        }

        private static bool IsArchived(string json)
        {
            VersusSeriesDocument document = UnityEngine.JsonUtility.FromJson<VersusSeriesDocument>(json);
            return document != null && document.archived;
        }
    }
}
