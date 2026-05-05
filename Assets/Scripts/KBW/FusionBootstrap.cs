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
    [SerializeField] private string sessionName = "LastRound_TestRoom";

    private NetworkRunner runner;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    // 탭 입력 누락 방지용
    private bool dashPressed;
    private bool abilityPressed;
    private bool reloadPressed;
    private bool aug1Pressed;
    private bool aug2Pressed;
    private bool aug3Pressed;
    private bool jumpPressed;

    private async void StartGame(GameMode mode)
    {
        if (runner != null)
            return;

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

        await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private void OnGUI()
    {
        if (runner != null)
            return;

        if (GUI.Button(new Rect(10, 10, 160, 40), "Host"))
            StartGame(GameMode.Host);

        if (GUI.Button(new Rect(10, 60, 160, 40), "Join"))
            StartGame(GameMode.Client);
    }

    private void Start()
    {
        GameManager.Instance?.RegisterBootstrap(this);
    }

    private void Update()
    {
        // 한 번 눌림만 필요한 입력은 Update에서 누적
        jumpPressed |= Input.GetKeyDown(KeyCode.Space);
        dashPressed |= Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        abilityPressed |= Input.GetKeyDown(KeyCode.Q);
        reloadPressed |= Input.GetKeyDown(KeyCode.R);

        // 능력 선택은 일단 마우스 입력으로만 처리, 나중에 키보드 입력도 추가할 수 있음
        aug1Pressed |= Input.GetKeyDown(KeyCode.Alpha1);
        aug2Pressed |= Input.GetKeyDown(KeyCode.Alpha2);
        aug3Pressed |= Input.GetKeyDown(KeyCode.Alpha3);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        GameplayInput data = new GameplayInput();

        bool blockGameplay = GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput;

        if (blockGameplay)
        {
            data.Move = Vector2.zero;
            data.Look = Vector2.zero;

            data.Buttons.Set(EInputButton.Fire, false);
            data.Buttons.Set(EInputButton.AltFire, false);
            data.Buttons.Set(EInputButton.Dash, false);
            data.Buttons.Set(EInputButton.Jump, false);
            data.Buttons.Set(EInputButton.Ability, false);
            data.Buttons.Set(EInputButton.Reload, false);

            input.Set(data);

            jumpPressed = false;
            dashPressed = false;
            abilityPressed = false;
            reloadPressed = false;
            aug1Pressed = false;
            aug2Pressed = false;
            aug3Pressed = false;
            return;
        }


        Vector2 move = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) move.y += 1f;
        if (Input.GetKey(KeyCode.S)) move.y -= 1f;
        if (Input.GetKey(KeyCode.A)) move.x -= 1f;
        if (Input.GetKey(KeyCode.D)) move.x += 1f;
        data.Move = move;

        // 마우스 이동도 네트워크 입력으로 전달
        data.Look = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        // 유지형 입력
        data.Buttons.Set(EInputButton.Fire, Input.GetMouseButton(0));
        data.Buttons.Set(EInputButton.AltFire, Input.GetMouseButton(1));

        // 탭형 입력
        data.Buttons.Set(EInputButton.Dash, dashPressed);
        data.Buttons.Set(EInputButton.Jump, jumpPressed);
        data.Buttons.Set(EInputButton.Ability, abilityPressed);
        data.Buttons.Set(EInputButton.Reload, reloadPressed);
        data.Buttons.Set(EInputButton.ConfirmAugment1, aug1Pressed);
        data.Buttons.Set(EInputButton.ConfirmAugment2, aug2Pressed);
        data.Buttons.Set(EInputButton.ConfirmAugment3, aug3Pressed);

        input.Set(data);

        // 이번 프레임 입력 전달 후 리셋
        jumpPressed = false;
        dashPressed = false;
        abilityPressed = false;
        reloadPressed = false;
        aug1Pressed = false;
        aug2Pressed = false;
        aug3Pressed = false;
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
            player
        );

        runner.SetPlayerObject(player, playerObj);

        PlayerNetwork pn = playerObj.GetComponent<PlayerNetwork>();
        if (pn != null)
            pn.ServerInitialize((byte)slot);

        spawnedPlayers.Add(player, playerObj);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            runner.Despawn(obj);
            spawnedPlayers.Remove(player);
        }
        runner.SetPlayerObject(player, null);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}