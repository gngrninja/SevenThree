using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SevenThree.Database;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests the import upsert logic by simulating the database operations
    /// that AdminSlashCommands.ImportPoolFromDirectory performs.
    /// This validates that the upsert strategy preserves UserAnswer history
    /// and correctly archives removed questions.
    /// </summary>
    public class ImportTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public ImportTests()
        {
            _dbOptions = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        public void Dispose()
        {
            using var context = new SevenThreeContext(_dbOptions);
            context.Database.EnsureDeleted();
        }

        private async Task<HamTest> SeedPool(SevenThreeContext db, string testName = "tech")
        {
            var test = new HamTest
            {
                TestName = testName,
                TestDescription = $"U.S. Ham Radio test for the {testName} class license.",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(test);
            await db.SaveChangesAsync();
            return test;
        }

        private async Task<Questions> SeedQuestion(SevenThreeContext db, HamTest test, string section, string text)
        {
            var question = new Questions
            {
                QuestionText = text,
                QuestionSection = section,
                SubelementName = "T1",
                SubelementDesc = "FCC Rules",
                FccPart = "97",
                Test = test,
                IsArchived = false,
                LastImportedAt = DateTime.UtcNow
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            // Add 4 answers (A is correct)
            for (int j = 0; j < 4; j++)
            {
                db.Answer.Add(new Answer
                {
                    Question = question,
                    AnswerText = $"Answer {(char)('A' + j)} for {section}",
                    IsAnswer = j == 0
                });
            }
            await db.SaveChangesAsync();
            return question;
        }

        #region Upsert Behavior Tests

        [Fact]
        public async Task Upsert_ExistingQuestion_PreservesQuestionId()
        {
            // arrange - simulate initial import
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var original = await SeedQuestion(db, test, "T1A01", "Original question text?");
            var originalId = original.QuestionId;

            // act - simulate re-import: update existing question's text
            original.QuestionText = "Updated question text?";
            original.LastImportedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // assert - QuestionId is preserved (FK to UserAnswers stays intact)
            var reloaded = await db.Questions.FirstAsync(q => q.QuestionSection == "T1A01");
            Assert.Equal(originalId, reloaded.QuestionId);
            Assert.Equal("Updated question text?", reloaded.QuestionText);
        }

        [Fact]
        public async Task Upsert_AnswerRecreation_DoesNotAffectUserAnswers()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var question = await SeedQuestion(db, test, "T1A01", "Question 1?");

            // Create a quiz with a user answer
            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = false,
                TimeStarted = DateTime.UtcNow,
                StartedById = 12345UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz,
                Question = question,
                UserId = 12345L,
                UserName = "TestUser",
                AnswerText = "A",
                IsAnswer = true
            });
            await db.SaveChangesAsync();

            // act - simulate re-import: delete old answers and add new ones
            var oldAnswers = await db.Answer
                .Where(a => a.Question.QuestionId == question.QuestionId)
                .ToListAsync();
            db.RemoveRange(oldAnswers);

            for (int j = 0; j < 4; j++)
            {
                db.Answer.Add(new Answer
                {
                    Question = question,
                    AnswerText = $"New answer {(char)('A' + j)}",
                    IsAnswer = j == 1 // B is now correct
                });
            }
            await db.SaveChangesAsync();

            // assert - UserAnswer record is still intact
            var userAnswer = await db.UserAnswer
                .Include(ua => ua.Question)
                .FirstOrDefaultAsync(ua => ua.UserId == 12345L);
            Assert.NotNull(userAnswer);
            Assert.Equal(question.QuestionId, userAnswer.Question.QuestionId);
            Assert.Equal("A", userAnswer.AnswerText);
        }

        #endregion

        #region Archive Behavior Tests

        [Fact]
        public async Task Archive_RemovedQuestion_SetsIsArchivedTrue()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestion(db, test, "T1A01", "Still in pool?");
            await SeedQuestion(db, test, "T1A02", "Removed from pool?");

            // act - simulate import where only T1A01 is in the new pool
            var importedSections = new HashSet<string> { "T1A01" };
            var existingQuestions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId)
                .ToListAsync();

            foreach (var q in existingQuestions)
            {
                if (!importedSections.Contains(q.QuestionSection) && !q.IsArchived)
                {
                    q.IsArchived = true;
                }
            }
            await db.SaveChangesAsync();

            // assert
            var q1 = await db.Questions.FirstAsync(q => q.QuestionSection == "T1A01");
            var q2 = await db.Questions.FirstAsync(q => q.QuestionSection == "T1A02");
            Assert.False(q1.IsArchived);
            Assert.True(q2.IsArchived);
        }

        [Fact]
        public async Task Archive_ReimportedQuestion_ClearsIsArchived()
        {
            // arrange - question is archived
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var question = await SeedQuestion(db, test, "T1A01", "Was archived?");
            question.IsArchived = true;
            await db.SaveChangesAsync();

            // act - simulate re-import that includes T1A01 again
            question.IsArchived = false;
            question.QuestionText = "Back in the pool!";
            question.LastImportedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // assert
            var reloaded = await db.Questions.FirstAsync(q => q.QuestionSection == "T1A01");
            Assert.False(reloaded.IsArchived);
            Assert.Equal("Back in the pool!", reloaded.QuestionText);
        }

        [Fact]
        public async Task ArchivedQuestions_ExcludedFromQuizQuery()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            await SeedQuestion(db, test, "T1A01", "Active question");
            var archived = await SeedQuestion(db, test, "T1A02", "Archived question");
            archived.IsArchived = true;
            await db.SaveChangesAsync();

            // act - simulate the quiz question query (from QuizUtil.GetRandomQuestions)
            var activeQuestions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId && !q.IsArchived)
                .ToListAsync();

            // assert
            Assert.Single(activeQuestions);
            Assert.Equal("T1A01", activeQuestions[0].QuestionSection);
        }

        [Fact]
        public async Task ArchivedQuestions_PreserveUserAnswerHistory()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var question = await SeedQuestion(db, test, "T1A01", "Soon to be archived");

            var quiz = new Quiz
            {
                ServerId = 12345UL,
                IsActive = false,
                TimeStarted = DateTime.UtcNow,
                StartedById = 12345UL,
                StartedByName = "TestUser"
            };
            db.Quiz.Add(quiz);
            await db.SaveChangesAsync();

            db.UserAnswer.Add(new UserAnswer
            {
                Quiz = quiz,
                Question = question,
                UserId = 12345L,
                UserName = "TestUser",
                AnswerText = "A",
                IsAnswer = true
            });
            await db.SaveChangesAsync();

            // act - archive the question
            question.IsArchived = true;
            await db.SaveChangesAsync();

            // assert - user answer still exists and references the question
            var userAnswer = await db.UserAnswer
                .Include(ua => ua.Question)
                .FirstOrDefaultAsync(ua => ua.UserId == 12345L);
            Assert.NotNull(userAnswer);
            Assert.Equal(question.QuestionId, userAnswer.Question.QuestionId);
            Assert.True(userAnswer.Question.IsArchived);
        }

        #endregion

        #region New Question Fields Tests

        [Fact]
        public async Task Questions_IsArchived_DefaultsFalse()
        {
            // arrange & act
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var question = new Questions
            {
                QuestionText = "Test?",
                QuestionSection = "T1A01",
                Test = test
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            // assert
            var saved = await db.Questions.FirstAsync();
            Assert.False(saved.IsArchived);
        }

        [Fact]
        public async Task Questions_LastImportedAt_CanBeSet()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            var importTime = DateTime.UtcNow;

            var question = new Questions
            {
                QuestionText = "Test?",
                QuestionSection = "T1A01",
                Test = test,
                LastImportedAt = importTime
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            // assert
            var saved = await db.Questions.FirstAsync();
            Assert.NotNull(saved.LastImportedAt);
            Assert.Equal(importTime, saved.LastImportedAt.Value, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Questions_LastImportedAt_NullByDefault()
        {
            // arrange & act
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            db.Questions.Add(new Questions
            {
                QuestionText = "Test?",
                QuestionSection = "T1A01",
                Test = test
            });
            await db.SaveChangesAsync();

            // assert
            var saved = await db.Questions.FirstAsync();
            Assert.Null(saved.LastImportedAt);
        }

        #endregion

        #region Multiple Pool Tests

        [Fact]
        public async Task MultiplePools_SameTestName_CanExist()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var pool1 = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            var pool2 = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician",
                FromDate = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2030, 6, 30), DateTimeKind.Utc)
            };

            // act
            db.HamTest.Add(pool1);
            db.HamTest.Add(pool2);
            await db.SaveChangesAsync();

            // assert
            var pools = await db.HamTest.Where(t => t.TestName == "tech").ToListAsync();
            Assert.Equal(2, pools.Count);
        }

        [Fact]
        public async Task MultiplePools_QuestionsArePoolSpecific()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var pool1 = await SeedPool(db, "tech");

            var pool2 = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician v2",
                FromDate = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2030, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(pool2);
            await db.SaveChangesAsync();

            await SeedQuestion(db, pool1, "T1A01", "Pool 1 question");
            await SeedQuestion(db, pool2, "T1A01", "Pool 2 question");

            // act
            var pool1Questions = await db.Questions
                .Where(q => q.Test.TestId == pool1.TestId)
                .ToListAsync();
            var pool2Questions = await db.Questions
                .Where(q => q.Test.TestId == pool2.TestId)
                .ToListAsync();

            // assert - same QuestionSection can exist in different pools
            Assert.Single(pool1Questions);
            Assert.Single(pool2Questions);
            Assert.Equal("Pool 1 question", pool1Questions[0].QuestionText);
            Assert.Equal("Pool 2 question", pool2Questions[0].QuestionText);
        }

        [Fact]
        public async Task CurrentPool_SelectedByDateRange()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var expired = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician",
                FromDate = DateTime.SpecifyKind(new DateTime(2018, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2022, 6, 30), DateTimeKind.Utc)
            };
            var current = new HamTest
            {
                TestName = "tech",
                TestDescription = "Technician",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2030, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.AddRange(expired, current);
            await db.SaveChangesAsync();

            // act - simulate the pool selection logic from HamTestSlashCommands
            var today = DateTime.UtcNow.Date;
            var selectedPool = await db.HamTest
                .Where(t => t.TestName == "tech" && t.FromDate <= today && t.ToDate >= today)
                .FirstOrDefaultAsync();

            // assert
            Assert.NotNull(selectedPool);
            Assert.Equal(current.TestId, selectedPool.TestId);
        }

        #endregion

        #region Figure Upsert Tests

        [Fact]
        public async Task Figure_Upsert_UpdatesExistingFigure()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            var test = await SeedPool(db);
            db.Figure.Add(new Figure
            {
                Test = test,
                FigureName = "T-1",
                FigureImage = new byte[] { 1, 2, 3 }
            });
            await db.SaveChangesAsync();

            // act - simulate re-import with new image data
            var existing = await db.Figure.FirstAsync(f => f.FigureName == "T-1");
            existing.FigureImage = new byte[] { 4, 5, 6, 7 };
            await db.SaveChangesAsync();

            // assert
            var updated = await db.Figure.FirstAsync(f => f.FigureName == "T-1");
            Assert.Equal(new byte[] { 4, 5, 6, 7 }, updated.FigureImage);
        }

        #endregion
    }
}
