using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Level5.Core.Match;

public class PlayerAttackQueue : MonoBehaviour
{
    [SerializeField]
    bool attackSlotOpen;
    [SerializeField]
    bool attackQueueLocked = false;
    [SerializeField]
    int currentEnemiesQueued;
    [SerializeField]
    int maxEnemiesQueued;
    [SerializeField]
    GameObject[] attackPositions;
    [SerializeField]
    List<GameObject> bodyGuards = new List<GameObject>();
    [SerializeField]
    List<GameObject> enemiesQueued = new List<GameObject>();
    [SerializeField]
    bool bodyGuardEngaged;

    readonly Dictionary<GameObject, QueueEntry> entriesByAttacker = new Dictionary<GameObject, QueueEntry>();
    readonly List<QueueEntry> entries = new List<QueueEntry>();
    PlayerIdentifier playerIdentifier;

    class QueueEntry
    {
        public GameObject attacker;
        public ICombatAgent agent;
        public PlayerAttackPosition slot;
    }

    private void Awake()
    {
        playerIdentifier = GetComponent<PlayerIdentifier>();
    }

    private void Start()
    {
        maxEnemiesQueued = GetMaxEnemiesQueued();
        CacheAttackPositions();
        RefreshBodyGuards();
        UpdateQueueState();
    }

    private void LateUpdate()
    {
        CleanupStaleEntries();
        UpdateAttackPositionTransforms();
        UpdateSlotEngagements();
        UpdateQueueState();
    }

    private int GetMaxEnemiesQueued()
    {
        if (MatchRuntime.Rules.IsBattleRoyal)
        {
            return 20;
        }

        if (MatchRuntime.Rules.EnemiesOnly && MatchRuntime.Rules.Hardcore)
        {
            return 8;
        }

        if (!MatchRuntime.Rules.EnemiesOnly && MatchRuntime.Rules.Hardcore)
        {
            return 6;
        }

        return 4;
    }

    private void CacheAttackPositions()
    {
        PlayerAttackPosition[] childSlots = GetComponentsInChildren<PlayerAttackPosition>(true);
        if (childSlots.Length == 0)
        {
            childSlots = GameObject.FindGameObjectsWithTag("playerAttackQueuePosition")
                .Select(go => go.GetComponent<PlayerAttackPosition>())
                .Where(slot => slot != null)
                .ToArray();
        }

        attackPositions = new GameObject[childSlots.Length];
        for (int i = 0; i < childSlots.Length; i++)
        {
            childSlots[i].Initialize(this, i);
            attackPositions[i] = childSlots[i].gameObject;
        }
    }

    private void RefreshBodyGuards()
    {
        bodyGuards = GameObject.FindGameObjectsWithTag("bodyGuard").ToList();
        bodyGuards.RemoveAll(bodyGuard => bodyGuard == null);
    }

    public IEnumerator RequestAddToQueue(GameObject enemy)
    {
        TryAddToQueue(enemy);
        yield break;
    }

    public bool TryAddToQueue(GameObject enemy)
    {
        return TryReserve(enemy, out _);
    }

    public bool TryReserve(ICombatAgent agent, out CombatReservation reservation)
    {
        reservation = default;
        if (agent == null || !agent.CanAct)
        {
            return false;
        }

        return TryReserve(agent.CombatObject, agent, out reservation);
    }

    public bool TryReserve(GameObject attacker, out CombatReservation reservation)
    {
        return TryReserve(attacker, null, out reservation);
    }

    private bool TryReserve(GameObject attacker, ICombatAgent agent, out CombatReservation reservation)
    {
        reservation = default;
        if (attacker == null || attackQueueLocked)
        {
            return false;
        }

        CleanupStaleEntries();
        UpdateQueueState();

        agent = agent ?? attacker.GetComponent<ICombatAgent>();
        if (agent != null && !agent.CanAct)
        {
            return false;
        }

        if (entriesByAttacker.TryGetValue(attacker, out QueueEntry existingEntry))
        {
            ApplyReservationState(attacker, existingEntry.slot);
            reservation = CreateReservation(existingEntry);
            return true;
        }

        if (!attackSlotOpen)
        {
            return false;
        }

        if (!CanReserve(attacker))
        {
            return false;
        }

        PlayerAttackPosition slot = SelectAttackSlot(attacker);
        if (slot == null)
        {
            return false;
        }

        QueueEntry entry = new QueueEntry
        {
            attacker = attacker,
            agent = agent,
            slot = slot
        };

        entries.Add(entry);
        entriesByAttacker[attacker] = entry;
        if (!enemiesQueued.Contains(attacker))
        {
            enemiesQueued.Add(attacker);
        }

        ApplyReservationState(attacker, slot);
        UpdateSlotEngagements();
        UpdateQueueState();
        reservation = CreateReservation(entry);
        return true;
    }

    public IEnumerator removeEnemyFromAttackQueue(GameObject enemy, int attackPostionId)
    {
        RemoveFromQueue(enemy, attackPostionId);
        yield break;
    }

    public void removeEnemyFromQueue(GameObject enemy, int attackPostionId)
    {
        RemoveFromQueue(enemy, attackPostionId);
    }

    public bool RemoveFromQueue(GameObject attacker, int attackPositionId)
    {
        return ReleaseReservation(attacker, attackPositionId);
    }

    public bool ReleaseReservation(GameObject attacker)
    {
        return ReleaseReservation(attacker, -1);
    }

    public bool ReleaseReservation(ICombatAgent agent)
    {
        if (agent == null)
        {
            return false;
        }

        return ReleaseReservation(agent.CombatObject, -1);
    }

    private bool ReleaseReservation(GameObject attacker, int attackPositionId)
    {
        if (attacker == null)
        {
            CleanupStaleEntries();
            UpdateQueueState();
            return false;
        }

        bool removed = RemoveEntry(attacker);
        if (!removed)
        {
            ClearLegacySlot(attacker, attackPositionId);
        }

        enemiesQueued.Remove(attacker);
        ClearAttackerDetection(attacker);
        UpdateSlotEngagements();
        UpdateQueueState();
        return removed;
    }

    private bool CanReserve(GameObject attacker)
    {
        // AUD-005: an actor that already holds a slot cannot reserve another. This used to ask the
        // two concrete detection components in turn; ICombatDetection answers for any of them.
        ICombatDetection detection = attacker.GetComponent<ICombatDetection>();
        if (detection != null)
        {
            return !detection.Attacking;
        }

        // an actor with no detection component at all can still queue if it is a combat agent
        return attacker.GetComponent<ICombatAgent>() != null;
    }

    private CombatReservation CreateReservation(QueueEntry entry)
    {
        if (entry == null || entry.slot == null)
        {
            return default;
        }

        return new CombatReservation(entry.attacker, entry.slot.attackPositionId, entry.slot.transform);
    }

    private PlayerAttackPosition SelectAttackSlot(GameObject attacker)
    {
        PlayerAttackPosition bestSlot = null;
        int bestOccupancy = int.MaxValue;
        float bestDistance = float.MaxValue;
        bool allowSharedSlots = maxEnemiesQueued > attackPositions.Length || MatchRuntime.Rules.IsBattleRoyal;

        foreach (GameObject attackPositionObject in attackPositions)
        {
            if (attackPositionObject == null || !attackPositionObject.TryGetComponent(out PlayerAttackPosition slot))
            {
                continue;
            }

            int occupancy = GetSlotOccupancy(slot);
            if (!allowSharedSlots && occupancy > 0)
            {
                continue;
            }

            float distance = (slot.transform.position - attacker.transform.position).sqrMagnitude;
            if (occupancy < bestOccupancy || (occupancy == bestOccupancy && distance < bestDistance))
            {
                bestSlot = slot;
                bestOccupancy = occupancy;
                bestDistance = distance;
            }
        }

        return bestSlot;
    }

    private int GetSlotOccupancy(PlayerAttackPosition slot)
    {
        int occupancy = 0;
        foreach (QueueEntry entry in entries)
        {
            if (entry.slot == slot)
            {
                occupancy++;
            }
        }

        return occupancy;
    }

    private bool RemoveEntry(GameObject attacker)
    {
        if (!entriesByAttacker.TryGetValue(attacker, out QueueEntry entry))
        {
            return false;
        }

        entriesByAttacker.Remove(attacker);
        entries.Remove(entry);
        return true;
    }

    private void ClearLegacySlot(GameObject attacker, int attackPositionId)
    {
        if (attackPositionId < 0 || attackPositionId >= attackPositions.Length)
        {
            return;
        }

        GameObject attackPositionObject = attackPositions[attackPositionId];
        if (attackPositionObject != null && attackPositionObject.TryGetComponent(out PlayerAttackPosition slot))
        {
            slot.ClearOccupant(attacker);
        }
    }

    // AUD-005: these two used to name EnemyDetection and BodyGuardDetection explicitly, in four
    // near-identical blocks. Everything about them was the same except which component held the
    // "I can see my target" flag and what it was called. Going through ICombatDetection means a
    // new melee actor type joins by implementing the interface, not by editing the queue.
    private void ApplyReservationState(GameObject attacker, PlayerAttackPosition slot)
    {
        if (slot == null)
        {
            return;
        }

        SetAttackerDetection(attacker, true, slot.attackPositionId);
    }

    private void ClearAttackerDetection(GameObject attacker)
    {
        SetAttackerDetection(attacker, false, -1);
    }

    private void SetAttackerDetection(GameObject attacker, bool attacking, int attackPositionId)
    {
        if (attacker == null)
        {
            return;
        }

        // GetComponent, not TryGetComponent - this resolves an interface, which is the same
        // pattern CanReserve already uses for ICombatAgent
        ICombatDetection detection = attacker.GetComponent<ICombatDetection>();
        if (detection == null)
        {
            return;
        }

        detection.Attacking = attacking;
        detection.AttackPositionId = attackPositionId;
        detection.TargetSighted = attacking;
    }

    private void CleanupStaleEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            QueueEntry entry = entries[i];
            if (ShouldRemoveEntry(entry))
            {
                if (entry != null && entry.attacker != null)
                {
                    ClearAttackerDetection(entry.attacker);
                }

                entries.RemoveAt(i);
            }
        }

        entriesByAttacker.Clear();
        enemiesQueued.Clear();
        foreach (QueueEntry entry in entries)
        {
            entriesByAttacker[entry.attacker] = entry;
            enemiesQueued.Add(entry.attacker);
        }

        bodyGuards.RemoveAll(bodyGuard => bodyGuard == null);
    }

    private bool ShouldRemoveEntry(QueueEntry entry)
    {
        if (entry == null || entry.attacker == null)
        {
            return true;
        }

        if (!entry.attacker.activeInHierarchy)
        {
            return true;
        }

        if (entry.agent != null && !entry.agent.CanAct)
        {
            return true;
        }

        return entry.attacker.TryGetComponent(out IDamageable damageable) && damageable.IsDead;
    }

    private void UpdateAttackPositionTransforms()
    {
        Vector3 anchorPosition = GetQueueAnchorPosition();
        foreach (GameObject attackPositionObject in attackPositions)
        {
            if (attackPositionObject != null && attackPositionObject.TryGetComponent(out PlayerAttackPosition slot))
            {
                slot.UpdatePosition(anchorPosition);
            }
        }
    }

    private Vector3 GetQueueAnchorPosition()
    {
        if (playerIdentifier != null)
        {
            if (playerIdentifier.isCpu && playerIdentifier.autoPlayer != null)
            {
                return playerIdentifier.autoPlayer.transform.position;
            }

            if (!playerIdentifier.isCpu && playerIdentifier.player != null)
            {
                return playerIdentifier.player.transform.position;
            }
        }

        return transform.position;
    }

    private void UpdateSlotEngagements()
    {
        foreach (GameObject attackPositionObject in attackPositions)
        {
            if (attackPositionObject != null && attackPositionObject.TryGetComponent(out PlayerAttackPosition slot))
            {
                slot.ClearOccupant();
            }
        }

        foreach (QueueEntry entry in entries)
        {
            if (entry.slot != null)
            {
                entry.slot.SetOccupant(entry.attacker);
            }
        }
    }

    private void UpdateQueueState()
    {
        currentEnemiesQueued = entries.Count;
        attackSlotOpen = currentEnemiesQueued < maxEnemiesQueued && attackPositions.Length > 0;
    }

    /// <summary>
    /// The first enemy in queue order. No production caller left as of the Enemy/Bodyguard AI
    /// architecture work - BodyGuardController now scores threats via CombatTargetSelector
    /// instead of always fighting whichever enemy queued first. Kept for compatibility rather
    /// than removed outright; a new caller should use <see cref="EnemiesQueued"/> with
    /// CombatTargetSelector, not this, or "first queued" tactical priority comes back.
    /// </summary>
    public GameObject GetFirstQueuedEnemy()
    {
        CleanupStaleEntries();
        UpdateQueueState();
        return enemiesQueued.Count > 0 ? enemiesQueued[0] : null;
    }

    public bool HasQueuedEnemies()
    {
        CleanupStaleEntries();
        UpdateQueueState();
        return currentEnemiesQueued > 0;
    }

    /// <summary>
    /// The first registered bodyguard. No production caller left as of the Enemy/Bodyguard AI
    /// architecture work - EnemyController now picks the nearest valid bodyguard via
    /// CombatTargetSelector instead of always engaging index 0. Kept for compatibility; a new
    /// caller should use <see cref="BodyGuards"/> with CombatTargetSelector instead.
    /// </summary>
    public GameObject GetFirstBodyGuard()
    {
        bodyGuards.RemoveAll(bodyGuard => bodyGuard == null);
        return bodyGuards.Count > 0 ? bodyGuards[0] : null;
    }

    public Transform GetAttackPositionTransform(int attackPositionId)
    {
        if (attackPositionId < 0 || attackPositionId >= attackPositions.Length)
        {
            return null;
        }

        GameObject attackPosition = attackPositions[attackPositionId];
        return attackPosition != null ? attackPosition.transform : null;
    }

    public void RemoveBodyGuard(GameObject bodyGuard)
    {
        if (bodyGuard == null)
        {
            bodyGuards.RemoveAll(existingBodyGuard => existingBodyGuard == null);
            return;
        }

        bodyGuards.Remove(bodyGuard);
    }

    public void RegisterBodyGuard(GameObject bodyGuard)
    {
        if (bodyGuard == null)
        {
            return;
        }

        bodyGuards.RemoveAll(existingBodyGuard => existingBodyGuard == null);
        if (!bodyGuards.Contains(bodyGuard))
        {
            bodyGuards.Add(bodyGuard);
        }
    }

    public void UnregisterBodyGuard(GameObject bodyGuard)
    {
        RemoveBodyGuard(bodyGuard);
    }

    public int CurrentEnemiesQueued { get => currentEnemiesQueued; set => currentEnemiesQueued = Mathf.Clamp(value, 0, maxEnemiesQueued); }
    public bool LockAttackQueue { get => attackQueueLocked; set => attackQueueLocked = value; }
    public bool AttackSlotOpen { get => attackSlotOpen; set => attackSlotOpen = value; }
    public IReadOnlyList<GameObject> AttackPositions => attackPositions;
    public IReadOnlyList<GameObject> EnemiesQueued => enemiesQueued;
    public IReadOnlyList<GameObject> BodyGuards => bodyGuards;
    public bool BodyGuardEngaged { get => bodyGuardEngaged; set => bodyGuardEngaged = value; }
}
