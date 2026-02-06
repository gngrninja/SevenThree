using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;
using SevenThree.Database;
using SevenThree.Models;

namespace SevenThree.Services
{
    public class StudyService
    {
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;
        private readonly ILogger<StudyService> _logger;

        // Cache for active study sessions (flashcard mode)
        private readonly ConcurrentDictionary<string, StudySession> _sessions = new();

        public StudyService(
            IDbContextFactory<SevenThreeContext> contextFactory,
            ILogger<StudyService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Get questions the user answered incorrectly
        /// </summary>
        public async Task<List<MissedQuestion>> GetMissedQuestionsAsync(ulong userId, StudyScope scope)
        {
            using var db = _contextFactory.CreateDbContext();

            var query = db.UserAnswer
                .Include(ua => ua.Question)
                    .ThenInclude(q => q.Test)
                .Where(ua => ua.UserId == (long)userId && !ua.IsAnswer);

            if (scope == StudyScope.Last)
            {
                // Get the user's most recent quiz
                var lastQuizId = await db.UserAnswer
                    .Where(ua => ua.UserId == (long)userId)
                    .OrderByDescending(ua => ua.Quiz.TimeStarted)
                    .Select(ua => ua.Quiz.QuizId)
                    .FirstOrDefaultAsync();

                if (lastQuizId == 0)
                    return new List<MissedQuestion>();

                query = query.Where(ua => ua.Quiz.QuizId == lastQuizId);
            }

            var results = await query
                .Select(ua => new MissedQuestion
                {
                    QuestionId = ua.Question.QuestionId,
                    QuestionText = ua.Question.QuestionText,
                    QuestionSection = ua.Question.QuestionSection,
                    SubelementName = ua.Question.SubelementName,
                    SubelementDesc = ua.Question.SubelementDesc,
                    FccPart = ua.Question.FccPart,
                    FigureName = ua.Question.FigureName,
                    TestName = ua.Question.Test.TestName,
                    UserAnswer = ua.AnswerText,
                    TimesAsked = 1, // Will be aggregated below
                    TimesMissed = 1
                })
                .ToListAsync();

            // Get correct answers for each question
            var questionIds = results.Select(r => r.QuestionId).Distinct().ToList();
            var correctAnswers = await db.Answer
                .Where(a => questionIds.Contains(a.Question.QuestionId) && a.IsAnswer)
                .Select(a => new { a.Question.QuestionId, a.AnswerText })
                .ToDictionaryAsync(a => a.QuestionId, a => a.AnswerText);

            foreach (var result in results)
            {
                result.CorrectAnswer = correctAnswers.GetValueOrDefault(result.QuestionId, "Not available");
            }

            // Deduplicate and count occurrences
            var grouped = results
                .GroupBy(q => q.QuestionId)
                .Select(g => new MissedQuestion
                {
                    QuestionId = g.Key,
                    QuestionText = g.First().QuestionText,
                    QuestionSection = g.First().QuestionSection,
                    SubelementName = g.First().SubelementName,
                    SubelementDesc = g.First().SubelementDesc,
                    FccPart = g.First().FccPart,
                    FigureName = g.First().FigureName,
                    TestName = g.First().TestName,
                    CorrectAnswer = g.First().CorrectAnswer,
                    UserAnswer = g.First().UserAnswer,
                    TimesAsked = g.Count(),
                    TimesMissed = g.Count()
                })
                .OrderBy(q => q.QuestionSection)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Get questions the user has missed multiple times (weak areas)
        /// </summary>
        public async Task<List<MissedQuestion>> GetWeakQuestionsAsync(ulong userId)
        {
            var allMissed = await GetMissedQuestionsAsync(userId, StudyScope.All);
            return allMissed
                .Where(q => q.TimesMissed >= StudyConstants.WEAK_THRESHOLD)
                .OrderByDescending(q => q.TimesMissed)
                .ToList();
        }

        /// <summary>
        /// Get stats broken down by subelement
        /// </summary>
        public async Task<UserStudyStats> GetUserStatsAsync(ulong userId)
        {
            using var db = _contextFactory.CreateDbContext();

            var answers = await db.UserAnswer
                .Include(ua => ua.Question)
                    .ThenInclude(q => q.Test)
                .Where(ua => ua.UserId == (long)userId)
                .Select(ua => new
                {
                    ua.Question.SubelementName,
                    ua.Question.SubelementDesc,
                    ua.Question.Test.TestName,
                    ua.IsAnswer
                })
                .ToListAsync();

            if (answers.Count == 0)
                return new UserStudyStats { TotalAnswered = 0 };

            var stats = new UserStudyStats
            {
                TotalAnswered = answers.Count,
                TotalCorrect = answers.Count(a => a.IsAnswer),
                SubelementStats = answers
                    .GroupBy(a => new { a.TestName, a.SubelementName, a.SubelementDesc })
                    .Select(g => new SubelementStat
                    {
                        TestName = g.Key.TestName,
                        SubelementName = g.Key.SubelementName ?? "Unknown",
                        SubelementDesc = g.Key.SubelementDesc ?? "No description",
                        TotalAnswered = g.Count(),
                        TotalCorrect = g.Count(x => x.IsAnswer),
                        PercentCorrect = g.Count() > 0
                            ? Math.Round((double)g.Count(x => x.IsAnswer) / g.Count() * 100, 1)
                            : 0
                    })
                    .OrderBy(s => s.PercentCorrect)
                    .ToList()
            };

            stats.OverallPercent = stats.TotalAnswered > 0
                ? Math.Round((double)stats.TotalCorrect / stats.TotalAnswered * 100, 1)
                : 0;

            return stats;
        }

        /// <summary>
        /// Get all answers for a question to display in flashcard/retry mode
        /// </summary>
        public async Task<List<AnswerOption>> GetAnswersForQuestionAsync(int questionId)
        {
            using var db = _contextFactory.CreateDbContext();

            return await db.Answer
                .Where(a => a.Question.QuestionId == questionId)
                .Select(a => new AnswerOption
                {
                    AnswerId = a.AnswerId,
                    AnswerText = a.AnswerText,
                    IsCorrect = a.IsAnswer
                })
                .ToListAsync();
        }

        #region Session Management

        public string CreateSession(ulong userId, List<MissedQuestion> questions)
        {
            var sessionId = Guid.NewGuid().ToString("N")[..8];
            var session = new StudySession
            {
                SessionId = sessionId,
                UserId = userId,
                Questions = questions,
                CurrentIndex = 0,
                CreatedAt = DateTime.UtcNow,
                ShowingAnswer = false
            };

            _sessions[sessionId] = session;
            CleanupExpiredSessions();

            return sessionId;
        }

        public StudySession GetSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (DateTime.UtcNow - session.CreatedAt < TimeSpan.FromMinutes(StudyConstants.SESSION_CACHE_MINUTES))
                {
                    return session;
                }
                _sessions.TryRemove(sessionId, out _);
            }
            return null;
        }

        public void UpdateSession(StudySession session)
        {
            _sessions[session.SessionId] = session;
        }

        public void RemoveSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        private void CleanupExpiredSessions()
        {
            var expiry = DateTime.UtcNow.AddMinutes(-StudyConstants.SESSION_CACHE_MINUTES);
            var expired = _sessions.Where(kvp => kvp.Value.CreatedAt < expiry).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
            {
                _sessions.TryRemove(key, out _);
            }
        }

        #endregion
    }
}
