using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The create-local-profile screen (<c>level_00_account_createNew</c>). Retired V1 registration's
/// email/password/first-name/last-name fields and its check-email/check-username preflight buttons -
/// a local profile only needs a name; <see cref="Assets.Scripts.database.DBHelper.CreateLocalProfile"/>
/// allocates the id and rejects a duplicate name deterministically, with no separate availability
/// check. <see cref="CreateAccountButton"/> is resolved here rather than left to its own authored
/// <c>onClick</c> - <c>AccountManager</c> code-owns Create Profile.
/// </summary>
public class AccountCreateUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameInputField;

    [SerializeField] private TMP_Text messageDisplay;

    [SerializeField] private Button createAccountButton;

    public TMP_InputField UsernameInputField => usernameInputField;

    public TMP_Text MessageDisplay => messageDisplay;

    public Button CreateAccountButton => createAccountButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (usernameInputField == null) missing.Add("AccountCreateUiObjects.usernameInputField");
        if (messageDisplay == null) missing.Add("AccountCreateUiObjects.messageDisplay");
        if (createAccountButton == null) missing.Add("AccountCreateUiObjects.createAccountButton");
        return missing.Count == before;
    }
}
