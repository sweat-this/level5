using System.Collections;
using System.Collections.Generic;
using Level5.BackendV2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The Backend V2 online-account screen (<c>level_00_account_online</c>): register, sign in, see the
/// current signed-in identity, and sign out. A first-class player-facing surface for
/// <c>BackendV2SessionStore</c>, reachable from the Account hub without going through Multiplayer.
///
/// Deliberately separate from <see cref="AccountManager"/>'s local-profile create/login screens -
/// Backend V2 online identity and local profile identity (<c>GameOptions.userid/userName</c>,
/// <c>LocalAccountIdentity</c>, legacy <c>UserModel</c>/<c>APIHelper</c>) are independent, and this
/// controller never reads or writes any of the latter. See <see cref="OnlineAccountCoordinator"/> for
/// the actual register/login/logout/profile orchestration - this class only wires UI to it.
/// </summary>
public class OnlineAccountController : MonoBehaviour
{
    [SerializeField] private OnlineAccountUiObjects onlineUi;
    [SerializeField] private MenuFooterUiObjects footer;

    private readonly OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();

    Button accountFooterButton;
    bool initialized;

    void Awake()
    {
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();
    }

    void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        BackendV2SessionStore.Changed += OnSessionChanged;
        if (initialized)
        {
            RegisterButtonCallbacks();
            Render();
        }
    }

    void OnDisable()
    {
        BackendV2SessionStore.Changed -= OnSessionChanged;
        UnregisterButtonCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "OnlineAccountController is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        accountFooterButton = footer.AccountButton;

        RegisterButtonCallbacks();
        initialized = true;

        // Reflects the already-known auth state immediately - e.g. a restored session shows the
        // signed-in panel (with blank fields) right away rather than the signed-out sign-in/register
        // form for however long the profile fetch below takes.
        Render();
        StartCoroutine(EnterScreenCoroutine());
    }

    void Update()
    {
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    /// <summary>True once <see cref="onlineUi"/> and <see cref="footer"/> carry every reference this
    /// screen needs. Callable from editor tooling as a pure check - it only reads already-serialized
    /// references.</summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        int before = missing.Count;
        if (onlineUi == null)
        {
            missing.Add("OnlineAccountController.onlineUi");
            return false;
        }

        onlineUi.Validate(missing);

        if (footer == null)
        {
            missing.Add("OnlineAccountController.footer");
            return false;
        }

        footer.Validate(missing, (footer.AccountButton, "accountButton"));
        return missing.Count == before;
    }

    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(accountFooterButton, LoadAccountHub);
        UiSelectionAdapter.RegisterButton(onlineUi.SignInButton, OnSignInButtonClicked);
        UiSelectionAdapter.RegisterButton(onlineUi.RegisterButton, OnRegisterButtonClicked);
        UiSelectionAdapter.RegisterButton(onlineUi.SignOutButton, OnSignOutButtonClicked);
        UiSelectionAdapter.RegisterButton(onlineUi.RetryProfileButton, OnRetryProfileButtonClicked);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(accountFooterButton, LoadAccountHub);
        UiSelectionAdapter.UnregisterButton(onlineUi.SignInButton, OnSignInButtonClicked);
        UiSelectionAdapter.UnregisterButton(onlineUi.RegisterButton, OnRegisterButtonClicked);
        UiSelectionAdapter.UnregisterButton(onlineUi.SignOutButton, OnSignOutButtonClicked);
        UiSelectionAdapter.UnregisterButton(onlineUi.RetryProfileButton, OnRetryProfileButtonClicked);
    }

    /// <summary>Keeps the screen honest if the session changes from outside this screen's own
    /// button-driven flows (e.g. a background call elsewhere hits a definitive auth failure and the
    /// session gets cleared) - without this, a player idling on the signed-in panel could keep seeing
    /// a stale identity until they next tapped something.</summary>
    private void OnSessionChanged(BackendV2Session session)
    {
        if (initialized)
        {
            Render();
        }
    }

    private void LoadAccountHub()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account);
    }

    private void OnSignInButtonClicked()
    {
        StartCoroutine(SignInCoroutine());
    }

    private void OnRegisterButtonClicked()
    {
        StartCoroutine(RegisterCoroutine());
    }

    private void OnSignOutButtonClicked()
    {
        StartCoroutine(SignOutCoroutine());
    }

    private void OnRetryProfileButtonClicked()
    {
        StartCoroutine(RetryProfileCoroutine());
    }

    private IEnumerator EnterScreenCoroutine()
    {
        yield return coordinator.EnterScreen();
        Render();
    }

    private IEnumerator SignInCoroutine()
    {
        string username = onlineUi.SignInUsernameInputField.text;
        string password = onlineUi.SignInPasswordInputField.text;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            onlineUi.SignInError.text = "enter a username and password";
            yield break;
        }

        onlineUi.SignInError.text = "signing in...";
        string error = null;
        yield return coordinator.SignIn(username, password, e => error = e);
        onlineUi.SignInError.text = error ?? string.Empty;
        Render();
    }

    private IEnumerator RegisterCoroutine()
    {
        string username = onlineUi.RegisterUsernameInputField.text;
        string password = onlineUi.RegisterPasswordInputField.text;
        string displayName = onlineUi.RegisterDisplayNameInputField.text;
        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrEmpty(password)
            || string.IsNullOrWhiteSpace(displayName))
        {
            onlineUi.RegisterError.text = "username, password, and display name are required";
            yield break;
        }

        onlineUi.RegisterError.text = "creating account...";
        string error = null;
        yield return coordinator.Register(username, password, displayName, e => error = e);
        onlineUi.RegisterError.text = error ?? string.Empty;
        Render();
    }

    private IEnumerator SignOutCoroutine()
    {
        onlineUi.SignedInStatus.text = "signing out...";
        yield return coordinator.SignOut(e => { });
        Render();
    }

    private IEnumerator RetryProfileCoroutine()
    {
        onlineUi.SignedInStatus.text = "loading...";
        yield return coordinator.RefreshProfile();
        Render();
    }

    private void Render()
    {
        bool signedIn = coordinator.IsSignedIn;
        onlineUi.SignedOutPanel.SetActive(!signedIn);
        onlineUi.SignedInPanel.SetActive(signedIn);
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());

        if (!signedIn)
        {
            return;
        }

        if (coordinator.Profile != null)
        {
            onlineUi.DisplayNameText.text = coordinator.Profile.DisplayName;
            onlineUi.PlayerTagText.text = coordinator.Profile.Tag;
            onlineUi.SignedInStatus.text = string.Empty;
        }
        else
        {
            onlineUi.DisplayNameText.text = string.Empty;
            onlineUi.PlayerTagText.text = string.Empty;
            onlineUi.SignedInStatus.text = coordinator.ProfileError ?? string.Empty;
        }
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        if (coordinator.IsSignedIn)
        {
            return onlineUi.SignOutButton != null ? onlineUi.SignOutButton.gameObject : null;
        }

        return onlineUi.SignInUsernameInputField != null ? onlineUi.SignInUsernameInputField.gameObject : null;
    }
}
