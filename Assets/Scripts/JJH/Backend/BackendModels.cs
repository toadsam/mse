using System;
using System.Collections.Generic;

// file: Assets/Scripts/JJH/Backend/BackendModels.cs
// Serializable request/response DTOs shared by the Unity backend integration layer.
[Serializable]
public class BackendApiResponse<T>
{
    public bool success;
    public T data;
    public string error;
}

[Serializable]
public class SignupRequest
{
    // Uses email as the backend login identifier.
    public string email;
    public string password;
    public string nickname;
}

[Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[Serializable]
public class RefreshRequest
{
    public string refreshToken;
}

[Serializable]
public class AuthResponse
{
    public string accessToken;
    public string refreshToken;
    public long userId;
    public string email;
    public string nickname;
}

[Serializable]
public class UserMeResponse
{
    public long id;
    public string email;
    public string nickname;
    public string createdAt;
}

[Serializable]
public class UserUpdateRequest
{
    public string nickname;
}

[Serializable]
public class MatchResultRequest
{
    public long player1Id;
    public long player2Id;
    public long winnerId;
    public int player1Score;
    public int player2Score;
    // The backend expects exactly two per-player result entries for a completed 1v1 match.
    public List<MatchPlayerResultRequest> players;
}

[Serializable]
public class MatchPlayerResultRequest
{
    public long userId;
    public string result;
    public int score;
    public int damageDealt;
    public string characterName;
    public List<MatchPlayerAugmentRequest> augments;
}

[Serializable]
public class MatchPlayerAugmentRequest
{
    // Optional backend augment identifier; 0 can be used when only the name is known.
    public long augmentId;
    public string augmentName;
    public int selectedOrder;
    public int selectedRound;
}

[Serializable]
public class MatchResponse
{
    public long id;
    public long player1Id;
    public long player2Id;
    public long winnerId;
    public int player1Score;
    public int player2Score;
    public string createdAt;
}

[Serializable]
public class MatchHistoryResponse
{
    public List<MatchResponse> content;
    public int page;
    public int size;
    public long totalElements;
    public int totalPages;
}

[Serializable]
public class LeaderboardEntry
{
    public long userId;
    public string nickname;
    public long totalMatches;
    public long totalWins;
    public double winRate;
}

[Serializable]
public class AugmentResponse
{
    public long id;
    public string name;
    public string description;
    public string effectType;
}

[Serializable]
public class BackendErrorResponse
{
    public bool success;
    public string error;
}
