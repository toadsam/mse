using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// file: Assets/Scripts/JJH/Backend/BackendApiClient.cs
// Persistent HTTP client for backend auth, profile, match, leaderboard, and augment requests.
public class BackendApiClient : MonoBehaviour
{
    private const string DefaultBaseUrl = "http://15.164.171.132:8080";
    private const string LegacyLocalhostUrl = "http://localhost:8080";
    private const string BaseUrlKey = "LastRound.BackendBaseUrl";

    public static BackendApiClient Instance { get; private set; }

    [SerializeField] private string baseUrl = DefaultBaseUrl;
    [SerializeField] private int timeoutSeconds = 10;

    public string BaseUrl
    {
        get => baseUrl;
        set
        {
            // Persist the normalized base URL so test/dev overrides survive scene reloads.
            baseUrl = NormalizeBaseUrl(value);
            PlayerPrefs.SetString(BaseUrlKey, baseUrl);
            PlayerPrefs.Save();
        }
    }

    // Auto-creates a singleton instance before any scene code attempts to call the backend.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject obj = new GameObject("BackendApiClient");
        DontDestroyOnLoad(obj);
        obj.AddComponent<BackendApiClient>();
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

        string savedBaseUrl = PlayerPrefs.GetString(BaseUrlKey, string.Empty);
        if (string.IsNullOrWhiteSpace(savedBaseUrl) || string.Equals(NormalizeBaseUrl(savedBaseUrl), LegacyLocalhostUrl, StringComparison.OrdinalIgnoreCase))
            BaseUrl = DefaultBaseUrl;
        else
            BaseUrl = savedBaseUrl;
    }

    // Creates a new backend account and stores the issued session tokens locally.
    public Coroutine Signup(SignupRequest request, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<SignupRequest, AuthResponse>("/api/auth/signup", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    // Authenticates an existing user and refreshes the local backend session state.
    public Coroutine Login(LoginRequest request, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<LoginRequest, AuthResponse>("/api/auth/login", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    // Exchanges the stored refresh token for a fresh access token pair.
    public Coroutine Refresh(Action<AuthResponse> onSuccess, Action<string> onError)
    {
        RefreshRequest request = new RefreshRequest { refreshToken = BackendSession.RefreshToken };

        return StartCoroutine(Post<RefreshRequest, AuthResponse>("/api/auth/refresh", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    // Loads the currently authenticated user's profile and syncs it into BackendSession.
    public Coroutine GetMe(Action<UserMeResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<UserMeResponse>("/api/user/me", true, response =>
        {
            BackendSession.UpdateUser(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    // Updates mutable user profile fields on the backend and stores the returned profile.
    public Coroutine UpdateUser(UserUpdateRequest request, Action<UserMeResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Put<UserUpdateRequest, UserMeResponse>("/api/user/update", request, true, response =>
        {
            BackendSession.UpdateUser(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    // Sends a completed match result payload for persistence.
    public Coroutine SaveMatchResult(MatchResultRequest request, Action<MatchResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<MatchResultRequest, MatchResponse>("/api/match/result", request, true, response => onSuccess?.Invoke(response.data), onError));
    }

    // Requests paginated match history for the signed-in user.
    public Coroutine GetMatchHistory(int page, int size, Action<MatchHistoryResponse> onSuccess, Action<string> onError)
    {
        string path = $"/api/match/history?page={Mathf.Max(0, page)}&size={Mathf.Max(1, size)}";
        return StartCoroutine(Get<MatchHistoryResponse>(path, true, response => onSuccess?.Invoke(response.data), onError));
    }

    // Loads the public leaderboard without requiring authentication.
    public Coroutine GetLeaderboard(Action<List<LeaderboardEntry>> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<List<LeaderboardEntry>>("/api/leaderboard", false, response => onSuccess?.Invoke(response.data), onError));
    }

    // Loads the public augment catalog without requiring authentication.
    public Coroutine GetAugments(Action<List<AugmentResponse>> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<List<AugmentResponse>>("/api/augments", false, response => onSuccess?.Invoke(response.data), onError));
    }

    // Shared GET helper used by read-only endpoints.
    private IEnumerator Get<T>(string path, bool auth, Action<BackendApiResponse<T>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(BuildUrl(path)))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

    // Shared POST helper that JSON-serializes the request body.
    private IEnumerator Post<TRequest, TResponse>(string path, TRequest body, bool auth, Action<BackendApiResponse<TResponse>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = BuildJsonRequest(BuildUrl(path), "POST", body))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

    // Shared PUT helper for profile updates.
    private IEnumerator Put<TRequest, TResponse>(string path, TRequest body, bool auth, Action<BackendApiResponse<TResponse>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = BuildJsonRequest(BuildUrl(path), "PUT", body))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

    // Applies headers, sends the request, and normalizes backend API success/error handling.
    private IEnumerator Send<T>(UnityWebRequest request, bool auth, Action<BackendApiResponse<T>> onSuccess, Action<string> onError)
    {
        request.timeout = timeoutSeconds;
        request.SetRequestHeader("Accept", "application/json");

        if (auth && !string.IsNullOrWhiteSpace(BackendSession.AccessToken))
            request.SetRequestHeader("Authorization", $"Bearer {BackendSession.AccessToken}");

        yield return request.SendWebRequest();

        string text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(FormatError(request.responseCode, request.error, text));
            yield break;
        }

        BackendApiResponse<T> response;
        try
        {
            response = JsonUtility.FromJson<BackendApiResponse<T>>(text);
        }
        catch (Exception ex)
        {
            onError?.Invoke($"JSON parse failed: {ex.Message}\n{text}");
            yield break;
        }

        if (response == null)
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        if (!response.success)
        {
            onError?.Invoke(string.IsNullOrWhiteSpace(response.error) ? "Backend returned success=false" : response.error);
            yield break;
        }

        onSuccess?.Invoke(response);
    }

    // Builds a JSON request manually because UnityWebRequest has no generic typed body helper.
    private UnityWebRequest BuildJsonRequest<T>(string url, string method, T body)
    {
        string json = body != null ? JsonUtility.ToJson(body) : "{}";
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(bytes),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    // Combines the configured base URL with an API-relative path.
    private string BuildUrl(string path)
    {
        string normalizedBase = NormalizeBaseUrl(baseUrl);
        string normalizedPath = path.StartsWith("/") ? path : "/" + path;
        return normalizedBase + normalizedPath;
    }

    // Removes trailing slashes and falls back to the production default when empty.
    private static string NormalizeBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.TrimEnd('/');
    }

    // Prefers the backend's structured error payload and falls back to raw transport details.
    private string FormatError(long status, string requestError, string body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                BackendErrorResponse errorResponse = JsonUtility.FromJson<BackendErrorResponse>(body);
                if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.error))
                    return $"HTTP {status}: {errorResponse.error}";
            }
            catch
            {
                // Fall through and include the raw body.
            }
        }

        return string.IsNullOrWhiteSpace(body)
            ? $"HTTP {status}: {requestError}"
            : $"HTTP {status}: {requestError}\n{body}";
    }
}
