using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The existing-account login screen (<c>level_00_account_loginExisting</c>). The login button
/// itself is not resolved here - it is wired with an authored <c>onClick</c> straight to
/// <see cref="AccountManager.LoginUser()"/> in the scene, which is the pre-existing wiring and is not
/// part of this manager's name-lookup fallback. Footer here is <c>account_menu</c> only.
/// </summary>
public class AccountLoginUiObjects : MonoBehaviour
{
    [SerializeField] private InputField usernameInputField;
    [SerializeField] private InputField passwordInputField;
    [SerializeField] private TMP_Text messageDisplay;
    [SerializeField] private Button checkUserNameButton;

    public InputField UsernameInputField => usernameInputField;
    public InputField PasswordInputField => passwordInputField;
    public TMP_Text MessageDisplay => messageDisplay;
    public Button CheckUserNameButton => checkUserNameButton;

    public GameObject UserNameTargetObject => usernameInputField != null ? usernameInputField.gameObject : null;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (usernameInputField == null) missing.Add("AccountLoginUiObjects.usernameInputField");
        if (passwordInputField == null) missing.Add("AccountLoginUiObjects.passwordInputField");
        if (messageDisplay == null) missing.Add("AccountLoginUiObjects.messageDisplay");
        if (checkUserNameButton == null) missing.Add("AccountLoginUiObjects.checkUserNameButton");
        return missing.Count == before;
    }
}
