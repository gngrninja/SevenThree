using Discord;
using Discord.Interactions;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SevenThree.Modules.BandConditions
{
    public class BandConditionSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IServiceProvider _services;

        public BandConditionSlashCommands(IServiceProvider services)
        {
            _services = services;
        }

        [SlashCommand("conditions", "Get current ham radio band conditions")]
        public async Task GetConditions()
        {
            await DeferAsync(ephemeral: true);

            var conds = _services.GetRequiredService<BandConditions>();
            var result = await conds.GetConditionsHamQsl();

            if (File.Exists(result))
            {
                var fileName = Path.GetFileName(result);
                var embed = new EmbedBuilder();
                embed.Title = "Current ham radio conditions";
                embed.Description = "Data gathered from [hamqsl](https://www.hamqsl.com)";
                embed.Footer = new EmbedFooterBuilder
                {
                    Text = "Data gathered from https://www.hamqsl.com"
                };
                embed.ImageUrl = $"attachment://{fileName}";

                await FollowupWithFileAsync(result, fileName, embed: embed.Build(), ephemeral: true);
            }
            else
            {
                await FollowupAsync("There was an error getting the conditions, please try again later.", ephemeral: true);
            }
        }
    }
}
