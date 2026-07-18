namespace DiscordBotCS.Configuration;

///<summary>
///strongly-typed bot configuration bound from the "Discord" section.
///</summary>
public sealed class DiscordSettings
{
    public const string SectionName = "Discord";

    ///<summary>Bot token. Provided via User Secrets or environment variables, never source control.</summary>
    public string Token { get; init; } = string.Empty;

    ///<summary>
    ///when set, slash commands are registered to this guild only (instant, ideal for development).
    ///when null, commands register globally (can take up to an hour to propagate).
    ///</summary>
    public ulong? TestGuildId { get; init; }
}
