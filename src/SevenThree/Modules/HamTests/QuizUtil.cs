using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.IO;
using SevenThree.Services;

namespace SevenThree.Modules
{
    public class QuizUtil
    {
        private readonly SemaphoreSlim _guessLock = new SemaphoreSlim(1, 1);
        private readonly SevenThreeContext _db;
        private bool _isActive = false;
        private bool _isDmTest = false;
        private ulong _discordServer;
        private ulong _id;
        private CancellationTokenSource _tokenSource;
        private ILogger _logger;
        Timer _questionCountdown;
        private readonly DiscordSocketClient _client;
        private readonly IGuild _guild;
        private readonly ITextChannel _channel;
        private List<Questions> _questions;
        private readonly HamTestService _hamTestService;
        private List<Questions> _questionsAsked;
        private readonly IUser _user;        
        private int _totalQuestions;
        private Quiz _quiz;
        private bool _wasAnswered;
        private int _questionDelay;
        private List<IMessage> _messages;

        public bool ShouldStopTest { get; private set; }
        public Questions CurrentQuestion { get; private set; }
        public List<Tuple<char, Answer>> Answers { get; private set; }
        public Quiz Quiz { get; private set; }


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
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<SevenThreeContext>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            //_questions = questions;
            _channel = channel;  
            _id = id;              
            //_totalQuestions = questions.Count;
            _questionsAsked = new List<Questions>();   
            _questionDelay = 60000;   
            _messages = new List<IMessage>();                                   
        }

        public QuizUtil(
            IGuild guild,
            IUser user,
            IServiceProvider services,
            ulong id
        )
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<SevenThreeContext>();
            _hamTestService = services.GetRequiredService<HamTestService>();

            _guild = guild;
            //_questions = questions;
            _user = user;       
            _id = id;   
            //_totalQuestions = questions.Count;
            _questionsAsked = new List<Questions>();        
            _isDmTest = true;    
            _questionDelay = 60000;                        
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
            _questionDelay = questionDelay;
            Quiz = quiz;  
            var testQuestions = await GetRandomQuestions(numQuestions, testName); 
            _questions = testQuestions;    
            _totalQuestions = _questions.Count();                  
            while (!ShouldStopTest)
            {
                if (_questions.Count == 0)
                {
                    await StopQuiz().ConfigureAwait(false);
                    return;
                }          
                _tokenSource = new CancellationTokenSource();
                var random = new Random();
                CurrentQuestion = _questions[random.Next(_questions.Count)];               
                _questions.Remove(CurrentQuestion);
                try
                {
                    //add question to questions asked pool
                    _questionsAsked.Add(CurrentQuestion);

                    //make code for old CurrentQuestions
                    var embed = GetQuestionEmbed();

                    //associate answers with letters (randomly)                    
                    await SetupAnswers(random, embed);
                    
                    if (!string.IsNullOrEmpty(CurrentQuestion.FigureName))
                    {
                        await SendReplyAsync(embed, true).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendReplyAsync(embed, false).ConfigureAwait(false);
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
                    //listen for answers    
                    _client.MessageReceived += ListenForAnswer;
                    IsActive = true;
                    try
                    {                        
                        await Task.Delay(_questionDelay, _tokenSource.Token).ConfigureAwait(false);                        
                    }        
                    catch (TaskCanceledException)
                    {
                        //answered correctly                       
                    }
                }   
                finally
                {     
                    _client.MessageReceived -= ListenForAnswer;
                    IsActive = false;                         
                }
                if (!_tokenSource.IsCancellationRequested && !IsActive && !ShouldStopTest)
                {       
                    await NoAnswer();                    
                }
                await Task.Delay(5000).ConfigureAwait(false);
            }                                        
        }

        private async Task NoAnswer()
        {            
            //actions if the timer expires and no one answered
            _client.MessageReceived -= ListenForAnswer;

            var embed = new EmbedBuilder();
            embed.Title = $"Nobody answered correctly :(";
            var sb = new StringBuilder();
            sb.AppendLine($"Question: [**{CurrentQuestion.QuestionText}**]");
            var answer = Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault();
            if (answer != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Answer: [**{answer.Item1}**] -> [**{answer.Item2.AnswerText}**]");
            }
            embed.Description = sb.ToString();
            embed.WithColor(new Color(255, 0, 0));
            await SendReplyAsync(embed, false);
        }

        private async Task SetupAnswers(Random random, EmbedBuilder embed)
        {
            var answerOptions = new List<Tuple<char, Answer>>();
            var letters = new List<char>() { 'A', 'B', 'C', 'D' };
            var usedNumbers = new List<int>();
            var usedLetters = new List<char>();

            var answers = await _db.Answer.Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();

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
                    Value = answerData.AnswerText,
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
                    Name    = $"Test started by [{Quiz.StartedByName}]",
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

            if (CurrentQuestion.FccPart != null)
            {
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = "FCC Part",
                    Value = CurrentQuestion.FccPart
                });
            }

            embed.AddField(new EmbedFieldBuilder
            {
                Name = $"Subelement [**{CurrentQuestion.SubelementName}**]",
                Value = CurrentQuestion.SubelementDesc
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = $"Question:",
                Value = $"**{CurrentQuestion.QuestionText}**",
                IsInline = false
            });

            embed.WithFooter(new EmbedFooterBuilder{
               Text = $"Question ({_questionsAsked.Count} / {_totalQuestions})... [{_questionDelay/1000}] seconds to answer!"
            });          

            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";  

            return embed;
        }

        private Task ListenForAnswer(SocketMessage msg)
        {
            var _ = Task.Run(async () => 
            {                               
                try
                {
                    if (msg.Author.IsBot)
                    {
                        return;
                    }
                    var txtChannel = msg?.Channel as ITextChannel;                                        
                    var usrChannel = msg?.Channel as IDMChannel;
                    if (txtChannel == null && usrChannel == null)
                    {
                        return;
                    }
                    if (txtChannel != null && txtChannel.Id != _id)
                    {
                        return;
                    }
                    if (usrChannel != null && msg.Author.Id != _id)
                    {
                        return;
                    }
                    var answered = false;
                    await _guessLock.WaitAsync().ConfigureAwait(false);                    
                    try
                    {                            
                        if (IsActive && !_tokenSource.IsCancellationRequested)
                        {
                            if (txtChannel != null)
                            {
                                _messages.Add(msg);
                            }
                            char? answerChar = null;
                            answerChar = char.Parse(msg.Content.ToUpper());
                            UserAnswer userAnswered = null;
                            
                            userAnswered = await _db.UserAnswer.Where(a => a.Question == CurrentQuestion && a.UserId == (long)msg.Author.Id && a.Quiz.QuizId == Quiz.QuizId).FirstOrDefaultAsync();
                                                       
                            if (userAnswered == null)
                            {                                                                
                                if (answerChar == 'A' || answerChar == 'B' || answerChar == 'C' || answerChar == 'D')
                            {
                                    Answer possibleAnswer = null;                                    
                                    possibleAnswer = Answers.Where(w => w.Item1 == answerChar).Select(w => w.Item2).FirstOrDefault();                                    
                                    if (possibleAnswer.IsAnswer)
                                    {
                                        answered = await UserAnsweredCorrectly(msg, answered, answerChar, possibleAnswer);
                                    }
                                    else
                                    {
                                        await IncorrectAnswer(msg, answerChar);
                                    }
                                }            
                            }                       
                        }
                    }
                    finally
                    {
                        _guessLock.Release();                                                                    
                    }                               
                if (!answered)
                {
                    return;
                }       
                _tokenSource.Cancel();         
                }
                catch (System.FormatException){}   
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }           
            });
            return Task.CompletedTask;
        }

        private async Task IncorrectAnswer(SocketMessage msg, char? answerChar)
        {
            var answerText = new StringBuilder();
            answerText.AppendLine($"Sorry -> **[{msg.Author.Mention}]** <-!");
            answerText.AppendLine($"[*{answerChar}*] was not the right answer!");
            if (!_isDmTest)
            {
                answerText.AppendLine("Please wait until the next question to try again.");
            }  
            else
            {
                var answer = Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault();
                if (answer != null)
                {
                    answerText.AppendLine($"The correct answer was [**{answer.Item1}**] -> [**{answer.Item2.AnswerText}**]");
                }                
                
            }          
            await msg?.Author.SendMessageAsync(answerText.ToString());
            _db.UserAnswer.Add(new UserAnswer
            {
                UserId = (long)msg.Author.Id,
                UserName = msg.Author.Username,
                Question = CurrentQuestion,
                AnswerText = answerChar.ToString(),
                Quiz = Quiz,
                IsAnswer = false
            });
            await _db.SaveChangesAsync();
            if (_isDmTest) {
                _tokenSource.Cancel();
            }            
        }

        private async Task<bool> UserAnsweredCorrectly(SocketMessage msg, bool answered, char? answerChar, Answer possibleAnswer)
        {
            List<UserAnswer> correctAnswers = null;
            _db.UserAnswer.Add(
                new UserAnswer
                {
                    UserId = (long)msg.Author.Id,
                    UserName = msg.Author.Username,
                    Question = CurrentQuestion,
                    AnswerText = answerChar.ToString(),
                    Quiz = Quiz,
                    IsAnswer = true
                });
            await _db.SaveChangesAsync();
            correctAnswers = await _db.UserAnswer.Where(u => (ulong)u.UserId == msg.Author.Id && u.IsAnswer && u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            var embed = new EmbedBuilder();
            embed.Title = "Question Answered Correctly!";
            embed.WithColor(new Color(0, 255, 0));                        
            var answerText = new StringBuilder();
            answerText.AppendLine($"Congrats -> **[{msg.Author.Mention}]** <-");
            answerText.AppendLine($"The question was -> [**{CurrentQuestion.QuestionText}**]");
            answerText.AppendLine();
            answerText.AppendLine($"You had the correct answer of [*{answerChar}*] -> [**{possibleAnswer.AnswerText}**]");

            if (correctAnswers.Count == 1)
            {
                embed.WithFooter(
                    new EmbedFooterBuilder
                    {
                        Text = $"[{msg?.Author.Username}] has just one correct answer so far!"
                    }
                );
            }
            else if (correctAnswers.Count > 1)
            {
                embed.WithFooter(
                    new EmbedFooterBuilder
                    {
                        Text = $"[{msg?.Author.Username}] has [{correctAnswers.Count}] correct answers so far!"
                    }
                );
            }
                        
            embed.Description = answerText.ToString();
            embed.ThumbnailUrl = msg.Author.GetAvatarUrl();
            await SendReplyAsync(embed, false);
            answered = true;
            return answered;
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
                        var figure = _db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
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
                        var figure = _db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
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
                        var figure = _db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
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
                        var figure = _db.Figure.Include(t => t.Test).Where(f => f.FigureName == CurrentQuestion.FigureName).FirstOrDefault();
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

        private async Task<List<Tuple<ulong, int>>> GetTopUsers()
        {
            var users = await _db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();            
            users = users.Where(u => u.IsAnswer).ToList();
            var userResults = new List<Tuple<ulong, int>>();            
            foreach (var user in users.Select(u => u.UserId).Distinct())
            {
                var userId     = user;
                var numCorrect = users.Where(u => (long)u.UserId == user && u.IsAnswer).ToList().Count;           
                userResults.Add(Tuple.Create((ulong)user, numCorrect));                            
            }            
            return userResults.OrderByDescending(o => o.Item2).ToList();
        }

        internal async Task StopQuiz()
        {            
            //wrap quiz up here           
            QuizUtil trivia = null;
            _hamTestService.RunningTests.TryRemove(Id, out trivia);
            ShouldStopTest = true;              
            _client.MessageReceived -= ListenForAnswer;                                 
            var quiz = _db.Quiz.Where(q => q.QuizId == Quiz.QuizId).FirstOrDefault();  
            quiz.TimeEnded = DateTime.Now;
            quiz.IsActive = false;
            await _db.SaveChangesAsync();   
            var embed = new EmbedBuilder();
            embed.Title = $"Test -> [{CurrentQuestion.Test.TestName}] Results!";
            embed.WithColor(new Color(0, 255, 0));
            var sb = new StringBuilder();
            sb.AppendLine($"Number of questions -> [**{_totalQuestions}**]");
            embed.ThumbnailUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true";
            embed.WithFooter(new EmbedFooterBuilder{
                Text = "SevenThree, your local ham radio Discord bot!",
                IconUrl = "https://github.com/gngrninja/SevenThree/raw/master/media/73.png?raw=true"
            });
            var users = await _db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            if (users != null)
            {                                
                sb.AppendLine();
                sb.AppendLine($"**__Leaderboard:__**");                
                var userResults = await GetTopUsers();
                if (userResults.Count > 0)
                {
                    int i = 0;
                    foreach (var user in userResults)
                    {
                        i++;
                        sb.AppendLine($"{GetNumberEmojiFromInt(i)} [**{users.Where(u => (ulong)u.UserId == user.Item1).FirstOrDefault().UserName}**] with [**{user.Item2}**]");   
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
        
        public string GetNumberEmojiFromInt(int number)
        {
            string numberEmoji = string.Empty;
            switch (number)
            {
                case 1:
                {
                    numberEmoji = ":one:";
                    break;
                }
                case 2:
                {
                    numberEmoji = ":two:";
                    break;
                }
                case 3:
                {
                    numberEmoji = ":three:";
                    break;
                }
                case 4:
                {
                    numberEmoji = ":four:";
                    break;
                }
                case 5:
                {
                    numberEmoji = ":five:";
                    break;
                }
                case 6:
                {
                    numberEmoji = ":six:";
                    break;
                }
                case 7:
                {
                    numberEmoji = ":seven:";
                    break;
                }
                case 8:
                {
                    numberEmoji = ":eight:";
                    break;
                }
                case 9:
                {
                    numberEmoji = ":nine:";
                    break;
                }
                case 10:
                {
                    numberEmoji = ":one::zero:";
                    break;
                }
                case 11:
                {
                    numberEmoji = ":one::one:";
                    break;
                }
                case 12:
                {
                    numberEmoji = ":one::two:";
                    break;
                }
                case 13:
                {
                    numberEmoji = ":one::three:";
                    break;
                }
                case 14:
                {
                    numberEmoji = ":one::four:";
                    break;
                }
                default:
                {
                    numberEmoji = ":zero:";
                    break;
                }                                                                                                                                                                                
            }
            return numberEmoji;
        } 

        private async Task<List<Questions>> GetRandomQuestions(int numQuestions, string testName)
        {            
            var questions = await _db.Questions.Include(q => q.Test).Where(q => q.Test.TestName == testName).ToListAsync();
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
            var settings = await _db.QuizSettings.Where(s => s.DiscordGuildId == Id).FirstOrDefaultAsync();
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