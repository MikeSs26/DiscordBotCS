using Discord;
using Discord.WebSocket;
using DiscordBotCS.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBotCS.Services;

///<summary>
///Hosted service that owns the bot lifecycle: forwards gateway logs, logs in,
///starts the connection, and shuts down cleanly with the host.
///</summary>
public sealed class DiscordBotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IEnumerable<IGatewayEventHandler> _eventHandlers;
    private readonly DiscordSettings _settings;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordSocketClient client,
        IEnumerable<IGatewayEventHandler> eventHandlers,
        IOptions<DiscordSettings> settings,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _eventHandlers = eventHandlers;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Token))
        {
            _logger.LogCritical(
                "No Discord token configured. Set it with: dotnet user-secrets set \"Discord:Token\" \"<token>\"");
            return;
        }

        _client.Log += OnLogAsync;
        _client.Ready += () =>
        {
            _logger.LogInformation("Connected as {User} ({Id}).", _client.CurrentUser, _client.CurrentUser.Id);
            return Task.CompletedTask;
        };

        foreach (var handler in _eventHandlers)
            await handler.InitializeAsync();

        await _client.LoginAsync(TokenType.Bot, _settings.Token);
        await _client.StartAsync();

        // Keep the service alive until the host is shutting down.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down Discord client...");
        await _client.LogoutAsync();
        await _client.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private Task OnLogAsync(LogMessage log)
    {
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(level, log.Exception, "[{Source}] {Message}", log.Source, log.Message);
        return Task.CompletedTask;
    }
}
