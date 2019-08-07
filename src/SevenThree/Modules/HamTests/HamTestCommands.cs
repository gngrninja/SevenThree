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
using System.IO;

namespace SevenThree.Modules
{
    // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
    public class HamTestCommands : InteractiveBase
    {
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;
        private readonly IServiceProvider _services;        

        public HamTestCommands(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _db = services.GetRequiredService<SevenThreeContext>();
            _services = services;            
        }

        [Command("start", RunMode = RunMode.Async)]
        public async Task StartQuiz([Remainder]string args = null)
        {                 
                        
            ulong id;

            if (Context.Channel is IDMChannel)
            {
                id = Context.User.Id;
            }
            else
            {
                id = (ulong)Context.Guild.Id;                
            }   

            var quiz = _db.Quiz.Where(q => (ulong)q.ServerId == id).FirstOrDefault();

            if (quiz == null)
            {
                await _db.Quiz.AddAsync(
                new Quiz{
                    ServerId = id,
                    IsActive = true,          
                    TimeStarted = DateTime.Now                    
                });
                await _db.SaveChangesAsync();
                var questions = await _db.Questions.Include(q => q.Test).ToListAsync();
                await ReplyAsync("Quiz is active!");
                QuizUtil startQuiz = null;
                if (Context.Channel is IDMChannel)
                {
                    startQuiz = new QuizUtil(
                        user: Context.User as IUser, 
                        services: _services,
                        guild: Context.Guild as IGuild,
                        questions: questions,
                        id: id
                    ); 
                } 
                else
                {
                    startQuiz = new QuizUtil(
                        channel: Context.Channel as ITextChannel, 
                        services: _services,
                        guild: Context.Guild as IGuild,
                        questions: questions,
                        id: id
                    ); 
                }
                await startQuiz.StartGame().ConfigureAwait(false);
            }
            else if (quiz.IsActive)
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

            var quiz = _db.Quiz.Where(q => (ulong)q.ServerId == id).FirstOrDefault();

            if (quiz != null && quiz.IsActive)
            {
                quiz.TimeEnded = DateTime.Now;
                quiz.IsActive  = false;
                _db.Remove(quiz);
                await _db.SaveChangesAsync();
                await ReplyAsync("Quiz ended!");
            }
            else
            {
                await ReplyAsync("No quiz to end!");
            }      
        }                

        [Command("import")]
        public async Task ImportQuestions()
        {
            var test = _db.HamTest.FirstOrDefault();
            if (test == null)
            {
                await _db.AddAsync(
                    new HamTest{
                        TestName = "Tech",
                        TestDescription = "Test for technician license"
                    });
                await _db.SaveChangesAsync();
            }
            test = _db.HamTest.FirstOrDefault();

            //get questions converted from json to C# objects
            var question = QuestionIngest.FromJson(File.ReadAllText("tech.json"));             
            
            //loop through and add to the database
            foreach (var item in question)
            {                
                var questionText = item.Question;
                var answerchar   = Char.Parse(item.AnswerKey.ToString());
                var questionId   = item.QuestionId;
                var fccPart      = item.FccPart;
                var subDesc      = item.SubelementDesc;
                var subName      = item.SubelementName;

                //_db.Database.ExecuteSqlCommand("PRAGMA foreign_keys = OFF");
                //_db.Database.ExecuteSqlCommand("DELETE FROM Questions");
                //_db.Database.ExecuteSqlCommand("DELETE FROM Answer");
                //_db.Database.ExecuteSqlCommand("PRAGMA foreign_keys = ON");

                if (string.IsNullOrEmpty(fccPart))
                {
                    await _db.AddAsync(
                    new Questions{
                        QuestionText    = questionText,
                        QuestionSection = questionId,  
                        SubelementDesc  = subDesc,
                        SubelementName  = subName.ToString(),
                        Test = _db.HamTest.FirstOrDefault()                      
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
                        SubelementName  = subName.ToString(),
                        Test = _db.HamTest.FirstOrDefault()                         
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
            await ReplyAsync("yay");
        }        
    }
}
