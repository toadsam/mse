using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Networked] public MatchPhase Phase { get; set; }

    [SerializeField] private AugmentDatabase augmentDatabase;

    [Networked] public int RoundIndex { get; private set; }

    public MatchPhase CurrentPhase => Phase;

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
            Phase = MatchPhase.Lobby;

        GameManager.Instance?.RegisterMatchManager(this);
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (Phase == MatchPhase.Lobby)
        {
            if (GetAllPlayers().Count >= 1) // È¥ÀÚ Å×½ºÆ®¸é 1·Î ¹Ù²ãµµ µÊ, ³ªÁß¿¡ 2·Î
                StartMatchFlow();
        }
    }

    public override void Render()
    {
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public void StartMatchFlow()
    {
        if (!HasStateAuthority) return;

        RoundIndex = 1;
        EnterAugmentPhase();
    }

    public void EnterAugmentPhase()
    {
        if (!HasStateAuthority) return;

        Phase = MatchPhase.ChoosingAugment;
        AssignAugmentsToAllPlayers();
    }

    public void EnterPlayingPhase()
    {
        if (!HasStateAuthority) return;

        Phase = MatchPhase.Playing;
    }

    public void OnRoundEnded()
    {
        if (!HasStateAuthority) return;

        RoundIndex++;
        EnterAugmentPhase();
    }

    private void AssignAugmentsToAllPlayers()
    {
        List<PlayerNetwork> players = GetAllPlayers();

        foreach (PlayerNetwork player in players)
        {
            List<AugmentDefinition> draws = augmentDatabase.DrawRandomUnique(3);
            if (draws.Count < 3) continue;

            player.SetOfferedAugments(draws[0].id, draws[1].id, draws[2].id);
        }
    }

    public void NotifyPlayerSelectedAugment(PlayerNetwork player)
    {
        if (!HasStateAuthority) return;

        if (HaveAllPlayersSelectedAugment())
            EnterPlayingPhase();
    }

    private bool HaveAllPlayersSelectedAugment()
    {
        List<PlayerNetwork> players = GetAllPlayers();
        if (players.Count == 0) return false;

        foreach (PlayerNetwork player in players)
        {
            if (!player.HasSelectedAugmentNet)
                return false;
        }

        return true;
    }

    private List<PlayerNetwork> GetAllPlayers()
    {
        List<PlayerNetwork> result = new();

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject obj = Runner.GetPlayerObject(playerRef);
            if (obj == null) continue;

            PlayerNetwork player = obj.GetComponent<PlayerNetwork>();
            if (player != null)
                result.Add(player);
        }

        return result;
    }

    public AugmentDefinition GetAugmentById(int id)
    {
        return augmentDatabase != null ? augmentDatabase.GetById(id) : null;
    }
}