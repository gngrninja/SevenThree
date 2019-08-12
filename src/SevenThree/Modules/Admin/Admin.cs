using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Database;

namespace SevenThree.Modules
{
    public class Admin : ModuleBase
    {
        private readonly SevenThreeContext _db;

        public Admin(IServiceProvider services)
        {
            _db =  services.GetRequiredService<SevenThreeContext>();
        }
        
        [Command("prefix",RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
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