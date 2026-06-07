public static class LocalPlayerProfile
{
    public const int MaxNameLength = 16;
    public const int MaxCharacterNameLength = 24;

    public static string PlayerName { get; private set; } = "Player";
    public static byte CharacterId { get; private set; } = 0;
    public static string CharacterName { get; private set; } = "";

    public static bool HasProfile { get; private set; }

    public static void SetProfile(string playerName, byte characterId, string characterName = "")
    {
        playerName = (playerName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        if (playerName.Length > MaxNameLength)
            playerName = playerName.Substring(0, MaxNameLength);

        characterName = (characterName ?? "").Trim();

        if (characterName.Length > MaxCharacterNameLength)
            characterName = characterName.Substring(0, MaxCharacterNameLength);

        PlayerName = playerName;
        CharacterId = characterId;
        CharacterName = characterName;
        HasProfile = true;
    }
}