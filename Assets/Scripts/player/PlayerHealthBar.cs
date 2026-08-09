using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Level5.Core.Match;

public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField]
    PlayerHealth playerHealth;
    [SerializeField]
    public Slider healthSlider;
    [SerializeField]
    public Slider blockSlider;
    [SerializeField]
    public Slider specialSlider;

    [SerializeField]
    Text characterNameText;
    [SerializeField]
    Text healthSliderValueText;

    const string characterNameName = "health_slider_character_text";
    const string healthSliderValueName = "health_slider_value_text";
    public static PlayerHealthBar instance;

    // Start is called before the first frame update
    void Start()
    {
        //GameOptions.sniperEnabled = true; // test flag
        if (MatchRuntime.Rules.EnemiesEnabled 
            || MatchRuntime.Rules.SniperEnabled 
            || MatchRuntime.Rules.EnemiesOnly
            || MatchRuntime.Rules.ObstaclesEnabled
            || MatchRuntime.Rules.IsBattleRoyal)
        {
            instance = this;
            playerHealth = GameLevelManager.instance.Player1.GetComponentInChildren<PlayerHealth>();
            healthSlider = transform.Find("health_bar").GetComponent<Slider>();
            blockSlider = transform.Find("block_bar").GetComponent<Slider>();
            specialSlider = transform.Find("special_bar").GetComponent<Slider>();

            healthSlider.maxValue = playerHealth.MaxHealth;
            blockSlider.maxValue = playerHealth.MaxBlock;
            specialSlider.maxValue = playerHealth.MaxSpecial;

            characterNameText = GameObject.Find(characterNameName).GetComponent<Text>();
            healthSliderValueText = GameObject.Find(healthSliderValueName).GetComponent<Text>();

            characterNameText.text = GameLevelManager.instance.Player1.GetComponent<CharacterProfile>().PlayerDisplayName;
            playerHealth.OnHealthChanged += setHealthSliderValue;
            playerHealth.OnBlockChanged += setBlockSliderValue;
            playerHealth.OnSpecialChanged += setSpecialSliderValue;
            setHealthSliderValue();
            setBlockSliderValue();
            setSpecialSliderValue();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnHealthChanged -= setHealthSliderValue;
        playerHealth.OnBlockChanged -= setBlockSliderValue;
        playerHealth.OnSpecialChanged -= setSpecialSliderValue;
    }

    public bool IsTracking(PlayerHealth health)
    {
        return playerHealth == health;
    }

    public void setHealthSliderValue()
    {
        healthSlider.value = playerHealth.Health;
        healthSliderValueText.text = healthSlider.value.ToString("0") + " / " + playerHealth.MaxHealth;
    }
    public void setBlockSliderValue()
    {
        blockSlider.value = playerHealth.Block;
    }

    public void setSpecialSliderValue()
    {
        specialSlider.value = playerHealth.Special;
    }

    public IEnumerator DisplayDamageTakenValue(int damage)
    {
        //transform.localScale = temp;
        GameLevelManager.instance.PlayerController1.DamageDisplayValueText.text = "-" + damage.ToString();
        yield return new WaitForSeconds(0.7f);
        GameLevelManager.instance.PlayerController1.DamageDisplayValueText.text = "";
    }
    public IEnumerator DisplayCustomMessageOnDamageDisplay(string message)
    {

        GameLevelManager.instance.PlayerController1.DamageDisplayValueText.text = message;
        yield return new WaitForSeconds(0.7f);
        GameLevelManager.instance.PlayerController1.DamageDisplayValueText.text = "";
    }
}
