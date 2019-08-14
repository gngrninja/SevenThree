using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SevenThree.Modules.BandConditions
{
    public class BandConditionCommands : ModuleBase
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;

        public BandConditionCommands(IServiceProvider services)
        {
            _services = services;
            _config = _services.GetRequiredService<IConfiguration>();
        }

        [Command("conditions", RunMode = RunMode.Async)]
        [Alias("conds")]
        public async Task GetConditions()
        {
            var embed = new EmbedBuilder();
            embed.Title = "Current ham radio conditions";
            /* 
            embed.Footer = new EmbedFooterBuilder
            {
                Text = "Data gathered from [HamQsl](https://www.hamqsl.com)"
            };
            */
            embed.Description = "Data gathered from [HamQsl](https://www.hamqsl.com)";
            embed.ImageUrl = _config["HamQslUrl"];
            await ReplyAsync(null, false, embed.Build());        
        }
    }
}