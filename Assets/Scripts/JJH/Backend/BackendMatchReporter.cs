using System;
using UnityEngine;

// file: Assets/Scripts/JJH/Backend/BackendMatchReporter.cs
// Convenience helper for reporting simple match results without constructing the payload manually.
public static class BackendMatchReporter
{
    public static bool CanReport => BackendApiClient.Instance != null && BackendSession.IsLoggedIn;

    // Builds the default 1v1 match payload and forwards it to the backend API client.
    public static void ReportResult(long player1Id, long player2Id, long winnerId, int player1Score, int player2Score, Action<MatchResponse> onSuccess = null, Action<string> onError = null)
    {
        if (BackendApiClient.Instance == null)
        {
            onError?.Invoke("BackendApiClient is not initialized.");
            return;
        }

        MatchResultRequest request = MatchResultService.CreateBasicRequest(player1Id, player2Id, winnerId, player1Score, player2Score);

        BackendApiClient.Instance.SaveMatchResult(request, onSuccess, onError);
    }
}
