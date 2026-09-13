using System;
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

    // AUD-012 Phase 2b: the primary human's tracked PlayerHealth, display name and resolved match
    // rules, plus a live damage-display-text reader, composed by
    // GameLevelManager.BindPlayerHealthBarContext instead of this component reading
    // GameLevelManager.instance/MatchRuntime.Rules directly.
    private ResolvedMatchRules matchRules;
    private string boundCharacterDisplayName;
    private Func<Text> damageDisplayTextReader;
    private bool contextBound;

    /// <summary>
    /// Explicit composition of everything this HUD needs, from
    /// <c>GameLevelManager.BindPlayerHealthBarContext</c>, called once from
    /// <c>GameLevelManager.Awake()</c> after the primary human has been spawned - not <c>Start()</c>:
    /// this HUD's own <c>Start()</c> consumes the bound context synchronously to decide whether to
    /// activate itself, and Unity does not order <c>Start()</c> across independent components, so
    /// binding from <c>GameLevelManager.Start()</c> could race this component's <c>Start()</c> and
    /// leave the HUD permanently deactivated for the match. Binding from <c>Awake()</c> guarantees this
    /// call precedes every <c>Start()</c> in the scene; everything it reads is already final by then
    /// (<c>PlayerHealth.Awake()</c> sets Health/Block/Special, and a human's
    /// <c>CharacterProfile.playerDisplayName</c> is set synchronously by
    /// <c>SpawnCoordinator.RegisterHuman</c> during that same <c>Awake()</c>). Replaces this HUD's
    /// former direct <c>MatchRuntime.Rules</c> read and its
    /// <c>GameLevelManager.instance.Player1</c>/<c>PlayerController1</c> reach-throughs. <paramref
    /// name="damageDisplayTextReader"/> is a live <see cref="Func{Text}"/> rather than a captured
    /// <c>Text</c>: <c>PlayerController.DamageDisplayValueText</c> is only populated inside that
    /// controller's own <c>Start()</c>, whose ordering relative to this call is not guaranteed, so it
    /// must be resolved fresh at the point a message is actually displayed - matching the original
    /// direct read's timing.
    /// </summary>
    public void BindPrimaryHumanContext(
        ResolvedMatchRules rules,
        PlayerHealth trackedHealth,
        string characterDisplayName,
        Func<Text> damageDisplayTextReader)
    {
        matchRules = rules;
        playerHealth = trackedHealth;
        boundCharacterDisplayName = characterDisplayName;
        this.damageDisplayTextReader = damageDisplayTextReader;
        contextBound = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        //GameOptions.sniperEnabled = true; // test flag
        if (contextBound
            && matchRules != null
            && (matchRules.EnemiesEnabled
            || matchRules.SniperEnabled
            || matchRules.EnemiesOnly
            || matchRules.ObstaclesEnabled
            || matchRules.IsBattleRoyal))
        {
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

            characterNameText.text = boundCharacterDisplayName;
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
        Text damageDisplayValueText = damageDisplayTextReader != null ? damageDisplayTextReader() : null;
        if (damageDisplayValueText == null)
        {
            yield break;
        }

        damageDisplayValueText.text = "-" + damage.ToString();
        yield return new WaitForSeconds(0.7f);
        damageDisplayValueText.text = "";
    }
    public IEnumerator DisplayCustomMessageOnDamageDisplay(string message)
    {
        Text damageDisplayValueText = damageDisplayTextReader != null ? damageDisplayTextReader() : null;
        if (damageDisplayValueText == null)
        {
            yield break;
        }

        damageDisplayValueText.text = message;
        yield return new WaitForSeconds(0.7f);
        damageDisplayValueText.text = "";
    }
}
