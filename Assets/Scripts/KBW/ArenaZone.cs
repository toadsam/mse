using UnityEngine;

// Defines one playable arena area with spawn points and an active boundary.
public class ArenaZone : MonoBehaviour
{
    // Spawn transforms used by each player slot in this arena.
    [Header("Spawn Points")]
    [SerializeField] private Transform player0Spawn;
    [SerializeField] private Transform player1Spawn;

    // Boundary object that is enabled only for the selected arena.
    [Header("Boundary")]
    [SerializeField] private GameObject boundaryRoot;

    // Returns the spawn position for the requested player slot.
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

    // Returns the spawn yaw angle for the requested player slot.
    public float GetSpawnYaw(int slotIndex)
    {
        Transform spawn = slotIndex == 0 ? player0Spawn : player1Spawn;

        if (spawn == null)
            return 0f;

        return spawn.eulerAngles.y;
    }

    // Toggles the arena boundary when this zone becomes active or inactive.
    public void SetActiveArena(bool active)
    {
        if (boundaryRoot != null)
            boundaryRoot.SetActive(active);
    }
}