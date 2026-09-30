using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The framing arithmetic behind the shared two-human camera, without a scene or a MonoBehaviour.
///
/// The camera used in these is level (no pitch) so the expected numbers can be worked out by hand: a
/// target at depth d and lateral offset x fits once (|x| + padding) / tan(half horizontal FOV) is at
/// most d + extra.
/// </summary>
public class Level5SharedCameraFramingTests
{
    private const float VerticalFov = 50f;
    private const float Aspect = 16f / 9f;
    private const float Padding = 2f;
    private const float MaxExtra = 12f;

    private static readonly Vector3 CameraPosition = new Vector3(0f, 2f, -12f);

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        created.Clear();
    }

    private static float Extra(params Vector3[] positions)
    {
        return SharedCameraFraming.RequiredExtraDistance(
            positions, CameraPosition, Quaternion.identity, VerticalFov, Aspect, Padding, MaxExtra);
    }

    private static float TanHalfHorizontal()
    {
        return Mathf.Tan(VerticalFov * 0.5f * Mathf.Deg2Rad) * Aspect;
    }

    private GameObject Actor(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    // ==================== bounds / midpoint ====================

    [Test]
    public void TwoPositionsProduceTheirMidpointAndBounds()
    {
        List<Vector3> positions = new List<Vector3> { new Vector3(-4f, 0f, -3f), new Vector3(6f, 0f, -7f) };

        Assert.That(SharedCameraFraming.TryGetBounds(positions, out Bounds bounds), Is.True);

        Assert.That(bounds.center, Is.EqualTo(new Vector3(1f, 0f, -5f)));
        Assert.That(bounds.size, Is.EqualTo(new Vector3(10f, 0f, 4f)));
    }

    [Test]
    public void OnePositionIsAValidFramingOfItself()
    {
        List<Vector3> positions = new List<Vector3> { new Vector3(3f, 1f, -4f) };

        Assert.That(SharedCameraFraming.TryGetBounds(positions, out Bounds bounds), Is.True);

        Assert.That(bounds.center, Is.EqualTo(new Vector3(3f, 1f, -4f)));
        Assert.That(bounds.size, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void NoPositionsHaveNoBounds()
    {
        Assert.That(SharedCameraFraming.TryGetBounds(new List<Vector3>(), out _), Is.False);
        Assert.That(SharedCameraFraming.TryGetBounds(null, out _), Is.False);
    }

    // ==================== required distance ====================

    [Test]
    public void IncreasingSeparationIncreasesTheRequiredFraming()
    {
        // Two targets centred on the camera axis, 10 and then 20 and then 30 apart.
        float close = Extra(new Vector3(-5f, 0f, -2f), new Vector3(5f, 0f, -2f));
        float wider = Extra(new Vector3(-10f, 0f, -2f), new Vector3(10f, 0f, -2f));
        float widest = Extra(new Vector3(-15f, 0f, -2f), new Vector3(15f, 0f, -2f));

        Assert.That(wider, Is.GreaterThan(close));
        Assert.That(widest, Is.GreaterThan(wider));
    }

    [Test]
    public void TheRequiredDistanceMatchesTheFieldOfViewGeometry()
    {
        // depth 10 (z = -2 from a camera at z = -12), lateral 12: needs (12 + 2) / tanH deep in total.
        float expected = ((12f + Padding) / TanHalfHorizontal()) - 10f;

        Assert.That(Extra(new Vector3(-12f, 0f, -2f), new Vector3(12f, 0f, -2f)), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void FramingClampsAtTheMinimumWhenEveryoneAlreadyFits()
    {
        Assert.That(Extra(new Vector3(-1f, 0f, -2f), new Vector3(1f, 0f, -2f)), Is.EqualTo(0f));
        Assert.That(Extra(new Vector3(0f, 0f, -2f)), Is.EqualTo(0f));
    }

    [Test]
    public void FramingClampsAtTheMaximumForPathologicalSeparation()
    {
        Assert.That(Extra(new Vector3(-500f, 0f, -2f), new Vector3(500f, 0f, -2f)), Is.EqualTo(MaxExtra));
    }

    [Test]
    public void ANearerTargetNeedsMoreDistanceThanAFarOneAtTheSameOffset()
    {
        float far = Extra(new Vector3(9f, 0f, 4f));
        float near = Extra(new Vector3(9f, 0f, -10f));

        Assert.That(near, Is.GreaterThan(far), "the same sideways offset is a bigger slice of a narrower field close to the lens");
    }

    [Test]
    public void TheOrderOfTheTargetsDoesNotMatter()
    {
        Vector3 a = new Vector3(-8f, 0f, -4f);
        Vector3 b = new Vector3(11f, 0f, -1f);

        Assert.That(Extra(a, b), Is.EqualTo(Extra(b, a)));
    }

    [Test]
    public void DegenerateCameraOrFieldOfViewNeverProducesAZoom()
    {
        List<Vector3> far = new List<Vector3> { new Vector3(-50f, 0f, 0f), new Vector3(50f, 0f, 0f) };

        Assert.That(SharedCameraFraming.RequiredExtraDistance(far, CameraPosition, Quaternion.identity, VerticalFov, 0f, Padding, MaxExtra), Is.EqualTo(0f));
        Assert.That(SharedCameraFraming.RequiredExtraDistance(far, CameraPosition, Quaternion.identity, 0f, Aspect, Padding, MaxExtra), Is.EqualTo(0f));
        Assert.That(SharedCameraFraming.RequiredExtraDistance(far, CameraPosition, Quaternion.identity, VerticalFov, Aspect, Padding, 0f), Is.EqualTo(0f));
        Assert.That(SharedCameraFraming.RequiredExtraDistance(null, CameraPosition, Quaternion.identity, VerticalFov, Aspect, Padding, MaxExtra), Is.EqualTo(0f));
        Assert.That(SharedCameraFraming.RequiredExtraDistance(new List<Vector3>(), CameraPosition, Quaternion.identity, VerticalFov, Aspect, Padding, MaxExtra), Is.EqualTo(0f));
    }

    [Test]
    public void APitchedCameraStillMeasuresLateralOffsetAcrossItsOwnAxis()
    {
        // The authored gameplay camera is pitched down; a pitch must not turn a sideways offset into
        // depth. Pitch about X leaves lateral (x) offsets alone.
        Quaternion pitch = Quaternion.Euler(13.6f, 0f, 0f);
        Vector3 target = new Vector3(14f, 0f, -2f);

        float level = SharedCameraFraming.RequiredExtraDistance(
            new List<Vector3> { target }, CameraPosition, Quaternion.identity, VerticalFov, Aspect, Padding, 100f);
        float pitched = SharedCameraFraming.RequiredExtraDistance(
            new List<Vector3> { target }, CameraPosition, pitch, VerticalFov, Aspect, Padding, 100f);

        Assert.That(pitched, Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(pitched - level), Is.LessThan(3f), "a small pitch only changes the depth of a low target slightly");
    }

    // ==================== missing / destroyed targets ====================

    [Test]
    public void DestroyedDisabledAndMissingActorsAreIgnored()
    {
        GameObject alive = Actor("alive", new Vector3(2f, 0f, 0f));
        GameObject disabled = Actor("disabled", new Vector3(9f, 0f, 0f));
        disabled.SetActive(false);
        GameObject destroyed = Actor("destroyed", new Vector3(-9f, 0f, 0f));
        Object.DestroyImmediate(destroyed);

        List<Vector3> positions = new List<Vector3>();
        int count = SharedCameraFraming.CollectLivePositions(new[] { alive, disabled, destroyed, null }, positions);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(positions, Is.EqualTo(new[] { new Vector3(2f, 0f, 0f) }));
    }

    [Test]
    public void TwoLiveActorsAreBothCollectedAndNoneCollectsNothing()
    {
        GameObject first = Actor("first", new Vector3(-3f, 0f, 0f));
        GameObject second = Actor("second", new Vector3(5f, 0f, 0f));
        List<Vector3> positions = new List<Vector3>();

        Assert.That(SharedCameraFraming.CollectLivePositions(new[] { first, second }, positions), Is.EqualTo(2));

        Object.DestroyImmediate(second);
        Assert.That(SharedCameraFraming.CollectLivePositions(new[] { first, second }, positions), Is.EqualTo(1));

        Object.DestroyImmediate(first);
        Assert.That(SharedCameraFraming.CollectLivePositions(new[] { first, second }, positions), Is.EqualTo(0));
        Assert.That(SharedCameraFraming.CollectLivePositions(null, positions), Is.EqualTo(0));
        Assert.That(positions, Is.Empty);
    }
}
