using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class BackendApiClient : MonoBehaviour
{
    public static BackendApiClient Instance { get; private set; }

    [SerializeField] private string baseUrl = "http://localhost:8080";
    [SerializeField] private int timeoutSeconds = 10;

    public string BaseUrl
    {
        get => baseUrl;
        set => baseUrl = string.IsNullOrWhiteSpace(value) ? "http://localhost:8080" : value.TrimEnd('/');
    }

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
        BaseUrl = baseUrl;
    }

    public Coroutine Signup(SignupRequest request, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<SignupRequest, AuthResponse>("/api/auth/signup", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    public Coroutine Login(LoginRequest request, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<LoginRequest, AuthResponse>("/api/auth/login", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    public Coroutine Refresh(Action<AuthResponse> onSuccess, Action<string> onError)
    {
        RefreshRequest request = new RefreshRequest { refreshToken = BackendSession.RefreshToken };

        return StartCoroutine(Post<RefreshRequest, AuthResponse>("/api/auth/refresh", request, false, response =>
        {
            BackendSession.Save(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    public Coroutine GetMe(Action<UserMeResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<UserMeResponse>("/api/user/me", true, response =>
        {
            BackendSession.UpdateUser(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    public Coroutine UpdateUser(UserUpdateRequest request, Action<UserMeResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Put<UserUpdateRequest, UserMeResponse>("/api/user/update", request, true, response =>
        {
            BackendSession.UpdateUser(response.data);
            onSuccess?.Invoke(response.data);
        }, onError));
    }

    public Coroutine SaveMatchResult(MatchResultRequest request, Action<MatchResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Post<MatchResultRequest, MatchResponse>("/api/match/result", request, true, response => onSuccess?.Invoke(response.data), onError));
    }

    public Coroutine GetMatchHistory(int page, int size, Action<MatchHistoryResponse> onSuccess, Action<string> onError)
    {
        string path = $"/api/match/history?page={Mathf.Max(0, page)}&size={Mathf.Max(1, size)}";
        return StartCoroutine(Get<MatchHistoryResponse>(path, true, response => onSuccess?.Invoke(response.data), onError));
    }

    public Coroutine GetLeaderboard(Action<List<LeaderboardEntry>> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<List<LeaderboardEntry>>("/api/leaderboard", false, response => onSuccess?.Invoke(response.data), onError));
    }

    public Coroutine GetAugments(Action<List<AugmentResponse>> onSuccess, Action<string> onError)
    {
        return StartCoroutine(Get<List<AugmentResponse>>("/api/augments", false, response => onSuccess?.Invoke(response.data), onError));
    }

    private IEnumerator Get<T>(string path, bool auth, Action<BackendApiResponse<T>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(BuildUrl(path)))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

    private IEnumerator Post<TRequest, TResponse>(string path, TRequest body, bool auth, Action<BackendApiResponse<TResponse>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = BuildJsonRequest(BuildUrl(path), "POST", body))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

    private IEnumerator Put<TRequest, TResponse>(string path, TRequest body, bool auth, Action<BackendApiResponse<TResponse>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = BuildJsonRequest(BuildUrl(path), "PUT", body))
        {
            yield return Send(request, auth, onSuccess, onError);
        }
    }

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

    private string BuildUrl(string path)
    {
        string normalizedBase = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8080" : baseUrl.TrimEnd('/');
        string normalizedPath = path.StartsWith("/") ? path : "/" + path;
        return normalizedBase + normalizedPath;
    }

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
