
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

            if (standaloneInputModule == null && inputSystemUIInputModule == null)
            {
                Debug.LogWarning("PlatformCheck found an EventSystem without a StandaloneInputModule or InputSystemUIInputModule.");
            }
        }
        else
        {
            Debug.LogWarning("PlatformCheck could not find an EventSystem.");
        }

#if UNITY_ANDROID || UNITY_IOS 

        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        SetUiModuleState(useStandaloneInputModule: false);
#endif

#if UNITY_STANDALONE || UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
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
}
