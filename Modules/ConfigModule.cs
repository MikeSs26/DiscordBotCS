using Discord;
using Discord.Interactions;
using DiscordBotCS.Data;
using DiscordBotCS.Data.Entities;
using DiscordBotCS.Modules.Shared;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotCS.Modules;

///<summary>Per-guild configuration. Requires Manage Server and only works inside a guild.</summary>
[Group("config", "Configuración del bot para este servidor.")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public sealed class ConfigModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;

    public ConfigModule(IDbContextFactory<BotDbContext> dbFactory) => _dbFactory = dbFactory;

    [SlashCommand("ver", "Muestra la configuración actual del servidor.")]
    public async Task ViewAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var config = await db.GuildConfigs.FindAsync(Context.Guild.Id);

        var embed = BotEmbed.Create("Configuración del servidor")
            .AddField("Canal de logs", Mention(config?.LogChannelId), inline: true)
            .AddField("Canal de bienvenidas", Mention(config?.WelcomeChannelId), inline: true)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("canal-log", "Define el canal donde el bot publicará los registros.")]
    public async Task SetLogChannelAsync(
        [Summary("canal", "Canal de texto para los logs.")]
        [ChannelTypes(ChannelType.Text)] ITextChannel channel)
    {
        var config = await GetOrCreateConfigAsync();
        config.LogChannelId = channel.Id;
        await SaveAsync(config);

        await RespondAsync($"✅ Canal de logs establecido en {channel.Mention}.", ephemeral: true);
    }

    [SlashCommand("canal-bienvenida", "Define el canal donde se publicarán las bienvenidas.")]
    public async Task SetWelcomeChannelAsync(
        [Summary("canal", "Canal de texto para las bienvenidas.")]
        [ChannelTypes(ChannelType.Text)] ITextChannel channel)
    {
        var config = await GetOrCreateConfigAsync();
        config.WelcomeChannelId = channel.Id;
        await SaveAsync(config);

        await RespondAsync($"✅ Canal de bienvenidas establecido en {channel.Mention}.", ephemeral: true);
    }

    private async Task<GuildConfig> GetOrCreateConfigAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GuildConfigs.FindAsync(Context.Guild.Id)
               ?? new GuildConfig { GuildId = Context.Guild.Id };
    }

    private async Task SaveAsync(GuildConfig config)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        //upsert: attach as added when new, updated when it already exists.
        var exists = await db.GuildConfigs.AnyAsync(g => g.GuildId == config.GuildId);
        db.Entry(config).State = exists ? EntityState.Modified : EntityState.Added;
        await db.SaveChangesAsync();
    }

    private static string Mention(ulong? channelId) =>
        channelId is ulong id ? $"<#{id}>" : "*sin configurar*";
}
