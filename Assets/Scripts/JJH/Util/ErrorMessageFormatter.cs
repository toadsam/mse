using System.Text.RegularExpressions;
using UnityEngine;

// file: Assets/Scripts/JJH/Util/ErrorMessageFormatter.cs
// Maps raw backend/auth transport errors to short user-facing messages for UI display.
public static class ErrorMessageFormatter
{
    // Formats generic backend errors with the default context.
    public static string ToFriendly(string rawError)
    {
        return ToFriendly(rawError, AuthErrorContext.General);
    }

    // Formats auth errors with login/signup-specific wording.
    public static string ToAuthFriendly(string rawError, bool isLogin)
    {
        return ToFriendly(rawError, isLogin ? AuthErrorContext.Login : AuthErrorContext.Signup);
    }

    // Applies keyword and HTTP status heuristics to convert raw errors into player-friendly copy.
    private static string ToFriendly(string rawError, AuthErrorContext context)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return GetDefaultMessage(context);

        Debug.Log($"[ErrorMessageFormatter] Raw error: {rawError}");

        int statusCode = ExtractHttpStatus(rawError);
        string normalized = rawError.ToLowerInvariant();

        if (ContainsAny(normalized, "networkerror", "cannot connect", "unable to connect", "connection refused", "failed to connect"))
            return "Unable to reach the server. Please check your connection and try again.";

        if (ContainsAny(normalized, "timeout", "timed out"))
            return "The request timed out. Please try again.";

        if (statusCode >= 500)
            return "The server is temporarily unavailable. Please try again later.";

        if (ContainsAny(normalized, "size must be between 8 and 64", "password must be between"))
            return "Password must be 8 to 64 characters.";

        if (ContainsAny(normalized, "size must be between 2 and 30", "nickname must be between"))
            return "Nickname must be 2 to 30 characters.";

        if (ContainsAny(normalized, "user id already exists", "email already exists", "email_already_exists", "user_id_already_exists"))
            return "This ID is already in use.";

        if (ContainsAny(normalized, "nickname already exists", "nickname_already_exists"))
            return "This nickname is already in use.";

        if (ContainsAny(normalized, "wrong password", "wrong_password", "invalid credentials", "invalid_credentials", "incorrect password"))
            return "The ID or password is incorrect.";

        if (ContainsAny(normalized, "user not found", "user_not_found"))
            return context == AuthErrorContext.Login
                ? "No account was found for this ID."
                : "The account could not be found.";

        if (ContainsAny(normalized, "must not be blank", "notblank"))
            return context == AuthErrorContext.Login
                ? "Please enter your ID and password."
                : "Please fill in all required fields.";

        if (ContainsAny(normalized, "invalid token", "token expired", "invalid_token", "token_expired"))
            return "Your session has expired. Please log in again.";

        if (statusCode == 400)
            return context == AuthErrorContext.Login
                ? "Please enter your ID and password."
                : "Please check your information and try again.";

        if (statusCode == 401)
            return context == AuthErrorContext.Login
                ? "The ID or password is incorrect."
                : "Your session has expired. Please log in again.";

        if (statusCode == 403)
            return "You do not have permission to do that.";

        if (statusCode == 404)
            return context == AuthErrorContext.Login
                ? "No account was found for this ID."
                : "The requested information could not be found.";

        if (statusCode == 409)
            return context == AuthErrorContext.Signup
                ? "This ID is already in use."
                : "This information is already in use.";

        return GetDefaultMessage(context);
    }

    // Extracts the leading HTTP status from errors shaped like "HTTP 401: ...".
    private static int ExtractHttpStatus(string rawError)
    {
        Match match = Regex.Match(rawError, @"^HTTP\s+(\d{3})");
        if (!match.Success)
            return -1;

        return int.TryParse(match.Groups[1].Value, out int statusCode) ? statusCode : -1;
    }

    // Utility matcher for a case-normalized error string against multiple phrases.
    private static bool ContainsAny(string value, params string[] patterns)
    {
        foreach (string pattern in patterns)
        {
            if (value.Contains(pattern))
                return true;
        }

        return false;
    }

    // Fallback message when no more specific backend error pattern matched.
    private static string GetDefaultMessage(AuthErrorContext context)
    {
        switch (context)
        {
            case AuthErrorContext.Login:
                return "Could not log in. Please check your ID and password.";
            case AuthErrorContext.Signup:
                return "Could not sign up. Please check your information and try again.";
            default:
                return "The request could not be completed. Please try again.";
        }
    }

    private enum AuthErrorContext
    {
        General,
        Login,
        Signup
    }
}
