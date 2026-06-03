using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRoomItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private SessionInfo session;
    private Action<SessionInfo> onJoinClicked;

    public void Bind(SessionInfo sessionInfo, Action<SessionInfo> joinCallback)
    {
        session = sessionInfo;
        onJoinClicked = joinCallback;

        if (roomNameText != null)
            roomNameText.text = sessionInfo.Name;

        if (playerCountText != null)
            playerCountText.text = $"{sessionInfo.PlayerCount}/{sessionInfo.MaxPlayers}";

        bool canJoin = sessionInfo.IsOpen && sessionInfo.PlayerCount < sessionInfo.MaxPlayers;

        if (joinButton != null)
        {
            joinButton.interactable = canJoin;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinButtonClicked);
        }
    }

    private void OnJoinButtonClicked()
    {
        onJoinClicked?.Invoke(session);
    }
}