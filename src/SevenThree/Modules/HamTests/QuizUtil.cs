using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using SevenThree.Models;
using Discord;
using Discord.Net;
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
        private bool _isActive = false;
        private bool _isDmTest = false;
        private ulong _discordServer;
        private ulong _id;
        private CancellationTokenSource _tokenSource;
        private ILogger _logger;
        private readonly DiscordSocketClient _client;
        private readonly IGuild _guild;
        private readonly ITextChannel _channel;
        private List<Questions> _questions;
        private readonly HamTestService _hamTestService;
        private List<Questions> _questionsAsked;
        private readonly IUser _user;
        private int _totalQuestions;
        private int _questionDelay;
        private List<IMessage> _messages;
        private List<IUser> _skipUsers;
        private QuizHelper _quizHelper;
        private QuizMode _quizMode = QuizMode.Private;
        private IUserMessage _currentButtonMessage;

        public IMessage CurMessage { get; private set; }
        public bool ShouldStopTest { get; private set; }
        public Questions CurrentQuestion { get; private set; }
        public List<Tuple<char, Answer>> Answers { get; private set; }
        public Quiz Quiz { get; private set; }
        public QuizMode Mode => _quizMode;        


        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                _isActive = value;
            }
        }

        public ulong Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }
        
        public CancellationTokenSource TokenSource
        {
            get
            {
                return _tokenSource;
            }
            set
            {
                _tokenSource = value;
            }
        }
        
        public QuizUtil(
            IGuild guild,
            ITextChannel channel,
            IServiceProvider services,
            ulong id
        )
        {
            _logger = services.GetRequiredService<ILogger<QuizUtil>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _contextFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            _channel = channel;
            _id = id;
            _questionsAsked = new List<Questions>();
            _questionDelay = 60000;
            _messages = new List<IMessage>();
            _skipUsers = new List<IUser>();
            _quizHelper = new QuizHelper();
        }

        public QuizUtil(
            IGuild guild,
            IUser user,
            IServiceProvider services,
            ulong id
        )
        {
            _logger = services.GetRequiredService<ILogger<QuizUtil>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _contextFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            _user = user;
            _id = id;
            _questionsAsked = new List<Questions>();
            _isDmTest = true;
            _questionDelay = 60000;
            _skipUsers = new List<IUser>();
            _quizHelper = new QuizHelper();
        }        

        public void SetServer(ulong discordServer)
        {
            _discordServer = discordServer;
            this.Id = _discordServer;
        }

        public void SetActive()
        {
            IsActive = true;
        }      

        public async Task StartGame(Quiz quiz, int numQuestions, string testName, int questionDelay)
        {
            //set class fields/properties based on passed in parameters
            //delay between questions
            _questionDelay = questionDelay;
            //quiz association from db
            Quiz = quiz;  
            //get test questions based on passed in parameters
            var testQuestions = await GetRandomQuestions(numQuestions, testName, figuresOnly: false); 
            //set class field
            _questions = testQuestions;  
            //set the number of total quesitons  
            _totalQuestions = _questions.Count();                  

            //now we will loop until the test should stop
            while (!ShouldStopTest)
            {
                //stop the test if there are 0 questions left
                if (_questions.Count == 0)
                {
                    await StopQuiz().ConfigureAwait(false);
                    return;
                }          
                //get a token we can use and cancel if it is answered correctly
                _tokenSource = new CancellationTokenSource();
                //get a new random instance            
                var random = new Random();
                //get a random question out of the pool of ones left
                CurrentQuestion = _questions[random.Next(_questions.Count)];               
                //remove the question we are using from the pool
                _questions.Remove(CurrentQuestion);
                try
                {
                    //add question to questions asked pool
                    _questionsAsked.Add(CurrentQuestion);

                    //make code for old CurrentQuestions
                    var embed = GetQuestionEmbed();

                    //associate answers with letters (randomly)                    
                    await SetupAnswers(random, embed);
                    
                    //send question information based on if there is a figure or not
                    if (!string.IsNullOrEmpty(CurrentQuestion.FigureName))
                    {
                        await SendQuestionWithButtons(embed, hasFigure: true);
                    }
                    else
                    {
                        await SendQuestionWithButtons(embed, hasFigure: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    await Task.Delay(5000).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    // For button mode, we wait for button clicks via QuizButtonHandler
                    // which will cancel the token when an answer is received
                    IsActive = true;
                    try
                    {
                        await Task.Delay(_questionDelay, _tokenSource.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // Answer received via button click
                    }
                }
                finally
                {
                    IsActive = false;
                    if (!ShouldStopTest && _quizMode == QuizMode.Public)
                    {
                        await SendQuestionResults();
                    }
                }
                // Auto-advance delay (3 seconds for button mode)
                await Task.Delay(3000).ConfigureAwait(false);                             
            }                                        
        }

        private async Task SendQuestionResults()
        {
            var message = CurMessage as IUserMessage;
            var usersWon = await GetCorrectUsersFromQuestion();
            var usersLost = await GetIncorrectUsersFromQuestion();
            EmbedBuilder embed = await GetQuestionResultsEmbed(usersWon, usersLost);
            await SendReplyAsync(embed, false);
        }

        private async Task<EmbedBuilder> GetQuestionResultsEmbed(List<UserAnswer> usersWon, List<UserAnswer> usersLost)
        {
            var sb = new StringBuilder();
            var embed = new EmbedBuilder();
            embed.Title = $"Question [{CurrentQuestion.QuestionSection}] Results";
            embed.AddField
            (
                new EmbedFieldBuilder
                {
                    Name = "Question:",
                    Value = $"**{CurrentQuestion.QuestionText ?? "Question not available"}**"
                }
            );
            var correctAnswer = Answers.FirstOrDefault(a => a.Item2.IsAnswer);
            var answerValue = correctAnswer != null
                ? $"**{correctAnswer.Item1}**. *{correctAnswer.Item2.AnswerText ?? "Answer text not available"}*"
                : "Correct answer not found";
            embed.AddField
            (
                new EmbedFieldBuilder
                {
                    Name = "Answer:",
                    Value = answerValue
                }
            );
            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";
            if (usersWon.Count > 0 && usersLost.Count == 0)
            {
                sb.AppendLine("**__Answered Correctly:__**");
                foreach (var user in usersWon)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
                embed.WithColor(new Color(0, 255, 0));
            }
            else if (usersWon.Count == 0 && usersLost.Count == 0)
            {
                embed.WithColor(new Color(255, 0, 0));
                sb.AppendLine("**__Nobody answered the question =(__**");
            }
            else if (usersLost.Count > 0 && usersWon.Count == 0)
            {
                embed.WithColor(new Color(255, 0, 0));
                sb.AppendLine("**__Answered Incorrectly:__**");
                foreach (var user in usersLost)
                {
                    sb.AppendLine($"**{user.UserName}**");
                }
            }
            else if (usersLost.Count > 0 && usersWon.Count > 0)
            {
                embed.WithColor(new Color(100, 155, 0));
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
                IUser guildUser = null;
                if (!_isDmTest)
                {
                    guildUser = await _guild.GetUserAsync((ulong)leader.Item1);
                    embed.WithFooter
                    (
                        new EmbedFooterBuilder
                        {
                            Text = $"[{guildUser?.Username ?? "Unknown"}] is currently in the lead with [{numCorrect}] correct answers!",
                            IconUrl = guildUser?.GetAvatarUrl() ?? guildUser?.GetDefaultAvatarUrl()
                        }
                    );
                }
                else
                {
                    embed.WithFooter
                    (
                        new EmbedFooterBuilder
                        {
                            Text = $"You have [{numCorrect}] right so far, [{_user.Username}]!",
                            IconUrl = _user.GetAvatarUrl() ?? _user.GetDefaultAvatarUrl()
                        }
                    );
                }
            }
            else
            {
                embed.WithFooter
                (
                    new EmbedFooterBuilder
                    {
                        Text = $"Nobody has any correct answers!",
                        IconUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true"
                    }
                );

            }

            return embed;
        }

        private async Task SetupAnswers(Random random, EmbedBuilder embed)
        {
            var answerOptions = new List<Tuple<char, Answer>>();
            var letters = new List<char>() { 'A', 'B', 'C', 'D' };
            var usedNumbers = new List<int>();
            var usedLetters = new List<char>();

            using var db = _contextFactory.CreateDbContext();
            var answers = await db.Answer.Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();

            bool addingAnswers = true;
            int i = 0;
            while (addingAnswers)
            {
                var randAnswer = random.Next(answers.Count());
                while (usedNumbers.Contains(randAnswer))
                {
                    randAnswer = random.Next(answers.Count());
                }
                var answer = answers[randAnswer];
                var letter = letters[i];

                answerOptions.Add(Tuple.Create(letter, answer));
                usedNumbers.Add(randAnswer);
                if (answerOptions.Count() == answers.Count())
                {
                    addingAnswers = false;
                }
                i++;
            }

            foreach (var answer in answerOptions)
            {
                var letter = answer.Item1;
                var answerData = answer.Item2;
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = $"{letter}.",
                    Value = answerData.AnswerText ?? "No answer text",
                    IsInline = true
                });
            }
            
            Answers = answerOptions;
        }

        private EmbedBuilder GetQuestionEmbed()
        {
            var embed = new EmbedBuilder();

            embed.WithAuthor(
                new EmbedAuthorBuilder
                {
                    Name    = $"Test started by [{Quiz.StartedByName ?? "Unknown"}]",
                    IconUrl = Quiz.StartedByIconUrl
                }
            );
            embed.Title = $"Question: [{CurrentQuestion.QuestionSection}] From Test: [{CurrentQuestion.Test.TestName}]!";

            switch (CurrentQuestion.Test.TestName)
            {
                case "tech":
                {
                    embed.WithColor(new Color(0, 128, 255));
                    break;
                }
                case "general":
                {
                    embed.WithColor(new Color(102, 102, 255));
                    break;
                }
                case "extra":
                {
                    embed.WithColor(new Color(255, 102, 255));
                    break;
                }
                default:
                {
                    embed.WithColor(new Color(0 ,255, 0));
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(CurrentQuestion.FccPart))
            {
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = "FCC Part",
                    Value = CurrentQuestion.FccPart
                });
            }

            embed.AddField(new EmbedFieldBuilder
            {
                Name = $"Subelement [**{CurrentQuestion.SubelementName ?? "Unknown"}**]",
                Value = CurrentQuestion.SubelementDesc ?? "No description available"
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = $"Question:",
                Value = $"**{CurrentQuestion.QuestionText ?? "Question not available"}**",
                IsInline = false
            });

            embed.WithFooter(new EmbedFooterBuilder{
               Text = $"Question ({_questionsAsked.Count} / {_totalQuestions})... [{_questionDelay/1000}] seconds to answer!"
            });          

            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";  

            return embed;
        }
       
        private async Task IncorrectAnswer(SocketMessage msg, char? answerChar)
        {
            using var db = _contextFactory.CreateDbContext();
            db.Attach(CurrentQuestion);
            db.Attach(Quiz);
            db.UserAnswer.Add(new UserAnswer
            {
                UserId = (long)msg.Author.Id,
                UserName = msg.Author.Username,
                Question = CurrentQuestion,
                AnswerText = answerChar.ToString(),
                Quiz = Quiz,
                IsAnswer = false
            });
            await db.SaveChangesAsync();
            if (_isDmTest)
            {
                _tokenSource.Cancel();
            }
        }

        private async Task IncorrectAnswer(IUser user, char? answerChar)
        {
            using var db = _contextFactory.CreateDbContext();
            db.Attach(CurrentQuestion);
            db.Attach(Quiz);
            db.UserAnswer.Add(new UserAnswer
            {
                UserId = (long)user.Id,
                UserName = user.Username,
                Question = CurrentQuestion,
                AnswerText = answerChar.ToString(),
                Quiz = Quiz,
                IsAnswer = false
            });
            await db.SaveChangesAsync();
            if (_isDmTest)
            {
                _tokenSource.Cancel();
            }
        }

        private async Task<bool> UserAnsweredCorrectly(IUser user, bool answered, char? answerChar, Answer possibleAnswer)
        {
            using var db = _contextFactory.CreateDbContext();
            db.Attach(CurrentQuestion);
            db.Attach(Quiz);
            db.UserAnswer.Add(new UserAnswer
            {
                UserId = (long)user.Id,
                UserName = user.Username,
                Question = CurrentQuestion,
                AnswerText = answerChar.ToString(),
                Quiz = Quiz,
                IsAnswer = true
            });
            await db.SaveChangesAsync();
            answered = true;
            return answered;
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

                // Send result as followup
                await component.FollowupAsync(embed: resultEmbed.Build(), ephemeral: _quizMode == QuizMode.Private);

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
                embed.WithColor(new Color(0, 255, 0));
                embed.Title = "Correct!";
                embed.Description = $"**{selectedAnswer}**. {selectedAnswerData.AnswerText ?? "Answer text not available"}";
            }
            else
            {
                embed.WithColor(new Color(255, 0, 0));
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

        private Task SendReplyAsync(string message)
        {
            var _ = Task.Run(async () => 
            {
                if (_channel != null)
                {
                    _messages.Add(await _channel.SendMessageAsync(message));
                }
                else if (_user != null)
                {
                    _messages.Add(await _user.SendMessageAsync(message));
                } 
            });
            return Task.CompletedTask;
        }

        private Task SendReplyAsync(EmbedBuilder embed, bool hasFigure)
        {
            var _ = Task.Run(async () =>
            {
                if (_channel != null)
                {
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            _messages.Add(await _channel.SendFileAsync($"{fileName}", "", false, embed.Build()));
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        _messages.Add(await _channel.SendMessageAsync(null, false, embed.Build()));
                    }
                }
                else if (_user != null)
                {
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            _messages.Add(await _user.SendFileAsync($"{fileName}", "", false, embed.Build()));
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        await _user.SendMessageAsync(null, false, embed.Build());
                    }
                }
            });
            return Task.CompletedTask;
        }

        private Task SendReplyAsync(EmbedBuilder embed, bool hasFigure, bool delete)
        {
            var _ = Task.Run(async () =>
            {
                if (_channel != null)
                {
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            await _channel.SendFileAsync($"{fileName}", "", false, embed.Build());
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        await _channel.SendMessageAsync(null, false, embed.Build());
                    }
                }
                else if (_user != null)
                {
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            _messages.Add(await _user.SendFileAsync($"{fileName}", "", false, embed.Build()));
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        await _user.SendMessageAsync(null, false, embed.Build());
                    }
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Send a question with button components instead of reactions
        /// </summary>
        private Task SendQuestionWithButtons(EmbedBuilder embed, bool hasFigure)
        {
            var _ = Task.Run(async () =>
            {
                var buttons = BuildAnswerButtons();

                if (_channel != null)
                {
                    // Public mode - send to channel
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            var message = await _channel.SendFileAsync(fileName, "", false, embed.Build(), components: buttons);
                            CurMessage = message;
                            SetCurrentButtonMessage(message);
                            _messages.Add(message);
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        var message = await _channel.SendMessageAsync(null, false, embed.Build(), components: buttons);
                        CurMessage = message;
                        SetCurrentButtonMessage(message);
                        _messages.Add(message);
                    }
                }
                else if (_user != null)
                {
                    // Private mode - send to user DM
                    if (hasFigure)
                    {
                        using var db = _contextFactory.CreateDbContext();
                        var figure = db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
                        if (figure != null)
                        {
                            var fileName = $"{figure.Test.TestName}_{figure.FigureName}.png";
                            if (!File.Exists(fileName))
                            {
                                await File.WriteAllBytesAsync(fileName, figure.FigureImage);
                            }
                            embed.WithImageUrl($"attachment://{fileName}");
                            var message = await _user.SendFileAsync(fileName, "", false, embed.Build(), components: buttons);
                            CurMessage = message;
                            SetCurrentButtonMessage(message);
                            File.Delete(fileName);
                        }
                    }
                    else
                    {
                        var message = await _user.SendMessageAsync(null, false, embed.Build(), components: buttons);
                        CurMessage = message;
                        SetCurrentButtonMessage(message);
                    }
                }
            });
            return Task.CompletedTask;
        }

        private async Task<List<Tuple<ulong, int>>> GetTopUsers()
        {
            using var db = _contextFactory.CreateDbContext();
            var users = await db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            users = users.Where(u => u.IsAnswer).ToList();
            var userResults = new List<Tuple<ulong, int>>();
            foreach (var user in users.Select(u => u.UserId).Distinct())
            {
                var userId = user;
                var numCorrect = users.Where(u => (long)u.UserId == user && u.IsAnswer).ToList().Count;
                userResults.Add(Tuple.Create((ulong)user, numCorrect));
            }
            return userResults.OrderByDescending(o => o.Item2).ToList();
        }

        private async Task<List<UserAnswer>> GetCorrectUsersFromQuestion()
        {
            using var db = _contextFactory.CreateDbContext();
            var users = await db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId && u.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();
            users = users.Where(u => u.IsAnswer).ToList();
            return users;
        }

        private async Task<List<UserAnswer>> GetIncorrectUsersFromQuestion()
        {
            using var db = _contextFactory.CreateDbContext();
            var users = await db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId && u.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();
            users = users.Where(u => !u.IsAnswer).ToList();
            return users;
        } 

        internal async Task StopQuiz()
        {
            QuizUtil trivia = null;
            _hamTestService.RunningTests.TryRemove(Id, out trivia);
            ShouldStopTest = true;

            using var db = _contextFactory.CreateDbContext();
            var quiz = db.Quiz.Where(q => q.QuizId == Quiz.QuizId).FirstOrDefault();
            if (quiz != null)
            {
                quiz.TimeEnded = DateTime.UtcNow;
                quiz.IsActive = false;
                await db.SaveChangesAsync();
            }

            var embed = new EmbedBuilder();
            embed.Title = $"[{CurrentQuestion.Test.TestName}] [{CurrentQuestion.Test.FromDate.ToShortDateString()} -> {CurrentQuestion.Test.ToDate.ToShortDateString()}] Test Results!";
            embed.WithColor(new Color(0, 255, 0));
            var sb = new StringBuilder();
            sb.AppendLine($"Number of questions -> [**{_totalQuestions}**]");
            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";
            embed.WithFooter(new EmbedFooterBuilder
            {
                Text = "SevenThree, your local ham radio Discord bot!",
                IconUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true"
            });
            var users = await db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            if (users != null)
            {                                
                sb.AppendLine();
                sb.AppendLine($"**__Leaderboard:__**");                
                var userResults = await GetTopUsers();
                if (userResults.Count > 0)
                {
                    int i = 0;
                    string passFailEmoji = string.Empty;
                    foreach (var user in userResults)
                    {
                        i++;
                        decimal percentage;
                        percentage = ((decimal)user.Item2 / (decimal)_totalQuestions) * 100;
                        passFailEmoji = _quizHelper.GetPassFail(percentage);
                        sb.AppendLine($"{_quizHelper.GetNumberEmojiFromInt(i)} [**{users.Where(u => (ulong)u.UserId == user.Item1).FirstOrDefault().UserName}**] with [**{user.Item2}**] [{passFailEmoji}] ({Math.Round(percentage, 0)}%)");
                    }
                    sb.AppendLine();
                    sb.AppendLine($"Thanks for taking the test! Happy learning.");
                    embed.Description = sb.ToString();
                    await SendReplyAsync(embed, false);
                    await ClearChannel();
                    return;
                }                
            }
            embed.Description = "Nobody scored!";                        
            await SendReplyAsync(embed, false, false);
            await ClearChannel();
        }

        private async Task<List<Questions>> GetRandomQuestions(int numQuestions, string testName, bool figuresOnly = false)
        {
            using var db = _contextFactory.CreateDbContext();
            List<Questions> questions;
            if (!figuresOnly)
            {
                questions = await db.Questions.Include(q => q.Test).Where(q => q.Test.TestName == testName).ToListAsync();
            }
            else
            {
                questions = await db.Questions.Include(q => q.Test).Where(q => q.Test.TestName == testName && q.FigureName != null).ToListAsync();
            }

            var random = new Random();
            var testQuestions = new List<Questions>();
            if (numQuestions > 100)
            {
                numQuestions = 100;
            }
            for (int i = 0; i < numQuestions; i++)
            {
                var randQuestion = questions[random.Next(questions.Count)];
                while (testQuestions.Contains(randQuestion))
                {
                    randQuestion = questions[random.Next(questions.Count)];
                }
                testQuestions.Add(randQuestion);
            }

            return testQuestions;
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