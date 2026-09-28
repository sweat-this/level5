using Assets.Scripts.database;
using Assets.Scripts.Models;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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

    // V1 account/auth transport (PostUser, PostToken, UserExists/UserNameExists/EmailExists,
    // GetUserByUserName, and the bearer-session machinery that backed them: bearerToken, HasSession,
    // BearerToken, ClearSession) was retired here. Local profiles are created and selected entirely
    // locally now (see DBHelper.CreateLocalProfile); Backend V2 (Level5.BackendV2, its own
    // IApiTransport/BackendV2SessionStore) is the sole online authentication system and was never
    // built on this class. What remains are anonymous V1 utility services this issue explicitly left
    // in place: user reports, server messages, and the latest-build-version check.
    public static class APIHelper
    {
        private const int RequestTimeoutSeconds = 10;
        private const float LockTimeoutSeconds = 12f;

        private static object activeRequestOwner;

        public static bool ApiLocked => activeRequestOwner != null;

        public static IEnumerator PutCharacterProfileStats(List<CharacterProfile> characters)
        {
            // The server currently exposes no character-profile batch endpoint.
            yield break;
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
                result => response = result);

            completed?.Invoke(response.Success
                ? ApiResult<bool>.Ok(true, response.StatusCode)
                : ApiResult<bool>.Fail(response.Error, response.StatusCode));
        }

        public static IEnumerator GetLatestBuildVersion(Action<ApiResult<string>> completed)
        {
            yield return GetText(Constants.API_ADDRESS_DEV_publicApplicationVersionCurrent, completed);
        }

        public static IEnumerator GetServerMessages(Action<ApiResult<List<ServerMessageModel>>> completed)
        {
            yield return GetJson(Constants.API_ADDRESS_DEV_publicServerMessages, completed);
        }

        private static IEnumerator GetJson<T>(string url, Action<ApiResult<T>> completed)
        {
            ApiResult<string> response = null;
            yield return GetText(url, result => response = result);
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

        private static IEnumerator GetText(string url, Action<ApiResult<string>> completed)
        {
            yield return SendJson(url, UnityWebRequest.kHttpVerbGET, null, completed);
        }

        private static IEnumerator SendJson(
            string url,
            string method,
            string json,
            Action<ApiResult<string>> completed)
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

                yield return request.SendWebRequest();
                long statusCode = request.responseCode;
                string responseText = request.downloadHandler?.text ?? string.Empty;
                bool successful = request.result == UnityWebRequest.Result.Success
                    && statusCode >= 200
                    && statusCode < 300;

                finalResult = successful
                    ? ApiResult<string>.Ok(responseText, statusCode)
                    : ApiResult<string>.Fail(GetRequestError(request, statusCode), statusCode);
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

    }
}
