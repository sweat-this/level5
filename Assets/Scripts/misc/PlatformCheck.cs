
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class PlatformCheck : MonoBehaviour
{
    [SerializeField] InputSystemUIInputModule inputSystemUIInputModule;
    [SerializeField] StandaloneInputModule standaloneInputModule;

    void Awake()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            inputSystemUIInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            EnsureInputSystemUiModule(eventSystem);

            if (standaloneInputModule == null && inputSystemUIInputModule == null)
            {
                Debug.LogWarning("PlatformCheck found an EventSystem without a StandaloneInputModule or InputSystemUIInputModule.");
            }
        }
        else
        {
            Debug.LogWarning("PlatformCheck could not find an EventSystem.");
        }

        // AUD-094: QualitySettings.asset authors a single quality level with vSyncCount: 0, and this
        // used to overwrite it with 1 on every platform - so the authored value was never the value
        // that ran. The runtime is the owner: it is the only place that can distinguish handheld
        // from desktop. QualitySettings.asset is now the fallback for anything that reads it before
        // this runs, not a competing source of truth.
#if UNITY_ANDROID || UNITY_IOS

        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        SetUiModuleState(useStandaloneInputModule: false);
#endif

#if UNITY_STANDALONE || UNITY_EDITOR
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;

        SetUiModuleState(useStandaloneInputModule: false);
#endif
    }

    private void SetUiModuleState(bool useStandaloneInputModule)
    {
        if (!CanUseInputSystemUiModule() && standaloneInputModule != null)
        {
            useStandaloneInputModule = true;
        }

        if (inputSystemUIInputModule != null)
        {
            inputSystemUIInputModule.enabled = !useStandaloneInputModule;
        }

        if (standaloneInputModule != null)
        {
            standaloneInputModule.enabled = useStandaloneInputModule;
        }
    }

    private bool CanUseInputSystemUiModule()
    {
        return inputSystemUIInputModule != null
            && inputSystemUIInputModule.actionsAsset != null;
    }

    private void EnsureInputSystemUiModule(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
        inputSystemUIInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
    }
}
