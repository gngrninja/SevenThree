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
using Microsoft.Extensions.Logging;
using SevenThree.Database;

namespace SevenThree.Modules
{
    public class Help : ModuleBase
    {

        private readonly SevenThreeContext _db;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public Help(IServiceProvider services)
        {
            _db = services.GetRequiredService<SevenThreeContext>();
            _logger = services.GetRequiredService<ILogger<Help>>();  
            _config = services.GetRequiredService<IConfiguration>();  
        }

        [Command("help", RunMode = RunMode.Async)]
        public async Task GetHelp()
        {                                       
            var embed        = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            var prefix       = GetServerPrefix();
                     
            var helpTxt = await System.IO.File.ReadAllLinesAsync("help.txt");            
            if (helpTxt != null)
            {
                foreach (var line in helpTxt)
                {
                    sb.AppendLine(line).Replace('!',prefix);
                }            
                embed.Description = sb.ToString();
                embed.WithColor(new Color(0, 255, 0));
                embed.Title = $"SevenThree Help!";            
                embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";
                embed.WithAuthor(
                new EmbedAuthorBuilder{
                    Name    = $"Help requested by: [{Context.User.Username}]",
                    IconUrl = Context.User.GetAvatarUrl()                
                });

                embed.WithFooter(
                    new EmbedFooterBuilder{
                        Text    = "SevenThree, your local ham radio Discord bot.",
                        IconUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true"
                });   
                await ReplyAsync("", false, embed.Build());
            }        
        }      

        public char GetServerPrefix()
        {
            char prefix = Char.Parse(_config["Prefix"]);
            PrefixList serverPrefix = null;
            if (!(Context.Channel is IDMChannel))
            {
                serverPrefix = GetPrefix((long)Context.Guild.Id); 
            }            
            if (serverPrefix != null)
            {
                prefix = serverPrefix.Prefix;
            }
            return prefix;
        }

        private PrefixList GetPrefix(long serverId)
        {
            PrefixList prefix = null;
           
            prefix = _db.PrefixList.Where(p => p.ServerId == serverId).FirstOrDefault();
            
            return prefix;
        } 
    }
}