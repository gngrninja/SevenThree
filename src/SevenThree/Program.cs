using System;
using System.IO;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SevenThree.Services;
using Serilog;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using SevenThree.Modules;
using SevenThree.Modules.BandConditions;
using SevenThree.Modules.PskReporter;

namespace SevenThree
{
    class Program
    {
        private readonly IConfiguration _config;
        private DiscordSocketClient _client;
        private static string _logLevel;

        static void Main(string[] args = null)
        {
            if (args != null && args.Length != 0)
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
            _config = new ConfigService().ConfigureServices();
        }

        public async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                try
                {
                    // Apply pending migrations on startup
                    Log.Information("Checking database migrations...");
                    var dbFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
                    using (var db = dbFactory.CreateDbContext())
                    {
                        db.Database.Migrate();
                    }
                    Log.Information("Database migrations applied.");

                    services.GetRequiredService<LoggingService>();

                    var client = services.GetRequiredService<DiscordSocketClient>();
                    _client = client;

                    // Handle disconnection
                    client.Disconnected += (ex) =>
                    {
                        Log.Error(ex, "Discord client disconnected");
                        return Task.CompletedTask;
                    };

                    // Get token from env (BOT_TOKEN) or config (Token)
                    var token = _config["BOT_TOKEN"] ?? _config["Token"];
                    if (string.IsNullOrEmpty(token))
                    {
                        Log.Error("No bot token found. Set SEVENTHREE_BOT_TOKEN environment variable or Token in config.json");
                        return;
                    }

                    // Initialize HamTestService (cleanup stale quizzes)
                    var hamTestService = services.GetRequiredService<HamTestService>();
                    await hamTestService.InitializeAsync();

                    // Initialize interaction handler
                    var interactionHandler = services.GetRequiredService<InteractionHandler>();
                    await interactionHandler.InitializeAsync();

                    // Register slash commands when ready
                    client.Ready += async () =>
                    {
                        Log.Information("Discord client ready, registering slash commands...");
                        await interactionHandler.RegisterCommandsAsync();
                    };

                    Log.Information("Logging in to Discord...");
                    await client.LoginAsync(TokenType.Bot, token);
                    Log.Information("Starting Discord client...");
                    await client.StartAsync();

                    // Initialize reaction service for quiz answers
                    services.GetRequiredService<ReactionService>();

                    Log.Information("Bot is running. Press Ctrl+C to exit.");
                    await Task.Delay(-1);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fatal error in MainAsync");
                    throw;
                }
            }
        }

        private ServiceProvider ConfigureServices()
        {
            // Discord.Net 3.x requires explicit Gateway Intents
            var socketConfig = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.Guilds
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.GuildMessageReactions
                    | GatewayIntents.DirectMessages
                    | GatewayIntents.DirectMessageReactions
                    | GatewayIntents.MessageContent
            };

            var interactionConfig = new InteractionServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                LogLevel = LogSeverity.Info
            };

            var services = new ServiceCollection()
                .AddSingleton(socketConfig)
                .AddSingleton(interactionConfig)
                .AddSingleton<LoggingService>()
                .AddSingleton(_config)
                .AddSingleton<DiscordSocketClient>(sp => new DiscordSocketClient(sp.GetRequiredService<DiscordSocketConfig>()))
                .AddSingleton<InteractionService>(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>(), sp.GetRequiredService<InteractionServiceConfig>()))
                .AddSingleton<InteractionHandler>()
                .AddLogging(configure => configure.AddSerilog())
                .AddSingleton<XmlServices>()
                .AddSingleton<SecurityServices>()
                .AddTransient<Quiz>()
                .AddSingleton<QrzApi>()
                .AddSingleton<HamTestService>()
                .AddSingleton<QuizButtonHandler>()
                .AddSingleton<PskButtonHandler>()
                .AddSingleton<ReactionService>()
                .AddSingleton<BandConditions>()
                .AddSingleton<PskReporterService>()
                .AddDbContextFactory<SevenThreeContext>(options =>
                {
                    // Use connection string from already-loaded config (ConfigService loads .env)
                    var connectionString = _config["ConnectionStrings:SevenThree"]
                        ?? _config["ConnectionString"]
                        ?? Environment.GetEnvironmentVariable("SEVENTHREE_ConnectionStrings__SevenThree")
                        ?? "Host=localhost;Database=seventhree;Username=postgres;Password=postgres";

                    if (connectionString == "Host=localhost;Database=seventhree;Username=postgres;Password=postgres")
                    {
                        Log.Warning("No connection string found, using default localhost connection");
                    }

                    options.UseNpgsql(connectionString);
                });

            if (!string.IsNullOrEmpty(_logLevel))
            {
                switch (_logLevel.ToLower())
                {
                    case "info":
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
                        break;
                    case "error":
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                    case "debug":
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
                        break;
                    default:
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                }
            }
            else
            {
                services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
            }

            return services.BuildServiceProvider();
        }
    }
}
