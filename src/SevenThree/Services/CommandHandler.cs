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
    public class CommandHandler
    {
        // setup fields to be set later in the constructor
        private readonly IConfiguration _config;
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;

        public CommandHandler(IServiceProvider services)
        {
            // juice up the fields with these services
            // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
            _config = services.GetRequiredService<IConfiguration>();
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _logger = services.GetService<ILogger<CommandHandler>>();
            _db = services.GetRequiredService<SevenThreeContext>();
            _services = services;
            
            // take action when we execute a command
            _commands.CommandExecuted += CommandExecutedAsync;

            // take action when we receive a message (so we can process it, and see if it is a valid command)
            _client.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync()
        {
            // register modules that are public and inherit ModuleBase<T>.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        // this class is where the magic starts, and takes actions upon receiving messages
        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // ensures we don't process system/other bot messages
            if (!(rawMessage is SocketUserMessage message)) 
            {
                return;
            }
            
            if (message.Source != MessageSource.User) 
            {
                return;
            }

            // sets the argument position away from the prefix we set
            var argPos = 0;

            // get prefix from the configuration file
            char prefix = Char.Parse(_config["Prefix"]);
           
            var context = new SocketCommandContext(_client, message);
            var serverPrefix = GetPrefix((long)context.Guild.Id); 

            if (serverPrefix != null)
            {
                prefix = serverPrefix.Prefix;
            }

            // determine if the message has a valid prefix, and adjust argPos based on prefix
            if (!(message.HasMentionPrefix(_client.CurrentUser, ref argPos) || message.HasCharPrefix(prefix, ref argPos))) 
            {
                return;
            }
            // execute command if one is found that matches
            await _commands.ExecuteAsync(context, argPos, _services); 
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {            
            if (result.IsSuccess) 
            {
                string logText = string.Empty;
                if (context.Channel is IGuildChannel)
                {
                    logText = $"User: [{context.User.Username}] Discord Server: [{context.Guild.Name}] -> [{context.Message.Content}]";
                
                }
                else
                {
                    logText = $"User: [{context.User.Username}] -> [{context.Message.Content}]";
                }
                _logger.LogInformation(logText);
                return;
            }
            
            // if a command isn't found, log that info to console and exit this method
            if (!command.IsSpecified)
            {
                _logger.LogInformation($"Command [{context.Message.Content}] failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                return;
            }
                
            // failure scenario, let's let the user know
            await context.Channel.SendMessageAsync($"Sorry, {context.User.Mention}... something went wrong -> [{result}]!");
        }   
        
        private PrefixList GetPrefix(long serverId)
        {
            PrefixList prefix = null;
           
            prefix = _db.PrefixList.Where(p => p.ServerId == serverId).FirstOrDefault();
            
            return prefix;
        }        
    }
}