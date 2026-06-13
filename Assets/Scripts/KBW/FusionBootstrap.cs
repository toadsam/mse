using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// Creates and manages the Photon Fusion runner, lobby flow, room sessions, and cleanup.
public class FusionBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network")]
    // Networked player prefab spawned when a player joins a room.
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Lobby")]
    // Lobby settings used for room discovery and 1v1 room creation.
    [SerializeField] private string customLobbyName = "LastRound_Lobby";
    [SerializeField] private int maxPlayersPerRoom = 2;
    [SerializeField] private string defaultRoomPrefix = "LastRound";

    [Header("Object Pool")]
    // Optional object provider used to pool spawned network objects.
    [SerializeField] private PooledNetworkObjectProvider objectProvider;
    [SerializeField] private int maxPooledObjectsPerPrefab = 64;

    [Header("Return Flow")]
    [SerializeField] private bool reloadSceneWhenReturningToLobby = true;

    private bool pendingSceneReloadForLobby;

    [SerializeField] private bool autoRejoinLobbyAfterReturn = true;
    private Coroutine rejoinLobbyRoutine;

    // Active Fusion runner for the lobby or current game session.
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    public event Action GameSessionStarted;

    // Maps connected players to their spawned network player objects.
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<SessionInfo> cachedSessions = new();

    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;

    // UI events used by lobby screens to update room list and status text.
    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;
    public event Action<string> StatusChanged;

    public event Action ReturnedToLobby;

    private bool isBusy;
    private bool isInLobby;

    private Coroutine returnToLobbyRoutine;
    private bool isReturningToLobby;

    private bool isGameSessionActive;
    private bool isApplicationQuitting;

    private static int cleanLobbySceneBuildIndex = -1;
    private static bool cleanLobbyReloading;

    // Buffered one-frame input buttons sent through Fusion input polling.
    private bool dashPressed;
    private bool abilityPressed;
    private bool reloadPressed;
    private bool aug1Pressed;
    private bool aug2Pressed;
    private bool aug3Pressed;
    private bool jumpPressed;

    private void Awake()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (cleanLobbySceneBuildIndex < 0 && scene.buildIndex >= 0)
        {
            cleanLobbySceneBuildIndex = scene.buildIndex;
            Debug.Log($"[FusionBootstrap] Cached clean lobby scene. Index={cleanLobbySceneBuildIndex}, Name={scene.name}");
        }
    }

    private void Start()
    {
        GameManager.Instance?.RegisterBootstrap(this);
    }

    private void Update()
    {
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

    // Creates a NetworkRunner and required helpers if none are active.
    private bool CreateRunnerIfNeeded()
    {
        if (runner != null)
            return true;

        NetworkRunner existingRunner = GetComponent<NetworkRunner>();
        if (existingRunner != null)
        {
            Debug.LogWarning("[FusionBootstrap] NetworkRunner still exists. Wait before creating a new runner.");
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

    // Connects this client to the custom Fusion lobby.
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
            isGameSessionActive = false;

            StatusChanged?.Invoke("Lobby connected. Select a room or create one.");
        }
        else
        {
            StatusChanged?.Invoke($"Failed to join lobby: {result.ShutdownReason}");
            Debug.LogError($"[FusionBootstrap] JoinLobby failed: {result.ShutdownReason}");
        }
    }

    // Starts a host session with a unique room name.
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

    // Joins an existing room by name as a client.
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

    // Starts a Fusion host or client session for the selected room.
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

        if (mode == GameMode.Client)
            args.EnableClientSessionCreation = false;

        var result = await runner.StartGame(args);

        isBusy = false;

        if (result.Ok)
        {
            isInLobby = false;
            isGameSessionActive = true;

            StatusChanged?.Invoke($"Connected: {roomName}");
            GameSessionStarted?.Invoke();
        }
        else
        {
            StatusChanged?.Invoke($"Connection failed: {result.ShutdownReason}");
            Debug.LogError($"[FusionBootstrap] StartSession failed: {result.ShutdownReason}");
        }
    }

    public void ReturnToLobby()
    {
        ShutdownAndReturnToLobby("Match finished. Returning to lobby...");
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

            cachedSessions.Add(session);
        }

        SessionListUpdated?.Invoke(cachedSessions);
        StatusChanged?.Invoke($"Rooms found: {cachedSessions.Count}");
    }

    // Server callback that spawns and initializes a player object.
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

    // Handles player disconnects and returns to lobby if a match was active.
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

    // Collects local keyboard, mouse, and buffered button input for Fusion.
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        GameplayInput data = new GameplayInput();

        bool blockGameplayInput =
            GameManager.Instance != null &&
            GameManager.Instance.BlocksGameplayInput;

        bool isPlaying =
            GameManager.Instance != null &&
            GameManager.Instance.CurrentPhase == MatchPhase.Playing;

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

    // Opens or hides the room depending on player count and match state.
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

    // Schedules an automatic return to the lobby after the match result screen.
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

    // Shuts down the current runner and restores the lobby flow.
    public async void ShutdownAndReturnToLobby(string message)
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;
        pendingSceneReloadForLobby = reloadSceneWhenReturningToLobby;

        Debug.Log($"[FusionBootstrap] ShutdownAndReturnToLobby: {message}");

        if (returnToLobbyRoutine != null)
        {
            StopCoroutine(returnToLobbyRoutine);
            returnToLobbyRoutine = null;
        }

        StatusChanged?.Invoke(message);

        NetworkRunner oldRunner = runner;

        if (oldRunner == null)
        {
            spawnedPlayers.Clear();
            cachedSessions.Clear();
            isBusy = false;
            isInLobby = false;
            ClearBufferedInput();

            GameManager.Instance?.ClearNetworkSessionState();

            if (pendingSceneReloadForLobby)
            {
                pendingSceneReloadForLobby = false;
                isReturningToLobby = false;
                StartCoroutine(ReloadCurrentSceneForCleanLobby());
                return;
            }

            ReturnToLobbyUI(message);
            isReturningToLobby = false;
            ScheduleRejoinLobbyAfterCleanup();
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

            bool wasGameSessionActive = isGameSessionActive;

            bool shouldReloadScene =
                pendingSceneReloadForLobby ||
                (!isApplicationQuitting && wasGameSessionActive);

            CleanupRunner(oldRunner);

            isReturningToLobby = false;

            if (shouldReloadScene)
            {
                pendingSceneReloadForLobby = false;
                StartCoroutine(ReloadCurrentSceneForCleanLobby());
                yield break;
            }

            ReturnToLobbyUI(message);
            ScheduleRejoinLobbyAfterCleanup();
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner callbackRunner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[FusionBootstrap] OnShutdown: {shutdownReason}");

        bool wasGameSessionActive = isGameSessionActive;

        bool shouldReloadScene =
            pendingSceneReloadForLobby ||
            (!isApplicationQuitting && wasGameSessionActive);

        CleanupRunner(callbackRunner);

        isReturningToLobby = false;

        if (shouldReloadScene)
        {
            pendingSceneReloadForLobby = false;
            StartCoroutine(ReloadCurrentSceneForCleanLobby());
            return;
        }

        ReturnToLobbyUI($"Disconnected: {shutdownReason}");
        ScheduleRejoinLobbyAfterCleanup();
    }


    public void OnDisconnectedFromServer(NetworkRunner callbackRunner, NetDisconnectReason reason)
    {
        Debug.Log($"[FusionBootstrap] OnDisconnectedFromServer: {reason}");

        bool wasGameSessionActive = isGameSessionActive;

        bool shouldReloadScene =
            pendingSceneReloadForLobby ||
            (!isApplicationQuitting && wasGameSessionActive);

        CleanupRunner(callbackRunner);

        isReturningToLobby = false;

        if (shouldReloadScene)
        {
            pendingSceneReloadForLobby = false;
            StartCoroutine(ReloadCurrentSceneForCleanLobby());
            return;
        }

        ReturnToLobbyUI($"Disconnected: {reason}");
        ScheduleRejoinLobbyAfterCleanup();
    }

    public void OnConnectedToServer(NetworkRunner runner) { }

    // Clears runner state, callbacks, pooled objects, and runtime leftovers.
    private void CleanupRunner(NetworkRunner callbackRunner)
    {
        Debug.Log("[FusionBootstrap] CleanupRunner");

        if (callbackRunner != null)
            callbackRunner.RemoveCallbacks(this);

        if (runner == callbackRunner)
            runner = null;

        if (callbackRunner != null)
            Destroy(callbackRunner);

        // Remove the SceneManager component attached to the old runner.
        DestroySceneManagerComponent();

        if (objectProvider == null)
            objectProvider = GetComponent<PooledNetworkObjectProvider>();

        if (objectProvider != null)
            objectProvider.ClearPool();

        DestroyLeftoverRuntimeObjects();

        spawnedPlayers.Clear();
        cachedSessions.Clear();

        isBusy = false;
        isInLobby = false;
        isGameSessionActive = false;

        ClearBufferedInput();

        GameManager.Instance?.ClearNetworkSessionState();
    }

    private void DestroySceneManagerComponent()
    {
        NetworkSceneManagerDefault[] managers =
            GetComponents<NetworkSceneManagerDefault>();

        foreach (NetworkSceneManagerDefault manager in managers)
        {
            if (manager != null)
                Destroy(manager);
        }

        sceneManager = null;

        Debug.Log("[FusionBootstrap] NetworkSceneManagerDefault destroyed.");
    }

    // Restores menu and lobby UI after network shutdown.
    private void ReturnToLobbyUI(string message)
    {
        Debug.Log($"[FusionBootstrap] ReturnToLobbyUI: {message}");

        GameManager.Instance?.ClearNetworkSessionState();
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

        LobbyMenuUI lobby =
            FindFirstObjectByType<LobbyMenuUI>(FindObjectsInactive.Include);

        if (lobby != null)
        {
            lobby.ClearRoomList();
            lobby.ShowLobby();
        }
        else
        {
            Debug.LogWarning("[FusionBootstrap] LobbyMenuUI not found.");
        }

        StatusChanged?.Invoke(message);

        ReturnedToLobby?.Invoke();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;

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
        yield return null;
        yield return null;

        rejoinLobbyRoutine = null;

        if (runner != null)
            yield break;

        if (isBusy)
            yield break;

        NetworkRunner existingRunner = GetComponent<NetworkRunner>();
        NetworkSceneManagerDefault existingSceneManager =
            GetComponent<NetworkSceneManagerDefault>();

        if (existingRunner != null || existingSceneManager != null)
        {
            Debug.LogWarning(
                "[FusionBootstrap] Old network components still exist. Waiting one more frame before rejoining lobby."
            );

            ScheduleRejoinLobbyAfterCleanup();
            yield break;
        }

        Debug.Log("[FusionBootstrap] Rejoining lobby after runner cleanup.");
        JoinLobby();
    }

    // Reloads the original lobby scene to remove leftover match state.
    private IEnumerator ReloadCurrentSceneForCleanLobby()
    {
        if (cleanLobbyReloading)
            yield break;

        cleanLobbyReloading = true;

        Scene activeScene = SceneManager.GetActiveScene();

        int sceneIndex = cleanLobbySceneBuildIndex >= 0
            ? cleanLobbySceneBuildIndex
            : activeScene.buildIndex;

        Debug.Log(
            $"[FusionBootstrap] Loading clean lobby scene. " +
            $"TargetIndex={sceneIndex}, ActiveScene={activeScene.name}, ActiveIndex={activeScene.buildIndex}"
        );

        if (sceneIndex < 0)
        {
            cleanLobbyReloading = false;

            Debug.LogError("[FusionBootstrap] Cannot reload scene because sceneIndex is invalid.");

            ReturnToLobbyUI("Disconnected. Failed to reload scene.");
            ScheduleRejoinLobbyAfterCleanup();
            yield break;
        }

        SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);

        cleanLobbyReloading = false;
        yield break;
    }

    private void DestroyLeftoverRuntimeObjects()
    {
        DestroyLeftoverObjectsOfType<PlayerNetwork>();
        DestroyLeftoverObjectsOfType<RifleProjectile>();
        DestroyLeftoverObjectsOfType<ThrowableItemProjectile>();
        DestroyLeftoverObjectsOfType<SmokeZone>();
        DestroyLeftoverObjectsOfType<NetworkTimedVfx>();
        DestroyLeftoverObjectsOfType<ThrowingAxeProjectile>();
    }

    private void DestroyLeftoverObjectsOfType<T>() where T : Component
    {
        T[] objects = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (T obj in objects)
        {
            if (obj == null)
                continue;

            Destroy(obj.gameObject);
        }
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