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
using System.IO;

namespace SevenThree.Modules
{
    public enum LicenseType
    {
        Tech,
        General,
        Extra
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
            using var db = _contextFactory.CreateDbContext();
            ulong id = GetId();
            var quiz = await db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefaultAsync();

            if (quiz == null)
            {
                await RespondAsync("No quiz to end!", ephemeral: true);
                return;
            }

            var gUser = Context.User as IGuildUser;
            bool canStop = Context.User.Id == quiz.StartedById ||
                          (gUser != null && gUser.GuildPermissions.KickMembers);

            if (!canStop)
            {
                await RespondAsync($"Sorry, {Context.User.Mention}, a test can only be stopped by the person who started it, or by someone with at least **KickMembers** permissions!", ephemeral: true);
                return;
            }

            QuizUtil trivia = null;
            if (_hamTestService.RunningTests.TryRemove(id, out trivia))
            {
                await trivia.StopQuiz().ConfigureAwait(false);
                await RespondAsync("Quiz stopped!", ephemeral: true);
            }
            else
            {
                await RespondAsync("No quiz to end!", ephemeral: true);
            }
        }

        [SlashCommand("import", "Import question pool(s) from JSON (bot owner only)")]
        [RequireOwner]
        public async Task ImportQuestions(
            [Summary("license", "License class to import")] LicenseType license)
        {
            await DeferAsync(ephemeral: true);

            using var db = _contextFactory.CreateDbContext();
            var testName = license.ToString().ToLower();
            var testDesc = $"U.S. Ham Radio test for the {testName} class license.";

            try
            {
                var importDir = $"import/{testName}";
                if (!Directory.Exists(importDir))
                {
                    await FollowupAsync($"Import directory not found: {importDir}");
                    return;
                }

                var importResults = new List<string>();
                var totalFiguresImported = 0;

                // Check for dated subfolders (e.g., "2022-2026", "2026-2030")
                var subDirs = Directory.GetDirectories(importDir)
                    .Where(d => Path.GetFileName(d).Contains('-'))
                    .ToList();

                if (subDirs.Count > 0)
                {
                    // Process each subfolder as a separate pool with its own figures
                    foreach (var subDir in subDirs)
                    {
                        var result = await ImportPoolFromDirectory(db, testName, testDesc, subDir);
                        importResults.Add(result.Message);
                        totalFiguresImported += result.FiguresImported;
                    }
                }
                else
                {
                    // Flat structure (backwards compatible) - single pool
                    var result = await ImportPoolFromDirectory(db, testName, testDesc, importDir);
                    importResults.Add(result.Message);
                    totalFiguresImported += result.FiguresImported;
                }

                if (importResults.Count == 0 || importResults.All(r => r.StartsWith("Skipped") || r.StartsWith("No ")))
                {
                    await FollowupAsync($"No valid import files found for {testName}!");
                    return;
                }

                var summary = string.Join("\n", importResults);
                await FollowupAsync($"Import complete for {testName.ToUpper()}:\n{summary}\nTotal figures: {totalFiguresImported}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing questions");
                await FollowupAsync($"Error importing {testName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Import a single test pool from a directory (subfolder or main folder)
        /// </summary>
        private async Task<(string Message, int FiguresImported)> ImportPoolFromDirectory(
            SevenThreeContext db, string testName, string testDesc, string directory)
        {
            // Find JSON file in this directory
            var jsonFiles = Directory.GetFiles(directory, $"{testName}_*.json");
            if (jsonFiles.Length == 0)
            {
                return ($"No JSON file in {Path.GetFileName(directory)}", 0);
            }

            var curFile = jsonFiles[0]; // Use first JSON file found
            var fileName = Path.GetFileNameWithoutExtension(curFile);
            var fileNameParts = fileName.Split('_');

            if (fileNameParts.Length < 3)
            {
                return ($"Skipped {fileName}: invalid format", 0);
            }

            if (!DateTime.TryParse(fileNameParts[1], out var parsedStartDate) ||
                !DateTime.TryParse(fileNameParts[2], out var parsedEndDate))
            {
                return ($"Skipped {fileName}: could not parse dates", 0);
            }

            DateTime startDate = DateTime.SpecifyKind(parsedStartDate, DateTimeKind.Utc);
            DateTime endDate = DateTime.SpecifyKind(parsedEndDate, DateTimeKind.Utc);

            // Find existing test pool by TestName + date range
            var test = await db.HamTest
                .Where(t => t.TestName == testName && t.FromDate == startDate && t.ToDate == endDate)
                .FirstOrDefaultAsync();

            if (test == null)
            {
                // Create new test pool
                test = new HamTest
                {
                    TestName = testName,
                    TestDescription = testDesc,
                    FromDate = startDate,
                    ToDate = endDate
                };
                await db.AddAsync(test);
                await db.SaveChangesAsync();
            }
            else
            {
                // Clear old items for THIS specific test pool only (order matters for FK constraints)
                var userAnswers = await db.UserAnswer
                    .Where(ua => ua.Question.Test.TestId == test.TestId)
                    .ToListAsync();
                db.RemoveRange(userAnswers);

                var answers = await db.Answer
                    .Where(a => a.Question.Test.TestId == test.TestId)
                    .ToListAsync();
                db.RemoveRange(answers);

                var questions = await db.Questions
                    .Where(q => q.Test.TestId == test.TestId)
                    .ToListAsync();
                db.RemoveRange(questions);

                var figures = await db.Figure
                    .Where(f => f.Test.TestId == test.TestId)
                    .ToListAsync();
                db.RemoveRange(figures);

                await db.SaveChangesAsync();
            }

            // Import questions
            var questionData = QuestionIngest.FromJson(await File.ReadAllTextAsync(curFile));

            foreach (var item in questionData)
            {
                var newQuestion = new Questions
                {
                    QuestionText = item.Question,
                    QuestionSection = item.QuestionId,
                    FccPart = item.FccPart,
                    SubelementDesc = item.SubelementDesc,
                    FigureName = item.Figure,
                    SubelementName = item.SubelementName?.ToString(),
                    Test = test
                };
                db.Questions.Add(newQuestion);

                var answerChar = item.AnswerKey.ToString()[0];
                foreach (var answer in item.PossibleAnswer)
                {
                    var posAnswerChar = answer[0];
                    var posAnswerText = answer.Length > 3 ? answer.Substring(3) : answer;

                    db.Answer.Add(new Answer
                    {
                        Question = newQuestion,
                        AnswerText = posAnswerText,
                        IsAnswer = answerChar == posAnswerChar
                    });
                }
            }

            await db.SaveChangesAsync();

            // Import figures from this directory - linked to THIS specific pool
            var figureFiles = Directory.EnumerateFiles(directory, $"{testName}_*.png");
            var figuresImported = 0;

            foreach (var file in figureFiles)
            {
                var figureName = Path.GetFileNameWithoutExtension(file)
                    .Replace($"{testName}_", "")
                    .Trim();

                var contents = await File.ReadAllBytesAsync(file);
                db.Figure.Add(new Figure
                {
                    Test = test,
                    FigureName = figureName,
                    FigureImage = contents
                });
                figuresImported++;
            }

            if (figuresImported > 0)
            {
                await db.SaveChangesAsync();
            }

            return ($"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}: {questionData.Count} questions, {figuresImported} figures", figuresImported);
        }

        private async Task StartTest(LicenseType license, int? testId, int numQuestions, int questionDelay, QuizMode mode)
        {
            using var db = _contextFactory.CreateDbContext();
            var testName = license.ToString().ToLower();
            var isPrivate = mode == QuizMode.Private;

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
                await RespondAsync($"No question pool found for {testName.ToUpper()}. Use `/quiz import {testName}` first.", ephemeral: true);
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
                        await RespondAsync($"{testName.ToUpper()} test commands cannot be used in this channel, please use them in <#{requiredChannel}>!", ephemeral: true);
                        return;
                    }
                }
            }

            ulong id = isPrivate ? Context.User.Id : GetId();

            var checkQuiz = db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefault();
            if (checkQuiz != null)
            {
                await RespondAsync("There is already an active quiz!", ephemeral: true);
                return;
            }

            await db.Quiz.AddAsync(new Quiz
            {
                ServerId = id,
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = Context.User.Id,
                StartedByName = Context.User.Username,
                StartedByIconUrl = Context.User.GetAvatarUrl()
            });
            await db.SaveChangesAsync();

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

            // Set the quiz mode
            startQuiz.SetQuizMode(mode);

            if (_hamTestService.RunningTests.TryAdd(id, startQuiz))
            {
                var poolInfo = $"{selectedPool.FromDate:yyyy-MM-dd} to {selectedPool.ToDate:yyyy-MM-dd}";
                var delayNote = delayWasClamped ? $" (delay adjusted to {questionDelay}s)" : "";
                await RespondAsync($"Starting {testName.ToUpper()} practice exam [{poolInfo}] with {numQuestions} questions. Answer using buttons!{delayNote}", ephemeral: isPrivate);

                var quiz = await db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefaultAsync();
                try
                {
                    await startQuiz.StartGame(quiz, numQuestions, selectedPool.TestId, questionDelay * 1000).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting quiz");
                }
            }
            else
            {
                await RespondAsync("There is already an active quiz!", ephemeral: true);
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
            await RespondAsync($"{license} test channel set to #{Context.Channel.Name}!", ephemeral: true);
        }

        [SlashCommand("unsetchannel", "Remove this channel from quiz settings")]
        public async Task UnsetChannel()
        {
            using var db = _contextFactory.CreateDbContext();
            var settings = await db.QuizSettings
                .Where(s => s.DiscordGuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                await RespondAsync("No quiz settings found for this server.", ephemeral: true);
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
                await RespondAsync("Channel unset from quiz settings!", ephemeral: true);
            }
            else
            {
                await RespondAsync("This channel is not set for any quiz type.", ephemeral: true);
            }
        }

        [SlashCommand("clearafter", "Toggle clearing messages after test completion")]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task ClearAfterTaken()
        {
            using var db = _contextFactory.CreateDbContext();
            var settings = await db.QuizSettings
                .Where(s => s.DiscordGuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (settings == null || (settings.TechChannelId == null && settings.GeneralChannelId == null && settings.ExtraChannelId == null))
            {
                await RespondAsync("Please set a channel to a specific test before using this command!", ephemeral: true);
                return;
            }

            settings.ClearAfterTaken = !settings.ClearAfterTaken;
            await db.SaveChangesAsync();

            var status = settings.ClearAfterTaken ? "enabled" : "disabled";
            await RespondAsync($"Clear after test completion is now **{status}**!", ephemeral: true);
        }
    }
}
