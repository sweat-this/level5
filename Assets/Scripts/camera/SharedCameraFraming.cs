using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The arithmetic behind the shared two-human camera, kept free of any MonoBehaviour lifecycle so it
/// can be tested on its own.
///
/// <see cref="cameraUpdater"/> owns what the camera does; this only answers "where are the live
/// human actors" and "how far back does a perspective camera have to sit to keep them all in frame".
/// It knows nothing about rosters, modes or arenas, and it is deliberately not a camera framework:
/// two targets, perspective only, one axis of zoom.
///
/// The zoom axis is the camera's own optical axis. The authored gameplay camera is a perspective camera
/// pitched down at the play field, and dollying it back along that axis keeps whatever ground point sits
/// at the middle of the screen fixed, so the composition only shrinks instead of sliding.
/// </summary>
public static class SharedCameraFraming
{
    /// <summary>
    /// Replaces <paramref name="positions"/> with the position of every actor that is still usable:
    /// not destroyed and still active in the hierarchy. Destroyed or disabled actors are skipped
    /// silently, which is what lets the camera degrade from two humans to one to none without ever
    /// holding an invalid target. Returns how many positions were collected.
    /// </summary>
    public static int CollectLivePositions(IReadOnlyList<GameObject> actors, List<Vector3> positions)
    {
        positions.Clear();
        if (actors == null)
        {
            return 0;
        }

        for (int i = 0; i < actors.Count; i++)
        {
            GameObject actor = actors[i];
            // Unity's overloaded == treats a destroyed object as null.
            if (actor != null && actor.activeInHierarchy)
            {
                positions.Add(actor.transform.position);
            }
        }

        return positions.Count;
    }

    /// <summary>The axis-aligned bounds of the positions, or false when there are none.</summary>
    public static bool TryGetBounds(IReadOnlyList<Vector3> positions, out Bounds bounds)
    {
        bounds = default;
        if (positions == null || positions.Count == 0)
        {
            return false;
        }

        bounds = new Bounds(positions[0], Vector3.zero);
        for (int i = 1; i < positions.Count; i++)
        {
            bounds.Encapsulate(positions[i]);
        }

        return true;
    }

    /// <summary>
    /// How much farther back along its optical axis a camera at <paramref name="cameraPosition"/> must
    /// move for every position to sit inside the horizontal field of view with
    /// <paramref name="padding"/> world units to spare, clamped to
    /// [0, <paramref name="maxExtraDistance"/>].
    ///
    /// Each target is measured in camera space. Moving back along the axis leaves its lateral offset
    /// unchanged and adds to its depth, so it fits once
    /// <c>(|lateral| + padding) &lt;= (depth + extra) * tan(horizontal half-FOV)</c>. Solving that per
    /// target and taking the largest gives the answer, and it handles targets standing at different
    /// depths, where a plain "world separation" would not.
    ///
    /// Only the horizontal fit is solved: the play field is wide and shallow, and characters are short
    /// against the vertical field of view.
    /// </summary>
    public static float RequiredExtraDistance(
        IReadOnlyList<Vector3> positions,
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        float verticalFieldOfViewDegrees,
        float aspect,
        float padding,
        float maxExtraDistance)
    {
        if (positions == null
            || positions.Count == 0
            || verticalFieldOfViewDegrees <= 0f
            || verticalFieldOfViewDegrees >= 180f
            || aspect <= 0f
            || maxExtraDistance <= 0f)
        {
            return 0f;
        }

        float tanHalfHorizontal = Mathf.Tan(verticalFieldOfViewDegrees * 0.5f * Mathf.Deg2Rad) * aspect;
        Quaternion toCamera = Quaternion.Inverse(cameraRotation);
        float required = 0f;

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 local = toCamera * (positions[i] - cameraPosition);
            float extra = ((Mathf.Abs(local.x) + Mathf.Max(0f, padding)) / tanHalfHorizontal) - local.z;
            if (extra > required)
            {
                required = extra;
            }
        }

        return Mathf.Clamp(required, 0f, maxExtraDistance);
    }
}
