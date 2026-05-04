using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Header("Augments")]
    [SerializeField] private AugmentDatabase augmentDatabase;

    [Header("Match Rules")]
    [SerializeField] private int playersRequiredToStart = 1; // 혼자 테스트 중이면 1, 실제 멀티 테스트는 2
    [SerializeField] private int roundsToWin = 3;
    [SerializeField] private float roundIntroSeconds = 2.0f;
    [SerializeField] private float roundResultSeconds = 3.0f;

    [Header("Spawn Points")]
    [SerializeField] private Vector3 player0SpawnPosition = new Vector3(-5f, 1f, 0f);
    [SerializeField] private Vector3 player1SpawnPosition = new Vector3(5f, 1f, 0f);
    [SerializeField] private float player0SpawnYaw = 90f;
    [SerializeField] private float player1SpawnYaw = -90f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugContextMenu = true;

    [Networked] public MatchPhase Phase { get; set; }
    [Networked] public int RoundIndex { get; private set; }

    [Networked] public int Player0Wins { get; private set; }
    [Networked] public int Player1Wins { get; private set; }

    [Networked] public int RoundWinnerSlot { get; private set; }
    [Networked] public int MatchWinnerSlot { get; private set; }

    [Networked] private TickTimer PhaseTimer { get; set; }

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
        {
            Phase = MatchPhase.Lobby;
            RoundIndex = 0;
            Player0Wins = 0;
            Player1Wins = 0;
            RoundWinnerSlot = -1;
            MatchWinnerSlot = -1;
        }

        GameManager.Instance?.RegisterMatchManager(this);
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (Phase)
        {
            case MatchPhase.Lobby:
                if (GetAllPlayers().Count >= playersRequiredToStart)
                    StartMatchFlow();
                break;

            case MatchPhase.RoundIntro:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                    EnterPlayingPhase();
                break;

            case MatchPhase.RoundResult:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                {
                    if (MatchWinnerSlot >= 0)
                        EnterMatchResultPhase();
                    else
                        EnterAugmentPhase();
                }
                break;
        }
    }

    public override void Render()
    {
        GameManager.Instance?.SyncCursorWithPhase();
    }

    public void StartMatchFlow()
    {
        if (!HasStateAuthority)
            return;

        RoundIndex = 1;
        Player0Wins = 0;
        Player1Wins = 0;
        RoundWinnerSlot = -1;
        MatchWinnerSlot = -1;

        EnterAugmentPhase();
    }

    public void EnterAugmentPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.ChoosingAugment;
        RoundWinnerSlot = -1;

        AssignAugmentsToAllPlayers();
    }

    private void EnterRoundIntroPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.RoundIntro;
        ResetAllPlayersForRound();

        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundIntroSeconds);
    }

    public void EnterPlayingPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.Playing;
        PhaseTimer = default;
    }

    private void EnterRoundResultPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.RoundResult;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundResultSeconds);
    }

    private void EnterMatchResultPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.MatchResult;
        PhaseTimer = default;
    }

    public void OnRoundEnded()
    {
        if (!HasStateAuthority)
            return;

        RoundIndex++;
        EnterAugmentPhase();
    }

    public void NotifyPlayerSelectedAugment(PlayerNetwork player)
    {
        if (!HasStateAuthority)
            return;

        if (HaveAllPlayersSelectedAugment())
            EnterRoundIntroPhase();
    }

    public void ReportPlayerDefeated(PlayerNetwork defeatedPlayer)
    {
        if (!HasStateAuthority)
            return;

        if (Phase != MatchPhase.Playing)
            return;

        if (defeatedPlayer == null)
            return;

        int defeatedSlot = defeatedPlayer.SlotIndex;
        int winnerSlot = defeatedSlot == 0 ? 1 : 0;

        RegisterRoundWin(winnerSlot);
    }

    public void RegisterRoundWin(int winnerSlot)
    {
        if (!HasStateAuthority)
            return;

        if (Phase != MatchPhase.Playing)
            return;

        RoundWinnerSlot = winnerSlot;

        if (winnerSlot == 0)
            Player0Wins++;
        else if (winnerSlot == 1)
            Player1Wins++;

        if (Player0Wins >= roundsToWin)
            MatchWinnerSlot = 0;
        else if (Player1Wins >= roundsToWin)
            MatchWinnerSlot = 1;
        else
            MatchWinnerSlot = -1;

        EnterRoundResultPhase();
    }

    private void AssignAugmentsToAllPlayers()
    {
        if (augmentDatabase == null)
        {
            Debug.LogError("[MatchManager] AugmentDatabase is not assigned.");
            return;
        }

        List<PlayerNetwork> players = GetAllPlayers();

        foreach (PlayerNetwork player in players)
        {
            List<AugmentDefinition> draws = augmentDatabase.DrawRandomUnique(3);

            if (draws.Count < 3)
            {
                Debug.LogError("[MatchManager] Not enough augments in AugmentDatabase.");
                continue;
            }

            player.SetOfferedAugments(draws[0].id, draws[1].id, draws[2].id);
        }
    }

    private bool HaveAllPlayersSelectedAugment()
    {
        List<PlayerNetwork> players = GetAllPlayers();

        if (players.Count == 0)
            return false;

        foreach (PlayerNetwork player in players)
        {
            if (!player.HasSelectedAugmentNet)
                return false;
        }

        return true;
    }

    private void ResetAllPlayersForRound()
    {
        List<PlayerNetwork> players = GetAllPlayers();

        foreach (PlayerNetwork player in players)
        {
            if (player == null)
                continue;

            Vector3 pos = GetSpawnPosition(player.SlotIndex);
            float yaw = GetSpawnYaw(player.SlotIndex);

            player.ResetForRound(pos, yaw);
        }
    }

    private Vector3 GetSpawnPosition(int slot)
    {
        return slot == 0 ? player0SpawnPosition : player1SpawnPosition;
    }

    private float GetSpawnYaw(int slot)
    {
        return slot == 0 ? player0SpawnYaw : player1SpawnYaw;
    }

    private List<PlayerNetwork> GetAllPlayers()
    {
        List<PlayerNetwork> result = new();

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject obj = Runner.GetPlayerObject(playerRef);
            if (obj == null)
                continue;

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

    [ContextMenu("Debug/Player 0 Win Round")]
    private void DebugPlayer0WinRound()
    {
        if (!enableDebugContextMenu)
            return;

        if (HasStateAuthority && Phase == MatchPhase.Playing)
            RegisterRoundWin(0);
    }

    [ContextMenu("Debug/Player 1 Win Round")]
    private void DebugPlayer1WinRound()
    {
        if (!enableDebugContextMenu)
            return;

        if (HasStateAuthority && Phase == MatchPhase.Playing)
            RegisterRoundWin(1);
    }
}