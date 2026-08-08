using System.Collections.Generic;
using Level5.Core.Match;

/// <summary>
/// Who is actually in the scene, once they have been spawned.
///
/// <see cref="PlayerRoster"/> says who should be playing; this says who is. Callers used to reach
/// for <c>GameLevelManager.Player1</c>..<c>Player4</c>, four separate fields that had to be kept in
/// step with a list of the same objects. There is one list here and the numbered accessors are
/// views onto it, so they cannot disagree.
/// </summary>
public sealed class PlayerRegistry
{
    private readonly List<PlayerIdentifier> participants = new List<PlayerIdentifier>();

    /// <summary>The participants in slot order. Read-only to callers; add through <see cref="Add"/>.</summary>
    public IReadOnlyList<PlayerIdentifier> Participants => participants;

    /// <summary>
    /// The same list, typed as <see cref="List{T}"/>, for the legacy callers that index and count
    /// it directly. Kept only while <c>GameLevelManager.players</c> is still a public list.
    /// </summary>
    public List<PlayerIdentifier> MutableParticipants => participants;

    public int Count => participants.Count;

    public void Clear()
    {
        participants.Clear();
    }

    public void Add(PlayerIdentifier participant)
    {
        if (participant != null)
        {
            participants.Add(participant);
        }
    }

    public PlayerIdentifier GetBySlot(int slotId)
    {
        return slotId >= 0 && slotId < participants.Count ? participants[slotId] : null;
    }

    /// <summary>The first non-CPU participant, which is the one every single-player HUD path means.</summary>
    public PlayerIdentifier PrimaryLocalHuman
    {
        get
        {
            foreach (PlayerIdentifier participant in participants)
            {
                if (participant != null && !participant.isCpu)
                {
                    return participant;
                }
            }

            return GetBySlot(0);
        }
    }
}
