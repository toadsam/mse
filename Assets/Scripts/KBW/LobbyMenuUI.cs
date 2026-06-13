using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI controller for creating, refreshing, and joining Photon lobby rooms.
public class LobbyMenuUI : MonoBehaviour
{
    [Header("References")]
    // Network bootstrap used to request lobby and room operations.
    [SerializeField] private FusionBootstrap bootstrap;

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Create Room")]
    // Input and button used to create a new room.
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;

    [Header("Room List")]
    [SerializeField] private Button refreshButton;
    // Container and item prefab used to render the room list.
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

    // Subscribes to lobby events and joins the lobby when the UI opens.
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

    // Rebuilds visible room buttons from the latest session list.
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

    // Called when returning to the lobby after a match.
    // It re-enables the lobby root that HideLobby() disabled.
    // Rejoining the lobby is handled by FusionBootstrap after cleanup.
    // Reactivates the lobby root after returning from a match.
    public void ShowLobby()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);
        else if (root == null && !gameObject.activeSelf)
            gameObject.SetActive(true);

        // Do not call JoinLobby() here; FusionBootstrap schedules it after cleanup.
    }

    // Clears stale room items while the lobby is reconnecting.
    public void ClearRoomList()
    {
        if (roomListContent == null)
            return;

        for (int i = roomListContent.childCount - 1; i >= 0; i--)
            Destroy(roomListContent.GetChild(i).gameObject);

        SetStatus("Disconnected. Reconnecting to lobby...");
    }
}
