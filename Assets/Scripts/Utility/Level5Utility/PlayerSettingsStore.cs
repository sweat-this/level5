using System;
using System.IO;
using Level5.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Version 1 requires all three finite, normalized gains. Invalid documents fall back
/// as a unit, first to the AtomicFile backup and then to full gain. Loading never writes.</summary>
public sealed class PlayerSettingsStore
{
    public const string FileName = "player-settings.json";
    private readonly string path;

    public PlayerSettingsStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", nameof(path));
        this.path = path;
    }

    public PlayerSettings Load()
    {
        return AtomicFile.TryReadAllText(path, IsValid, out string json) && TryParse(json, out PlayerSettings settings)
            ? settings : PlayerSettings.Defaults;
    }

    public bool TrySave(PlayerSettings settings, out string error)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        try
        {
            // Do not rotate a corrupt primary over the only valid backup after recovery.
            if (File.Exists(path) && !IsValid(File.ReadAllText(path)))
                File.Delete(path);

            JObject document = new JObject
            {
                ["version"] = 1,
                ["master"] = settings.Master,
                ["music"] = settings.Music,
                ["sfx"] = settings.Sfx
            };
            AtomicFile.WriteAllText(path, document.ToString(Formatting.None));
            error = null;
            return true;
        }
        catch (IOException exception) { error = exception.Message; }
        catch (UnauthorizedAccessException exception) { error = exception.Message; }
        return false;
    }

    private static bool IsValid(string json) => TryParse(json, out _);

    private static bool TryParse(string json, out PlayerSettings settings)
    {
        settings = null;
        try
        {
            JObject document = JObject.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (document["version"]?.Type != JTokenType.Integer || (double)document["version"] != 1d
                || !TryGain(document["master"], out float master)
                || !TryGain(document["music"], out float music)
                || !TryGain(document["sfx"], out float sfx)) return false;
            settings = new PlayerSettings(master, music, sfx);
            return true;
        }
        catch (JsonException) { return false; }
        catch (OverflowException) { return false; }
    }

    private static bool TryGain(JToken token, out float gain)
    {
        gain = 1f;
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false;
        double value = (double)token;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d) return false;
        gain = (float)value;
        return true;
    }
}
