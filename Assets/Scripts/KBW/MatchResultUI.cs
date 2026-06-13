using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays the Match_Result panel after the match ends.
// It waits for backend save completion or a timeout, then shows winner, score, characters, augments, and duration.
// The player can return to the lobby manually or through the automatic countdown.
public class MatchResultUI : MonoBehaviour
{
    [Header("Root")]
    // Root object for the final result panel.
    [SerializeField] private GameObject root;

    [Header("Result Texts")]
    // Text fields filled with final match result information.
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text player1ScoreText;
    [SerializeField] private TMP_Text player2ScoreText;
    [SerializeField] private TMP_Text charactersText;
    [SerializeField] private TMP_Text augmentsText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text saveStatusText;

    [Header("Return To Lobby")]
    // Bootstrap reference used to shut down the match and return to lobby.
    [SerializeField] private FusionBootstrap bootstrap;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private float autoReturnSeconds = 10f;
    [SerializeField] private TMP_Text autoReturnText;

    [Header("Menu Panels To Restore")]
    [SerializeField] private GameObject[] panelsToShowOnReturn;

    [Header("Fallback")]
    [SerializeField] private float showResultTimeoutSeconds = 6f;

    [Header("Loading")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string loadingMessage = "Loading result";
    [SerializeField] private float loadingDotInterval = 0.35f;

    // UI state flags used to prevent repeated result display or lobby return.
    private bool isShown;
    private bool isReturning;
    private bool loadingVisible;
    private bool loadingVisibilityInitialized;
    private float matchResultEnteredTime = -1f;
    private float shownTime = -1f;
    private UIPanelAnimator resultAnimator;
    private UIPanelAnimator loadingAnimator;
    private Coroutine scoreRoutine;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<FusionBootstrap>();

        if (returnToLobbyButton != null)
        {
            UIAnimationBootstrap.InstallButton(returnToLobbyButton);
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        }

        PrepareAnimators();
        SetVisible(false, true);
        SetLoadingVisible(false, true);
    }

    // Waits for match result data, handles loading UI, and updates auto-return.
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

    // Fills all result texts from the final match state.
    private void ShowResult(MatchManager match)
    {
        SetLoadingVisible(false);

        isShown = true;
        shownTime = Time.time;

        PlayerNetwork slot0 = match.GetPlayerBySlot(0);
        PlayerNetwork slot1 = match.GetPlayerBySlot(1);

        string name0 = GetNickname(slot0, 1);
        string name1 = GetNickname(slot1, 2);

        // Winner nickname.
        if (winnerText != null)
        {
            string winnerName = match.MatchWinnerSlot == 0 ? name0
                : match.MatchWinnerSlot == 1 ? name1
                : "-";
            winnerText.text = $"Winner: {winnerName}";
        }

        // Player scores shown by nickname.
        if (scoreRoutine != null)
            StopCoroutine(scoreRoutine);

        if (player1ScoreText != null)
            player1ScoreText.text = $"{name0}: 0";

        if (player2ScoreText != null)
            player2ScoreText.text = $"{name1}: 0";

        // Selected character names.
        if (charactersText != null)
        {
            string c0 = GetCharacterName(slot0);
            string c1 = GetCharacterName(slot1);
            charactersText.text = $"{name0}: {c0}\n{name1}: {c1}";
        }

        // Selected augment names.
        if (augmentsText != null)
        {
            string a0 = FormatAugments(match.GetSelectedAugmentNames(slot0));
            string a1 = FormatAugments(match.GetSelectedAugmentNames(slot1));
            augmentsText.text = $"{name0}: {a0}\n{name1}: {a1}";
        }

        // Final match duration.
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
        PlayWinnerIntro();
        scoreRoutine = StartCoroutine(AnimateScores(name0, name1, match.Player0Wins, match.Player1Wins));
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

    // Requests Fusion shutdown and restores the lobby screen.
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

    // Formats match duration as mm:ss.
    private static string FormatDuration(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int total = Mathf.RoundToInt(seconds);
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes:00}:{secs:00}";
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        if (root == null)
            return;

        if (resultAnimator == null)
        {
            resultAnimator = UIPanelAnimator.Ensure(root);
            if (resultAnimator != null)
                resultAnimator.Configure(new Vector2(0f, -14f), 0.96f, 0.18f, 0.1f);
        }

        if (resultAnimator != null)
        {
            if (visible)
                resultAnimator.Show(instant);
            else
                resultAnimator.Hide(instant);
        }
        else if (root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    private void SetLoadingVisible(bool visible, bool instant = false)
    {
        if (loadingRoot == null)
            return;

        if (loadingVisibilityInitialized && loadingVisible == visible)
            return;

        loadingVisibilityInitialized = true;
        loadingVisible = visible;

        if (loadingAnimator == null)
        {
            loadingAnimator = UIPanelAnimator.Ensure(loadingRoot);
            if (loadingAnimator != null)
                loadingAnimator.Configure(new Vector2(0f, -8f), 0.98f, 0.14f, 0.08f);
        }

        if (loadingAnimator != null)
        {
            if (visible)
                loadingAnimator.Show(instant);
            else
                loadingAnimator.Hide(instant);
        }
        else if (loadingRoot.activeSelf != visible)
        {
            loadingRoot.SetActive(visible);
        }
    }

    private void UpdateLoadingText()
    {
        if (loadingText == null)
            return;

        int dotCount = Mathf.FloorToInt(Time.unscaledTime / loadingDotInterval) % 4;
        loadingText.text = loadingMessage + new string('.', dotCount);
    }

    private void PrepareAnimators()
    {
        if (root != null)
        {
            resultAnimator = UIPanelAnimator.Ensure(root);
            if (resultAnimator != null)
                resultAnimator.Configure(new Vector2(0f, -14f), 0.96f, 0.18f, 0.1f);
        }

        if (loadingRoot != null)
        {
            loadingAnimator = UIPanelAnimator.Ensure(loadingRoot);
            if (loadingAnimator != null)
                loadingAnimator.Configure(new Vector2(0f, -8f), 0.98f, 0.14f, 0.08f);
        }
    }

    private void PlayWinnerIntro()
    {
        if (winnerText == null)
            return;

        UIPanelAnimator animator = UIPanelAnimator.Ensure(winnerText.gameObject);
        if (animator != null)
            animator.Punch(1.08f, 0.22f, 0.08f);
    }

    private IEnumerator AnimateScores(string name0, string name1, int target0, int target1)
    {
        const float duration = 0.55f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            int score0 = Mathf.RoundToInt(Mathf.Lerp(0f, target0, t));
            int score1 = Mathf.RoundToInt(Mathf.Lerp(0f, target1, t));

            if (player1ScoreText != null)
                player1ScoreText.text = $"{name0}: {score0}";

            if (player2ScoreText != null)
                player2ScoreText.text = $"{name1}: {score1}";

            yield return null;
        }

        if (player1ScoreText != null)
            player1ScoreText.text = $"{name0}: {target0}";

        if (player2ScoreText != null)
            player2ScoreText.text = $"{name1}: {target1}";

        scoreRoutine = null;
    }
}
