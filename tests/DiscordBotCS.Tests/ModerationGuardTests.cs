using DiscordBotCS.Modules.Shared;

namespace DiscordBotCS.Tests;

public class ModerationGuardTests
{
    private const ulong OwnerId = 1;
    private const ulong BotId = 2;
    private const ulong ModeratorId = 3;
    private const ulong TargetId = 4;

    private static readonly ModerationSubject Bot = new(BotId, Hierarchy: 100);
    private static readonly ModerationSubject Moderator = new(ModeratorId, Hierarchy: 50);

    private static ModerationCheck Check(ModerationSubject target, ModerationSubject? moderator = null) =>
        ModerationGuard.Check(moderator ?? Moderator, target, Bot, OwnerId);

    [Fact]
    public void Allows_target_below_both_moderator_and_bot()
    {
        Assert.Equal(ModerationCheck.Allowed, Check(new ModerationSubject(TargetId, 10)));
    }

    [Fact]
    public void Blocks_moderating_yourself()
    {
        Assert.Equal(ModerationCheck.TargetIsSelf, Check(new ModerationSubject(ModeratorId, 10)));
    }

    [Fact]
    public void Blocks_moderating_the_bot()
    {
        Assert.Equal(ModerationCheck.TargetIsBot, Check(new ModerationSubject(BotId, 100)));
    }

    [Fact]
    public void Blocks_moderating_the_guild_owner()
    {
        Assert.Equal(ModerationCheck.TargetIsOwner, Check(new ModerationSubject(OwnerId, 10)));
    }

    [Fact]
    public void Blocks_target_ranked_above_the_moderator()
    {
        Assert.Equal(ModerationCheck.TargetOutranksModerator, Check(new ModerationSubject(TargetId, 60)));
    }

    [Fact]
    public void Blocks_target_with_the_same_rank_as_the_moderator()
    {
        // Equal rank cannot act on equal rank: Discord rejects it too.
        Assert.Equal(ModerationCheck.TargetOutranksModerator, Check(new ModerationSubject(TargetId, 50)));
    }

    [Fact]
    public void Blocks_target_ranked_above_the_bot_even_when_the_moderator_outranks_them()
    {
        var highModerator = new ModerationSubject(ModeratorId, 200);
        Assert.Equal(ModerationCheck.TargetOutranksBot, Check(new ModerationSubject(TargetId, 150), highModerator));
    }

    [Fact]
    public void Owner_may_moderate_a_member_ranked_above_them()
    {
        // The owner's own role position is irrelevant; ownership outranks everything.
        var owner = new ModerationSubject(OwnerId, 1);
        Assert.Equal(ModerationCheck.Allowed, Check(new ModerationSubject(TargetId, 90), owner));
    }

    [Fact]
    public void Owner_is_still_bound_by_what_the_bot_can_reach()
    {
        var owner = new ModerationSubject(OwnerId, 1);
        Assert.Equal(ModerationCheck.TargetOutranksBot, Check(new ModerationSubject(TargetId, 100), owner));
    }

    [Fact]
    public void Self_check_wins_when_the_owner_targets_themselves()
    {
        var owner = new ModerationSubject(OwnerId, 1);
        Assert.Equal(ModerationCheck.TargetIsSelf, Check(new ModerationSubject(OwnerId, 1), owner));
    }
}
