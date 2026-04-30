using System;
using UnityEngine;

public static class BackendMatchReporter
{
    public static bool CanReport => BackendApiClient.Instance != null && BackendSession.IsLoggedIn;

    public static void ReportResult(long player1Id, long player2Id, long winnerId, int player1Score, int player2Score, Action<MatchResponse> onSuccess = null, Action<string> onError = null)
    {
        if (BackendApiClient.Instance == null)
        {
            onError?.Invoke("BackendApiClient is not initialized.");
            return;
        }

        MatchResultRequest request = new MatchResultRequest
        {
            player1Id = player1Id,
            player2Id = player2Id,
            winnerId = winnerId,
            player1Score = Mathf.Clamp(player1Score, 0, 10),
            player2Score = Mathf.Clamp(player2Score, 0, 10)
        };

        BackendApiClient.Instance.SaveMatchResult(request, onSuccess, onError);
    }
}
