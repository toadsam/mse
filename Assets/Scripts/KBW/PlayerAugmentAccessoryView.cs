using System.Collections.Generic;
using UnityEngine;

public class PlayerAugmentAccessoryView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNetwork player;
    [SerializeField] private Transform visualRoot;

    [Header("Visual Prefabs")]
    [SerializeField] private GameObject shieldVisualPrefab;
    [SerializeField] private GameObject orbitMeleeVisualPrefab;

    [Header("Visual Settings")]
    [SerializeField] private float visualHeight = 1.1f;
    [SerializeField] private Vector3 shieldEulerOffset = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 meleeEulerOffset = new Vector3(0f, 0f, 90f);

    private readonly List<Transform> shieldInstances = new();
    private readonly List<Transform> meleeInstances = new();

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerNetwork>();

        if (visualRoot == null)
            visualRoot = transform;
    }

    private void Update()
    {
        if (player == null)
            return;

        UpdateShieldVisuals();
        UpdateOrbitMeleeVisuals();
    }

    private void UpdateShieldVisuals()
    {
        int count = Mathf.Max(0, player.OrbitShieldCount);
        EnsureInstanceCount(shieldInstances, count, shieldVisualPrefab, "OrbitShield");

        for (int i = 0; i < shieldInstances.Count; i++)
        {
            Transform instance = shieldInstances[i];

            if (instance == null)
                continue;

            if (i >= count)
            {
                instance.gameObject.SetActive(false);
                continue;
            }

            instance.gameObject.SetActive(true);

            Vector3 pos = player.GetOrbitAccessoryPosition(i, count, player.AccessoryRadius, visualHeight);
            Vector3 dir = pos - player.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
                dir = player.transform.forward;

            dir.Normalize();

            instance.position = pos;
            instance.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(shieldEulerOffset);
        }
    }

    private void UpdateOrbitMeleeVisuals()
    {
        int count = Mathf.Max(0, player.OrbitMeleeCount);
        EnsureInstanceCount(meleeInstances, count, orbitMeleeVisualPrefab, "OrbitMelee");

        for (int i = 0; i < meleeInstances.Count; i++)
        {
            Transform instance = meleeInstances[i];

            if (instance == null)
                continue;

            if (i >= count)
            {
                instance.gameObject.SetActive(false);
                continue;
            }

            instance.gameObject.SetActive(true);

            Vector3 pos = player.GetOrbitAccessoryPosition(i, count, player.AccessoryRadius, visualHeight);
            Vector3 dir = pos - player.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
                dir = player.transform.forward;

            dir.Normalize();

            instance.position = pos;
            instance.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(meleeEulerOffset);
        }
    }

    private void EnsureInstanceCount(
        List<Transform> list,
        int targetCount,
        GameObject prefab,
        string objectName
    )
    {
        if (prefab == null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    list[i].gameObject.SetActive(false);
            }

            return;
        }

        while (list.Count < targetCount)
        {
            GameObject obj = Instantiate(prefab, visualRoot);
            obj.name = $"{objectName}_{list.Count}";
            list.Add(obj.transform);
        }
    }
}