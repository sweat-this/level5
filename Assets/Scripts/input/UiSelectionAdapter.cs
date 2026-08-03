using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

            if (inputSystemUIInputModule.actionsAsset == null)
            {
                inputSystemUIInputModule.AssignDefaultActions();
            }

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
}
