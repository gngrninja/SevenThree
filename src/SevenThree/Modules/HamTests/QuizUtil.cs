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

        public bool ShouldStopTest { get; private set; }
        public Questions CurrentQuestion { get; private set; }
        public List<Tuple<char, Answer>> Answers { get; private set; }

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

             _questionsAsked = new List<Questions>();
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

        private static void TimerCallBack(object o)
        {
            System.Console.WriteLine("hi");
        }

        internal void StopQuiz()
        {
            ShouldStopTest = true;
            _client.MessageReceived -= PotentialAnswer;
        }

        public async Task StartGame()
        {
            while (!ShouldStopTest)
            {
                _tokenSource = new CancellationTokenSource();
                var random = new Random();
                CurrentQuestion = _questions[random.Next(_questions.Count)];
                _questionsAsked.Add(CurrentQuestion);
                _questions.Remove(CurrentQuestion);
                if (_questions.Count <= 0)
                {
                    StopQuiz();
                    continue;
                }
                try
                {
                    var activeQuiz = await _db.Quiz.Where(r => r.ServerId == Id).FirstOrDefaultAsync();
                    if (activeQuiz != null)
                    {
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
                        continue;
                    }              
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }   
                try 
                {                    
                    _client.MessageReceived += PotentialAnswer;
                    IsActive = true;
                    try
                    {
                        await Task.Delay(60000, _tokenSource.Token).ConfigureAwait(false);
                        //await Task.Delay(60000, _tokenSource.Token).ConfigureAwait(false);
                    }        
                    catch (TaskCanceledException)
                    {
                        //question as answered
                        Thread.Sleep(5000);
                    }
                }   
                finally
                {
                    IsActive = false;
                }
                if (!_tokenSource.IsCancellationRequested)
                {

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

            return embed;
        }

        private Task PotentialAnswer(SocketMessage msg)
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
                        char? answerChar = null;
                        if (IsActive && !_tokenSource.IsCancellationRequested)
                        {
                            answerChar = char.Parse(msg.Content.ToUpper());
                            var possibleAnswer = Answers.Where(w => w.Item1 == answerChar).Select(w => w.Item2).FirstOrDefault();
                            if (possibleAnswer.IsAnswer)
                            {
                                var answerText = new StringBuilder();
                                answerText.AppendLine($"Congrats -> **[{msg.Author.Mention}]** <-!");
                                answerText.AppendLine($"You had the correct answer of [*{answerChar}*] -> [**{possibleAnswer.AnswerText}**]");
                                await SendReplyAsync(answerText.ToString());                    
                                answered = true;
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
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            });
            return Task.CompletedTask;
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
    }
}