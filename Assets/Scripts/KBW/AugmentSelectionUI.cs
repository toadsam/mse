using TMPro;
using UnityEngine;

public class AugmentSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private AugmentCardUI[] cards;
    [SerializeField] private AugmentDatabase augmentDatabase;

    private PlayerNetwork localPlayer;
    private MatchManager matchManager;

    private bool lastVisible;

    private int lastA0 = -999;
    private int lastA1 = -999;
    private int lastA2 = -999;

    private void Start()
    {
        if (titleText) titleText.text = "Choose Your Augment";
        if (subText) subText.text = "Select one augment before the round starts.";
        if (waitingText) waitingText.gameObject.SetActive(false);

        SetVisible(false);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        matchManager = GameManager.Instance.Match;
        localPlayer = GameManager.Instance.LocalPlayer;

        bool shouldShow =
            matchManager != null &&
            localPlayer != null &&
            matchManager.CurrentPhase == MatchPhase.ChoosingAugment;

        if (shouldShow != lastVisible)
        {
            SetVisible(shouldShow);

            if (shouldShow)
                RefreshUI();
        }

        lastVisible = shouldShow;

        if (!shouldShow || localPlayer == null)
            return;

        if (localPlayer.HasSelectedAugmentNet)
        {
            if (waitingText) waitingText.gameObject.SetActive(true);

            foreach (var card in cards)
                card.SetInteractable(false);
        }

        if (shouldShow && localPlayer != null)
        {
            if (localPlayer.OfferedAugmentId0 != lastA0 ||
                localPlayer.OfferedAugmentId1 != lastA1 ||
                localPlayer.OfferedAugmentId2 != lastA2)
            {
                RefreshUI();
                lastA0 = localPlayer.OfferedAugmentId0;
                lastA1 = localPlayer.OfferedAugmentId1;
                lastA2 = localPlayer.OfferedAugmentId2;
            }
        }
    }

    public void RefreshUI()
    {
        if (localPlayer == null || augmentDatabase == null) return;

        int[] ids =
        {
            localPlayer.OfferedAugmentId0,
            localPlayer.OfferedAugmentId1,
            localPlayer.OfferedAugmentId2
        };

        for (int i = 0; i < cards.Length; i++)
        {
            if (i >= ids.Length) continue;

            AugmentDefinition def = augmentDatabase.GetById(ids[i]);
            if (def == null) continue;

            cards[i].Bind(def, i, OnCardClicked);
        }

        if (waitingText) waitingText.gameObject.SetActive(false);
    }

    private void OnCardClicked(int slotIndex)
    {
        if (localPlayer == null) return;
        if (localPlayer.HasSelectedAugmentNet) return;

        localPlayer.RPC_RequestSelectAugment(slotIndex);
    }

    private void SetVisible(bool visible)
    {
        if (root) root.SetActive(visible);
    }
}