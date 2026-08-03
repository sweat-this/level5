using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        if (GameOptions.battleRoyalEnabled)
        {
            return 20;
        }

        if (GameOptions.EnemiesOnlyEnabled && GameOptions.hardcoreModeEnabled)
        {
            return 8;
        }

        if (!GameOptions.EnemiesOnlyEnabled && GameOptions.hardcoreModeEnabled)
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
        if (attacker.TryGetComponent(out EnemyDetection enemyDetection))
        {
            return !enemyDetection.Attacking;
        }

        if (attacker.TryGetComponent(out BodyGuardDetection bodyGuardDetection))
        {
            return !bodyGuardDetection.Attacking;
        }

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
        bool allowSharedSlots = maxEnemiesQueued > attackPositions.Length || GameOptions.battleRoyalEnabled;

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

    private void ApplyReservationState(GameObject attacker, PlayerAttackPosition slot)
    {
        if (attacker == null || slot == null)
        {
            return;
        }

        if (attacker.TryGetComponent(out EnemyDetection enemyDetection))
        {
            enemyDetection.Attacking = true;
            enemyDetection.AttackPositionId = slot.attackPositionId;
            enemyDetection.PlayerSighted = true;
        }

        if (attacker.TryGetComponent(out BodyGuardDetection bodyGuardDetection))
        {
            bodyGuardDetection.Attacking = true;
            bodyGuardDetection.AttackPositionId = slot.attackPositionId;
            bodyGuardDetection.EnemySighted = true;
        }
    }

    private void ClearAttackerDetection(GameObject attacker)
    {
        if (attacker == null)
        {
            return;
        }

        if (attacker.TryGetComponent(out EnemyDetection enemyDetection))
        {
            enemyDetection.Attacking = false;
            enemyDetection.AttackPositionId = -1;
            enemyDetection.PlayerSighted = false;
        }

        if (attacker.TryGetComponent(out BodyGuardDetection bodyGuardDetection))
        {
            bodyGuardDetection.Attacking = false;
            bodyGuardDetection.AttackPositionId = -1;
            bodyGuardDetection.EnemySighted = false;
        }
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
