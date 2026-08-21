using UnityEngine;

public class SunglassesCollision : MonoBehaviour
{
    // Start is called before the first frame update
    public SpriteRenderer spriteRenderer;
    bool sunglassesDisabled;
    [SerializeField]
    GameObject CameraPostProcessing;

    private void Start()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // AUD-080: CameraPostProcessing is a serialized reference nothing validates at build time,
        // and TheyLiveManager.instance below is a scene-scoped singleton not guaranteed present in
        // every scene this component could be placed in - both are now guarded rather than
        // dereferenced directly.
        if (CameraPostProcessing != null)
        {
            CameraPostProcessing.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || sunglassesDisabled)
        {
            return;
        }

        sunglassesDisabled = true;
        if (TheyLiveManager.instance != null)
        {
            TheyLiveManager.instance.TheyLiveEnabled = true;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (CameraPostProcessing != null)
        {
            CameraPostProcessing.SetActive(true);
        }
    }
}
