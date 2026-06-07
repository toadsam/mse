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

    [Header("Health Color")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Color normalHealthColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color poisonHealthColor = new Color(0.1f, 0.9f, 0.1f, 1f);

    [Header("Active Item")]
    [SerializeField] private GameObject activeItemPanel;
    [SerializeField] private Image activeItemIconImage;
    [SerializeField] private TMP_Text activeItemNameText;
    [SerializeField] private TMP_Text activeItemStateText;

    [SerializeField] private Sprite medKitIcon;
    [SerializeField] private Sprite grenadeIcon;
    [SerializeField] private Sprite smokeBombIcon;
    [SerializeField] private Sprite throwingAxeIcon;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        if (healthFillImage == null && healthSlider != null && healthSlider.fillRect != null)
            healthFillImage = healthSlider.fillRect.GetComponent<Image>();

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

        // MatchResult 단계는 전용 Match_Result 패널이 담당하므로 HUD는 숨긴다.
        bool shouldShowHud =
            match.CurrentPhase == MatchPhase.RoundIntro ||
            match.CurrentPhase == MatchPhase.Playing ||
            match.CurrentPhase == MatchPhase.RoundResult;

        SetHudVisible(shouldShowHud);

        if (!shouldShowHud)
            return;

        UpdateHealth(localPlayer);
        UpdateRoundAndScore(match, localPlayer);
        UpdateActiveItem(localPlayer);
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

            if (healthFillImage != null)
                healthFillImage.color = normalHealthColor;

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

        if (healthFillImage != null)
            healthFillImage.color = health.IsPoisonedNet ? poisonHealthColor : normalHealthColor;
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

    private void UpdateActiveItem(PlayerNetwork localPlayer)
    {
        if (activeItemPanel != null)
            activeItemPanel.SetActive(localPlayer != null);

        if (localPlayer == null)
            return;

        ActiveItemType item = localPlayer.CurrentActiveItem;

        if (activeItemIconImage != null)
            activeItemIconImage.sprite = GetActiveItemIcon(item);

        if (activeItemNameText != null)
            activeItemNameText.text = GetActiveItemName(item);

        if (activeItemStateText != null)
            activeItemStateText.text = GetActiveItemStateText(localPlayer);
    }

    private Sprite GetActiveItemIcon(ActiveItemType item)
    {
        return item switch
        {
            ActiveItemType.MedKit => medKitIcon,
            ActiveItemType.Grenade => grenadeIcon,
            ActiveItemType.SmokeBomb => smokeBombIcon,
            ActiveItemType.ThrowingAxe => throwingAxeIcon,
            _ => null
        };
    }

    private string GetActiveItemName(ActiveItemType item)
    {
        return item switch
        {
            ActiveItemType.MedKit => "Med Kit",
            ActiveItemType.Grenade => "Grenade",
            ActiveItemType.SmokeBomb => "Smoke Bomb",
            ActiveItemType.ThrowingAxe => "Throwing Axe",
            _ => "-"
        };
    }

    private string GetActiveItemStateText(PlayerNetwork player)
    {
        if (player == null)
            return "";

        if (player.CurrentActiveItem == ActiveItemType.ThrowingAxe)
        {
            return player.ActiveItemUsesRemaining > 0
                ? "Ready"
                : "Retrieve";
        }

        return $"{player.ActiveItemUsesRemaining}";
    }
}
