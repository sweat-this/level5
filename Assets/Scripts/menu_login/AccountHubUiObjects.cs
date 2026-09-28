using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The account hub screen (<c>level_00_account</c>): navigation to Create Local Profile, Local
/// Profile (select/login-local), and Online Account. The legacy "Login Existing" V1 password screen
/// is retired from production navigation - see <c>AccountManager</c>'s class doc comment. This scene
/// has no email/username/password fields at all - see the other <c>Account*UiObjects</c> types for
/// those. Footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class AccountHubUiObjects : MonoBehaviour
{
    [SerializeField] private Button createNewButton;
    [SerializeField] private Button loginLocalButton;
    [SerializeField] private Button onlineAccountButton;

    public Button CreateNewButton => createNewButton;
    public Button LoginLocalButton => loginLocalButton;
    public Button OnlineAccountButton => onlineAccountButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (createNewButton == null) missing.Add("AccountHubUiObjects.createNewButton");
        if (loginLocalButton == null) missing.Add("AccountHubUiObjects.loginLocalButton");
        if (onlineAccountButton == null) missing.Add("AccountHubUiObjects.onlineAccountButton");
        return missing.Count == before;
    }
}
