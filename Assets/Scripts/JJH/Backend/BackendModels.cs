using System;
using System.Collections.Generic;

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
