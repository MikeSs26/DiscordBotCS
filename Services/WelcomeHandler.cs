using Discord.WebSocket;
using DiscordBotCS.Data;
using DiscordBotCS.Modules.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBotCS.Services;

///<summary>Posts a welcome message when a member joins, if the guild configured a channel.</summary>
public sealed class WelcomeHandler : IGatewayEventHandler
{
    private readonly DiscordSocketClient _client;
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly ILogger<WelcomeHandler> _logger;

    public WelcomeHandler(
        DiscordSocketClient client,
        IDbContextFactory<BotDbContext> dbFactory,
        ILogger<WelcomeHandler> logger)
    {
        _client = client;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _client.UserJoined += OnUserJoinedAsync;
        return Task.CompletedTask;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var config = await db.GuildConfigs.FindAsync(user.Guild.Id);

        if (config?.WelcomeChannelId is not ulong channelId)
            return;

        if (user.Guild.GetTextChannel(channelId) is not { } channel)
        {
            _logger.LogWarning(
                "Welcome channel {ChannelId} not found in guild {GuildId}.", channelId, user.Guild.Id);
            return;
        }

        var embed = BotEmbed.Create(color: BotEmbed.Success)
            .WithDescription($"¡Bienvenido/a {user.Mention} a **{user.Guild.Name}**! 🎉")
            .WithThumbnailUrl(user.GetDisplayAvatarUrl(size: 256))
            .AddField("Miembro nº", user.Guild.MemberCount, inline: true)
            .AddField("Cuenta creada", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>", inline: true)
            .Build();

        try
        {
            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send welcome message in guild {GuildId}.", user.Guild.Id);
        }
    }
}
