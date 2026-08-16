
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using UnityEngine;


public class APIConnector : MonoBehaviour
{
    public void CreateNewUser(UserModel user)
    {
        if (!UtilityFunctions.IsValidEmail(user.Email))
        {
            Debug.LogWarning("Account creation rejected because the email address is invalid.");
            return;
        }

        StartCoroutine(CreateNewUserCoroutine(user));
    }

    private System.Collections.IEnumerator CreateNewUserCoroutine(UserModel user)
    {
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        ApiResult<bool> existsResult = null;
        yield return APIHelper.UserNameExists(user.UserName, result => existsResult = result);
        if (existsResult == null || !existsResult.Success)
        {
            Debug.LogWarning(existsResult != null ? existsResult.Error : "Could not check the username.");
            yield break;
        }

        if (existsResult.Value)
        {
            Debug.LogWarning("Account creation rejected because the username already exists.");
            yield break;
        }

        ApiResult<UserModel> createResult = null;
        yield return APIHelper.PostUser(user, result => createResult = result);
        if (createResult == null || !createResult.Success)
        {
            Debug.LogWarning(createResult != null ? createResult.Error : "Could not create the account.");
        }
    }
}


