using System;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Services;
using Serilog;
using Serilog.Sinks.File;
using Serilog.Sinks.SystemConsole;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SevenThree.Database;
using Microsoft.EntityFrameworkCore.Design;

namespace SevenThree
{
    class Program 
    {
        // setup our fields we assign later
        private readonly IConfiguration _config;
        private DiscordSocketClient _client;
        private static string _logLevel;

        static void Main(string[] args = null)
        {
            if (args.Count() != 0)
            {
                _logLevel = args[0];
            }
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("logs/svnthree.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public Program()
        {
            // create the configuration
            var _builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");  

            // build the configuration and assign to _config          
            _config = _builder.Build();
        }

        public async Task MainAsync()
        {
            // call ConfigureServices to create the ServiceCollection/Provider for passing around the services
            using (var services = ConfigureServices())
            {
                // you get the services via GetRequiredService<T>

                // get the database service
                var context = services.GetRequiredService<SevenThreeContext>();
                using (var db = context)
                {
                    db.CallSignAssociation.Add(new CallSignAssociation
                    {
                        DiscordUserId = 12345,
                        DiscordUserName = "Ginja#1234",
                        CallSign = "KF7IGN"
                    });
                    db.SaveChanges();
                }
                // get the logging service
                services.GetRequiredService<LoggingService>();

                // get the client service so we can start the bot up
                var client = services.GetRequiredService<DiscordSocketClient>();
                _client = client;

                // this is where we get the Token value from the configuration file, and start the bot
                await client.LoginAsync(TokenType.Bot, _config["Token"]);
                await client.StartAsync();

                // we get the CommandHandler class here and call the InitializeAsync method to start things up for the CommandHandler service
                await services.GetRequiredService<CommandHandler>().InitializeAsync();

                await Task.Delay(-1);
            }
        }

        // this method handles the ServiceCollection creation/configuration, and builds out the service provider we can call on later
        private ServiceProvider ConfigureServices()
        {
            // this returns a ServiceProvider that is used later to call for those services
            // we can add types we have access to here, hence adding the new using statement:
            // using SevenThree.Services;
            // the config we build is also added, which comes in handy for setting the command prefix!

            var services = new ServiceCollection()
                .AddSingleton<LoggingService>()
                .AddSingleton(_config)
                .AddSingleton<DiscordSocketClient>()
                .AddLogging(configure => configure.AddSerilog())
                .AddSingleton<CommandService>()
                .AddDbContext<SevenThreeContext>(
                    options => options.UseSqlite($"Data Source={_config["Db"]}"))             
                .AddSingleton<CommandHandler>();
            
            if (!string.IsNullOrEmpty(_logLevel))            
            {
                switch (_logLevel.ToLower())
                {
                    case "info":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
                        break;
                    }
                    case "error":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                    }  
                    case "debug":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
                        break;
                    }                     
                    default: 
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                    }
                }
                
            }
            else
            {
                services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
            }
            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }
    }
}
