/// <summary>
/// Basketball-domain contract for resolving the exact colliding participant's
/// <see cref="BasketBallState"/> from wherever a marker trigger's collider hierarchy carries
/// participant identity.
///
/// AUD-010 Phase 1c: <see cref="BasketBallShotMarker"/>'s trigger resolution
/// (<c>ResolveParticipantState</c>) queries this instead of walking straight to the actor-side
/// <c>PlayerIdentifier</c>, so the marker itself no longer references that concrete player type at
/// all. <c>PlayerIdentifier</c> implements this over its existing <c>basketball</c>/
/// <c>autoBasketball</c> references - no participant state is duplicated.
/// </summary>
public interface IBasketballParticipantStateProvider
{
    /// <summary>
    /// Resolves this participant's <see cref="BasketBallState"/> for the given marker trigger role -
    /// the human (<paramref name="cpuRoute"/> == false) or CPU (<paramref name="cpuRoute"/> == true)
    /// basketball this identifier owns. <paramref name="cpuRoute"/> mirrors the exact trigger tag the
    /// marker already branched on (<c>playerHitbox</c> vs <c>autoPlayerHitbox</c>) - implementers must
    /// not cross-check it against their own role. Returns false, with <paramref name="state"/> left
    /// null, if the selected basketball reference or its <see cref="BasketBallState"/> is missing.
    /// </summary>
    bool TryGetBasketballState(bool cpuRoute, out BasketBallState state);
}
