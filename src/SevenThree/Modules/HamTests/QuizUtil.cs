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
        private readonly List<Questions> _questions;
        private List<Questions> _questionsAsked;
        private readonly IUser _user;        
        private readonly int _totalQuestions;
        private Quiz _quiz;
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
            List<Questions> questions,
            IServiceProvider services,
            ulong id            
        )
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<SevenThreeContext>();

            _guild = guild;
            _questions = questions;
            _channel = channel;  
            _id = id;              
            _totalQuestions = questions.Count;
            _questionsAsked = new List<Questions>();                                   
        }

        public QuizUtil(
            IGuild guild,
            IUser user,
            List<Questions> questions,
            IServiceProvider services,
            ulong id
        )
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<SevenThreeContext>();

            _guild = guild;
            _questions = questions;
            _user = user;       
            _id = id;   
            _totalQuestions = questions.Count;
            _questionsAsked = new List<Questions>();        
            _isDmTest = true;                
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

        public async Task StartGame(Quiz quiz)
        {
            Quiz = quiz;
            while (!ShouldStopTest)
            {
                if (_questions.Count == 0)
                {
                    StopQuiz();
                    return;
                } 
                _tokenSource = new CancellationTokenSource();
                var random = new Random();
                CurrentQuestion = _questions[random.Next(_questions.Count)];     
          
                _questions.Remove(CurrentQuestion);
                try
                {
                    var activeQuiz = await _db.Quiz.Where(r => r.ServerId == Id).FirstOrDefaultAsync();
                    if (activeQuiz != null)
                    {
                        //add question to questions asked pool
                        _questionsAsked.Add(CurrentQuestion);

                        //make code for old CurrentQuestions
                        var embed = GetQuestionEmbed();

                        //associate answers with letters (randomly)                    
                        await SetupAnswers(random, embed);
                        
                        if (!string.IsNullOrEmpty(CurrentQuestion.FigureName))
                        {
                            await SendReplyAsync(embed, true);
                        }
                        else
                        {
                            await SendReplyAsync(embed, false);
                        }                        
                    }      
                    else
                    {
                        StopQuiz();
                        return;
                    }              
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }   
                try 
                {                    
                    _client.MessageReceived += ListenForAnswer;
                    IsActive = true;
                    try
                    {
                        await Task.Delay(60000, _tokenSource.Token).ConfigureAwait(false);
                    }        
                    catch (TaskCanceledException)
                    {
                        //question is answered
                        Thread.Sleep(5000);
                    }
                }   
                finally
                {
                    _client.MessageReceived -= ListenForAnswer;
                    IsActive = false;
                }
                if (!_tokenSource.IsCancellationRequested)
                {
                    //actions if the timer expires and no one answered
                    var embed = new EmbedBuilder();
                    _client.MessageReceived -= ListenForAnswer;
                }
            }                                        
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

            embed.Title = $"Question: [{CurrentQuestion.QuestionSection}] From Test: [{CurrentQuestion.Test.TestName}]!";
            embed.WithColor(new Color(0, 255, 0));

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
                Value = CurrentQuestion.QuestionText,
                IsInline = false
            });

            embed.WithFooter(new EmbedFooterBuilder{
               Text = $"Question ({_questionsAsked.Count} / {_totalQuestions})!"
            });            

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
                    if (txtChannel != null && txtChannel.Guild.Id != _id)
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
                answerText.AppendLine($"The correct answer was [{Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault().Item1}] -> [{Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault().Item2.AnswerText}]");
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
            var answerText = new StringBuilder();
            answerText.AppendLine($"Congrats -> **[{msg.Author.Mention}]** <-!");
            answerText.AppendLine($"You had the correct answer of [*{answerChar}*] -> [**{possibleAnswer.AnswerText}**]");
            answerText.Append($"You have [**{correctAnswers.Count}**] correct answers so far.");
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
                    await _channel.SendMessageAsync(message);
                }
                else if (_user != null)
                {
                    await _user.SendMessageAsync(message);
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
                            await _user.SendFileAsync($"{fileName}", "", false, embed.Build());                                
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
            ShouldStopTest = true;  
            _client.MessageReceived -= ListenForAnswer;                                 
            var quiz = _db.Quiz.Where(q => q.QuizId == Quiz.QuizId).FirstOrDefault();  
            quiz.TimeEnded = DateTime.Now;
            quiz.IsActive = false;
            await _db.SaveChangesAsync();   

            var embed = new EmbedBuilder();
            embed.Title = $"Results!";
            embed.WithColor(new Color(0, 255, 0));

            var sb = new StringBuilder();
            var users = await _db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId).ToListAsync();
            if (users != null)
            {                                
                var userResults = await GetTopUsers();
                if (userResults.Count > 0)
                {
                    int i = 0;
                    foreach (var user in userResults)
                    {
                        i++;
                        sb.AppendLine($"{i}. [{users.Where(u => (ulong)u.UserId == user.Item1).FirstOrDefault().UserName}] with [{user.Item2}]");   
                    }
                    embed.Description = sb.ToString();
                    await SendReplyAsync(embed, false);
                    return;
                }                
            }
            embed.Description = "Nobody scored!";
            await SendReplyAsync(embed, false);
        }        
    }
}