using Assets.Scripts.Utility;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DevFunctions : MonoBehaviour
{

    //[SerializeField] CharacterProfile player;
    [SerializeField] GameObject fpsCounter;
    [SerializeField] GameObject[] enemies;
    [SerializeField] Text messageText;
    //[SerializeField] float smoothSpeed;

    public static DevFunctions instance;

    bool fpsActive = false;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        //player = GameLevelManager.instance.CharacterProfile;
        //debugText = GameObject.Find("debug_text").GetComponent<Text>();
        messageText = GameObject.Find("messageDisplay").GetComponent<Text>();

        if (GameLevelManager.instance != null)
        {
            fpsCounter = GameObject.Find("LiteFPSCounter");
            fpsCounter.SetActive(false);
        }
    }
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //if (GameLevelManager.instance.Controls.Other.change.enabled
        //    && GameLevelManager.instance.Controls.Other.toggle_character_max_stats.triggered)
        //{
        //    setMaxPlayerStats();
        //}
        if (GameLevelManager.instance.Controls.Other.change.enabled
            && GameLevelManager.instance.Controls.Other.toggle_fps_counter.triggered)
        {
            ToggleFpsCounter();
        }
        if (GameLevelManager.instance.Controls.Other.change.enabled && Input.GetKeyDown(KeyCode.Alpha7))
        {
            InstantiateRob();
        }
        if (GameLevelManager.instance.Controls.Other.change.enabled && Input.GetKeyDown(KeyCode.Alpha8))
        {
            Shrinkplayer();
        }
        if (GameLevelManager.instance.Controls.Other.change.enabled && Input.GetKeyDown(KeyCode.Alpha9))
        {
            StartCoroutine( LoadGame.LoadDevLevelVersus(1));
        }
#endif
    }

    private void Shrinkplayer()
    {
        Debug.Log("player struck by lightning");
        PlayerController player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
        if (!player.isShrunk)
        {
            StartCoroutine(player.ShrinkPlayer());
        }
    }

    private void InstantiateRob()
    {
        Vector3 _playerSpawnLocation = GameLevelManager.instance.Player1.transform.position;
        Vector3 spawn = new Vector3(_playerSpawnLocation.x + 1.5f,
            _playerSpawnLocation.y,
            _playerSpawnLocation.z);
        string playerPrefabPath = "Prefabs/characters/npc_specific/npc_rob";
        GameObject _playerClone = Resources.Load(playerPrefabPath) as GameObject;

        Instantiate(_playerClone, spawn, Quaternion.identity);
    }

    //public void setMaxPlayerStats()
    //{
    //    player.Accuracy2Pt = 100;
    //    player.Accuracy3Pt = 100;
    //    player.Accuracy4Pt = 100;
    //    player.Accuracy7Pt = 100;
    //    player.Release = 100;
    //    player.Range = 100;
    //    player.Luck = 10;

    //    messageText.text = "max player stats enabled";
    //    StartCoroutine(turnOffMessageLogDisplayAfterSeconds(3));
    //}

    public void ToggleFpsCounter()
    {
        fpsActive = !fpsActive;

        if (fpsActive)
        {
            fpsCounter.SetActive(true);
        }
        else
        {
            fpsCounter.SetActive(false);
        }
    }

    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "";
    }
}
