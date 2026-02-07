using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace SevenThree.Modules.Help
{
    public class HelpSelectMenuHandler
    {
        private readonly InteractionService _interactions;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<HelpSelectMenuHandler> _logger;

        public const string SELECT_MENU_ID = "help_category_select";

        public HelpSelectMenuHandler(
            InteractionService interactions,
            DiscordSocketClient client,
            ILogger<HelpSelectMenuHandler> logger)
        {
            _interactions = interactions;
            _client = client;
            _logger = logger;
        }

        public async Task HandleSelectionAsync(SocketMessageComponent component)
        {
            try
            {
                var categoryId = component.Data.Values.First();
                var isOwner = await IsOwnerAsync(component.User.Id);
                var commands = HelpSlashCommands.GetAvailableCommands(_interactions, isOwner);

                if (categoryId == "welcome")
                {
                    var welcomeEmbed = HelpSlashCommands.BuildWelcomeEmbed();
                    var menu = HelpSlashCommands.BuildCategorySelectMenu(commands, isOwner);

                    await component.UpdateAsync(m =>
                    {
                        m.Embed = welcomeEmbed.Build();
                        m.Components = menu;
                    });
                    return;
                }

                var embed = HelpSlashCommands.BuildCategoryEmbed(categoryId, commands);
                var selectMenu = HelpSlashCommands.BuildCategorySelectMenu(commands, isOwner);

                await component.UpdateAsync(m =>
                {
                    m.Embed = embed.Build();
                    m.Components = selectMenu;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling help category selection");
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

        private async Task<bool> IsOwnerAsync(ulong userId)
        {
            try
            {
                var appInfo = await _client.GetApplicationInfoAsync();
                if (appInfo.Owner?.Id == userId)
                    return true;
                if (appInfo.Team != null)
                    return appInfo.Team.TeamMembers.Any(m => m.User.Id == userId);
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
