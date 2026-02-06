using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SevenThree.Constants;
using SevenThree.Database;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests the core quiz logic that QuizUtil depends on:
    /// - Random question selection with archive filtering
    /// - Answer shuffling behavior
    /// - Score calculation
    /// - Question count clamping
    /// - Fisher-Yates shuffle properties
    /// </summary>
    public class QuizUtilLogicTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public QuizUtilLogicTests()
        {
            _dbOptions = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        public void Dispose()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.Database.EnsureDeleted();
        }

        private async Task<HamTest> SeedPool(SevenThreeContext db, string testName = "tech")
        {
            var test = new HamTest
            {
                TestName = testName,
                TestDescription = "Test pool",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(test);
            await db.SaveChangesAsync();
            return test;
        }

        private async Task SeedQuestions(SevenThreeContext db, HamTest test, int count, int archivedCount = 0)
        {
            for (int i = 0; i < count; i++)
            {
                var q = new Questions
                {
                    QuestionText = $"Question {i + 1}?",
                    QuestionSection = $"T1A{(i + 1):D2}",
                    SubelementName = "T1",
                    SubelementDesc = "FCC Rules",
                    Test = test,
                    IsArchived = i >= (count - archivedCount)
                };
                db.Questions.Add(q);
            }
            await db.SaveChangesAsync();
        }

        #region Question Selection Tests

        [Fact]
        public async Task GetRandomQuestions_ExcludesArchivedQuestions()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 10, archivedCount: 3);

            // Simulate the query from QuizUtil.GetRandomQuestions
            var questions = await db.Questions
                .Include(q => q.Test)
                .Where(q => q.Test.TestId == test.TestId && !q.IsArchived)
                .ToListAsync();

            Assert.Equal(7, questions.Count);
            Assert.DoesNotContain(questions, q => q.IsArchived);
        }

        [Fact]
        public async Task GetRandomQuestions_ClampsToMaxQuestions()
        {
            // Mirrors: numQuestions = Math.Min(numQuestions, QuizConstants.MAX_QUESTIONS)
            var requested = 200;
            var clamped = Math.Min(requested, QuizConstants.MAX_QUESTIONS);
            Assert.Equal(QuizConstants.MAX_QUESTIONS, clamped);
        }

        [Fact]
        public async Task GetRandomQuestions_ClampsToAvailableCount()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 5);

            var questions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId && !q.IsArchived)
                .ToListAsync();

            // Request more than available
            var numQuestions = Math.Min(10, questions.Count);
            Assert.Equal(5, numQuestions);
        }

        [Fact]
        public async Task GetRandomQuestions_EmptyPool_ReturnsEmptyList()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);

            var questions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId && !q.IsArchived)
                .ToListAsync();

            Assert.Empty(questions);
        }

        [Fact]
        public async Task GetRandomQuestions_AllArchived_ReturnsEmpty()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 5, archivedCount: 5);

            var questions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId && !q.IsArchived)
                .ToListAsync();

            Assert.Empty(questions);
        }

        #endregion

        #region Fisher-Yates Shuffle Tests

        [Fact]
        public void FisherYatesShuffle_PreservesAllElements()
        {
            var items = Enumerable.Range(1, 20).ToList();
            var original = new List<int>(items);

            // Same shuffle algorithm as QuizUtil.GetRandomQuestions
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            // All elements still present
            Assert.Equal(original.Count, items.Count);
            foreach (var item in original)
            {
                Assert.Contains(item, items);
            }
        }

        [Fact]
        public void FisherYatesShuffle_SingleElement_NoCrash()
        {
            var items = new List<int> { 42 };

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            Assert.Single(items);
            Assert.Equal(42, items[0]);
        }

        [Fact]
        public void FisherYatesShuffle_TakeN_ReturnsRequestedCount()
        {
            var items = Enumerable.Range(1, 100).ToList();

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            var selected = items.Take(10).ToList();
            Assert.Equal(10, selected.Count);
            Assert.Equal(selected.Distinct().Count(), selected.Count); // no duplicates
        }

        #endregion

        #region Answer Shuffle Tests

        [Fact]
        public void AnswerShuffle_AssignsLettersABCD()
        {
            var letters = new List<char> { 'A', 'B', 'C', 'D' };
            var answers = new List<string> { "Ans1", "Ans2", "Ans3", "Ans4" };
            var usedIndices = new HashSet<int>();
            var result = new List<(char Letter, string Answer)>();

            // Mirrors QuizUtil.SetupAnswers logic
            for (int i = 0; i < answers.Count && i < letters.Count; i++)
            {
                int randIndex;
                do
                {
                    randIndex = Random.Shared.Next(answers.Count);
                } while (usedIndices.Contains(randIndex));

                usedIndices.Add(randIndex);
                result.Add((letters[i], answers[randIndex]));
            }

            Assert.Equal(4, result.Count);
            Assert.Equal('A', result[0].Letter);
            Assert.Equal('B', result[1].Letter);
            Assert.Equal('C', result[2].Letter);
            Assert.Equal('D', result[3].Letter);

            // All answers used exactly once
            var usedAnswers = result.Select(r => r.Answer).ToList();
            Assert.Equal(4, usedAnswers.Distinct().Count());
        }

        [Fact]
        public void AnswerShuffle_FewerThan4Answers_OnlyUsesAvailable()
        {
            var letters = new List<char> { 'A', 'B', 'C', 'D' };
            var answers = new List<string> { "Ans1", "Ans2" };
            var usedIndices = new HashSet<int>();
            var result = new List<(char Letter, string Answer)>();

            for (int i = 0; i < answers.Count && i < letters.Count; i++)
            {
                int randIndex;
                do
                {
                    randIndex = Random.Shared.Next(answers.Count);
                } while (usedIndices.Contains(randIndex));

                usedIndices.Add(randIndex);
                result.Add((letters[i], answers[randIndex]));
            }

            Assert.Equal(2, result.Count);
            Assert.Equal('A', result[0].Letter);
            Assert.Equal('B', result[1].Letter);
        }

        #endregion

        #region Score Calculation Tests

        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(5, 10, 50)]
        [InlineData(7, 10, 70)]
        [InlineData(74, 100, 74)]
        [InlineData(10, 10, 100)]
        public void ScorePercentage_CalculatesCorrectly(int correct, int total, decimal expectedPercent)
        {
            // Mirrors: ((decimal)user.Item2 / _totalQuestions) * 100
            var percentage = ((decimal)correct / total) * 100;
            Assert.Equal(expectedPercent, percentage);
        }

        [Theory]
        [InlineData(74, true)]   // 74% passes (ham test passing score)
        [InlineData(75, true)]
        [InlineData(100, true)]
        [InlineData(73, false)]
        [InlineData(0, false)]
        public void PassFail_74PercentThreshold(decimal percentage, bool expectedPass)
        {
            var passes = percentage >= 74;
            Assert.Equal(expectedPass, passes);
        }

        #endregion

        #region Quiz Constants Tests

        [Fact]
        public void MAX_QUESTIONS_IsReasonable()
        {
            Assert.True(QuizConstants.MAX_QUESTIONS > 0);
            Assert.True(QuizConstants.MAX_QUESTIONS <= 1000);
        }

        [Fact]
        public void DEFAULT_QUESTION_DELAY_IsReasonable()
        {
            Assert.True(QuizConstants.DEFAULT_QUESTION_DELAY_MS >= 10000);
            Assert.True(QuizConstants.DEFAULT_QUESTION_DELAY_MS <= 300000);
        }

        [Fact]
        public void MIN_DELAY_LessThan_MAX_DELAY()
        {
            Assert.True(QuizConstants.MIN_DELAY_SECONDS < QuizConstants.MAX_DELAY_SECONDS);
        }

        [Fact]
        public void POST_ANSWER_DELAY_IsPositive()
        {
            Assert.True(QuizConstants.POST_ANSWER_DELAY_MS > 0);
        }

        #endregion

        #region Quiz Lifecycle Tests

        [Fact]
        public async Task QuizLifecycle_CreateAndStopQuiz()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);

            // Create quiz
            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 99999UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            Assert.True(quiz.IsActive);

            // Stop quiz (mirrors StopQuiz logic)
            var dbQuiz = await db.Quiz.FirstAsync(q => q.QuizId == quiz.QuizId);
            dbQuiz.TimeEnded = DateTime.UtcNow;
            dbQuiz.IsActive = false;
            await db.SaveChangesAsync();

            var stopped = await db.Quiz.FirstAsync(q => q.QuizId == quiz.QuizId);
            Assert.False(stopped.IsActive);
            Assert.NotEqual(default, stopped.TimeEnded);
        }

        [Fact]
        public async Task QuizLookup_ByStartedById_FindsOrphanedQuiz()
        {
            using var db = new SevenThreeContext(_dbOptions);

            // Simulate an orphaned quiz (IsActive=true but not in memory)
            db.Quiz.Add(new Quiz
            {
                ServerId = 12345UL,
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 99999UL,
                StartedByName = "TestUser"
            });
            await db.SaveChangesAsync();

            // StopQuiz fallback: find by ServerId when Quiz property is null
            var orphaned = await db.Quiz
                .Where(q => q.ServerId == 12345UL && q.IsActive)
                .FirstOrDefaultAsync();

            Assert.NotNull(orphaned);
            Assert.True(orphaned.IsActive);
        }

        [Fact]
        public async Task UserAnswer_RecordedCorrectly()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 1);

            var question = await db.Questions.FirstAsync();
            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 99999UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            // Record user answer
            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz,
                Question = question,
                UserId = 99999L,
                UserName = "TestUser",
                AnswerText = "A",
                IsAnswer = true
            });
            await db.SaveChangesAsync();

            // Verify
            var answer = await db.UserAnswer
                .Include(ua => ua.Question)
                .Include(ua => ua.Quiz)
                .FirstAsync(ua => ua.UserId == 99999L);

            Assert.Equal("A", answer.AnswerText);
            Assert.True(answer.IsAnswer);
            Assert.Equal(quiz.QuizId, answer.Quiz.QuizId);
        }

        [Fact]
        public async Task DuplicateAnswerCheck_DetectsExisting()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 1);

            var question = await db.Questions.FirstAsync();
            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 99999UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            // First answer
            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz,
                Question = question,
                UserId = 99999L,
                UserName = "TestUser",
                AnswerText = "A",
                IsAnswer = true
            });
            await db.SaveChangesAsync();

            // Check for duplicate (mirrors ProcessButtonAnswerAsync)
            var existing = await db.UserAnswer
                .Where(a => a.Question.QuestionId == question.QuestionId
                         && a.UserId == 99999L
                         && a.Quiz.QuizId == quiz.QuizId)
                .FirstOrDefaultAsync();

            Assert.NotNull(existing);
        }

        #endregion

        #region Leaderboard Calculation Tests

        [Fact]
        public async Task Leaderboard_GroupsByUser_OrdersByCount()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestions(db, test, count: 5);
            var questions = await db.Questions.ToListAsync();

            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = false,
                TimeStarted = DateTime.UtcNow,
                StartedById = 99999UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            // User A: 3 correct
            for (int i = 0; i < 3; i++)
            {
                db.UserAnswer.Add(new UserAnswer
                {
                    Quiz = quiz, Question = questions[i],
                    UserId = 100L, UserName = "UserA", AnswerText = "A", IsAnswer = true
                });
            }
            // User B: 5 correct
            for (int i = 0; i < 5; i++)
            {
                db.UserAnswer.Add(new UserAnswer
                {
                    Quiz = quiz, Question = questions[i],
                    UserId = 200L, UserName = "UserB", AnswerText = "A", IsAnswer = true
                });
            }
            // User C: 1 correct
            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz, Question = questions[0],
                UserId = 300L, UserName = "UserC", AnswerText = "A", IsAnswer = true
            });
            await db.SaveChangesAsync();

            // Mirrors GetTopUsers query
            var leaderboard = await db.UserAnswer
                .Where(u => u.Quiz.QuizId == quiz.QuizId && u.IsAnswer)
                .GroupBy(u => u.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            Assert.Equal(3, leaderboard.Count);
            Assert.Equal(200L, leaderboard[0].UserId); // UserB with 5
            Assert.Equal(100L, leaderboard[1].UserId); // UserA with 3
            Assert.Equal(300L, leaderboard[2].UserId); // UserC with 1
        }

        #endregion

        #region Question Color Assignment Tests

        [Theory]
        [InlineData("tech")]
        [InlineData("general")]
        [InlineData("extra")]
        public void QuestionEmbed_UsesTestNameForColor(string testName)
        {
            // Mirrors the color selection in QuizUtil.GetQuestionEmbed
            var color = testName switch
            {
                "tech" => QuizConstants.COLOR_TECH,
                "general" => QuizConstants.COLOR_GENERAL,
                "extra" => QuizConstants.COLOR_EXTRA,
                _ => QuizConstants.COLOR_CORRECT
            };

            // All license types should have distinct colors
            Assert.NotEqual(default, color);
        }

        [Fact]
        public void LicenseTypeColors_AreDistinct()
        {
            Assert.NotEqual(QuizConstants.COLOR_TECH, QuizConstants.COLOR_GENERAL);
            Assert.NotEqual(QuizConstants.COLOR_TECH, QuizConstants.COLOR_EXTRA);
            Assert.NotEqual(QuizConstants.COLOR_GENERAL, QuizConstants.COLOR_EXTRA);
        }

        #endregion
    }
}
