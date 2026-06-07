using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileAuthUI : MonoBehaviour
{
    [Header("Auth Input")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Auth Buttons")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Popup")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text popupMessageText;
    [SerializeField] private Button popupOkButton;

    public event Action OnProceedRequested;

    private bool _pendingProceed;
    private bool _lastActionWasLogin;
    private UIPanelAnimator _popupAnimator;
    private UIPanelAnimator _popupBoxAnimator;

    private void Awake()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);

        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);

        ResolvePopupRefs();

        if (popupOkButton != null)
            popupOkButton.onClick.AddListener(OnPopupOkClicked);

        HidePopup(true);
    }

    private void ResolvePopupRefs()
    {
        Transform popupT = popupPanel != null ? popupPanel.transform : FindAuthPopupPanel();
        if (popupT == null)
            return;

        Transform box = popupT.Find("PopupBox");
        popupPanel       = popupT.gameObject;

        if (popupMessageText == null)
            popupMessageText = box?.Find("AuthPopupMessage")?.GetComponent<TMP_Text>();

        if (popupOkButton == null)
            popupOkButton = box?.Find("AuthPopupOkButton")?.GetComponent<Button>();

        _popupAnimator = UIPanelAnimator.Ensure(popupPanel);
        if (_popupAnimator != null)
            _popupAnimator.Configure(Vector2.zero, 1f, 0.12f, 0.08f);

        if (box != null)
        {
            _popupBoxAnimator = UIPanelAnimator.Ensure(box.gameObject);
            _popupBoxAnimator.Configure(new Vector2(0f, -16f), 0.94f, 0.18f, 0.1f);
        }

        UIAnimationBootstrap.InstallButtonsIn(popupPanel);
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

    private void OnEnable()
    {
        if (AuthManager.Instance == null) return;
        AuthManager.Instance.LoginSucceeded  += OnLoginSucceeded;
        AuthManager.Instance.SignupSucceeded += OnSignupSucceeded;
        AuthManager.Instance.AuthFailed      += OnAuthFailed;
    }

    private void OnDisable()
    {
        if (AuthManager.Instance == null) return;
        AuthManager.Instance.LoginSucceeded  -= OnLoginSucceeded;
        AuthManager.Instance.SignupSucceeded -= OnSignupSucceeded;
        AuthManager.Instance.AuthFailed      -= OnAuthFailed;
    }

    private void OnLoginClicked()
    {
        if (emailInput == null || passwordInput == null) return;
        _lastActionWasLogin = true;
        SetStatus("Logging in...");
        AuthManager.Instance.Login(emailInput.text, passwordInput.text);
    }

    private void OnSignUpClicked()
    {
        if (emailInput == null || passwordInput == null) return;
        _lastActionWasLogin = false;
        SetStatus("Signing up...");
        string userId = emailInput.text;
        AuthManager.Instance.Signup(userId, passwordInput.text, userId);
    }

    private void OnLoginSucceeded(AuthResponse auth)
    {
        SetStatus(string.Empty);
        Debug.Log($"[ProfileAuthUI] Login succeeded. UserId={auth.userId}, Nickname={auth.nickname}");
        ShowPopup("User verified.", proceed: true);
    }

    private void OnSignupSucceeded(AuthResponse auth)
    {
        SetStatus(string.Empty);
        Debug.Log($"[ProfileAuthUI] Signup succeeded. UserId={auth.userId}, Nickname={auth.nickname}");
        ShowPopup("User verified.", proceed: true);
    }

    private void OnAuthFailed(string error)
    {
        SetStatus(string.Empty);
        Debug.LogWarning($"[ProfileAuthUI] Auth failed: {error}");
        ShowPopup(ErrorMessageFormatter.ToAuthFriendly(error, _lastActionWasLogin), proceed: false);
    }

    private void ShowPopup(string message, bool proceed)
    {
        if (popupPanel == null || popupMessageText == null || _popupAnimator == null)
            ResolvePopupRefs();

        _pendingProceed = proceed;
        if (popupMessageText != null)
            popupMessageText.text = message;

        if (_popupAnimator != null)
            _popupAnimator.Show();
        else if (popupPanel != null)
            popupPanel.SetActive(true);

        if (_popupBoxAnimator != null)
        {
            _popupBoxAnimator.Show(false, 0.02f);

            if (!proceed)
                _popupBoxAnimator.Shake(0.18f, 8f, 0.16f);
        }
    }

    private void HidePopup(bool instant = false)
    {
        if (_popupBoxAnimator != null)
            _popupBoxAnimator.Hide(instant);

        if (_popupAnimator != null)
            _popupAnimator.Hide(instant);
        else if (popupPanel != null)
            popupPanel.SetActive(false);

        _pendingProceed = false;
    }

    private void OnPopupOkClicked()
    {
        bool shouldProceed = _pendingProceed;
        HidePopup(false);
        if (shouldProceed)
            OnProceedRequested?.Invoke();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
