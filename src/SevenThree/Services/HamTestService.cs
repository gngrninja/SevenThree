using System.Threading.Tasks;
using SevenThree.Modules;
using System.Collections.Concurrent;
using SevenThree.Database;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SevenThree.Services
{
    public class HamTestService
    {
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;
        private readonly ILogger<HamTestService> _logger;
        private bool _initialized;

        public ConcurrentDictionary<ulong, QuizUtil> RunningTests { get; }

        public HamTestService(
            IDbContextFactory<SevenThreeContext> contextFactory,
            ILogger<HamTestService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            RunningTests = new ConcurrentDictionary<ulong, QuizUtil>();
        }

        /// <summary>
        /// Initialize the service by cleaning up stale quizzes from previous runs.
        /// Should be called during application startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                await CleanupStaleQuizzesAsync();
                _initialized = true;
                _logger.LogInformation("HamTestService initialized successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize HamTestService");
                throw;
            }
        }

        private async Task CleanupStaleQuizzesAsync()
        {
            using var db = _contextFactory.CreateDbContext();

            // Only clean up active quizzes (stale from previous crash/restart)
            var activeQuizzes = await db.Quiz.Where(q => q.IsActive).ToListAsync();

            if (activeQuizzes.Count == 0)
            {
                _logger.LogDebug("No stale quizzes to clean up");
                return;
            }

            _logger.LogInformation("Cleaning up {Count} stale quizzes", activeQuizzes.Count);

            foreach (var quiz in activeQuizzes)
            {
                quiz.IsActive = false;

                // Only remove user answers for stale quizzes, not all historical data
                var orphanedAnswers = await db.UserAnswer
                    .Where(u => u.Quiz.QuizId == quiz.QuizId)
                    .ToListAsync();

                db.RemoveRange(orphanedAnswers);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Stale quiz cleanup complete");
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