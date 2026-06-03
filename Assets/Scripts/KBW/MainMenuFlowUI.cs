using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuFlowUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("World Preview")]
    [Tooltip("Canvas 밖 월드에 배치한 캐릭터 프리뷰 전체 루트입니다.")]
    [SerializeField] private GameObject characterPreviewArea;

    [Header("Title")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Profile")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Button prevCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button backButton;

    [Header("Character Preview")]
    [SerializeField] private GameObject[] previewCharacters;
    [SerializeField] private string[] characterNames;

    private int selectedCharacterId;

    private void Awake()
    {
        if (playButton != null)
            playButton.onClick.AddListener(ShowProfile);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (prevCharacterButton != null)
            prevCharacterButton.onClick.AddListener(SelectPreviousCharacter);

        if (nextCharacterButton != null)
            nextCharacterButton.onClick.AddListener(SelectNextCharacter);

        if (continueButton != null)
            continueButton.onClick.AddListener(ConfirmProfileAndShowLobby);

        if (backButton != null)
            backButton.onClick.AddListener(ShowTitle);
    }

    private void Start()
    {
        selectedCharacterId = 0;
        RefreshCharacterPreview();
        ShowTitle();
    }

    private void ShowTitle()
    {
        SetPanel(titlePanel, true);
        SetPanel(profilePanel, false);
        SetPanel(lobbyPanel, false);

        SetPanel(characterPreviewArea, false);
    }

    private void ShowProfile()
    {
        SetPanel(titlePanel, false);
        SetPanel(profilePanel, true);
        SetPanel(lobbyPanel, false);

        SetPanel(characterPreviewArea, true);
        RefreshCharacterPreview();

        if (statusText != null)
            statusText.text = "";
    }

    private void ConfirmProfileAndShowLobby()
    {
        string playerName = nameInput != null ? nameInput.text : "";
        LocalPlayerProfile.SetProfile(playerName, (byte)selectedCharacterId);

        Debug.Log($"[MainMenu] Profile saved. Name={LocalPlayerProfile.PlayerName}, CharacterId={LocalPlayerProfile.CharacterId}");

        SetPanel(titlePanel, false);
        SetPanel(profilePanel, false);
        SetPanel(lobbyPanel, true);

        SetPanel(characterPreviewArea, false);
    }

    private void SelectPreviousCharacter()
    {
        if (previewCharacters == null || previewCharacters.Length == 0)
            return;

        selectedCharacterId--;

        if (selectedCharacterId < 0)
            selectedCharacterId = previewCharacters.Length - 1;

        RefreshCharacterPreview();
    }

    private void SelectNextCharacter()
    {
        if (previewCharacters == null || previewCharacters.Length == 0)
            return;

        selectedCharacterId++;

        if (selectedCharacterId >= previewCharacters.Length)
            selectedCharacterId = 0;

        RefreshCharacterPreview();
    }

    private void RefreshCharacterPreview()
    {
        if (previewCharacters == null)
            return;

        for (int i = 0; i < previewCharacters.Length; i++)
        {
            if (previewCharacters[i] != null)
                previewCharacters[i].SetActive(i == selectedCharacterId);
        }

        if (characterNameText != null)
        {
            if (characterNames != null &&
                selectedCharacterId >= 0 &&
                selectedCharacterId < characterNames.Length &&
                !string.IsNullOrWhiteSpace(characterNames[selectedCharacterId]))
            {
                characterNameText.text = characterNames[selectedCharacterId];
            }
            else
            {
                characterNameText.text = $"Character {selectedCharacterId + 1}";
            }
        }
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}