using Discord;
using Discord.Interactions;
using System.Threading.Tasks;

namespace SevenThree.Modules
{
    public class HelpSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        public HelpSlashCommands() { }

        [SlashCommand("help", "Get help with SevenThree bot commands")]
        public async Task GetHelp()
        {
            var embed = new EmbedBuilder();
            embed.Title = "SevenThree Help";
            embed.Description = "SevenThree is a Discord bot for ham radio enthusiasts. Here are the available commands:";
            embed.WithColor(new Color(0, 255, 0));
            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";

            embed.AddField("Practice Exams",
                "`/quiz tech` - Start a Technician practice exam\n" +
                "`/quiz general` - Start a General practice exam\n" +
                "`/quiz extra` - Start an Extra practice exam\n" +
                "`/quiz start` - Start an exam with custom options\n" +
                "`/quiz stop` - Stop the current exam",
                false);

            embed.AddField("QRZ Lookup",
                "`/qrz lookup <callsign>` - Look up a callsign on QRZ.com\n" +
                "`/qrz dxcc <entity>` - Look up DXCC information",
                false);

            embed.AddField("Callsign",
                "`/callsign get` - Get your associated callsign\n" +
                "`/callsign set <callsign>` - Set your callsign",
                false);

            embed.AddField("Other",
                "`/conditions` - Get current band conditions\n" +
                "`/ping` - Check bot latency",
                false);

            embed.AddField("Server Settings (Moderators)",
                "`/quizsettings setchannel <license>` - Set quiz channel for a license type\n" +
                "`/quizsettings unsetchannel` - Remove channel from quiz settings\n" +
                "`/quizsettings clearafter` - Toggle clearing messages after test",
                false);

            embed.WithAuthor(new EmbedAuthorBuilder
            {
                Name = $"Help requested by: [{Context.User.Username}]",
                IconUrl = Context.User.GetAvatarUrl()
            });

            embed.WithFooter(new EmbedFooterBuilder
            {
                Text = "SevenThree, your local ham radio Discord bot. 73!",
                IconUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true"
            });

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
