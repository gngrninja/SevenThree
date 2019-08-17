using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SevenThree.Database;
using SevenThree.Models;
using SevenThree.Services;
using System.IO;

namespace SevenThree.Modules
{
    // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
    public class HamTestCommands : ModuleBase
    {
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;
        private readonly IServiceProvider _services;     
        private readonly HamTestService _hamTestService;

        public HamTestCommands(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _db = services.GetRequiredService<SevenThreeContext>();
            _services = services;            
            _hamTestService = services.GetRequiredService<HamTestService>();
        }

        [Command("tech", RunMode = RunMode.Async)]
        public async Task StartTech()
        {
            ulong id = GetId();
            await StartTest(numQuestions: 35, questionDelay: 45, directMessage: null, testName: "tech", id: id);
        }

        [Command("general", RunMode = RunMode.Async)]
        public async Task StartGeneral()
        {
            ulong id = GetId();
            await StartTest(numQuestions: 35, questionDelay: 45, directMessage: null, testName: "general", id: id);
        }

        [Command("extra", RunMode = RunMode.Async)]
        public async Task StartExtra()
        {
            ulong id = GetId();
            await StartTest(numQuestions: 35, questionDelay: 45, directMessage: null, testName: "extra", id: id);
        }

        [Command("start", RunMode = RunMode.Async)]
        public async Task StartQuiz(string args, int numQuestions = 35, int questionDelay = 45, [Remainder]string directMessage = null)
        {
            if (args == null)
            {
                await ReplyAsync("Please specify tech, general, or extra!");
                return;
            }
            var testName = string.Empty;
            
            switch (args.ToLower())
            {
                case "tech":
                    {
                        testName = "tech";
                        break;
                    }
                case "general":
                    {
                        testName = "general";
                        break;
                    }
                case "extra":
                    {
                        testName = "extra";
                        break;
                    }
                default:
                    {
                        await ReplyAsync("Please specify tech, general, or extra!");
                        return;
                    }
            }                        
            if (questionDelay > 120)
            {
                questionDelay = 120;
                await ReplyAsync("Question delay set to 120 seconds, as that is the max.");
            }
            else if (questionDelay < 15)
            {
                questionDelay = 15;
                await ReplyAsync("Question delay set to 15 seconds, as that is the minimum");
            }

            ulong id = 0;
            if (!string.IsNullOrEmpty(directMessage))
            {
                id = GetId(directMessage);
            }
            else
            {
                id = GetId();
            }
            
            await StartTest(numQuestions, questionDelay, directMessage, testName, id);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopQuiz()
        {                               
            ulong id = GetId();
            var quiz = await _db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefaultAsync();  
            if (quiz != null)
            {                  
                var gUser = Context.User as IGuildUser;                        
                if (gUser != null && Context.User.Id != quiz.StartedById && !gUser.GuildPermissions.KickMembers)
                {
                    await ReplyAsync($"Sorry, {Context.User.Mention}, a test can only be stopped by the person who started it, or by someone with at least **KickMembers** permissions in {Context.Guild.Name}!");
                    return;
                }
                else if (quiz != null && gUser != null && gUser.GuildPermissions.KickMembers)
                {
                    QuizUtil trivia = null;
                    if (_hamTestService.RunningTests.TryRemove(id, out trivia))
                    {                
                        await trivia.StopQuiz().ConfigureAwait(false);                                
                    }
                    else
                    {
                        await ReplyAsync("No quiz to end!");
                    }          
                    return;      
                }
                if (Context.User.Id == quiz.StartedById)
                {
                    QuizUtil trivia = null;
                    if (_hamTestService.RunningTests.TryRemove(id, out trivia))
                    {                
                        await trivia.StopQuiz().ConfigureAwait(false);                                
                    }
                    else
                    {
                        await ReplyAsync("No quiz to end!");
                    }     
                }
                else
                {
                    await ReplyAsync($"Sorry, {Context.User.Mention}, a test can only be stopped by the person who started it! (or by a moderator)");
                }                               
            } 
            else
            {
                await ReplyAsync("No quiz to end!");
            }
        }                

        [Command("import", RunMode = RunMode.Async)]
        [RequireOwner]
        public async Task ImportQuestions([Remainder]string args = null)
        {    
            if (args == null)
            {
                await ReplyAsync("Please specify tech, general, or extra!");
                return;
            }

            var testName = string.Empty;
            var testDesc = string.Empty;

            switch (args.ToLower())
            {
                case "tech":
                {
                    testName = "tech";
                    testDesc = "U.S. Ham Radio test for the technician class license.";
                    break;
                }
                case "general":
                {
                    testName = "general";
                    testDesc = "U.S. Ham Radio test for the general class license.";
                    break;
                }
                case "extra":
                {
                    testName = "extra";
                    testDesc = "U.S. Ham Radio test for the extra class license.";
                    break;
                }               
                default:
                {
                   await ReplyAsync("Please specify tech, general, or extra!");
                   return; 
                } 
            }

            var test = _db.HamTest.Where(t => t.TestName == testName).FirstOrDefault();

            if (test == null)
            {
                await _db.AddAsync(
                    new HamTest{
                        TestName = testName,
                        TestDescription = testDesc
                    });
                await _db.SaveChangesAsync();
            } 
            else {

                //clear old test items
                var figures = _db.Figure.Where(f => f.Test.TestName == testName).ToList();
                if (figures != null)
                {
                    foreach (var figure in figures)
                    {
                        _db.Remove(figure);
                    }
                    await _db.SaveChangesAsync();
                }
                var answers = _db.Answer.Where(a => a.Question.Test.TestName == testName).ToList();
                if (answers != null)
                {
                    foreach (var answer in answers)
                    {
                        _db.Remove(answer);
                    }
                    await _db.SaveChangesAsync();
                }
                var questions = _db.Questions.Where(q => q.Test.TestName == testName).ToList();
                if (questions != null)
                {
                    foreach (var questionItem in questions)
                    {
                        _db.Remove(questionItem);
                    }
                    await _db.SaveChangesAsync();
                }
            }

            test = _db.HamTest.Where(t => t.TestName == testName).FirstOrDefault();
            
            //get questions converted from json to C# objects
            var question = QuestionIngest.FromJson(File.ReadAllText($"import/{testName}/{testName}.json"));             
            
            //loop through and add to the database
            foreach (var item in question)
            {                
                var questionText = item.Question;
                var answerchar   = Char.Parse(item.AnswerKey.ToString());
                var questionId   = item.QuestionId;
                var fccPart      = item.FccPart;
                var subDesc      = item.SubelementDesc;
                var subName      = item.SubelementName;

                if (string.IsNullOrEmpty(fccPart))
                {
                    await _db.AddAsync(
                    new Questions{
                        QuestionText    = questionText,
                        QuestionSection = questionId,  
                        SubelementDesc  = subDesc,
                        FigureName      = item.Figure,
                        SubelementName  = subName.ToString(),
                        Test = test                      
                    });
                }
                else
                {
                    await _db.AddAsync(
                    new Questions{
                        QuestionText    = questionText,
                        QuestionSection = questionId,
                        FccPart         = fccPart,
                        SubelementDesc  = subDesc,
                        FigureName      = item.Figure,
                        SubelementName  = subName.ToString(),
                        Test = test                         
                    });
                }

                //save question to db
                await _db.SaveChangesAsync();

                //get the current question we just added, so we can associate the answer
                var curQuestion = _db.Questions.Where(q => q.QuestionSection == item.QuestionId).FirstOrDefault();
                
                //iterate through all the answers and add them
                foreach (var answer in item.PossibleAnswer)
                {
                    bool isAnswer = false;
                    var posAnswerText = answer.Substring(3);
                    var posAnswerChar = answer.Substring(0, 1);
                    if (answerchar == Char.Parse(posAnswerChar))
                    {                        
                        isAnswer = true;
                    }                 
                    await _db.AddAsync(
                        new Answer{
                            Question   = curQuestion,
                            AnswerText = posAnswerText,
                            IsAnswer   = isAnswer
                    });
                }
                await _db.SaveChangesAsync();
            }
            
            var files = Directory.EnumerateFiles($"import/{testName}",$"{testName}_*.png");

            if (test != null)
            {
                foreach (var file in files)
                {
                    string curFigure = file;                
                    if (File.Exists(curFigure))
                    {                    
                        var contents = File.ReadAllBytes(curFigure);

                        await _db.AddAsync(
                        new Figure{
                            Test        = test,
                            FigureName  = file.Split('_')[1].Replace(".png","").Trim(),
                            FigureImage = contents
                        });                    

                        await _db.SaveChangesAsync();
                    }
                }
            }                       
            await ReplyAsync($"Imported {testName} into the database!");
        }    

        private async Task StartTest(
            int numQuestions, 
            int questionDelay, 
            string directMessage, 
            string testName, 
            ulong id
        )
        {            
            if (Context.Channel is IDMChannel || directMessage != null)
            {
                var channelInfo = await _db.QuizSettings.Where(q => q.DiscordGuildId == Context.Guild.Id).FirstOrDefaultAsync();
                switch (testName)
                {
                    case "tech":
                    {
                        if (channelInfo != null && channelInfo.TechChannelId != null && channelInfo.TechChannelId != Context.Channel.Id)
                        {
                            var goodChan = await Context.Guild.GetChannelAsync((ulong)channelInfo.TechChannelId);
                            await ReplyAsync($"Tech test commands cannot be used in this channel, please use them in [#{goodChan.Name}]!");                            
                            return;
                        }
                        break;
                    }
                    case "general":
                    {
                        if (channelInfo != null && channelInfo.GeneralChannelId != null && channelInfo.GeneralChannelId != Context.Channel.Id)
                        {                        
                            var goodChan = await Context.Guild.GetChannelAsync((ulong)channelInfo.GeneralChannelId);
                            await ReplyAsync($"General test commands cannot be used in this channel, please use them in [#{goodChan.Name}]!");                            
                            return;
                        }
                        break;
                    }
                    case "extra":
                    {
                        if (channelInfo != null && channelInfo.ExtraChannelId != null && channelInfo.ExtraChannelId != Context.Channel.Id)
                        {
                            var goodChan = await Context.Guild.GetChannelAsync((ulong)channelInfo.ExtraChannelId);
                            await ReplyAsync($"Extra test commands cannot be used in this channel, please use them in [#{goodChan.Name}]!");                            
                            return;
                        }                    
                        break;
                    }
                }            
            }
            var checkQuiz = _db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefault();
            if (checkQuiz == null)
            {
                await _db.Quiz.AddAsync(
                    new Quiz
                    {
                        ServerId = id,
                        IsActive = true,
                        TimeStarted = DateTime.Now,
                        StartedById = Context.User.Id,
                        StartedByName = Context.User.Username,
                        StartedByIconUrl = Context.User.GetAvatarUrl()
                    });
                await _db.SaveChangesAsync();
                QuizUtil startQuiz = null;
                if (Context.Channel is IDMChannel || directMessage != null)
                {
                    startQuiz = new QuizUtil(
                        user: Context.User as IUser,
                        services: _services,
                        guild: Context.Guild as IGuild,
                        id: id
                    );
                }
                else
                {
                    startQuiz = new QuizUtil(
                        channel: Context.Channel as ITextChannel,
                        services: _services,
                        guild: Context.Guild as IGuild,
                        id: id
                    );
                }
                if (_hamTestService.RunningTests.TryAdd(id, startQuiz))
                {
                    var quiz = await _db.Quiz.Where(q => q.ServerId == id && q.IsActive).FirstOrDefaultAsync();
                    try
                    {
                        await startQuiz.StartGame(quiz, numQuestions, testName, questionDelay * 1000).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{ex.Message}");
                    }
                }
                else
                {
                    _logger.LogInformation($"server n{Context.Guild.Name} i{Context.Guild.Id} -> Dictionary");
                    await ReplyAsync("There is already an active quiz!");
                }
            }
            else
            {
                _logger.LogInformation($"server n{Context.Guild.Name} i{Context.Guild.Id} -> DB");
                await ReplyAsync("There is already an active quiz!");
            }
        } 

        private ulong GetId(string directMessage)
        {
            ulong id;
            if (Context.Channel is IDMChannel || directMessage.ToLower() == "private")
            {
                id = Context.User.Id;
            }
            else
            {
                id = Context.Guild.Id;
            }

            return id;
        }

        private ulong GetId()
        {
            ulong id;
            if (Context.Channel is IDMChannel)
            {
                id = Context.User.Id;
            }
            else
            {
                id = Context.Guild.Id;
            }

            return id;
        }                           
    }
}
