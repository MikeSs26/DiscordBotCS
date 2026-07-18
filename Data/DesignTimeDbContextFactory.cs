using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBotCS.Data;

///<summary>
///used only by the EF Core CLI tools (migrations) so they don't need to build the full host.
///</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite("Data Source=bot.db")
            .Options;

        return new BotDbContext(options);
    }
}
