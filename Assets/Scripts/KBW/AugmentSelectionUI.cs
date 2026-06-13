using TMPro;
using UnityEngine;

// Controls the local augment selection screen between rounds.
public class AugmentSelectionUI : MonoBehaviour
{
    // Main UI references for augment selection and waiting states.
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private AugmentCardUI[] cards;
    [SerializeField] private AugmentDatabase augmentDatabase;

    // Cached match/player references used to read networked offer state.
    private PlayerNetwork localPlayer;
    private MatchManager matchManager;

    // Tracks visibility and offered ids to avoid unnecessary UI rebuilds.
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

    // Shows, hides, and refreshes the UI according to the current match phase.
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
            lastVisible = shouldShow;

            if (shouldShow)
            {
                // Reset cached offer ids so the new round always refreshes the cards.
                lastA0 = -999;
                lastA1 = -999;
                lastA2 = -999;

                RefreshUI();
                UpdateOfferCache();
            }
            else
            {
                if (waitingText)
                    waitingText.gameObject.SetActive(false);
            }
        }

        if (!shouldShow || localPlayer == null)
            return;

        if (!localPlayer.CanSelectAugmentNet)
        {
            if (titleText) titleText.text = "Opponent is choosing an augment";
            if (subText) subText.text = "You won the previous round.";
            if (waitingText)
            {
                waitingText.text = "Waiting for opponent...";
                waitingText.gameObject.SetActive(true);
            }

            foreach (var card in cards)
            {
                if (card != null)
                {
                    card.SetInteractable(false);
                    card.gameObject.SetActive(false);
                }
            }

            return;
        }

        if (localPlayer.OfferedAugmentId0 != lastA0 ||
            localPlayer.OfferedAugmentId1 != lastA1 ||
            localPlayer.OfferedAugmentId2 != lastA2)
        {
            RefreshUI();
            UpdateOfferCache();
        }

        UpdateSelectionState();
    }

    // Rebuilds card contents from the local player's offered augment ids.
    public void RefreshUI()
    {
        if (localPlayer == null || augmentDatabase == null) return;

        if (titleText) titleText.text = "Choose Your Augment";
        if (subText) subText.text = "Select one augment before the round starts.";

        foreach (var card in cards)
        {
            if (card != null)
                card.gameObject.SetActive(true);
        }

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

    // Sends the selected card slot to the state authority.
    private void OnCardClicked(int slotIndex)
    {
        if (localPlayer == null) return;
        if (!localPlayer.CanSelectAugmentNet) return;
        if (localPlayer.HasSelectedAugmentNet) return;

        localPlayer.RPC_RequestSelectAugment(slotIndex);
    }

    private void SetVisible(bool visible)
    {
        if (root) root.SetActive(visible);
    }

    private void UpdateOfferCache()
    {
        if (localPlayer == null) return;

        lastA0 = localPlayer.OfferedAugmentId0;
        lastA1 = localPlayer.OfferedAugmentId1;
        lastA2 = localPlayer.OfferedAugmentId2;
    }

    // Updates the card and waiting text after this player has selected.
    private void UpdateSelectionState()
    {
        bool selected = localPlayer != null && localPlayer.HasSelectedAugmentNet;

        if (selected)
        {
            if (titleText) titleText.text = "Augment Selected";
            if (subText) subText.text = "Waiting for opponent...";

            if (waitingText)
            {
                waitingText.text = "Waiting for opponent...";
                waitingText.gameObject.SetActive(true);
            }

            foreach (var card in cards)
            {
                if (card != null)
                    card.gameObject.SetActive(false);
            }

            return;
        }

        if (titleText) titleText.text = "Choose Your Augment";
        if (subText) subText.text = "Select one augment before the round starts.";

        if (waitingText)
            waitingText.gameObject.SetActive(false);

        foreach (var card in cards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
                card.SetInteractable(true);
            }
        }
    }
}