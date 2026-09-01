using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    public float initialHeight, finalHeight;
    private PlayerController playerController;
    [SerializeField]
    private AutoPlayerController autoPlayerController;

    [SerializeField]
    private BasketBallState basketBallState;

    private void Start()
    {
        // This component is authored both as a child of the player/autoPlayer actor and as a child
        // of the basketball itself. The actor case still has an actor-side PlayerIdentifier to walk
        // up to; the ball case does not (AUD-013 removed the ball's own duplicate PlayerIdentifier),
        // so it resolves the same data through the ball's IBasketballRuntime binding instead - not a
        // fallback to a ball-side PlayerIdentifier, a different legitimate parent hierarchy.
        PlayerIdentifier playerIdentifier = GetComponentInParent<PlayerIdentifier>();
        if (playerIdentifier != null)
        {
            if (playerIdentifier.isCpu)
            {
                autoPlayerController = playerIdentifier.autoPlayer.GetComponent<AutoPlayerController>();
                if (!playerIdentifier.isDefensivePlayer)
                {
                    basketBallState = playerIdentifier.autoBasketball.GetComponent<BasketBallState>();
                }
            }
            else
            {
                playerController = playerIdentifier.player.GetComponent<PlayerController>();
                basketBallState = playerIdentifier.basketball.GetComponent<BasketBallState>();
            }

            return;
        }

        IBasketballRuntime runtime = GetComponentInParent<IBasketballRuntime>();
        if (runtime == null)
        {
            // SetActive rather than enabled = false: OnTriggerStay/OnTriggerExit below fire regardless
            // of this component's enabled state, and both dereference basketBallState/playerController
            // unconditionally.
            Debug.LogError(
                $"GroundCheck on '{gameObject.name}' found neither a PlayerIdentifier nor an IBasketballRuntime in its parent hierarchy.",
                this);
            gameObject.SetActive(false);
            return;
        }

        basketBallState = runtime.State;
        if (runtime.IsCpu)
        {
            autoPlayerController = runtime.OwnerActor.GetComponent<AutoPlayerController>();
        }
        else
        {
            playerController = runtime.OwnerActor.GetComponent<PlayerController>();
        }
    }


    public void OnTriggerStay(Collider other)
    {
        // later 11 is ground/terrain
        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("Player"))
        {
            //initialHeight = _player.transform.position.y;
            //if (finalHeight - initialHeight > 1)
            //{
            //    //Debug.Log("fall distance : " + (finalHeight - initialHeight));
            //}

            playerController.Grounded = true;
            playerController.InAir = false;
            playerController.SetPlayerAnim("jump", false);

            //reset state flags
             playerController.CallBallToPlayer.Locked = false;
        }
        // later 11 is ground/terrain
        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("autoPlayer"))
        {
            //initialHeight = _player.transform.position.y;
            //if (finalHeight - initialHeight > 1)
            //{
            //    //Debug.Log("fall distance : " + (finalHeight - initialHeight));
            //}

            autoPlayerController.Grounded = true;
            autoPlayerController.InAir = false;
            autoPlayerController.SetPlayerAnim("jump", false);

            //reset state flags
            //CallBallToPlayer.instance.Locked = false;
        }
        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("basketball"))
        {
            //initialHeight = _player.transform.position.y;
            //if (finalHeight - initialHeight > 1)
            //{
            //    //Debug.Log("fall distance : " + (finalHeight - initialHeight));
            //}

            basketBallState.Grounded = true;
            basketBallState.InAir = false;
            if (playerController != null)
            {
                playerController.CallBallToPlayer.Locked = false;
            }
            if (autoPlayerController != null)
            {
                autoPlayerController.CallBallToPlayer.Locked = false;
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("Player"))
        {
            playerController.Grounded = false;
            playerController.InAir = true;
            playerController.SetPlayerAnim("jump", true);
        }
        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("autoPlayer"))
        {
            autoPlayerController.Grounded = false;
            autoPlayerController.InAir = true;
            autoPlayerController.SetPlayerAnim("jump", true);
        }

        if (other.gameObject.layer == 11 && gameObject.transform.parent.CompareTag("basketball"))
        {
            //initialHeight = _player.transform.position.y;
            //if (finalHeight - initialHeight > 1)
            //{
            //    //Debug.Log("fall distance : " + (finalHeight - initialHeight));
            //}

            basketBallState.Grounded = false;
            basketBallState.InAir = true;
        }
    }
}
