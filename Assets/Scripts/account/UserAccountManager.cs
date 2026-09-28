using Assets.Scripts.database;
using Level5.BackendV2;
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
    private const float DatabaseWaitTimeoutSeconds = 8f;

    [SerializeField]
    private List<UserModel> userAccountData;

    private bool usersLoaded = false;
    const int guestUserid = 74;
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

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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
        // Application-wide Backend V2 session restoration seam (issue: promote persisted-session
        // restoration to app startup). This is the earliest ordinary production composition point -
        // the build's first scene is level_00_account_loginLocal - so a Backend V2 session persisted
        // from a prior run is available to any later feature (MatchResult submission, leaderboard
        // reads) without requiring the player to open Multiplayer first. Idempotent and local-only:
        // performs zero network requests and is independent of local-account/guest/database state.
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();

        instance = this;
        controls = PlayerControlsProvider.Controls;
        if (!SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_00_loading))
        {
            StartCoroutine(loadUserData());
        }
    }

    public void LoginButton()
    {
        UserModel user = null;

        if (usersLoaded && !string.IsNullOrWhiteSpace(userNameSelected))
        {
            user = userAccountData.Where(x => x.UserName == userNameSelected).SingleOrDefault();
        }

        if (user != null)
        {
            // A local profile is a save/profile selector, not a security principal - selecting one
            // is the whole operation, with no password screen and no network call.
            GameOptions.userName = user.UserName;
            GameOptions.userid = user.Userid;
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
            return;
        }

        ContinueButton();
    }

    public void ContinueButton()
    {
        // Fully local: no PostToken call, no ClearSession call. Backend V2's session (if any) is
        // untouched - only the local profile selection changes.
        GameOptions.userid = guestUserid;
        GameOptions.userName = guestUsername;
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
    }

    public IEnumerator RemoveUserButton(string userName)
    {
        if (DialogueManager.instance == null)
        {
            yield break;
        }

        // set confirm button slected
        ConfirmDialogue confirmDialogue = UnityEngine.Object.FindAnyObjectByType<ConfirmDialogue>();
        if (confirmDialogue == null || confirmDialogue.confirmButton == null)
        {
            yield break;
        }

        Button selectedButton = confirmDialogue.confirmButton;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }

        // wait for button press
        yield return new WaitUntil(() => DialogueManager.instance != null && DialogueManager.instance.ButtonPressed);
        // wait for confirm/cancel
        yield return new WaitUntil(() => DialogueManager.instance != null
            && DialogueManager.instance.LastDialogResult != DialogueManager.DialogNone);

        if (DialogueManager.instance == null)
        {
            yield break;
        }

        int dialogResult = DialogueManager.instance.LastDialogResult;
        // remove local user / reload scene
        if (dialogResult == DialogueManager.DialogYes)
        {
            if (DBHelper.instance == null)
            {
                yield break;
            }

            // Resolve the exact selected profile from the already-loaded local-profile list rather
            // than trusting the raw row text - this also deterministically rejects an unknown target
            // and the generated guest row (guest is a fallback scope, never a persisted User row, so
            // it is never routed into profile deletion).
            UserModel target = ResolveDeletionTarget(userName);
            if (target == null)
            {
                SetMessage("That profile could not be found.");
                yield break;
            }

            // DBHelper owns DatabaseLocked for the whole deletion - this method must never pre-set it
            // (doing so used to make DBHelper's own deletion guard return immediately without ever
            // deleting anything or releasing the lock). The deletion result is used directly; there is
            // no need to poll WaitForDatabase to infer whether it succeeded.
            bool deleted = DBHelper.instance.DeleteLocalProfile(target, out string error);
            if (!deleted)
            {
                SetMessage(string.IsNullOrEmpty(error) ? "The local account could not be removed." : error);
                yield break;
            }

            // SQLite is authoritative and has already committed the deletion at this point - a
            // partial filesystem cleanup failure only logs a warning, it never re-creates the User
            // row or blocks the reload below.
            CleanUpAccountFiles(target);

            if (MatchesCurrentLocalIdentity(target, GameOptions.userid, GameOptions.userName))
            {
                // Clear the local selection back to the same "nothing selected" state
                // CharacterProgressAccountId.Resolve already treats as the guest fallback scope.
                // Backend V2's session (BackendV2SessionStore) is untouched - local profile deletion
                // is unrelated to online account authentication.
                GameOptions.userid = 0;
                GameOptions.userName = null;
            }

            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_account_loginLocal);
        }
        // do nothing
        if (dialogResult == DialogueManager.DialogCancel)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
            }
        }
    }

    IEnumerator loadUserData()
    {
        bool databaseReady = false;
        yield return WaitForDatabase(value => databaseReady = value);
        if (!databaseReady)
        {
            usersLoaded = false;
            SetMessage("Local accounts are unavailable. Continue as guest or retry.");
            yield break;
        }

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
            if (messageText != null)
            {
                messageText.text = e.ToString();
            }

            StartCoroutine(CreateUserButtons());
        }
        DBHelper.instance.DatabaseLocked = false;
    }
    IEnumerator CreateUserButtons()
    {
        bool databaseReady = false;
        yield return WaitForDatabase(value => databaseReady = value);
        if (!databaseReady)
        {
            SetMessage("Local accounts are unavailable.");
            yield break;
        }

        if (localAccountPrefab == null || localAccountPrefabSpawnLocation == null || localAccounsList == null)
        {
            SetMessage("The local account screen is missing required UI references.");
            yield break;
        }

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

    /// <summary>
    /// Resolves the raw selected row text to the exact <see cref="UserModel"/> it names in the
    /// already-loaded local-profile list. Returns null for an unknown target and, deterministically,
    /// for the generated guest row - "clear guest save" is explicitly out of scope for profile
    /// deletion (see docs/persistence-boundaries.md). The guest row is identified by the reserved
    /// scope itself (<see cref="guestUserid"/>, 74) rather than by display name, so a real,
    /// differently-scoped profile that also happens to be named "guest" remains deletable; the
    /// generated guest row's <see cref="UserModel.Userid"/> is never positive, so it never reaches
    /// this list to begin with (see <see cref="usersLoaded"/>/<see cref="CreateUserButtons"/>).
    /// </summary>
    private UserModel ResolveDeletionTarget(string userName)
    {
        if (!usersLoaded || userAccountData == null || string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        UserModel candidate = userAccountData.SingleOrDefault(x => x.UserName == userName);
        if (candidate == null || candidate.Userid == guestUserid)
        {
            return null;
        }

        return candidate;
    }

    /// <summary>
    /// Cleans up the deleted account's local projection/recovery files after its SQLite rows have
    /// already committed as deleted. Narrowly delegates filename ownership to each store rather than
    /// reconstructing their paths here.
    /// </summary>
    private static void CleanUpAccountFiles(UserModel profile)
    {
        string accountId = CharacterProgressAccountId.Resolve(profile.Userid, profile.UserName);

        bool allRemoved = true;
        allRemoved &= CharacterProgressStore.DeleteAccountFiles(accountId);
        allRemoved &= PendingProgressionStore.DeleteAccountFiles(accountId);
        allRemoved &= ProgressionResultStore.DeleteAccountFiles(accountId);

        if (!allRemoved)
        {
            Debug.LogWarning(
                "Deleted local profile '" + profile.UserName
                + "' from SQLite, but some account-scoped local files could not be fully removed.");
        }
    }

    /// <summary>
    /// Pure decision extracted from the deletion coroutine so it has a directly testable seam (the
    /// coroutine itself needs a live DialogueManager/EventSystem to drive): whether the just-deleted
    /// profile is the one currently selected via <see cref="GameOptions"/>/<see cref="Level5.Core.LocalAccountIdentity"/>,
    /// in which case the local selection must be cleared. Internal (not private) for EditMode tests.
    /// </summary>
    internal static bool MatchesCurrentLocalIdentity(UserModel profile, int currentUserId, string currentUserName)
    {
        return profile != null && currentUserId == profile.Userid && currentUserName == profile.UserName;
    }

    private static bool IsDatabaseUnlocked()
    {
        return DBHelper.instance != null && !DBHelper.instance.DatabaseLocked;
    }

    private static IEnumerator WaitForDatabase(Action<bool> completed)
    {
        float deadline = Time.realtimeSinceStartup + DatabaseWaitTimeoutSeconds;
        while (!IsDatabaseUnlocked() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        completed?.Invoke(IsDatabaseUnlocked());
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public List<UserModel> UserAccountData { get => userAccountData; }
    public bool UsersLoaded { get => usersLoaded; set => usersLoaded = value; }
    public static int GuestUserid => guestUserid;
    public static string GuestUsername => guestUsername;
}
