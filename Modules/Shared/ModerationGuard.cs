namespace DiscordBotCS.Modules.Shared;

///<summary>A participant in a moderation action, reduced to what the rank rules need.</summary>
///<param name="Id">Discord user snowflake.</param>
///<param name="Hierarchy">Position of the user's highest role. Discord.Net exposes this
///as <c>SocketGuildUser.Hierarchy</c>.</param>
public readonly record struct ModerationSubject(ulong Id, int Hierarchy);

///<summary>Why a moderation action was refused, or that it may proceed.</summary>
public enum ModerationCheck
{
    Allowed,
    TargetIsSelf,
    TargetIsBot,
    TargetIsOwner,
    TargetOutranksModerator,
    TargetOutranksBot
}

///<summary>
///Rank rules for moderation actions. Pure logic, deliberately free of Discord.Net types so
///the edge cases can be tested without simulating a guild.
///</summary>
public static class ModerationGuard
{
    ///<summary>
    ///Decides whether <paramref name="moderator"/> may act on <paramref name="target"/>.
    ///Order matters: it determines which reason the moderator is told when several apply.
    ///</summary>
    public static ModerationCheck Check(
        ModerationSubject moderator,
        ModerationSubject target,
        ModerationSubject bot,
        ulong guildOwnerId)
    {
        if (target.Id == moderator.Id)
            return ModerationCheck.TargetIsSelf;

        if (target.Id == bot.Id)
            return ModerationCheck.TargetIsBot;

        if (target.Id == guildOwnerId)
            return ModerationCheck.TargetIsOwner;

        //ownership outranks everything, so the owner skips the rank comparison.
        if (moderator.Id != guildOwnerId && target.Hierarchy >= moderator.Hierarchy)
            return ModerationCheck.TargetOutranksModerator;

        //nobody can act through the bot on someone the bot cannot touch.
        if (target.Hierarchy >= bot.Hierarchy)
            return ModerationCheck.TargetOutranksBot;

        return ModerationCheck.Allowed;
    }
}
