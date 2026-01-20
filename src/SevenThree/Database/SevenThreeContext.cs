using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DotNetEnv;

namespace SevenThree.Database
{
    public class SevenThreeContext : DbContext
    {
        public DbSet<CallSignAssociation> CallSignAssociation { get; set; }
        public DbSet<HamTest> HamTest { get; set; }
        public DbSet<Questions> Questions { get; set; }
        public DbSet<Answer> Answer { get; set; }
        public DbSet<Quiz> Quiz { get; set; }
        public DbSet<Figure> Figure { get; set; }
        public DbSet<UserAnswer> UserAnswer { get; set; }
        public DbSet<PrefixList> PrefixList { get; set; }
        public DbSet<Cred> Cred { get; set; }
        public DbSet<ApiData> ApiData { get; set; }
        public DbSet<QuizSettings> QuizSettings { get; set; }

        /// <summary>
        /// Constructor for IDbContextFactory pattern
        /// </summary>
        public SevenThreeContext(DbContextOptions<SevenThreeContext> options) : base(options)
        {
        }

        /// <summary>
        /// Parameterless constructor for EF Core migrations
        /// </summary>
        public SevenThreeContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            // Load .env file based on environment
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "development";
            var envFile = environment.ToLower() switch
            {
                "production" => ".env.production",
                "docker" => ".env.development.docker",
                _ => ".env.development"
            };

            var envPath = Path.Combine(Directory.GetCurrentDirectory(), envFile);
            if (!File.Exists(envPath))
            {
                envPath = Path.Combine(AppContext.BaseDirectory, envFile);
            }

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }

            // Build configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory);

            var configJsonPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(configJsonPath))
            {
                builder.AddJsonFile("config.json", optional: true);
            }

            builder.AddEnvironmentVariables("SEVENTHREE_");
            var configuration = builder.Build();

            // Get connection string from env or config
            var connectionString = configuration["ConnectionStrings:SevenThree"]
                ?? configuration["ConnectionString"]
                ?? Environment.GetEnvironmentVariable("SEVENTHREE_ConnectionStrings__SevenThree");

            if (!string.IsNullOrEmpty(connectionString))
            {
                // Use PostgreSQL
                optionsBuilder.UseNpgsql(connectionString);
            }
            else
            {
                // Fallback to SQLite for backwards compatibility
                var dbPath = configuration["Db"] ?? "seventhree.db";
                optionsBuilder.UseNpgsql($"Host=localhost;Database=seventhree;Username=postgres;Password=postgres");
            }
        }
    }
}