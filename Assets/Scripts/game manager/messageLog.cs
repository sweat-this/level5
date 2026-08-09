using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class messageLog : MonoBehaviour
{
    Text log;

    public static messageLog instance;

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
        instance = this;
        log = GetComponent<Text>();
    }

    public void toggleMessageDisplay(String message)
    {

        StartCoroutine(ToggleMessageDisplayLog(5, message));
    }

    IEnumerator ToggleMessageDisplayLog(float seconds, String message)
    {
        log.text = message;
        yield return new WaitForSeconds(seconds);
        log.text = "";
    }
}
