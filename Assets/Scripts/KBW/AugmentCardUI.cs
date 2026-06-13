using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays one selectable augment card in the augment selection screen.
public class AugmentCardUI : MonoBehaviour
{
    // UI references updated when an augment is bound to this card.
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button button;

    // Slot index passed back to the selection callback.
    private int slotIndex;
    private Action<int> onClick;

    // Fills the card with augment data and connects its click callback.
    public void Bind(AugmentDefinition def, int slot, Action<int> clickCallback)
    {
        slotIndex = slot;
        onClick = clickCallback;

        if (iconImage) iconImage.sprite = def.icon;
        if (nameText) nameText.text = def.displayName;
        if (descriptionText) descriptionText.text = def.description;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            button.interactable = true;
        }
    }

    // Enables or disables card selection without changing the shown data.
    public void SetInteractable(bool interactable)
    {
        if (button)
            button.interactable = interactable;
    }

    // Sends the selected slot index to the owner UI.
    private void OnClick()
    {
        onClick?.Invoke(slotIndex);
    }
}
