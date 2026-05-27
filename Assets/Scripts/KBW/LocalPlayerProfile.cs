public static class LocalPlayerProfile
{
    public const int MaxNameLength = 16;

    public static string PlayerName { get; private set; } = "Player";
    public static byte CharacterId { get; private set; } = 0;

    public static void SetProfile(string playerName, byte characterId)
    {
        playerName = (playerName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        if (playerName.Length > MaxNameLength)
            playerName = playerName.Substring(0, MaxNameLength);

        PlayerName = playerName;
        CharacterId = characterId;
    }
}