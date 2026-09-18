namespace Level5.BackendV2
{
    /// <summary>Turns a failed <see cref="ApiResponse{T}"/> into a short diagnostic string, shared by
    /// the correspondence UI coordinators - and by <c>CorrespondenceScreenController</c>, which lives
    /// in the default assembly alongside <c>VersusLauncher</c>, hence public rather than internal -
    /// so failure messages are described consistently instead of each one re-deriving the same
    /// three-way (network/server/validation vs auth vs conflict) distinction the issue calls for at
    /// the message level.</summary>
    public static class BackendV2ErrorMessages
    {
        public static string Describe<T>(ApiResponse<T> response)
        {
            if (response == null)
            {
                return "no response";
            }

            string code = response.Problem?.Code;
            return code != null
                ? $"{response.ErrorKind} ({code})"
                : response.ErrorKind?.ToString() ?? "unknown error";
        }
    }
}
