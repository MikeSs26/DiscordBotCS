using Discord;
using Discord.Interactions;
using DiscordBotCS.Modules.Shared;

namespace DiscordBotCS.Modules;

///<summary>Commands that surface information about users.</summary>
[Group("usuario", "Comandos relacionados con usuarios.")]
public sealed class UserModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("avatar", "Muestra el avatar de un usuario.")]
    public async Task AvatarAsync(
        [Summary("usuario", "Usuario del que ver el avatar. Por defecto, tú.")] IUser? user = null)
    {
        user ??= Context.User;
        var avatarUrl = user.GetAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl();

        var embed = BotEmbed.Create($"Avatar de {user.Username}")
            .WithImageUrl(avatarUrl)
            .WithUrl(avatarUrl)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("info", "Muestra información sobre un usuario.")]
    public async Task InfoAsync(
        [Summary("usuario", "Usuario a consultar. Por defecto, tú.")] IUser? user = null)
    {
        user ??= Context.User;

        var embed = BotEmbed.Create($"Información de {user.Username}")
            .WithThumbnailUrl(user.GetAvatarUrl(size: 256) ?? user.GetDefaultAvatarUrl())
            .AddField("Nombre", user.Username, inline: true)
            .AddField("ID", user.Id, inline: true)
            .AddField("Bot", user.IsBot ? "Sí" : "No", inline: true)
            .AddField("Cuenta creada", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>", inline: false);

        if (user is IGuildUser guildUser && guildUser.JoinedAt is { } joined)
            embed.AddField("Se unió al servidor", $"<t:{joined.ToUnixTimeSeconds()}:R>", inline: false);

        await RespondAsync(embed: embed.Build());
    }
}
