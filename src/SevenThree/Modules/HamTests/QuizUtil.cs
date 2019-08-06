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
            IServiceProvider services
        )
        {
             _logger = services.GetRequiredService<ILogger<CallAssociation>>();
             _client = services.GetRequiredService<DiscordSocketClient>();
             _db = services.GetRequiredService<SevenThreeContext>();

             _guild = guild;
             _questions = questions;
             _channel = channel;             
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

        public async Task StartGame()
        {
            while (!ShouldStopTest)
            {
                _tokenSource = new CancellationTokenSource();
                var random = new Random();
                CurrentQuestion = _questions[random.Next(_questions.Count)];
                
                try
                {                                        
                    //make code for old CurrentQuestions
                    var embed = new EmbedBuilder();

                    embed.Title = $"CurrentQuestion [{CurrentQuestion.QuestionSection}] from test: [{CurrentQuestion.Test.TestName}]!";            
                    embed.WithColor(new Color(0, 255, 0));
                    
                    if (CurrentQuestion.FccPart != null)
                    {
                        embed.AddField(new EmbedFieldBuilder{
                            Name  = "FCC Part",
                            Value = CurrentQuestion.FccPart 
                        });
                    }

                    embed.AddField(new EmbedFieldBuilder{
                        Name  = $"Subelement [**{CurrentQuestion.SubelementName}**]",
                        Value = CurrentQuestion.SubelementDesc
                    });   

                    embed.AddField(new EmbedFieldBuilder{
                        Name     = $"CurrentQuestion:",
                        Value    = CurrentQuestion.QuestionText,
                        IsInline = false                
                    });
                                    
                    //associate answers with letters (randomly)
                    var answerOptions = new List<Tuple<char, Answer>>();               
                    var letters       = new List<char>(){'A','B','C','D'};
                    var usedNumbers   = new List<int>();
                    var usedLetters   = new List<char>();

                    var answers = await _db.Answer.Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();

                    bool addingAnswers = true;
                    int i = 0;
                    while(addingAnswers) 
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
                        var letter     = answer.Item1;
                        var answerData = answer.Item2;
                        embed.AddField(new EmbedFieldBuilder{
                            Name     = $"{letter}.",
                            Value    = answerData.AnswerText,
                            IsInline = true
                        });                
                    }     
                    Answers = answerOptions;
                    await _channel.SendMessageAsync(null,false,embed.Build()); 
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
                        await Task.Delay(60000, _tokenSource.Token).ConfigureAwait(false);
                    }        
                    catch (TaskCanceledException)
                    {

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

                    var userMsg = msg as SocketUserMessage;
                    var txtChannel = msg?.Channel as ITextChannel;
                    if (txtChannel == null || txtChannel.Guild != _guild)
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
                                await _channel.SendMessageAsync(answerText.ToString());                    
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

        public async Task StartCurrentQuestionTimer()
        {
            TokenSource = new CancellationTokenSource();
            //var timerAction = new Action(_emailUtils.CheckEmails);
            //await EmailCheckTimer(timerAction, TimeSpan.FromSeconds(60), TokenSource.Token);
        }

        private async Task StopCurrentQuestionTimer()
        {
            TokenSource.Cancel();
        }
    }
}