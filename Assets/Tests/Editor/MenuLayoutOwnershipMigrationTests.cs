using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-090: <c>MenuLayoutOwnershipMigration.Classify</c> is the sole authority behind
/// <c>CollectForbiddenChildLayoutOverrides</c> (and therefore the
/// <c>PrefabDrivenMenuScreensDoNotOverridePrefabOwnedChildLayout</c> regression test), but its own
/// correctness had previously only been exercised indirectly, by running it against the four real
/// menu scenes once. These tests drive it directly against synthetic <see cref="PropertyModification"/>
/// data so a future edit to the classification rules gets caught here instead of only surfacing as a
/// silent misclassification in production scene data.
/// </summary>
public class MenuLayoutOwnershipMigrationTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject gameObject in spawned)
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        spawned.Clear();
    }

    private GameObject SpawnChildRectTransform(GameObject parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        spawned.Add(gameObject);
        gameObject.transform.SetParent(parent.transform, false);
        return gameObject;
    }

    private static PropertyModification MakeModification(Object target, string propertyPath, string value)
    {
        return new PropertyModification
        {
            target = target,
            propertyPath = propertyPath,
            value = value,
            objectReference = null,
        };
    }

    [Test]
    public void ChildAnchoredPositionMatchingThePrefabIsRedundant()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        GameObject child = SpawnChildRectTransform(root, "child");
        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 0f);

        PropertyModification modification = MakeModification(child.GetComponent<RectTransform>(), "m_AnchoredPosition.x", "10");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.ChildLayout));
        Assert.That(result.Redundant, Is.True);
        Assert.That(result.HierarchyPath, Is.EqualTo("root/child"));
    }

    [Test]
    public void ChildAnchoredPositionDifferingFromThePrefabIsDivergent()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        GameObject child = SpawnChildRectTransform(root, "child");
        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(450f, -690f);

        PropertyModification modification = MakeModification(child.GetComponent<RectTransform>(), "m_AnchoredPosition.x", "0");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.ChildLayout));
        Assert.That(result.Redundant, Is.False);
    }

    [Test]
    public void AnchoredPositionOnThePrefabRootItselfIsRootCompositionNotChildLayout()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        root.GetComponent<RectTransform>().anchoredPosition = new Vector2(293.5f, 182.5f);

        // Mirrors the AUD-090 task's own example: a root override numerically equal to the prefab's
        // value must still be RootComposition, never silently folded into (and auto-reverted as)
        // ChildLayout just because the value happens to match.
        PropertyModification modification =
            MakeModification(root.GetComponent<RectTransform>(), "m_AnchoredPosition.x", "293.5");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.RootComposition));
    }

    [Test]
    public void IsActiveOnAChildIsSemantic()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        GameObject child = SpawnChildRectTransform(root, "child");

        PropertyModification modification = MakeModification(child, "m_IsActive", "0");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.Semantic));
    }

    [Test]
    public void TextOnAChildTextComponentIsSemantic()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        GameObject child = SpawnChildRectTransform(root, "child");
        Text text = child.AddComponent<Text>();

        PropertyModification modification = MakeModification(text, "m_Text", "hello");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.Semantic));
    }

    /// <summary>
    /// Regression coverage for the classification bug found in review: m_LocalPosition/m_LocalRotation
    /// /m_LocalScale/m_RootOrder were previously bucketed as RootComposition (and therefore silently
    /// exempted from CollectForbiddenChildLayoutOverrides) for ANY object, not just the prefab's own
    /// root - so a non-root Transform override on one of these properties would have gone undetected.
    /// It must classify as Unknown instead, which Report() surfaces and the forbidden-overrides
    /// contract still ignores (deliberately - it is not a recognized child-layout property) but at
    /// least visibly, not silently, as a false RootComposition would.
    /// </summary>
    [Test]
    public void LocalPositionOnANonRootChildIsUnknownNotRootComposition()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);
        GameObject child = SpawnChildRectTransform(root, "child");

        PropertyModification modification = MakeModification(child.GetComponent<RectTransform>(), "m_LocalPosition.x", "5");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.Unknown));
    }

    [Test]
    public void RootOrderOnTheActualRootIsStillRootComposition()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);

        PropertyModification modification = MakeModification(root, "m_RootOrder", "2");
        MenuLayoutOwnershipMigration.Classified result =
            MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>());

        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.RootComposition));
    }

    [Test]
    public void UnresolvableNullTargetIsUnknownAndDoesNotThrow()
    {
        GameObject root = new GameObject("root", typeof(RectTransform));
        spawned.Add(root);

        PropertyModification modification = MakeModification(null, "m_AnchorMax.x", "0");

        MenuLayoutOwnershipMigration.Classified result = default;
        Assert.DoesNotThrow(() =>
            result = MenuLayoutOwnershipMigration.Classify(modification, root, new Dictionary<Object, SerializedObject>()));
        Assert.That(result.Category, Is.EqualTo(MenuLayoutOwnershipMigration.Category.Unknown));
        Assert.That(result.HierarchyPath, Is.EqualTo("<unresolved target>"));
    }

    [Test]
    public void MatchesPrefixRequiresBothSceneAndHierarchyPrefixToLineUp()
    {
        MenuLayoutOwnershipMigration.HierarchyPrefixTarget[] entries =
        {
            new MenuLayoutOwnershipMigration.HierarchyPrefixTarget(
                "Assets/Scenes/level_00_options.unity", "OptionManager/panel", expectedMatchCount: 1),
        };

        Assert.That(
            MenuLayoutOwnershipMigration.MatchesPrefix(
                "Assets/Scenes/level_00_options.unity", "OptionManager/panel", entries),
            Is.True,
            "an exact hierarchy path match should match");
        Assert.That(
            MenuLayoutOwnershipMigration.MatchesPrefix(
                "Assets/Scenes/level_00_options.unity", "OptionManager/panel/child", entries),
            Is.True,
            "a descendant of the prefix should match");
        Assert.That(
            MenuLayoutOwnershipMigration.MatchesPrefix(
                "Assets/Scenes/level_00_options.unity", "OptionManager/panelSibling", entries),
            Is.False,
            "a sibling whose name merely starts with the same characters must not match");
        Assert.That(
            MenuLayoutOwnershipMigration.MatchesPrefix(
                "Assets/Scenes/level_00_credits.unity", "OptionManager/panel", entries),
            Is.False,
            "a matching hierarchy path in the wrong scene must not match");
    }
}
