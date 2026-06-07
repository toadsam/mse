using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 매치 종료 후 Match_Result 패널을 표시한다.
// - 백엔드(MySQL) 저장이 끝나면(성공/실패 무관) 결과를 표시한다.
// - 닉네임 기준 승자/점수, 선택 캐릭터, 선택 augment 이름, 매치 시간을 보여준다.
// - "로비로" 버튼 또는 자동 타이머로 로비에 복귀한다.
public class MatchResultUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Result Texts")]
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text player1ScoreText;
    [SerializeField] private TMP_Text player2ScoreText;
    [SerializeField] private TMP_Text charactersText;
    [SerializeField] private TMP_Text augmentsText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text saveStatusText;

    [Header("Return To Lobby")]
    [SerializeField] private FusionBootstrap bootstrap;
    [SerializeField] private Button returnToLobbyButton;
    [Tooltip("결과 표시 후 자동으로 로비에 복귀하기까지의 시간(초). 0 이하면 자동 복귀 안 함.")]
    [SerializeField] private float autoReturnSeconds = 10f;
    [SerializeField] private TMP_Text autoReturnText;

    [Header("Menu Panels To Restore")]
    [Tooltip("로비 복귀 시 다시 활성화할 메뉴/로비 패널들(MainMenuFlowUI의 lobbyPanel, titleHeader 등).")]
    [SerializeField] private GameObject[] panelsToShowOnReturn;

    [Header("Fallback")]
    [Tooltip("저장이 끝나지 않아도 결과 화면을 강제로 표시하기까지의 최대 대기 시간(초).")]
    [SerializeField] private float showResultTimeoutSeconds = 6f;

    [Header("Loading")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string loadingMessage = "Loading result";
    [SerializeField] private float loadingDotInterval = 0.35f;

    private bool isShown;
    private bool isReturning;
    private float matchResultEnteredTime = -1f;
    private float shownTime = -1f;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<FusionBootstrap>();

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);

        SetVisible(false);
        SetLoadingVisible(false);
    }

    private void Update()
    {
        MatchManager match = GameManager.Instance != null ? GameManager.Instance.Match : null;

        // Runner stopped unexpectedly (disconnect, shutdown) before the user returned to lobby.
        // Trigger ReturnToLobby() so the lobby panel is restored instead of staying on the result screen.
        if (match != null && (match.Runner == null || !match.Runner.IsRunning))
        {
            if (!isReturning)
                ReturnToLobby();
            return;
        }

        if (match == null)
        {
            if (isShown && !isReturning)
                SetVisible(false);

            ResetState();
            return;
        }

        // Access the networked Phase property inside a try-catch.
        // There is a window between Code-104 arrival and runner.IsRunning becoming false
        // during which the NetworkObject is already despawned but IsRunning is still true.
        // Accessing [Networked] properties in that window throws InvalidOperationException.
        MatchPhase currentPhase;
        try
        {
            currentPhase = match.CurrentPhase;
        }
        catch (System.InvalidOperationException)
        {
            // Fusion NetworkObject no longer valid — runner is mid-shutdown.
            if (!isReturning)
                ReturnToLobby();
            return;
        }

        if (currentPhase != MatchPhase.MatchResult)
        {
            if (isShown && !isReturning)
                SetVisible(false);

            SetLoadingVisible(false);
            ResetState();
            return;
        }

        if (matchResultEnteredTime < 0f)
            matchResultEnteredTime = Time.time;

        if (!isShown)
        {
            bool saveResolved = match.ResultSaveResolved;
            bool timedOut = Time.time - matchResultEnteredTime >= showResultTimeoutSeconds;

            if (saveResolved || timedOut)
            {
                SetLoadingVisible(false);
                ShowResult(match);
            }
            else
            {
                SetLoadingVisible(true);
                UpdateLoadingText();
            }

            return;
        }

        UpdateAutoReturnCountdown();
    }

    private void ShowResult(MatchManager match)
    {
        SetLoadingVisible(false);

        isShown = true;
        shownTime = Time.time;

        PlayerNetwork slot0 = match.GetPlayerBySlot(0);
        PlayerNetwork slot1 = match.GetPlayerBySlot(1);

        string name0 = GetNickname(slot0, 1);
        string name1 = GetNickname(slot1, 2);

        // Winner (닉네임)
        if (winnerText != null)
        {
            string winnerName = match.MatchWinnerSlot == 0 ? name0
                : match.MatchWinnerSlot == 1 ? name1
                : "-";
            winnerText.text = $"Winner: {winnerName}";
        }

        // Player1 / Player2 Score (닉네임 기준)
        if (player1ScoreText != null)
            player1ScoreText.text = $"{name0}: {match.Player0Wins}";

        if (player2ScoreText != null)
            player2ScoreText.text = $"{name1}: {match.Player1Wins}";

        // Selected Character
        if (charactersText != null)
        {
            string c0 = GetCharacterName(slot0);
            string c1 = GetCharacterName(slot1);
            charactersText.text = $"{name0}: {c0}\n{name1}: {c1}";
        }

        // Selected Augments (이름)
        if (augmentsText != null)
        {
            string a0 = FormatAugments(match.GetSelectedAugmentNames(slot0));
            string a1 = FormatAugments(match.GetSelectedAugmentNames(slot1));
            augmentsText.text = $"{name0}: {a0}\n{name1}: {a1}";
        }

        // Match Duration
        if (durationText != null)
            durationText.text = $"Duration: {FormatDuration(match.MatchDurationSeconds)}";

        if (saveStatusText != null)
        {
            if (!match.ResultSaveResolved)
                saveStatusText.text = "Checking result...";
            else if (match.ResultSaveSucceeded)
                saveStatusText.text = "Result saved.";
            else
                saveStatusText.text = "Failed to save result.";
        }

        SetVisible(true);
    }

    private void UpdateAutoReturnCountdown()
    {
        if (autoReturnSeconds <= 0f)
        {
            if (autoReturnText != null)
                autoReturnText.gameObject.SetActive(false);
            return;
        }

        float remaining = Mathf.Max(0f, autoReturnSeconds - (Time.time - shownTime));

        if (autoReturnText != null)
        {
            autoReturnText.gameObject.SetActive(true);
            autoReturnText.text = $"Returning to Lobby in {Mathf.CeilToInt(remaining)}s";
        }

        if (remaining <= 0f)
            ReturnToLobby();
    }

    private void OnReturnToLobbyClicked()
    {
        ReturnToLobby();
    }

    private void ReturnToLobby()
    {
        if (isReturning)
            return;

        isReturning = true;
        SetVisible(false);

        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<FusionBootstrap>(FindObjectsInactive.Include);

        if (bootstrap != null)
        {
            bootstrap.ShutdownAndReturnToLobby("Match finished. Returning to lobby...");
        }
        else
        {
            Debug.LogWarning("[MatchResultUI] FusionBootstrap ref missing. Cannot return to lobby.");
            isReturning = false;
        }
    }

    private void ResetState()
    {
        isShown = false;
        matchResultEnteredTime = -1f;
        shownTime = -1f;
        isReturning = false;
    }

    private static string GetNickname(PlayerNetwork player, int slotForFallback)
    {
        if (player == null)
            return $"Player {slotForFallback}";

        string name = player.PlayerName.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"Player {slotForFallback}" : name;
    }

    private static string GetCharacterName(PlayerNetwork player)
    {
        if (player == null)
            return "-";

        string name = player.CharacterDisplayName.ToString();
        return string.IsNullOrWhiteSpace(name) ? "-" : name;
    }

    private static string FormatAugments(List<string> names)
    {
        if (names == null || names.Count == 0)
            return "-";

        return string.Join(", ", names);
    }

    private static string FormatDuration(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int total = Mathf.RoundToInt(seconds);
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes:00}:{secs:00}";
    }

    private void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);
    }

    private void SetLoadingVisible(bool visible)
    {
        if (loadingRoot != null && loadingRoot.activeSelf != visible)
            loadingRoot.SetActive(visible);
    }

    private void UpdateLoadingText()
    {
        if (loadingText == null)
            return;

        int dotCount = Mathf.FloorToInt(Time.unscaledTime / loadingDotInterval) % 4;
        loadingText.text = loadingMessage + new string('.', dotCount);
    }
}
