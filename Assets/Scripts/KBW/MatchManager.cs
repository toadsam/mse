using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Header("Augments")]
    [SerializeField] private AugmentDatabase augmentDatabase;

    [Header("Match Rules")]
    [SerializeField] private int playersRequiredToStart = 2; // ȥ�� �׽�Ʈ ���̸� 1, ���� ��Ƽ �׽�Ʈ�� 2
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

    // 매치 지속 시간(초). 결과 화면 표시용. 호스트가 매치 종료 시 확정한다.
    [Networked] public float MatchDurationSeconds { get; private set; }
    [Networked] private float MatchStartSimTime { get; set; }

    // 백엔드 저장 결과 상태(결과 화면이 저장 성공 후 표시되도록 하는 신호).
    // Resolved: 저장 시도/스킵이 끝남, Succeeded: DB 저장 성공.
    [Networked] public NetworkBool ResultSaveResolved { get; private set; }
    [Networked] public NetworkBool ResultSaveSucceeded { get; private set; }

    // 호스트 콜백(코루틴)에서 세팅 → FixedUpdateNetwork에서 네트워크 상태로 미러링.
    private bool pendingSaveResolved;
    private bool pendingSaveSucceeded;

    [Networked] private TickTimer PhaseTimer { get; set; }

    private int lastAppliedArenaIndex = -999;

    // 매치 결과를 백엔드(MySQL)로 호스트가 1회만 전송하도록 막는 플래그.
    private bool matchResultReported = false;

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
                // 백엔드 저장 콜백(코루틴)에서 세팅한 결과를 네트워크 상태로 미러링한다.
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

        MatchStartSimTime = (float)Runner.SimulationTime;
        MatchDurationSeconds = 0f;
        ResultSaveResolved = false;
        ResultSaveSucceeded = false;
        pendingSaveResolved = false;
        pendingSaveSucceeded = false;

        usedOfferedAugmentIds.Clear();

        // 새 매치 시작 시 각 플레이어의 augment 선택 누적 기록 초기화.
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

        ChooseArenaForRound();       // �� �� �߰�
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

        Debug.Log($"[MatchManager] Round {RoundIndex} Arena: {ActiveArenaIndex}");
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

        MatchDurationSeconds = Mathf.Max(0f, (float)Runner.SimulationTime - MatchStartSimTime);

        ReportMatchResultToBackend();
    }

    // 매치 종료 시 호스트(StateAuthority)가 최종 결과를 백엔드로 1회 전송한다.
    // 저장 실패/비로그인이어도 게임 결과 화면은 정상 진행된다(로그만 남김).
    private void ReportMatchResultToBackend()
    {
        if (!HasStateAuthority)
            return;

        if (matchResultReported)
            return;

        if (MatchWinnerSlot < 0)
            return;

        List<PlayerNetwork> players = GetAllPlayers();
        PlayerNetwork slot0 = players.Find(p => p.SlotIndex == 0);
        PlayerNetwork slot1 = players.Find(p => p.SlotIndex == 1);

        if (slot0 == null || slot1 == null)
        {
            Debug.LogWarning("[MatchManager] 매치 결과 저장 스킵: 두 플레이어를 찾지 못했어(연결 종료 등).");
            return;
        }

        long player1Id = slot0.BackendUserId;
        long player2Id = slot1.BackendUserId;

        if (player1Id <= 0 || player2Id <= 0 || player1Id == player2Id)
        {
            Debug.LogWarning($"[MatchManager] 매치 결과 저장 스킵: 유효하지 않은 backend userId (p1={player1Id}, p2={player2Id}). 게스트/비로그인 또는 미동기화일 수 있어.");
            return;
        }

        long winnerId = MatchWinnerSlot == 0 ? player1Id : player2Id;

        // 여기까지 왔으면 전송 시도 → 중복 방지 플래그를 먼저 세운다.
        matchResultReported = true;

        if (MatchResultService.Instance == null)
        {
            Debug.LogWarning("[MatchManager] MatchResultService.Instance가 없어 매치 결과를 저장하지 못했어.");
            // 저장 불가 → 결과 화면은 정상 표시되도록 resolved 처리(저장 실패 상태).
            pendingSaveSucceeded = false;
            pendingSaveResolved = true;
            return;
        }

        MatchResultRequest request = BuildMatchResultRequest(slot0, slot1, winnerId);

        Debug.Log($"[MatchManager] 매치 결과 저장 요청: p1={player1Id}, p2={player2Id}, winner={winnerId}, score={Player0Wins}:{Player1Wins}, duration={MatchDurationSeconds:0.0}s");

        MatchResultService.Instance.SaveResult(
            request,
            match =>
            {
                Debug.Log($"[MatchManager] 매치 결과 저장 성공. matchId={match?.id}");
                pendingSaveSucceeded = true;
                pendingSaveResolved = true;
            },
            error =>
            {
                Debug.LogError($"[MatchManager] 매치 결과 저장 실패: {error}");
                pendingSaveSucceeded = false;
                pendingSaveResolved = true;
            });
    }

    // 두 플레이어의 닉네임/캐릭터/선택 augment 이름/점수를 채운 저장 요청을 만든다.
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
                continue; // 이름을 알 수 없는 항목은 저장하지 않는다.

            augments.Add(new MatchPlayerAugmentRequest
            {
                augmentId = 0, // Unity augment는 DB augments와 매핑되지 않음 → 이름으로만 저장.
                augmentName = augmentName,
                selectedOrder = i + 1,               // 유저별로 (round, order) 유일하도록 전역 증가.
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

        // ù ���� ���� ������ �� �� ����
        if (RoundIndex == 1 && Player0Wins == 0 && Player1Wins == 0)
            return true;

        // ���Ŀ��� ���� ���� ���ڸ� ����
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

            // ���� Runner�� ���� �÷��̾ ���
            if (Runner != null && player.Runner != Runner)
                continue;

            players.Add(player);
        }

        // ���� ������ �׻� �����ϵ��� ����
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

    // 결과 화면용: 슬롯(0/1)에 해당하는 플레이어를 반환한다.
    public PlayerNetwork GetPlayerBySlot(int slot)
    {
        return GetAllPlayers().Find(p => p.SlotIndex == slot);
    }

    // 결과 화면용: 해당 플레이어가 매치 중 선택한 augment 표시 이름 목록.
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