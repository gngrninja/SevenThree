using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using System.Linq;

namespace SevenThree.Services
{
    public class ReactionService
    {
        private readonly DiscordSocketClient _client;

        public ReactionService(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();    
            _client.ReactionAdded += ReactionAdded;
            _client.ReactionRemoved += ReactionRemoved;
        }

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var guild  = _client.Guilds.Where(g => g.Id == 249709417963061248).FirstOrDefault() as IGuild;            
            var emotes = guild.Emotes;
            var roles  = guild.Roles; 
            if (arg1.Id == 282548374312452096)
            {
                if (arg3.Emote.Name == "ham")
                {                                       
                    if (!arg3.User.Value.IsBot)
                    {
                        var user = arg3.User.Value as IGuildUser;
                        await user.RemoveRoleAsync(roles.Where(r => r.Name == "ham").FirstOrDefault());
                    }
                }
                else if (arg3.Emote.Name == "ham2be")
                {                    
                    if (!arg3.User.Value.IsBot)
                    {
                        var user = arg3.User.Value as IGuildUser;
                        await user.RemoveRoleAsync(roles.Where(r => r.Name == "ham2be").FirstOrDefault());
                    }                    
                }
            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var guild  = _client.Guilds.Where(g => g.Id == 611634254438465537).FirstOrDefault() as IGuild;            
            var emotes = guild.Emotes;
            var roles  = guild.Roles; 
            if (arg1.Id == 612768518152388648)
            {
                if (arg3.Emote.Name == "ham")                
                {                    
                    System.Console.WriteLine("ham");
                    if (!arg3.User.Value.IsBot)
                    {
                        var user = arg3.User.Value as IGuildUser;
                        await user.AddRoleAsync(roles.Where(r => r.Name == "ham").FirstOrDefault());
                        await user.RemoveRoleAsync(roles.Where(r => r.Name == "ham2be").FirstOrDefault());
                    }
                }
                else if (arg3.Emote.Name == "ham2be")
                {                    
                    System.Console.WriteLine("ham2be");
                    if (!arg3.User.Value.IsBot)
                    {
                        var user = arg3.User.Value as IGuildUser;
                        await user.AddRoleAsync(roles.Where(r => r.Name == "ham2be").FirstOrDefault());
                        await user.RemoveRoleAsync(roles.Where(r => r.Name == "ham").FirstOrDefault());
                    }                    
                }
            }
        }
    }
}