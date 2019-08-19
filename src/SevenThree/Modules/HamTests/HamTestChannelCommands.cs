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
using SevenThree.Models;
using SevenThree.Services;
using System.IO;

namespace SevenThree.Modules
{
    public class HamTestChannelCommands : ModuleBase
    {

        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;        
        private readonly HamTestService _hamTestService;

        public HamTestChannelCommands(IServiceProvider services)
        {
            _logger          = services.GetRequiredService<ILogger<HamTestChannelCommands>>();
            _db              = services.GetRequiredService<SevenThreeContext>();
            _hamTestService  = services.GetRequiredService<HamTestService>();
        }

        [Command("clearafter")]
        [RequireUserPermission(GuildPermission.KickMembers)]        
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task ClearAfterTaken()
        {
            var sb = new StringBuilder();

            var discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id).FirstOrDefaultAsync();

            if (discordSettings != null && discordSettings.ExtraChannelId != null || discordSettings.GeneralChannelId != null || discordSettings.TechChannelId != null)
            {    
                if (discordSettings.ClearAfterTaken == true)
                {
                    discordSettings.ClearAfterTaken = false;
                }            
                else
                {
                    discordSettings.ClearAfterTaken = true;      
                }                              
                sb.AppendLine($"Test channel contents will be cleared upon test completion!");                
            }        
            else
            {
                sb.AppendLine("Please set a channel to a specific test before using this command!");
            }
            await _db.SaveChangesAsync();             
            await ReplyAsync(sb.ToString());            
        }

        [Command("stech")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task SetTechChannel()        
        {
            var discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (discordSettings == null)
            {
                discordSettings                = new QuizSettings();
                discordSettings.DiscordGuildId = Context.Guild.Id;
                discordSettings.TechChannelId  = Context.Channel.Id;
                await _db.QuizSettings.AddAsync(discordSettings);
            }
            else
            {
                discordSettings.TechChannelId  = Context.Channel.Id;
            }
            await _db.SaveChangesAsync();
            await ReplyAsync($"Tech test channel set to [{Context.Channel.Name}]!");
        }   

        [Command("sgeneral")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task SetGeneralChannel()        
        {
            var discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (discordSettings == null)
            {
                discordSettings                = new QuizSettings();
                discordSettings.DiscordGuildId = Context.Guild.Id;
                discordSettings.GeneralChannelId  = Context.Channel.Id;
                await _db.QuizSettings.AddAsync(discordSettings);
            }
            else
            {
                discordSettings.GeneralChannelId  = Context.Channel.Id;
            }
            await _db.SaveChangesAsync();
            await ReplyAsync($"General test channel set to [{Context.Channel.Name}]!");        
        }         

        [Command("sextra")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task SetExtraChannel()        
        {
            var discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (discordSettings == null)
            {
                discordSettings                = new QuizSettings();
                discordSettings.DiscordGuildId = Context.Guild.Id;
                discordSettings.ExtraChannelId = Context.Channel.Id;
                await _db.QuizSettings.AddAsync(discordSettings);
            }
            else
            {
                discordSettings.ExtraChannelId = Context.Channel.Id;
            }
            await _db.SaveChangesAsync();
            await ReplyAsync($"Extra test channel set to [{Context.Channel.Name}]!");
        }     

        [Command("unsetc")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task UnsetChannel()        
        {
            var discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id && s.ExtraChannelId == Context.Channel.Id).FirstOrDefaultAsync();
            if (discordSettings != null)
            {                
                discordSettings.ExtraChannelId = null;
                
                await ReplyAsync($"Channel unset!");
            }
            
            discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id && s.GeneralChannelId == Context.Channel.Id).FirstOrDefaultAsync();
            if (discordSettings != null)
            {                
                discordSettings.GeneralChannelId = null;
                
                await ReplyAsync($"Channel unset!");
            }

            discordSettings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Context.Guild.Id && s.TechChannelId == Context.Channel.Id).FirstOrDefaultAsync();
            if (discordSettings != null)
            {                
                discordSettings.TechChannelId = null;
                
                await ReplyAsync($"Channel unset!");
            }            
            await _db.SaveChangesAsync();
        }                                     
    }
}