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

    private void Awake()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);

        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);

        ResolvePopupRefs();

        if (popupOkButton != null)
            popupOkButton.onClick.AddListener(OnPopupOkClicked);

        HidePopup();
    }

    private void ResolvePopupRefs()
    {
        if (popupPanel != null) return;

        Transform popupT = FindAuthPopupPanel();
        if (popupT == null) return;

        Transform box = popupT.Find("PopupBox");
        popupPanel       = popupT.gameObject;
        popupMessageText = box?.Find("AuthPopupMessage")?.GetComponent<TMP_Text>();
        popupOkButton    = box?.Find("AuthPopupOkButton")?.GetComponent<Button>();
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
        string email = emailInput.text;
        string nickname = email.Contains("@") ? email.Split('@')[0] : email;
        AuthManager.Instance.Signup(email, passwordInput.text, nickname);
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

        string message;
        if (_lastActionWasLogin)
        {
            if (error.Contains("404") || error.Contains("User not found"))
                message = "User not found. Please sign up first.";
            else if (error.Contains("401") || error.Contains("Wrong password"))
                message = "Incorrect password. Please try again.";
            else
                message = error;
        }
        else
        {
            message = error;
        }

        ShowPopup(message, proceed: false);
    }

    private void ShowPopup(string message, bool proceed)
    {
        _pendingProceed = proceed;
        if (popupMessageText != null)
            popupMessageText.text = message;
        if (popupPanel != null)
            popupPanel.SetActive(true);
    }

    private void HidePopup()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);
        _pendingProceed = false;
    }

    private void OnPopupOkClicked()
    {
        bool shouldProceed = _pendingProceed;
        HidePopup();
        if (shouldProceed)
            OnProceedRequested?.Invoke();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
