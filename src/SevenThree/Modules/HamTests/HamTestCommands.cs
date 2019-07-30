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
using System.IO;

namespace SevenThree.Modules
{
    // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
    public class HamTestCommands : ModuleBase
    {
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;

        public HamTestCommands(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _db = services.GetRequiredService<SevenThreeContext>();
        }

        [Command("question")]
        public async Task QuestionCommand([Remainder]string args = null)
        {            
            var questions = await _db.Questions.Include(q => q.Test).ToListAsync();
            var question  = questions[100];
            var answers   = await _db.Answer.Where(a => a.Question.QuestionId == question.QuestionId).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"questionId -> [{question.QuestionId}]");
            sb.AppendLine($"questionText -> [{question.QuestionText}]");
            
            sb.AppendLine($"TestId -> [{question.Test.TestId}]");
            sb.AppendLine($"TestName -> [{question.Test.TestName}]");
            sb.AppendLine($"TestName -> [{question.Test.TestDescription}]");

            sb.AppendLine("Answers:");

            int i = 0;
            foreach (var answer in answers)
            {
                var prefix = "a";
                switch (i)
                {
                    case 0:
                    {
                        prefix = "a";
                        break;
                    }
                    case 1:
                    {
                        prefix = "b";
                        break;
                    }
                    case 2:
                    {
                        prefix = "c";
                        break;
                    }
                    case 3:
                    {
                        prefix = "d";
                        break;
                    }
                }
                sb.AppendLine($"{prefix} -> [{answer.AnswerText}]");
                i++;
            }
            await ReplyAsync(sb.ToString());
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
