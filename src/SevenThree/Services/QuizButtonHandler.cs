using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Database;
using SevenThree.Modules;

namespace SevenThree.Services
{
    /// <summary>
    /// Handles button interactions for quiz answers.
    /// Button custom ID format: {BUTTON_PREFIX}:{sessionId}:{answer}
    /// Example: quiz:123456789:A
    /// </summary>
    public class QuizButtonHandler
    {
        private readonly HamTestService _hamTestService;
        private readonly ILogger<QuizButtonHandler> _logger;

        public QuizButtonHandler(
            HamTestService hamTestService,
            ILogger<QuizButtonHandler> logger)
        {
            _hamTestService = hamTestService;
            _logger = logger;
        }

        public async Task HandleStopButtonAsync(SocketMessageComponent component)
        {
            // Parse custom ID: {STOP_BUTTON_PREFIX}:{sessionId}
            var parts = component.Data.CustomId.Split(':');
            if (parts.Length < 2)
            {
                await component.RespondAsync("Invalid stop button.", ephemeral: true);
                return;
            }

            if (!ulong.TryParse(parts[1], out var sessionId))
            {
                await component.RespondAsync("Invalid quiz session.", ephemeral: true);
                return;
            }

            // Find the active quiz session
            if (!_hamTestService.RunningTests.TryGetValue(sessionId, out var quizUtil))
            {
                await component.RespondAsync("This quiz has already ended.", ephemeral: true);
                return;
            }

            // Verify the user can stop the quiz (quiz owner or has KickMembers permission in guilds)
            var userId = component.User.Id;
            var canStop = userId == quizUtil.Quiz.StartedById;

            if (!canStop && component.GuildId.HasValue)
            {
                var guildUser = component.User as Discord.IGuildUser;
                canStop = guildUser?.GuildPermissions.KickMembers ?? false;
            }

            if (!canStop)
            {
                await component.RespondAsync("Only the quiz owner can stop this quiz.", ephemeral: true);
                return;
            }

            _logger.LogInformation("Quiz stopped via button: User {UserId} stopped session {SessionId}", userId, sessionId);

            // Stop the quiz
            if (_hamTestService.RunningTests.TryRemove(sessionId, out var trivia))
            {
                await trivia.StopQuiz().ConfigureAwait(false);
                await component.RespondAsync("Quiz stopped!", ephemeral: quizUtil.Mode == Models.QuizMode.Private);
            }
            else
            {
                await component.RespondAsync("Quiz has already ended.", ephemeral: true);
            }
        }

        public async Task HandleQuizButtonAsync(SocketMessageComponent component)
        {
            // Parse custom ID: {BUTTON_PREFIX}:{sessionId}:{answer}
            var parts = component.Data.CustomId.Split(':');
            if (parts.Length < 3)
            {
                await component.RespondAsync("Invalid quiz button.", ephemeral: true);
                return;
            }

            if (!ulong.TryParse(parts[1], out var sessionId))
            {
                await component.RespondAsync("Invalid quiz session.", ephemeral: true);
                return;
            }

            var answer = parts[2]; // A, B, C, or D

            // Find the active quiz session
            if (!_hamTestService.RunningTests.TryGetValue(sessionId, out var quizUtil))
            {
                await component.RespondAsync("This quiz has ended or expired.", ephemeral: true);
                return;
            }

            // Verify this is the user who started the quiz (for private mode)
            // or allow anyone to answer (for public mode)
            var userId = component.User.Id;

            _logger.LogInformation("Quiz button pressed: User {UserId} answered {Answer} for session {SessionId}",
                userId, answer, sessionId);

            // Defer the response while we process (if not already responded)
            if (!component.HasResponded)
            {
                await component.DeferAsync();
            }

            try
            {
                // Process the answer through QuizUtil
                await quizUtil.ProcessButtonAnswerAsync(component, answer, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing quiz button answer");
                await component.FollowupAsync("An error occurred processing your answer.", ephemeral: true);
            }
        }
    }
}
