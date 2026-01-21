using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Modules.PskReporter;
using SevenThree.Services;

namespace SevenThree.Services
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;
        private readonly ILogger<InteractionHandler> _logger;
        private readonly QuizButtonHandler _quizButtonHandler;
        private readonly PskButtonHandler _pskButtonHandler;

        public InteractionHandler(
            DiscordSocketClient client,
            InteractionService interactions,
            IServiceProvider services,
            ILogger<InteractionHandler> logger,
            QuizButtonHandler quizButtonHandler,
            PskButtonHandler pskButtonHandler)
        {
            _client = client;
            _interactions = interactions;
            _services = services;
            _logger = logger;
            _quizButtonHandler = quizButtonHandler;
            _pskButtonHandler = pskButtonHandler;
        }

        public async Task InitializeAsync()
        {
            // Add modules from this assembly
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Handle interaction execution
            _client.InteractionCreated += HandleInteractionAsync;

            // Handle button clicks for quiz answers
            _client.ButtonExecuted += HandleButtonExecutedAsync;

            // Handle post-execution for logging
            _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;
        }

        public async Task RegisterCommandsAsync()
        {
            // Register commands globally (takes up to 1 hour to propagate)
            // For development, you can register to a specific guild for instant updates
            try
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Slash commands registered globally");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register slash commands");
            }
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            try
            {
                // Skip component interactions (buttons, select menus) - they're handled by dedicated handlers
                if (interaction.Type == InteractionType.MessageComponent)
                {
                    return;
                }

                var context = new SocketInteractionContext(_client, interaction);
                var result = await _interactions.ExecuteCommandAsync(context, _services);

                if (!result.IsSuccess)
                {
                    _logger.LogError("Interaction failed: {Error} - {ErrorReason}", result.Error, result.ErrorReason);

                    // Respond with error if not already responded
                    if (!interaction.HasResponded)
                    {
                        await interaction.RespondAsync($"Error: {result.ErrorReason}", ephemeral: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handling interaction");

                if (interaction.Type == InteractionType.ApplicationCommand)
                {
                    if (!interaction.HasResponded)
                    {
                        await interaction.RespondAsync("An error occurred while processing the command.", ephemeral: true);
                    }
                }
            }
        }

        private Task SlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Slash command {CommandName} failed: {Error} - {ErrorReason}",
                    command.Name, result.Error, result.ErrorReason);
            }
            else
            {
                _logger.LogInformation("Slash command {CommandName} executed by {Username}",
                    command.Name, context.User.Username);
            }

            return Task.CompletedTask;
        }

        private async Task HandleButtonExecutedAsync(SocketMessageComponent component)
        {
            try
            {
                // Route quiz answer button clicks to QuizButtonHandler
                if (component.Data.CustomId.StartsWith($"{QuizConstants.BUTTON_PREFIX}:"))
                {
                    await _quizButtonHandler.HandleQuizButtonAsync(component);
                    return;
                }

                // Route quiz stop button clicks to QuizButtonHandler
                if (component.Data.CustomId.StartsWith($"{QuizConstants.STOP_BUTTON_PREFIX}:"))
                {
                    await _quizButtonHandler.HandleStopButtonAsync(component);
                    return;
                }

                // Route PSK pagination button clicks to PskButtonHandler
                if (component.Data.CustomId.StartsWith($"{PskReporterService.BUTTON_PREFIX}:"))
                {
                    await _pskButtonHandler.HandlePskButtonAsync(component);
                    return;
                }

                // Unknown button - acknowledge but do nothing
                if (!component.HasResponded)
                {
                    await component.DeferAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handling button interaction");
                if (!component.HasResponded)
                {
                    await component.RespondAsync("An error occurred processing your response.", ephemeral: true);
                }
            }
        }
    }
}
