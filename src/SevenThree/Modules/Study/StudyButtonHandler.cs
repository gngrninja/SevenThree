using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Modules.Study
{
    public class StudyButtonHandler
    {
        private readonly ILogger<StudyButtonHandler> _logger;
        private readonly StudyService _studyService;

        public StudyButtonHandler(
            ILogger<StudyButtonHandler> logger,
            StudyService studyService)
        {
            _logger = logger;
            _studyService = studyService;
        }

        /// <summary>
        /// Handle flashcard navigation buttons
        /// </summary>
        public async Task HandleStudyButtonAsync(SocketMessageComponent component)
        {
            try
            {
                // Parse button ID: study:{action}:{sessionId}:{index}
                var parts = component.Data.CustomId.Split(':');
                if (parts.Length < 4)
                {
                    await component.RespondAsync("Invalid button.", ephemeral: true);
                    return;
                }

                var action = parts[1];
                var sessionId = parts[2];

                var session = _studyService.GetSession(sessionId);
                if (session == null)
                {
                    await component.RespondAsync("This study session has expired. Please start a new one.", ephemeral: true);
                    return;
                }

                // Verify user owns this session
                if (session.UserId != component.User.Id)
                {
                    await component.RespondAsync("This is not your study session.", ephemeral: true);
                    return;
                }

                switch (action)
                {
                    case "show":
                        session.ShowingAnswer = true;
                        break;
                    case "hide":
                        session.ShowingAnswer = false;
                        break;
                    case "next":
                        if (session.CurrentIndex < session.Questions.Count - 1)
                        {
                            session.CurrentIndex++;
                            session.ShowingAnswer = false;
                        }
                        break;
                    case "prev":
                        if (session.CurrentIndex > 0)
                        {
                            session.CurrentIndex--;
                            session.ShowingAnswer = false;
                        }
                        break;
                    case "done":
                        _studyService.RemoveSession(sessionId);
                        await component.UpdateAsync(m =>
                        {
                            m.Content = "Study session complete! Keep up the good work! 📚";
                            m.Embed = null;
                            m.Components = new ComponentBuilder().Build();
                        });
                        return;
                }

                _studyService.UpdateSession(session);

                // Update the message with new state
                var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
                var buttons = StudyEmbedBuilder.BuildFlashcardButtons(session);

                await component.UpdateAsync(m =>
                {
                    m.Embed = embed.Build();
                    m.Components = buttons;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling study button");
                try
                {
                    await component.RespondAsync("An error occurred.", ephemeral: true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Handle retry mode answer buttons
        /// </summary>
        public async Task HandleRetryButtonAsync(SocketMessageComponent component)
        {
            try
            {
                // Parse button ID: studyretry:{sessionId}:{answer}:{answerId}
                var parts = component.Data.CustomId.Split(':');
                if (parts.Length < 4)
                {
                    await component.RespondAsync("Invalid button.", ephemeral: true);
                    return;
                }

                var sessionId = parts[1];
                var answerKey = parts[2];
                var answerIdStr = parts[3];

                var session = _studyService.GetSession(sessionId);
                if (session == null)
                {
                    await component.RespondAsync("This study session has expired. Please start a new one.", ephemeral: true);
                    return;
                }

                // Verify user owns this session
                if (session.UserId != component.User.Id)
                {
                    await component.RespondAsync("This is not your study session.", ephemeral: true);
                    return;
                }

                if (answerKey == "stop")
                {
                    _studyService.RemoveSession(sessionId);
                    await component.UpdateAsync(m =>
                    {
                        m.Content = "Retry session ended. Keep practicing! 📚";
                        m.Embed = null;
                        m.Components = new ComponentBuilder().Build();
                    });
                    return;
                }

                var question = session.Questions[session.CurrentIndex];
                bool isCorrect = false;
                bool isSkip = answerKey == "skip";

                if (!isSkip && int.TryParse(answerIdStr, out var answerId))
                {
                    var answers = await _studyService.GetAnswersForQuestionAsync(question.QuestionId);
                    isCorrect = answers.FirstOrDefault(a => a.AnswerId == answerId)?.IsCorrect ?? false;
                }

                // Build result message
                var resultEmbed = new EmbedBuilder();
                if (isSkip)
                {
                    resultEmbed.Title = "⏭️ Skipped";
                    resultEmbed.WithColor(StudyConstants.COLOR_STUDY);
                }
                else if (isCorrect)
                {
                    resultEmbed.Title = "✅ Correct!";
                    resultEmbed.WithColor(QuizConstants.COLOR_CORRECT);
                }
                else
                {
                    resultEmbed.Title = "❌ Incorrect";
                    resultEmbed.WithColor(QuizConstants.COLOR_INCORRECT);
                }

                resultEmbed.AddField("Correct Answer", question.CorrectAnswer ?? "Not available");
                resultEmbed.AddField("Question", question.QuestionText ?? "Not available");

                // Check if there are more questions
                bool hasMore = session.CurrentIndex < session.Questions.Count - 1;

                if (hasMore)
                {
                    session.CurrentIndex++;
                    _studyService.UpdateSession(session);

                    // Show result briefly, then next question
                    var nextQuestion = session.Questions[session.CurrentIndex];
                    var nextAnswers = await _studyService.GetAnswersForQuestionAsync(nextQuestion.QuestionId);
                    var nextShuffled = nextAnswers.OrderBy(_ => Random.Shared.Next()).ToList();
                    var nextEmbed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, nextShuffled);
                    var nextButtons = StudyEmbedBuilder.BuildRetryAnswerButtons(session, nextShuffled);

                    await component.UpdateAsync(m =>
                    {
                        m.Content = isCorrect ? "✅ Correct! Here's the next question:" :
                                   isSkip ? "Skipped. Here's the next question:" :
                                   $"❌ The answer was: **{question.CorrectAnswer}**\nHere's the next question:";
                        m.Embed = nextEmbed.Build();
                        m.Components = nextButtons;
                    });
                }
                else
                {
                    // Session complete
                    _studyService.RemoveSession(sessionId);
                    await component.UpdateAsync(m =>
                    {
                        m.Content = isCorrect ? "✅ Correct! You've completed all the retry questions!" :
                                   $"❌ The answer was: **{question.CorrectAnswer}**\n\nYou've completed all the retry questions!";
                        m.Embed = null;
                        m.Components = new ComponentBuilder().Build();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling retry button");
                try
                {
                    await component.RespondAsync("An error occurred.", ephemeral: true);
                }
                catch { }
            }
        }
    }
}
