using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using DotNetEnv;

namespace SevenThree.Services
{
    public class ConfigService
    {
        public IConfigurationRoot ConfigureServices()
        {
            // Load .env file based on environment
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "development";
            var envFile = environment.ToLower() switch
            {
                "production" => ".env.production",
                "docker" => ".env.development.docker",
                _ => ".env.development"
            };

            // Try to load env file from current directory or app base directory
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), envFile);
            if (!File.Exists(envPath))
            {
                envPath = Path.Combine(AppContext.BaseDirectory, envFile);
            }

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }

            // Build configuration with env-first approach
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory);

            // Add config.json as fallback if it exists
            var configJsonPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(configJsonPath))
            {
                builder.AddJsonFile("config.json", optional: true);
            }

            // Environment variables take precedence (SEVENTHREE_ prefix)
            builder.AddEnvironmentVariables("SEVENTHREE_");

            return builder.Build();
        }
    }
}