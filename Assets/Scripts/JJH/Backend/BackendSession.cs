using UnityEngine;

public static class BackendSession
{
    private const string AccessTokenKey = "LastRound.AccessToken";
    private const string RefreshTokenKey = "LastRound.RefreshToken";
    private const string UserIdKey = "LastRound.UserId";
    private const string EmailKey = "LastRound.Email";
    private const string NicknameKey = "LastRound.Nickname";

    public static string AccessToken { get; private set; }
    public static string RefreshToken { get; private set; }
    public static long UserId { get; private set; }
    public static string Email { get; private set; }
    public static string Nickname { get; private set; }

    public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken) && UserId > 0;

    static BackendSession()
    {
        Load();
    }

    public static void Save(AuthResponse auth)
    {
        if (auth == null)
            return;

        AccessToken = auth.accessToken;
        RefreshToken = auth.refreshToken;
        UserId = auth.userId;
        Email = auth.email;
        Nickname = auth.nickname;

        PlayerPrefs.SetString(AccessTokenKey, AccessToken ?? string.Empty);
        PlayerPrefs.SetString(RefreshTokenKey, RefreshToken ?? string.Empty);
        PlayerPrefs.SetString(UserIdKey, UserId.ToString());
        PlayerPrefs.SetString(EmailKey, Email ?? string.Empty);
        PlayerPrefs.SetString(NicknameKey, Nickname ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void UpdateUser(UserMeResponse user)
    {
        if (user == null)
            return;

        UserId = user.id;
        Email = user.email;
        Nickname = user.nickname;

        PlayerPrefs.SetString(UserIdKey, UserId.ToString());
        PlayerPrefs.SetString(EmailKey, Email ?? string.Empty);
        PlayerPrefs.SetString(NicknameKey, Nickname ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        UserId = 0;
        Email = string.Empty;
        Nickname = string.Empty;

        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.DeleteKey(RefreshTokenKey);
        PlayerPrefs.DeleteKey(UserIdKey);
        PlayerPrefs.DeleteKey(EmailKey);
        PlayerPrefs.DeleteKey(NicknameKey);
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        AccessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        RefreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        Email = PlayerPrefs.GetString(EmailKey, string.Empty);
        Nickname = PlayerPrefs.GetString(NicknameKey, string.Empty);

        string rawUserId = PlayerPrefs.GetString(UserIdKey, "0");
        long.TryParse(rawUserId, out long userId);
        UserId = userId;
    }
}
