using UnityEngine;

public class PlayerAttackPosition : MonoBehaviour
{
    public bool engaged;
    public GameObject enemyEngaged;
    public int attackPositionId;
    public Vector3 position;

    public void Initialize(PlayerAttackQueue owner, int slotId)
    {
        attackPositionId = slotId;
    }

    public void UpdatePosition(Vector3 playerPosition)
    {
        Vector3 offset = GetOffsetForSlot(attackPositionId);
        transform.position = new Vector3(playerPosition.x + offset.x, playerPosition.y + offset.y, playerPosition.z + offset.z);
        position = transform.position;
    }

    public void SetOccupant(GameObject enemy)
    {
        if (engaged)
        {
            return;
        }

        engaged = enemy != null;
        enemyEngaged = enemy;
    }

    public void ClearOccupant(GameObject enemy = null)
    {
        if (enemy != null && enemyEngaged != enemy)
        {
            return;
        }

        engaged = false;
        enemyEngaged = null;
    }

    private Vector3 GetOffsetForSlot(int slotId)
    {
        switch (slotId)
        {
            case 0:
                return new Vector3(-0.6f, 0, -0.25f);
            case 1:
                return new Vector3(-0.6f, 0, 0.25f);
            case 2:
                return new Vector3(0.6f, 0, -0.25f);
            case 3:
                return new Vector3(0.6f, 0, 0.25f);
            default:
                return GetSharedSlotOffset(slotId);
        }
    }

    private Vector3 GetSharedSlotOffset(int slotId)
    {
        float angle = slotId * 137.5f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * 0.8f, 0, Mathf.Sin(angle) * 0.35f);
    }
}
