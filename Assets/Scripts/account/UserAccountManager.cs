using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserAccountManager : MonoBehaviour
{

    [SerializeField]
    private List<UserModel> userAccountData;

    private bool usersLoaded = false;
    const int guestUserid = 74;
    const string guestPassword = "guest";
    const string guestUsername = "guest";

    [SerializeField]
    GameObject localAccountPrefab;
    [SerializeField]
    GameObject localAccountPrefabSpawnLocation;
    [SerializeField]
    List<GameObject> localAccounsList;
    [SerializeField]
    string userNameSelected;
    [SerializeField]
    Text messageText;

    PlayerControls controls;
    public static UserAccountManager instance;

    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
        PlayerControlsProvider.EnableMenuMaps();
    }
    private void OnDisable()
    {
        PlayerControlsProvider.DisableMenuMaps();
    }

    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
        controls = PlayerControlsProvider.Controls;
        if (!SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_00_loading))
        {
            StartCoroutine(loadUserData());
        }
    }

    public void LoginButton()
    {
        UserModel user = new UserModel();
        GameOptions.userName = userNameSelected;

        if (usersLoaded)
        {
            user = userAccountData.Where(x => x.UserName == userNameSelected).SingleOrDefault();
            GameOptions.userid = user.Userid;
        }
        else
        {
            GameOptions.userid = guestUserid;
            GameOptions.userName = guestUsername;
            user.Userid = guestUserid;
            user.UserName = guestPassword;
            user.Password = guestPassword;
        }

        // if connected to internet
        if (UtilityFunctions.IsConnectedToInternet())
        {
            StartCoroutine(APIHelper.PostToken(user));
        }
        else
        {
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }
    }

    public void ContinueButton()
    {
        //GameOptions.userName = "";
        //GameOptions.userid = 0;
        //SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        UserModel user = new UserModel();
        GameOptions.userName = userNameSelected;

        GameOptions.userid = guestUserid;
        GameOptions.userName = guestUsername;
        user.Userid = guestUserid;
        user.UserName = guestPassword;
        user.Password = guestPassword;

        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        // if connected to internet
        //if (UtilityFunctions.IsConnectedToInternet())
        //{
        //    StartCoroutine(APIHelper.PostToken(user));
        //}
        //else
        //{
        //    SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        //}
    }

    public IEnumerator RemoveUserButton(string userName)
    {
        // set confirm button slected
        Button selectedButton = UnityEngine.Object.FindAnyObjectByType<ConfirmDialogue>().confirmButton;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);

        // wait for button press
        yield return new WaitUntil(() => DialogueManager.instance.ButtonPressed);
        // wait for confirm/cancel
        yield return new WaitUntil(() => DialogueManager.instance.PreviousDialog.result != DialogueManager.instance.PreviousDialog.NONE);
        // remove local user / reload scene
        if (DialogueManager.instance.PreviousDialog.result == DialogueManager.instance.PreviousDialog.YES)
        {
            DBHelper.instance.DatabaseLocked = true;
            DBHelper.instance.deleteLocalUser(userName);

            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_account_loginLocal);
        }
        // do nothing
        if (DialogueManager.instance.PreviousDialog.result == DialogueManager.instance.PreviousDialog.CANCEL)
        {
            EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        }
    }

    IEnumerator loadUserData()
    {
        yield return new WaitUntil(() => DBHelper.instance != null);
        yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

        try
        {
            DBHelper.instance.DatabaseLocked = true;
            // check if database is empty
            if (!DBHelper.instance.isTableEmpty(Constants.LOCAL_DATABASE_tableName_user))
            {
                // get local users data
                userAccountData = DBHelper.instance.getUserProfileStats();
                GameOptions.numOfLocalUsers = userAccountData.Count;

                if (userAccountData.Count > 0)
                {
                    usersLoaded = true;
                    if (messageText != null)
                    {
                        messageText.text = "select user to log in";
                    }
                }
            }
            else
            {
                usersLoaded = false;
                if (messageText != null)
                {
                    messageText.text = "no users found";
                }
            }
            DBHelper.instance.DatabaseLocked = false;
            StartCoroutine(CreateUserButtons());
        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
            usersLoaded = false;
            DBHelper.instance.DatabaseLocked = false;
            messageText.text = e.ToString();
            StartCoroutine(CreateUserButtons());
        }
        DBHelper.instance.DatabaseLocked = false;
    }
    IEnumerator CreateUserButtons()
    {
        yield return new WaitUntil(() => DBHelper.instance != null);
        yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

        int index = 0;
        if (usersLoaded)
        {
            foreach (UserModel u in userAccountData)
            {
                // instantiate a max of 5 rows
                if (index < 10)
                {
                    GameObject prefabClone =
                    Instantiate(localAccountPrefab, localAccountPrefabSpawnLocation.transform.position, Quaternion.identity);
                    // set parent to object with vertical layout
                    prefabClone.transform.SetParent(localAccountPrefabSpawnLocation.transform, false);
                    // add to list
                    localAccounsList.Add(prefabClone);
                    //set text
                    localAccounsList[index].GetComponentInChildren<Text>().text = u.UserName;
                }
                index++;
            }
        }
        else
        {
            UserModel u = new UserModel();
            u.UserName = "guest";
            u.Password = "guest";

            GameObject prefabClone =
                Instantiate(localAccountPrefab, localAccountPrefabSpawnLocation.transform.position, Quaternion.identity);
            // set parent to object with vertical layout
            prefabClone.transform.SetParent(localAccountPrefabSpawnLocation.transform, false);
            // add to list
            localAccounsList.Add(prefabClone);
            //set text
            localAccounsList[index].GetComponentInChildren<Text>().text = u.UserName;
        }
    }

    public List<UserModel> UserAccountData { get => userAccountData; }
    public bool UsersLoaded { get => usersLoaded; set => usersLoaded = value; }
    public static int GuestUserid => guestUserid;
    public static string GuestPassword => guestPassword;
}
