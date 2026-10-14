using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class PlayerAudioCompositionTests
{
    [Test]
    public void MixerHasExactlyThreeExposedGainsAndMasterHierarchyAtFullGain()
    {
        AudioMixer mixer = Resources.Load<AudioMixer>(PlayerSettingsRuntime.MixerResourcePath);
        Assert.That(mixer, Is.Not.Null);
        SerializedObject serialized = new SerializedObject(mixer);
        SerializedProperty parameters = serialized.FindProperty("m_ExposedParameters");
        Assert.That(parameters.arraySize, Is.EqualTo(3));
        string[] names = Enumerable.Range(0, parameters.arraySize)
            .Select(i => parameters.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue).ToArray();
        Assert.That(names, Is.EquivalentTo(new[] { "MasterGain", "MusicGain", "SfxGain" }));
        Object master = serialized.FindProperty("m_MasterGroup").objectReferenceValue;
        SerializedProperty children = new SerializedObject(master).FindProperty("m_Children");
        Assert.That(children.arraySize, Is.EqualTo(2));
        Assert.That(Enumerable.Range(0, 2).Select(i => children.GetArrayElementAtIndex(i).objectReferenceValue.name),
            Is.EquivalentTo(new[] { "Music", "SFX" }));
        string yaml = File.ReadAllText(AssetDatabase.GetAssetPath(mixer));
        Assert.That(Regex.Matches(yaml, @"^    [a-f0-9]{32}: 0\r?$", RegexOptions.Multiline).Count, Is.EqualTo(3));
    }

    [TestCase(1f, 0f)]
    [TestCase(0.5f, -6.0206f)]
    [TestCase(0f, -80f)]
    [TestCase(0.000001f, -80f)]
    [TestCase(-1f, -80f)]
    [TestCase(2f, 0f)]
    [TestCase(float.NaN, 0f)]
    public void LinearGainConversionIsFiniteAndBounded(float gain, float expected)
        => Assert.That(PlayerSettingsRuntime.LinearToDecibels(gain), Is.EqualTo(expected).Within(0.001f));

    [Test]
    public void EveryInventoriedSourceHasItsClassifiedRouteAndUnchangedAuthoredGain()
    {
        string mixerGuid = AssetDatabase.AssetPathToGUID("Assets/Resources/Audio/PlayerAudio.mixer");
        int count = 0;
        foreach (string line in File.ReadLines("docs/player-audio-source-inventory.md"))
        {
            if (!line.StartsWith("| Assets/") || line.Split('|').Length != 8) continue;
            string[] fields = line.Split('|').Select(s => s.Trim()).ToArray();
            string source = File.ReadAllText(fields[1]);
            Match document = Regex.Match(source, @"--- !u!82 &" + fields[2] + @"\r?\n.*?(?=--- !u!|\z)", RegexOptions.Singleline);
            Assert.That(document.Success, Is.True, fields[1] + "#" + fields[2]);
            string group = fields[4] == "Music" ? "24300001" : "24300002";
            Assert.That(document.Value, Does.Contain("OutputAudioMixerGroup: {fileID: " + group + ", guid: " + mixerGuid));
            Assert.That(float.Parse(Regex.Match(document.Value, @"m_Volume: (.*)").Groups[1].Value, CultureInfo.InvariantCulture),
                Is.EqualTo(float.Parse(fields[5], CultureInfo.InvariantCulture)), fields[1]);
            count++;
        }
        Assert.That(count, Is.EqualTo(399));
    }

    [Test]
    public void OptionsPrefabReferencesAndSliderNavigationAreComplete()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/critical/OptionManager");
        OptionsManager manager = prefab.GetComponent<OptionsManager>();
        List<string> missing = new List<string>();
        Assert.That(manager.ValidateMenuUi(missing), Is.True, string.Join(", ", missing));
        OptionsUiObjects ui = prefab.GetComponent<OptionsUiObjects>();
        foreach (Slider slider in new[] { ui.MasterSlider, ui.MusicSlider, ui.SfxSlider })
        {
            Assert.That(slider.minValue, Is.Zero);
            Assert.That(slider.maxValue, Is.EqualTo(100f));
            Assert.That(slider.wholeNumbers, Is.True);
            for (Transform ancestor = slider.transform; ancestor != null; ancestor = ancestor.parent)
                Assert.That(ancestor.gameObject.activeSelf, Is.True, slider.name + " has an inactive parent");
            Assert.That(slider.targetGraphic, Is.Not.Null);
            Assert.That(slider.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(slider.navigation.selectOnUp, Is.Not.Null);
            Assert.That(slider.navigation.selectOnDown, Is.Not.Null);
        }
        Assert.That(ui.TouchButton.navigation.selectOnDown, Is.SameAs(ui.MasterSlider));
        Assert.That(ui.MasterSlider.navigation.selectOnDown, Is.SameAs(ui.MusicSlider));
        Assert.That(ui.MusicSlider.navigation.selectOnDown, Is.SameAs(ui.SfxSlider));
    }

    [Test]
    public void SfxPrefabSourcesAreExplicitDistinctAndPreserveRelativeGain()
    {
        foreach (string path in new[] { "Prefabs/critical/SFX", "Prefabs/SFX" })
        {
            SFXBB sfx = Resources.Load<GameObject>(path).GetComponent<SFXBB>();
            SerializedObject serialized = new SerializedObject(sfx);
            AudioSource music = (AudioSource)serialized.FindProperty("musicSource").objectReferenceValue;
            AudioSource effects = (AudioSource)serialized.FindProperty("effectsSource").objectReferenceValue;
            Assert.That(music, Is.Not.Null);
            Assert.That(effects, Is.Not.Null.And.Not.SameAs(music));
            Assert.That(music.outputAudioMixerGroup.name, Is.EqualTo("Music"));
            Assert.That(effects.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
            Assert.That(effects.volume, Is.EqualTo(music.volume));
            Assert.That(effects.pitch, Is.EqualTo(music.pitch));
            Assert.That(effects.spatialBlend, Is.EqualTo(music.spatialBlend));
            Assert.That(effects.playOnAwake, Is.False);
            Assert.That(effects.clip, Is.Null);
        }
    }
}
