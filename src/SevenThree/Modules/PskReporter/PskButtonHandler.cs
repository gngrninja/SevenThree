using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SevenThree.Services;

namespace SevenThree.Modules.PskReporter
{
    /// <summary>
    /// Handles button interactions for PSKReporter pagination
    /// </summary>
    public class PskButtonHandler
    {
        private readonly PskReporterService _pskService;
        private readonly ILogger<PskButtonHandler> _logger;

        public PskButtonHandler(
            PskReporterService pskService,
            ILogger<PskButtonHandler> logger)
        {
            _pskService = pskService;
            _logger = logger;
        }

        /// <summary>
        /// Handle PSK pagination button click
        /// </summary>
        public async Task HandlePskButtonAsync(SocketMessageComponent component)
        {
            var parts = component.Data.CustomId.Split(':');
            if (parts.Length < 3)
            {
                await component.RespondAsync("Invalid button.", ephemeral: true);
                return;
            }

            var action = parts[1]; // "prev", "next", "page"
            var sessionId = parts[2];

            // "page" button is disabled, ignore
            if (action == "page")
            {
                await component.DeferAsync();
                return;
            }

            // Get current page from button customId
            if (parts.Length < 4 || !int.TryParse(parts[3], out var currentPage))
            {
                await component.RespondAsync("Invalid button state.", ephemeral: true);
                return;
            }

            // Get cached data
            var cached = _pskService.GetCachedSpots(sessionId);
            if (cached == null)
            {
                await component.RespondAsync(
                    "This result has expired. Please run the command again.",
                    ephemeral: true);
                return;
            }

            // Calculate new page
            var newPage = action switch
            {
                "prev" => Math.Max(0, currentPage - 1),
                "next" => Math.Min(cached.TotalPages - 1, currentPage + 1),
                _ => currentPage
            };

            // Build updated embed and buttons
            var embed = PskReporterSlashCommands.BuildPaginatedEmbed(cached, newPage);
            var components = PskReporterSlashCommands.BuildNavigationButtons(sessionId, newPage, cached.TotalPages);

            // Update the message
            await component.UpdateAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = components;
            });

            _logger.LogDebug("PSK pagination: {Action} to page {Page} for session {SessionId}",
                action, newPage + 1, sessionId);
        }
    }
}
