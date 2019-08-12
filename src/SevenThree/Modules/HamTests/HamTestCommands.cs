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
using Discord.Addons.Interactive;
using SevenThree.Services;
using System.IO;

namespace SevenThree.Modules
{
    // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
    public class HamTestCommands : InteractiveBase
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

        [Command("start", RunMode = RunMode.Async)]
        public async Task StartQuiz(string args, int numQuestions = 35, [Remainder]string directMessage = null)
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
            ulong id;
            if (Context.Channel is IDMChannel || directMessage != null)
            {
                id = Context.User.Id;
            }
            else
            {
                id = Context.Guild.Id;                
            }               
            await _db.Quiz.AddAsync(
                new Quiz
                {
                    ServerId = id,
                    IsActive = true,
                    TimeStarted = DateTime.Now,
                    StartedById = (long)Context.User.Id,
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
                var quiz = _db.Quiz.Where(q => (ulong)q.ServerId == id && q.IsActive).FirstOrDefault();   
                if (quiz != null)
                {
                    try
                    {
                        await startQuiz.StartGame(quiz, numQuestions, testName).ConfigureAwait(false); 
                    }
                    finally
                    {
                        _hamTestService.RunningTests.TryRemove(id, out startQuiz);
                    } 
                }                                                        
            }
            else
            {
                await ReplyAsync("There is already an active quiz!");
            }        
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopQuiz([Remainder]string args = null)
        {                   
            ulong id;            
            if (Context.Channel is IDMChannel)
            {
                id = (ulong)Context.User.Id;                
            }
            else
            {
                id = (ulong)Context.Guild.Id;                
            }   
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
                        //System.Console.WriteLine($"The answer is {posAnswerText}");
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
                    //System.Console.WriteLine(name);
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

        
    }
}
