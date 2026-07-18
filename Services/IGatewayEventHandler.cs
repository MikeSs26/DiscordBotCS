namespace DiscordBotCS.Services;

///<summary>
///A component that subscribes to Discord gateway events. Implementations are
///discovered via DI and initialized once when the bot starts.
///</summary>
public interface IGatewayEventHandler
{
    Task InitializeAsync();
}
