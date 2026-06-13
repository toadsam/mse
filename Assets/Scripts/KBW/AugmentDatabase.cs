using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Stores all augment definitions and provides lookup/random draw helpers.
public class AugmentDatabase : MonoBehaviour
{
    // Augment assets assigned from the Unity Inspector.
    [SerializeField] private List<AugmentDefinition> augments = new();

    // Runtime cache for fast augment lookup by id.
    private Dictionary<int, AugmentDefinition> byId;

    // Builds the id lookup table when the database is loaded.
    private void Awake()
    {
        byId = augments.ToDictionary(a => a.id, a => a);
    }

    // Returns the augment definition that matches the given id.
    public AugmentDefinition GetById(int id)
    {
        if (byId == null || byId.Count == 0)
            Awake();

        byId.TryGetValue(id, out var result);
        return result;
    }

    // Draws unique random augments from the full pool.
    public List<AugmentDefinition> DrawRandomUnique(int count)
    {
        List<AugmentDefinition> pool = new List<AugmentDefinition>(augments);
        List<AugmentDefinition> result = new List<AugmentDefinition>();

        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    // Draws unique augments while avoiding ids that were already offered.
    public List<AugmentDefinition> DrawRandomUniqueExcluding(
    int count,
    ICollection<int> excludedIds
)
    {
        if (byId == null || byId.Count == 0)
            Awake();

        List<AugmentDefinition> pool = new List<AugmentDefinition>();

        foreach (AugmentDefinition augment in augments)
        {
            if (augment == null)
                continue;

            if (excludedIds != null && excludedIds.Contains(augment.id))
                continue;

            pool.Add(augment);
        }

        // Fallback for testing when the unique augment pool is too small.
        if (pool.Count < count)
        {
            Debug.LogWarning("[AugmentDatabase] Unique augment pool is too small. Allowing repeated offers for testing.");

            pool.Clear();

            foreach (AugmentDefinition augment in augments)
            {
                if (augment != null)
                    pool.Add(augment);
            }
        }

        List<AugmentDefinition> result = new List<AugmentDefinition>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
}