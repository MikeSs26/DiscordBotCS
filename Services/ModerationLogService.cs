using Discord;
using DiscordBotCS.Data;
using DiscordBotCS.Modules.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBotCS.Services;

///<summary>Kind of moderation action, which decides how the log entry looks.</summary>
public enum ModerationAction
{
    Purge,
    Timeout,
    Kick
}

///<summary>
///Writes moderation entries to the guild's configured log channel. Callers say what
///happened; this service decides where it goes and how it looks.
///</summary>
public sealed class ModerationLogService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly ILogger<ModerationLogService> _logger;

    public ModerationLogService(
        IDbContextFactory<BotDbContext> dbFactory,
        ILogger<ModerationLogService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    ///<summary>
    ///Records an action, or does nothing if the guild has no log channel configured.
    ///Never throws: the action being logged has already happened, so a logging problem
    ///must not turn into a failed command for the moderator.
    ///</summary>
    public async Task LogAsync(
        IGuild guild,
        ModerationAction action,
        IUser moderator,
        string description,
        string? reason = null)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var config = await db.GuildConfigs.FindAsync(guild.Id);

            if (config?.LogChannelId is not ulong channelId)
                return;

            if (await guild.GetTextChannelAsync(channelId) is not { } channel)
            {
                _logger.LogWarning(
                    "Log channel {ChannelId} not found in guild {GuildId}.", channelId, guild.Id);
                return;
            }

            var (title, color) = Describe(action);
            var embed = BotEmbed.Create(title, color)
                .WithDescription(description)
                .AddField("Moderador", $"{moderator.Mention} (`{moderator.Id}`)");

            if (!string.IsNullOrWhiteSpace(reason))
                embed.AddField("Razón", reason);

            await channel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write moderation log for guild {GuildId}.", guild.Id);
        }
    }

    private static (string Title, Color Color) Describe(ModerationAction action) => action switch
    {
        ModerationAction.Purge => ("🧹 Mensajes eliminados", BotEmbed.Brand),
        ModerationAction.Timeout => ("🔇 Aislamiento aplicado", BotEmbed.Warning),
        ModerationAction.Kick => ("👢 Miembro expulsado", BotEmbed.Danger),
        _ => ("Acción de moderación", BotEmbed.Brand)
    };
}
