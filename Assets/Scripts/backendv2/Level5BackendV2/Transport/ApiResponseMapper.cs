namespace Level5.BackendV2
{
    /// <summary>
    /// Turns a <see cref="RawApiResponse"/> into a typed <see cref="ApiResponse{T}"/>.
    ///
    /// Pure and Unity-free on purpose: every status-code/body combination Backend V2 can send is
    /// exercised in EditMode tests by constructing a <see cref="RawApiResponse"/> directly, with no
    /// transport, no coroutine and no network involved.
    /// </summary>
    public static class ApiResponseMapper
    {
        /// <param name="requestRequiredAuth">
        /// Whether the request this response answers was sent with <c>RequiresAuth = true</c>. A 401
        /// on an authenticated request most likely means the access token expired and is worth one
        /// refresh+retry; a 401 on an unauthenticated call (login, refresh) means the credentials
        /// themselves were rejected and retrying with the same ones would only fail again.
        /// </param>
        public static ApiResponse<T> Map<T>(RawApiResponse raw, bool requestRequiredAuth)
        {
            if (raw.IsNetworkError)
            {
                return ApiResponse<T>.Fail(ApiErrorKind.Network, correlationId: raw.CorrelationId);
            }

            if (raw.TimedOut)
            {
                return ApiResponse<T>.Fail(ApiErrorKind.Timeout, correlationId: raw.CorrelationId);
            }

            if (raw.StatusCode >= 200 && raw.StatusCode < 300)
            {
                if (typeof(T) == typeof(ApiVoid))
                {
                    return ApiResponse<T>.Ok((T)(object)ApiVoid.Instance, raw.CorrelationId);
                }

                if (!BackendV2Json.TryDeserialize<T>(raw.Body, out T value))
                {
                    return ApiResponse<T>.Fail(
                        ApiErrorKind.MalformedResponse, ApiProblem.Unknown((int)raw.StatusCode), raw.CorrelationId);
                }

                return ApiResponse<T>.Ok(value, raw.CorrelationId);
            }

            ApiProblem problem = ParseProblem(raw);
            ApiErrorKind kind = Classify((int)raw.StatusCode, requestRequiredAuth);
            return ApiResponse<T>.Fail(kind, problem, raw.CorrelationId);
        }

        private static ApiProblem ParseProblem(RawApiResponse raw)
        {
            if (BackendV2Json.TryDeserialize<ProblemDetailsWireDto>(raw.Body, out ProblemDetailsWireDto dto)
                && dto != null)
            {
                int status = dto.Status != 0 ? dto.Status : (int)raw.StatusCode;
                return new ApiProblem(status, dto.Title, dto.Type, dto.Code, dto.TraceId);
            }

            return ApiProblem.Unknown((int)raw.StatusCode);
        }

        private static ApiErrorKind Classify(int status, bool requestRequiredAuth)
        {
            switch (status)
            {
                case 400:
                    return ApiErrorKind.Validation;
                case 401:
                    return requestRequiredAuth ? ApiErrorKind.Expired : ApiErrorKind.Unauthenticated;
                case 403:
                    return ApiErrorKind.Forbidden;
                case 404:
                    return ApiErrorKind.NotFound;
                case 409:
                    return ApiErrorKind.Conflict;
                case 429:
                    return ApiErrorKind.RateLimited;
                default:
                    return status >= 500 ? ApiErrorKind.ServerError : ApiErrorKind.Validation;
            }
        }
    }
}
