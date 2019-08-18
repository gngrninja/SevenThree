using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Database;
using Discord;
using Discord.WebSocket;

namespace SevenThree.Modules
{
    public class Admin : ModuleBase
    {
        private readonly SevenThreeContext _db;
        private readonly DiscordSocketClient _client;

        public Admin(IServiceProvider services)
        {
            _db     = services.GetRequiredService<SevenThreeContext>();
            _client = services.GetRequiredService<DiscordSocketClient>();
        }
        
        [Command("playing")]
        [RequireOwner]
        public async Task ChangePlaying([Remainder] string args)
        {
            await _client.SetGameAsync(args);
        }

        [Command("prefix",RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task ChangePrefix(char prefix)
        {                        
            var currentPrefix = _db.PrefixList.Where(p => p.ServerId == (long)Context.Guild.Id).FirstOrDefault();
            if (currentPrefix != null)
            {
                currentPrefix.Prefix = prefix;
                currentPrefix.SetById = (long)Context.User.Id;
            }
            else
            {
                _db.PrefixList.Add(new PrefixList
                {
                    ServerId = (long)Context.Guild.Id,
                    ServerName = Context.Guild.Name,
                    Prefix = prefix,
                    SetById = (long)Context.User.Id
                });
            }
            await _db.SaveChangesAsync();
            
            await ReplyAsync($"Prefix for [**{Context.Guild.Name}**] changed to [**{prefix}**]");
        }
    }
}