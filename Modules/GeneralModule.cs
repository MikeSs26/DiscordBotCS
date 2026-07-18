using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBotCS.Modules.Shared;

namespace DiscordBotCS.Modules;

///<summary>General-purpose commands: health checks and bot info.</summary>
public sealed class GeneralModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;
    private readonly DiscordSocketClient _client;

    public GeneralModule(DiscordSocketClient client) => _client = client;

    [SlashCommand("ping", "Muestra la latencia del bot.")]
    public async Task PingAsync()
    {
        var gatewayLatency = _client.Latency;

        //measure round-trip: time from responding until Discord acknowledges the edit.
        var sw = Stopwatch.StartNew();
        await RespondAsync("🏓 Calculando...");
        sw.Stop();

        var embed = BotEmbed.Create("🏓 Pong")
            .AddField("Gateway", $"{gatewayLatency} ms", inline: true)
            .AddField("Respuesta", $"{sw.ElapsedMilliseconds} ms", inline: true)
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = null;
            msg.Embed = embed;
        });
    }

    [SlashCommand("info", "Información general sobre el bot.")]
    public async Task InfoAsync()
    {
        var uptime = DateTimeOffset.UtcNow - StartedAt;

        var embed = BotEmbed.Create($"Información de {_client.CurrentUser.Username}")
            .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl() ?? _client.CurrentUser.GetDefaultAvatarUrl())
            .AddField("Servidores", _client.Guilds.Count, inline: true)
            .AddField("Latencia", $"{_client.Latency} ms", inline: true)
            .AddField("Activo desde", $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s", inline: true)
            .AddField("Librería", $"Discord.Net {DiscordConfig.Version}", inline: true)
            .Build();

        await RespondAsync(embed: embed);
    }
}
