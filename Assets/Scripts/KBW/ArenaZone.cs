using UnityEngine;

public class ArenaZone : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform player0Spawn;
    [SerializeField] private Transform player1Spawn;

    [Header("Boundary")]
    [SerializeField] private GameObject boundaryRoot;

    public Vector3 GetSpawnPosition(int slotIndex)
    {
        Transform spawn = slotIndex == 0 ? player0Spawn : player1Spawn;

        if (spawn == null)
        {
            Debug.LogError($"[ArenaZone] Spawn point is missing. Arena: {name}, Slot: {slotIndex}");
            return transform.position + Vector3.up;
        }

        return spawn.position;
    }

    public float GetSpawnYaw(int slotIndex)
    {
        Transform spawn = slotIndex == 0 ? player0Spawn : player1Spawn;

        if (spawn == null)
            return 0f;

        return spawn.eulerAngles.y;
    }

    public void SetActiveArena(bool active)
    {
        if (boundaryRoot != null)
            boundaryRoot.SetActive(active);
    }
}