namespace Level5.BackendV2
{
    /// <summary>The value of an <see cref="ApiResponse{T}"/> for a call whose success body is empty
    /// (a 204 No Content), so those endpoints can still return <c>ApiResponse&lt;ApiVoid&gt;</c>
    /// instead of a bare bool that would carry no error detail on failure.</summary>
    public readonly struct ApiVoid
    {
        public static readonly ApiVoid Instance = default;
    }
}
