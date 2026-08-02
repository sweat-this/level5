using System.Collections;
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

        enemyHealth = transform.parent.GetComponentInChildren<EnemyHealth>();
        healthSlider = GetComponentInChildren<Slider>();
        healthSlider.maxValue = enemyHealth.MaxEnemyHealth;
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

    // Update is called once per frame
    public void setHealthSliderValue()
    {
        healthSlider.maxValue = enemyHealth.MaxEnemyHealth;
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
