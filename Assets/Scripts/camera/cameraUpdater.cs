
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Level5.Core.Match;

public class cameraUpdater : MonoBehaviour
{

    GameObject player;
    Camera cam;
    Vector3 basketBallRim;
    public Vector3 playerPos, camPos, rimPos;

    public float xMin, xMax, zMin, zMax, yMin, yMax;
    public float distanceCamFromPlayer, distanceRimFromPlayer;
    public float floatcameraDistanceFromGoal;

    public float ZoomAmount = 0; //With Positive and negative values
    public float MaxToClamp = 10;
    public float ROTSpeed = 0.1f;

    [SerializeField]
    public bool customCamera;

    [SerializeField]
    bool cameraZoomedIn, cameraZoomedOut;
    [SerializeField]
    float startZoomDistance;
    [SerializeField]
    private float addToCameraPosY;
    [SerializeField]
    float playerDistanceFromRimX;
    [SerializeField]
    float playerDistanceFromRimZ;

    [SerializeField]
    bool isOrthoGraphic;

    bool mainPerspectiveCamActive;
    //bool orthoCam1Active;
    //bool orthoCam2Active;
    bool isFollowBallCamera;

    [SerializeField]
    public float smoothSpeed = 0.125f;
    [SerializeField]
    private Vector3 lockOnGoalCameraOffset;

    bool cameraLockToGoal;
    private bool locked;
    [SerializeField]
    private bool isLockOnGoalCamera;

    bool onGoalCameraEnabled;
    [SerializeField]
    bool smoothCameraMotion;

    // flag for activating weather system prefab
    // set this in camera manager because it is based on a specific level
    // GM if( level requires weather ) --> for each cam, requires weather = true;
    // Cam Update if(requires weather) weather.setActive(true)
    GameObject weatherSystemObject;
    [SerializeField]
    bool requiresWeatherSystem;
    [SerializeField]
    private float cameraOffset;
    [SerializeField]
    private bool sniperCamera = false;

    // ---- shared framing (two local humans) ----------------------------------------------------
    // Only used when the roster seats more than one local human. A one-human match never reads any
    // of this and keeps the single-target path above exactly as it was.

    /// <summary>World units of room kept between each human and the edge of the frame.</summary>
    [SerializeField]
    private float sharedFramingPadding = 1.5f;

    /// <summary>
    /// The farthest the shared camera will dolly back from its authored position. Beyond this the
    /// humans can separate past the frame; the zoom is bounded rather than following without limit.
    ///
    /// Seven is roughly what The Scrapyard tolerates: the gameplay camera is pitched down at a ground plane that
    /// ends a few units behind the authored position, and dollying back much further shows the void
    /// under it at the bottom of the frame.
    /// </summary>
    [SerializeField]
    private float sharedFramingMaxExtraDistance = 7f;

    private bool multiHumanTopology;
    private readonly List<GameObject> humanActors = new List<GameObject>(2);
    private readonly List<Vector3> humanPositions = new List<Vector3>(2);
    private Vector3 restPosition;
    private float restFarClip;
    private Vector3 sharedBase;
    private float sharedExtra;
    private bool sharedInitialized;
    private bool warnedSpecialCamera;

    public bool RequiresWeatherSystem { get => requiresWeatherSystem; set => requiresWeatherSystem = value; }

    void Start()
    {
        sniperCamera = false;
        requiresWeatherSystem = MatchRuntime.LevelHasWeather;

        // get weather system object reference
        foreach (Transform t in gameObject.transform)
        {
            //Debug.Log("transform name : " + t.name + "  transform tage : "+ t.tag);
            //#hack
            if (t.CompareTag("weather_system") && !t.name.Contains("goal"))
            {
                weatherSystemObject = t.gameObject;
                if (requiresWeatherSystem || SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_03_snow))
                {
                    //Debug.Log("WEATHER ACTIVE -- \ntransform name : " + t.name + "  transform tage : " + t.tag);
                    weatherSystemObject.SetActive(true);
                }
                else
                {
                    weatherSystemObject.SetActive(false);
                }
            }
        }

        if (GameLevelManager.instance != null)
        {
            basketBallRim = GameLevelManager.instance.BasketballRimVector;
            PlayerIdentifier playerIdentifier = GameLevelManager.instance.players != null
                ? GameLevelManager.instance.players.Find((x) => x != null && x.pid == 0)
                : null;
            player = playerIdentifier != null
                ? playerIdentifier.player ?? playerIdentifier.autoPlayer
                : null;
            smoothCameraMotion = player != null;
        }

        if (player == null)
        {
            GameObject humanPlayer = GameObject.FindGameObjectWithTag("Player");
            player = humanPlayer != null ? humanPlayer : GameObject.FindGameObjectWithTag("autoPlayer");
            smoothCameraMotion = false;
        }

        cam = GetComponent<Camera>();
        //cam.depth = -5;

        // The camera answers to who is seated, not to a mode: any roster with more than one local
        // human is framed together.
        multiHumanTopology = MatchRuntime.Roster != null && MatchRuntime.Roster.LocalHumanCount > 1;
        restPosition = transform.position;
        restFarClip = cam.farClipPlane;

        // this is for the sorting layers. when using perspective camera like i am,
        // sometimes the rendering isnt always done by z values because perspective 
        // uses a value that closest to center of the camera or something
        // this should finally fix all the rendering problems i've been having
        cam.transparencySortMode = TransparencySortMode.Orthographic;

        // will check settings and set intial camera
        setCamera();

  
        //relCameraPos = player.position - transform.position;

    }


    void Update()
    {
        if (multiHumanTopology)
        {
            UpdateMultiHumanPresentation();
            return;
        }

        if (player == null)
        {
            return;
        }

        if (GameLevelManager.instance != null) 
        {
            playerDistanceFromRimX = basketBallRim.x - player.transform.position.x;
            playerDistanceFromRimZ = Math.Abs(player.transform.position.z);
        }

        // CameraManager.switchCamera() can force the goal camera off directly (it always excludes
        // the goal camera from the regular switch cycle), without going through toggleCameraOnGoal -
        // AUD-070. Re-sync the tracked flag to the camera's real active state before trusting it, the
        // same self-healing the CameraOnGoalAllowed check below already does for the other case.
        bool goalCameraActuallyActive =
            CameraManager.instance.Cameras[CameraManager.instance.CameraOnGoalIndex].activeSelf;
        if (onGoalCameraEnabled != goalCameraActuallyActive)
        {
            onGoalCameraEnabled = goalCameraActuallyActive;
        }

        if (!CameraManager.instance.CameraOnGoalAllowed && onGoalCameraEnabled)
        {
            CameraManager.instance.Cameras[CameraManager.instance.CameraOnGoalIndex].SetActive(false);
            onGoalCameraEnabled = false;
        }

        // * note change var to player distance because each camera is in a different spot
        if (Math.Abs(playerDistanceFromRimX) > 8 && !onGoalCameraEnabled
            && CameraManager.instance.CameraOnGoalAllowed
            && !MatchRuntime.Rules.EnemiesOnly
            && !MatchRuntime.Rules.IsBattleRoyal)
        {
            toggleCameraOnGoal();
        }

        if (Math.Abs(playerDistanceFromRimX) < 8 && onGoalCameraEnabled
            && CameraManager.instance.CameraOnGoalAllowed
            && !MatchRuntime.Rules.EnemiesOnly
            && !MatchRuntime.Rules.IsBattleRoyal)
        {
            toggleCameraOnGoal();
        }
        //if (isLockOnGoalCamera)
        //{
        //    transform.position = basketBallRim + lockOnGoalCameraOffset;
        //}


        //if (distanceRimFromPlayer > startZoomDistance
        //    && !cameraZoomedOut && !isFollowBallCamera && !isLockOnGoalCamera)
        ////&& cam.transform.position.z > zMin)
        //{
        //    zoomOut();
        //}
        //if (distanceRimFromPlayer < startZoomDistance && cameraZoomedOut)
        //{
        //    zoomIn();
        //
        if ((player != null) && isOrthoGraphic && !isFollowBallCamera && !isLockOnGoalCamera)
        {
            if (!sniperCamera)
            {
                transform.position = new Vector3(Mathf.Clamp(player.transform.position.x, xMin, xMax),
                //cam.transform.position.y,
                addToCameraPosY,
                cam.transform.position.z);
            }
            else
            {
                transform.position = new Vector3(BasketBall.instance.transform.position.x,
                     transform.position.y,
                     transform.position.z);
            }
        }
        if ((player != null) && isFollowBallCamera && !isLockOnGoalCamera)
        {
            if (!sniperCamera)
            {
                transform.position = new Vector3(BasketBall.instance.transform.position.x,
                     BasketBall.instance.transform.position.y + 0.5f,
                     BasketBall.instance.transform.position.z - 2);
            }
            else
            {
                transform.position = new Vector3(BasketBall.instance.transform.position.x,
                     transform.position.y,
                     transform.position.z);
            }
        }

    }

    void FixedUpdate()
    {
        if (multiHumanTopology)
        {
            UpdateMultiHumanPosition();
            return;
        }

        if (isLockOnGoalCamera && !sniperCamera)
        {
            transform.position = basketBallRim + lockOnGoalCameraOffset;
        }

        if ((player != null) && mainPerspectiveCamActive && !isFollowBallCamera && !isLockOnGoalCamera)
        {
            // * note change var to player distance because each camera is in a different spot
            if ((playerDistanceFromRimX < -7 || playerDistanceFromRimX > 7)
                && !((playerDistanceFromRimX < -8 || playerDistanceFromRimX > 8)))
            {
                updatePositionNearGoal();
            }
            else
            {
                updatePositionOnPlayer();
            }
        }

    }

    /// <summary>
    /// Everything the one-human <see cref="Update"/> does that a shared camera must not: the goal
    /// inset is opened and closed by player 0's distance from the rim, which says nothing about the
    /// other human, so it is held closed for the whole match.
    ///
    /// The follow-ball, orthographic and sniper paths are single-target too (they read
    /// <c>BasketBall.instance</c>, which is one player's ball). None is reachable from a normal
    /// two-human match - <see cref="CameraManager"/> starts on the perspective camera and refuses
    /// to cycle away from it - so they are not extended here.
    /// </summary>
    private void UpdateMultiHumanPresentation()
    {
        CameraManager manager = CameraManager.instance;
        if (manager == null || manager.Cameras == null)
        {
            return;
        }

        int goalIndex = manager.CameraOnGoalIndex;
        if (goalIndex >= 0 && goalIndex < manager.Cameras.Length)
        {
            GameObject goalCamera = manager.Cameras[goalIndex];
            if (goalCamera != null && goalCamera.name.Contains("goal") && goalCamera.activeSelf)
            {
                goalCamera.SetActive(false);
            }
        }

        onGoalCameraEnabled = false;
    }

    private void UpdateMultiHumanPosition()
    {
        bool sharedPerspective = mainPerspectiveCamActive
            && !isOrthoGraphic
            && !isFollowBallCamera
            && !isLockOnGoalCamera
            && !sniperCamera;
        if (!sharedPerspective)
        {
            if (!warnedSpecialCamera && gameObject.activeInHierarchy)
            {
                warnedSpecialCamera = true;
                Debug.LogWarning($"cameraUpdater '{name}' is a single-target camera and does not frame two local humans; it is holding still.");
            }

            return;
        }

        ResolveHumanActors();
        if (SharedCameraFraming.CollectLivePositions(humanActors, humanPositions) == 0
            || !SharedCameraFraming.TryGetBounds(humanPositions, out Bounds bounds))
        {
            // No live human: hold where the camera is rather than chase a destroyed target.
            return;
        }

        if (!sharedInitialized)
        {
            sharedBase = transform.position;
            sharedExtra = 0f;
            sharedInitialized = true;
        }

        Vector3 middle = bounds.center;

        // The camera's own height rule, applied to the middle of the group instead of one actor. Z is
        // the authored rest depth: the single-target path reads the current z, which is only safe
        // while nothing ever changes it.
        bool tracksHeight = !customCamera || SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_21_shore);
        Vector3 targetBase = new Vector3(
            middle.x + cameraOffset,
            tracksHeight ? middle.y + addToCameraPosY : restPosition.y,
            restPosition.z);

        // Always eased: the single-target path only eases once it has found player 0 in Start, a flag
        // that says nothing about whether this camera has live humans to follow.
        float blend = Mathf.Clamp01(smoothSpeed * Time.fixedDeltaTime);
        sharedBase = Vector3.Lerp(sharedBase, targetBase, blend);

        // Measured from where the camera actually is, not where it is heading, so a fast walker is
        // never left out of frame while the smoothing catches up. Zooming out is immediate; zooming
        // back in eases.
        float required = SharedCameraFraming.RequiredExtraDistance(
            humanPositions,
            sharedBase,
            transform.rotation,
            cam.fieldOfView,
            cam.aspect,
            sharedFramingPadding,
            sharedFramingMaxExtraDistance);
        sharedExtra = required >= sharedExtra
            ? required
            : Mathf.Lerp(sharedExtra, required, blend);

        transform.position = sharedBase - (transform.forward * sharedExtra);

        // Keep the same stretch of world in the far plane the authored camera saw.
        cam.farClipPlane = restFarClip + sharedExtra;
    }

    /// <summary>
    /// The actors to frame: every non-CPU participant the scene has spawned. Read from the live
    /// registry each tick rather than cached, so a human who is destroyed or disabled simply drops out.
    /// </summary>
    private void ResolveHumanActors()
    {
        humanActors.Clear();
        if (GameLevelManager.instance == null || GameLevelManager.instance.players == null)
        {
            return;
        }

        foreach (PlayerIdentifier participant in GameLevelManager.instance.players)
        {
            if (participant != null && !participant.isCpu)
            {
                humanActors.Add(participant.player);
            }
        }
    }

    public void toggleCameraOnGoal()
    {
        onGoalCameraEnabled = !onGoalCameraEnabled;
        if (onGoalCameraEnabled)
        {
            CameraManager.instance.Cameras[CameraManager.instance.CameraOnGoalIndex].SetActive(true);
        }
        if (!onGoalCameraEnabled)
        {
            CameraManager.instance.Cameras[CameraManager.instance.CameraOnGoalIndex].SetActive(false);
        }
    }

    private void updatePositionOnPlayer()
    {
        Vector3 targetPosition;
        if (!sniperCamera)
        {
            if (!customCamera || SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_21_shore))
            {
                targetPosition = new Vector3(player.transform.position.x + cameraOffset, player.transform.position.y + addToCameraPosY, cam.transform.position.z);
            }
            else
            {
                targetPosition = new Vector3(player.transform.position.x + cameraOffset, gameObject.transform.position.y, cam.transform.position.z);
            }
        }
        else
        {
            targetPosition = new Vector3(player.transform.position.x + cameraOffset, cam.transform.position.y, cam.transform.position.z);
        }
        if (smoothCameraMotion)
        {
            Vector3 desiredPosition = targetPosition;
            Vector3 smoothedPosition = Vector3.Lerp(gameObject.transform.position, targetPosition, smoothSpeed * Time.fixedDeltaTime);
            transform.position = smoothedPosition;
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    private void updatePositionNearGoal()
    {
        Vector3 targetPosition = new Vector3();
        if (!customCamera || SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_21_shore))
        {
            targetPosition = new Vector3(cam.transform.position.x, player.transform.position.y + addToCameraPosY, cam.transform.position.z);
        }
        if (customCamera &&  !SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_21_shore))
        {
            targetPosition = new Vector3(cam.transform.position.x, gameObject.transform.position.y, cam.transform.position.z);
        }
        Vector3 desiredPosition = targetPosition;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    //private void zoomOut()
    //{
    //    if (ZoomAmount == -20)
    //    {
    //        cameraZoomedOut = true;
    //    }
    //    else
    //    {
    //        cameraZoomedOut = false;
    //    }

    //    //Debug.Log("zoom out camera");
    //    ZoomAmount -= .5f;
    //    //Debug.Log("zoomAmount : " + ZoomAmount + "Input.GetAxis(mouse_axis_2) : " + Input.GetAxis("mouse_axis_2"));
    //    ZoomAmount = Mathf.Clamp(ZoomAmount, -MaxToClamp, MaxToClamp);
    //    var translate = Mathf.Min(Mathf.Abs(-1), MaxToClamp - Mathf.Abs(ZoomAmount));
    //    gameObject.transform.Translate(0, 0, translate * ROTSpeed * Mathf.Sign(-1));
    //}


    //private void zoomIn()
    //{
    //    if (ZoomAmount == 0.5f)
    //    {
    //        cameraZoomedOut = false;
    //    }
    //    //Debug.Log("zoom in camera");
    //    ZoomAmount += .5f;
    //    //Debug.Log("zoomAmount : " + ZoomAmount + "Input.GetAxis(mouse_axis_2) : " + Input.GetAxis("mouse_axis_2"));
    //    ZoomAmount = Mathf.Clamp(ZoomAmount, -MaxToClamp, MaxToClamp);
    //    var translate = Mathf.Min(Mathf.Abs(1), MaxToClamp - Mathf.Abs(ZoomAmount));
    //    gameObject.transform.Translate(0, 0, translate * ROTSpeed * Mathf.Sign(1));
    //}

    void setCamera()
    {
        if (customCamera)
        {
            mainPerspectiveCamActive = true;
        }
        // if perspective camera
        if (!isOrthoGraphic && !customCamera && !sniperCamera)
        {
            //if (SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_17_steel_cage))
            //{
            //    addToCameraPosY = 1;
            //    gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            //}
            //else
            //{
            //    addToCameraPosY = 1.835f;
            //}
            addToCameraPosY = 1.835f;
            mainPerspectiveCamActive = true;
            //orthoCam1Active = false;
            //orthoCam2Active = false;
        }

        // 2 orthographic cameras
        if (isOrthoGraphic && !customCamera)
        {
            if (cam.name.Contains("camera_orthographic_1"))
            {
                Debug.Log("link");
                addToCameraPosY = 2.5f;
                mainPerspectiveCamActive = false;
                //orthoCam1Active = true;
            }
            if (cam.name.Contains("camera_orthographic_2"))
            {
                Debug.Log("link");
                addToCameraPosY = 3.3f;
                mainPerspectiveCamActive = false;
                //orthoCam1Active = false;
                //orthoCam2Active = true;
            }
        }
        if (cam.name.Contains("follow_ball"))
        {
            isFollowBallCamera = true;
        }
        else
        {
            isFollowBallCamera = false;
        }
        if (cam.name.Contains("goal"))
        {
            isLockOnGoalCamera = true;
            if (!sniperCamera)
            {
                transform.position = basketBallRim + lockOnGoalCameraOffset;
            }
        }
        else
        {
            isLockOnGoalCamera = false;
        }
    }
}
