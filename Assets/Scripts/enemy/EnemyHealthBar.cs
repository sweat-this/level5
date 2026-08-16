using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField]
    EnemyHealth enemyHealth;
    [SerializeField]
    public Slider healthSlider;
    [SerializeField]
    Text heathBarMessageDisplayText;

    public Slider Slider => healthSlider;

    public Text HeathBarMessageDisplayText { get => heathBarMessageDisplayText;  }

    //public static PlayerHealthBar instance;

    // Start is called before the first frame update
    void Start()
    {
        // AUD-074: resolve both lookups before touching either, same shape AUD-071 fixed for
        // PlayerHealthBar - a partial wire-up (e.g. enemyHealth found but no Slider child) used
        // to NRE on healthSlider.maxValue before enemyHealth.OnHealthChanged was ever subscribed.
        enemyHealth = transform.parent != null ? transform.parent.GetComponentInChildren<EnemyHealth>() : null;
        healthSlider = GetComponentInChildren<Slider>();
        if (enemyHealth == null || healthSlider == null)
        {
            List<string> missing = new List<string>();
            if (enemyHealth == null)
            {
                missing.Add("EnemyHealth");
            }
            if (healthSlider == null)
            {
                missing.Add("Slider");
            }

            Debug.LogError("EnemyHealthBar is disabled: missing " +
                string.Join(", ", missing) + " on " + gameObject.name, this);
            gameObject.SetActive(false);
            return;
        }

        healthSlider.maxValue = enemyHealth.MaxHealth;
        enemyHealth.OnHealthChanged += setHealthSliderValue;
        setHealthSliderValue();
    }

    private void OnDestroy()
    {
        if (enemyHealth == null)
        {
            return;
        }

        enemyHealth.OnHealthChanged -= setHealthSliderValue;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (heathBarMessageDisplayText != null)
        {
            heathBarMessageDisplayText.text = string.Empty;
        }
    }

    // Update is called once per frame
    public void setHealthSliderValue()
    {
        healthSlider.maxValue = enemyHealth.MaxHealth;
        healthSlider.value = enemyHealth.Health;
        //healthSliderValueText.text = healthSlider.value.ToString("0") + "%";
        //Debug.Log(gameObject.transform.root.name +  " slider value : " + healthSlider.value.ToString());
    }

    public IEnumerator DisplayCustomMessageOnDamageDisplay(string message)
    {

        heathBarMessageDisplayText.text = message;
        yield return new WaitForSeconds(0.7f);
        heathBarMessageDisplayText.text = "";
    }
}
