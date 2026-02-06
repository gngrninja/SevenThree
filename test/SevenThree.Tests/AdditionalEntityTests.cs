using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SevenThree.Database;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests for database entities that were not previously covered:
    /// ApiData, Cred, PrefixList, and additional relationship tests.
    /// </summary>
    public class AdditionalEntityTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public AdditionalEntityTests()
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

        #region ApiData Tests

        [Fact]
        public async Task ApiData_CanBeCreated()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.ApiData.Add(new ApiData
            {
                AppName = "QRZ",
                ApiKey = "test-key-123",
                ApiBaseUrl = "https://xmldata.qrz.com/xml/current"
            });
            await db.SaveChangesAsync();

            var saved = await db.ApiData.FirstAsync();
            Assert.Equal("QRZ", saved.AppName);
            Assert.Equal("test-key-123", saved.ApiKey);
            Assert.Equal("https://xmldata.qrz.com/xml/current", saved.ApiBaseUrl);
        }

        [Fact]
        public async Task ApiData_CanBeUpdated()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.ApiData.Add(new ApiData
            {
                AppName = "QRZ",
                ApiKey = "old-key",
                ApiBaseUrl = "https://old-url.com"
            });
            await db.SaveChangesAsync();

            var existing = await db.ApiData.FirstAsync();
            existing.ApiKey = "new-key";
            await db.SaveChangesAsync();

            var updated = await db.ApiData.FirstAsync();
            Assert.Equal("new-key", updated.ApiKey);
        }

        [Fact]
        public async Task ApiData_MultipleApps_CanCoexist()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.ApiData.Add(new ApiData { AppName = "QRZ", ApiKey = "key1", ApiBaseUrl = "url1" });
            db.ApiData.Add(new ApiData { AppName = "HamQTH", ApiKey = "key2", ApiBaseUrl = "url2" });
            await db.SaveChangesAsync();

            var count = await db.ApiData.CountAsync();
            Assert.Equal(2, count);

            var qrz = await db.ApiData.FirstAsync(a => a.AppName == "QRZ");
            Assert.Equal("key1", qrz.ApiKey);
        }

        #endregion

        #region Cred Tests

        [Fact]
        public async Task Cred_CanBeCreated()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.Cred.Add(new Cred { User = "testuser", Pass = "testpass" });
            await db.SaveChangesAsync();

            var saved = await db.Cred.FirstAsync();
            Assert.Equal("testuser", saved.User);
            Assert.Equal("testpass", saved.Pass);
            Assert.True(saved.Id > 0);
        }

        [Fact]
        public async Task Cred_CanBeDeleted()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.Cred.Add(new Cred { User = "testuser", Pass = "testpass" });
            await db.SaveChangesAsync();

            var cred = await db.Cred.FirstAsync();
            db.Cred.Remove(cred);
            await db.SaveChangesAsync();

            Assert.Empty(await db.Cred.ToListAsync());
        }

        #endregion

        #region PrefixList Tests

        [Fact]
        public async Task PrefixList_CanBeCreated()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.PrefixList.Add(new PrefixList
            {
                ServerId = 123456789L,
                ServerName = "Test Server",
                Prefix = '+',
                SetById = 987654321L
            });
            await db.SaveChangesAsync();

            var saved = await db.PrefixList.FirstAsync();
            Assert.Equal(123456789L, saved.ServerId);
            Assert.Equal("Test Server", saved.ServerName);
            Assert.Equal('+', saved.Prefix);
            Assert.Equal(987654321L, saved.SetById);
        }

        [Fact]
        public async Task PrefixList_DifferentServers_DifferentPrefixes()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.PrefixList.Add(new PrefixList
            {
                ServerId = 111L,
                ServerName = "Server A",
                Prefix = '+',
                SetById = 1L
            });
            db.PrefixList.Add(new PrefixList
            {
                ServerId = 222L,
                ServerName = "Server B",
                Prefix = '!',
                SetById = 2L
            });
            await db.SaveChangesAsync();

            var prefixA = await db.PrefixList.FirstAsync(p => p.ServerId == 111L);
            var prefixB = await db.PrefixList.FirstAsync(p => p.ServerId == 222L);

            Assert.Equal('+', prefixA.Prefix);
            Assert.Equal('!', prefixB.Prefix);
        }

        [Fact]
        public async Task PrefixList_CanUpdatePrefix()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.PrefixList.Add(new PrefixList
            {
                ServerId = 111L,
                ServerName = "Test",
                Prefix = '+',
                SetById = 1L
            });
            await db.SaveChangesAsync();

            var existing = await db.PrefixList.FirstAsync();
            existing.Prefix = '!';
            await db.SaveChangesAsync();

            var updated = await db.PrefixList.FirstAsync();
            Assert.Equal('!', updated.Prefix);
        }

        #endregion

        #region Figure Relationship Tests

        [Fact]
        public async Task Figure_LinkedToCorrectPool()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var pool1 = new HamTest
            {
                TestName = "tech",
                TestDescription = "Tech",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            var pool2 = new HamTest
            {
                TestName = "tech",
                TestDescription = "Tech v2",
                FromDate = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2030, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.AddRange(pool1, pool2);
            await db.SaveChangesAsync();

            // Same figure name, different pools
            db.Figure.Add(new Figure { Test = pool1, FigureName = "T-1", FigureImage = new byte[] { 1, 2 } });
            db.Figure.Add(new Figure { Test = pool2, FigureName = "T-1", FigureImage = new byte[] { 3, 4 } });
            await db.SaveChangesAsync();

            var figures = await db.Figure.Include(f => f.Test).Where(f => f.FigureName == "T-1").ToListAsync();
            Assert.Equal(2, figures.Count);

            var fig1 = figures.First(f => f.Test.TestId == pool1.TestId);
            var fig2 = figures.First(f => f.Test.TestId == pool2.TestId);
            Assert.Equal(new byte[] { 1, 2 }, fig1.FigureImage);
            Assert.Equal(new byte[] { 3, 4 }, fig2.FigureImage);
        }

        #endregion

        #region Question-Answer Relationship Tests

        [Fact]
        public async Task Question_WithAnswers_CascadeNavigation()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = new HamTest
            {
                TestName = "tech",
                TestDescription = "Test",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(test);
            await db.SaveChangesAsync();

            var question = new Questions
            {
                QuestionText = "What is 2+2?",
                QuestionSection = "T1A01",
                Test = test
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            db.Answer.Add(new Answer { Question = question, AnswerText = "4", IsAnswer = true });
            db.Answer.Add(new Answer { Question = question, AnswerText = "3", IsAnswer = false });
            db.Answer.Add(new Answer { Question = question, AnswerText = "5", IsAnswer = false });
            db.Answer.Add(new Answer { Question = question, AnswerText = "6", IsAnswer = false });
            await db.SaveChangesAsync();

            // Navigate from Question -> Answers
            var answers = await db.Answer
                .Where(a => a.Question.QuestionId == question.QuestionId)
                .ToListAsync();

            Assert.Equal(4, answers.Count);
            Assert.Single(answers, a => a.IsAnswer);
        }

        [Fact]
        public async Task Answer_CorrectAnswer_IsIdentifiable()
        {
            using var db = new SevenThreeContext(_dbOptions);
            var test = new HamTest
            {
                TestName = "tech",
                TestDescription = "Test",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            };
            db.HamTest.Add(test);
            await db.SaveChangesAsync();

            var question = new Questions { QuestionText = "Test?", QuestionSection = "T1A01", Test = test };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            db.Answer.Add(new Answer { Question = question, AnswerText = "Correct", IsAnswer = true });
            db.Answer.Add(new Answer { Question = question, AnswerText = "Wrong", IsAnswer = false });
            await db.SaveChangesAsync();

            var correct = await db.Answer
                .Where(a => a.Question.QuestionId == question.QuestionId && a.IsAnswer)
                .FirstOrDefaultAsync();

            Assert.NotNull(correct);
            Assert.Equal("Correct", correct.AnswerText);
        }

        #endregion

        #region QuizSettings Tests

        [Fact]
        public async Task QuizSettings_ClearAfterTaken_DefaultsCorrectly()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.QuizSettings.Add(new QuizSettings
            {
                DiscordGuildId = 12345UL,
                TechChannelId = 67890UL,
                ClearAfterTaken = true
            });
            await db.SaveChangesAsync();

            var settings = await db.QuizSettings.FirstAsync(s => s.DiscordGuildId == 12345UL);
            Assert.True(settings.ClearAfterTaken);
            Assert.Equal(67890UL, settings.TechChannelId);
        }

        #endregion

        #region HamTest Date Range Tests

        [Fact]
        public async Task HamTest_DateRanges_QueryByDateCorrectly()
        {
            using var db = new SevenThreeContext(_dbOptions);
            db.HamTest.Add(new HamTest
            {
                TestName = "tech",
                TestDescription = "Old",
                FromDate = DateTime.SpecifyKind(new DateTime(2018, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2022, 6, 30), DateTimeKind.Utc)
            });
            db.HamTest.Add(new HamTest
            {
                TestName = "tech",
                TestDescription = "Current",
                FromDate = DateTime.SpecifyKind(new DateTime(2022, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc)
            });
            db.HamTest.Add(new HamTest
            {
                TestName = "tech",
                TestDescription = "Future",
                FromDate = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc),
                ToDate = DateTime.SpecifyKind(new DateTime(2030, 6, 30), DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();

            var today = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Utc);

            var current = await db.HamTest
                .Where(t => t.TestName == "tech" && t.FromDate <= today && t.ToDate >= today)
                .ToListAsync();

            Assert.Single(current);
            Assert.Equal("Current", current[0].TestDescription);
        }

        #endregion
    }
}
