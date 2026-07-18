namespace DiscordBotCS.Data.Entities;

///<summary>Per-guild settings. One row per server the bot is in.</summary>
public sealed class GuildConfig
{
    ///<summary>Discord guild (server) snowflake. Primary key.</summary>
    public ulong GuildId { get; set; }

    ///<summary>Channel where the bot posts audit/log messages, if configured.</summary>
    public ulong? LogChannelId { get; set; }

    ///<summary>Channel where welcome messages are posted, if configured.</summary>
    public ulong? WelcomeChannelId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
