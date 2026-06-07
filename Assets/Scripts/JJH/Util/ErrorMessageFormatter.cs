using System.Text.RegularExpressions;
using UnityEngine;

public static class ErrorMessageFormatter
{
    // Strips "HTTP 4xx/5xx: " prefix and returns the remainder.
    // Capitalizes the first character and ensures the string ends with a period.
    public static string ToFriendly(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return "Something went wrong. Please try again.";

        Debug.Log($"[ErrorMessageFormatter] Raw error: {rawError}");

        // HTTP 5xx -> always generic (internal server errors are meaningless to users)
        if (Regex.IsMatch(rawError, @"^HTTP\s+5\d{2}"))
            return "Something went wrong. Please try again.";

        // Known patterns -> best friendly message
        if (rawError.Contains("size must be between 8 and 64") || rawError.Contains("Password must be between"))
            return "Password must be between 8 and 64 characters.";
        if (rawError.Contains("USER_NOT_FOUND") || rawError.Contains("User not found"))
            return "User not found. Please sign up first.";
        if (rawError.Contains("WRONG_PASSWORD") || rawError.Contains("Wrong password") || rawError.Contains("Incorrect password"))
            return "Incorrect password. Please try again.";
        if (rawError.Contains("EMAIL_ALREADY_EXISTS") || rawError.Contains("USER_ID_ALREADY_EXISTS")
            || rawError.Contains("User ID already exists") || rawError.Contains("Email already exists"))
            return "User ID already exists. Please log in.";
        if (rawError.Contains("LOGIN_FAILED"))
            return "Login failed. Please try again.";
        if (rawError.Contains("SIGNUP_FAILED"))
            return "Sign up failed. Please try again.";
        if (rawError.Contains("NetworkError") || rawError.Contains("Cannot connect") || rawError.Contains("Unable to connect"))
            return "Network error. Please check your connection.";

        // Unknown errors: strip "HTTP xxx: " prefix and show the actual backend message
        string stripped = Regex.Replace(rawError, @"^HTTP\s+\d{3}:\s*", string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(stripped))
            return "Something went wrong. Please try again.";

        // Capitalize first letter and ensure ends with period
        stripped = char.ToUpper(stripped[0]) + stripped.Substring(1);
        if (!stripped.EndsWith(".") && !stripped.EndsWith("!") && !stripped.EndsWith("?"))
            stripped += ".";

        return stripped;
    }
}
