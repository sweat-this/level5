using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Level5DocumentationExporterTests
{
    private const string TempRoot = "Assets/Tests/Editor/TempDocumentationExporter";

    [TearDown]
    public void TearDown()
    {
        if (AssetDatabase.IsValidFolder(TempRoot))
        {
            AssetDatabase.DeleteAsset(TempRoot);
        }
    }

    [Test]
    public void SchemaVersionReflectsStableIdentityContract()
    {
        Assert.That(Level5DocumentationExporter.SchemaVersion, Is.EqualTo(2));
    }

    [Test]
    public void HierarchyPathDisambiguatesDuplicateSiblingNamesAndEscapesDelimiters()
    {
        GameObject root = new GameObject("Root/Name");
        GameObject first = new GameObject("Child[Name]");
        GameObject second = new GameObject("Child[Name]");
        first.transform.SetParent(root.transform);
        second.transform.SetParent(root.transform);

        try
        {
            string firstPath = InvokePrivate<string>("GetHierarchyPath", first.transform);
            string secondPath = InvokePrivate<string>("GetHierarchyPath", second.transform);

            Assert.That(firstPath, Is.Not.EqualTo(secondPath));
            Assert.That(firstPath, Does.Contain("Root%2FName"));
            Assert.That(firstPath, Does.EndWith("Child%5BName%5D[0]"));
            Assert.That(secondPath, Does.EndWith("Child%5BName%5D[1]"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PrefabInternalReferencesPreserveChildAndDuplicateComponentIdentity()
    {
        EnsureTempRoot();
        const string prefabPath = TempRoot + "/identity.prefab";

        GameObject root = new GameObject("Root");
        GameObject child = new GameObject("Child");
        child.transform.SetParent(root.transform);
        child.AddComponent<BoxCollider>();
        child.AddComponent<BoxCollider>();

        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject prefabChild = prefab.transform.GetChild(0).gameObject;
        BoxCollider[] colliders = prefabChild.GetComponents<BoxCollider>();
        Assert.That(colliders.Length, Is.EqualTo(2));

        string rootReference = InvokePrivate<string>("ObjectReference", prefab);
        string childReference = InvokePrivate<string>("ObjectReference", prefabChild);
        string firstColliderReference = InvokePrivate<string>("ObjectReference", colliders[0]);
        string secondColliderReference = InvokePrivate<string>("ObjectReference", colliders[1]);

        Assert.That(rootReference, Is.EqualTo(prefabPath));
        Assert.That(
            childReference,
            Is.EqualTo("asset:" + prefabPath + "#Root[0]/Child[0]"));
        Assert.That(firstColliderReference, Does.EndWith("@UnityEngine.BoxCollider[0]"));
        Assert.That(secondColliderReference, Does.EndWith("@UnityEngine.BoxCollider[1]"));
        Assert.That(firstColliderReference, Is.Not.EqualTo(secondColliderReference));
    }

    [Test]
    public void OrdinaryPersistentAssetsKeepConciseAssetPathReferences()
    {
        EnsureTempRoot();
        const string assetPath = TempRoot + "/test.asset";
        DocumentationExporterTestAsset asset = ScriptableObject.CreateInstance<DocumentationExporterTestAsset>();
        AssetDatabase.CreateAsset(asset, assetPath);

        string reference = InvokePrivate<string>("ObjectReference", asset);

        Assert.That(reference, Is.EqualTo(assetPath));
    }

    private static void EnsureTempRoot()
    {
        if (!AssetDatabase.IsValidFolder(TempRoot))
        {
            AssetDatabase.CreateFolder("Assets/Tests/Editor", "TempDocumentationExporter");
        }
    }

    private static T InvokePrivate<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(Level5DocumentationExporter).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Missing exporter helper: " + methodName);
        return (T)method.Invoke(null, arguments);
    }
}

public sealed class DocumentationExporterTestAsset : ScriptableObject
{
}
