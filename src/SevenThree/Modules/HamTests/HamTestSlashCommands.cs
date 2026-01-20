using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SevenThree.Database;
using SevenThree.Models;
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
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.Tech, questions, delay, mode);
        }

        [SlashCommand("general", "Start a General class practice exam")]
        public async Task StartGeneral(
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.General, questions, delay, mode);
        }

        [SlashCommand("extra", "Start an Extra class practice exam")]
        public async Task StartExtra(
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(LicenseType.Extra, questions, delay, mode);
        }

        [SlashCommand("start", "Start a ham radio practice exam")]
        public async Task StartQuiz(
            [Summary("license", "License class to practice")] LicenseType license,
            [Summary("questions", "Number of questions (default: 35)")] int questions = 35,
            [Summary("delay", "Seconds between questions (15-120, default: 45)")] int delay = 45,
            [Summary("mode", "Quiz mode: Private (ephemeral, default) or Public (channel)")] QuizMode mode = QuizMode.Private)
        {
            await StartTest(license, questions, delay, mode);
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

        [SlashCommand("import", "Import question pool from JSON (bot owner only)")]
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
                var curFile = Directory.GetFiles($"import/{testName}")
                    .Where(f => f.Contains(testName) && f.Contains(".json"))
                    .FirstOrDefault();

                if (curFile == null)
                {
                    await FollowupAsync($"No import file found for {testName}!");
                    return;
                }

                var fileName = Path.GetFileNameWithoutExtension(curFile);
                DateTime startDate = DateTime.SpecifyKind(DateTime.Parse(fileName.Split('_')[1]), DateTimeKind.Utc);
                DateTime endDate = DateTime.SpecifyKind(DateTime.Parse(fileName.Split('_')[2]), DateTimeKind.Utc);

                var test = db.HamTest.Where(t => t.TestName == testName).FirstOrDefault();
                if (test == null)
                {
                    await db.AddAsync(new HamTest
                    {
                        TestName = testName,
                        TestDescription = testDesc,
                        FromDate = startDate,
                        ToDate = endDate
                    });
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Clear old test items
                    var figures = db.Figure.Where(f => f.Test.TestName == testName).ToList();
                    db.RemoveRange(figures);

                    var answers = db.Answer.Where(a => a.Question.Test.TestName == testName).ToList();
                    db.RemoveRange(answers);

                    var questions = db.Questions.Where(q => q.Test.TestName == testName).ToList();
                    db.RemoveRange(questions);

                    await db.SaveChangesAsync();

                    test.FromDate = startDate;
                    test.ToDate = endDate;
                    await db.SaveChangesAsync();
                }

                test = db.HamTest.Where(t => t.TestName == testName).FirstOrDefault();
                var questionData = QuestionIngest.FromJson(File.ReadAllText(curFile));

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

                    await db.AddAsync(newQuestion);
                    await db.SaveChangesAsync();

                    var curQuestion = db.Questions.Where(q => q.QuestionSection == item.QuestionId).FirstOrDefault();
                    var answerChar = char.Parse(item.AnswerKey.ToString());

                    foreach (var answer in item.PossibleAnswer)
                    {
                        var posAnswerText = answer.Substring(3);
                        var posAnswerChar = answer.Substring(0, 1);
                        bool isAnswer = answerChar == char.Parse(posAnswerChar);

                        await db.AddAsync(new Answer
                        {
                            Question = curQuestion,
                            AnswerText = posAnswerText,
                            IsAnswer = isAnswer
                        });
                    }
                    await db.SaveChangesAsync();
                }

                // Import figures
                var files = Directory.EnumerateFiles($"import/{testName}", $"{testName}_*.png");
                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        var contents = File.ReadAllBytes(file);
                        await db.AddAsync(new Figure
                        {
                            Test = test,
                            FigureName = file.Split('_')[1].Replace(".png", "").Trim(),
                            FigureImage = contents
                        });
                        await db.SaveChangesAsync();
                    }
                }

                await FollowupAsync($"Imported {testName} into the database!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing questions");
                await FollowupAsync($"Error importing {testName}: {ex.Message}");
            }
        }

        private async Task StartTest(LicenseType license, int numQuestions, int questionDelay, QuizMode mode)
        {
            using var db = _contextFactory.CreateDbContext();
            var testName = license.ToString().ToLower();
            var isPrivate = mode == QuizMode.Private;

            // Validate delay (15-120 seconds)
            var originalDelay = questionDelay;
            questionDelay = Math.Clamp(questionDelay, 15, 120);
            var delayWasClamped = originalDelay != questionDelay;

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
                var delayNote = delayWasClamped ? $" (delay adjusted to {questionDelay}s)" : "";
                await RespondAsync($"Starting {testName.ToUpper()} practice exam with {numQuestions} questions. Answer using buttons!{delayNote}", ephemeral: isPrivate);

                var quiz = await db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefaultAsync();
                try
                {
                    await startQuiz.StartGame(quiz, numQuestions, testName, questionDelay * 1000).ConfigureAwait(false);
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
