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
        private List<Tuple<Emoji, char>> _emojiList;        
        private List<IUser> _skipUsers;

        public IMessage CurMessage { get; private set; }        
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
            _logger = services.GetRequiredService<ILogger<QuizUtil>>();
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
            _emojiList = new List<Tuple<Emoji, char>>
            {
                Tuple.Create(new Emoji("🇦"), 'A'),
                Tuple.Create(new Emoji("🇧"), 'B'),
                Tuple.Create(new Emoji("🇨"), 'C'),
                Tuple.Create(new Emoji("🇩"), 'D'),
                Tuple.Create(new Emoji("\u23E9"),'S')
            };  
            _skipUsers = new List<IUser>();                         
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
            _emojiList = new List<Tuple<Emoji, char>>
            {
                Tuple.Create(new Emoji("🇦"), 'A'),
                Tuple.Create(new Emoji("🇧"), 'B'),
                Tuple.Create(new Emoji("🇨"), 'C'),
                Tuple.Create(new Emoji("🇩"), 'D'),
                Tuple.Create(new Emoji("\u23E9"),'S'),                
            };          
            _skipUsers = new List<IUser>();                           
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
                        await SendQuestionWithReactions(embed, hasFigure: true);
                    }
                    else
                    {
                        await SendQuestionWithReactions(embed, hasFigure: false);
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
                    //listen for answers via reactions                      
                    _client.ReactionAdded += ListenForReactionAdded;
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
                    _client.ReactionAdded -= ListenForReactionAdded;
                    IsActive = false;   
                    if (!ShouldStopTest)
                    {
                        await SendQuestionResults();                         
                    }          
                               
                } 
                await Task.Delay(5000).ConfigureAwait(false);                             
            }                                        
        }

        private async Task SendQuestionResults()
        {
            var message = CurMessage as IUserMessage;
            var answers = await _db.UserAnswer.Where(a => a.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();
            var sb = new StringBuilder();
            var usersWon = await GetCorrectUsersFromQuestion();
            var usersLost = await GetIncorrectUsersFromQuestion();
            var embed = new EmbedBuilder();
            embed.Title = $"Question [{CurrentQuestion.QuestionSection}] Results";
            embed.AddField
            (
                new EmbedFieldBuilder
                {
                    Name = "Question:",
                    Value = $"**{CurrentQuestion.QuestionText}**"
                }
            );
            embed.AddField
            (
                new EmbedFieldBuilder
                {
                    Name = "Answer:",
                    Value = $"**{Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault().Item1}**. *{Answers.Where(a => a.Item2.IsAnswer).FirstOrDefault().Item2.AnswerText}*"
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
                            Text = $"[{guildUser.Username}] is currently in the lead with [{numCorrect}] correct answers!",
                            IconUrl = guildUser.GetAvatarUrl()                        
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
                            IconUrl = _user.GetAvatarUrl()                        
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
            await SendReplyAsync(embed, false);
        }

        private Task ListenForReactionAdded(Cacheable<IUserMessage, ulong> question, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var _ = Task.Run(async () =>
            {                
                try
                {                    
                    if (reaction.User.Value.IsBot)
                    {
                        return;
                    }            
                    var answered = false;        
                    await _guessLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (IsActive && !_tokenSource.IsCancellationRequested)
                        {
                            if (question.Id == CurMessage.Id)
                            {              
                                if (_skipUsers.Count > 0)
                                {
                                    var numSkip = question.Value.Reactions.Where(r => r.Key.Name == _emojiList.Last().Item1.Name).FirstOrDefault().Value.ReactionCount - 1;
                                    var skipChar = _emojiList.Where(e => reaction.Emote.Name == e.Item1.Name).FirstOrDefault();                                    
                                    if (skipChar.Item2 == 'S' && _skipUsers.Contains(reaction.User.Value) && numSkip == _skipUsers.Count)
                                    {
                                        _tokenSource.Cancel();
                                        return;
                                    } 
                                }                      
                                UserAnswer userAnswered = null;
                                userAnswered = await _db.UserAnswer.Where(a => a.Question == CurrentQuestion && a.UserId == (long)reaction.User.Value.Id && a.Quiz.QuizId == Quiz.QuizId).FirstOrDefaultAsync();                            
                                char? answerChar = null;
                                if (userAnswered == null)
                                {
                                    answerChar = _emojiList.Where(e => e.Item1.Name == reaction.Emote.Name).FirstOrDefault().Item2;                                                                                                        
                                    _logger.LogInformation($"User answer -> {answerChar}");  
                                    var possibleAnswer = Answers.Where(w => w.Item1 == answerChar).Select(w => w.Item2).FirstOrDefault();                                    
                                    if (possibleAnswer.IsAnswer)
                                    {
                                        answered = await UserAnsweredCorrectly(reaction.User.Value, answered, answerChar, possibleAnswer);
                                        if (!_skipUsers.Contains(reaction.User.Value))
                                        {
                                            _skipUsers.Add(reaction.User.Value);
                                        }
                                        
                                    }
                                    else
                                    {
                                        await IncorrectAnswer(reaction.User.Value, answerChar);
                                        if (!_skipUsers.Contains(reaction.User.Value))
                                        {
                                            _skipUsers.Add(reaction.User.Value);
                                        }
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
                    if (_user != null)
                    {
                        _tokenSource.Cancel();
                    }                                     
                }    
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }  
            });
            return Task.CompletedTask;
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
       
        private async Task IncorrectAnswer(SocketMessage msg, char? answerChar)
        {
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

        private async Task IncorrectAnswer(IUser user, char? answerChar)
        {
            _db.UserAnswer.Add(new UserAnswer
            {
                UserId = (long)user.Id,
                UserName = user.Username,
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

        private async Task<bool> UserAnsweredCorrectly(IUser user, bool answered, char? answerChar, Answer possibleAnswer)
        {            
            _db.UserAnswer.Add(
                new UserAnswer
                {
                    UserId = (long)user.Id,
                    UserName = user.Username,
                    Question = CurrentQuestion,
                    AnswerText = answerChar.ToString(),
                    Quiz = Quiz,
                    IsAnswer = true
                });
            await _db.SaveChangesAsync();
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

        private Task SendQuestionWithReactions(EmbedBuilder embed, bool hasFigure)
        {
            var _ = Task.Run(async () => 
            {
                var emojiList = new List<Emoji>();
                foreach (var emoji in _emojiList)
                {
                    emojiList.Add(emoji.Item1);
                }
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
                            var message = await _channel.SendFileAsync($"{fileName}", "", false, embed.Build());                            
                            CurMessage = message;
                            await message.AddReactionsAsync(emojiList.Take(4).ToArray());
                            if (_skipUsers.Count > 0)
                            {
                                await message.AddReactionAsync(emojiList.Last());
                            }
                            _messages.Add(message);                
                            File.Delete(fileName);                            
                         }
                    }
                    else
                    {
                        var message = await _channel.SendMessageAsync(null, false, embed.Build());
                        CurMessage = message;
                        await message.AddReactionsAsync(emojiList.Take(4).ToArray());
                        if (_skipUsers.Count > 0)
                        {
                            await message.AddReactionAsync(emojiList.Last());
                        }
                        _messages.Add(message);
                        
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
                            var message = await _user.SendFileAsync($"{fileName}", "", false, embed.Build());                            
                            CurMessage = message;
                            File.Delete(fileName); 
                            await message.AddReactionsAsync(emojiList.Take(4).ToArray());      
                            if (_skipUsers.Count > 0)
                            {
                                await message.AddReactionAsync(emojiList.Last());
                            }                                                                             
                            //_messages.Add(message);                                
                         }
                    }
                    else
                    {
                        var message = await _user.SendMessageAsync(null, false, embed.Build());                                             
                        CurMessage = message;
                        await message.AddReactionsAsync(emojiList.Take(4).ToArray());     
                        if (_skipUsers.Count > 0)
                        {
                            await message.AddReactionAsync(emojiList.Last());
                        }                                                                      
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

        private async Task<List<UserAnswer>> GetCorrectUsersFromQuestion()
        {
            var users = await _db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId && u.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();            
            users = users.Where(u => u.IsAnswer).ToList();            
            return users;
        }        

        private async Task<List<UserAnswer>> GetIncorrectUsersFromQuestion()
        {
            var users = await _db.UserAnswer.Where(u => u.Quiz.QuizId == Quiz.QuizId && u.Question.QuestionId == CurrentQuestion.QuestionId).ToListAsync();            
            users = users.Where(u => !u.IsAnswer).ToList();            
            return users;
        } 

        internal async Task StopQuiz()
        {          
            //wrap quiz up here           
            QuizUtil trivia = null;
            _hamTestService.RunningTests.TryRemove(Id, out trivia);
            ShouldStopTest = true;              
            //_client.MessageReceived -= ListenForAnswer;    
            _client.ReactionAdded += ListenForReactionAdded;                             
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
                    string passFailEmoji = string.Empty;
                    foreach (var user in userResults)
                    {                        
                        i++;
                        decimal percentage = ((decimal)user.Item2 / (decimal)_totalQuestions) * 100;
                        if (percentage >= 74)
                        {
                            passFailEmoji = ":white_check_mark:";            
                        }
                        else
                        {
                            passFailEmoji = ":no_entry_sign:";
                        }
                        sb.AppendLine($"{GetNumberEmojiFromInt(i)} [**{users.Where(u => (ulong)u.UserId == user.Item1).FirstOrDefault().UserName}**] with [**{user.Item2}**] [{passFailEmoji}] ({Math.Round(percentage,0)}%)");   
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

        private async Task<List<Questions>> GetRandomQuestions(int numQuestions, string testName, bool figuresOnly = false)        
        {       
            List<Questions> questions = null;     
            if (!figuresOnly)
            {
                questions = await _db.Questions.Include(q => q.Test).Where(q => q.Test.TestName == testName).ToListAsync();
            }
            else
            {
                questions = await _db.Questions.Include(q => q.Test).Where(q => q.Test.TestName == testName && q.FigureName != null).ToListAsync();
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
                var settings = await _db.QuizSettings.Where(s => s.DiscordGuildId == _guild.Id).FirstOrDefaultAsync();
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