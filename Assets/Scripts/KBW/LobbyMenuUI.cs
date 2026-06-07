using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FusionBootstrap bootstrap;

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Create Room")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;

    [Header("Room List")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private LobbyRoomItemUI roomItemPrefab;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<FusionBootstrap>();

        if (createRoomButton != null)
        {
            UIAnimationBootstrap.InstallButton(createRoomButton);
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        }

        if (refreshButton != null)
        {
            UIAnimationBootstrap.InstallButton(refreshButton);
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }
    }

    private void OnEnable()
    {
        if (bootstrap == null)
            return;

        bootstrap.SessionListUpdated += RefreshRoomList;
        bootstrap.StatusChanged += SetStatus;
        bootstrap.GameSessionStarted += HideLobby;

        bootstrap.JoinLobby();
    }

    private void OnDisable()
    {
        if (bootstrap == null)
            return;

        bootstrap.SessionListUpdated -= RefreshRoomList;
        bootstrap.StatusChanged -= SetStatus;
        bootstrap.GameSessionStarted -= HideLobby;
    }

    private void OnCreateRoomClicked()
    {
        if (bootstrap == null)
            return;

        string roomName = roomNameInput != null ? roomNameInput.text : "";
        bootstrap.CreateRoom(roomName);
    }

    private void OnRefreshClicked()
    {
        if (bootstrap == null)
            return;

        bootstrap.JoinLobby();
        RefreshRoomList(bootstrap.CachedSessions);
    }

    private void RefreshRoomList(IReadOnlyList<SessionInfo> sessions)
    {
        if (roomListContent == null || roomItemPrefab == null)
            return;

        for (int i = roomListContent.childCount - 1; i >= 0; i--)
            Destroy(roomListContent.GetChild(i).gameObject);

        if (sessions == null || sessions.Count == 0)
        {
            SetStatus("No rooms found. Create a room first.");
            return;
        }

        SetStatus("Select the rooms for Play!");

        int visibleIndex = 0;

        foreach (SessionInfo session in sessions)
        {
            if (session == null)
                continue;

            if (!session.IsOpen || session.PlayerCount >= session.MaxPlayers)
                continue;

            LobbyRoomItemUI item = Instantiate(roomItemPrefab, roomListContent);
            item.Bind(session, bootstrap.JoinRoom);
            item.PlayIntro(visibleIndex);
            visibleIndex++;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void HideLobby()
    {
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    // 매치 종료 후 로비로 복귀할 때 호출한다.
    // HideLobby()가 끈 LobbyRoot(root)를 다시 켜고, 새 NetworkRunner로 로비에 재접속한다.
    // LobbyMenuUI는 항상 active인 부모(LobbyPanel)에 붙어 있어 root만 다시 켜도
    // OnEnable이 재발화되지 않으므로, 여기서 JoinLobby를 직접 호출해야 룸 리스트가 갱신된다.
    public void ShowLobby()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);
        else if (root == null && !gameObject.activeSelf)
            gameObject.SetActive(true);

        // 여기서 JoinLobby()를 직접 호출하지 않습니다.
        // FusionBootstrap.ScheduleRejoinLobbyAfterCleanup()가 담당합니다.
    }

    public void ClearRoomList()
    {
        if (roomListContent == null)
            return;

        for (int i = roomListContent.childCount - 1; i >= 0; i--)
            Destroy(roomListContent.GetChild(i).gameObject);

        SetStatus("Disconnected. Reconnecting to lobby...");
    }
}
