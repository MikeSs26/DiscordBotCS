using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBotCS.Modules.Shared;
using DiscordBotCS.Services;

namespace DiscordBotCS.Modules;

///<summary>
///Moderation commands. Top-level rather than grouped: they get typed in a hurry.
///Every action is mirrored to the guild's log channel when one is configured.
///</summary>
[RequireContext(ContextType.Guild)]
public sealed class ModerationModule : InteractionModuleBase<SocketInteractionContext>
{
    //Discord refuses to bulk delete messages older than this.
    private static readonly TimeSpan BulkDeleteWindow = TimeSpan.FromDays(14);

    //28 days, Discord's hard ceiling for a timeout.
    private const int MaxTimeoutMinutes = 40320;

    private readonly ModerationLogService _log;

    public ModerationModule(ModerationLogService log) => _log = log;

    [SlashCommand("limpiar", "Elimina mensajes recientes de este canal.")]
    [DefaultMemberPermissions(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task PurgeAsync(
        [Summary("cantidad", "Cuántos mensajes recientes revisar (1-100).")]
        [MinValue(1)][MaxValue(100)] int cantidad,
        [Summary("usuario", "Si lo indicas, solo se borran los mensajes de este usuario.")]
        IUser? usuario = null)
    {
        await DeferAsync(ephemeral: true);

        if (Context.Channel is not ITextChannel channel)
        {
            await FollowupAsync("Este comando solo funciona en canales de texto.", ephemeral: true);
            return;
        }

        var messages = (await channel.GetMessagesAsync(cantidad).FlattenAsync()).ToList();

        if (usuario is not null)
            messages = messages.Where(m => m.Author.Id == usuario.Id).ToList();

        var cutoff = DateTimeOffset.UtcNow - BulkDeleteWindow;
        var deletable = messages.Where(m => m.Timestamp > cutoff).ToList();
        var tooOld = messages.Count - deletable.Count;

        if (deletable.Count == 0)
        {
            var nothing = tooOld > 0
                ? "No hay mensajes que pueda borrar: todos tienen más de 14 días."
                : "No se encontraron mensajes que coincidan.";
            await FollowupAsync(nothing, ephemeral: true);
            return;
        }

        //the bulk endpoint requires at least two messages.
        if (deletable.Count == 1)
            await deletable[0].DeleteAsync();
        else
            await channel.DeleteMessagesAsync(deletable);

        var summary = $"✅ Eliminados **{deletable.Count}** mensajes.";
        if (tooOld > 0)
            summary += $" Se omitieron {tooOld} por tener más de 14 días.";

        await FollowupAsync(summary, ephemeral: true);

        var target = usuario is null ? "" : $" de {usuario.Mention}";
        await _log.LogAsync(
            Context.Guild,
            ModerationAction.Purge,
            Context.User,
            $"Se eliminaron **{deletable.Count}** mensajes{target} en {channel.Mention}.");
    }

    [SlashCommand("timeout", "Aísla temporalmente a un miembro.")]
    [DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task TimeoutAsync(
        [Summary("usuario", "Miembro a aislar.")] SocketGuildUser usuario,
        [Summary("minutos", "Duración en minutos (hasta 40320, es decir 28 días).")]
        [MinValue(1)][MaxValue(MaxTimeoutMinutes)] int minutos,
        [Summary("razón", "Motivo del aislamiento.")] string? razon = null)
    {
        if (await RefuseIfOutrankedAsync(usuario))
            return;

        var duration = TimeSpan.FromMinutes(minutos);
        await usuario.SetTimeOutAsync(duration, new RequestOptions { AuditLogReason = razon });

        var until = DateTimeOffset.UtcNow + duration;
        await RespondAsync(
            $"🔇 {usuario.Mention} aislado hasta <t:{until.ToUnixTimeSeconds()}:f>.",
            ephemeral: true);

        await _log.LogAsync(
            Context.Guild,
            ModerationAction.Timeout,
            Context.User,
            $"{usuario.Mention} (`{usuario.Id}`) fue aislado durante **{minutos} min**, " +
            $"hasta <t:{until.ToUnixTimeSeconds()}:f>.",
            razon);
    }

    [SlashCommand("expulsar", "Expulsa a un miembro del servidor.")]
    [DefaultMemberPermissions(GuildPermission.KickMembers)]
    [RequireBotPermission(GuildPermission.KickMembers)]
    public async Task KickAsync(
        [Summary("usuario", "Miembro a expulsar.")] SocketGuildUser usuario,
        [Summary("razón", "Motivo de la expulsión.")] string? razon = null)
    {
        if (await RefuseIfOutrankedAsync(usuario))
            return;

        //capture before the kick: the member object stops resolving afterwards.
        var label = $"{usuario.Username} (`{usuario.Id}`)";

        await usuario.KickAsync(razon);

        await RespondAsync($"👢 {label} ha sido expulsado.", ephemeral: true);

        await _log.LogAsync(
            Context.Guild,
            ModerationAction.Kick,
            Context.User,
            $"{label} fue expulsado del servidor.",
            razon);
    }

    ///<summary>
    ///Replies with the reason and returns true when the action must not proceed.
    ///</summary>
    private async Task<bool> RefuseIfOutrankedAsync(SocketGuildUser target)
    {
        if (Context.User is not SocketGuildUser moderator)
        {
            await RespondAsync("No pude verificar tus permisos en este servidor.", ephemeral: true);
            return true;
        }

        var bot = Context.Guild.CurrentUser;
        var verdict = ModerationGuard.Check(
            new ModerationSubject(moderator.Id, moderator.Hierarchy),
            new ModerationSubject(target.Id, target.Hierarchy),
            new ModerationSubject(bot.Id, bot.Hierarchy),
            Context.Guild.OwnerId);

        if (verdict is ModerationCheck.Allowed)
            return false;

        await RespondAsync(Explain(verdict), ephemeral: true);
        return true;
    }

    private static string Explain(ModerationCheck verdict) => verdict switch
    {
        ModerationCheck.TargetIsSelf => "No puedes aplicarte esta acción a ti mismo.",
        ModerationCheck.TargetIsBot => "No puedo aplicarme esta acción a mí mismo.",
        ModerationCheck.TargetIsOwner => "No se puede moderar al propietario del servidor.",
        ModerationCheck.TargetOutranksModerator =>
            "Ese miembro tiene un rol igual o superior al tuyo.",
        ModerationCheck.TargetOutranksBot =>
            "Ese miembro tiene un rol igual o superior al mío. Sube mi rol para poder actuar.",
        _ => "No se permite esta acción."
    };
}
