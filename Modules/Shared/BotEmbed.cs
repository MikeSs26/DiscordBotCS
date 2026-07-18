using Discord;

namespace DiscordBotCS.Modules.Shared;

/// <summary>Factory for embeds with a consistent look across every command.</summary>
public static class BotEmbed
{
    public static readonly Color Brand = new(88, 101, 242);   // Discord blurple
    public static readonly Color Success = new(87, 242, 135);
    public static readonly Color Danger = new(237, 66, 69);

    public static EmbedBuilder Create(string? title = null, Color? color = null) =>
        new EmbedBuilder()
            .WithColor(color ?? Brand)
            .WithTitle(title)
            .WithCurrentTimestamp();
}
