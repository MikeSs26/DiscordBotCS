using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBotCS.Services;

/// <summary>
/// Posts a heartbeat to the portfolio every minute so it can show this bot as
/// online. Push-only on purpose: the droplet exposes nothing, and when the
/// process dies the beats stop and the portfolio marks it offline by itself
/// (the server side expires the last beat; there is no state to clean up).
/// </summary>
public sealed class HeartbeatService(IConfiguration config, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    private static readonly Uri Endpoint = new("https://miguellucia.com/api/heartbeat");
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //read at start, not at startup registration, so a missing value only
        //disables this service instead of failing the whole host.
        var secret = config["HEARTBEAT_SECRET"];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogInformation("HEARTBEAT_SECRET is not set; portfolio heartbeat disabled.");
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await http.PostAsync(Endpoint, content: null, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                //a briefly unreachable network is not fatal: the next tick retries,
                //and the portfolio simply shows offline until a beat lands again.
                logger.LogDebug(ex, "Heartbeat POST failed; retrying on the next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
