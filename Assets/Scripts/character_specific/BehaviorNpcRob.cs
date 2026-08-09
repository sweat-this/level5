using Level5.Core.Match;
﻿using UnityEngine;

public class BehaviorNpcRob : MonoBehaviour
{
    [SerializeField] GameObject[] enemies;
    private AudioSource audioSource;
    private GameObject spriteObject;

    private void Start()
    {
        spriteObject = transform.GetComponentInChildren<SpriteRenderer>().gameObject;
        if (MatchRuntime.CustomCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        //enemies = GameObject.FindGameObjectsWithTag("enemy");
        if (GameLevelManager.instance.players != null)
        {
            audioSource = GameObject.FindWithTag("basketball").GetComponent<AudioSource>();
        }
    }

    private void LightningStrike()
    {
        enemies = GameObject.FindGameObjectsWithTag("enemy");
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyController enemyController = enemy.GetComponent<EnemyController>();
                StartCoroutine(enemyController.struckByLighning(0));
                //if (enemyController.SpriteRenderer.isVisible)
                //{
                //    StartCoroutine(enemyController.struckByLighning(0));
                //}
            }
        }
    }
    private void DestroyRob()
    {
        Destroy(transform.root.gameObject);
    }

    public void playSfxCloudOfSmoke()
    {
        audioSource.PlayOneShot(SFXBB.instance.turnIntoBat);
    }
}
