using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// Fusion NetworkObject¿ë Object Pool.
public class PooledNetworkObjectProvider : Fusion.Behaviour, INetworkObjectProvider
{
    [Header("Pool Settings")]
    [SerializeField] private bool delayIfSceneManagerIsBusy = true;

    [SerializeField] private int maxPoolCountPerPrefab = 64;

    private readonly Dictionary<NetworkPrefabId, Queue<NetworkObject>> freeObjects = new();

    public void SetMaxPoolCount(int count)
    {
        maxPoolCountPerPrefab = count;
    }

    public NetworkObjectAcquireResult AcquirePrefabInstance(
        NetworkRunner runner,
        in NetworkPrefabAcquireContext context,
        out NetworkObject instance)
    {
        instance = null;

        if (delayIfSceneManagerIsBusy &&
            runner.SceneManager != null &&
            runner.SceneManager.IsBusy)
        {
            return NetworkObjectAcquireResult.Retry;
        }

        NetworkObject prefab;

        try
        {
            prefab = runner.Prefabs.Load(
                context.PrefabId,
                isSynchronous: context.IsSynchronous
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"[PooledNetworkObjectProvider] Failed to load prefab. {e}");
            return NetworkObjectAcquireResult.Failed;
        }

        if (prefab == null)
            return NetworkObjectAcquireResult.Retry;

        instance = GetOrCreateInstance(prefab, context.PrefabId);

        if (instance == null)
            return NetworkObjectAcquireResult.Failed;

        if (context.DontDestroyOnLoad)
            runner.MakeDontDestroyOnLoad(instance.gameObject);
        else
            runner.MoveToRunnerScene(instance.gameObject);

        runner.Prefabs.AddInstance(context.PrefabId);

        return NetworkObjectAcquireResult.Success;
    }

    public void ReleaseInstance(
        NetworkRunner runner,
        in NetworkObjectReleaseContext context)
    {
        NetworkObject instance = context.Object;

        if (instance == null)
            return;

        if (!context.IsBeingDestroyed && context.TypeId.IsPrefab)
        {
            ReturnInstanceToPool(context.TypeId.AsPrefabId, instance);
        }
        else
        {
            Destroy(instance.gameObject);
        }

        if (context.TypeId.IsPrefab)
            runner.Prefabs.RemoveInstance(context.TypeId.AsPrefabId);
    }

    private NetworkObject GetOrCreateInstance(NetworkObject prefab, NetworkPrefabId prefabId)
    {
        if (!freeObjects.TryGetValue(prefabId, out Queue<NetworkObject> queue))
        {
            queue = new Queue<NetworkObject>();
            freeObjects.Add(prefabId, queue);
        }

        while (queue.Count > 0)
        {
            NetworkObject pooledObject = queue.Dequeue();

            if (pooledObject == null)
                continue;

            pooledObject.gameObject.SetActive(true);
            return pooledObject;
        }

        NetworkObject newObject = Instantiate(prefab);
        newObject.gameObject.name = $"{prefab.name}_Pooled";

        return newObject;
    }

    private void ReturnInstanceToPool(NetworkPrefabId prefabId, NetworkObject instance)
    {
        if (!freeObjects.TryGetValue(prefabId, out Queue<NetworkObject> queue))
        {
            Destroy(instance.gameObject);
            return;
        }

        if (maxPoolCountPerPrefab > 0 && queue.Count >= maxPoolCountPerPrefab)
        {
            Destroy(instance.gameObject);
            return;
        }

        instance.gameObject.SetActive(false);
        queue.Enqueue(instance);
    }

    public NetworkPrefabId GetPrefabId(NetworkRunner runner, NetworkObjectGuid prefabGuid)
    {
        if (runner == null)
            return default;

        return runner.Prefabs.GetId(prefabGuid);
    }
}