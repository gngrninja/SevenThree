using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using SevenThree.Models;
using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.IO;
using SevenThree.Constants;
using SevenThree.Services;

namespace SevenThree.Modules
{
    public class QuizUtil
    {
        private readonly SemaphoreSlim _guessLock = new SemaphoreSlim(1, 1);
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;
        private readonly ILogger _logger;
        private readonly IGuild _guild;
        private readonly ITextChannel _channel;
        private readonly HamTestService _hamTestService;
        private readonly IUser _user;
        private readonly bool _isDmTest;
        private readonly QuizHelper _quizHelper;
        private readonly List<IMessage> _messages;
        private readonly List<Questions> _questionsAsked;

        private List<Questions> _questions;
        private int _totalQuestions;
        private int _questionDelay;
        private QuizMode _quizMode = QuizMode.Private;
        private IUserMessage _currentButtonMessage;
        private CancellationTokenSource _tokenSource;

        public IMessage CurMessage { get; private set; }
        public bool ShouldStopTest { get; private set; }
        public Questions CurrentQuestion { get; private set; }
        public List<Tuple<char, Answer>> Answers { get; private set; }
        public Quiz Quiz { get; private set; }
        public QuizMode Mode => _quizMode;
        public bool IsActive { get; set; }
        public ulong Id { get; set; }
        public CancellationTokenSource TokenSource
        {
            get => _tokenSource;
            set => _tokenSource = value;
        }
        
        public QuizUtil(
            IGuild guild,
            ITextChannel channel,
            IServiceProvider services,
            ulong id)
        {
            _logger = services.GetRequiredService<ILogger<QuizUtil>>();
            _contextFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            _channel = channel;
            _isDmTest = false;
            Id = id;
            _questionsAsked = new List<Questions>();
            _questionDelay = QuizConstants.DEFAULT_QUESTION_DELAY_MS;
            _messages = new List<IMessage>();
            _quizHelper = new QuizHelper();
        }

        public QuizUtil(
            IGuild guild,
            IUser user,
            IServiceProvider services,
            ulong id)
        {
            _logger = services.GetRequiredService<ILogger<QuizUtil>>();
            _contextFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            _user = user;
            _isDmTest = true;
            Id = id;
            _questionsAsked = new List<Questions>();
            _questionDelay = QuizConstants.DEFAULT_QUESTION_DELAY_MS;
            _messages = new List<IMessage>();
            _quizHelper = new QuizHelper();
        }      

        public async Task StartGame(Quiz quiz, int numQuestions, int testId, int questionDelay)
        {
            _questionDelay = questionDelay;
            Quiz = quiz;
            _questions = await GetRandomQuestions(numQuestions, testId, figuresOnly: false);
            _totalQuestions = _questions.Count;

            while (!ShouldStopTest)
            {
                if (_questions.Count == 0)
                {
                    await StopQuiz().ConfigureAwait(false);
                    return;
                }

                _tokenSource = new CancellationTokenSource();
                CurrentQuestion = _questions[Random.Shared.Next(_questions.Count)];
                _questions.Remove(CurrentQuestion);

                try
                {
                    _questionsAsked.Add(CurrentQuestion);
                    var embed = GetQuestionEmbed();
                    await SetupAnswers(embed);

                    // Await the question send to avoid race condition
                    bool hasFigure = !string.IsNullOrEmpty(CurrentQuestion.FigureName);
                    await SendQuestionWithButtonsAsync(embed, hasFigure);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending question");
                    await Task.Delay(5000).ConfigureAwait(false);
                    continue;
                }

                bool wasAnswered = false;
                try
                {
                    IsActive = true;
                    try
                    {
                        await Task.Delay(_questionDelay, _tokenSource.Token).ConfigureAwait(false);
                        // If we reach here, the delay completed naturally (timeout, no answer)
                    }
                    catch (TaskCanceledException)
                    {
                        // Answer received via button click - expected behavior
                        wasAnswered = true;
                    }
                }
                finally
                {
                    IsActive = false;

                    // If question timed out (not answered), disable buttons showing correct answer
                    if (!wasAnswered && !ShouldStopTest && _currentButtonMessage != null)
                    {
                        try
                        {
                            var timedOutButtons = BuildTimedOutButtons();
                            await _currentButtonMessage.ModifyAsync(m =>
                            {
                                m.Components = timedOutButtons;
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to disable buttons on timed-out question");
                        }
                    }

                    if (!ShouldStopTest && _quizMode == QuizMode.Public)
                    {
                        await SendQuestionResultsAsync();
                    }
                }

                await Task.Delay(QuizConstants.POST_ANSWER_DELAY_MS).ConfigureAwait(false);
            }
        }

        private async Task SendQuestionResultsAsync()
        {
            var usersWon = await GetUsersFromQuestion(isCorrect: true);
            var usersLost = await GetUsersFromQuestion(isCorrect: false);
            var embed = await GetQuestionResultsEmbedAsync(usersWon, usersLost);
            await SendMessageAsync(embed);
        }

        private async Task<EmbedBuilder> GetQuestionResultsEmbedAsync(List<UserAnswer> usersWon, List<UserAnswer> usersLost)
        {
            var sb = new StringBuilder();
            var embed = new EmbedBuilder();
            embed.Title = $"Question [{CurrentQuestion.QuestionSection}] Results";
            embed.AddField("Question:", $"**{CurrentQuestion.QuestionText ?? "Question not available"}**");

            var correctAnswer = Answers.FirstOrDefault(a => a.Item2.IsAnswer);
            var answerValue = correctAnswer != null
                ? $"**{correctAnswer.Item1}**. *{correctAnswer.Item2.AnswerText ?? "Answer text not available"}*"
                : "Correct answer not found";
            embed.AddField("Answer:", answerValue);
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            if (usersWon.Count > 0 && usersLost.Count == 0)
            {
                sb.AppendLine("**__Answered Correctly:__**");
                foreach (var user in usersWon)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
                embed.WithColor(QuizConstants.COLOR_CORRECT);
            }
            else if (usersWon.Count == 0 && usersLost.Count == 0)
            {
                embed.WithColor(QuizConstants.COLOR_INCORRECT);
                sb.AppendLine("**__Nobody answered the question =(__**");
            }
            else if (usersLost.Count > 0 && usersWon.Count == 0)
            {
                embed.WithColor(QuizConstants.COLOR_INCORRECT);
                sb.AppendLine("**__Answered Incorrectly:__**");
                foreach (var user in usersLost)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
            }
            else
            {
                embed.WithColor(QuizConstants.COLOR_MIXED);
                sb.AppendLine("**__Answered Correctly:__**");
                foreach (var user in usersWon)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
                sb.AppendLine("**__Answered Incorrectly:__**");
                foreach (var user in usersLost)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
            }

            embed.Description = sb.ToString();
            var usersCorrect = await GetTopUsers();

            if (usersCorrect.Count > 0)
            {
                var leader = usersCorrect[0];
                var numCorrect = leader.Item2;

                if (!_isDmTest)
                {
                    var guildUser = await _guild.GetUserAsync((ulong)leader.Item1);
                    embed.WithFooter(new EmbedFooterBuilder
                    {
                        Text = $"[{guildUser?.Username ?? "Unknown"}] is currently in the lead with [{numCorrect}] correct answers!",
                        IconUrl = guildUser?.GetAvatarUrl() ?? guildUser?.GetDefaultAvatarUrl()
                    });
                }
                else
                {
                    embed.WithFooter(new EmbedFooterBuilder
                    {
                        Text = $"You have [{numCorrect}] right so far, [{_user.Username}]!",
                        IconUrl = _user.GetAvatarUrl() ?? _user.GetDefaultAvatarUrl()
                    });
                }
            }
            else
            {
                embed.WithFooter(new EmbedFooterBuilder
                {
                    Text = "Nobody has any correct answers!",
                    IconUrl = QuizConstants.BOT_THUMBNAIL_URL
                });
            }

            return embed;
        }

        private async Task SetupAnswers(EmbedBuilder embed)
        {
            var answerOptions = new List<Tuple<char, Answer>>();
            var letters = new List<char> { 'A', 'B', 'C', 'D' };
            var usedIndices = new HashSet<int>();

            using var db = _contextFactory.CreateDbContext();
            var answers = await db.Answer
                .Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId)
                .ToListAsync();

            // Shuffle answers by randomly assigning to letters
            for (int i = 0; i < answers.Count && i < letters.Count; i++)
            {
                int randIndex;
                do
                {
                    randIndex = Random.Shared.Next(answers.Count);
                } while (usedIndices.Contains(randIndex));

                usedIndices.Add(randIndex);
                answerOptions.Add(Tuple.Create(letters[i], answers[randIndex]));
            }

            foreach (var (letter, answerData) in answerOptions)
            {
                embed.AddField($"{letter}.", answerData.AnswerText ?? "No answer text", inline: true);
            }

            Answers = answerOptions;
        }

        private EmbedBuilder GetQuestionEmbed()
        {
            var embed = new EmbedBuilder();

            embed.WithAuthor(new EmbedAuthorBuilder
            {
                Name = $"Test started by [{Quiz.StartedByName ?? "Unknown"}]",
                IconUrl = Quiz.StartedByIconUrl
            });

            embed.Title = $"Question: [{CurrentQuestion.QuestionSection}] From Test: [{CurrentQuestion.Test.TestName}]!";

            var color = CurrentQuestion.Test.TestName switch
            {
                "tech" => QuizConstants.COLOR_TECH,
                "general" => QuizConstants.COLOR_GENERAL,
                "extra" => QuizConstants.COLOR_EXTRA,
                _ => QuizConstants.COLOR_CORRECT
            };
            embed.WithColor(color);

            if (!string.IsNullOrWhiteSpace(CurrentQuestion.FccPart))
            {
                embed.AddField("FCC Part", CurrentQuestion.FccPart);
            }

            embed.AddField(
                $"Subelement [**{CurrentQuestion.SubelementName ?? "Unknown"}**]",
                CurrentQuestion.SubelementDesc ?? "No description available");

            embed.AddField("Question:", $"**{CurrentQuestion.QuestionText ?? "Question not available"}**");

            embed.WithFooter($"Question ({_questionsAsked.Count} / {_totalQuestions})... [{_questionDelay / 1000}] seconds to answer!");
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;

            return embed;
        }

        /// <summary>
        /// Sets the quiz mode (Private or Public)
        /// </summary>
        public void SetQuizMode(QuizMode mode)
        {
            _quizMode = mode;
        }

        /// <summary>
        /// Process a button answer click from the QuizButtonHandler
        /// </summary>
        public async Task ProcessButtonAnswerAsync(SocketMessageComponent component, string answerLetter, ulong userId)
        {
            await _guessLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsActive || CurrentQuestion == null)
                {
                    await component.FollowupAsync("This question is no longer active.", ephemeral: true);
                    return;
                }

                // For private mode, verify it's the quiz owner
                if (_quizMode == QuizMode.Private && userId != Quiz.StartedById)
                {
                    await component.FollowupAsync("This is not your quiz.", ephemeral: true);
                    return;
                }

                using var db = _contextFactory.CreateDbContext();

                // Check if user already answered this question
                var userAnswered = await db.UserAnswer
                    .Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId && a.UserId == (long)userId && a.Quiz.QuizId == Quiz.QuizId)
                    .FirstOrDefaultAsync();

                if (userAnswered != null)
                {
                    await component.FollowupAsync("You already answered this question!", ephemeral: true);
                    return;
                }

                // Find the answer by letter
                var answerChar = answerLetter.ToUpper()[0];
                var selectedAnswer = Answers.FirstOrDefault(a => a.Item1 == answerChar);

                if (selectedAnswer == null)
                {
                    await component.FollowupAsync("Invalid answer selection.", ephemeral: true);
                    return;
                }

                var user = component.User;
                var isCorrect = selectedAnswer.Item2.IsAnswer;

                // Attach entities from other context for FK resolution
                db.Attach(CurrentQuestion);
                db.Attach(Quiz);

                // Save the answer
                db.UserAnswer.Add(new UserAnswer
                {
                    UserId = (long)user.Id,
                    UserName = user.Username,
                    Question = CurrentQuestion,
                    AnswerText = answerChar.ToString(),
                    Quiz = Quiz,
                    IsAnswer = isCorrect
                });
                await db.SaveChangesAsync();

                // Build result embed
                var resultEmbed = BuildAnswerResultEmbed(isCorrect, answerChar, selectedAnswer.Item2);

                // Disable buttons on the original message
                var disabledButtons = BuildDisabledAnswerButtons(answerChar, isCorrect);

                // Update the message with disabled buttons and result
                if (_currentButtonMessage != null)
                {
                    await _currentButtonMessage.ModifyAsync(m =>
                    {
                        m.Components = disabledButtons;
                    });
                }

                // Send result as followup (always ephemeral so only the answerer sees their result)
                await component.FollowupAsync(embed: resultEmbed.Build(), ephemeral: true);

                // Cancel the timeout to advance to next question
                _tokenSource?.Cancel();
            }
            finally
            {
                _guessLock.Release();
            }
        }

        /// <summary>
        /// Build the answer buttons for a question
        /// </summary>
        public MessageComponent BuildAnswerButtons()
        {
            var builder = new ComponentBuilder();

            foreach (var answer in Answers)
            {
                var customId = $"{QuizConstants.BUTTON_PREFIX}:{Id}:{answer.Item1}";
                builder.WithButton(
                    label: answer.Item1.ToString(),
                    customId: customId,
                    style: ButtonStyle.Primary,
                    row: 0
                );
            }

            // Add stop button on row 1
            builder.WithButton(
                label: "Stop Quiz",
                customId: $"{QuizConstants.STOP_BUTTON_PREFIX}:{Id}",
                style: ButtonStyle.Danger,
                row: 1
            );

            return builder.Build();
        }

        /// <summary>
        /// Build disabled buttons for a timed-out question (no answer selected, show correct answer)
        /// </summary>
        private MessageComponent BuildTimedOutButtons()
        {
            var builder = new ComponentBuilder();

            foreach (var answer in Answers)
            {
                var style = answer.Item2.IsAnswer ? ButtonStyle.Success : ButtonStyle.Secondary;

                builder.WithButton(
                    label: answer.Item1.ToString(),
                    customId: $"{QuizConstants.BUTTON_PREFIX}:timeout:{answer.Item1}",
                    style: style,
                    disabled: true,
                    row: 0
                );
            }

            // Add disabled stop button on row 1
            builder.WithButton(
                label: "Stop Quiz",
                customId: $"{QuizConstants.STOP_BUTTON_PREFIX}:timeout",
                style: ButtonStyle.Secondary,
                disabled: true,
                row: 1
            );

            return builder.Build();
        }

        /// <summary>
        /// Build disabled buttons showing which answer was selected
        /// </summary>
        private MessageComponent BuildDisabledAnswerButtons(char selectedAnswer, bool wasCorrect)
        {
            var builder = new ComponentBuilder();
            var correctAnswer = Answers.FirstOrDefault(a => a.Item2.IsAnswer);

            foreach (var answer in Answers)
            {
                var style = ButtonStyle.Secondary; // Default grey
                if (answer.Item1 == selectedAnswer)
                {
                    style = wasCorrect ? ButtonStyle.Success : ButtonStyle.Danger;
                }
                else if (answer.Item2.IsAnswer)
                {
                    style = ButtonStyle.Success; // Show correct answer in green
                }

                builder.WithButton(
                    label: answer.Item1.ToString(),
                    customId: $"{QuizConstants.BUTTON_PREFIX}:disabled:{answer.Item1}",
                    style: style,
                    disabled: true,
                    row: 0
                );
            }

            // Add disabled stop button on row 1
            builder.WithButton(
                label: "Stop Quiz",
                customId: $"{QuizConstants.STOP_BUTTON_PREFIX}:disabled",
                style: ButtonStyle.Secondary,
                disabled: true,
                row: 1
            );

            return builder.Build();
        }

        /// <summary>
        /// Build an embed showing if the answer was correct
        /// </summary>
        private EmbedBuilder BuildAnswerResultEmbed(bool isCorrect, char selectedAnswer, Answer selectedAnswerData)
        {
            var embed = new EmbedBuilder();
            var correctAnswer = Answers.FirstOrDefault(a => a.Item2.IsAnswer);

            if (isCorrect)
            {
                embed.WithColor(QuizConstants.COLOR_CORRECT);
                embed.Title = "Correct!";
                embed.Description = $"**{selectedAnswer}**. {selectedAnswerData.AnswerText ?? "Answer text not available"}";
            }
            else
            {
                embed.WithColor(QuizConstants.COLOR_INCORRECT);
                embed.Title = "Incorrect";
                embed.Description = $"You answered: **{selectedAnswer}**\n\n" +
                    $"Correct answer: **{correctAnswer?.Item1}**. {correctAnswer?.Item2.AnswerText ?? "Answer text not available"}";
            }

            embed.WithFooter($"Question {_questionsAsked.Count} of {_totalQuestions}");

            return embed;
        }

        /// <summary>
        /// Store the current button message for later modification
        /// </summary>
        public void SetCurrentButtonMessage(IUserMessage message)
        {
            _currentButtonMessage = message;
        }

        /// <summary>
        /// Get figure data for the current question if it has one
        /// </summary>
        private async Task<(string fileName, byte[] data)?> GetFigureDataAsync(string figureName)
        {
            if (string.IsNullOrEmpty(figureName))
                return null;

            using var db = _contextFactory.CreateDbContext();
            var figure = await db.Figure
                .Include(f => f.Test)
                .Where(f => f.FigureName == figureName)
                .FirstOrDefaultAsync();

            if (figure == null)
                return null;

            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
            return (fileName, figure.FigureImage);
        }

        /// <summary>
        /// Send a message using a temporary file for the figure attachment
        /// </summary>
        private async Task<IUserMessage> SendWithFigureAsync(
            EmbedBuilder embed,
            string fileName,
            byte[] figureData,
            MessageComponent components = null)
        {
            // Write temp file with unique identifier to prevent collisions
            var uniqueFileName = $"{Guid.NewGuid():N}_{fileName}";
            var tempPath = Path.Combine(Path.GetTempPath(), uniqueFileName);
            try
            {
                await File.WriteAllBytesAsync(tempPath, figureData);
                embed.WithImageUrl($"attachment://{fileName}");

                if (_channel != null)
                {
                    return await _channel.SendFileAsync(tempPath, embed: embed.Build(), components: components);
                }
                else if (_user != null)
                {
                    return await _user.SendFileAsync(tempPath, embed: embed.Build(), components: components);
                }
                return null;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to delete temp file {TempPath}", tempPath);
                    }
                }
            }
        }

        /// <summary>
        /// Send a simple embed message (no figure)
        /// </summary>
        private async Task<IUserMessage> SendMessageAsync(EmbedBuilder embed, MessageComponent components = null)
        {
            if (_channel != null)
            {
                var msg = await _channel.SendMessageAsync(embed: embed.Build(), components: components);
                _messages.Add(msg);
                return msg;
            }
            else if (_user != null)
            {
                return await _user.SendMessageAsync(embed: embed.Build(), components: components);
            }
            return null;
        }

        /// <summary>
        /// Send a question with button components
        /// </summary>
        private async Task SendQuestionWithButtonsAsync(EmbedBuilder embed, bool hasFigure)
        {
            var buttons = BuildAnswerButtons();
            IUserMessage message;

            if (hasFigure)
            {
                var figureData = await GetFigureDataAsync(CurrentQuestion.FigureName);
                if (figureData.HasValue)
                {
                    message = await SendWithFigureAsync(embed, figureData.Value.fileName, figureData.Value.data, buttons);
                }
                else
                {
                    // Fallback if figure not found
                    message = await SendMessageAsync(embed, buttons);
                }
            }
            else
            {
                message = await SendMessageAsync(embed, buttons);
            }

            if (message != null)
            {
                CurMessage = message;
                SetCurrentButtonMessage(message);
                if (_channel != null)
                {
                    _messages.Add(message);
                }
            }
        }

        private async Task<List<Tuple<ulong, int>>> GetTopUsers()
        {
            using var db = _contextFactory.CreateDbContext();
            // Fetch grouped data first, then project to tuples in memory
            // (EF Core can't translate Tuple.Create to SQL)
            var grouped = await db.UserAnswer
                .Where(u => u.Quiz.QuizId == Quiz.QuizId && u.IsAnswer)
                .GroupBy(u => u.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return grouped.Select(x => Tuple.Create((ulong)x.UserId, x.Count)).ToList();
        }

        /// <summary>
        /// Gets user answers for the current question, filtered by correctness.
        /// </summary>
        private async Task<List<UserAnswer>> GetUsersFromQuestion(bool isCorrect)
        {
            using var db = _contextFactory.CreateDbContext();
            return await db.UserAnswer
                .Where(u => u.Quiz.QuizId == Quiz.QuizId
                         && u.Question.QuestionId == CurrentQuestion.QuestionId
                         && u.IsAnswer == isCorrect)
                .ToListAsync();
        } 

        internal async Task StopQuiz()
        {
            _hamTestService.RunningTests.TryRemove(Id, out _);
            ShouldStopTest = true;

            using var db = _contextFactory.CreateDbContext();

            // Find quiz by QuizId if available, otherwise by ServerId (handles startup failure)
            Database.Quiz quiz;
            if (Quiz != null)
            {
                quiz = await db.Quiz.Where(q => q.QuizId == Quiz.QuizId).FirstOrDefaultAsync();
            }
            else
            {
                // Fallback: find active quiz by ServerId (startup failure case - Quiz was never set)
                quiz = await db.Quiz.Where(q => q.ServerId == Id && q.IsActive).FirstOrDefaultAsync();
            }

            if (quiz != null)
            {
                quiz.TimeEnded = DateTime.UtcNow;
                quiz.IsActive = false;
                await db.SaveChangesAsync();
            }

            // If Quiz was never set (startup failure), skip the results embed
            if (Quiz == null)
            {
                return;
            }

            var embed = new EmbedBuilder();
            var testName = CurrentQuestion?.Test?.TestName ?? "Unknown";
            var fromDate = CurrentQuestion?.Test?.FromDate.ToShortDateString() ?? "?";
            var toDate = CurrentQuestion?.Test?.ToDate.ToShortDateString() ?? "?";

            embed.Title = $"[{testName}] [{fromDate} -> {toDate}] Test Results!";
            embed.WithColor(QuizConstants.COLOR_CORRECT);
            embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;
            embed.WithFooter(new EmbedFooterBuilder
            {
                Text = "SevenThree, your local ham radio Discord bot!",
                IconUrl = QuizConstants.BOT_THUMBNAIL_URL
            });

            var sb = new StringBuilder();
            sb.AppendLine($"Number of questions -> [**{_totalQuestions}**]");

            var users = await db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            var userResults = await GetTopUsers();

            if (userResults.Count > 0 && _totalQuestions > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**__Leaderboard:__**");

                for (int i = 0; i < userResults.Count; i++)
                {
                    var user = userResults[i];
                    var percentage = ((decimal)user.Item2 / _totalQuestions) * 100;
                    var passFailEmoji = _quizHelper.GetPassFail(percentage);
                    var userName = users.FirstOrDefault(u => (ulong)u.UserId == user.Item1)?.UserName ?? "Unknown";

                    sb.AppendLine($"{_quizHelper.GetNumberEmojiFromInt(i + 1)} [**{userName}**] with [**{user.Item2}**] [{passFailEmoji}] ({Math.Round(percentage, 0)}%)");
                }

                sb.AppendLine();
                sb.AppendLine("Thanks for taking the test! Happy learning.");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("Nobody scored!");
            }

            embed.Description = sb.ToString();
            await SendMessageAsync(embed);
            await ClearChannel();
        }

        private async Task<List<Questions>> GetRandomQuestions(int numQuestions, int testId, bool figuresOnly = false)
        {
            using var db = _contextFactory.CreateDbContext();

            var query = db.Questions.Include(q => q.Test)
                .Where(q => q.Test.TestId == testId && !q.IsArchived);
            if (figuresOnly)
            {
                query = query.Where(q => q.FigureName != null);
            }

            var questions = await query.ToListAsync();

            if (questions.Count == 0)
            {
                _logger.LogWarning("No questions found for test ID {TestId}", testId);
                return new List<Questions>();
            }

            // Clamp to max and available questions to prevent infinite loop
            numQuestions = Math.Min(numQuestions, QuizConstants.MAX_QUESTIONS);
            numQuestions = Math.Min(numQuestions, questions.Count);

            // Use Fisher-Yates shuffle for efficient random selection
            for (int i = questions.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (questions[i], questions[j]) = (questions[j], questions[i]);
            }

            return questions.Take(numQuestions).ToList();
        }

        private async Task ClearChannel()
        {
            if (_guild != null)
            {
                using var db = _contextFactory.CreateDbContext();
                var settings = await db.QuizSettings.Where(s => s.DiscordGuildId == _guild.Id).FirstOrDefaultAsync();
                if (settings != null && settings.ClearAfterTaken)
                {
                    if (_messages.Count > 100)
                    {
                        do 
                        {
                            if (_messages.Count > 100)
                            {
                                var delMe = _messages.Take(100);
                                await _channel.DeleteMessagesAsync(delMe);                
                                _messages.RemoveRange(0, 100);
                            }
                            else
                            {
                                await _channel.DeleteMessagesAsync(_messages); 
                            }                        
                        }
                        while (_messages.Count > 100);                    
                    }
                    else
                    {
                        await _channel.DeleteMessagesAsync(_messages);
                    }                
                }
            }
        }      
    }
}