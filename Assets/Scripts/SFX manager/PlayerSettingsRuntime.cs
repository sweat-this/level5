using System.IO;
using Level5.Core;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>One application preference authority and mixer application boundary. No scene sources
/// or individual source gains are changed. The persistent component only owns startup/save retries.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class PlayerSettingsRuntime : MonoBehaviour
{
    public const string MixerResourcePath = "Audio/PlayerAudio";
    public const string MasterParameter = "MasterGain";
    public const string MusicParameter = "MusicGain";
    public const string SfxParameter = "SfxGain";
    public const float MuteDecibels = -80f;

    private static PlayerSettingsStore store;
    private static PlayerSettings current;
    private static AudioMixer mixer;
    private static bool dirty;

    public static PlayerSettings Current { get { EnsureLoaded(); return current; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        store = null;
        current = null;
        mixer = null;
        dirty = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureLoaded();
        GameObject owner = new GameObject(nameof(PlayerSettingsRuntime));
        DontDestroyOnLoad(owner);
        owner.AddComponent<PlayerSettingsRuntime>();
    }

    private static void EnsureLoaded()
    {
        if (store != null) return;
        store = new PlayerSettingsStore(Path.Combine(Application.persistentDataPath, PlayerSettingsStore.FileName));
        current = store.Load();
    }

    // Unity documents SetFloat in Start or later, rather than Awake/OnEnable/BeforeSceneLoad.
    private void Start() => ApplyCurrent();

    public static float LinearToDecibels(float value)
    {
        value = PlayerSettings.Normalize(value);
        return value <= 0f ? MuteDecibels : Mathf.Clamp(20f * Mathf.Log10(value), MuteDecibels, 0f);
    }

    public static bool ApplyCurrent()
    {
        EnsureLoaded();
        if (mixer == null) mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        bool applied = mixer != null;
        if (mixer != null)
        {
            // Evaluate all parameters even if one is missing, then report the broken asset contract.
            applied &= mixer.SetFloat(MasterParameter, LinearToDecibels(current.Master));
            applied &= mixer.SetFloat(MusicParameter, LinearToDecibels(current.Music));
            applied &= mixer.SetFloat(SfxParameter, LinearToDecibels(current.Sfx));
        }
        if (!applied) Debug.LogError("Player audio mixer or exposed gain parameters are missing.");
        return applied;
    }

    /// <summary>Applies immediately and synchronously saves each actual percentage change.
    /// A failed save keeps the live value and retries on Options exit, focus loss, pause or quit.</summary>
    public static bool UpdateAndSave(PlayerSettings settings)
    {
        if (settings == null) throw new System.ArgumentNullException(nameof(settings));
        EnsureLoaded();
        if (current.Master == settings.Master && current.Music == settings.Music && current.Sfx == settings.Sfx)
            return Flush();
        current = settings;
        dirty = true;
        ApplyCurrent();
        return Flush();
    }

    public static bool Flush()
    {
        if (!dirty) return true;
        if (!store.TrySave(current, out string error))
        {
            Debug.LogWarning("Could not save player settings; changes are active but not durable: " + error);
            return false;
        }
        dirty = false;
        return true;
    }

    private void OnApplicationPause(bool paused) { if (paused) Flush(); }
    private void OnApplicationFocus(bool focused) { if (!focused) Flush(); }
    private void OnApplicationQuit() => Flush();
}
