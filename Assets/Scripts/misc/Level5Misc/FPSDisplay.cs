using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    float deltaTime = 0.0f;
    Text fpsText;
    float msec;
    float fps;

    private void Awake()
    {
        fpsText = GetComponent<Text>();
        //InvokeRepeating("updateFPS", 0, 0.5f);
    }

    void Update()
    {
        updateFPS();
    }

    void updateFPS()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        msec = deltaTime * 1000.0f;
        fps = 1.0f / deltaTime;
        fpsText.text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
    }
}