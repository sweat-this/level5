using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The existing-account login screen (<c>level_00_account_loginExisting</c>). AUD-092 Phase 5B: fields
/// are <see cref="TMP_InputField"/> (migrated from legacy <see cref="InputField"/>), and
/// <see cref="LoginButton"/> is resolved here rather than left to its own authored <c>onClick</c> -
/// <c>AccountManager</c> now code-owns Login the same way it already owned Check Username.
/// </summary>
public class AccountLoginUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_Text messageDisplay;
    [SerializeField] private Button checkUserNameButton;
    [SerializeField] private Button loginButton;

    public TMP_InputField UsernameInputField => usernameInputField;
    public TMP_InputField PasswordInputField => passwordInputField;
    public TMP_Text MessageDisplay => messageDisplay;
    public Button CheckUserNameButton => checkUserNameButton;
    public Button LoginButton => loginButton;

    public GameObject UserNameTargetObject => usernameInputField != null ? usernameInputField.gameObject : null;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (usernameInputField == null) missing.Add("AccountLoginUiObjects.usernameInputField");
        if (passwordInputField == null) missing.Add("AccountLoginUiObjects.passwordInputField");
        if (messageDisplay == null) missing.Add("AccountLoginUiObjects.messageDisplay");
        if (checkUserNameButton == null) missing.Add("AccountLoginUiObjects.checkUserNameButton");
        if (loginButton == null) missing.Add("AccountLoginUiObjects.loginButton");
        return missing.Count == before;
    }
}
