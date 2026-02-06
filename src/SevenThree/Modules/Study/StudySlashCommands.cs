using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Modules.Study
{
    [Group("study", "Review and study questions you've missed")]
    public class StudySlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<StudySlashCommands> _logger;
        private readonly StudyService _studyService;

        public StudySlashCommands(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<StudySlashCommands>>();
            _studyService = services.GetRequiredService<StudyService>();
        }

        [SlashCommand("missed", "Review questions you answered incorrectly (flashcard mode)")]
        public async Task StudyMissed(
            [Summary("scope", "Which quizzes to review")] StudyScope scope = StudyScope.Last)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var missed = await _studyService.GetMissedQuestionsAsync(Context.User.Id, scope);

                if (missed.Count == 0)
                {
                    var scopeText = scope == StudyScope.Last ? "your last quiz" : "any quiz";
                    await FollowupAsync($"No missed questions found in {scopeText}. Great job!", ephemeral: true);
                    return;
                }

                // Create a study session
                var sessionId = _studyService.CreateSession(Context.User.Id, missed);
                var session = _studyService.GetSession(sessionId);

                // Show first question
                var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
                var buttons = StudyEmbedBuilder.BuildFlashcardButtons(session);

                await FollowupAsync(embed: embed.Build(), components: buttons, ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StudyMissed");
                await FollowupAsync("An error occurred while loading missed questions.", ephemeral: true);
            }
        }

        [SlashCommand("weak", "Study questions you've missed multiple times")]
        public async Task StudyWeak()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var weak = await _studyService.GetWeakQuestionsAsync(Context.User.Id);

                if (weak.Count == 0)
                {
                    await FollowupAsync($"No weak areas found! You need to miss a question at least {StudyConstants.WEAK_THRESHOLD} times for it to appear here.", ephemeral: true);
                    return;
                }

                // Create a study session
                var sessionId = _studyService.CreateSession(Context.User.Id, weak);
                var session = _studyService.GetSession(sessionId);

                // Show first question
                var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
                var buttons = StudyEmbedBuilder.BuildFlashcardButtons(session);

                await FollowupAsync(
                    $"Found {weak.Count} questions you've struggled with. Let's review!",
                    embed: embed.Build(),
                    components: buttons,
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StudyWeak");
                await FollowupAsync("An error occurred while loading weak questions.", ephemeral: true);
            }
        }

        [SlashCommand("stats", "View your performance statistics by topic")]
        public async Task StudyStats()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var stats = await _studyService.GetUserStatsAsync(Context.User.Id);

                if (stats.TotalAnswered == 0)
                {
                    await FollowupAsync("No quiz history found. Take a practice exam first!", ephemeral: true);
                    return;
                }

                var embed = BuildStatsEmbed(stats);
                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StudyStats");
                await FollowupAsync("An error occurred while loading stats.", ephemeral: true);
            }
        }

        [SlashCommand("retry", "Re-take questions you missed (quiz mode)")]
        public async Task StudyRetry(
            [Summary("scope", "Which quizzes to retry")] StudyScope scope = StudyScope.Last)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var missed = await _studyService.GetMissedQuestionsAsync(Context.User.Id, scope);

                if (missed.Count == 0)
                {
                    var scopeText = scope == StudyScope.Last ? "your last quiz" : "any quiz";
                    await FollowupAsync($"No missed questions found in {scopeText}. Great job!", ephemeral: true);
                    return;
                }

                // Create a study session for retry mode
                var sessionId = _studyService.CreateSession(Context.User.Id, missed);
                var session = _studyService.GetSession(sessionId);

                // Show first question with answer buttons
                var answers = await _studyService.GetAnswersForQuestionAsync(
                    session.Questions[session.CurrentIndex].QuestionId);
                var shuffled = answers.OrderBy(_ => Random.Shared.Next()).ToList();
                var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, shuffled);
                var buttons = StudyEmbedBuilder.BuildRetryAnswerButtons(session, shuffled);

                await FollowupAsync(
                    $"Retrying {missed.Count} missed questions. Good luck!",
                    embed: embed.Build(),
                    components: buttons,
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StudyRetry");
                await FollowupAsync("An error occurred while loading questions.", ephemeral: true);
            }
        }

        #region Embed Builders

        private EmbedBuilder BuildStatsEmbed(UserStudyStats stats)
        {
            var embed = new EmbedBuilder();
            embed.Title = "Your Study Statistics";
            embed.WithColor(StudyConstants.COLOR_STATS);
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            var passEmoji = stats.OverallPercent >= 74 ? "✅" : "📚";
            embed.Description = $"{passEmoji} **Overall: {stats.OverallPercent}%** ({stats.TotalCorrect}/{stats.TotalAnswered} correct)";

            // Group by test type
            var byTest = stats.SubelementStats.GroupBy(s => s.TestName?.ToUpper() ?? "UNKNOWN");

            foreach (var testGroup in byTest)
            {
                var sb = new StringBuilder();
                var sortedStats = testGroup.OrderBy(s => s.PercentCorrect).Take(10);

                foreach (var stat in sortedStats)
                {
                    var emoji = stat.PercentCorrect >= 74 ? "✅" : (stat.PercentCorrect >= 50 ? "⚠️" : "❌");
                    sb.AppendLine($"{emoji} **{stat.SubelementName}**: {stat.PercentCorrect}% ({stat.TotalCorrect}/{stat.TotalAnswered})");
                }

                if (sb.Length > 0)
                {
                    embed.AddField($"📊 {testGroup.Key} (weakest first)", sb.ToString());
                }
            }

            embed.WithFooter("74% is the passing score for FCC exams");

            return embed;
        }

        #endregion
    }
}
