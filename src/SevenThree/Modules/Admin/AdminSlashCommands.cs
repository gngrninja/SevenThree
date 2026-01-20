using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace SevenThree.Modules
{
    public class AdminSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _client;

        public AdminSlashCommands(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("playing", "Set the bot's playing status (bot owner only)")]
        [RequireOwner]
        public async Task ChangePlaying(
            [Summary("status", "The status message to display")] string status)
        {
            await _client.SetGameAsync(status);
            await RespondAsync($"Playing status set to: {status}", ephemeral: true);
        }

        [SlashCommand("ping", "Check if the bot is responsive")]
        public async Task Ping()
        {
            var latency = _client.Latency;
            await RespondAsync($"Pong! Latency: {latency}ms", ephemeral: true);
        }
    }
}
