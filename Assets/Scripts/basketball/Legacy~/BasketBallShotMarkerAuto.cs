//using UnityEngine;
//using UnityEngine.UI;

//public class BasketBallShotMarkerAuto : MonoBehaviour
//{
//    //* note if var starts with underscore, it will have a publicly accessible property at bottom of file
//    // get/set. sometimes get only

//    // main state bool
//    [SerializeField]
//    private bool _playerOnMarker;
//    [SerializeField]
//    private bool markerEnabled; // flag used to indicate max shots have not been achieved

//    private GameObject basketBallTarget;
//    private SpriteRenderer spriteRenderer;
//    [SerializeField] private BasketBallState basketBallState;

//    [SerializeField] private int positionMarkerId; // identitfy specific marker
//    // spcific marker's stats
//    [SerializeField] private int _shotMade;
//    [SerializeField] private int _shotAttempt;
//    [SerializeField] private int maxShotAttempt;
//    [SerializeField] private int maxShotMade;

//    // flags used to idenify marker
//    // true value determines whether or not marker is active in Gamerules.cs, aprox. line 250
//    [SerializeField] private bool shotTypeThree;
//    [SerializeField] private bool shotTypeFour;
//    [SerializeField] private bool shotTypeSeven;
//    [SerializeField]
//    private bool detectCollisions;
//    private float distanceFromRim;

//    // text stuff todo: move to game rules
//    private Text displayCurrentMarkerStats;
//    private const string displayStatsTextObject = "shot_marker_stats";

//    // Start is called before the first frame update
//    void Start()
//    {
//        _shotMade = 0;
//        _shotAttempt = 0;

//        // get reference for accessing basketball state
//        basketBallState = GameLevelManager.instance.Basketball.BasketBallState;
//        displayCurrentMarkerStats = GameObject.Find(displayStatsTextObject).GetComponent<Text>();
//        displayCurrentMarkerStats.text = "";

//        // used to control opacity of marker image 
//        // todo: maybe just disable object. might require more work than it's worth
//        spriteRenderer = GetComponent<SpriteRenderer>();

//        // initial text display
//        setDisplayText();
//        // set what type of shot marker is based on distance from rim
//        // using basketball state
//        setMarkerShotType();

//        GameOptions.gameModeRequiresShotMarkers3s = true;
//        if (GameOptions.gameModeRequiresShotMarkers3s || GameOptions.gameModeRequiresShotMarkers4s)
//        {
//            markerEnabled = true;
//            setDisplayText();
//            // set what type of shot marker is based on distance from rim
//            // using basketball state
//            setMarkerShotType();
//        }
//        else // marker is not needed
//        {
//            // disable text and disable script
//            displayCurrentMarkerStats.text = "";
//            this.enabled = false;
//        }

//        // failsafe check. data is serialzed and can be set manually but automatic is better. trust the code
//        if (GameRules.instance.GameModeThreePointContest
//            || GameRules.instance.GameModeFourPointContest
//            || GameRules.instance.GameModeAllPointContest)
//        {
//            maxShotAttempt = 5;
//        }

//        // if script disabled, disable collisions flag.
//        // collisions/colliders still detected if script disabled
//        detectCollisions = this.enabled;
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        // in theory, disables all update checking unless required by game mode

//        //// if time's up
//        //if (Time.timeScale <= 0)
//        //{
//        //    displayCurrentMarkerStats.text = "";
//        //}
//        // this needs to be turned off if ball hits ground
//        if (PlayerOnMarker)
//        {
//            BasketBall.instance.BasketBallState.CurrentShotMarkerId = positionMarkerId;
//            // if marker not completed yet
//            //if (markerEnabled)
//            //{
//            //    setDisplayText();
//            //}
//        }

//        //// if game mode IS 3/4/all point contest
//        //if (GameRules.instance.GameModeThreePointContest
//        //    || GameRules.instance.GameModeFourPointContest
//        //    || GameRules.instance.GameModeAllPointContest)
//        //{
//        //    // max shot attempts reached
//        //    // player NOT in air, player does NOT have ball, ball ! in air
//        //    if (ShotAttempt >= maxShotAttempt & markerEnabled
//        //        && !GameLevelManager.instance.PlayerController.hasBasketball
//        //        && !GameLevelManager.instance.PlayerController.InAir
//        //        && !basketBallState.InAir)
//        //    {
//        //        markerEnabled = false;
//        //        // decrease markers remaining
//        //        GameRules.instance.MarkersRemaining--;
//        //        spriteRenderer.color = new Color(1f, 1f, 1f, 0f); // opacity to 0
//        //        setDisplayText();

//        //        //check if last remaining shot marker
//        //        if (GameRules.instance.IsGameOver())
//        //        {
//        //            //GameRules.instance.CounterTime = Timer.instance.CurrentTime;
//        //            GameRules.instance.GameOver = true;
//        //        }
//        //    }
//        //}
//        // game mode is NOT 3/4/All point contest
//        //if (!GameRules.instance.GameModeThreePointContest
//        //    || !GameRules.instance.GameModeFourPointContest
//        //    || !GameRules.instance.GameModeAllPointContest)
//        //{
//        //    // if made # of shots required at shot marker
//        //    if (ShotMade >= MaxShotMade && markerEnabled)
//        //    {
//        //        markerEnabled = false;
//        //        // decrease markers remaining
//        //        GameRules.instance.MarkersRemaining--;
//        //        spriteRenderer.color = new Color(1f, 1f, 1f, 0f); // opacity to 0
//        //        setDisplayText();

//        //        // check if last remaining shot marker
//        //        if (GameRules.instance.IsGameOver())
//        //        {

//        //            //GameRules.instance.CounterTime = Timer.instance.CurrentTime;
//        //            GameRules.instance.GameOver = true;
//        //        }
//        //    }
//        //}

//    }

//    void OnTriggerEnter(Collider other)
//    {
//        // if player enters shot marker area
//        if ((other.gameObject.CompareTag("playerHitbox") || other.gameObject.CompareTag("autoPlayerHitbox"))  
//            && gameObject.CompareTag("shotMarkerAuto")
//            && detectCollisions)
//        {
//            Debug.Log("enemy cpu ENTER marker");
//            AutoPlayerControllerTest enemy = other.transform.parent.GetComponent<AutoPlayerControllerTest>();
//            enemy.AutoPlayerArrivedAtMarker();
//            //enemy.targetCreated = false;
//            _playerOnMarker = true;
//        }
//    }

//    void OnTriggerExit(Collider other)
//    {
//        // if player exits shot marker area
//        if ((other.gameObject.CompareTag("playerHitbox") || other.gameObject.CompareTag("autoPlayerHitbox"))
//            && gameObject.CompareTag("shotMarkerAuto")
//                && detectCollisions)
//        {
//            Debug.Log("enemy cpu EXIT marker");
//            _playerOnMarker = false;
//            setDisplayText(); // update display to empty
//        }
//    }

//    private void setDisplayText()
//    {
//        // if player on marker and markers necessary for game mode and IS 3,4,All point contest
//        if (PlayerOnMarker && markerEnabled
//            && displayCurrentMarkerStats != null
//            && (GameRules.instance.GameModeThreePointContest
//            || GameRules.instance.GameModeFourPointContest
//            || GameRules.instance.GameModeAllPointContest))
//        {
//            displayCurrentMarkerStats.text = "total points : " + BasketBall.instance.GameStats.TotalPoints + "\n"
//                                             // + "current marker : " + positionMarkerId + "\n"
//                                             + "made : " + ShotMade + " / " + ShotAttempt + "\n"
//                                             + "remaining : " + (maxShotAttempt - ShotAttempt);
//        }
//        // if player on marker and markers necessary for game mode and NOT 3,4,All point contest
//        if (PlayerOnMarker && markerEnabled
//            && displayCurrentMarkerStats != null
//            && !(GameRules.instance.GameModeThreePointContest
//            || GameRules.instance.GameModeFourPointContest
//            || GameRules.instance.GameModeAllPointContest))
//        {
//            displayCurrentMarkerStats.text = "markers remaining : " + GameRules.instance.MarkersRemaining + "\n"
//                                             // + "current marker : " + positionMarkerId + "\n"
//                                             + "made : " + ShotMade + " / " + ShotAttempt + "\n"
//                                             + "remaining : " + (maxShotMade - ShotMade);
//        }
//        // if player not on marker or marker disabled (max shots made)
//        if ((!PlayerOnMarker || !markerEnabled)
//            && displayCurrentMarkerStats != null)//&& markerEnabled)
//        {
//            displayCurrentMarkerStats.text = "markers remaining : " + GameRules.instance.MarkersRemaining + "\n"
//                                             //   + "current marker : \n"
//                                             + "made : \n"
//                                             + "remaining : ";
//        }
//    }

//    // the shot type is set manually but this is a failsafe check that sets it automatically based 
//    // on distance from the rim
//    void setMarkerShotType()
//    {
//        // get distance from rim
//        basketBallTarget = basketBallState.BasketBallTarget;
//        distanceFromRim = Vector3.Distance(transform.position, basketBallTarget.transform.position);

//        //if (distanceFromRim > Constants.DISTANCE_3point)
//        //{
//        //    shotTypeThree = true;
//        //    shotTypeFour = false;
//        //    shotTypeSeven = false;
//        //}

//        //if (distanceFromRim > Constants.DISTANCE_4point)
//        //{
//        //    shotTypeThree = false;
//        //    shotTypeFour = true;
//        //    shotTypeSeven = false;
//        //}

//        //if (distanceFromRim > Constants.DISTANCE_7point)
//        //{
//        //    shotTypeThree = false;
//        //    shotTypeFour = false;
//        //    shotTypeSeven = true;
//        //}

//        if (distanceFromRim > Constants.DISTANCE_3point && distanceFromRim < Constants.DISTANCE_4point
//            && !shotTypeFour && !shotTypeSeven)
//        {
//            shotTypeThree = true;
//            shotTypeFour = false;
//            shotTypeSeven = false;
//            return;
//        }
//        else
//        {
//            shotTypeThree = false;
//        }

//        if (distanceFromRim > Constants.DISTANCE_4point && distanceFromRim < Constants.DISTANCE_7point
//            && !shotTypeThree && !shotTypeSeven)
//        {
//            shotTypeThree = false;
//            shotTypeFour = true;
//            shotTypeSeven = false;
//            return;
//        }
//        else
//        {
//            shotTypeFour = false;
//        }

//        if (distanceFromRim > Constants.DISTANCE_7point
//            && !shotTypeThree && !shotTypeFour)
//        {
//            shotTypeThree = false;
//            shotTypeFour = false;
//            shotTypeSeven = true;
//            return;
//        }
//        else
//        {
//            shotTypeSeven = false;
//        }
//    }

//    public int ShotMade
//    {
//        get => _shotMade;
//        set => _shotMade = value;
//    }

//    public int ShotAttempt
//    {
//        get => _shotAttempt;
//        set => _shotAttempt = value;
//    }

//    public bool PlayerOnMarker => _playerOnMarker;
//}


