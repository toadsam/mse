using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AugmentDatabase : MonoBehaviour
{
    [SerializeField] private List<AugmentDefinition> augments = new();

    private Dictionary<int, AugmentDefinition> byId;

    private void Awake()
    {
        byId = augments.ToDictionary(a => a.id, a => a);
    }

    public AugmentDefinition GetById(int id)
    {
        if (byId == null || byId.Count == 0)
            Awake();

        byId.TryGetValue(id, out var result);
        return result;
    }

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

        // 개발 중 카드 수가 부족할 때만 중복 허용 fallback
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