using DiscordBotCS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotCS.Data;

public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    public DbSet<GuildConfig> GuildConfigs => Set<GuildConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuildConfig>(entity =>
        {
            entity.HasKey(g => g.GuildId);
            entity.Property(g => g.GuildId).ValueGeneratedNever();
        });
    }
}
