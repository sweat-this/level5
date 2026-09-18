using Level5.Core.Versus;

namespace Level5.BackendV2
{
    /// <summary>
    /// The explicit adapter between a Backend V2 player id (a <c>Guid</c>, authenticated identity)
    /// and the local, opaque-string <see cref="ParticipantId"/> the versus domain already uses.
    ///
    /// <c>ParticipantId</c> is deliberately opaque and was never tied to the legacy integer account
    /// id, so a remote participant slots into it without needing to touch legacy identity at all -
    /// this type exists only to make that one conversion explicit and in one place, rather than
    /// scattering <c>PlayerId.ToString()</c> calls through the correspondence code.
    /// </summary>
    public static class BackendV2ParticipantIdentity
    {
        /// <summary>The signed-in Backend V2 player, as a <see cref="ParticipantId"/>. Empty when
        /// there is no session.</summary>
        public static ParticipantId Current()
        {
            BackendV2Session session = BackendV2SessionStore.Current;
            return session == null ? ParticipantId.None : new ParticipantId(session.PlayerId.ToString());
        }
    }
}
