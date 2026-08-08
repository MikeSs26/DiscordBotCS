using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBotCS.Configuration;
using DiscordBotCS.Data;
using DiscordBotCS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

//layer secrets on top of appsettings so the token never lives in source control.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.Configure<DiscordSettings>(
    builder.Configuration.GetSection(DiscordSettings.SectionName));

builder.Services.AddDbContextFactory<BotDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

var socketConfig = new DiscordSocketConfig
{
    //GuildMembers (privileged) is required for the UserJoined event and member caching.
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = true,
    LogGatewayIntentWarnings = false
};

builder.Services
    .AddSingleton(socketConfig)
    .AddSingleton<DiscordSocketClient>()
    .AddSingleton(sp => new InteractionService(
        sp.GetRequiredService<DiscordSocketClient>(),
        new InteractionServiceConfig { UseCompiledLambda = true }))
    .AddSingleton<InteractionHandler>()
    .AddSingleton<IGatewayEventHandler>(sp => sp.GetRequiredService<InteractionHandler>())
    .AddSingleton<IGatewayEventHandler, WelcomeHandler>()
    .AddSingleton<ModerationLogService>()
    .AddHostedService<DiscordBotService>()
    .AddHostedService<HeartbeatService>();

var host = builder.Build();

//apply pending migrations before the bot connects so the schema is always ready.
await using (var scope = host.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

await host.RunAsync();

///<summary>Anchor type used to locate the User Secrets assembly.</summary>
public partial class Program;
