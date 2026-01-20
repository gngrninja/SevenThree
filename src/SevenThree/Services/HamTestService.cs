using System.Threading.Tasks;
using SevenThree.Modules;
using System.Collections.Concurrent;
using SevenThree.Database;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SevenThree.Services
{
    public class HamTestService
    {
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        public ConcurrentDictionary<ulong, QuizUtil> RunningTests { get; set; }

        public HamTestService(IDbContextFactory<SevenThreeContext> contextFactory)
        {
            _contextFactory = contextFactory;
            RunningTests = new ConcurrentDictionary<ulong, QuizUtil>();
            _ = QuizCleanupAsync();
        }

        private async Task QuizCleanupAsync()
        {
            using var db = _contextFactory.CreateDbContext();
            var quizzes = await db.Quiz.ToListAsync();
            foreach (var quiz in quizzes.Where(q => q.IsActive))
            {
                quiz.IsActive = false;
            }
            foreach (var quiz in quizzes)
            {
                var userAnswers = await db.UserAnswer.Where(u => u.Quiz.QuizId == quiz.QuizId).ToListAsync();
                if (userAnswers != null)
                {
                    foreach (var answer in userAnswers)
                    {
                        db.Remove(answer);
                    }
                }
            }
            await db.SaveChangesAsync();
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