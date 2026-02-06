using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Constants;

namespace SevenThree.Modules
{
    public class HelpSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly InteractionService _interactions;

        public HelpSlashCommands(IServiceProvider services)
        {
            _interactions = services.GetRequiredService<InteractionService>();
        }

        [SlashCommand("help", "Get help with SevenThree bot commands")]
        public async Task GetHelp()
        {
            await DeferAsync(ephemeral: true);

            var embed = new EmbedBuilder();
            embed.Title = "SevenThree Help";
            embed.Description = "SevenThree is a Discord bot for ham radio enthusiasts. Here are the available commands:";
            embed.WithColor(new Color(0, 255, 0));
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            // Get all slash commands, excluding owner-only commands for non-owners
            var isOwner = await IsOwnerAsync();
            var commands = GetAvailableCommands(isOwner);

            // Group commands by category
            var categories = new Dictionary<string, List<(string Name, string Description)>>
            {
                ["Quick Start"] = new(),
                ["Practice Exams"] = new(),
                ["Study & Review"] = new(),
                ["QRZ Lookup"] = new(),
                ["PSK Reporter"] = new(),
                ["Callsign"] = new(),
                ["Band Conditions"] = new(),
                ["Server Settings"] = new(),
                ["Other"] = new(),
                ["Admin"] = new()
            };

            foreach (var cmd in commands)
            {
                var category = CategorizeCommand(cmd.Name, cmd.ModuleName);
                var displayName = FormatCommandName(cmd.Name, cmd.ModuleName);

                if (categories.ContainsKey(category))
                {
                    categories[category].Add((displayName, cmd.Description));
                }
            }

            // Add non-empty categories to embed
            foreach (var (category, cmds) in categories)
            {
                if (cmds.Count == 0) continue;

                // Skip admin category for non-owners
                if (category == "Admin" && !isOwner) continue;

                var value = string.Join("\n", cmds.Select(c => $"`{c.Name}` - {c.Description}"));
                embed.AddField(category, value, false);
            }

            embed.WithAuthor(new EmbedAuthorBuilder
            {
                Name = $"Help requested by: [{Context.User.Username}]",
                IconUrl = Context.User.GetAvatarUrl()
            });

            embed.WithFooter(new EmbedFooterBuilder
            {
                Text = "SevenThree, your local ham radio Discord bot. 73!",
                IconUrl = QuizConstants.BOT_THUMBNAIL_URL
            });

            await FollowupAsync(embed: embed.Build());
        }

        private async Task<bool> IsOwnerAsync()
        {
            try
            {
                var appInfo = await Context.Client.GetApplicationInfoAsync();
                // Check both direct owner and team members if it's a team-owned app
                if (appInfo.Owner?.Id == Context.User.Id)
                    return true;

                if (appInfo.Team != null)
                {
                    return appInfo.Team.TeamMembers.Any(m => m.User.Id == Context.User.Id);
                }

                return false;
            }
            catch
            {
                // If we can't determine ownership, default to not showing admin commands
                return false;
            }
        }

        private List<CommandInfo> GetAvailableCommands(bool includeOwnerOnly)
        {
            var result = new List<CommandInfo>();

            foreach (var module in _interactions.Modules)
            {
                // Get the group prefix if this is a grouped module
                var groupAttr = module.Attributes.OfType<GroupAttribute>().FirstOrDefault();
                var groupPrefix = groupAttr?.Name;

                foreach (var cmd in module.SlashCommands)
                {
                    // Check if command is owner-only
                    var isOwnerOnly = cmd.Preconditions.Any(p => p is RequireOwnerAttribute) ||
                                      module.Preconditions.Any(p => p is RequireOwnerAttribute);

                    if (isOwnerOnly && !includeOwnerOnly)
                        continue;

                    result.Add(new CommandInfo
                    {
                        Name = cmd.Name,
                        Description = cmd.Description,
                        ModuleName = groupPrefix ?? module.Name,
                        GroupPrefix = groupPrefix,
                        IsOwnerOnly = isOwnerOnly,
                        RequiresPermissions = cmd.Preconditions.Any(p => p is RequireUserPermissionAttribute) ||
                                              module.Preconditions.Any(p => p is RequireUserPermissionAttribute)
                    });
                }
            }

            return result;
        }

        private string CategorizeCommand(string commandName, string moduleName)
        {
            // Quick start commands (top-level /tech, /general, /extra)
            if (commandName is "tech" or "general" or "extra" && moduleName == "QuickStartSlashCommands")
                return "Quick Start";

            // Quiz commands
            if (moduleName == "quiz")
                return "Practice Exams";

            // Study commands
            if (moduleName == "study")
                return "Study & Review";

            // QRZ commands
            if (moduleName == "qrz")
                return "QRZ Lookup";

            // PSK commands
            if (moduleName == "psk")
                return "PSK Reporter";

            // Callsign commands
            if (moduleName == "callsign")
                return "Callsign";

            // Conditions
            if (commandName == "conditions")
                return "Band Conditions";

            // Server settings
            if (moduleName == "quizsettings")
                return "Server Settings";

            // Admin commands (owner-only or with special permissions)
            if (commandName is "import" or "playing")
                return "Admin";

            return "Other";
        }

        private string FormatCommandName(string commandName, string moduleName)
        {
            // Check if this is part of a command group
            if (moduleName is "quiz" or "qrz" or "psk" or "callsign" or "quizsettings" or "study")
            {
                return $"/{moduleName} {commandName}";
            }

            return $"/{commandName}";
        }

        private class CommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string ModuleName { get; set; }
            public string GroupPrefix { get; set; }
            public bool IsOwnerOnly { get; set; }
            public bool RequiresPermissions { get; set; }
        }
    }
}
