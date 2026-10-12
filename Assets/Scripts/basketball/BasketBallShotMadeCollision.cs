
using UnityEngine;

public class BasketBallShotMadeCollision : MonoBehaviour
{
    [SerializeField]
    BasketBallShotMade basketBallShotMade;

    void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled
            && (other.gameObject.CompareTag("basketball") || other.gameObject.CompareTag("basketballAuto"))
            && gameObject.name.Equals("basketBallMadeShot2"))
        {
            if (basketBallShotMade == null)
            {
                Debug.LogError($"'{name}' has no bound upper made-shot trigger; skipping crossing.", this);
                return;
            }
            // AUD-013: the colliding ball's own runtime binding, not a ball-side PlayerIdentifier.
            IBasketballRuntime runtime = other.GetComponent<IBasketballRuntime>();
            if (runtime == null)
            {
                Debug.LogError($"'{other.gameObject.name}' triggered a made shot with no basketball runtime binding.", other.gameObject);
                return;
            }

            // Consecutive-shot tracking (AUD-065) now happens inside shotMade(), before scoring
            // reads it, so the streak bonus reflects the shot that was just made.
            basketBallShotMade.shotMade(runtime);
        }
    }
}

