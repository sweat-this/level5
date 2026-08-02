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
        if (enemy == null)
        {
            return false;
        }

        CleanupStaleEntries();
        UpdateQueueState();

        if (entriesByAttacker.TryGetValue(enemy, out QueueEntry existingEntry))
        {
            ApplyEnemyDetection(enemy, existingEntry.slot);
            return true;
        }

        if (!attackSlotOpen)
        {
            return false;
        }

        EnemyDetection enemyDetection = enemy.GetComponent<EnemyDetection>();
        if (enemyDetection == null || enemyDetection.Attacking)
        {
            return false;
        }

        PlayerAttackPosition slot = SelectAttackSlot(enemy);
        if (slot == null)
        {
            return false;
        }

        QueueEntry entry = new QueueEntry
        {
            attacker = enemy,
            slot = slot
        };

        entries.Add(entry);
        entriesByAttacker[enemy] = entry;
        if (!enemiesQueued.Contains(enemy))
        {
            enemiesQueued.Add(enemy);
        }

        ApplyEnemyDetection(enemy, slot);
        UpdateSlotEngagements();
        UpdateQueueState();
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

    private void ApplyEnemyDetection(GameObject enemy, PlayerAttackPosition slot)
    {
        EnemyDetection enemyDetection = enemy.GetComponent<EnemyDetection>();
        if (enemyDetection == null || slot == null)
        {
            return;
        }

        enemyDetection.Attacking = true;
        enemyDetection.AttackPositionId = slot.attackPositionId;
        enemyDetection.PlayerSighted = true;
    }

    private void ClearAttackerDetection(GameObject attacker)
    {
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
            if (entries[i].attacker == null)
            {
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

    public int CurrentEnemiesQueued { get => currentEnemiesQueued; set => currentEnemiesQueued = Mathf.Clamp(value, 0, maxEnemiesQueued); }
    public bool LockAttackQueue { get => attackQueueLocked; set => attackQueueLocked = value; }
    public bool AttackSlotOpen { get => attackSlotOpen; set => attackSlotOpen = value; }
    public GameObject[] AttackPositions { get => attackPositions; set => attackPositions = value; }
    public List<GameObject> EnemiesQueued { get => enemiesQueued; set => enemiesQueued = value; }
    public List<GameObject> BodyGuards { get => bodyGuards; set => bodyGuards = value; }
    public bool BodyGuardEngaged { get => bodyGuardEngaged; set => bodyGuardEngaged = value; }
}
