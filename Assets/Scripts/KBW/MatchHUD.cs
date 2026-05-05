using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchHUD : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject hudRoot;

    [Header("Health")]
    [SerializeField] private GameObject healthPanel;
    [SerializeField] private Slider healthSlider;

    [Header("Round Info")]
    [SerializeField] private GameObject roundInfoPanel;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Center Message")]
    [SerializeField] private GameObject centerMessagePanel;
    [SerializeField] private TMP_Text centerMessageText;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        SetCenterMessage(false, "");
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;

        if (gm == null || gm.Match == null || gm.LocalPlayer == null)
        {
            SetHudVisible(false);
            return;
        }

        MatchManager match = gm.Match;
        PlayerNetwork localPlayer = gm.LocalPlayer;

        bool shouldShowHud =
            match.CurrentPhase == MatchPhase.RoundIntro ||
            match.CurrentPhase == MatchPhase.Playing ||
            match.CurrentPhase == MatchPhase.RoundResult ||
            match.CurrentPhase == MatchPhase.MatchResult;

        SetHudVisible(shouldShowHud);

        if (!shouldShowHud)
            return;

        UpdateHealth(localPlayer);
        UpdateRoundAndScore(match, localPlayer);
        UpdateCenterMessage(match, localPlayer);
    }

    private void SetHudVisible(bool visible)
    {
        if (hudRoot != null && hudRoot.activeSelf != visible)
            hudRoot.SetActive(visible);
    }

    private void UpdateHealth(PlayerNetwork localPlayer)
    {
        PlayerHealth health = localPlayer != null ? localPlayer.Health : null;

        if (health == null)
        {
            if (healthSlider != null)
            {
                healthSlider.minValue = 0;
                healthSlider.maxValue = 1;
                healthSlider.value = 0;
            }

            return;
        }

        int current = health.CurrentHealth;
        int max = Mathf.Max(1, health.MaxHealth);

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
    }

    private void UpdateRoundAndScore(MatchManager match, PlayerNetwork localPlayer)
    {
        if (roundText != null)
            roundText.text = $"Round {match.RoundIndex}";

        int myWins = GetWinsForSlot(match, localPlayer.SlotIndex);
        int enemySlot = localPlayer.SlotIndex == 0 ? 1 : 0;
        int enemyWins = GetWinsForSlot(match, enemySlot);

        if (scoreText != null)
            scoreText.text = $"Score  {myWins} : {enemyWins}";
    }

    private int GetWinsForSlot(MatchManager match, int slot)
    {
        if (slot == 0)
            return match.Player0Wins;

        if (slot == 1)
            return match.Player1Wins;

        return 0;
    }

    private void UpdateCenterMessage(MatchManager match, PlayerNetwork localPlayer)
    {
        switch (match.CurrentPhase)
        {
            case MatchPhase.RoundIntro:
                SetCenterMessage(true, $"ROUND {match.RoundIndex}\nSTART");
                break;

            case MatchPhase.Playing:
                SetCenterMessage(false, "");
                break;

            case MatchPhase.RoundResult:
                if (match.RoundWinnerSlot == localPlayer.SlotIndex)
                    SetCenterMessage(true, "YOU WIN");
                else
                    SetCenterMessage(true, "YOU LOSE");
                break;

            case MatchPhase.MatchResult:
                if (match.MatchWinnerSlot == localPlayer.SlotIndex)
                    SetCenterMessage(true, "FINAL VICTORY");
                else
                    SetCenterMessage(true, "FINAL DEFEAT");
                break;

            default:
                SetCenterMessage(false, "");
                break;
        }
    }

    private void SetCenterMessage(bool visible, string message)
    {
        if (centerMessagePanel != null)
            centerMessagePanel.SetActive(visible);

        if (centerMessageText != null)
            centerMessageText.text = message;
    }
}
