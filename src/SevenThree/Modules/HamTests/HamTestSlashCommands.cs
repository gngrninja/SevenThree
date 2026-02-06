using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SevenThree.Constants;
using SevenThree.Database;
using SevenThree.Models;
using SevenThree.Modules.HamTests;
using SevenThree.Services;

namespace SevenThree.Modules
{
    public enum LicenseType
    {
        Tech,
        General,
        Extra
    }

    /// <summary>
    /// Shorthand commands for quickly starting practice exams with defaults.
    /// These are top-level commands (/tech, /general, /extra) without the /quiz prefix.
    /// </summary>
    public class QuickStartSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<QuickStartSlashCommands> _logger;
        private readonly IServiceProvider _services;
        private readonly HamTestService _hamTestService;
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        // Defaults for quick start commands
        private const int DefaultQuestions = 35;
        private const int DefaultDelaySeconds = 45;
        private const QuizMode DefaultMode = QuizMode.Private;

        public QuickStartSlashCommands(
            IServiceProvider services,
            IDbContextFactory<SevenThreeContext> contextFactory)
        {
            _logger = services.GetRequiredService<ILogger<QuickStartSlashCommands>>();
            _services = services;
            _hamTestService = services.GetRequiredService<HamTestService>();
            _contextFactory = contextFactory;
        }

        [SlashCommand("tech", "Quick start a Technician practice exam with defaults")]
        public async Task QuickTech()
        {
            await QuickStartTest(LicenseType.Tech);
        }

        [SlashCommand("general", "Quick start a General practice exam with defaults")]
        public async Task QuickGeneral()
        {
            await QuickStartTest(LicenseType.General);
        }

        [SlashCommand("extra", "Quick start an Extra practice exam with defaults")]
        public async Task QuickExtra()
        {
            await QuickStartTest(LicenseType.Extra);
        }

        private async Task QuickStartTest(LicenseType license)
        {
            // Always private and ephemeral for quick start
            await DeferAsync(ephemeral: true);

            try
            {
                using var db = _contextFactory.CreateDbContext();
                var testName = license.ToString().ToLower();

                // Find the current (effective) pool - today is between FromDate and ToDate
                var today = DateTime.UtcNow.Date;
                var selectedPool = await db.HamTest
                    .Where(t => t.TestName == testName && t.FromDate <= today && t.ToDate >= today)
                    .FirstOrDefaultAsync();

                // Fall back to most recent pool if no current pool
                selectedPool ??= await db.HamTest
                    .Where(t => t.TestName == testName)
                    .OrderByDescending(t => t.FromDate)
                    .FirstOrDefaultAsync();

                if (selectedPool == null)
                {
                    await FollowupAsync($"No question pool found for {testName.ToUpper()}. Use `/import {testName}` first.", ephemeral: true);
                    return;
                }

                // Validate that the pool has questions
                var questionCount = await db.Questions.CountAsync(q => q.Test.TestId == selectedPool.TestId && !q.IsArchived);
                if (questionCount == 0)
                {
                    await FollowupAsync($"No questions found in the {testName.ToUpper()} pool. The pool may need to be imported.", ephemeral: true);
                    return;
                }

                ulong id = Context.User.Id; // Private mode uses user ID

                // Create QuizUtil instance for private mode
                var startQuiz = new QuizUtil(
                    user: Context.User as IUser,
                    services: _services,
                    guild: Context.Guild as IGuild,
                    id: id
                );
                startQuiz.SetQuizMode(DefaultMode);

                // Use ConcurrentDictionary.TryAdd as the atomic gate
                if (!_hamTestService.RunningTests.TryAdd(id, startQuiz))
                {
                    await FollowupAsync("You already have an active quiz! Use `/quiz stop` to end it first.", ephemeral: true);
                    return;
                }

                // We now "own" this slot - create DB record and start quiz
                try
                {
                    var quiz = new Quiz
                    {
                        ServerId = id,
                        IsActive = true,
                        TimeStarted = DateTime.UtcNow,
                        StartedById = Context.User.Id,
                        StartedByName = Context.User.Username,
                        StartedByIconUrl = Context.User.GetAvatarUrl()
                    };
                    await db.Quiz.AddAsync(quiz);
                    await db.SaveChangesAsync();

                    var poolInfo = $"{selectedPool.FromDate:yyyy-MM-dd} to {selectedPool.ToDate:yyyy-MM-dd}";
                    await FollowupAsync($"Starting {testName.ToUpper()} practice exam [{poolInfo}] with {DefaultQuestions} questions. Answer using buttons!", ephemeral: true);

                    await startQuiz.StartGame(quiz, DefaultQuestions, selectedPool.TestId, DefaultDelaySeconds * 1000).ConfigureAwait(false);
                }
                catch
                {
                    // Failed after claiming slot - release it so user can try again
                    _hamTestService.RunningTests.TryRemove(id, out _);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QuickStartTest for {License}", license);
                try
                {
                    await FollowupAsync("An error occurred starting the quiz. Please try again.", ephemeral: true);
                }
                catch
                {
                    // Followup failed, nothing more we can do
                }
            }
        }
    }

    [Group("quiz", "Ham radio license exam practice commands")]
    public class HamTestSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<HamTestSlashCommands> _logger;
        private readonly IServiceProvider _services;
        private readonly HamTestService _hamTestService;
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        public HamTestSlashCommands(
            IServiceProvider services,
            IDbContextFactory<SevenThreeContext> contextFactory)
        {
            _logger = services.GetRequiredService<ILogger<HamTestSlashCommands>>();
            _services = services;
            _hamTestService = services.GetRequiredService<HamTestService>();
            _contextFactory = contextFactory;
        }

        [SlashCommand("tech", "Start a Technician class practice exam")]
        public async Task StartTech(
            [Summary("pool", "Question pool (defaults to current)")][Autocomplete(typeof(TestPoolAutocompleteHandler))] int? testId = null,
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.Tech, testId, questions, delay, mode);
        }

        [SlashCommand("general", "Start a General class practice exam")]
        public async Task StartGeneral(
            [Summary("pool", "Question pool (defaults to current)")][Autocomplete(typeof(TestPoolAutocompleteHandler))] int? testId = null,
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.General, testId, questions, delay, mode);
        }

        [SlashCommand("extra", "Start an Extra class practice exam")]
        public async Task StartExtra(
            [Summary("pool", "Question pool (defaults to current)")][Autocomplete(typeof(TestPoolAutocompleteHandler))] int? testId = null,
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.Extra, testId, questions, delay, mode);
        }

        [SlashCommand("start", "Start a ham radio practice exam")]
        public async Task StartQuiz(
            [Summary("license", "License class to practice")] LicenseType license,
            [Summary("pool", "Question pool (defaults to current)")][Autocomplete(typeof(TestPoolAutocompleteHandler))] int? testId = null,
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(license, testId, questions, delay, mode);
        }

        [SlashCommand("stop", "Stop the current practice exam")]
        public async Task StopQuiz()
        {
            // Defer immediately to avoid Discord timeout
            await DeferAsync(ephemeral: true);

            try
            {
                using var db = _contextFactory.CreateDbContext();

                ulong channelId = GetId();
                ulong userId = Context.User.Id;

                // For private quizzes, the key is the user's ID
                // For public quizzes, the key is the channel ID
                // Check both possible keys in the dictionary
                QuizUtil triviaFromDict = null;
                ulong foundKey = 0;

                // Try user ID first (private quiz)
                if (_hamTestService.RunningTests.TryGetValue(userId, out triviaFromDict))
                {
                    foundKey = userId;
                }
                // Try channel ID (public quiz)
                else if (_hamTestService.RunningTests.TryGetValue(channelId, out triviaFromDict))
                {
                    foundKey = channelId;
                }

                // Also check DB for quiz record
                var quiz = await db.Quiz
                    .Where(q => (q.StartedById == userId || q.ServerId == channelId || q.ServerId == userId) && q.IsActive)
                    .FirstOrDefaultAsync();

                // No quiz in dictionary AND no quiz in DB
                if (triviaFromDict == null && quiz == null)
                {
                    await FollowupAsync("No quiz to end!", ephemeral: true);
                    return;
                }

                // Permission check - use DB record if available, otherwise allow owner
                var gUser = Context.User as IGuildUser;
                bool canStop = (quiz != null && Context.User.Id == quiz.StartedById) ||
                              (gUser != null && gUser.GuildPermissions.KickMembers) ||
                              (quiz == null && triviaFromDict != null); // Allow stopping orphaned dict entries

                if (!canStop)
                {
                    await FollowupAsync($"Sorry, {Context.User.Mention}, a test can only be stopped by the person who started it, or by someone with at least **KickMembers** permissions!", ephemeral: true);
                    return;
                }

                // Stop quiz in dictionary if present
                if (triviaFromDict != null && _hamTestService.RunningTests.TryRemove(foundKey, out var trivia))
                {
                    await trivia.StopQuiz().ConfigureAwait(false);
                }

                // Clean up DB record if present
                if (quiz != null)
                {
                    quiz.IsActive = false;
                    quiz.TimeEnded = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                // Report what was cleaned up
                if (triviaFromDict != null && quiz != null)
                {
                    await FollowupAsync("Quiz stopped!", ephemeral: true);
                }
                else if (triviaFromDict != null)
                {
                    await FollowupAsync("Quiz stopped! (cleaned up orphaned memory session)", ephemeral: true);
                }
                else
                {
                    await FollowupAsync("Quiz stopped! (cleaned up orphaned database session)", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StopQuiz");
                try
                {
                    await FollowupAsync("An error occurred stopping the quiz. Please try again.", ephemeral: true);
                }
                catch
                {
                    // Followup failed, nothing more we can do
                }
            }
        }

        private async Task StartTest(LicenseType license, int? testId, int numQuestions, int questionDelay, QuizMode mode)
        {
            var isPrivate = mode == QuizMode.Private;

            // Defer immediately to avoid Discord timeout (gives us 15 minutes)
            await DeferAsync(ephemeral: isPrivate);

            try
            {
                using var db = _contextFactory.CreateDbContext();
                var testName = license.ToString().ToLower();

                // Validate delay using constants
                var originalDelay = questionDelay;
                questionDelay = Math.Clamp(questionDelay, QuizConstants.MIN_DELAY_SECONDS, QuizConstants.MAX_DELAY_SECONDS);
                var delayWasClamped = originalDelay != questionDelay;

                // Resolve test pool if not specified
                HamTest selectedPool = null;
                if (testId.HasValue)
                {
                    selectedPool = await db.HamTest.Where(t => t.TestId == testId.Value).FirstOrDefaultAsync();
                }

                if (selectedPool == null)
                {
                    // Find current pool (today is between FromDate and ToDate)
                    var today = DateTime.UtcNow.Date;
                    selectedPool = await db.HamTest
                        .Where(t => t.TestName == testName && t.FromDate <= today && t.ToDate >= today)
                        .FirstOrDefaultAsync();

                    // Fall back to most recent pool if no current pool
                    selectedPool ??= await db.HamTest
                        .Where(t => t.TestName == testName)
                        .OrderByDescending(t => t.FromDate)
                        .FirstOrDefaultAsync();
                }

                if (selectedPool == null)
                {
                    await FollowupAsync($"No question pool found for {testName.ToUpper()}. Use `/import {testName}` first.", ephemeral: isPrivate);
                    return;
                }

                // Check channel restrictions (only for public mode)
                if (Context.Guild != null && !isPrivate)
                {
                    var channelInfo = await db.QuizSettings
                        .Where(q => q.DiscordGuildId == Context.Guild.Id)
                        .FirstOrDefaultAsync();

                    if (channelInfo != null)
                    {
                        ulong? requiredChannel = testName switch
                        {
                            "tech" => channelInfo.TechChannelId,
                            "general" => channelInfo.GeneralChannelId,
                            "extra" => channelInfo.ExtraChannelId,
                            _ => null
                        };

                        if (requiredChannel.HasValue && requiredChannel != Context.Channel.Id)
                        {
                            await FollowupAsync($"{testName.ToUpper()} test commands cannot be used in this channel, please use them in <#{requiredChannel}>!", ephemeral: isPrivate);
                            return;
                        }
                    }
                }

                ulong id = isPrivate ? Context.User.Id : GetId();

                // Validate that the pool has questions before doing anything else
                var questionCount = await db.Questions.CountAsync(q => q.Test.TestId == selectedPool.TestId && !q.IsArchived);
                if (questionCount == 0)
                {
                    await FollowupAsync($"No questions found in the {testName.ToUpper()} pool. The pool may need to be imported.", ephemeral: isPrivate);
                    return;
                }

                // Create QuizUtil instance first (no side effects, just object construction)
                QuizUtil startQuiz;
                if (isPrivate)
                {
                    startQuiz = new QuizUtil(
                        user: Context.User as IUser,
                        services: _services,
                        guild: Context.Guild as IGuild,
                        id: id
                    );
                }
                else
                {
                    startQuiz = new QuizUtil(
                        channel: Context.Channel as ITextChannel,
                        services: _services,
                        guild: Context.Guild as IGuild,
                        id: id
                    );
                }
                startQuiz.SetQuizMode(mode);

                // Use ConcurrentDictionary.TryAdd as the atomic gate - this prevents race conditions
                // Only one thread can successfully TryAdd for a given key
                if (!_hamTestService.RunningTests.TryAdd(id, startQuiz))
                {
                    await FollowupAsync("There is already an active quiz!", ephemeral: isPrivate);
                    return;
                }

                // We now "own" this slot - create DB record and start quiz
                // If anything fails, we must remove from dictionary
                try
                {
                    var quiz = new Quiz
                    {
                        ServerId = id,
                        IsActive = true,
                        TimeStarted = DateTime.UtcNow,
                        StartedById = Context.User.Id,
                        StartedByName = Context.User.Username,
                        StartedByIconUrl = Context.User.GetAvatarUrl()
                    };
                    await db.Quiz.AddAsync(quiz);
                    await db.SaveChangesAsync();

                    var poolInfo = $"{selectedPool.FromDate:yyyy-MM-dd} to {selectedPool.ToDate:yyyy-MM-dd}";
                    var delayNote = delayWasClamped ? $" (delay adjusted to {questionDelay}s)" : "";
                    await FollowupAsync($"Starting {testName.ToUpper()} practice exam [{poolInfo}] with {numQuestions} questions. Answer using buttons!{delayNote}", ephemeral: isPrivate);

                    await startQuiz.StartGame(quiz, numQuestions, selectedPool.TestId, questionDelay * 1000).ConfigureAwait(false);
                }
                catch
                {
                    // Failed after claiming slot - release it so user can try again
                    _hamTestService.RunningTests.TryRemove(id, out _);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartTest");
                try
                {
                    await FollowupAsync("An error occurred starting the quiz. Please try again.", ephemeral: isPrivate);
                }
                catch
                {
                    // Followup failed, nothing more we can do
                }
            }
        }

        private ulong GetId()
        {
            if (Context.Channel is IDMChannel)
            {
                return Context.User.Id;
            }
            return Context.Channel.Id;
        }
    }

    [Group("quizsettings", "Configure quiz settings for this server")]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public class HamTestChannelSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        public HamTestChannelSlashCommands(IDbContextFactory<SevenThreeContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [SlashCommand("setchannel", "Set this channel for a specific license test")]
        public async Task SetChannel(
            [Summary("license", "License class for this channel")] LicenseType license)
        {
            await DeferAsync(ephemeral: true);

            using var db = _contextFactory.CreateDbContext();
            var discordSettings = await db.QuizSettings
                .Where(s => s.DiscordGuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (discordSettings == null)
            {
                discordSettings = new QuizSettings { DiscordGuildId = Context.Guild.Id };
                await db.QuizSettings.AddAsync(discordSettings);
            }

            switch (license)
            {
                case LicenseType.Tech:
                    discordSettings.TechChannelId = Context.Channel.Id;
                    break;
                case LicenseType.General:
                    discordSettings.GeneralChannelId = Context.Channel.Id;
                    break;
                case LicenseType.Extra:
                    discordSettings.ExtraChannelId = Context.Channel.Id;
                    break;
            }

            await db.SaveChangesAsync();
            await FollowupAsync($"{license} test channel set to #{Context.Channel.Name}!");
        }

        [SlashCommand("unsetchannel", "Remove this channel from quiz settings")]
        public async Task UnsetChannel()
        {
            await DeferAsync(ephemeral: true);

            using var db = _contextFactory.CreateDbContext();
            var settings = await db.QuizSettings
                .Where(s => s.DiscordGuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                await FollowupAsync("No quiz settings found for this server.");
                return;
            }

            bool found = false;
            if (settings.TechChannelId == Context.Channel.Id)
            {
                settings.TechChannelId = null;
                found = true;
            }
            if (settings.GeneralChannelId == Context.Channel.Id)
            {
                settings.GeneralChannelId = null;
                found = true;
            }
            if (settings.ExtraChannelId == Context.Channel.Id)
            {
                settings.ExtraChannelId = null;
                found = true;
            }

            if (found)
            {
                await db.SaveChangesAsync();
                await FollowupAsync("Channel unset from quiz settings!");
            }
            else
            {
                await FollowupAsync("This channel is not set for any quiz type.");
            }
        }

        [SlashCommand("clearafter", "Toggle clearing messages after test completion")]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task ClearAfterTaken()
        {
            await DeferAsync(ephemeral: true);

            using var db = _contextFactory.CreateDbContext();
            var settings = await db.QuizSettings
                .Where(s => s.DiscordGuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (settings == null || (settings.TechChannelId == null && settings.GeneralChannelId == null && settings.ExtraChannelId == null))
            {
                await FollowupAsync("Please set a channel to a specific test before using this command!");
                return;
            }

            settings.ClearAfterTaken = !settings.ClearAfterTaken;
            await db.SaveChangesAsync();

            var status = settings.ClearAfterTaken ? "enabled" : "disabled";
            await FollowupAsync($"Clear after test completion is now **{status}**!");
        }
    }
}
