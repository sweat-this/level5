using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The authored facts <c>level_00_local_versus</c> must keep for <see cref="LocalVersusScreenModel"/> and
/// the screen's input behavior to stay correct. These are properties of the scene, not of the model, so
/// nothing in the model tests notices when a scene edit or a regeneration
/// (<c>LocalVersusSceneBootstrap</c>) changes them; the rendered check in
/// <c>LocalVersusProcessCertificationPlayModeTests</c> is opt-in and needs a graphics device.
/// </summary>
public class LocalVersusSceneContractTests
{
    private const string ScenePath = "Assets/Scenes/level_00_local_versus.unity";

    /// <summary>The name fields' limit the widest-row measurement behind <see cref="LocalVersusScreenModel.ListLineBudget"/> assumed.</summary>
    private const int MeasuredNameLimit = 16;

    private readonly List<Scene> openedByThisTest = new List<Scene>();

    [TearDown]
    public void TearDown()
    {
        foreach (Scene scene in openedByThisTest)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        openedByThisTest.Clear();
    }

    private LocalVersusUiObjects OpenUi()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByThisTest.Add(scene);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LocalVersusUiObjects ui = root.GetComponentInChildren<LocalVersusUiObjects>(true);
            if (ui != null)
            {
                return ui;
            }
        }

        Assert.Fail("no LocalVersusUiObjects in " + ScenePath);
        return null;
    }

    [Test]
    public void TheSeriesListBoxStillHoldsTheLineBudget()
    {
        // ListLineBudget lines were measured to fit this box (28pt in 210px, one line per row). A smaller
        // box or a larger font silently brings back the cut-off list; a wider name limit invalidates the
        // widest-row measurement. Rerun the rendered layout check when this fails on purpose.
        LocalVersusUiObjects ui = OpenUi();
        TMP_Text list = ui.SeriesListText;
        LayoutElement box = list.GetComponent<LayoutElement>();
        Assert.That(box, Is.Not.Null, "the list box's height comes from its LayoutElement");

        float lineHeight = list.font.faceInfo.lineHeight * list.fontSize / list.font.faceInfo.pointSize;
        Assert.That(
            LocalVersusScreenModel.ListLineBudget * lineHeight,
            Is.LessThanOrEqualTo(box.preferredHeight),
            $"{LocalVersusScreenModel.ListLineBudget} lines of {list.fontSize}pt ({lineHeight:0.0}px each) no longer fit {box.preferredHeight}px");

        Assert.That(
            ui.Player1NameInputField.characterLimit,
            Is.LessThanOrEqualTo(MeasuredNameLimit).And.GreaterThan(0),
            "the widest-row measurement assumed names of at most " + MeasuredNameLimit + " characters");
        Assert.That(ui.Player2NameInputField.characterLimit, Is.EqualTo(ui.Player1NameInputField.characterLimit));
    }

    [Test]
    public void TheTallestPanelStatesStillFitTheReferenceCanvasAndClearBack()
    {
        // The simultaneous state adds Player 2's selector to the series panel, and the Mode selector
        // makes the create panel taller than it was. Both are measured in canvas units after a layout
        // rebuild (the authored panels hang from the top edge), so a future control or a larger font
        // cannot silently push Play or Create off the 1080 reference canvas or under the Back button.
        LocalVersusUiObjects ui = OpenUi();
        CanvasScaler scaler = ui.GetComponentInChildren<CanvasScaler>(true);
        Assert.That(scaler, Is.Not.Null);
        float canvasHeight = scaler.referenceResolution.y;

        ui.Character2Button.gameObject.SetActive(true);
        RectTransform seriesPanel = (RectTransform)ui.CharacterButton.transform.parent;
        RectTransform createPanel = (RectTransform)ui.RulesetButton.transform.parent;
        LayoutRebuilder.ForceRebuildLayoutImmediate(seriesPanel);
        LayoutRebuilder.ForceRebuildLayoutImmediate(createPanel);

        float seriesBottom = -seriesPanel.anchoredPosition.y + seriesPanel.rect.height;
        float createBottom = -createPanel.anchoredPosition.y + createPanel.rect.height;
        RectTransform back = (RectTransform)ui.BackButton.transform;
        float backTop = canvasHeight - back.anchoredPosition.y - back.rect.height;
        TestContext.WriteLine($"series panel bottom {seriesBottom}, create panel bottom {createBottom}, back top {backTop}, canvas {canvasHeight}");

        Assert.That(seriesBottom, Is.LessThanOrEqualTo(canvasHeight), "the simultaneous series panel runs off the canvas");
        Assert.That(createBottom, Is.LessThanOrEqualTo(backTop), "the create panel overlaps the Back button");
        Assert.That(ui.Character2Button.GetComponent<LayoutElement>().preferredHeight,
            Is.EqualTo(ui.CharacterButton.GetComponent<LayoutElement>().preferredHeight),
            "Player 2's selector is laid out like Player 1's");
    }

    [Test]
    public void PlayerTwosSelectorIsAuthoredHiddenUntilASimultaneousSeriesIsSelected()
    {
        LocalVersusUiObjects ui = OpenUi();
        Assert.That(ui.Character2Button.gameObject.activeSelf, Is.False);
        Assert.That(ui.ModeButton.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void SelectingANameFieldDoesNotStartEditingIt()
    {
        // An active field swallows the d-pad, the stick and Cancel, so a gamepad that moved onto one could
        // not move off it. Click, Enter and Submit still activate a field deliberately.
        LocalVersusUiObjects ui = OpenUi();
        Assert.That(ui.Player1NameInputField.shouldActivateOnSelect, Is.False);
        Assert.That(ui.Player2NameInputField.shouldActivateOnSelect, Is.False);
    }
}
