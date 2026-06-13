using Fusion;
using System.Collections.Generic;
using UnityEngine;

// State-authoritative controller for match phases, rounds, augments, arenas, and result reporting.
public class MatchManager : NetworkBehaviour
{
    // Singleton-style access to the active networked match manager.
    public static MatchManager Instance { get; private set; }

    [Header("Augments")]
    // Source database used to offer and resolve augment definitions.
    [SerializeField] private AugmentDatabase augmentDatabase;

    [Header("Match Rules")]
    // Core match rule values configured from the Inspector.
    [SerializeField] private int playersRequiredToStart = 2; 
    [SerializeField] private int roundsToWin = 3;
    [SerializeField] private float roundIntroSeconds = 2.0f;
    [SerializeField] private float roundResultSeconds = 3.0f;

    [Header("Spawn Points")]
    [SerializeField] private Vector3 player0SpawnPosition = new Vector3(-5f, 1f, 0f);
    [SerializeField] private Vector3 player1SpawnPosition = new Vector3(5f, 1f, 0f);
    [SerializeField] private float player0SpawnYaw = 90f;
    [SerializeField] private float player1SpawnYaw = -90f;

    [Header("Arena Zones")]
    // Arena zones used as non-repeating round maps.
    [SerializeField] private ArenaZone[] arenaZones;
    [SerializeField] private bool resetArenaPoolWhenEmpty = false;

    [Networked] public int ActiveArenaIndex { get; private set; }
    [Networked] public int AugmentChooserMask { get; private set; }

    private readonly List<int> unusedArenaIndices = new();

    private readonly HashSet<int> usedOfferedAugmentIds = new();

    [Header("Debug")]
    [SerializeField] private bool enableDebugContextMenu = true;

    // Networked match state replicated to clients and UI.
    [Networked] public MatchPhase Phase { get; set; }
    [Networked] public int RoundIndex { get; private set; }

    // Networked round score for each player slot.
    [Networked] public int Player0Wins { get; private set; }
    [Networked] public int Player1Wins { get; private set; }

    [Networked] public int RoundWinnerSlot { get; private set; }
    [Networked] public int MatchWinnerSlot { get; private set; }

    [Networked] public float MatchDurationSeconds { get; private set; }
    [Networked] private float MatchStartSimTime { get; set; }


    // Backend save status used by the result UI.
    [Networked] public NetworkBool ResultSaveResolved { get; private set; }
    [Networked] public NetworkBool ResultSaveSucceeded { get; private set; }

    private bool pendingSaveResolved;
    private bool pendingSaveSucceeded;

    private int lastLoggedLobbyPlayerCount = -1;

    [Networked] private TickTimer PhaseTimer { get; set; }

    private int lastAppliedArenaIndex = -999;


    private bool matchResultReported = false;

    private int lastRoundResetBeforePlaying = -1;

    public bool IsNetworkSpawned { get; private set; }
    public MatchPhase CurrentPhase => IsNetworkSpawned ? Phase : MatchPhase.Lobby;

    [SerializeField] private bool submitMatchResultToBackend = true;
    [SerializeField] private float returnToLobbyAfterMatchSeconds = 6f;

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
        Instance = this;

        Debug.Log(
            $"[MatchManager] Spawned. HasStateAuthority={HasStateAuthority}, Runner={Runner}"
        );

        if (HasStateAuthority)
        {
            Phase = MatchPhase.Lobby;
            RoundIndex = 0;
            Player0Wins = 0;
            Player1Wins = 0;
            RoundWinnerSlot = -1;
            MatchWinnerSlot = -1;

            ActiveArenaIndex = -1;
            AugmentChooserMask = 0;
            ResultSaveResolved = false;
            ResultSaveSucceeded = false;
        }

        IsNetworkSpawned = true;
        lastAppliedArenaIndex = -999;

        GameManager.Instance?.RegisterMatchManager(this);
        GameManager.Instance?.SyncCursorWithPhase();
    }

    // Advances phase timers and host-only match flow.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (Phase)
        {
            case MatchPhase.Lobby:
                {
                    int count = GetAllPlayers().Count;

                    if (count != lastLoggedLobbyPlayerCount)
                    {
                        lastLoggedLobbyPlayerCount = count;
                        Debug.Log($"[MatchManager] Lobby player count={count}, Required={playersRequiredToStart}, HasStateAuthority={HasStateAuthority}");
                    }

                    if (count >= playersRequiredToStart)
                        StartMatchFlow();

                    break;
                }

            case MatchPhase.RoundIntro:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                    EnterPlayingPhase();
                break;

            case MatchPhase.RoundResult:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                {
                    if (MatchWinnerSlot >= 0)
                    {
                        EnterMatchResultPhase();
                    }
                    else
                    {
                        RoundIndex++;
                        EnterAugmentPhase();
                    }
                }
                break;

            case MatchPhase.MatchResult:
                if (pendingSaveResolved && !ResultSaveResolved)
                {
                    ResultSaveSucceeded = pendingSaveSucceeded;
                    ResultSaveResolved = true;
                }
                break;
        }
    }

    public override void Render()
    {
        GameManager.Instance?.SyncCursorWithPhase();

        if (lastAppliedArenaIndex != ActiveArenaIndex)
        {
            lastAppliedArenaIndex = ActiveArenaIndex;
            ApplyActiveArenaVisuals();
        }
    }

    // Resets match state and starts the first augment phase.
    public void StartMatchFlow()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("[MatchManager] StartMatchFlow");

        RoundIndex = 1;
        Player0Wins = 0;
        Player1Wins = 0;
        RoundWinnerSlot = -1;
        MatchWinnerSlot = -1;
        lastRoundResetBeforePlaying = -1;
        matchResultReported = false;

        MatchStartSimTime = (float)Runner.SimulationTime;
        MatchDurationSeconds = 0f;
        ResultSaveResolved = false;
        ResultSaveSucceeded = false;
        pendingSaveResolved = false;
        pendingSaveSucceeded = false;
        usedOfferedAugmentIds.Clear();

        foreach (PlayerNetwork player in GetAllPlayers())
            player.ResetAugmentHistory();

        InitializeArenaPool();
        EnterAugmentPhase();
    }

    // Builds the list of arena indices available for random selection.
    private void InitializeArenaPool()
    {
        unusedArenaIndices.Clear();

        if (arenaZones == null || arenaZones.Length == 0)
        {
            Debug.LogError("[MatchManager] ArenaZones are not assigned.");
            ActiveArenaIndex = -1;
            return;
        }

        for (int i = 0; i < arenaZones.Length; i++)
        {
            if (arenaZones[i] != null)
            {
                unusedArenaIndices.Add(i);
            }
            else
            {
                Debug.LogWarning($"[MatchManager] ArenaZone index {i} is null.");
            }
        }

        if (unusedArenaIndices.Count == 0)
        {
            Debug.LogError("[MatchManager] No valid ArenaZone exists.");
            ActiveArenaIndex = -1;
            return;
        }

        ActiveArenaIndex = -1;
        ApplyActiveArenaVisuals();

        Debug.Log($"[MatchManager] Arena pool initialized. Count: {unusedArenaIndices.Count}");
    }

    // Starts the between-round augment selection phase.
    public void EnterAugmentPhase()
    {
        if (!HasStateAuthority)
            return;

        PhaseTimer = default;

        AssignAugmentsByRoundRule();

        Phase = MatchPhase.ChoosingAugment;
    }

    // Chooses an arena and resets players before combat begins.
    private void EnterRoundIntroPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.RoundIntro;

        ChooseArenaForRound();       
        ResetAllPlayersForRound();

        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundIntroSeconds);
    }

    // Selects a random unused arena for the next round.
    private void ChooseArenaForRound()
    {
        if (arenaZones == null || arenaZones.Length == 0)
        {
            Debug.LogError("[MatchManager] ArenaZones are not assigned.");
            ActiveArenaIndex = -1;
            return;
        }

        if (unusedArenaIndices.Count == 0)
        {
            if (resetArenaPoolWhenEmpty)
            {
                InitializeArenaPool();
            }
            else
            {
                Debug.LogWarning("[MatchManager] No unused arena left. Reusing arena 0.");
                ActiveArenaIndex = 0;
                ApplyActiveArenaVisuals();
                return;
            }
        }

        int randomListIndex = Random.Range(0, unusedArenaIndices.Count);
        ActiveArenaIndex = unusedArenaIndices[randomListIndex];
        unusedArenaIndices.RemoveAt(randomListIndex);

        ApplyActiveArenaVisuals();

        Debug.Log($"[MatchManager] Round {RoundIndex} Arena: {ActiveArenaIndex}, Name: {arenaZones[ActiveArenaIndex].name}"
);
    }

    // Starts active combat for the current round.
    public void EnterPlayingPhase()
    {
        if (!HasStateAuthority)
            return;

        if (lastRoundResetBeforePlaying != RoundIndex)
        {
            Debug.Log($"[MatchManager] Safety reset before Playing. Round={RoundIndex}, ActiveArenaIndex={ActiveArenaIndex}");

            if (GetActiveArena() == null)
            {
                Debug.LogWarning("[MatchManager] Active arena is invalid before Playing. Choosing arena again.");
                ChooseArenaForRound();
            }

            ResetAllPlayersForRound();
            lastRoundResetBeforePlaying = RoundIndex;
        }

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

    // Finalizes match duration and starts backend result reporting.
    private void EnterMatchResultPhase()
    {
        if (!HasStateAuthority)
            return;

        if (Phase == MatchPhase.MatchResult)
            return;

        Phase = MatchPhase.MatchResult;
        PhaseTimer = default;

        MatchDurationSeconds = Mathf.Max(0f, (float)Runner.SimulationTime - MatchStartSimTime);

        ReportMatchResultToBackend();

        MatchResultUI resultUI = FindFirstObjectByType<MatchResultUI>(FindObjectsInactive.Include);
        if (resultUI == null)
        {
            FusionBootstrap bootstrap =
                FindFirstObjectByType<FusionBootstrap>(FindObjectsInactive.Include);

            if (bootstrap != null)
            {
                Debug.Log("[MatchManager] MatchResultUI not found. Scheduling fallback return to lobby.");
                bootstrap.ReturnToLobbyAfter(
                    returnToLobbyAfterMatchSeconds,
                    "Match finished. Returning to lobby..."
                );
            }
            else
            {
                Debug.LogError("[MatchManager] FusionBootstrap not found.");
            }
        }
    }

    private void ResolveResultSaveFailure(string reason)
    {
        Debug.LogWarning(reason);

        matchResultReported = true;
        pendingSaveSucceeded = false;
        pendingSaveResolved = true;
    }

    // Sends the completed match result to the backend service.
    private void ReportMatchResultToBackend()
    {
        if (!HasStateAuthority)
            return;

        if (matchResultReported)
            return;

        if (MatchWinnerSlot < 0)
            return;

        if (!submitMatchResultToBackend)
        {
            matchResultReported = true;
            pendingSaveSucceeded = false;
            pendingSaveResolved = true;
            return;
        }

        List<PlayerNetwork> players = GetAllPlayers();
        PlayerNetwork slot0 = players.Find(p => p.SlotIndex == 0);
        PlayerNetwork slot1 = players.Find(p => p.SlotIndex == 1);

        if (slot0 == null || slot1 == null)
        {
            ResolveResultSaveFailure("[MatchManager] Cannot save result. Both players are not found.");
            return;
        }

        long player1Id = slot0.BackendUserId;
        long player2Id = slot1.BackendUserId;

        if (player1Id <= 0 || player2Id <= 0 || player1Id == player2Id)
        {
            ResolveResultSaveFailure($"[MatchManager] Invalid backend userId. p1={player1Id}, p2={player2Id}");
            return;
        }

        long winnerId = MatchWinnerSlot == 0 ? player1Id : player2Id;

        matchResultReported = true;

        if (MatchResultService.Instance == null)
        {
            ResolveResultSaveFailure("[MatchManager] MatchResultService.Instance is missing.");
            return;
        }

        MatchResultRequest request = BuildMatchResultRequest(slot0, slot1, winnerId);

        Debug.Log($"[MatchManager] 留ㅼ튂 寃곌낵 ????붿껌: p1={player1Id}, p2={player2Id}, winner={winnerId}, score={Player0Wins}:{Player1Wins}, duration={MatchDurationSeconds:0.0}s");

        MatchResultService.Instance.SaveResult(
            request,
            match =>
            {
                Debug.Log($"[MatchManager] 留ㅼ튂 寃곌낵 ????깃났. matchId={match?.id}");
                pendingSaveSucceeded = true;
                pendingSaveResolved = true;
            },
            error =>
            {
                Debug.LogError($"[MatchManager] 留ㅼ튂 寃곌낵 ????ㅽ뙣: {error}");
                pendingSaveSucceeded = false;
                pendingSaveResolved = true;
            });
    }

    // Builds the backend payload for the match result endpoint.
    private MatchResultRequest BuildMatchResultRequest(PlayerNetwork slot0, PlayerNetwork slot1, long winnerId)
    {
        MatchResultRequest request = new MatchResultRequest
        {
            player1Id = slot0.BackendUserId,
            player2Id = slot1.BackendUserId,
            winnerId = winnerId,
            player1Score = Player0Wins,
            player2Score = Player1Wins,
            players = new List<MatchPlayerResultRequest>
            {
                BuildPlayerResult(slot0, winnerId, Player0Wins),
                BuildPlayerResult(slot1, winnerId, Player1Wins)
            }
        };

        return request;
    }

    private MatchPlayerResultRequest BuildPlayerResult(PlayerNetwork player, long winnerId, int score)
    {
        List<MatchPlayerAugmentRequest> augments = new List<MatchPlayerAugmentRequest>();

        for (int i = 0; i < player.AugmentHistoryCount; i++)
        {
            int augmentId = player.GetSelectedAugmentId(i);
            int round = player.GetSelectedAugmentRound(i);

            AugmentDefinition def = GetAugmentById(augmentId);
            string augmentName = def != null ? def.displayName : null;

            if (string.IsNullOrEmpty(augmentName))
                continue; 

            augments.Add(new MatchPlayerAugmentRequest
            {
                augmentId = 0, 
                augmentName = augmentName,
                selectedOrder = i + 1,              
                selectedRound = round > 0 ? round : i + 1
            });
        }

        return new MatchPlayerResultRequest
        {
            userId = player.BackendUserId,
            result = player.BackendUserId == winnerId ? "WIN" : "LOSE",
            score = Mathf.Clamp(score, 0, 10),
            damageDealt = 0,
            characterName = player.CharacterDisplayName.ToString(),
            augments = augments
        };
    }

    public void OnRoundEnded()
    {
        if (!HasStateAuthority)
            return;

        if (Phase != MatchPhase.RoundResult)
            return;

        if (MatchWinnerSlot >= 0)
        {
            EnterMatchResultPhase();
        }
        else
        {
            RoundIndex++;
            EnterAugmentPhase();
        }
    }

    // Moves to round intro after every required player selected an augment.
    public void NotifyPlayerSelectedAugment(PlayerNetwork player)
    {
        if (!HasStateAuthority)
            return;

        if (HaveAllPlayersSelectedAugment())
            EnterRoundIntroPhase();
    }

    // Registers a round win when a player reaches zero HP.
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

    // Updates round score and decides whether the match is finished.
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

    // Offers augments to both players in round one, then only to the round loser.
    private void AssignAugmentsByRoundRule()
    {
        if (augmentDatabase == null)
        {
            Debug.LogError("[MatchManager] AugmentDatabase is not assigned.");
            return;
        }

        List<PlayerNetwork> players = GetAllPlayers();

        AugmentChooserMask = 0;

        foreach (PlayerNetwork player in players)
        {
            bool canChoose = ShouldPlayerChooseAugment(player);

            if (canChoose)
            {
                AugmentChooserMask |= 1 << player.SlotIndex;

                List<AugmentDefinition> draws =
                    augmentDatabase.DrawRandomUniqueExcluding(3, usedOfferedAugmentIds);

                if (draws.Count < 3)
                {
                    Debug.LogError("[MatchManager] Not enough unique augments.");
                    player.SetOfferedAugments(-1, -1, -1, false);
                    continue;
                }

                usedOfferedAugmentIds.Add(draws[0].id);
                usedOfferedAugmentIds.Add(draws[1].id);
                usedOfferedAugmentIds.Add(draws[2].id);

                player.SetOfferedAugments(draws[0].id, draws[1].id, draws[2].id, true);
            }
            else
            {
                player.SetOfferedAugments(-1, -1, -1, false);
            }
        }
    }

    private bool ShouldPlayerChooseAugment(PlayerNetwork player)
    {
        if (player == null)
            return false;

        if (RoundIndex == 1 && Player0Wins == 0 && Player1Wins == 0)
            return true;

        if (RoundWinnerSlot < 0)
            return false;

        int loserSlot = RoundWinnerSlot == 0 ? 1 : 0;
        return player.SlotIndex == loserSlot;
    }

    // Finds all player objects owned by this runner and sorts them by slot.
    private List<PlayerNetwork> GetAllPlayers()
    {
        List<PlayerNetwork> players = new List<PlayerNetwork>();

        PlayerNetwork[] foundPlayers = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);

        foreach (PlayerNetwork player in foundPlayers)
        {
            if (player == null)
                continue;

            if (player.Object == null)
                continue;

            if (Runner != null && player.Runner != Runner)
                continue;

            players.Add(player);
        }

        players.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

        return players;
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

    // Teleports and resets every player at the active arena spawn points.
    private void ResetAllPlayersForRound()
    {
        List<PlayerNetwork> players = GetAllPlayers();

        Debug.Log($"[MatchManager] ResetAllPlayersForRound / ActiveArenaIndex: {ActiveArenaIndex}");

        foreach (PlayerNetwork player in players)
        {
            if (player == null)
                continue;

            Vector3 pos = GetSpawnPosition(player.SlotIndex);
            float yaw = GetSpawnYaw(player.SlotIndex);

            Debug.Log($"[MatchManager] Reset Player Slot {player.SlotIndex} -> Pos {pos}, Yaw {yaw}");

            player.ResetForRound(pos, yaw);

            Debug.Log(
                $"[MatchManager] After Reset Slot {player.SlotIndex} / Transform Pos {player.transform.position}"
            );


        }
    }

    private Vector3 GetSpawnPosition(int slot)
    {
        ArenaZone arena = GetActiveArena();

        if (arena != null)
        {
            Vector3 arenaPos = arena.GetSpawnPosition(slot);
            Debug.Log($"[MatchManager] Using Arena Spawn. ArenaIndex: {ActiveArenaIndex}, Slot: {slot}, Pos: {arenaPos}");
            return arenaPos;
        }

        Vector3 fallback = slot == 0 ? player0SpawnPosition : player1SpawnPosition;
        Debug.LogWarning($"[MatchManager] Using Fallback Spawn. ActiveArenaIndex: {ActiveArenaIndex}, Slot: {slot}, Pos: {fallback}");

        return fallback;
    }

    private float GetSpawnYaw(int slot)
    {
        ArenaZone arena = GetActiveArena();

        if (arena != null)
            return arena.GetSpawnYaw(slot);

        // fallback
        return slot == 0 ? player0SpawnYaw : player1SpawnYaw;
    }

    private ArenaZone GetActiveArena()
    {
        if (arenaZones == null)
            return null;

        if (ActiveArenaIndex < 0 || ActiveArenaIndex >= arenaZones.Length)
            return null;

        return arenaZones[ActiveArenaIndex];
    }

    private void ApplyActiveArenaVisuals()
    {
        if (arenaZones == null)
            return;

        for (int i = 0; i < arenaZones.Length; i++)
        {
            if (arenaZones[i] == null)
                continue;

            arenaZones[i].SetActiveArena(i == ActiveArenaIndex);
        }
    }

    public AugmentDefinition GetAugmentById(int id)
    {
        return augmentDatabase != null ? augmentDatabase.GetById(id) : null;
    }

    public PlayerNetwork GetPlayerBySlot(int slot)
    {
        return GetAllPlayers().Find(p => p.SlotIndex == slot);
    }

    // Returns selected augment names for the match result UI and backend payload.
    public List<string> GetSelectedAugmentNames(PlayerNetwork player)
    {
        List<string> names = new List<string>();

        if (player == null)
            return names;

        for (int i = 0; i < player.AugmentHistoryCount; i++)
        {
            AugmentDefinition def = GetAugmentById(player.GetSelectedAugmentId(i));
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                names.Add(def.displayName);
        }

        return names;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsNetworkSpawned = false;

        if (Instance == this)
            Instance = null;

        GameManager.Instance?.UnregisterMatchManager(this);
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
