using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Lobby")]
    [SerializeField] private string customLobbyName = "LastRound_Lobby";
    [SerializeField] private int maxPlayersPerRoom = 2;
    [SerializeField] private string defaultRoomPrefix = "LastRound";

    [Header("Object Pool")]
    [SerializeField] private PooledNetworkObjectProvider objectProvider;
    [SerializeField] private int maxPooledObjectsPerPrefab = 64;

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    public event Action GameSessionStarted;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<SessionInfo> cachedSessions = new();

    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;

    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;
    public event Action<string> StatusChanged;

    private bool isBusy;
    private bool isInLobby;


    // 탭 입력 누락 방지용
    private bool dashPressed;
    private bool abilityPressed;
    private bool reloadPressed;
    private bool aug1Pressed;
    private bool aug2Pressed;
    private bool aug3Pressed;
    private bool jumpPressed;

    private void Start()
    {
        GameManager.Instance?.RegisterBootstrap(this);
    }

    private void Update()
    {
        // UI 상태에서는 게임플레이 입력을 누적하지 않음
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
            return;

        dashPressed |= Input.GetKeyDown(KeyCode.LeftShift);
        jumpPressed |= Input.GetKeyDown(KeyCode.Space);
        abilityPressed |= Input.GetKeyDown(KeyCode.F);
        reloadPressed |= Input.GetKeyDown(KeyCode.R);

        aug1Pressed |= Input.GetKeyDown(KeyCode.Alpha1);
        aug2Pressed |= Input.GetKeyDown(KeyCode.Alpha2);
        aug3Pressed |= Input.GetKeyDown(KeyCode.Alpha3);
    }

    private void CreateRunnerIfNeeded()
    {
        if (runner != null)
            return;

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        if (sceneManager == null)
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        if (objectProvider == null)
            objectProvider = GetComponent<PooledNetworkObjectProvider>();

        if (objectProvider == null)
            objectProvider = gameObject.AddComponent<PooledNetworkObjectProvider>();

        objectProvider.SetMaxPoolCount(maxPooledObjectsPerPrefab);
    }

    public async void JoinLobby()
    {
        if (isBusy)
            return;

        CreateRunnerIfNeeded();

        if (isInLobby)
        {
            SessionListUpdated?.Invoke(cachedSessions);
            return;
        }

        isBusy = true;
        StatusChanged?.Invoke("Connecting to lobby...");

        var result = await runner.JoinSessionLobby(SessionLobby.Custom, customLobbyName);

        isBusy = false;

        if (result.Ok)
        {
            isInLobby = true;
            StatusChanged?.Invoke("Lobby connected. Select a room or create one.");
        }
        else
        {
            StatusChanged?.Invoke($"Failed to join lobby: {result.ShutdownReason}");
            Debug.LogError($"[FusionBootstrap] JoinLobby failed: {result.ShutdownReason}");
        }
    }

    public void CreateRoom(string requestedRoomName)
    {
        if (isBusy)
            return;

        string roomName = BuildRoomName(requestedRoomName);

        if (IsDuplicateRoomName(roomName))
        {
            StatusChanged?.Invoke($"Room already exists: {roomName}");
            return;
        }

        StartSession(GameMode.Host, roomName);
    }

    public void JoinRoom(string roomName)
    {
        if (isBusy)
            return;

        if (string.IsNullOrWhiteSpace(roomName))
        {
            StatusChanged?.Invoke("Room name is empty.");
            return;
        }

        StartSession(GameMode.Client, roomName);
    }

    public void JoinRoom(SessionInfo session)
    {
        if (session == null)
            return;

        if (!session.IsOpen || session.PlayerCount >= session.MaxPlayers)
        {
            StatusChanged?.Invoke("This room is full or closed.");
            return;
        }

        JoinRoom(session.Name);
    }

    private async void StartSession(GameMode mode, string roomName)
    {
        CreateRunnerIfNeeded();

        isBusy = true;
        StatusChanged?.Invoke(mode == GameMode.Host
            ? $"Creating room: {roomName}"
            : $"Joining room: {roomName}");

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

        var args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            CustomLobbyName = customLobbyName,
            PlayerCount = maxPlayersPerRoom,
            IsOpen = true,
            IsVisible = true,
            Scene = scene,
            SceneManager = sceneManager,
            ObjectProvider = objectProvider
        };

        // Client가 선택한 방이 사라졌을 때 새 방을 만들어버리는 것을 방지
        if (mode == GameMode.Client)
            args.EnableClientSessionCreation = false;

        var result = await runner.StartGame(args);

        isBusy = false;

        if (result.Ok)
        {
            isInLobby = false;
            StatusChanged?.Invoke($"Connected: {roomName}");

            GameSessionStarted?.Invoke();
        }
        else
        {
            StatusChanged?.Invoke($"Connection failed: {result.ShutdownReason}");
            Debug.LogError($"[FusionBootstrap] StartSession failed: {result.ShutdownReason}");
        }
    }

    private string BuildRoomName(string requestedRoomName)
    {
        if (!string.IsNullOrWhiteSpace(requestedRoomName))
            return requestedRoomName.Trim();

        int random = UnityEngine.Random.Range(1000, 9999);
        return $"{defaultRoomPrefix}_{random}";
    }

    private bool IsDuplicateRoomName(string roomName)
    {
        foreach (SessionInfo session in cachedSessions)
        {
            if (session != null && session.Name == roomName)
                return true;
        }

        return false;
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessions.Clear();

        foreach (SessionInfo session in sessionList)
        {
            if (session == null)
                continue;

            // Last Round 로비에서 보여줄 수 있는 방만 캐싱
            cachedSessions.Add(session);
        }

        SessionListUpdated?.Invoke(cachedSessions);
        StatusChanged?.Invoke($"Rooms found: {cachedSessions.Count}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        int slot = spawnedPlayers.Count;
        Vector3 spawnPos = slot == 0
            ? new Vector3(-3f, 1f, 0f)
            : new Vector3(3f, 1f, 0f);

        NetworkObject playerObj = runner.Spawn(
            playerPrefab,
            spawnPos,
            Quaternion.identity,
            player,
            (runner, obj) =>
            {
                PlayerNetwork pn = obj.GetComponent<PlayerNetwork>();
                if (pn != null)
                    pn.ServerInitialize((byte)slot);
            }
        );

        runner.SetPlayerObject(player, playerObj);

        spawnedPlayers.Add(player, playerObj);

        UpdateRoomAvailability();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            runner.Despawn(obj);
            spawnedPlayers.Remove(player);
        }

        runner.SetPlayerObject(player, null);

        UpdateRoomAvailability();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        GameplayInput data = new GameplayInput();

        bool blockGameplayInput =
            GameManager.Instance != null &&
            GameManager.Instance.BlocksGameplayInput;

        bool isPlaying =
            GameManager.Instance != null &&
            GameManager.Instance.CurrentPhase == MatchPhase.Playing;

        // Playing 상태가 아니거나 UI 상태면 빈 입력만 전달
        if (!isPlaying || blockGameplayInput)
        {
            input.Set(data);
            ClearBufferedInput();
            return;
        }

        Vector2 move = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) move.y += 1f;
        if (Input.GetKey(KeyCode.S)) move.y -= 1f;
        if (Input.GetKey(KeyCode.D)) move.x += 1f;
        if (Input.GetKey(KeyCode.A)) move.x -= 1f;


        data.Move = Vector2.ClampMagnitude(move, 1f);

        data.Look = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        Camera cam = Camera.main;
        if (cam != null)
        {
            data.AimOrigin = cam.transform.position;
            data.AimDirection = cam.transform.forward;
        }
        else
        {
            data.AimOrigin = Vector3.zero;
            data.AimDirection = Vector3.forward;
        }

        NetworkButtons buttons = default;

        buttons.Set(EInputButton.Fire, Input.GetMouseButton(0));
        buttons.Set(EInputButton.AltFire, Input.GetMouseButton(1));
        buttons.Set(EInputButton.Dash, dashPressed);
        buttons.Set(EInputButton.Jump, jumpPressed);
        buttons.Set(EInputButton.Ability, abilityPressed);
        buttons.Set(EInputButton.Reload, reloadPressed);

        buttons.Set(EInputButton.ConfirmAugment1, aug1Pressed);
        buttons.Set(EInputButton.ConfirmAugment2, aug2Pressed);
        buttons.Set(EInputButton.ConfirmAugment3, aug3Pressed);

        data.Buttons = buttons;

        input.Set(data);

        ClearBufferedInput();
    }

    private void ClearBufferedInput()
    {
        dashPressed = false;
        jumpPressed = false;
        abilityPressed = false;
        reloadPressed = false;

        aug1Pressed = false;
        aug2Pressed = false;
        aug3Pressed = false;
    }

    private void UpdateRoomAvailability()
    {
        if (runner == null)
            return;

        if (!runner.IsServer)
            return;

        if (!runner.SessionInfo.IsValid)
            return;

        bool waitingForOpponent =
            MatchManager.Instance == null ||
            MatchManager.Instance.CurrentPhase == MatchPhase.Lobby;

        bool canJoin = waitingForOpponent && spawnedPlayers.Count < maxPlayersPerRoom;

        runner.SessionInfo.IsOpen = canJoin;
        runner.SessionInfo.IsVisible = canJoin;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}