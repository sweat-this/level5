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
    /// </summary>
    public static class BackendV2Json
    {
        private static readonly JsonSerializerSettings SerializeSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
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
