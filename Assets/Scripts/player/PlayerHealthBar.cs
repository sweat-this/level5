using System.Collections;
using Assets.Scripts.Utility;
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
            playerHealth = GameLevelManager.instance.Player1.GetComponentInChildren<PlayerHealth>();
            Transform healthBarTransform = transform.Find("health_bar");
            Transform blockBarTransform = transform.Find("block_bar");
            Transform specialBarTransform = transform.Find("special_bar");
            GameObject characterNameObject = SceneObjects.Find(characterNameName, this);
            GameObject healthSliderValueObject = SceneObjects.Find(healthSliderValueName, this);

            healthSlider = healthBarTransform != null ? healthBarTransform.GetComponent<Slider>() : null;
            blockSlider = blockBarTransform != null ? blockBarTransform.GetComponent<Slider>() : null;
            specialSlider = specialBarTransform != null ? specialBarTransform.GetComponent<Slider>() : null;
            characterNameText = characterNameObject != null ? characterNameObject.GetComponent<Text>() : null;
            healthSliderValueText = healthSliderValueObject != null ? healthSliderValueObject.GetComponent<Text>() : null;

            // All five are required - setHealthSliderValue/setBlockSliderValue/setSpecialSliderValue
            // dereference them unconditionally, and stay subscribed to playerHealth's change events for
            // this component's whole lifetime, so a partial resolution here would crash later instead
            // of now. Bail out the same way the mode-gate's else branch already does.
            if (playerHealth == null || healthSlider == null || blockSlider == null || specialSlider == null
                || characterNameText == null || healthSliderValueText == null)
            {
                Debug.LogError("PlayerHealthBar could not resolve its required scene objects and has been disabled.", this);
                gameObject.SetActive(false);
                return;
            }

            instance = this;
            healthSlider.maxValue = playerHealth.MaxHealth;
            blockSlider.maxValue = playerHealth.MaxBlock;
            specialSlider.maxValue = playerHealth.MaxSpecial;

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
        // Released first, and outside the playerHealth guard below: a bar destroyed before it ever
        // resolved its health source would otherwise return early and leave the static pointing at
        // a destroyed object.
        if (instance == this)
        {
            instance = null;
        }

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
