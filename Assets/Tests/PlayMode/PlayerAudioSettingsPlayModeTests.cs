#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Settings = Level5.Core.PlayerSettings;

public class PlayerAudioSettingsPlayModeTests
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly Dictionary<FieldInfo, object> runtime = new Dictionary<FieldInfo, object>();
    private readonly Dictionary<FieldInfo, object> options = new Dictionary<FieldInfo, object>();
    private readonly List<Object> created = new List<Object>();
    private readonly Dictionary<string, float> gains = new Dictionary<string, float>();
    private string directory;
    private string path;
    private AudioMixer mixer;
    private InputTestFixture input;
    private Gamepad pad;
    private Keyboard keyboard;
    private LocalProfileTestDatabase database;
    private CampaignBoundaryPersistenceIsolation persistence;
    private object previousPlayerData;
    private Scene runnerScene;
    private bool recording;
    private int previousCaptureRate;
    private float previousListenerVolume;
    private bool previousListenerPause;
#if UNITY_EDITOR
    private bool previousEditorMute;
#endif

    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static FieldInfo RuntimeField(string name) => typeof(PlayerSettingsRuntime).GetField(name, Static);

    [SetUp]
    public void SetUp()
    {
        runnerScene = SceneManager.GetActiveScene();
        previousCaptureRate = Time.captureFramerate;
        recording = false;
        previousListenerVolume = AudioListener.volume;
        previousListenerPause = AudioListener.pause;
#if UNITY_EDITOR
        previousEditorMute = UnityEditor.EditorUtility.audioMasterMute;
#endif
        created.Clear(); runtime.Clear(); options.Clear(); gains.Clear();
        database = null; persistence = null;
        directory = Path.Combine(Path.GetTempPath(), "level5-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, PlayerSettingsStore.FileName);
        foreach (string name in new[] { "store", "current", "mixer", "dirty" })
        {
            FieldInfo field = RuntimeField(name);
            runtime[field] = field.GetValue(null);
        }
        RuntimeField("store").SetValue(null, new PlayerSettingsStore(path));
        RuntimeField("current").SetValue(null, Settings.Defaults);
        RuntimeField("dirty").SetValue(null, false);
        mixer = Resources.Load<AudioMixer>(PlayerSettingsRuntime.MixerResourcePath);
        foreach (string parameter in new[] { "MasterGain", "MusicGain", "SfxGain" })
        {
            Assert.That(mixer.GetFloat(parameter, out float gain), Is.True);
            gains[parameter] = gain;
        }
        RuntimeField("mixer").SetValue(null, mixer);
        foreach (FieldInfo field in Production("GameOptions").GetFields(BindingFlags.Public | BindingFlags.Static))
            if (!field.IsLiteral && !field.IsInitOnly) options[field] = field.GetValue(null);
        previousPlayerData = Production("PlayerData").GetField("instance").GetValue(null);
        typeof(PlayerControlsProvider).GetMethod("ResetState", Static).Invoke(null, null);
        input = new InputTestFixture(); input.Setup();
        pad = InputSystem.AddDevice<Gamepad>();
        keyboard = InputSystem.AddDevice<Keyboard>();
        PlayerSettingsRuntime.ApplyCurrent();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (recording) AudioRenderer.Stop();
        Time.captureFramerate = previousCaptureRate;
        AudioListener.volume = previousListenerVolume;
        AudioListener.pause = previousListenerPause;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.audioMasterMute = previousEditorMute;
#endif
        foreach (Object item in created) if (item != null) Object.Destroy(item);
        ActiveMatch.Clear();
        // Additive menu/probe tests retain the runner scene. Unloading it aborts NUnit itself.
        if (runnerScene.IsValid() && runnerScene.isLoaded)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != runnerScene && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
        else yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("audio-settings-cleanup");
        if (database != null) database.Dispose();
        persistence?.Dispose();
        Production("PlayerData").GetField("instance").SetValue(null, previousPlayerData);
        foreach (var pair in options) pair.Key.SetValue(null, pair.Value);
        foreach (var pair in runtime) pair.Key.SetValue(null, pair.Value);
        foreach (var pair in gains) mixer.SetFloat(pair.Key, pair.Value);
        typeof(PlayerControlsProvider).GetMethod("ResetState", Static).Invoke(null, null);
        input.TearDown();
        Directory.Delete(directory, true);
        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator OptionsRestoresWithoutWritesAndNavigatesWithKeyboardAndGamepad()
    {
        PlayerSettingsStore initial = new PlayerSettingsStore(path);
        initial.TrySave(new Settings(0.8f, 0.6f, 0.4f), out _);
        // Simulated fresh process: discard the in-memory model/store, reload disk, run startup seam.
        RuntimeField("store").SetValue(null, new PlayerSettingsStore(path));
        RuntimeField("current").SetValue(null, new PlayerSettingsStore(path).Load());
        Assert.That(PlayerSettingsRuntime.ApplyCurrent(), Is.True);
        string saved = File.ReadAllText(path);
        DateTime written = File.GetLastWriteTimeUtc(path);
        yield return SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_options, LoadSceneMode.Additive);
        yield return null; yield return null;
        Scene scene = SceneManager.GetSceneByName(Constants.SCENE_NAME_level_00_options);
        MonoBehaviour manager = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(scene, "OptionsManager");
        Assert.That(manager.enabled, Is.True);
        Component ui = manager.GetComponent("OptionsUiObjects");
        Slider master = SliderOf(ui, "MasterSlider");
        Slider music = SliderOf(ui, "MusicSlider");
        Slider sfx = SliderOf(ui, "SfxSlider");
        Assert.That(master.value, Is.EqualTo(80));
        Assert.That(music.value, Is.EqualTo(60));
        Assert.That(sfx.value, Is.EqualTo(40));
        Canvas.ForceUpdateCanvases();
        Transform canvas = manager.transform.Find("Canvas");
        Transform footer = canvas.Find("menu_footer");
        Bounds footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, footer);
        foreach (Slider slider in new[] { master, music, sfx })
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, slider.transform);
            Assert.That(bounds.min.y, Is.GreaterThan(footerBounds.max.y), slider.name + " overlaps the footer");
        }
        Assert.That(File.ReadAllText(path), Is.EqualTo(saved));
        Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(written));
        AssertGain("MasterGain", 0.8f); AssertGain("MusicGain", 0.6f); AssertGain("SfxGain", 0.4f);

        foreach (Slider slider in new[] { master, music, sfx }) Assert.That(RuntimeListenerCount(slider), Is.EqualTo(1));
        for (int i = 0; i < 3; i++) { manager.enabled = false; manager.enabled = true; }
        foreach (Slider slider in new[] { master, music, sfx }) Assert.That(RuntimeListenerCount(slider), Is.EqualTo(1));
        Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(written));

        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("controls_keyboard"));
        yield return Tap(keyboard.downArrowKey);
        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("controls_keyboardMouse"));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("controls_gamepad"));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("controls_touch"));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(master.gameObject));
        yield return Tap(keyboard.leftArrowKey);
        Assert.That(master.value, Is.EqualTo(79));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(music.gameObject));
        yield return Tap(pad.dpad.left);
        Assert.That(music.value, Is.EqualTo(59));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(sfx.gameObject));
        yield return Tap(pad.dpad.left);
        Assert.That(sfx.value, Is.EqualTo(39));
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<Button>(), Is.Not.Null);
        Settings restored = new PlayerSettingsStore(path).Load();
        Assert.That(restored.Master, Is.EqualTo(0.79f));
        Assert.That(restored.Music, Is.EqualTo(0.59f));
        Assert.That(restored.Sfx, Is.EqualTo(0.39f));

        yield return SceneManager.UnloadSceneAsync(scene);
        yield return SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_options, LoadSceneMode.Additive);
        yield return null; yield return null;
        manager = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetSceneByName(Constants.SCENE_NAME_level_00_options), "OptionsManager");
        Assert.That(SliderOf(manager.GetComponent("OptionsUiObjects"), "MasterSlider").value, Is.EqualTo(79));
        AssertGain("MusicGain", 0.59f);
    }

    [UnityTest]
    public IEnumerator PlaylistAndEffectsRemainIndependentAcrossGainChangesAndPause()
    {
        GameObject owner = Object.Instantiate(Resources.Load<GameObject>("Prefabs/critical/SFX"));
        created.Add(owner);
        SFXBB sfx = owner.GetComponent<SFXBB>();
        AudioSource music = (AudioSource)typeof(SFXBB).GetField("musicSource", Instance).GetValue(sfx);
        AudioSource effects = (AudioSource)typeof(SFXBB).GetField("effectsSource", Instance).GetValue(sfx);
        yield return null;
        Assert.That(music.isPlaying, Is.True);
        AudioClip playlistClip = music.clip;
        AudioClip tone = Tone();
        sfx.playSFX(tone);
        yield return null;
        Assert.That(effects.isPlaying, Is.True);
        PlayerSettingsRuntime.UpdateAndSave(new Settings(0.5f, 0f, 0.25f));
        AssertGain("MasterGain", 0.5f); AssertGain("MusicGain", 0f); AssertGain("SfxGain", 0.25f);
        Assert.That(music.isPlaying && effects.isPlaying, Is.True);
        Assert.That(music.volume, Is.EqualTo(0.7f));
        Assert.That(effects.volume, Is.EqualTo(0.7f));

        GameObject pauseOwner = new GameObject("pause audio probe"); pauseOwner.SetActive(false); created.Add(pauseOwner);
        Pause pause = pauseOwner.AddComponent<Pause>();
        Time.timeScale = 0f;
        typeof(Pause).GetMethod("pauseAllAudio", Instance).Invoke(pause, null);
        int song = (int)typeof(SFXBB).GetField("currentSongIndex", Instance).GetValue(sfx);
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.That(music.isPlaying || effects.isPlaying, Is.False);
        Assert.That(music.clip, Is.SameAs(playlistClip));
        Assert.That((int)typeof(SFXBB).GetField("currentSongIndex", Instance).GetValue(sfx), Is.EqualTo(song));
        Time.timeScale = 1f;
        typeof(Pause).GetMethod("resumeAllAudio", Instance).Invoke(pause, null);
        yield return null;
        Assert.That(music.isPlaying && effects.isPlaying, Is.True);
        typeof(SFXBB).GetField("musicEnabled", Instance).SetValue(sfx, false);
        music.Stop(); sfx.playSFX(tone);
        yield return null;
        Assert.That(music.isPlaying, Is.False);
        Assert.That(effects.isPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator MixerMuteFloorAttenuatesActualAudioEngineOutput()
    {
#if UNITY_EDITOR
        Debug.Log("Player audio editor mute before probe: " + UnityEditor.EditorUtility.audioMasterMute);
        UnityEditor.EditorUtility.audioMasterMute = false;
#endif
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        GameObject listener = new GameObject("audio probe listener", typeof(AudioListener)); created.Add(listener);
        GameObject owner = new GameObject("audio probe source", typeof(AudioSource)); created.Add(owner);
        AudioSource source = owner.GetComponent<AudioSource>();
        source.clip = Tone(); source.loop = true; source.volume = 0.5f;
        // Batch mode may suspend hardware output. Record the actual mixer output offline instead.
        Time.captureFramerate = 60;
        recording = AudioRenderer.Start();
        Assert.That(recording, Is.True, "Could not acquire Unity audio renderer");
        source.Play();
        yield return null;
        float unrouted = OutputRms();
        if (unrouted <= 0.001f)
            Assert.Inconclusive("No unrouted engine output; mixer attenuation acceptance is BLOCKED in this environment.");
        source.outputAudioMixerGroup = mixer.FindMatchingGroups("Music").Single(g => g.name == "Music");
        yield return null;
        float baseline = OutputRms();
        Assert.That(baseline, Is.GreaterThan(0.001f), "No measurable audio engine output; mute cannot be certified");
        Assert.That(baseline / unrouted, Is.EqualTo(1f).Within(0.02f), "Full-gain routing preserves output");
        PlayerSettingsRuntime.UpdateAndSave(new Settings(0.5f, 0.5f, 1f));
        yield return null;
        Assert.That(OutputRms() / baseline, Is.EqualTo(0.25f).Within(0.02f), "Master and category gains multiply");
        PlayerSettingsRuntime.UpdateAndSave(new Settings(1f, 0f, 1f));
        yield return null;
        Assert.That(OutputRms(), Is.LessThan(baseline * 0.005f), "Music mute floor");
        source.outputAudioMixerGroup = mixer.FindMatchingGroups("SFX").Single(g => g.name == "SFX");
        yield return null;
        Assert.That(OutputRms(), Is.GreaterThan(baseline * 0.8f), "SFX unaffected by Music mute");
        PlayerSettingsRuntime.UpdateAndSave(new Settings(1f, 1f, 0f));
        yield return null;
        Assert.That(OutputRms(), Is.LessThan(baseline * 0.005f), "SFX mute floor");
        PlayerSettingsRuntime.UpdateAndSave(new Settings(0f, 1f, 1f));
        yield return null;
        Assert.That(OutputRms(), Is.LessThan(baseline * 0.005f), "Master mute floor");
        Assert.That(source.volume, Is.EqualTo(0.5f));
    }

    [UnityTest]
    public IEnumerator RealScrapyardSourcesAndSpawnedBallRetainRoutingAcrossOptionsTransition()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        persistence = new CampaignBoundaryPersistenceIsolation(Application.persistentDataPath);
        database = new LocalProfileTestDatabase(); yield return database.Open();
        Production("PlayerData").GetField("instance").SetValue(null, null);
        created.Add(Object.Instantiate(Resources.Load<GameObject>("Prefabs/critical/player_data")));
        yield return null;
        Component modeSource = Resources.Load<GameObject>("Prefabs/menu_start/mode_selected_objects/mode_selected_99")
            .GetComponent("StartScreenModeSelected");
        GameModeDefinition mode = (GameModeDefinition)Production("GameModeDefinitionFactory").GetMethod("Create")
            .Invoke(null, new object[] { modeSource }); created.Add(mode);
        Component levelSource = Resources.Load<GameObject>("Prefabs/menu_start/level_selected_objects/level_selected_01_scrapyard")
            .GetComponent("LevelSelected");
        LevelDefinition level = (LevelDefinition)Production("LevelDefinitionFactory").GetMethod("Create", new[] { Production("LevelSelected") })
            .Invoke(null, new object[] { levelSource }); created.Add(level);
        CharacterProfile shooter = Resources.Load<GameObject>("Prefabs/menu_start/default_shooter_profiles/player_selected_johnnyb")
            .GetComponent<CharacterProfile>();
        PlayerRoster roster = PlayerRoster.Build(new[] { PlayerRosterEntry.LocalHuman(new CharacterSelection(
            shooter.PlayerId, shooter.PlayerObjectName, shooter.PlayerDisplayName, shooter.IsShooter, shooter.IsFighter)) });
        MatchBuildResult built = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { level })).Build(new MatchRequest(mode.Id, 1, roster));
        Assert.That(built.Succeeded, Is.True, built.Validation.ToString());
        ActiveMatch.Begin(built.Configuration);
        Production("LegacyGameOptionsBridge").GetMethod("Apply").Invoke(null, new object[] { built.Configuration });
        PlayerSettingsRuntime.UpdateAndSave(new Settings(0.6f, 0.4f, 0.2f));
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(built.Configuration.SceneName, _ => { });
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        Assert.That(sources.Length, Is.GreaterThan(3));
        Dictionary<AudioSource, float> volumes = sources.ToDictionary(s => s, s => s.volume);
        foreach (AudioSource source in sources)
        {
            Assert.That(source.outputAudioMixerGroup, Is.Not.Null, source.name);
            Assert.That(source.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer), source.name);
        }
        Assert.That(Object.FindAnyObjectByType<BasketBall>(), Is.Not.Null, "Human ball spawned from routed prefab");
        PlayerSettingsRuntime.UpdateAndSave(new Settings(1f, 0f, 0.5f));
        foreach (var pair in volumes) Assert.That(pair.Key.volume, Is.EqualTo(pair.Value), pair.Key.name);
        yield return SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_options);
        yield return null; yield return null;
        AssertGain("MusicGain", 0f); AssertGain("SfxGain", 0.5f);
    }

    private static Slider SliderOf(Component ui, string property) => (Slider)ui.GetType().GetProperty(property).GetValue(ui);
    private static int RuntimeListenerCount(Slider slider)
    {
        object calls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls", Instance).GetValue(slider.onValueChanged);
        return ((ICollection)calls.GetType().GetField("m_RuntimeCalls", Instance).GetValue(calls)).Count;
    }
    private IEnumerator Tap(ButtonControl control)
    {
        input.Press(control); yield return null; yield return null;
        input.Release(control); yield return null; yield return null;
    }
    private void AssertGain(string parameter, float linear)
    {
        Assert.That(mixer.GetFloat(parameter, out float gain), Is.True);
        Assert.That(gain, Is.EqualTo(PlayerSettingsRuntime.LinearToDecibels(linear)).Within(0.001f));
    }
    private AudioClip Tone()
    {
        AudioClip clip = AudioClip.Create("audio-settings-test-tone", 48000, 1, 48000, false);
        float[] samples = new float[48000];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.2f * Mathf.Sin(2f * Mathf.PI * 440f * i / 48000);
        clip.SetData(samples, 0); created.Add(clip); return clip;
    }
    private static float OutputRms()
    {
        int count = AudioRenderer.GetSampleCountForCaptureFrame() * 2; // Project output is stereo.
        // Editor batch mode can report no elapsed hardware capture frame. Render an explicit
        // 1/60-second stereo block in that case; Render still runs the actual DSP graph.
        if (count == 0) count = 1600;
        using (NativeArray<float> samples = new NativeArray<float>(count, Allocator.Temp))
        {
            // Let the engine settle gain smoothing; measure the final rendered block.
            for (int i = 0; i < 30; i++) Assert.That(AudioRenderer.Render(samples), Is.True);
            double sum = 0d;
            foreach (float sample in samples) sum += sample * sample;
            float rms = (float)Math.Sqrt(sum / samples.Length);
            Debug.Log("Player audio rendered output RMS: " + rms);
            return rms;
        }
    }
}
#endif
