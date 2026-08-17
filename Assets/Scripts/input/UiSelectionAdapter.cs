using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class UiSelectionAdapter
{
    public static GameObject CurrentSelected
    {
        get
        {
            return EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
        }
    }

    public static bool InputSystemUiActive
    {
        get
        {
            return EventSystem.current != null
                && EventSystem.current.currentInputModule is InputSystemUIInputModule;
        }
    }

    public static GameObject EnsureSelected(GameObject fallback)
    {
        if (EventSystem.current == null)
        {
            return null;
        }

        if (EventSystem.current.currentSelectedGameObject == null && fallback != null)
        {
            EventSystem.current.SetSelectedGameObject(fallback);
        }

        return EventSystem.current.currentSelectedGameObject;
    }

    public static bool TrySelect(GameObject target)
    {
        if (EventSystem.current == null || target == null)
        {
            return false;
        }

        EventSystem.current.SetSelectedGameObject(target);
        return EventSystem.current.currentSelectedGameObject == target;
    }

    public static bool TryInvokeSelectedButton(GameObject fallback)
    {
        GameObject selected = EnsureSelected(fallback);
        if (selected == null)
        {
            return false;
        }

        return TryInvokeButton(selected.GetComponent<Button>());
    }

    public static bool TryInvokeButton(Button button)
    {
        if (button == null || !button.IsActive() || !button.interactable)
        {
            return false;
        }

        button.onClick.Invoke();
        return true;
    }

    public static bool TryGetSelectedName(GameObject fallback, out string selectedName)
    {
        selectedName = null;
        GameObject selected = EnsureSelected(fallback);
        if (selected == null)
        {
            return false;
        }

        selectedName = selected.name;
        return true;
    }

    /// <summary>
    /// Registers a code-owned handler on a button, replacing any previous registration of the same
    /// action so repeated calls stay idempotent.
    ///
    /// This used to skip the registration entirely when the button already carried inspector-authored
    /// persistent listeners, while a duplicated `RegisterRequiredButtonCallback` in five different
    /// managers always registered (AUD-105). Whether a button's behaviour came from code or from the
    /// scene therefore depended on which helper the screen happened to use - and for the menu screens
    /// still authored in binary prefabs, the scene half is not reviewable. Code owns menu button
    /// behaviour; scenes author none. <see cref="HasPersistentListeners"/> remains so the validator
    /// can assert that.
    /// </summary>
    public static void RegisterButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    public static void UnregisterButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    public static bool HasPersistentListeners(Button button)
    {
        return button != null && button.onClick.GetPersistentEventCount() > 0;
    }

    /// <summary>
    /// Points the module at <c>PlayerControls.UINavigation</c>.
    ///
    /// This used to call <c>AssignDefaultActions()</c>, which builds Unity's stock
    /// <c>DefaultInputActions</c> asset (AUD-095). The project's own menu bindings were therefore
    /// not what drove standard UI navigation: WASD, <c>Gamepad/start</c> and every
    /// <c>HID::Sony PLAYSTATION(R)3 Controller</c> binding exist only in
    /// <c>PlayerControls.UINavigation</c>, so on any screen already migrated to Button.onClick they
    /// silently stopped working. It also allocated a fresh InputActionAsset on every scene load.
    /// </summary>
    private static InputActionReference navigateReference;
    private static InputActionReference submitReference;
    private static InputActionReference cancelReference;
    private static InputActionReference pointReference;
    private static InputActionReference clickReference;
    private static InputActionReference scrollReference;
    private static InputActionAsset boundAsset;

    public static bool EnsureInputSystemUiModule()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        StandaloneInputModule standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
        InputSystemUIInputModule inputSystemUIInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        try
        {
            if (inputSystemUIInputModule == null)
            {
                inputSystemUIInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            AssignMenuActions(inputSystemUIInputModule);

            inputSystemUIInputModule.enabled = true;
            if (standaloneInputModule != null)
            {
                standaloneInputModule.enabled = false;
            }

            return inputSystemUIInputModule.actionsAsset != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not configure InputSystemUIInputModule. Falling back to StandaloneInputModule. " + exception.Message);

            if (inputSystemUIInputModule != null)
            {
                inputSystemUIInputModule.enabled = false;
            }

            if (standaloneInputModule != null)
            {
                standaloneInputModule.enabled = true;
            }

            return false;
        }
    }

    /// <summary>
    /// Binds the module's move/submit/cancel/point/click/scroll to the UINavigation map.
    ///
    /// The references are cached against the asset they came from. InputActionReference.Create
    /// allocates a ScriptableObject, and this runs on every menu scene load.
    /// </summary>
    private static void AssignMenuActions(InputSystemUIInputModule module)
    {
        InputActionAsset asset = PlayerControlsProvider.Controls != null
            ? PlayerControlsProvider.Controls.asset
            : null;
        if (asset == null)
        {
            return;
        }

        if (boundAsset != asset)
        {
            navigateReference = CreateReference(asset, "UINavigation/Navigate");
            submitReference = CreateReference(asset, "UINavigation/Submit");
            cancelReference = CreateReference(asset, "UINavigation/Cancel");
            pointReference = CreateReference(asset, "UINavigation/Point");
            clickReference = CreateReference(asset, "UINavigation/Click");
            scrollReference = CreateReference(asset, "UINavigation/ScrollWheel");
            boundAsset = asset;
        }

        // assigning the asset resets the individual references, so it goes first
        module.actionsAsset = asset;
        module.move = navigateReference;
        module.submit = submitReference;
        module.cancel = cancelReference;
        module.point = pointReference;
        module.leftClick = clickReference;
        module.scrollWheel = scrollReference;
    }

    private static InputActionReference CreateReference(InputActionAsset asset, string actionPath)
    {
        InputAction action = asset.FindAction(actionPath, throwIfNotFound: false);
        if (action == null)
        {
            Debug.LogWarning("PlayerControls is missing the UI action '" + actionPath + "'.");
            return null;
        }

        return InputActionReference.Create(action);
    }
}
