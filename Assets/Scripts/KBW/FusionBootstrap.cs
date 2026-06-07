using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    public event Action GameSessionStarted;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<SessionInfo> cachedSessions = new();

    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;

    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;
    public event Action<string> StatusChanged;

    // 매치 종료 후 룸을 떠나 로비로 복귀했음을 알린다(UI가 메뉴/로비 패널을 다시 표시).
    public event Action ReturnedToLobby;

    private bool isBusy;
    private bool isInLobby;


    // �� �Է� ���� ������
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
        // UI ���¿����� �����÷��� �Է��� �������� ����
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
            return;

        dashPressed |= Input.GetKeyDown(KeyCode.LeftShift);
        jumpPressed |= Input.GetKeyDown(KeyCode.Space);
        abilityPressed |= Input.GetKeyDown(KeyCode.Q);
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
            SceneManager = sceneManager
        };

        // Client�� ������ ���� ������� �� �� ���� ���������� ���� ����
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

    // 매치 종료 후 호출. 현재 Photon 룸을 떠나고 로비 상태로 되돌린다.
    public async void ReturnToLobby()
    {
        if (isBusy)
            return;

        isBusy = true;
        StatusChanged?.Invoke("Returning to lobby...");

        if (runner != null)
        {
            try
            {
                // Only call Shutdown() when the runner is still active.
                // If Photon already sent a Code 104 disconnect, runner.IsRunning is false
                // and calling Shutdown() on an internally-shutting-down runner will hang
                // indefinitely, blocking ReturnedToLobby from ever firing.
                if (runner.IsRunning)
                {
                    // destroyGameObject:false is critical. The NetworkRunner is added to
                    // THIS GameObject (the FusionBootstrap), and Shutdown()'s default
                    // destroyGameObject:true would destroy the whole FusionBootstrap object.
                    // That kills the lobby (LobbyMenuUI/MatchResultUI all reference this
                    // bootstrap), so room list / create / join silently stop working and
                    // the game appears frozen. Keep the GameObject; only tear down the runner.
                    var shutdownTask = runner.Shutdown(destroyGameObject: false);
                    // 5-second timeout: guards against hanging when the Photon server
                    // has already initiated disconnect (e.g., Code 104) but IsRunning
                    // hasn't transitioned to false yet.
                    await Task.WhenAny(shutdownTask, Task.Delay(5000));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FusionBootstrap] Runner shutdown error: {e.Message}");
            }
            finally
            {
                // DestroyImmediate (not Destroy) so the old components are gone THIS frame.
                // ReturnedToLobby?.Invoke() below re-enables the lobby panel, whose
                // LobbyMenuUI.OnEnable calls JoinLobby() -> CreateRunnerIfNeeded() within
                // this same call stack. A deferred Destroy would leave the shut-down runner
                // on the GameObject while a new NetworkRunner is added, so two runners would
                // briefly coexist on one GameObject and Fusion would misbehave.
                if (runner != null)
                {
                    runner.RemoveCallbacks(this);
                    DestroyImmediate(runner);
                    runner = null;
                }
                if (sceneManager != null)
                {
                    DestroyImmediate(sceneManager);
                    sceneManager = null;
                }
            }
        }

        spawnedPlayers.Clear();
        cachedSessions.Clear();
        isInLobby = false;
        isBusy = false;

        // runner 종료가 끝난 뒤에 알린다. 구독자(UI)가 로비 패널을 다시 활성화하면
        // LobbyMenuUI.OnEnable이 새 runner로 JoinLobby를 호출한다.
        ReturnedToLobby?.Invoke();
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

            // Last Round �κ񿡼� ������ �� �ִ� �游 ĳ��
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

        // Playing ���°� �ƴϰų� UI ���¸� �� �Է¸� ����
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