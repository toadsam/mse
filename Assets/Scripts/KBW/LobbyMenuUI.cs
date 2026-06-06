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
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClicked);
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

        foreach (SessionInfo session in sessions)
        {
            if (session == null)
                continue;

            // �� �� �浵 �����ְ� ������ �� if���� �����ص� �˴ϴ�.
            if (!session.IsOpen || session.PlayerCount >= session.MaxPlayers)
                continue;

            LobbyRoomItemUI item = Instantiate(roomItemPrefab, roomListContent);
            item.Bind(session, bootstrap.JoinRoom);
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
}