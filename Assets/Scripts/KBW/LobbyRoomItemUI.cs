using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays one available lobby room and its join button.
public class LobbyRoomItemUI : MonoBehaviour
{
    // UI fields showing room name, occupancy, and join state.
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    // Session data and callback used when the join button is pressed.
    private SessionInfo session;
    private Action<SessionInfo> onJoinClicked;
    private Coroutine introRoutine;

    private void Awake()
    {
        UIAnimationBootstrap.InstallButton(joinButton);
    }

    // Fills this list item with session data and connects the join callback.
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
            UIAnimationBootstrap.InstallButton(joinButton);
        }
    }

    public void PlayIntro(int index)
    {
        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(PlayIntroRoutine(index));
    }

    private void OnJoinButtonClicked()
    {
        onJoinClicked?.Invoke(session);
    }

    private System.Collections.IEnumerator PlayIntroRoutine(int index)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        UIPanelAnimator animator = UIPanelAnimator.Ensure(gameObject);
        if (animator != null)
        {
            animator.ResetBaseTransform();
            animator.Configure(new Vector2(0f, -8f), 0.98f, 0.16f, 0.1f);
            animator.Show(false, Mathf.Min(index * 0.04f, 0.2f));
        }

        introRoutine = null;
    }
}
