using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Constants;
using SevenThree.Modules.Help;

namespace SevenThree.Modules
{
    public class HelpSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly InteractionService _interactions;

        public HelpSlashCommands(IServiceProvider services)
        {
            _interactions = services.GetRequiredService<InteractionService>();
        }

        private static readonly List<CategoryDefinition> Categories = new()
        {
            new("exams", "Exams", "Practice exams, quick start shortcuts, and quiz management", "📝"),
            new("study", "Study & Review", "Review missed questions and study weak areas", "📚"),
            new("qrz", "QRZ Lookup", "Look up callsign information via QRZ", "🔍"),
            new("psk", "PSK Reporter", "Propagation spots, band activity, and grid lookups", "📡"),
            new("callsign", "Callsign", "Associate your callsign with your Discord account", "📛"),
            new("conditions", "Band Conditions", "Check current band conditions", "🌐"),
            new("settings", "Server Settings", "Server-specific quiz settings", "⚙️"),
            new("other", "Other", "Other commands", "❓"),
        };

        [SlashCommand("help", "Get help with SevenThree bot commands")]
        public async Task GetHelp()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var commands = GetAvailableCommands(_interactions);

                var embed = BuildWelcomeEmbed();
                var menu = BuildCategorySelectMenu(commands);

                await FollowupAsync(embed: embed.Build(), components: menu, ephemeral: true);
            }
            catch (Exception)
            {
                await FollowupAsync("An error occurred loading help.", ephemeral: true);
            }
        }

        public static EmbedBuilder BuildWelcomeEmbed()
        {
            var embed = new EmbedBuilder();
            embed.Title = "SevenThree Help";
            embed.Description = "SevenThree is a Discord bot for ham radio enthusiasts.\n\n" +
                "Select a category below to view available commands.";
            embed.WithColor(new Color(0, 255, 0));
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;
            embed.WithFooter("SevenThree, your local ham radio Discord bot. 73!");

            return embed;
        }

        public static EmbedBuilder BuildCategoryEmbed(string categoryId, List<CommandInfo> commands)
        {
            var catDef = Categories.FirstOrDefault(c => c.Id == categoryId);
            if (catDef == null)
                return BuildWelcomeEmbed();

            var categoryName = CatIdToCategory(categoryId);
            var catCommands = commands
                .Where(c => CategorizeCommand(c.Name, c.ModuleName) == categoryName)
                .ToList();

            var embed = new EmbedBuilder();
            embed.Title = $"{catDef.Emoji} {catDef.Name}";
            embed.WithColor(categoryId == "admin" ? new Color(255, 100, 100) : new Color(0, 255, 0));
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            if (catCommands.Count == 0)
            {
                embed.Description = "No commands available in this category.";
                return embed;
            }

            foreach (var cmd in catCommands.Take(25))
            {
                var displayName = FormatCommandName(cmd.Name, cmd.ModuleName);
                embed.AddField($"`{displayName}`", cmd.Description, inline: false);
            }

            embed.WithFooter($"{catCommands.Count} command{(catCommands.Count != 1 ? "s" : "")} | SevenThree 73!");

            return embed;
        }

        public static MessageComponent BuildCategorySelectMenu(List<CommandInfo> commands)
        {
            var builder = new ComponentBuilder();
            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select a command category...")
                .WithCustomId(HelpSelectMenuHandler.SELECT_MENU_ID)
                .WithMinValues(1)
                .WithMaxValues(1);

            selectMenu.AddOption("Home", "welcome", "Return to help home", new Emoji("🏠"));

            foreach (var catDef in Categories)
            {
                var categoryName = CatIdToCategory(catDef.Id);
                var count = commands.Count(c => CategorizeCommand(c.Name, c.ModuleName) == categoryName);
                if (count == 0) continue;

                var description = catDef.Description;
                if (description.Length > 100)
                    description = description[..97] + "...";

                selectMenu.AddOption(
                    label: $"{catDef.Emoji} {catDef.Name}",
                    value: catDef.Id,
                    description: description);
            }

            builder.WithSelectMenu(selectMenu);
            return builder.Build();
        }

        #region Command Scanning

        public static List<CommandInfo> GetAvailableCommands(InteractionService interactions)
        {
            var result = new List<CommandInfo>();

            foreach (var module in interactions.Modules)
            {
                var groupAttr = module.Attributes.OfType<GroupAttribute>().FirstOrDefault();
                var groupPrefix = groupAttr?.Name;

                foreach (var cmd in module.SlashCommands)
                {
                    var isOwnerOnly = cmd.Preconditions.Any(p => p is RequireOwnerAttribute) ||
                                      module.Preconditions.Any(p => p is RequireOwnerAttribute);

                    if (isOwnerOnly)
                        continue;

                    result.Add(new CommandInfo
                    {
                        Name = cmd.Name,
                        Description = cmd.Description,
                        ModuleName = groupPrefix ?? module.Name,
                        GroupPrefix = groupPrefix,
                    });
                }
            }

            return result;
        }

        #endregion

        #region Categorization

        internal static string CategorizeCommand(string commandName, string moduleName)
        {
            if (commandName is "tech" or "general" or "extra" && moduleName == "QuickStartSlashCommands")
                return "Exams";

            if (moduleName == "quiz")
                return "Exams";

            if (moduleName == "study")
                return "Study & Review";

            if (moduleName == "qrz")
                return "QRZ Lookup";

            if (moduleName == "psk")
                return "PSK Reporter";

            if (moduleName == "callsign")
                return "Callsign";

            if (commandName == "conditions")
                return "Band Conditions";

            if (moduleName == "quizsettings")
                return "Server Settings";

            if (commandName is "import" or "playing")
                return "Admin";

            return "Other";
        }

        internal static string FormatCommandName(string commandName, string moduleName)
        {
            if (moduleName is "quiz" or "qrz" or "psk" or "callsign" or "quizsettings" or "study")
            {
                return $"/{moduleName} {commandName}";
            }

            return $"/{commandName}";
        }

        /// <summary>
        /// Maps a category dropdown ID back to the category name used by CategorizeCommand
        /// </summary>
        private static string CatIdToCategory(string catId)
        {
            return catId switch
            {
                "exams" => "Exams",
                "study" => "Study & Review",
                "qrz" => "QRZ Lookup",
                "psk" => "PSK Reporter",
                "callsign" => "Callsign",
                "conditions" => "Band Conditions",
                "settings" => "Server Settings",
                "admin" => "Admin",
                "other" => "Other",
                _ => "Other"
            };
        }

        #endregion

        public class CommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string ModuleName { get; set; }
            public string GroupPrefix { get; set; }
        }

        private record CategoryDefinition(string Id, string Name, string Description, string Emoji);
    }
}
