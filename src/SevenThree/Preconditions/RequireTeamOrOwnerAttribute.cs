using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace SevenThree.Preconditions
{
    public class RequireTeamOrOwnerAttribute : PreconditionAttribute
    {
        private static IApplication _cachedApplication;

        public override async Task<PreconditionResult> CheckRequirementsAsync(
            IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            // Cache the application info — team membership doesn't change mid-session.
            // GetCurrentBotInfoAsync uses the applications/@me endpoint which reliably
            // includes team data, unlike GetApplicationInfoAsync's legacy endpoint.
            var application = _cachedApplication;
            if (application == null)
            {
                application = context.Client is DiscordSocketClient socketClient
                    ? await socketClient.Rest.GetCurrentBotInfoAsync().ConfigureAwait(false)
                    : await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                _cachedApplication = application;
            }

            if (application.Team != null)
            {
                if (application.Team.TeamMembers.Any(m => m.User.Id == context.User.Id))
                    return PreconditionResult.FromSuccess();

                return PreconditionResult.FromError(ErrorMessage ?? "Command can only be run by a team member of the bot's application.");
            }

            if (application.Owner.Id == context.User.Id)
                return PreconditionResult.FromSuccess();

            return PreconditionResult.FromError(ErrorMessage ?? "Command can only be run by the owner of the bot.");
        }
    }
}
