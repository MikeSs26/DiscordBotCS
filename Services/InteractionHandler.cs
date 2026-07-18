using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBotCS.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBotCS.Services;

///<summary>
///Wires the gateway to the <see cref="InteractionService"/>: discovers command modules,
///registers them with Discord, and routes incoming interactions to the right handler.
///</summary>
public sealed class InteractionHandler : IGatewayEventHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly DiscordSettings _settings;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        IOptions<DiscordSettings> settings,
        ILogger<InteractionHandler> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _client.Ready += RegisterCommandsAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _interactions.InteractionExecuted += OnInteractionExecutedAsync;

        //auto-discover every InteractionModuleBase in this assembly.
        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
    }

    private async Task RegisterCommandsAsync()
    {
        try
        {
            if (_settings.TestGuildId is ulong guildId)
            {
                await _interactions.RegisterCommandsToGuildAsync(guildId);
                _logger.LogInformation("Slash commands registered to test guild {GuildId}.", guildId);
            }
            else
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Slash commands registered globally.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands.");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(context, _services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error dispatching interaction {Id}.", interaction.Id);

            //if we already acknowledged, delete the empty deferral to avoid a dangling "thinking" state.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
        }
    }

    private Task OnInteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess)
            return Task.CompletedTask;

        _logger.LogWarning(
            "Command '{Command}' failed: {Error} - {Reason}",
            command.Name, result.Error, result.ErrorReason);

        var message = result.Error switch
        {
            InteractionCommandError.UnmetPrecondition => "No tienes permiso para usar este comando.",
            InteractionCommandError.UnknownCommand => "Ese comando ya no existe.",
            InteractionCommandError.BadArgs => "Los argumentos que enviaste no son válidos.",
            _ => "Ha ocurrido un error al ejecutar el comando. Inténtalo de nuevo más tarde."
        };

        return context.Interaction.HasResponded
            ? context.Interaction.FollowupAsync(message, ephemeral: true)
            : context.Interaction.RespondAsync(message, ephemeral: true);
    }
}
