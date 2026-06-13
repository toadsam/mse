using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controls the title, login/profile, character selection, and lobby panel flow.
public class MainMenuFlowUI : MonoBehaviour
{
    // Main title header that is hidden after entering the lobby.
    [Header("Header")]
    [Tooltip("Title header shown at the top of the main menu.")]
    [SerializeField] private GameObject titleHeader;

    // Menu panels shown one at a time during the main menu flow.
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private GameObject lobbyPanel;

    // World-space character preview shown during character selection.
    [Header("World Preview")]
    [Tooltip("World-space character preview root placed outside the Canvas.")]
    [SerializeField] private GameObject characterPreviewArea;

    [Header("Title")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button quitButton;

    [Header("Profile")]
    [SerializeField] private Button profileToCharacterButton;
    [SerializeField] private Button profileBackButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Character Select")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Button prevCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button backButton;

    [Header("Character Preview")]
    [SerializeField] private GameObject[] previewCharacters;
    [SerializeField] private string[] characterNames;

    [Header("Root")]
    [SerializeField] private GameObject menuRoot;

    // Currently selected character index in the preview carousel.
    private int selectedCharacterId;
    private bool _waitingForNicknameUpdate;

    private GameObject _popupPanel;
    private TMP_Text _popupMessageText;
    private UIPanelAnimator _popupAnimator;
    private UIPanelAnimator _popupBoxAnimator;

    private void Awake()
    {
        if (menuRoot == null)
            menuRoot = gameObject;

        if (playButton != null)
            playButton.onClick.AddListener(ShowProfile);

        if (settingButton != null)
            settingButton.onClick.AddListener(OnSettingClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (profileBackButton != null)
            profileBackButton.onClick.AddListener(ShowTitle);

        if (prevCharacterButton != null)
            prevCharacterButton.onClick.AddListener(SelectPreviousCharacter);

        if (nextCharacterButton != null)
            nextCharacterButton.onClick.AddListener(SelectNextCharacter);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (backButton != null)
            backButton.onClick.AddListener(ShowProfile);
    }

    private void OnEnable()
    {
        if (AuthManager.Instance == null) return;
        AuthManager.Instance.UserUpdated += OnNicknameUpdated;
        AuthManager.Instance.AuthFailed  += OnNicknameUpdateFailed;
    }

    private void OnDisable()
    {
        if (AuthManager.Instance == null) return;
        AuthManager.Instance.UserUpdated -= OnNicknameUpdated;
        AuthManager.Instance.AuthFailed  -= OnNicknameUpdateFailed;
    }

    private void Start()
    {
        selectedCharacterId = 0;
        RefreshCharacterPreview();
        ResolvePopupRefs();

        if (profileToCharacterButton != null)
            profileToCharacterButton.gameObject.SetActive(false);

        if (profilePanel != null && profilePanel.TryGetComponent(out ProfileAuthUI authUI))
            authUI.OnProceedRequested += ShowCharacterSelect;

        if (BackendSession.IsLoggedIn && LocalPlayerProfile.HasProfile)
        {
            Debug.Log("[MainMenuFlowUI] Restore lobby after scene reload.");
            ShowLobbyDirect();
        }
        else
        {
            ShowTitle();
        }
    }

    private void ResolvePopupRefs()
    {
        Transform popupT = FindAuthPopupPanel();
        if (popupT == null) return;

        _popupPanel = popupT.gameObject;

        Transform msgT = popupT.Find("PopupBox/AuthPopupMessage");
        if (msgT != null)
            _popupMessageText = msgT.GetComponent<TMP_Text>();

        _popupAnimator = UIPanelAnimator.Ensure(_popupPanel);
        if (_popupAnimator != null)
            _popupAnimator.Configure(Vector2.zero, 1f, 0.12f, 0.08f);

        Transform box = popupT.Find("PopupBox");
        if (box != null)
        {
            _popupBoxAnimator = UIPanelAnimator.Ensure(box.gameObject);
            _popupBoxAnimator.Configure(new Vector2(0f, -16f), 0.94f, 0.18f, 0.1f);
        }

        UIAnimationBootstrap.InstallButtonsIn(_popupPanel);
    }

    private Transform FindAuthPopupPanel()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            Transform popup = canvas.transform.Find("AuthPopupPanel");
            if (popup != null)
                return popup;
        }

        return null;
    }

    private void ShowPopup(string message)
    {
        if (_popupPanel == null || _popupMessageText == null)
            ResolvePopupRefs();

        if (_popupMessageText != null)
            _popupMessageText.text = message;

        if (_popupAnimator != null)
            _popupAnimator.Show();
        else if (_popupPanel != null)
            _popupPanel.SetActive(true);

        if (_popupBoxAnimator != null)
        {
            _popupBoxAnimator.Show(false, 0.02f);
            _popupBoxAnimator.Shake(0.18f, 8f, 0.16f);
        }
    }

    // Shows the first title screen state.
    private void ShowTitle()
    {
        SetPanel(titleHeader, true);
        SetPanel(titlePanel, true);
        SetPanel(profilePanel, false);
        SetPanel(characterSelectPanel, false);
        SetPanel(lobbyPanel, false);

        SetPanel(characterPreviewArea, false);
    }

    // Opens the login/profile panel.
    private void ShowProfile()
    {
        SetPanel(titleHeader, true);
        SetPanel(titlePanel, false);
        SetPanel(profilePanel, true);
        SetPanel(characterSelectPanel, false);
        SetPanel(lobbyPanel, false);

        SetPanel(characterPreviewArea, false);

        if (statusText != null)
            statusText.text = "";
    }

    // Opens character selection after profile login succeeds.
    private void ShowCharacterSelect()
    {
        SetPanel(titleHeader, true);
        SetPanel(titlePanel, false);
        SetPanel(profilePanel, false);
        SetPanel(characterSelectPanel, true);
        SetPanel(lobbyPanel, false);

        SetPanel(characterPreviewArea, true);
        RefreshCharacterPreview();
    }

    // Validates nickname input and requests backend nickname update.
    private void OnContinueClicked()
    {
        string playerName = nameInput != null ? nameInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(playerName))
        {
            ShowPopup("Please enter a nickname.");
            return;
        }

        if (AuthManager.Instance == null || !BackendSession.IsLoggedIn)
        {
            ShowPopup("Not logged in. Please log in first.");
            return;
        }

        if (continueButton != null) continueButton.interactable = false;
        _waitingForNicknameUpdate = true;
        AuthManager.Instance.UpdateNickname(playerName);
    }

    private void OnNicknameUpdated(UserMeResponse user)
    {
        if (!_waitingForNicknameUpdate) return;
        _waitingForNicknameUpdate = false;

        if (continueButton != null) continueButton.interactable = true;

        Debug.Log($"[MainMenu] Nickname updated. Nickname={user.nickname}");
        LocalPlayerProfile.SetProfile(user.nickname, (byte)selectedCharacterId, GetSelectedCharacterName());

        SetPanel(titleHeader, false);
        SetPanel(titlePanel, false);
        SetPanel(profilePanel, false);
        SetPanel(characterSelectPanel, false);
        SetPanel(lobbyPanel, true);
        SetPanel(characterPreviewArea, false);
    }

    private void OnNicknameUpdateFailed(string error)
    {
        if (!_waitingForNicknameUpdate) return;
        _waitingForNicknameUpdate = false;

        if (continueButton != null) continueButton.interactable = true;

        Debug.LogWarning($"[MainMenu] Failed to update nickname: {error}");
        ShowPopup($"Failed to update nickname.\n{ErrorMessageFormatter.ToFriendly(error)}");
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

    // Updates the active preview model and character name label.
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

    private string GetSelectedCharacterName()
    {
        if (characterNames != null &&
            selectedCharacterId >= 0 &&
            selectedCharacterId < characterNames.Length &&
            !string.IsNullOrWhiteSpace(characterNames[selectedCharacterId]))
        {
            return characterNames[selectedCharacterId];
        }

        return $"Character {selectedCharacterId + 1}";
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel == null)
            return;

        if (!IsUIPanel(panel))
        {
            panel.SetActive(active);
            return;
        }

        UIPanelAnimator animator = UIPanelAnimator.Ensure(panel);
        if (animator == null)
        {
            panel.SetActive(active);
            return;
        }

        animator.Configure(new Vector2(0f, -10f), 0.97f, 0.14f, 0.1f);

        if (active)
            animator.Show();
        else
            animator.Hide();
    }

    private static bool IsUIPanel(GameObject panel)
    {
        return panel.GetComponent<RectTransform>() != null;
    }

    private void OnSettingClicked()
    {
        AudioSettingsUI.OpenSettings();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Restores the lobby panel directly after scene reload or match return.
    public void ShowLobbyDirect()
    {
        Debug.Log("[MainMenuFlowUI] ShowLobbyDirect");

        if (menuRoot != null)
            menuRoot.SetActive(true);

        gameObject.SetActive(true);

        SetPanel(titlePanel, false);
        SetPanel(profilePanel, false);
        SetPanel(characterSelectPanel, false);
        SetPanel(titleHeader, false);
        SetPanel(lobbyPanel, true);
        SetPanel(characterPreviewArea, false);
    }
}
