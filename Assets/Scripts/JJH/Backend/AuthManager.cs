using System;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    public event Action<AuthResponse> SignupSucceeded;
    public event Action<AuthResponse> LoginSucceeded;
    public event Action<AuthResponse> RefreshSucceeded;
    public event Action<UserMeResponse> UserLoaded;
    public event Action<UserMeResponse> UserUpdated;
    public event Action LoggedOut;
    public event Action<string> AuthFailed;

    public bool IsBusy { get; private set; }
    public string LastError { get; private set; }
    public bool IsLoggedIn => BackendSession.IsLoggedIn;
    public long CurrentUserId => BackendSession.UserId;
    public string CurrentEmail => BackendSession.Email;
    public string CurrentNickname => BackendSession.Nickname;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject obj = new GameObject("AuthManager");
        DontDestroyOnLoad(obj);
        obj.AddComponent<AuthManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Signup(string email, string password, string nickname)
    {
        if (!CanStartRequest())
            return;

        if (!ValidateAuthInput(email, password, true, nickname))
            return;

        IsBusy = true;
        BackendApiClient.Instance.Signup(new SignupRequest
        {
            email = email.Trim(),
            password = password,
            nickname = nickname.Trim()
        }, auth =>
        {
            IsBusy = false;
            LastError = string.Empty;
            SignupSucceeded?.Invoke(auth);
        }, Fail);
    }

    public void Login(string email, string password)
    {
        if (!CanStartRequest())
            return;

        if (!ValidateAuthInput(email, password, false, string.Empty))
            return;

        IsBusy = true;
        BackendApiClient.Instance.Login(new LoginRequest
        {
            email = email.Trim(),
            password = password
        }, auth =>
        {
            IsBusy = false;
            LastError = string.Empty;
            LoginSucceeded?.Invoke(auth);
        }, Fail);
    }

    public void Refresh()
    {
        if (!CanStartRequest())
            return;

        if (string.IsNullOrWhiteSpace(BackendSession.RefreshToken))
        {
            Fail("저장된 refresh token이 없어. 먼저 로그인해야 해.");
            return;
        }

        IsBusy = true;
        BackendApiClient.Instance.Refresh(auth =>
        {
            IsBusy = false;
            LastError = string.Empty;
            RefreshSucceeded?.Invoke(auth);
        }, Fail);
    }

    public void LoadMe()
    {
        if (!CanStartRequest())
            return;

        if (!RequireLogin())
            return;

        IsBusy = true;
        BackendApiClient.Instance.GetMe(user =>
        {
            IsBusy = false;
            LastError = string.Empty;
            UserLoaded?.Invoke(user);
        }, Fail);
    }

    public void UpdateNickname(string nickname)
    {
        if (!CanStartRequest())
            return;

        if (!RequireLogin())
            return;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Fail("닉네임을 입력해야 해.");
            return;
        }

        IsBusy = true;
        BackendApiClient.Instance.UpdateUser(new UserUpdateRequest { nickname = nickname.Trim() }, user =>
        {
            IsBusy = false;
            LastError = string.Empty;
            UserUpdated?.Invoke(user);
        }, Fail);
    }

    public void Logout()
    {
        BackendSession.Clear();
        LastError = string.Empty;
        IsBusy = false;
        LoggedOut?.Invoke();
    }

    private bool CanStartRequest()
    {
        if (BackendApiClient.Instance == null)
        {
            Fail("BackendApiClient가 아직 준비되지 않았어.");
            return false;
        }

        if (IsBusy)
        {
            Fail("이미 서버 요청 중이야. 잠깐 기다려줘.");
            return false;
        }

        return true;
    }

    private bool RequireLogin()
    {
        if (BackendSession.IsLoggedIn)
            return true;

        Fail("먼저 로그인해야 해.");
        return false;
    }

    private bool ValidateAuthInput(string email, string password, bool needsNickname, string nickname)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Fail("이메일을 입력해야 해.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            Fail("비밀번호를 입력해야 해.");
            return false;
        }

        if (needsNickname && string.IsNullOrWhiteSpace(nickname))
        {
            Fail("닉네임을 입력해야 해.");
            return false;
        }

        return true;
    }

    private void Fail(string message)
    {
        IsBusy = false;
        LastError = message;
        AuthFailed?.Invoke(message);
    }
}
