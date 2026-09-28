namespace Level5.Core
{
    /// <summary>
    /// The locally selected account identity - which account was picked, or the offline guest
    /// fallback. AUD-012 Phase 2b: extracted from <c>GameOptions.userid</c>/<c>GameOptions.userName</c>
    /// so this state has an owner outside <c>Assembly-CSharp</c> (<c>CharacterProgressAccountId</c>, in
    /// <c>Level5.Player</c>, needs to read it without depending on that assembly).
    /// <c>GameOptions.userid</c>/<c>userName</c> are now properties forwarding here, so every existing
    /// caller (login, account switching, high-score upload, ...) keeps working unchanged.
    ///
    /// A local selection only - it proves nothing about authentication. There is no local session
    /// concept any more (the V1 bearer-session machinery, including <c>APIHelper.HasSession</c>, was
    /// retired along with the rest of the V1 account/auth transport); the only remaining authenticated
    /// identity is <c>BackendV2SessionStore.IsAuthenticated</c>, which deliberately does not live here
    /// either - see docs/persistence-boundaries.md.
    /// </summary>
    public static class LocalAccountIdentity
    {
        public static int UserId { get; set; }

        public static string UserName { get; set; }
    }
}
