using System;
using System.Collections.Generic;
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

        // In-memory cache of all question pools (HamTest rows), refreshed at startup and after /import.
        // Autocomplete reads this instead of the DB: autocomplete cannot defer and must answer within
        // Discord's 3-second window, so a remote DB call there risks "Unknown interaction" (10062).
        private volatile IReadOnlyList<HamTest> _pools = Array.Empty<HamTest>();

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
        /// Initialize the service by cleaning up stale quizzes from previous runs and warming
        /// caches/hot paths. Should be called during application startup (before the gateway connects).
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                await CleanupStaleQuizzesAsync();
                await RefreshPoolCacheAsync();   // also warms the EF model + HamTest query + connection pool
                await WarmUpAsync();             // warm remaining hot query shapes off the interaction path
                _initialized = true;
                _logger.LogInformation("HamTestService initialized successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize HamTestService");
                throw;
            }
        }

        /// <summary>
        /// Reloads the in-memory question-pool cache from the database. Called at startup and after
        /// an import, so autocomplete never makes a (remote) DB call on the un-deferrable path.
        /// </summary>
        public async Task RefreshPoolCacheAsync()
        {
            using var db = _contextFactory.CreateDbContext();
            _pools = await db.HamTest.AsNoTracking().ToListAsync();
            _logger.LogDebug("Pool cache refreshed: {Count} pools", _pools.Count);
        }

        /// <summary>All cached question pools.</summary>
        public IReadOnlyList<HamTest> GetPools() => _pools;

        /// <summary>Cached pools for a license type (tech/general/extra).</summary>
        public IReadOnlyList<HamTest> GetPools(string testName) =>
            _pools.Where(p => p.TestName == testName).ToList();

        /// <summary>
        /// Warms cold first-use paths (JIT, EF query compilation, connection pool) off the
        /// interaction path so the first real command/autocomplete answers inside Discord's 3s window.
        /// </summary>
        private async Task WarmUpAsync()
        {
            try
            {
                using var db = _contextFactory.CreateDbContext();
                // Warm the UserAnswer query shape used by /study autocomplete (user-specific, not cached).
                await db.UserAnswer.AsNoTracking().Select(u => u.UserId).Take(1).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warm-up query failed (non-fatal)");
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
