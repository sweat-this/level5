using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-111 section 10/11: <c>LoadManager.TryLoadFallbackData</c> is the product's actual
/// database-unavailable/default-data path (invoked explicitly by <c>LoadedData</c>'s
/// HasAllRequiredData()/retry flow, not by catching an exception - see LoadManager.cs and
/// LoadedData.cs). Its load*DataList methods only read this project's real Resources catalogs, so
/// this exercises it against the genuine project content rather than a mock.
///
/// Uses the same inactive-GameObject technique as <see cref="MenuActionWrapperExceptionTests"/>:
/// TryLoadFallbackData does not depend on anything LoadManager.Awake() sets up (it never touches
/// the database), so Awake never needs to run.
/// </summary>
public class LoadManagerFallbackDataTests
{
    [Test]
    public void TryLoadFallbackData_LoadsEveryRequiredDefaultCatalogFromResources()
    {
        GameObject go = new GameObject("LoadManager-FallbackDataTest");
        try
        {
            go.SetActive(false);
            LoadManager manager = go.AddComponent<LoadManager>();

            bool complete = manager.TryLoadFallbackData(out string error);

            Assert.IsTrue(
                complete,
                "TryLoadFallbackData should load every default catalog from this project's "
                    + "Resources folders when the database is unavailable: " + error);
            Assert.IsEmpty(error);
            Assert.IsNotEmpty(manager.PlayerSelectedData, "default player catalog");
            Assert.IsNotEmpty(manager.CpuPlayerSelectedData, "default CPU catalog");
            Assert.IsNotEmpty(manager.CheerleaderSelectedData, "default cheerleader catalog");
            Assert.IsNotEmpty(manager.LevelSelectedData, "level catalog");
            Assert.IsNotEmpty(manager.ModeSelectedData, "mode catalog");
            Assert.IsNotNull(manager.LevelCatalog, "level catalog should have been constructed");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
