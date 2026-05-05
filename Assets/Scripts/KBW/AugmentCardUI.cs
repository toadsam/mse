using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentCardUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject highlightObject;

    private int slotIndex;
    private Action<int> onClick;

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
        }

        SetHighlight(false);
        SetInteractable(true);
    }

    public void SetInteractable(bool interactable)
    {
        if (button)
            button.interactable = interactable;
    }

    public void SetHighlight(bool value)
    {
        if (highlightObject)
            highlightObject.SetActive(value);
    }

    private void OnClick()
    {
        onClick?.Invoke(slotIndex);
    }
}