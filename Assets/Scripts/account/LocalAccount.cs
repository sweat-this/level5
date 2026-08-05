using Assets.Scripts.database;
using Assets.Scripts.restapi;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LocalAccount : MonoBehaviour
{
    [SerializeField]
    string userNameSelected;

    private void Update()
    {
        if (TryGetSelectedUserName(out string selectedName))
        {
            userNameSelected = selectedName;
        }
    }

    private bool TryGetSelectedUserName(out string selectedName)
    {
        selectedName = null;

        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        Transform selectedTransform = EventSystem.current.currentSelectedGameObject.transform;
        Transform userRow = selectedTransform.parent;
        if (userRow == null || userRow.childCount == 0)
        {
            return false;
        }

        Text userNameText = userRow.GetChild(0).GetComponent<Text>();
        if (userNameText == null || string.IsNullOrWhiteSpace(userNameText.text))
        {
            return false;
        }

        selectedName = userNameText.text;
        return true;
    }

    // OnClick UI
    public void LoginButton()
    {
        if (UserAccountManager.instance == null)
        {
            LoginAsGuest();
            return;
        }

        if (UserAccountManager.instance.UsersLoaded && !string.IsNullOrWhiteSpace(userNameSelected))
        {
            UserModel user = UserAccountManager.instance.UserAccountData
                .SingleOrDefault(x => x.UserName == userNameSelected);
            if (user == null)
            {
                LoginAsGuest();
                return;
            }

            GameOptions.userName = user.UserName;
            GameOptions.userid = user.Userid;
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_account_loginExisting);
        }
        else
        {
            LoginAsGuest();
        }
    }

    private void LoginAsGuest()
    {
        UserModel user = new UserModel
        {
            Userid = UserAccountManager.GuestUserid,
            UserName = UserAccountManager.GuestUsername,
            Password = UserAccountManager.GuestPassword
        };

        StartCoroutine(LoginAsGuestCoroutine(user));
    }

    private System.Collections.IEnumerator LoginAsGuestCoroutine(UserModel user)
    {
        ApiResult<string> result = null;
        yield return APIHelper.PostToken(user, value => result = value, false);
        if (!result.Success)
        {
            APIHelper.ClearSession();
            GameOptions.userName = user.UserName;
            GameOptions.userid = user.Userid;
        }

        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
    }

    // OnClick UI
    public void RemoveUserButton()
    {
        if (string.IsNullOrWhiteSpace(userNameSelected) || UserAccountManager.instance == null)
        {
            return;
        }

        if (DialogueManager.instance == null || DialogueManager.instance.Coroutine != null)
        {
            return;
        }

        // bring up dialogue
        if (DialogueManager.instance.Coroutine == null)
        {
            DialogueManager.instance.Coroutine = StartCoroutine(DialogueManager.instance.ShowConfirmationDialog());
        }
        // start coroutine to remove locally
        StartCoroutine(UserAccountManager.instance.RemoveUserButton(userNameSelected));
    }
}
