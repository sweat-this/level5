using UnityEngine;
using UnityEngine.UI;

public class BodyGuardHealthBar : MonoBehaviour
{
    [SerializeField]
    BodyGuardHealth bodyGuardHealth;
    [SerializeField]
    public Slider healthSlider;

    public Slider Slider => healthSlider;

    // Start is called before the first frame update
    void Start()
    {

        bodyGuardHealth = transform.parent.GetComponentInChildren<BodyGuardHealth>();
        healthSlider = GetComponentInChildren<Slider>();
        healthSlider.maxValue = bodyGuardHealth.MaxEnemyHealth;
        bodyGuardHealth.OnHealthChanged += setHealthSliderValue;
        setHealthSliderValue();
    }

    private void OnDestroy()
    {
        if (bodyGuardHealth == null)
        {
            return;
        }

        bodyGuardHealth.OnHealthChanged -= setHealthSliderValue;
    }

    // Update is called once per frame
    public void setHealthSliderValue()
    {
        healthSlider.maxValue = bodyGuardHealth.MaxEnemyHealth;
        healthSlider.value = bodyGuardHealth.Health;
        //healthSliderValueText.text = healthSlider.value.ToString("0") + "%";
        //Debug.Log("slider.value : " + slider.value.ToString());
    }
}
