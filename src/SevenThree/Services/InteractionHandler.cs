using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Modules.Help;
using SevenThree.Modules.PskReporter;
using SevenThree.Modules.Study;
using SevenThree.Services;

namespace SevenThree.Services
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;
        private readonly ILogger<InteractionHandler> _logger;
        private readonly IConfiguration _config;
        private readonly QuizButtonHandler _quizButtonHandler;
        private readonly PskButtonHandler _pskButtonHandler;
        private readonly StudyButtonHandler _studyButtonHandler;
        private readonly HelpSelectMenuHandler _helpSelectMenuHandler;

        public InteractionHandler(
            DiscordSocketClient client,
            InteractionService interactions,
            IServiceProvider services,
            ILogger<InteractionHandler> logger,
            IConfiguration config,
            QuizButtonHandler quizButtonHandler,
            PskButtonHandler pskButtonHandler,
            StudyButtonHandler studyButtonHandler,
            HelpSelectMenuHandler helpSelectMenuHandler)
        {
            _client = client;
            _interactions = interactions;
            _services = services;
            _logger = logger;
            _config = config;
            _quizButtonHandler = quizButtonHandler;
            _pskButtonHandler = pskButtonHandler;
            _studyButtonHandler = studyButtonHandler;
            _helpSelectMenuHandler = helpSelectMenuHandler;
        }

        public async Task InitializeAsync()
        {
            // Add modules from this assembly
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Handle interaction execution
            _client.InteractionCreated += HandleInteractionAsync;

            // Handle button clicks for quiz answers
            _client.ButtonExecuted += HandleButtonExecutedAsync;

            // Handle select menu interactions
            _client.SelectMenuExecuted += HandleSelectMenuExecutedAsync;

            // Handle post-execution for logging
            _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;
        }

        public async Task RegisterCommandsAsync()
        {
            try
            {
                // Check for dev guild ID - if set, register to that guild (instant)
                // Otherwise register globally (takes up to 1 hour to propagate)
                var devGuildIdStr = _config["DevGuildId"] ?? Environment.GetEnvironmentVariable("SEVENTHREE_DevGuildId");

                if (!string.IsNullOrEmpty(devGuildIdStr) && ulong.TryParse(devGuildIdStr, out var devGuildId))
                {
                    await _interactions.RegisterCommandsToGuildAsync(devGuildId);
                    _logger.LogInformation("Slash commands registered to dev guild {GuildId} (instant)", devGuildId);
                }
                else
                {
                    await _interactions.RegisterCommandsGloballyAsync();
                    _logger.LogInformation("Slash commands registered globally (may take up to 1 hour to propagate)");
                }
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

                // Route study flashcard button clicks to StudyButtonHandler
                if (component.Data.CustomId.StartsWith($"{StudyConstants.BUTTON_PREFIX}:"))
                {
                    await _studyButtonHandler.HandleStudyButtonAsync(component);
                    return;
                }

                // Route study retry button clicks to StudyButtonHandler
                if (component.Data.CustomId.StartsWith($"{StudyConstants.RETRY_BUTTON_PREFIX}:"))
                {
                    await _studyButtonHandler.HandleRetryButtonAsync(component);
                    return;
                }

                // Unknown button - acknowledge but do nothing
                if (!component.HasResponded)
                {
                    await component.DeferAsync();
                }
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == Discord.DiscordErrorCode.UnknownInteraction)
            {
                // Interaction expired - nothing we can do
                _logger.LogWarning("Button interaction expired before we could respond");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handling button interaction");
                try
                {
                    if (!component.HasResponded)
                    {
                        await component.RespondAsync("An error occurred processing your response.", ephemeral: true);
                    }
                }
                catch
                {
                    // Failed to respond - interaction likely expired, nothing more we can do
                }
            }
        }

        private async Task HandleSelectMenuExecutedAsync(SocketMessageComponent component)
        {
            try
            {
                if (component.Data.CustomId == HelpSelectMenuHandler.SELECT_MENU_ID)
                {
                    await _helpSelectMenuHandler.HandleSelectionAsync(component);
                    return;
                }

                // Unknown select menu - acknowledge but do nothing
                if (!component.HasResponded)
                {
                    await component.DeferAsync();
                }
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == Discord.DiscordErrorCode.UnknownInteraction)
            {
                _logger.LogWarning("Select menu interaction expired before we could respond");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handling select menu interaction");
                try
                {
                    if (!component.HasResponded)
                    {
                        await component.RespondAsync("An error occurred.", ephemeral: true);
                    }
                }
                catch { }
            }
        }
    }
}
