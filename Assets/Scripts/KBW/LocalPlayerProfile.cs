// Stores the local profile selected before joining a network match.
public static class LocalPlayerProfile
{
    // Maximum lengths used before profile data is sent over the network.
    public const int MaxNameLength = 16;
    public const int MaxCharacterNameLength = 24;

    // Selected nickname and character values for the local client.
    public static string PlayerName { get; private set; } = "Player";
    public static byte CharacterId { get; private set; } = 0;
    public static string CharacterName { get; private set; } = "";

    public static bool HasProfile { get; private set; }

    // Validates and stores local profile data.
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