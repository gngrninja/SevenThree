using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using SevenThree.Database;

namespace SevenThree.Modules
{
    [Group("callsign", "Manage your associated ham radio callsign")]
    public class CallAssociationSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        public CallAssociationSlashCommands(IDbContextFactory<SevenThreeContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [SlashCommand("get", "Get your associated callsign")]
        public async Task GetCall()
        {
            using var db = _contextFactory.CreateDbContext();
            var callInfo = db.CallSignAssociation
                .Where(d => d.DiscordUserId == (long)Context.User.Id)
                .FirstOrDefault();

            var embed = new EmbedBuilder();
            embed.Title = $"Callsign information for {Context.User.Username}";

            if (callInfo != null)
            {
                embed.Description = $"{Context.User.Mention}, your callsign is: **{callInfo.CallSign}**";
                embed.WithColor(new Color(0, 255, 0));
            }
            else
            {
                embed.Description = "No callsign found! Use `/callsign set` to associate your callsign.";
                embed.WithColor(new Color(255, 165, 0));
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        [SlashCommand("set", "Set your associated callsign")]
        public async Task SetCall(
            [Summary("callsign", "Your ham radio callsign")] string callsign)
        {
            using var db = _contextFactory.CreateDbContext();
            var callInfo = db.CallSignAssociation
                .Where(d => d.DiscordUserId == (long)Context.User.Id)
                .FirstOrDefault();

            var upperCallsign = callsign.ToUpper();

            if (callInfo != null)
            {
                callInfo.CallSign = upperCallsign;
            }
            else
            {
                db.CallSignAssociation.Add(new CallSignAssociation
                {
                    DiscordUserId = (long)Context.User.Id,
                    DiscordUserName = Context.User.Username,
                    CallSign = upperCallsign
                });
            }

            await db.SaveChangesAsync();

            var embed = new EmbedBuilder();
            embed.Title = $"Callsign information for {Context.User.Username}";
            embed.Description = $"{Context.User.Mention}, your callsign is now set to: **{upperCallsign}**";
            embed.WithColor(new Color(0, 255, 0));

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
