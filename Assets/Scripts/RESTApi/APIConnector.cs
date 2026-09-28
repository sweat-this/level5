
using Assets.Scripts.database;
using UnityEngine;


public class APIConnector : MonoBehaviour
{
    // V1 account registration (APIHelper.UserNameExists/PostUser) was retired along with the rest of
    // the legacy account/auth transport. This component stays - it is attached via
    // Assets/Resources/Prefabs/api/restapi.prefab in every menu scene's PrefabInstance, and removing
    // the script would leave a "missing script" reference in each of them - but CreateNewUser has no
    // remaining callers (confirmed by repo-wide search) and nothing left for it to do.
    public void CreateNewUser(UserModel user)
    {
        Debug.LogWarning("APIConnector.CreateNewUser is retired; local profiles are created via DBHelper.CreateLocalProfile instead.");
    }
}

