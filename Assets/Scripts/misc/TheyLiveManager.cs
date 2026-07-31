using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheyLiveManager : MonoBehaviour
{
    Sprite currentSprite;
    //SpriteRenderer spriteRenderer;
    [SerializeField]
    bool theyLiveEnabled;
    bool on = false;

    GameObject[] billboardList;

    public List<Sprite> theyLiveSpriteList;

    string path = "billboards";

    public static TheyLiveManager instance;

    void Awake()
    {
        //spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        //currentSprite = spriteRenderer.sprite;
        instance = this;

        if (theyLiveSpriteList == null)
        {
            theyLiveSpriteList = new List<Sprite>();
        }

        billboardList = GameObject.FindGameObjectsWithTag("billboard");

        Sprite[] temp = Resources.LoadAll<Sprite>(path) as Sprite[];
        foreach (Sprite sprite in temp)
        {
            if (sprite.name.ToLower().Contains("theylive"))
            {
                theyLiveSpriteList.Add(sprite);
            }
        }
    }

    private void Update()
    {
        if (theyLiveEnabled && !on)
        {
            on = true;
            LoadBillboards();
        }
        if (!theyLiveEnabled)
        {
            on = false;
        }
    }

    public void LoadBillboards()
    {
        if (billboardList == null || theyLiveSpriteList == null || theyLiveSpriteList.Count == 0)
        {
            return;
        }

        Sprite watchTvSprite = theyLiveSpriteList.FirstOrDefault(x => x.name.ToLower().Contains("watchtv"));
        foreach (GameObject billboard in billboardList)
        {
            if (billboard == null)
            {
                continue;
            }

            SpriteRenderer spriteRenderer = billboard.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                continue;
            }

            int randomIndex = Random.Range(0, theyLiveSpriteList.Count);
            Sprite currentSprite = spriteRenderer.sprite;

            if (currentSprite != null && currentSprite.name.ToLower().Contains("fbipi") && watchTvSprite != null)
            {
                spriteRenderer.sprite = watchTvSprite;
            }
            else
            {
                spriteRenderer.sprite = theyLiveSpriteList[randomIndex];
            }
        }
    }

    public bool TheyLiveEnabled { get => theyLiveEnabled; set => theyLiveEnabled = value; }
    public bool On { get => on; set => on = value; }

}
