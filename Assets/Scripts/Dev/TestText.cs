using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
public class TestText : MonoBehaviour
{
    [SerializeField]
    Text testText;
    [SerializeField]
    public static TestText instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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
