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
}