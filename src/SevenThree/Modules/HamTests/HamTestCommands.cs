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
                id = (ulong)Context.User.Id;                
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
                var startQuiz = new QuizUtil(
                    channel: Context.Channel as ITextChannel, 
                    services: _services,
                    guild: Context.Guild as IGuild,
                    questions: questions
                ); 
                await startQuiz.StartGame();
            }
            else if (quiz.IsActive)
            {
                await ReplyAsync("There is already an active quiz!");
            }     
            /*               
            var random    = new Random();
            var questions = await _db.Questions.Include(q => q.Test).ToListAsync();
            var question  = questions[random.Next(questions.Count())];
            var answers   = await _db.Answer.Where(a => a.Question.QuestionId == question.QuestionId).ToListAsync();
            var sb        = new StringBuilder();

            var embed = new EmbedBuilder();

            embed.Title = $"Question [{question.QuestionSection}] from test: [{question.Test.TestName}]!";            
            embed.WithColor(new Color(0, 255, 0));
            
            if (question.FccPart != null)
            {
                embed.AddField(new EmbedFieldBuilder{
                    Name  = "FCC Part",
                    Value = question.FccPart 
                });
            }

            embed.AddField(new EmbedFieldBuilder{
                Name  = $"Subelement [**{question.SubelementName}**]",
                Value = question.SubelementDesc
            });   

            embed.AddField(new EmbedFieldBuilder{
                Name     = $"Question:",
                Value    = question.QuestionText,
                IsInline = false                
            });
                             
            //associate answers with letters (randomly)
            var answerOptions = new List<Tuple<char, Answer>>();               
            var letters       = new List<char>(){'A','B','C','D'};
            var usedNumbers   = new List<int>();
            var usedLetters   = new List<char>();
            
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
               
            await ReplyAsync("",false,embed.Build());               

            bool answered = false;
            
            do 
            {
                var response = await NextMessageAsync();
                char? answerChar = null;
                try 
                {
                    answerChar = char.Parse(response.Content.ToUpper());
                }
                catch(Exception ex)
                {
                    System.Console.WriteLine($"Error [{ex.Message}]!");
                }
                if (answerChar.HasValue)
                {
                    var possibleAnswer = answerOptions.Where(w => w.Item1 == answerChar).Select(w => w.Item2).FirstOrDefault();
                    if (possibleAnswer.IsAnswer)
                    {
                        var answerText = new StringBuilder();
                        answerText.AppendLine($"Congrats -> **[{response.Author.Mention}]** <-!");
                        answerText.AppendLine($"You had the correct answer of [*{answerChar}*] -> [**{possibleAnswer.AnswerText}**]");
                        await ReplyAsync(answerText.ToString());                    
                        answered = true;
                    }  
                }              
            }
            while (!answered);    
            */        
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
            /*               
            var random    = new Random();
            var questions = await _db.Questions.Include(q => q.Test).ToListAsync();
            var question  = questions[random.Next(questions.Count())];
            var answers   = await _db.Answer.Where(a => a.Question.QuestionId == question.QuestionId).ToListAsync();
            var sb        = new StringBuilder();

            var embed = new EmbedBuilder();

            embed.Title = $"Question [{question.QuestionSection}] from test: [{question.Test.TestName}]!";            
            embed.WithColor(new Color(0, 255, 0));
            
            if (question.FccPart != null)
            {
                embed.AddField(new EmbedFieldBuilder{
                    Name  = "FCC Part",
                    Value = question.FccPart 
                });
            }

            embed.AddField(new EmbedFieldBuilder{
                Name  = $"Subelement [**{question.SubelementName}**]",
                Value = question.SubelementDesc
            });   

            embed.AddField(new EmbedFieldBuilder{
                Name     = $"Question:",
                Value    = question.QuestionText,
                IsInline = false                
            });
                             
            //associate answers with letters (randomly)
            var answerOptions = new List<Tuple<char, Answer>>();               
            var letters       = new List<char>(){'A','B','C','D'};
            var usedNumbers   = new List<int>();
            var usedLetters   = new List<char>();
            
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
               
            await ReplyAsync("",false,embed.Build());               

            bool answered = false;
            
            do 
            {
                var response = await NextMessageAsync();
                char? answerChar = null;
                try 
                {
                    answerChar = char.Parse(response.Content.ToUpper());
                }
                catch(Exception ex)
                {
                    System.Console.WriteLine($"Error [{ex.Message}]!");
                }
                if (answerChar.HasValue)
                {
                    var possibleAnswer = answerOptions.Where(w => w.Item1 == answerChar).Select(w => w.Item2).FirstOrDefault();
                    if (possibleAnswer.IsAnswer)
                    {
                        var answerText = new StringBuilder();
                        answerText.AppendLine($"Congrats -> **[{response.Author.Mention}]** <-!");
                        answerText.AppendLine($"You had the correct answer of [*{answerChar}*] -> [**{possibleAnswer.AnswerText}**]");
                        await ReplyAsync(answerText.ToString());                    
                        answered = true;
                    }  
                }              
            }
            while (!answered);    
            */        
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
