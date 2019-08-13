using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SevenThree.Modules;
using System.Collections.Concurrent;
using SevenThree.Database;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SevenThree.Services
{
    public class HamTestService
    {
        private readonly ConcurrentDictionary<ulong, QuizUtil> _tests;
        private readonly SevenThreeContext _db;

        public ConcurrentDictionary<ulong, QuizUtil> RunningTests { get; set; }

        public HamTestService(IServiceProvider services)
        {
            RunningTests = new ConcurrentDictionary<ulong, QuizUtil>();
            _db = services.GetRequiredService<SevenThreeContext>();
            QuizCleanup();
        }

        private async Task QuizCleanup()
        {
            var quizzes = await _db.Quiz.ToListAsync();
            foreach (var quiz in quizzes.Where(q => q.IsActive))
            {
                quiz.IsActive = false;
            }
            foreach (var quiz in quizzes)
            {
                var userAnswers = await _db.UserAnswer.Where(u => u.Quiz.QuizId == quiz.QuizId).ToListAsync();
                if (userAnswers != null)
                {
                    foreach (var answer in userAnswers)
                    {
                        _db.Remove(answer);
                    }
                }                                
            }
            await _db.SaveChangesAsync();                        
        }

        public async Task StopTests()
        {
            foreach (var test in RunningTests)
            {
                await test.Value.StopQuiz().ConfigureAwait(false);
            }
        }
    }
}