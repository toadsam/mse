using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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

    [SerializeField] private bool autoRejoinLobbyAfterReturn = true;
    private Coroutine rejoinLobbyRoutine;

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    public event Action GameSessionStarted;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<SessionInfo> cachedSessions = new();

    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;

    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;
    public event Action<string> StatusChanged;

    // ë§¤ì¹˜ ì¢…ë£Œ í›„ ë£¸ì„ ë– ë‚˜ ë¡œë¹„ë¡œ ë³µê·€í–ˆìŒì„ ì•Œë¦°ë‹¤(UIê°€ ë©”ë‰´/ë¡œë¹„ íŒ¨ë„ì„ ë‹¤ì‹œ í‘œì‹œ).
    public event Action ReturnedToLobby;

    private bool isBusy;
    private bool isInLobby;

    private Coroutine returnToLobbyRoutine;
    private bool isReturningToLobby;


    // ï¿½ï¿½ ï¿½Ô·ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
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
        // UI ï¿½ï¿½ï¿½Â¿ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½Ã·ï¿½ï¿½ï¿½ ï¿½Ô·ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½
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

    private bool CreateRunnerIfNeeded()
    {
        if (runner != null)
            return true;

        NetworkRunner existingRunner = GetComponent<NetworkRunner>();
        if (existingRunner != null)
        {
            Debug.LogWarning("[FusionBootstrap] NetworkRunner component is still on this object. Wait before creating a new runner.");
            return false;
        }

        runner = gameObject.AddComponent<NetworkRunner>();

        if (runner == null)
        {
            Debug.LogError("[FusionBootstrap] Failed to create NetworkRunner.");
            return false;
        }

        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        if (sceneManager == null)
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        if (objectProvider == null)
            objectProvider = GetComponent<PooledNetworkObjectProvider>();

        if (objectProvider == null)
            objectProvider = gameObject.AddComponent<PooledNetworkObjectProvider>();

        objectProvider.SetMaxPoolCount(maxPooledObjectsPerPrefab);

        return true;
    }

    public async void JoinLobby()
    {
        if (isBusy)
            return;

        if (!CreateRunnerIfNeeded())
        {
            ScheduleRejoinLobbyAfterCleanup();
            return;
        }

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
        if (!CreateRunnerIfNeeded())
        {
            StatusChanged?.Invoke("Network cleanup is still running. Please wait a moment.");
            return;
        }

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

        // Clientï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½
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

    // ë§¤ì¹˜ ì¢…ë£Œ í›„ í˜¸ì¶œ. í˜„ì¬ Photon ë£¸ì„ ë– ë‚˜ê³  ë¡œë¹„ ìƒíƒœë¡œ ë˜ëŒë¦°ë‹¤.
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

        // runner ì¢…ë£Œê°€ ëë‚œ ë’¤ì— ì•Œë¦°ë‹¤. êµ¬ë…ì(UI)ê°€ ë¡œë¹„ íŒ¨ë„ì„ ë‹¤ì‹œ í™œì„±í™”í•˜ë©´
        // LobbyMenuUI.OnEnableì´ ìƒˆ runnerë¡œ JoinLobbyë¥¼ í˜¸ì¶œí•œë‹¤.
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

            // Last Round ï¿½Îºñ¿¡¼ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ ï¿½Ö´ï¿½ ï¿½æ¸¸ Ä³ï¿½ï¿½
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

    public void OnPlayerLeft(NetworkRunner callbackRunner, PlayerRef player)
    {
        Debug.Log($"[FusionBootstrap] OnPlayerLeft: {player}");

        bool wasInMatch =
            MatchManager.Instance != null &&
            MatchManager.Instance.CurrentPhase != MatchPhase.Lobby;

        Debug.Log($"[FusionBootstrap] OnPlayerLeft / wasInMatch={wasInMatch}");

        if (spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            callbackRunner.Despawn(obj);
            spawnedPlayers.Remove(player);
        }

        callbackRunner.SetPlayerObject(player, null);

        if (wasInMatch)
        {
            ShutdownAndReturnToLobby("Disconnected. Returning to lobby...");
            return;
        }

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

        // Playing ï¿½ï¿½ï¿½Â°ï¿½ ï¿½Æ´Ï°Å³ï¿½ UI ï¿½ï¿½ï¿½Â¸ï¿½ ï¿½ï¿½ ï¿½Ô·Â¸ï¿½ ï¿½ï¿½ï¿½ï¿½
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

    public void ReturnToLobbyAfter(float seconds, string message)
    {
        Debug.Log($"[FusionBootstrap] ReturnToLobbyAfter scheduled. Seconds={seconds}, Message={message}");

        if (returnToLobbyRoutine != null)
        {
            StopCoroutine(returnToLobbyRoutine);
            returnToLobbyRoutine = null;
        }

        returnToLobbyRoutine = StartCoroutine(ReturnToLobbyAfterRoutine(seconds, message));
    }

    private IEnumerator ReturnToLobbyAfterRoutine(float seconds, string message)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));

        Debug.Log("[FusionBootstrap] ReturnToLobbyAfterRoutine expired.");

        returnToLobbyRoutine = null;
        ShutdownAndReturnToLobby(message);
    }

    public async void ShutdownAndReturnToLobby(string message)
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        Debug.Log($"[FusionBootstrap] ShutdownAndReturnToLobby: {message}");

        if (returnToLobbyRoutine != null)
        {
            StopCoroutine(returnToLobbyRoutine);
            returnToLobbyRoutine = null;
        }

        // ¸ÕÀú È­¸éÀ» ·Îºñ·Î µ¹¸³´Ï´Ù.
        ReturnToLobbyUI(message);

        NetworkRunner oldRunner = runner;

        if (oldRunner == null)
        {
            spawnedPlayers.Clear();
            cachedSessions.Clear();
            isBusy = false;
            isInLobby = false;
            ClearBufferedInput();

            isReturningToLobby = false;
            return;
        }

        await oldRunner.Shutdown();

        StartCoroutine(ShutdownFallbackCheck(oldRunner, message));
    }

    private IEnumerator ShutdownFallbackCheck(NetworkRunner oldRunner, string message)
    {
        yield return null;

        if (runner == oldRunner)
        {
            Debug.LogWarning("[FusionBootstrap] OnShutdown fallback cleanup.");
            CleanupRunner(oldRunner);
            ReturnToLobbyUI(message);
            isReturningToLobby = false;

            ScheduleRejoinLobbyAfterCleanup();
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner callbackRunner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[FusionBootstrap] OnShutdown: {shutdownReason}");

        CleanupRunner(callbackRunner);
        ReturnToLobbyUI($"Disconnected: {shutdownReason}");

        isReturningToLobby = false;

        ScheduleRejoinLobbyAfterCleanup();
    }

    public void OnDisconnectedFromServer(NetworkRunner callbackRunner, NetDisconnectReason reason)
    {
        Debug.Log($"[FusionBootstrap] OnDisconnectedFromServer: {reason}");

        CleanupRunner(callbackRunner);
        ReturnToLobbyUI($"Disconnected: {reason}");

        isReturningToLobby = false;

        ScheduleRejoinLobbyAfterCleanup();
    }

    public void OnConnectedToServer(NetworkRunner runner) { }

    private void CleanupRunner(NetworkRunner callbackRunner)
    {
        Debug.Log("[FusionBootstrap] CleanupRunner");

        if (callbackRunner != null)
            callbackRunner.RemoveCallbacks(this);

        if (runner == callbackRunner)
            runner = null;

        if (callbackRunner != null)
            Destroy(callbackRunner);

        spawnedPlayers.Clear();
        cachedSessions.Clear();

        isBusy = false;
        isInLobby = false;

        ClearBufferedInput();
    }

    private void ReturnToLobbyUI(string message)
    {
        Debug.Log($"[FusionBootstrap] ReturnToLobbyUI: {message}");

        GameManager.Instance?.SetMenuCursor();

        MainMenuFlowUI menu =
            FindFirstObjectByType<MainMenuFlowUI>(FindObjectsInactive.Include);

        if (menu == null)
        {
            Debug.LogError("[FusionBootstrap] MainMenuFlowUI not found.");
        }
        else
        {
            Debug.Log("[FusionBootstrap] MainMenuFlowUI found. Showing lobby.");
            menu.ShowLobbyDirect();
        }

        StatusChanged?.Invoke(message);
    }

    private void OnApplicationQuit()
    {
        if (runner != null)
        {
            Debug.Log("[FusionBootstrap] OnApplicationQuit - shutting down runner.");
            runner.Shutdown();
        }
    }

    private void ScheduleRejoinLobbyAfterCleanup()
    {
        if (!autoRejoinLobbyAfterReturn)
            return;

        if (!isActiveAndEnabled)
            return;

        if (rejoinLobbyRoutine != null)
            StopCoroutine(rejoinLobbyRoutine);

        rejoinLobbyRoutine = StartCoroutine(RejoinLobbyAfterCleanupRoutine());
    }

    private IEnumerator RejoinLobbyAfterCleanupRoutine()
    {
        // Destroy(callbackRunner)°¡ ½ÇÁ¦·Î ¹İ¿µµÉ ½Ã°£À» Áİ´Ï´Ù.
        yield return null;
        yield return null;

        rejoinLobbyRoutine = null;

        if (runner != null)
            yield break;

        if (isBusy)
            yield break;

        // ¾ÆÁ÷ °°Àº GameObject¿¡ NetworkRunner°¡ ³²¾Æ ÀÖÀ¸¸é ÇÑ ¹ø ´õ ±â´Ù¸³´Ï´Ù.
        NetworkRunner existingRunner = GetComponent<NetworkRunner>();
        if (existingRunner != null)
        {
            Debug.LogWarning("[FusionBootstrap] NetworkRunner still exists. Waiting one more frame before rejoining lobby.");
            ScheduleRejoinLobbyAfterCleanup();
            yield break;
        }

        Debug.Log("[FusionBootstrap] Rejoining lobby after runner cleanup.");
        JoinLobby();
    }

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