using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Header("Augments")]
    [SerializeField] private AugmentDatabase augmentDatabase;

    [Header("Match Rules")]
    [SerializeField] private int playersRequiredToStart = 2; // 혼占쏙옙 占쌓쏙옙트 占쏙옙占싱몌옙 1, 占쏙옙占쏙옙 占쏙옙티 占쌓쏙옙트占쏙옙 2
    [SerializeField] private int roundsToWin = 3;
    [SerializeField] private float roundIntroSeconds = 2.0f;
    [SerializeField] private float roundResultSeconds = 3.0f;

    [Header("Spawn Points")]
    [SerializeField] private Vector3 player0SpawnPosition = new Vector3(-5f, 1f, 0f);
    [SerializeField] private Vector3 player1SpawnPosition = new Vector3(5f, 1f, 0f);
    [SerializeField] private float player0SpawnYaw = 90f;
    [SerializeField] private float player1SpawnYaw = -90f;

    [Header("Arena Zones")]
    [SerializeField] private ArenaZone[] arenaZones;
    [SerializeField] private bool resetArenaPoolWhenEmpty = false;

    [Networked] public int ActiveArenaIndex { get; private set; }
    [Networked] public int AugmentChooserMask { get; private set; }

    private readonly List<int> unusedArenaIndices = new();

    private readonly HashSet<int> usedOfferedAugmentIds = new();

    [Header("Debug")]
    [SerializeField] private bool enableDebugContextMenu = true;

    [Networked] public MatchPhase Phase { get; set; }
    [Networked] public int RoundIndex { get; private set; }

    [Networked] public int Player0Wins { get; private set; }
    [Networked] public int Player1Wins { get; private set; }

    [Networked] public int RoundWinnerSlot { get; private set; }
    [Networked] public int MatchWinnerSlot { get; private set; }

    // 留ㅼ튂 吏???쒓컙(珥?. 寃곌낵 ?붾㈃ ?쒖떆?? ?몄뒪?멸? 留ㅼ튂 醫낅즺 ???뺤젙?쒕떎.
    [Networked] public float MatchDurationSeconds { get; private set; }
    [Networked] private float MatchStartSimTime { get; set; }

    // 諛깆뿏?????寃곌낵 ?곹깭(寃곌낵 ?붾㈃??????깃났 ???쒖떆?섎룄濡??섎뒗 ?좏샇).
    // Resolved: ????쒕룄/?ㅽ궢???앸궓, Succeeded: DB ????깃났.
    [Networked] public NetworkBool ResultSaveResolved { get; private set; }
    [Networked] public NetworkBool ResultSaveSucceeded { get; private set; }

    // ?몄뒪??肄쒕갚(肄붾（???먯꽌 ?명똿 ??FixedUpdateNetwork?먯꽌 ?ㅽ듃?뚰겕 ?곹깭濡?誘몃윭留?
    private bool pendingSaveResolved;
    private bool pendingSaveSucceeded;

    [Networked] private TickTimer PhaseTimer { get; set; }

    private int lastAppliedArenaIndex = -999;

    // 留ㅼ튂 寃곌낵瑜?諛깆뿏??MySQL)濡??몄뒪?멸? 1?뚮쭔 ?꾩넚?섎룄濡?留됰뒗 ?뚮옒洹?
    private bool matchResultReported = false;

    public MatchPhase CurrentPhase => Phase;

    [SerializeField] private bool submitMatchResultToBackend = true;
    [SerializeField] private float returnToLobbyAfterMatchSeconds = 6f;

    private struct SelectedAugmentRecord
    {
        public int augmentId;
        public string augmentName;
        public int selectedRound;
        public int selectedOrder;
    }

    private readonly Dictionary<int, List<SelectedAugmentRecord>> selectedAugmentsBySlot = new();

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
                // 諛깆뿏?????肄쒕갚(肄붾（???먯꽌 ?명똿??寃곌낵瑜??ㅽ듃?뚰겕 ?곹깭濡?誘몃윭留곹븳??
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

    public void StartMatchFlow()
    {
        if (!HasStateAuthority)
            return;

        RoundIndex = 1;
        Player0Wins = 0;
        Player1Wins = 0;
        RoundWinnerSlot = -1;
        MatchWinnerSlot = -1;

        matchResultReported = false;
        selectedAugmentsBySlot.Clear();

        MatchStartSimTime = (float)Runner.SimulationTime;
        MatchDurationSeconds = 0f;
        ResultSaveResolved = false;
        ResultSaveSucceeded = false;
        pendingSaveResolved = false;
        pendingSaveSucceeded = false;
        usedOfferedAugmentIds.Clear();

        // ??留ㅼ튂 ?쒖옉 ??媛??뚮젅?댁뼱??augment ?좏깮 ?꾩쟻 湲곕줉 珥덇린??
        foreach (PlayerNetwork player in GetAllPlayers())
            player.ResetAugmentHistory();

        InitializeArenaPool();
        EnterAugmentPhase();
    }

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

    public void EnterAugmentPhase()
    {
        if (!HasStateAuthority)
            return;

        PhaseTimer = default;

        AssignAugmentsByRoundRule();

        Phase = MatchPhase.ChoosingAugment;
    }

    private void EnterRoundIntroPhase()
    {
        if (!HasStateAuthority)
            return;

        Phase = MatchPhase.RoundIntro;

        ChooseArenaForRound();       // 占쏙옙 占쏙옙 占쌩곤옙
        ResetAllPlayersForRound();

        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundIntroSeconds);
    }

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

    // 留ㅼ튂 醫낅즺 ???몄뒪??StateAuthority)媛 理쒖쥌 寃곌낵瑜?諛깆뿏?쒕줈 1???꾩넚?쒕떎.
    // ????ㅽ뙣/鍮꾨줈洹몄씤?댁뼱??寃뚯엫 寃곌낵 ?붾㈃? ?뺤긽 吏꾪뻾?쒕떎(濡쒓렇留??④?).
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
            Debug.LogWarning("[MatchManager] 留ㅼ튂 寃곌낵 ????ㅽ궢: ???뚮젅?댁뼱瑜?李얠? 紐삵뻽???곌껐 醫낅즺 ??.");
            return;
        }

        long player1Id = slot0.BackendUserId;
        long player2Id = slot1.BackendUserId;

        if (player1Id <= 0 || player2Id <= 0 || player1Id == player2Id)
        {
            Debug.LogWarning($"[MatchManager] 留ㅼ튂 寃곌낵 ????ㅽ궢: ?좏슚?섏? ?딆? backend userId (p1={player1Id}, p2={player2Id}). 寃뚯뒪??鍮꾨줈洹몄씤 ?먮뒗 誘몃룞湲고솕?????덉뼱.");
            return;
        }

        long winnerId = MatchWinnerSlot == 0 ? player1Id : player2Id;

        // ?ш린源뚯? ?붿쑝硫??꾩넚 ?쒕룄 ??以묐났 諛⑹? ?뚮옒洹몃? 癒쇱? ?몄슫??
        matchResultReported = true;

        if (MatchResultService.Instance == null)
        {
            Debug.LogWarning("[MatchManager] MatchResultService.Instance媛 ?놁뼱 留ㅼ튂 寃곌낵瑜???ν븯吏 紐삵뻽??");
            // ???遺덇? ??寃곌낵 ?붾㈃? ?뺤긽 ?쒖떆?섎룄濡?resolved 泥섎━(????ㅽ뙣 ?곹깭).
            pendingSaveSucceeded = false;
            pendingSaveResolved = true;
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

    // ???뚮젅?댁뼱???됰꽕??罹먮┃???좏깮 augment ?대쫫/?먯닔瑜?梨꾩슫 ????붿껌??留뚮뱺??
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
                continue; // ?대쫫???????녿뒗 ??ぉ? ??ν븯吏 ?딅뒗??

            augments.Add(new MatchPlayerAugmentRequest
            {
                augmentId = 0, // Unity augment??DB augments? 留ㅽ븨?섏? ?딆쓬 ???대쫫?쇰줈留????
                augmentName = augmentName,
                selectedOrder = i + 1,               // ?좎?蹂꾨줈 (round, order) ?좎씪?섎룄濡??꾩뿭 利앷?.
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

        // 첫 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙 占쏙옙 占쏙옙占쏙옙
        if (RoundIndex == 1 && Player0Wins == 0 && Player1Wins == 0)
            return true;

        // 占쏙옙占식울옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쌘몌옙 占쏙옙占쏙옙
        if (RoundWinnerSlot < 0)
            return false;

        int loserSlot = RoundWinnerSlot == 0 ? 1 : 0;
        return player.SlotIndex == loserSlot;
    }

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

            // 占쏙옙占쏙옙 Runner占쏙옙 占쏙옙占쏙옙 占시뤄옙占싱어만 占쏙옙占?
            if (Runner != null && player.Runner != Runner)
                continue;

            players.Add(player);
        }

        // 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쌓삼옙 占쏙옙占쏙옙占싹듸옙占쏙옙 占쏙옙占쏙옙
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

    public void RecordSelectedAugment(PlayerNetwork player, AugmentDefinition def)
    {
        if (!HasStateAuthority)
            return;

        if (player == null || def == null)
            return;

        int slot = player.SlotIndex;

        if (!selectedAugmentsBySlot.TryGetValue(slot, out List<SelectedAugmentRecord> records))
        {
            records = new List<SelectedAugmentRecord>();
            selectedAugmentsBySlot.Add(slot, records);
        }

        records.Add(new SelectedAugmentRecord
        {
            augmentId = def.id,
            augmentName = def.displayName,
            selectedRound = RoundIndex,
            selectedOrder = records.Count + 1
        });
    }

    public AugmentDefinition GetAugmentById(int id)
    {
        return augmentDatabase != null ? augmentDatabase.GetById(id) : null;
    }

    // 寃곌낵 ?붾㈃?? ?щ’(0/1)???대떦?섎뒗 ?뚮젅?댁뼱瑜?諛섑솚?쒕떎.
    public PlayerNetwork GetPlayerBySlot(int slot)
    {
        return GetAllPlayers().Find(p => p.SlotIndex == slot);
    }

    // 寃곌낵 ?붾㈃?? ?대떦 ?뚮젅?댁뼱媛 留ㅼ튂 以??좏깮??augment ?쒖떆 ?대쫫 紐⑸줉.
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
