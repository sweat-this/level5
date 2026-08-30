using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The create-account screen (<c>level_00_account_createNew</c>). AUD-092 Phase 5B: fields are
/// <see cref="TMP_InputField"/> (migrated from legacy <see cref="InputField"/>), and
/// <see cref="CreateAccountButton"/> is resolved here rather than left to its own authored
/// <c>onClick</c> - <c>AccountManager</c> now code-owns Create Account the same way it already owned
/// Check Email/Check Username.
/// </summary>
public class AccountCreateUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField firstNameInputField;
    [SerializeField] private TMP_InputField lastNameInputField;

    [SerializeField] private TMP_Text messageDisplay;

    [SerializeField] private Button checkEmailButton;
    [SerializeField] private Button checkUserNameButton;
    [SerializeField] private Button createAccountButton;

    public TMP_InputField EmailInputField => emailInputField;
    public TMP_InputField UsernameInputField => usernameInputField;
    public TMP_InputField PasswordInputField => passwordInputField;
    public TMP_InputField FirstNameInputField => firstNameInputField;
    public TMP_InputField LastNameInputField => lastNameInputField;

    public TMP_Text MessageDisplay => messageDisplay;

    public Button CheckEmailButton => checkEmailButton;
    public Button CheckUserNameButton => checkUserNameButton;
    public Button CreateAccountButton => createAccountButton;

    public GameObject EmailAddressTargetObject => emailInputField != null ? emailInputField.gameObject : null;
    public GameObject UserNameTargetObject => usernameInputField != null ? usernameInputField.gameObject : null;
    public GameObject CheckEmailButtonObject => checkEmailButton != null ? checkEmailButton.gameObject : null;
    public GameObject CheckUserNameButtonObject => checkUserNameButton != null ? checkUserNameButton.gameObject : null;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (emailInputField == null) missing.Add("AccountCreateUiObjects.emailInputField");
        if (usernameInputField == null) missing.Add("AccountCreateUiObjects.usernameInputField");
        if (passwordInputField == null) missing.Add("AccountCreateUiObjects.passwordInputField");
        if (firstNameInputField == null) missing.Add("AccountCreateUiObjects.firstNameInputField");
        if (lastNameInputField == null) missing.Add("AccountCreateUiObjects.lastNameInputField");
        if (messageDisplay == null) missing.Add("AccountCreateUiObjects.messageDisplay");
        if (checkEmailButton == null) missing.Add("AccountCreateUiObjects.checkEmailButton");
        if (checkUserNameButton == null) missing.Add("AccountCreateUiObjects.checkUserNameButton");
        if (createAccountButton == null) missing.Add("AccountCreateUiObjects.createAccountButton");
        return missing.Count == before;
    }
}
