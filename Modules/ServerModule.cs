using Discord;
using Discord.Interactions;
using DiscordBotCS.Modules.Shared;

namespace DiscordBotCS.Modules;

///<summary>Commands that report on the current guild. Guild-only by design.</summary>
public sealed class ServerModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("servidor", "Muestra información sobre el servidor.")]
    [RequireContext(ContextType.Guild)]
    public async Task ServerInfoAsync()
    {
        var guild = Context.Guild;

        var embed = BotEmbed.Create($"Información de {guild.Name}")
            .WithThumbnailUrl(guild.IconUrl)
            .AddField("Propietario", $"<@{guild.OwnerId}>", inline: true)
            .AddField("Miembros", guild.MemberCount, inline: true)
            .AddField("Canales", guild.Channels.Count, inline: true)
            .AddField("Roles", guild.Roles.Count, inline: true)
            .AddField("Boosts", guild.PremiumSubscriptionCount, inline: true)
            .AddField("Creado", $"<t:{guild.CreatedAt.ToUnixTimeSeconds()}:R>", inline: true)
            .WithFooter($"ID: {guild.Id}")
            .Build();

        await RespondAsync(embed: embed);
    }
}
