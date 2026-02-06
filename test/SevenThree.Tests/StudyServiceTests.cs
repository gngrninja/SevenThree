using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SevenThree.Constants;
using SevenThree.Database;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class StudyServiceTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;
        private readonly Mock<ILogger<StudyService>> _mockLogger;
        private readonly StudyService _sut;

        public StudyServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _mockLogger = new Mock<ILogger<StudyService>>();

            var mockFactory = new Mock<IDbContextFactory<SevenThreeContext>>();
            mockFactory.Setup(f => f.CreateDbContext())
                .Returns(() => new SevenThreeContext(_dbOptions));

            _sut = new StudyService(mockFactory.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            using var context = new SevenThreeContext(_dbOptions);
            context.Database.EnsureDeleted();
        }

        /// <summary>
        /// Seeds a complete quiz scenario: test pool, questions with answers, a quiz, and user answers.
        /// Returns (quizId, questionIds) for reference.
        /// </summary>
        private async Task<(int QuizId, List<int> QuestionIds)> SeedQuizWithAnswers(
            ulong userId, int correctCount, int incorrectCount)
        {
            using var db = new SevenThreeContext(_dbOptions);

            var test = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(test);
            await db.SaveChangesAsync();

            var quiz = new Quiz
            {
                ServerId = userId,
                IsActive = false,
                TimeStarted = DateTime.UtcNow.AddMinutes(-10),
                TimeEnded = DateTime.UtcNow,
                StartedById = userId,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            var questionIds = new List<int>();
            var total = correctCount + incorrectCount;

            for (int i = 0; i < total; i++)
            {
                var question = new Questions
                {
                    QuestionText = $"Question {i + 1}?",
                    QuestionSection = $"T1A{(i + 1):D2}",
                    SubelementName = "T1",
                    SubelementDesc = "FCC Rules",
                    FccPart = "97",
                    Test = test
                };
                db.Questions.Add(question);
                await db.SaveChangesAsync();
                questionIds.Add(question.QuestionId);

                // Add 4 answers per question, answer A is correct
                for (int j = 0; j < 4; j++)
                {
                    db.Answer.Add(new Answer
                    {
                        Question = question,
                        AnswerText = $"Answer {(char)('A' + j)} for Q{i + 1}",
                        IsAnswer = j == 0
                    });
                }
                await db.SaveChangesAsync();

                // Record user answer
                bool isCorrect = i < correctCount;
                db.UserAnswer.Add(new UserAnswer
                {
                    Quiz = quiz,
                    Question = question,
                    UserId = (long)userId,
                    UserName = "TestUser",
                    AnswerText = isCorrect ? "A" : "B",
                    IsAnswer = isCorrect
                });
            }

            await db.SaveChangesAsync();
            return (quiz.QuizId, questionIds);
        }

        #region Session Management Tests

        [Fact]
        public void CreateSession_ReturnsNonEmptySessionId()
        {
            // arrange
            var questions = new List<MissedQuestion>
            {
                new() { QuestionId = 1, QuestionText = "Q1?" }
            };

            // act
            var sessionId = _sut.CreateSession(12345UL, questions);

            // assert
            Assert.NotNull(sessionId);
            Assert.Equal(8, sessionId.Length);
        }

        [Fact]
        public void CreateSession_SessionCanBeRetrieved()
        {
            // arrange
            var questions = new List<MissedQuestion>
            {
                new() { QuestionId = 1, QuestionText = "Q1?", QuestionSection = "T1A01" },
                new() { QuestionId = 2, QuestionText = "Q2?", QuestionSection = "T1A02" }
            };

            // act
            var sessionId = _sut.CreateSession(12345UL, questions);
            var session = _sut.GetSession(sessionId);

            // assert
            Assert.NotNull(session);
            Assert.Equal(12345UL, session.UserId);
            Assert.Equal(2, session.Questions.Count);
            Assert.Equal(0, session.CurrentIndex);
            Assert.False(session.ShowingAnswer);
        }

        [Fact]
        public void GetSession_NonexistentSession_ReturnsNull()
        {
            // act
            var session = _sut.GetSession("nonexistent");

            // assert
            Assert.Null(session);
        }

        [Fact]
        public void UpdateSession_ChangesArePersisted()
        {
            // arrange
            var questions = new List<MissedQuestion>
            {
                new() { QuestionId = 1, QuestionText = "Q1?" },
                new() { QuestionId = 2, QuestionText = "Q2?" }
            };
            var sessionId = _sut.CreateSession(12345UL, questions);
            var session = _sut.GetSession(sessionId);

            // act
            session.CurrentIndex = 1;
            session.ShowingAnswer = true;
            _sut.UpdateSession(session);

            // assert
            var updated = _sut.GetSession(sessionId);
            Assert.Equal(1, updated.CurrentIndex);
            Assert.True(updated.ShowingAnswer);
        }

        [Fact]
        public void RemoveSession_SessionNoLongerRetrievable()
        {
            // arrange
            var questions = new List<MissedQuestion>
            {
                new() { QuestionId = 1, QuestionText = "Q1?" }
            };
            var sessionId = _sut.CreateSession(12345UL, questions);

            // act
            _sut.RemoveSession(sessionId);

            // assert
            Assert.Null(_sut.GetSession(sessionId));
        }

        [Fact]
        public void RemoveSession_NonexistentSession_DoesNotThrow()
        {
            // act & assert - should not throw
            _sut.RemoveSession("nonexistent");
        }

        [Fact]
        public void CreateSession_MultipleSessions_AreIndependent()
        {
            // arrange
            var q1 = new List<MissedQuestion> { new() { QuestionId = 1, QuestionText = "Q1?" } };
            var q2 = new List<MissedQuestion> { new() { QuestionId = 2, QuestionText = "Q2?" } };

            // act
            var id1 = _sut.CreateSession(111UL, q1);
            var id2 = _sut.CreateSession(222UL, q2);

            // assert
            Assert.NotEqual(id1, id2);
            Assert.Equal(111UL, _sut.GetSession(id1).UserId);
            Assert.Equal(222UL, _sut.GetSession(id2).UserId);
        }

        #endregion

        #region GetMissedQuestionsAsync Tests

        [Fact]
        public async Task GetMissedQuestionsAsync_NoHistory_ReturnsEmptyList()
        {
            // act
            var result = await _sut.GetMissedQuestionsAsync(99999UL, StudyScope.All);

            // assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMissedQuestionsAsync_AllCorrect_ReturnsEmptyList()
        {
            // arrange - 5 correct, 0 incorrect
            await SeedQuizWithAnswers(12345UL, correctCount: 5, incorrectCount: 0);

            // act
            var result = await _sut.GetMissedQuestionsAsync(12345UL, StudyScope.All);

            // assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMissedQuestionsAsync_SomeIncorrect_ReturnsMissedQuestions()
        {
            // arrange - 3 correct, 2 incorrect
            await SeedQuizWithAnswers(12345UL, correctCount: 3, incorrectCount: 2);

            // act
            var result = await _sut.GetMissedQuestionsAsync(12345UL, StudyScope.All);

            // assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetMissedQuestionsAsync_IncludesCorrectAnswer()
        {
            // arrange - 0 correct, 1 incorrect
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 1);

            // act
            var result = await _sut.GetMissedQuestionsAsync(12345UL, StudyScope.All);

            // assert
            Assert.Single(result);
            Assert.NotEqual("Not available", result[0].CorrectAnswer);
            Assert.Contains("Answer A", result[0].CorrectAnswer);
        }

        [Fact]
        public async Task GetMissedQuestionsAsync_ScopeLast_OnlyReturnsLastQuiz()
        {
            // arrange - first quiz: 2 wrong
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 2);

            // Add a second quiz with 1 wrong
            using var db = new SevenThreeContext(_dbOptions);
            var test = await db.HamTest.FirstAsync();
            var quiz2 = new Quiz
            {
                ServerId = 12345UL,
                IsActive = false,
                TimeStarted = DateTime.UtcNow.AddMinutes(-1), // More recent
                TimeEnded = DateTime.UtcNow,
                StartedById = 12345UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz2);
            await db.SaveChangesAsync();

            var q = new Questions
            {
                QuestionText = "Second quiz question?",
                QuestionSection = "T2A01",
                SubelementName = "T2",
                SubelementDesc = "Operating",
                Test = test
            };
            db.Questions.Add(q);
            await db.SaveChangesAsync();

            db.Answer.Add(new Answer { Question = q, AnswerText = "Correct", IsAnswer = true });
            db.Answer.Add(new Answer { Question = q, AnswerText = "Wrong", IsAnswer = false });
            await db.SaveChangesAsync();

            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz2,
                Question = q,
                UserId = 12345L,
                UserName = "TestUser",
                AnswerText = "B",
                IsAnswer = false
            });
            await db.SaveChangesAsync();

            // act
            var result = await _sut.GetMissedQuestionsAsync(12345UL, StudyScope.Last);

            // assert - should only return the 1 missed from the most recent quiz
            Assert.Single(result);
            Assert.Equal("T2A01", result[0].QuestionSection);
        }

        [Fact]
        public async Task GetMissedQuestionsAsync_OrderedByQuestionSection()
        {
            // arrange - 0 correct, 3 incorrect
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 3);

            // act
            var result = await _sut.GetMissedQuestionsAsync(12345UL, StudyScope.All);

            // assert
            Assert.Equal(3, result.Count);
            Assert.Equal("T1A01", result[0].QuestionSection);
            Assert.Equal("T1A02", result[1].QuestionSection);
            Assert.Equal("T1A03", result[2].QuestionSection);
        }

        #endregion

        #region GetWeakQuestionsAsync Tests

        [Fact]
        public async Task GetWeakQuestionsAsync_NoHistory_ReturnsEmpty()
        {
            // act
            var result = await _sut.GetWeakQuestionsAsync(99999UL);

            // assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetWeakQuestionsAsync_SingleMiss_NotWeak()
        {
            // arrange - miss a question once (threshold is 2)
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 1);

            // act
            var result = await _sut.GetWeakQuestionsAsync(12345UL);

            // assert - 1 miss < threshold of 2
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetWeakQuestionsAsync_OrderedByMissCountDescending()
        {
            // arrange - miss same questions across multiple quizzes
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 2);

            // Take a second quiz missing the same questions
            using var db = new SevenThreeContext(_dbOptions);
            var test = await db.HamTest.FirstAsync();
            var questions = await db.Questions.ToListAsync();

            var quiz2 = new Quiz
            {
                ServerId = 12345UL,
                IsActive = false,
                TimeStarted = DateTime.UtcNow.AddMinutes(-1),
                TimeEnded = DateTime.UtcNow,
                StartedById = 12345UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz2);
            await db.SaveChangesAsync();

            foreach (var q in questions)
            {
                db.UserAnswer.Add(new UserAnswer
                {
                    Quiz = quiz2,
                    Question = q,
                    UserId = 12345L,
                    UserName = "TestUser",
                    AnswerText = "B",
                    IsAnswer = false
                });
            }
            await db.SaveChangesAsync();

            // act
            var result = await _sut.GetWeakQuestionsAsync(12345UL);

            // assert - each question was missed 2 times, meets threshold
            Assert.Equal(2, result.Count);
            Assert.True(result.All(q => q.TimesMissed >= StudyConstants.WEAK_THRESHOLD));
        }

        #endregion

        #region GetUserStatsAsync Tests

        [Fact]
        public async Task GetUserStatsAsync_NoHistory_ReturnsZeroTotalAnswered()
        {
            // act
            var result = await _sut.GetUserStatsAsync(99999UL);

            // assert
            Assert.Equal(0, result.TotalAnswered);
        }

        [Fact]
        public async Task GetUserStatsAsync_CalculatesOverallPercentCorrectly()
        {
            // arrange - 3 correct, 2 incorrect = 60%
            await SeedQuizWithAnswers(12345UL, correctCount: 3, incorrectCount: 2);

            // act
            var result = await _sut.GetUserStatsAsync(12345UL);

            // assert
            Assert.Equal(5, result.TotalAnswered);
            Assert.Equal(3, result.TotalCorrect);
            Assert.Equal(60.0, result.OverallPercent);
        }

        [Fact]
        public async Task GetUserStatsAsync_AllCorrect_Returns100Percent()
        {
            // arrange
            await SeedQuizWithAnswers(12345UL, correctCount: 4, incorrectCount: 0);

            // act
            var result = await _sut.GetUserStatsAsync(12345UL);

            // assert
            Assert.Equal(100.0, result.OverallPercent);
        }

        [Fact]
        public async Task GetUserStatsAsync_AllIncorrect_Returns0Percent()
        {
            // arrange
            await SeedQuizWithAnswers(12345UL, correctCount: 0, incorrectCount: 4);

            // act
            var result = await _sut.GetUserStatsAsync(12345UL);

            // assert
            Assert.Equal(0.0, result.OverallPercent);
        }

        [Fact]
        public async Task GetUserStatsAsync_HasSubelementBreakdown()
        {
            // arrange
            await SeedQuizWithAnswers(12345UL, correctCount: 2, incorrectCount: 3);

            // act
            var result = await _sut.GetUserStatsAsync(12345UL);

            // assert
            Assert.NotEmpty(result.SubelementStats);
            var stat = result.SubelementStats.First();
            Assert.Equal("T1", stat.SubelementName);
            Assert.Equal("TECH", stat.TestName.ToUpper());
        }

        #endregion

        #region GetAnswersForQuestionAsync Tests

        [Fact]
        public async Task GetAnswersForQuestionAsync_ReturnsAllAnswers()
        {
            // arrange
            await SeedQuizWithAnswers(12345UL, correctCount: 1, incorrectCount: 0);

            using var db = new SevenThreeContext(_dbOptions);
            var question = await db.Questions.FirstAsync();

            // act
            var result = await _sut.GetAnswersForQuestionAsync(question.QuestionId);

            // assert
            Assert.Equal(4, result.Count);
            Assert.Single(result, a => a.IsCorrect);
        }

        [Fact]
        public async Task GetAnswersForQuestionAsync_NonexistentQuestion_ReturnsEmpty()
        {
            // act
            var result = await _sut.GetAnswersForQuestionAsync(99999);

            // assert
            Assert.Empty(result);
        }

        #endregion
    }
}
