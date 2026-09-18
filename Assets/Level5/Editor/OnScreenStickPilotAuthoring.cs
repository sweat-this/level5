using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

/// <summary>
/// AUD-012 Phase 5 Slice 81: additive Input System <see cref="OnScreenStick"/> pilot for mobile
/// movement (docs/player-input-architecture.md migration plan step 3, first half only - the legacy
/// joystick fallback removal is deferred to a later slice, after device playtesting).
///
/// Adds one new sibling object next to the existing "Floating Joystick" prefab instance inside
/// touch_joystick.prefab's Canvas, bound to <c>&lt;Gamepad&gt;/leftStick</c> - the same control path
/// <c>PlayerControls.inputactions</c>' <c>Player/movement</c> composite already binds, so no action
/// map, action, or binding changes. Does not touch <c>PlayerInputReader</c>, the legacy
/// <c>FloatingJoystick</c>, or the <c>touch_joystick</c> root/tag.
///
/// Anchored bottom-right (<see cref="PilotAnchoredPosition"/>) rather than bottom-left deliberately -
/// the legacy "Floating Joystick" instance's raycastable area covers roughly
/// x:[0,384], y:[0,258] in canvas units (960x645 sizeDelta * 0.4 scale, anchored at the bottom-left
/// corner). Both controls receive input through the same UGUI IPointerDownHandler/IDragHandler path
/// (neither uses raw Input.touches), so an overlapping rect would let whichever one renders on top
/// silently swallow the other's touches in the overlap band. Bottom-right keeps the two rects clear of
/// each other on any supported aspect ratio without touching the legacy joystick's layout.
///
/// Idempotent: re-running finds the existing pilot by component type and re-applies every property
/// this method sets (layout, sprite, static flags, control path) rather than only the control path -
/// so a re-run also repairs a pilot authored by an older version of this tool, matching
/// <see cref="MenuUiObjectsWiring"/>'s wiring methods, which likewise always re-apply their fields
/// through `AddOrGet` rather than only on first creation. Kept in the repository afterward as a record
/// of how the pilot was authored, matching <c>MenuSceneCleanup</c>.
/// </summary>
public static class OnScreenStickPilotAuthoring
{
    private const string PrefabPath = "Assets/Resources/Prefabs/critical/touch_joystick.prefab";
    private const string PilotName = "OnScreenStickMovement";
    private const string ControlPath = "<Gamepad>/leftStick";
    private const string BackgroundSpriteGuid = "3a74f678ee8f3bd49aacf3de1ae4bcaa";

    private static readonly Vector2 PilotAnchorMin = new Vector2(1f, 0f);
    private static readonly Vector2 PilotAnchorMax = new Vector2(1f, 0f);
    private static readonly Vector2 PilotSizeDelta = new Vector2(200f, 200f);
    private static readonly Vector2 PilotAnchoredPosition = new Vector2(-150f, 220f);

    [MenuItem("Level5/Author OnScreenStick Movement Pilot")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Transform canvas = root.transform.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("OnScreenStickPilotAuthoring: touch_joystick.prefab has no direct 'Canvas' child.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        OnScreenStick[] existing = root.GetComponentsInChildren<OnScreenStick>(true);
        if (existing.Length > 1)
        {
            Debug.LogError(
                "OnScreenStickPilotAuthoring: expected at most one OnScreenStick, found " + existing.Length);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GameObject pilot;
        if (existing.Length == 1)
        {
            pilot = existing[0].gameObject;
        }
        else
        {
            pilot = new GameObject(PilotName, typeof(RectTransform), typeof(Image));
            pilot.transform.SetParent(canvas, false);
            pilot.AddComponent<OnScreenStick>();
        }

        RectTransform rect = (RectTransform)pilot.transform;
        rect.anchorMin = PilotAnchorMin;
        rect.anchorMax = PilotAnchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = PilotSizeDelta;
        rect.anchoredPosition = PilotAnchoredPosition;

        Image image = pilot.GetComponent<Image>();
        Sprite sprite = LoadSprite(BackgroundSpriteGuid);
        if (sprite == null)
        {
            Debug.LogWarning(
                "OnScreenStickPilotAuthoring: could not resolve background sprite (guid "
                + BackgroundSpriteGuid + ") - the pilot will render without one.");
        }

        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0.6f);
        image.raycastTarget = true;

        // Matches every other object in this prefab, which all serialize every static flag - a plain
        // `new GameObject(...)` otherwise defaults to none, an inconsistency with no functional effect
        // on a UI Canvas child but worth matching for the rest of the prefab's convention.
        GameObjectUtility.SetStaticEditorFlags(pilot, (StaticEditorFlags)~0);

        OnScreenStick stick = pilot.GetComponent<OnScreenStick>();
        SerializedObject serializedStick = new SerializedObject(stick);
        SerializedProperty controlPathProperty = serializedStick.FindProperty("m_ControlPath");
        if (controlPathProperty == null)
        {
            Debug.LogError(
                "OnScreenStickPilotAuthoring: OnScreenStick has no serialized field 'm_ControlPath' - "
                + "the Input System package's internal layout may have changed.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        controlPathProperty.stringValue = ControlPath;
        serializedStick.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("OnScreenStickPilotAuthoring: run complete.");
    }

    private static Sprite LoadSprite(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
