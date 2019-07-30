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
using Microsoft.EntityFrameworkCore;
using SevenThree.Database;

namespace SevenThree.Modules
{
    // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
    public class CallAssociation : ModuleBase
    {
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;

        public CallAssociation(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _db = services.GetRequiredService<SevenThreeContext>();
        }

        [Command("Get-Call", RunMode = RunMode.Async)]
        public async Task GetCall([Remainder] string args = null)
        {
            StringBuilder sb = new StringBuilder();
            var embed = new EmbedBuilder();
            CallSignAssociation callInfo = null;
        
            using (var db = _db)
            {
                callInfo = db.CallSignAssociation.Where(d => d.DiscordUserId == (long)Context.User.Id).FirstOrDefault();
            }
            if (callInfo != null)
            {
                sb.AppendLine($"{Context.User.Mention} your call sign is:");
                sb.AppendLine($"{callInfo.CallSign}");
                
            }
            else
            {
                sb.AppendLine($"No data found!");
            }
            embed.Title = $"Call sign information for {Context.User.Username}!";
            embed.Description = sb.ToString();
            await ReplyAsync(null,false,embed.Build());
        }

        [Command("Set-Call", RunMode = RunMode.Async)]
        public async Task SetCall([Remainder] string args = null)
        {
            StringBuilder sb = new StringBuilder();
            var embed = new EmbedBuilder();
            CallSignAssociation callInfo = null;
        

            callInfo = _db.CallSignAssociation.Where(d => d.DiscordUserId == (long)Context.User.Id).FirstOrDefault();
            

            if (callInfo != null)
            {
                
                var record = _db.CallSignAssociation.Where(r => r.DiscordUserId == (long)Context.User.Id).FirstOrDefault();
                record.CallSign = args.ToUpper();
                await _db.SaveChangesAsync();
                
                
            }
            else
            {
                _db.CallSignAssociation.Add(new CallSignAssociation
                {
                    DiscordUserId = (long)Context.User.Id,
                    DiscordUserName = Context.User.Username,
                    CallSign = args.ToUpper()
                });
                await _db.SaveChangesAsync();
            }

            sb.AppendLine($"{Context.User.Mention} your call sign is now set to:");
            sb.AppendLine($"{args.ToUpper()}");
            embed.Title = $"Call sign information for {Context.User.Username}!";
            embed.Description = sb.ToString();
            await ReplyAsync(null,false,embed.Build());
        }        
    }
}