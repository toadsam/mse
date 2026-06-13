using System;
using System.Collections.Generic;
using UnityEngine;

// file: Assets/Scripts/JJH/Backend/BackendDataService.cs
// Singleton service that caches public backend data such as leaderboard and augments.
public class BackendDataService : MonoBehaviour
{
    public static BackendDataService Instance { get; private set; }

    public event Action<List<LeaderboardEntry>> LeaderboardLoaded;
    public event Action<List<AugmentResponse>> AugmentsLoaded;
    public event Action<string> LeaderboardFailed;
    public event Action<string> AugmentsFailed;

    public List<LeaderboardEntry> LastLeaderboard { get; private set; } = new List<LeaderboardEntry>();
    public List<AugmentResponse> LastAugments { get; private set; } = new List<AugmentResponse>();

    // Auto-creates a persistent singleton before scene code starts requesting backend data.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject obj = new GameObject("BackendDataService");
        DontDestroyOnLoad(obj);
        obj.AddComponent<BackendDataService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Requests the latest leaderboard and caches the returned snapshot for reuse.
    public void LoadLeaderboard()
    {
        if (BackendApiClient.Instance == null)
        {
            LeaderboardFailed?.Invoke("BackendApiClient가 아직 준비되지 않았어.");
            return;
        }

        BackendApiClient.Instance.GetLeaderboard(entries =>
        {
            LastLeaderboard = entries ?? new List<LeaderboardEntry>();
            LeaderboardLoaded?.Invoke(LastLeaderboard);
        }, error => LeaderboardFailed?.Invoke(error));
    }

    // Requests the augment catalog and caches the returned list for UI consumers.
    public void LoadAugments()
    {
        if (BackendApiClient.Instance == null)
        {
            AugmentsFailed?.Invoke("BackendApiClient가 아직 준비되지 않았어.");
            return;
        }

        BackendApiClient.Instance.GetAugments(augments =>
        {
            LastAugments = augments ?? new List<AugmentResponse>();
            AugmentsLoaded?.Invoke(LastAugments);
        }, error => AugmentsFailed?.Invoke(error));
    }
}
