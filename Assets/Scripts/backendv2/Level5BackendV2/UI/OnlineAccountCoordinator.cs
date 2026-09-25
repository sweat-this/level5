using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// Orchestrates the online-account screen's register/sign-in/sign-out/self-profile flows against
    /// <see cref="BackendV2Runtime.Session"/>/<see cref="BackendV2Runtime.Players"/>. Same split as
    /// <see cref="FriendsCoordinator"/>/<see cref="ChallengeCoordinator"/>: this holds only view state
    /// and orchestration, no UI - <c>OnlineAccountController</c> is the MonoBehaviour that renders it.
    ///
    /// Never touches local-profile identity (<c>GameOptions.userid/userName</c>,
    /// <c>LocalAccountIdentity</c>, legacy <c>UserModel</c>/<c>APIHelper</c>) - this is the Backend V2
    /// online account layer only.
    ///
    /// Every public entry point (<see cref="EnterScreen"/>, <see cref="RefreshProfile"/>,
    /// <see cref="SignIn"/>, <see cref="Register"/>, <see cref="SignOut"/>) shares one
    /// <see cref="OperationInProgress"/> guard, released in a <c>finally</c> block the same way
    /// <c>AccountManager.CreateUserCoroutineGuarded</c>/<c>LoginUserCoroutineGuarded</c> already do -
    /// so a rapid repeated activation (e.g. double-tapping Retry) cannot start a second concurrent
    /// call, and an unexpected exception mid-flow cannot leave the guard stuck forever.
    /// </summary>
    public sealed class OnlineAccountCoordinator
    {
        public bool IsSignedIn => BackendV2SessionStore.IsAuthenticated;

        public bool OperationInProgress { get; private set; }

        public PlayerProfileResponseDto Profile { get; private set; }

        public string ProfileError { get; private set; }

        /// <summary>Call when the screen opens: if a session already exists (restored at app startup
        /// or from an earlier visit), fetches the self-profile so the signed-in view can render
        /// immediately - never starts a login, since a restored session must not require
        /// re-authentication.</summary>
        public IEnumerator EnterScreen()
        {
            Profile = null;
            ProfileError = null;

            if (!IsSignedIn || !TryBegin(null))
            {
                yield break;
            }

            try
            {
                yield return RefreshProfileCore();
            }
            finally
            {
                OperationInProgress = false;
            }
        }

        /// <summary>Fetches the current player's self-profile. A failure here is always treated as
        /// transient/recoverable - it never clears the session or the signed-in identity, only leaves
        /// <see cref="Profile"/> unset and <see cref="ProfileError"/> set so the UI can offer a retry.
        /// Guarded the same as every other entry point, so a rapid repeated Retry tap cannot start a
        /// second concurrent fetch that could race the first and overwrite it with a stale result.</summary>
        public IEnumerator RefreshProfile()
        {
            if (!TryBegin(null))
            {
                yield break;
            }

            try
            {
                yield return RefreshProfileCore();
            }
            finally
            {
                OperationInProgress = false;
            }
        }

        /// <summary>No-ops (reporting a friendly error) if a session already exists - switching
        /// accounts always requires an explicit <see cref="SignOut"/> first, never a silent
        /// replacement of one active session with another.</summary>
        public IEnumerator SignIn(string username, string password, Action<string> completed)
        {
            if (IsSignedIn)
            {
                completed?.Invoke("already signed in - sign out first");
                yield break;
            }

            if (!TryBegin(completed))
            {
                yield break;
            }

            try
            {
                ApiResponse<BackendV2Session> response = null;
                yield return BackendV2Runtime.Session.Login(username, password, r => response = r);
                yield return CompleteAuth(response, completed);
            }
            finally
            {
                OperationInProgress = false;
            }
        }

        /// <summary>Same account-switching guard as <see cref="SignIn"/>.</summary>
        public IEnumerator Register(string username, string password, string displayName, Action<string> completed)
        {
            if (IsSignedIn)
            {
                completed?.Invoke("already signed in - sign out first");
                yield break;
            }

            if (!TryBegin(completed))
            {
                yield break;
            }

            try
            {
                ApiResponse<BackendV2Session> response = null;
                yield return BackendV2Runtime.Session.Register(username, password, displayName, r => response = r);
                yield return CompleteAuth(response, completed);
            }
            finally
            {
                OperationInProgress = false;
            }
        }

        /// <summary>Always ends signed-out locally, matching <see cref="BackendV2SessionManager.Logout"/>'s
        /// own contract - a player must always be able to sign out, even offline.</summary>
        public IEnumerator SignOut(Action<string> completed)
        {
            if (!TryBegin(completed))
            {
                yield break;
            }

            try
            {
                ApiResponse<ApiVoid> response = null;
                yield return BackendV2Runtime.Session.Logout(r => response = r);

                Profile = null;
                ProfileError = null;
                completed?.Invoke(response != null && response.Success ? null : DescribeFailure(response));
            }
            finally
            {
                OperationInProgress = false;
            }
        }

        private bool TryBegin(Action<string> completed)
        {
            if (OperationInProgress)
            {
                completed?.Invoke("already in progress");
                return false;
            }

            OperationInProgress = true;
            return true;
        }

        /// <summary>The actual profile fetch, shared by <see cref="EnterScreen"/>/<see cref="RefreshProfile"/>
        /// (which each hold the guard themselves) and <see cref="CompleteAuth"/> (called while
        /// <see cref="SignIn"/>/<see cref="Register"/> already holds it) - never acquires
        /// <see cref="OperationInProgress"/> itself, so it never double-guards a caller that already
        /// holds it.</summary>
        private IEnumerator RefreshProfileCore()
        {
            ApiResponse<PlayerProfileResponseDto> response = null;
            yield return BackendV2Runtime.Players.GetMyProfile(r => response = r);

            if (response != null && response.Success)
            {
                Profile = response.Value;
                ProfileError = null;
            }
            else
            {
                ProfileError = DescribeFailure(response);
            }
        }

        private IEnumerator CompleteAuth(ApiResponse<BackendV2Session> response, Action<string> completed)
        {
            if (response != null && response.Success)
            {
                yield return RefreshProfileCore();
                completed?.Invoke(null);
            }
            else
            {
                completed?.Invoke(DescribeFailure(response));
            }
        }

        private static string DescribeFailure<T>(ApiResponse<T> response) => BackendV2ErrorMessages.Describe(response);
    }
}
