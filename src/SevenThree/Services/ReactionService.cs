using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace SevenThree.Services
{
    /// <summary>
    /// Handles reaction-based role assignment for a specific guild.
    /// TODO: Make guild/message IDs configurable instead of hardcoded.
    /// </summary>
    public class ReactionService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<ReactionService> _logger;

        // Hardcoded IDs for specific guild feature
        // TODO: Move to configuration
        private const ulong TARGET_GUILD_ID = 611634254438465537;
        private const ulong TARGET_MESSAGE_ID = 612768518152388648;
        private const string HAM_ROLE = "ham";
        private const string HAM2BE_ROLE = "ham2be";

        public ReactionService(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _logger = services.GetRequiredService<ILogger<ReactionService>>();

            _client.ReactionAdded += HandleReactionAddedAsync;
            _client.ReactionRemoved += HandleReactionRemovedAsync;
        }

        private async Task HandleReactionRemovedAsync(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            try
            {
                if (message.Id != TARGET_MESSAGE_ID)
                    return;

                if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                    return;

                var guild = _client.GetGuild(TARGET_GUILD_ID);
                if (guild == null)
                    return;

                var user = reaction.User.Value as IGuildUser;
                if (user == null)
                    return;

                var roleName = reaction.Emote.Name switch
                {
                    HAM_ROLE => HAM_ROLE,
                    HAM2BE_ROLE => HAM2BE_ROLE,
                    _ => null
                };

                if (roleName == null)
                    return;

                var role = guild.Roles.FirstOrDefault(r => r.Name == roleName);
                if (role != null)
                {
                    await user.RemoveRoleAsync(role);
                    _logger.LogDebug("Removed role {Role} from {User}", roleName, user.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling reaction removal");
            }
        }

        private async Task HandleReactionAddedAsync(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            try
            {
                if (message.Id != TARGET_MESSAGE_ID)
                    return;

                if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                    return;

                var guild = _client.GetGuild(TARGET_GUILD_ID);
                if (guild == null)
                    return;

                var user = reaction.User.Value as IGuildUser;
                if (user == null)
                    return;

                var emoteName = reaction.Emote.Name;
                if (emoteName != HAM_ROLE && emoteName != HAM2BE_ROLE)
                    return;

                var hamRole = guild.Roles.FirstOrDefault(r => r.Name == HAM_ROLE);
                var ham2beRole = guild.Roles.FirstOrDefault(r => r.Name == HAM2BE_ROLE);

                if (emoteName == HAM_ROLE && hamRole != null)
                {
                    await user.AddRoleAsync(hamRole);
                    if (ham2beRole != null)
                        await user.RemoveRoleAsync(ham2beRole);
                    _logger.LogDebug("Added role {Role} to {User}", HAM_ROLE, user.Username);
                }
                else if (emoteName == HAM2BE_ROLE && ham2beRole != null)
                {
                    await user.AddRoleAsync(ham2beRole);
                    if (hamRole != null)
                        await user.RemoveRoleAsync(hamRole);
                    _logger.LogDebug("Added role {Role} to {User}", HAM2BE_ROLE, user.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling reaction add");
            }
        }
    }
}