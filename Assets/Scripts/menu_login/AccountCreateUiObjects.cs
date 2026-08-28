using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The create-account screen (<c>level_00_account_createNew</c>). Each input field's own submit
/// target is that same field's GameObject for password/first/last name (the pre-existing behaviour -
/// <c>AccountManager</c> resolved them by the identical name it used for the field itself, so
/// submitting one of those three fields reselects the field), and the check-email/check-username
/// button for the email/username fields respectively. Footer here is <c>account_menu</c> only,
/// sourced from <see cref="MenuFooterUiObjects.AccountButton"/> - the other six footer names are not
/// present on this scene. The "createUser" button is not resolved by name at all - it has its own
/// authored <c>onClick</c> straight to <see cref="AccountManager.createUser()"/> in the scene, so it
/// is out of scope for this migration (same shape as the login button on the login screen).
/// </summary>
public class AccountCreateUiObjects : MonoBehaviour
{
    [SerializeField] private InputField emailInputField;
    [SerializeField] private InputField usernameInputField;
    [SerializeField] private InputField passwordInputField;
    [SerializeField] private InputField firstNameInputField;
    [SerializeField] private InputField lastNameInputField;

    [SerializeField] private TMP_Text messageDisplay;

    [SerializeField] private Button checkEmailButton;
    [SerializeField] private Button checkUserNameButton;

    public InputField EmailInputField => emailInputField;
    public InputField UsernameInputField => usernameInputField;
    public InputField PasswordInputField => passwordInputField;
    public InputField FirstNameInputField => firstNameInputField;
    public InputField LastNameInputField => lastNameInputField;

    public TMP_Text MessageDisplay => messageDisplay;

    public Button CheckEmailButton => checkEmailButton;
    public Button CheckUserNameButton => checkUserNameButton;

    public GameObject EmailAddressTargetObject => emailInputField != null ? emailInputField.gameObject : null;
    public GameObject UserNameTargetObject => usernameInputField != null ? usernameInputField.gameObject : null;
    public GameObject PasswordTargetObject => passwordInputField != null ? passwordInputField.gameObject : null;
    public GameObject FirstNameTargetObject => firstNameInputField != null ? firstNameInputField.gameObject : null;
    public GameObject LastNameTargetObject => lastNameInputField != null ? lastNameInputField.gameObject : null;
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
        return missing.Count == before;
    }
}
