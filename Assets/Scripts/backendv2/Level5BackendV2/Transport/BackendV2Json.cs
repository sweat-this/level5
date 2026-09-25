using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Level5.BackendV2
{
    /// <summary>
    /// JSON (de)serialization for Backend V2 wire DTOs.
    ///
    /// Outgoing bodies are written with an explicit camelCase resolver so they match Backend V2's
    /// System.Text.Json default wire format exactly (its DTOs are plain C# records with no
    /// [JsonPropertyName] overrides) rather than relying on ASP.NET's case-insensitive model
    /// binding to paper over PascalCase. Incoming bodies rely on Newtonsoft's own case-insensitive
    /// property matching, which needs no configuration.
    ///
    /// <see cref="CamelCasePropertyNamesContractResolver"/>'s default <c>NamingStrategy</c> also
    /// camelCases <c>Dictionary&lt;string, TValue&gt;</c> <em>keys</em>, not just declared property
    /// names - correct for nothing this client actually sends: every dictionary-shaped payload
    /// (<c>CompleteAttemptDto.Metrics</c>, <c>SubmitMatchResultDto.Metrics</c>, and the local
    /// persistence stores that round-trip the same metrics dictionaries to disk) is keyed by a
    /// <see cref="AttemptMetric"/>/<see cref="MatchResultMetric"/> name, which Backend V2's own
    /// <c>ResultMetric</c> matches by exact member name (PascalCase) - see those enums' own doc
    /// comments. Left at the default, a metric named <c>"Score"</c> would silently become
    /// <c>"score"</c> on the wire and be dropped or misread server-side, and would fail to round-trip
    /// back out of local disk persistence at all (a different, unrelated dictionary key on read).
    /// <c>ProcessDictionaryKeys = false</c> keeps ordinary declared properties camelCased while
    /// leaving every dictionary key exactly as given.
    /// </summary>
    public static class BackendV2Json
    {
        private static readonly JsonSerializerSettings SerializeSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = { ProcessDictionaryKeys = false }
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, SerializeSettings);
        }

        public static bool TryDeserialize<T>(string json, out T value)
        {
            if (string.IsNullOrEmpty(json))
            {
                value = default;
                return false;
            }

            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
                return true;
            }
            catch (JsonException)
            {
                value = default;
                return false;
            }
        }
    }
}
