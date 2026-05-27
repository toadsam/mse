using System;
using System.Collections.Generic;
using UnityEngine;

public class MatchResultService : MonoBehaviour
{
    public static MatchResultService Instance { get; private set; }

    public event Action<MatchResultRequest> SaveStarted;
    public event Action<MatchResponse> SaveSucceeded;
    public event Action<MatchHistoryResponse> HistoryLoaded;
    public event Action<string> SaveFailed;
    public event Action<string> HistoryFailed;

    public bool IsSaving { get; private set; }
    public string LastError { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject obj = new GameObject("MatchResultService");
        DontDestroyOnLoad(obj);
        obj.AddComponent<MatchResultService>();
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

    public void SaveResult(long player1Id, long player2Id, long winnerId, int player1Score, int player2Score)
    {
        SaveResult(CreateBasicRequest(player1Id, player2Id, winnerId, player1Score, player2Score));
    }

    public void SaveResult(MatchResultRequest request)
    {
        if (BackendApiClient.Instance == null)
        {
            FailSave("BackendApiClient가 아직 준비되지 않았어.");
            return;
        }

        if (!BackendSession.IsLoggedIn)
        {
            FailSave("매치 결과 저장은 로그인 후에 가능해.");
            return;
        }

        if (!ValidateResult(request))
            return;

        EnsurePlayerPayload(request);

        IsSaving = true;
        LastError = string.Empty;
        SaveStarted?.Invoke(request);

        BackendApiClient.Instance.SaveMatchResult(request, match =>
        {
            IsSaving = false;
            LastError = string.Empty;
            SaveSucceeded?.Invoke(match);
        }, FailSave);
    }

    public void LoadMyHistory(int page = 0, int size = 20)
    {
        if (BackendApiClient.Instance == null)
        {
            FailHistory("BackendApiClient가 아직 준비되지 않았어.");
            return;
        }

        if (!BackendSession.IsLoggedIn)
        {
            FailHistory("매치 기록 조회는 로그인 후에 가능해.");
            return;
        }

        BackendApiClient.Instance.GetMatchHistory(page, size, history =>
        {
            LastError = string.Empty;
            HistoryLoaded?.Invoke(history);
        }, FailHistory);
    }

    private bool ValidateResult(MatchResultRequest request)
    {
        if (request == null)
        {
            FailSave("저장할 매치 결과가 없어.");
            return false;
        }

        if (request.player1Id <= 0 || request.player2Id <= 0 || request.winnerId <= 0)
        {
            FailSave("player1Id, player2Id, winnerId는 0보다 커야 해.");
            return false;
        }

        if (request.player1Id == request.player2Id)
        {
            FailSave("1P와 2P는 서로 다른 유저여야 해.");
            return false;
        }

        if (request.winnerId != request.player1Id && request.winnerId != request.player2Id)
        {
            FailSave("승자 ID는 1P 또는 2P 중 하나여야 해.");
            return false;
        }

        request.player1Score = Mathf.Clamp(request.player1Score, 0, 10);
        request.player2Score = Mathf.Clamp(request.player2Score, 0, 10);
        return true;
    }

    public static MatchResultRequest CreateBasicRequest(long player1Id, long player2Id, long winnerId, int player1Score, int player2Score)
    {
        MatchResultRequest request = new MatchResultRequest
        {
            player1Id = player1Id,
            player2Id = player2Id,
            winnerId = winnerId,
            player1Score = Mathf.Clamp(player1Score, 0, 10),
            player2Score = Mathf.Clamp(player2Score, 0, 10)
        };

        EnsurePlayerPayload(request);
        return request;
    }

    public static void EnsurePlayerPayload(MatchResultRequest request)
    {
        if (request == null)
            return;

        if (request.players != null && request.players.Count == 2)
            return;

        request.players = new List<MatchPlayerResultRequest>
        {
            CreatePlayerResult(request.player1Id, request.winnerId == request.player1Id, request.player1Score),
            CreatePlayerResult(request.player2Id, request.winnerId == request.player2Id, request.player2Score)
        };
    }

    private static MatchPlayerResultRequest CreatePlayerResult(long userId, bool isWinner, int score)
    {
        return new MatchPlayerResultRequest
        {
            userId = userId,
            result = isWinner ? "WIN" : "LOSE",
            score = Mathf.Clamp(score, 0, 10),
            damageDealt = 0,
            characterName = string.Empty,
            augments = new List<MatchPlayerAugmentRequest>()
        };
    }

    private void FailSave(string message)
    {
        IsSaving = false;
        LastError = message;
        SaveFailed?.Invoke(message);
    }

    private void FailHistory(string message)
    {
        LastError = message;
        HistoryFailed?.Invoke(message);
    }
}
