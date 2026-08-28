using Assets.Scripts.database;
using Assets.Scripts.Models;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.restapi
{
    public sealed class ApiResult<T>
    {
        private ApiResult(bool success, long statusCode, T value, string error)
        {
            Success = success;
            StatusCode = statusCode;
            Value = value;
            Error = error;
        }

        public bool Success { get; }
        public long StatusCode { get; }
        public T Value { get; }
        public string Error { get; }

        public static ApiResult<T> Ok(T value, long statusCode)
        {
            return new ApiResult<T>(true, statusCode, value, string.Empty);
        }

        public static ApiResult<T> Fail(string error, long statusCode = 0)
        {
            return new ApiResult<T>(false, statusCode, default(T), error);
        }
    }

    public static class APIHelper
    {
        private const int RequestTimeoutSeconds = 10;
        private const float LockTimeoutSeconds = 12f;

        private static object activeRequestOwner;
        private static string bearerToken;

        public static string BearerToken => bearerToken;
        public static bool ApiLocked => activeRequestOwner != null;

        /// <summary>
        /// True only when a token has actually been obtained from the server.
        ///
        /// This is the test for "may we call an authenticated endpoint". `GameOptions.userid` is
        /// NOT that test - it is a local identity that gets set when a user picks an account, and
        /// when an offline guest falls back, neither of which proves a session. Gating uploads on
        /// the id meant posting scores with no Authorization header at all.
        /// </summary>
        public static bool HasSession => !string.IsNullOrEmpty(bearerToken);

        public static void ClearSession()
        {
            bearerToken = null;
            GameOptions.userName = string.Empty;
            GameOptions.userid = 0;
        }

        public static IEnumerator PostHighscore(HighScoreModel score, Action<ApiResult<bool>> completed = null)
        {
            if (score == null)
            {
                completed?.Invoke(ApiResult<bool>.Fail("No score was provided."));
                yield break;
            }

            // enforced here as well as at the call sites, so a future caller cannot post a score
            // with no Authorization header. treated exactly like a failed submission: the row stays
            // marked unsubmitted and is retried once a session exists.
            if (!HasSession)
            {
                if (DBHelper.instance != null)
                {
                    yield return SetScoreSubmittedWhenAvailable(score.Scoreid, false);
                }

                completed?.Invoke(ApiResult<bool>.Fail("Sign in to submit scores."));
                yield break;
            }

            ApiResult<string> response = null;
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicApiHighScores,
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(score),
                true,
                result => response = result);

            bool accepted = response.Success || response.StatusCode == 409;
            if (DBHelper.instance != null)
            {
                yield return SetScoreSubmittedWhenAvailable(score.Scoreid, accepted);
            }

            completed?.Invoke(accepted
                ? ApiResult<bool>.Ok(true, response.StatusCode)
                : ApiResult<bool>.Fail(response.Error, response.StatusCode));
        }

        public static IEnumerator PutCharacterProfileStats(List<CharacterProfile> characters)
        {
            // The server currently exposes no character-profile batch endpoint.
            yield break;
        }

        public static IEnumerator PostUnsubmittedHighscores(
            List<HighScoreModel> highscores,
            Action<ApiResult<int>> completed = null)
        {
            if (highscores == null || highscores.Count == 0)
            {
                completed?.Invoke(ApiResult<int>.Ok(0, 204));
                yield break;
            }

            // the stamping below claims an identity, so it must not happen without a session
            if (!HasSession)
            {
                completed?.Invoke(ApiResult<int>.Fail("Sign in to submit scores."));
                yield break;
            }

            foreach (HighScoreModel score in highscores)
            {
                score.Userid = GameOptions.userid;
                score.UserName = GameOptions.userName;
            }

            ApiResult<string> response = null;
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicApiHighScoresUnsubmitted,
                UnityWebRequest.kHttpVerbPOST,
                JsonConvert.SerializeObject(highscores),
                true,
                result => response = result);

            if (response.Success || response.StatusCode == 409)
            {
                if (DBHelper.instance != null)
                {
                    foreach (HighScoreModel score in highscores)
                    {
                        yield return SetScoreSubmittedWhenAvailable(score.Scoreid, true);
                    }
                }

                completed?.Invoke(ApiResult<int>.Ok(highscores.Count, response.StatusCode));
                yield break;
            }

            completed?.Invoke(ApiResult<int>.Fail(response.Error, response.StatusCode));
        }

        public static IEnumerator PutHighscore(HighScoreModel score, Action<ApiResult<bool>> completed = null)
        {
            if (score == null)
            {
                completed?.Invoke(ApiResult<bool>.Fail("No score was provided."));
                yield break;
            }

            ApiResult<string> response = null;
            string scoreId = UnityWebRequest.EscapeURL(score.Scoreid ?? string.Empty);
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicApiHighScores + scoreId,
                UnityWebRequest.kHttpVerbPUT,
                JsonUtility.ToJson(score),
                true,
                result => response = result);

            completed?.Invoke(response.Success
                ? ApiResult<bool>.Ok(true, response.StatusCode)
                : ApiResult<bool>.Fail(response.Error, response.StatusCode));
        }

        public static IEnumerator GetHighscoreByScoreid(
            string scoreId,
            Action<ApiResult<List<HighScoreModel>>> completed)
        {
            string url = Constants.API_ADDRESS_DEV_publicApiHighScoresByScoreid
                + UnityWebRequest.EscapeURL(scoreId ?? string.Empty);
            yield return GetJson(url, true, completed);
        }

        public static IEnumerator GetHighscoreByModeid(
            int modeId,
            int hardcore,
            int traffic,
            int enemies,
            int sniper,
            int page,
            int results,
            Action<ApiResult<List<StatsTableHighScoreRow>>> completed)
        {
            if (modeId > 19 && modeId < 23)
            {
                enemies = 1;
            }

            string url;
            if (hardcore == 0 && traffic == 0 && enemies == 0 && sniper == 0)
            {
                url = Constants.API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayAll + modeId
                    + "?page=" + page
                    + "&results=" + results;
            }
            else
            {
                url = Constants.API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayFiltered + modeId
                    + "?hardcore=" + hardcore
                    + "&traffic=" + traffic
                    + "&enemies=" + enemies
                    + "&sniper=" + sniper
                    + "&page=" + page
                    + "&results=" + results;
            }

            yield return GetJson(url, true, completed);
        }

        public static IEnumerator GetHighscoreCountByModeid(
            int modeId,
            int hardcore,
            int traffic,
            int enemies,
            int sniper,
            Action<ApiResult<int>> completed)
        {
            if (modeId > 19 && modeId < 23)
            {
                enemies = 1;
            }

            string url = Constants.API_ADDRESS_DEV_publicApiHighScoresCountByModeid + modeId
                + "?hardcore=" + hardcore
                + "&traffic=" + traffic
                + "&enemies=" + enemies
                + "&sniper=" + sniper;
            yield return GetJson(url, true, completed);
        }

        public static IEnumerator PostUser(UserModel user, Action<ApiResult<UserModel>> completed = null)
        {
            if (user == null)
            {
                completed?.Invoke(ApiResult<UserModel>.Fail("No account data was provided."));
                yield break;
            }

            ApiResult<string> response = null;
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicApiUsers,
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(user),
                false,
                result => response = result);

            if (!response.Success)
            {
                completed?.Invoke(ApiResult<UserModel>.Fail(response.Error, response.StatusCode));
                yield break;
            }

            try
            {
                UserModel created = JsonConvert.DeserializeObject<UserModel>(response.Value);
                completed?.Invoke(ApiResult<UserModel>.Ok(created, response.StatusCode));
            }
            catch (Exception exception)
            {
                completed?.Invoke(ApiResult<UserModel>.Fail("The account response was invalid: " + exception.Message));
            }
        }

        public static IEnumerator UserExists(string username, Action<ApiResult<bool>> completed)
        {
            yield return UserNameExists(username, completed);
        }

        public static IEnumerator ScoreIdExists(string scoreId, Action<ApiResult<bool>> completed)
        {
            string url = Constants.API_ADDRESS_DEV_publicApiHighScoresByScoreid
                + UnityWebRequest.EscapeURL(scoreId ?? string.Empty);
            yield return ResourceExists(url, false, completed);
        }

        public static IEnumerator UserNameExists(string username, Action<ApiResult<bool>> completed)
        {
            string url = Constants.API_ADDRESS_DEV_publicApiUsersByUserName
                + UnityWebRequest.EscapeURL(username ?? string.Empty);
            yield return ResourceExists(url, false, completed);
        }

        public static IEnumerator EmailExists(string email, Action<ApiResult<bool>> completed)
        {
            string url = Constants.API_ADDRESS_DEV_publicApiUsersByEmail
                + UnityWebRequest.EscapeURL(email ?? string.Empty);
            yield return ResourceExists(url, false, completed);
        }

        public static IEnumerator GetUserByUserName(string username, Action<ApiResult<UserModel>> completed)
        {
            string url = Constants.API_ADDRESS_DEV_publicApiUsersByUserName
                + UnityWebRequest.EscapeURL(username ?? string.Empty);
            yield return GetJson(url, false, completed);
        }

        /// <summary>
        /// AUD-092 Phase 4B: no longer takes a UI widget to write status text into - it used to accept
        /// the caller's <c>InputField</c> and set its <c>text</c> directly (<c>SetInputMessage</c>),
        /// which meant this networking helper owned a piece of Credits' UI presentation and would have
        /// needed re-coupling to <c>TMP_InputField</c> to keep working through this migration. The
        /// <paramref name="completed"/> callback's <see cref="ApiResult{T}"/> already carries
        /// success/failure and the server's error message; <c>CreditsManager</c> renders that into its
        /// own field, so this stays free of any concrete UI type.
        /// </summary>
        public static IEnumerator PostReport(
            UserReportModel userReport,
            Action<ApiResult<bool>> completed = null)
        {
            if (userReport == null)
            {
                completed?.Invoke(ApiResult<bool>.Fail("No report was provided."));
                yield break;
            }

            userReport.UserId = string.IsNullOrEmpty(GameOptions.userName) ? 999 : GameOptions.userid;
            userReport.UserName = string.IsNullOrEmpty(GameOptions.userName) ? "not logged in" : GameOptions.userName;
            userReport.Os = SystemInfo.operatingSystem;
            userReport.Device = SystemInfo.deviceModel;
            userReport.DeviceName = SystemInfo.deviceModel;
            userReport.Version = Application.version;
            userReport.IpAddress = string.Empty;

            ApiResult<string> response = null;
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicUserReport,
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(userReport),
                false,
                result => response = result);

            completed?.Invoke(response.Success
                ? ApiResult<bool>.Ok(true, response.StatusCode)
                : ApiResult<bool>.Fail(response.Error, response.StatusCode));
        }

        public static IEnumerator PostToken(
            UserModel user,
            Action<ApiResult<string>> completed = null,
            bool loadSceneOnSuccess = true)
        {
            ClearSession();
            if (user == null || string.IsNullOrWhiteSpace(user.UserName) || string.IsNullOrEmpty(user.Password))
            {
                completed?.Invoke(ApiResult<string>.Fail("Enter a username and password."));
                yield break;
            }

            ApiResult<string> response = null;
            yield return SendJson(
                Constants.API_ADDRESS_DEV_publicApiToken,
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(user),
                false,
                result => response = result);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Value))
            {
                string error = response.StatusCode == 400 || response.StatusCode == 401
                    ? "Invalid username or password."
                    : response.Error;
                completed?.Invoke(ApiResult<string>.Fail(error, response.StatusCode));
                yield break;
            }

            // The token stays here and nowhere else. It used to be copied into a public static on
            // GameOptions that nothing ever read - a live credential parked in global state where
            // any script could pick it up, for no benefit. HasSession is what callers actually want.
            bearerToken = response.Value.Trim().Trim('"');
            GameOptions.userName = user.UserName;
            GameOptions.userid = user.Userid;
            ApiResult<string> success = ApiResult<string>.Ok(bearerToken, response.StatusCode);
            completed?.Invoke(success);

            if (loadSceneOnSuccess)
            {
                SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
            }
        }

        public static IEnumerator GetLatestBuildVersion(Action<ApiResult<string>> completed)
        {
            yield return GetText(Constants.API_ADDRESS_DEV_publicApplicationVersionCurrent, true, completed);
        }

        public static IEnumerator GetServerMessages(Action<ApiResult<List<ServerMessageModel>>> completed)
        {
            yield return GetJson(Constants.API_ADDRESS_DEV_publicServerMessages, false, completed);
        }

        private static IEnumerator ResourceExists(string url, bool authenticated, Action<ApiResult<bool>> completed)
        {
            ApiResult<string> response = null;
            yield return GetText(url, authenticated, result => response = result, allowNotFound: true);

            if (response.StatusCode == 404)
            {
                completed?.Invoke(ApiResult<bool>.Ok(false, response.StatusCode));
                yield break;
            }

            completed?.Invoke(response.Success
                ? ApiResult<bool>.Ok(true, response.StatusCode)
                : ApiResult<bool>.Fail(response.Error, response.StatusCode));
        }

        private static IEnumerator GetJson<T>(string url, bool authenticated, Action<ApiResult<T>> completed)
        {
            ApiResult<string> response = null;
            yield return GetText(url, authenticated, result => response = result);
            if (!response.Success)
            {
                completed?.Invoke(ApiResult<T>.Fail(response.Error, response.StatusCode));
                yield break;
            }

            try
            {
                T value = JsonConvert.DeserializeObject<T>(response.Value);
                completed?.Invoke(ApiResult<T>.Ok(value, response.StatusCode));
            }
            catch (Exception exception)
            {
                completed?.Invoke(ApiResult<T>.Fail("The server returned invalid data: " + exception.Message));
            }
        }

        private static IEnumerator GetText(
            string url,
            bool authenticated,
            Action<ApiResult<string>> completed,
            bool allowNotFound = false)
        {
            yield return SendJson(url, UnityWebRequest.kHttpVerbGET, null, authenticated, completed, allowNotFound);
        }

        private static IEnumerator SendJson(
            string url,
            string method,
            string json,
            bool authenticated,
            Action<ApiResult<string>> completed,
            bool allowNotFound = false)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri requestUri)
                || (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
            {
                completed?.Invoke(ApiResult<string>.Fail("The network service address is invalid."));
                yield break;
            }

            object requestOwner = new object();
            float lockDeadline = Time.realtimeSinceStartup + LockTimeoutSeconds;
            while (activeRequestOwner != null && Time.realtimeSinceStartup < lockDeadline)
            {
                yield return null;
            }

            if (activeRequestOwner != null)
            {
                completed?.Invoke(ApiResult<string>.Fail("The network service is busy. Try again."));
                yield break;
            }

            activeRequestOwner = requestOwner;
            ApiResult<string> finalResult = null;
            UnityWebRequest request = new UnityWebRequest(requestUri, method);
            try
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = RequestTimeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");

                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }

                if (authenticated && !string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                }

                yield return request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseText = request.downloadHandler?.text ?? string.Empty;
                bool successful = request.result == UnityWebRequest.Result.Success
                    && statusCode >= 200
                    && statusCode < 300;

                if (successful)
                {
                    finalResult = ApiResult<string>.Ok(responseText, statusCode);
                }
                else if (allowNotFound && statusCode == 404)
                {
                    finalResult = ApiResult<string>.Fail("Not found.", statusCode);
                }
                else
                {
                    finalResult = ApiResult<string>.Fail(GetRequestError(request, statusCode), statusCode);
                }
            }
            finally
            {
                request.Dispose();
                if (ReferenceEquals(activeRequestOwner, requestOwner))
                {
                    activeRequestOwner = null;
                }
            }

            completed?.Invoke(finalResult ?? ApiResult<string>.Fail("The request did not complete."));
        }

        private static string GetRequestError(UnityWebRequest request, long statusCode)
        {
            if (statusCode == 400 || statusCode == 401)
            {
                return "The request was rejected.";
            }

            if (statusCode == 403)
            {
                return "This account is not authorized for that action.";
            }

            if (statusCode == 404)
            {
                return "The requested resource was not found.";
            }

            if (statusCode >= 500)
            {
                return "The server is temporarily unavailable.";
            }

            return request.result == UnityWebRequest.Result.ConnectionError
                ? "Could not connect to the server."
                : "The request failed. Try again.";
        }

        private static IEnumerator SetScoreSubmittedWhenAvailable(string scoreId, bool submitted)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (DBHelper.instance != null
                && DBHelper.instance.DatabaseLocked
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (DBHelper.instance == null || DBHelper.instance.DatabaseLocked)
            {
                Debug.LogWarning("Could not update the local submission state for score " + scoreId + ".");
                yield break;
            }

            DBHelper.instance.setGameScoreSubmitted(scoreId, submitted);
        }
    }
}
