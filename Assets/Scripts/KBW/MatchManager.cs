using Fusion;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Networked] public MatchPhase Phase { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
            Phase = MatchPhase.Playing;

        GameManager.Instance?.RegisterMatchManager(this);
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public override void Render()
    {
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public void EnterAugmentPhase()
    {
        if (!HasStateAuthority) return;
        Phase = MatchPhase.ChoosingAugment;
    }

    public void EnterPlayingPhase()
    {
        if (!HasStateAuthority) return;
        Phase = MatchPhase.Playing;
    }
}