using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Backend V2 online-account screen (<c>level_00_account_online</c>): register, sign in, see the
/// current signed-in identity, and sign out of a Backend V2 online account. Entirely separate from the
/// local-profile screens (<see cref="AccountCreateUiObjects"/>/<see cref="AccountLoginUiObjects"/>) -
/// see <see cref="OnlineAccountController"/> for why. Footer buttons live on
/// <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class OnlineAccountUiObjects : MonoBehaviour
{
    [SerializeField] private GameObject signedOutPanel;
    [SerializeField] private GameObject signedInPanel;

    [SerializeField] private TMP_InputField signInUsernameInputField;
    [SerializeField] private TMP_InputField signInPasswordInputField;
    [SerializeField] private Button signInButton;
    [SerializeField] private TMP_Text signInError;

    [SerializeField] private TMP_InputField registerUsernameInputField;
    [SerializeField] private TMP_InputField registerPasswordInputField;
    [SerializeField] private TMP_InputField registerDisplayNameInputField;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text registerError;

    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text playerTagText;
    [SerializeField] private Button signOutButton;
    [SerializeField] private TMP_Text signedInStatus;
    [SerializeField] private Button retryProfileButton;

    public GameObject SignedOutPanel => signedOutPanel;
    public GameObject SignedInPanel => signedInPanel;

    public TMP_InputField SignInUsernameInputField => signInUsernameInputField;
    public TMP_InputField SignInPasswordInputField => signInPasswordInputField;
    public Button SignInButton => signInButton;
    public TMP_Text SignInError => signInError;

    public TMP_InputField RegisterUsernameInputField => registerUsernameInputField;
    public TMP_InputField RegisterPasswordInputField => registerPasswordInputField;
    public TMP_InputField RegisterDisplayNameInputField => registerDisplayNameInputField;
    public Button RegisterButton => registerButton;
    public TMP_Text RegisterError => registerError;

    public TMP_Text DisplayNameText => displayNameText;
    public TMP_Text PlayerTagText => playerTagText;
    public Button SignOutButton => signOutButton;
    public TMP_Text SignedInStatus => signedInStatus;
    public Button RetryProfileButton => retryProfileButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (signedOutPanel == null) missing.Add("OnlineAccountUiObjects.signedOutPanel");
        if (signedInPanel == null) missing.Add("OnlineAccountUiObjects.signedInPanel");
        if (signInUsernameInputField == null) missing.Add("OnlineAccountUiObjects.signInUsernameInputField");
        if (signInPasswordInputField == null) missing.Add("OnlineAccountUiObjects.signInPasswordInputField");
        if (signInButton == null) missing.Add("OnlineAccountUiObjects.signInButton");
        if (signInError == null) missing.Add("OnlineAccountUiObjects.signInError");
        if (registerUsernameInputField == null) missing.Add("OnlineAccountUiObjects.registerUsernameInputField");
        if (registerPasswordInputField == null) missing.Add("OnlineAccountUiObjects.registerPasswordInputField");
        if (registerDisplayNameInputField == null) missing.Add("OnlineAccountUiObjects.registerDisplayNameInputField");
        if (registerButton == null) missing.Add("OnlineAccountUiObjects.registerButton");
        if (registerError == null) missing.Add("OnlineAccountUiObjects.registerError");
        if (displayNameText == null) missing.Add("OnlineAccountUiObjects.displayNameText");
        if (playerTagText == null) missing.Add("OnlineAccountUiObjects.playerTagText");
        if (signOutButton == null) missing.Add("OnlineAccountUiObjects.signOutButton");
        if (signedInStatus == null) missing.Add("OnlineAccountUiObjects.signedInStatus");
        if (retryProfileButton == null) missing.Add("OnlineAccountUiObjects.retryProfileButton");
        return missing.Count == before;
    }
}
