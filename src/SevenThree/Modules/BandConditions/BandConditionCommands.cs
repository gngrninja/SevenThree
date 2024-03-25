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
using System.IO;

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
                await Context.Channel.SendFileAsync(result, null, false, embed.Build());        
            }
            else
            {
                await ReplyAsync("There was an error getting the conditions, please try again later.");
            }            
        }
    }
}