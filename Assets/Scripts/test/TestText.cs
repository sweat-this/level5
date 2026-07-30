using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
public class TestText : MonoBehaviour
{
    [SerializeField]
    Text testText;
    [SerializeField]
    public static TestText instance;

    // Start is called before the first frame update
    void Start()
    {
        testText = GetComponent<Text>();
        instance = this;
    }

    public void setText(string text)
    {
        testText.text = text;
    }
}
#endif
