using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SevenThree.Database;

namespace SevenThree.Tests
{
    public class DatabaseEntityTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public DatabaseEntityTests()
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

        #region Quiz Entity Tests

        [Fact]
        public async Task Quiz_CanBeCreated()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var quiz = new Quiz
            {
                ServerId = 123456789,
                ServerName = "Test Server",
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 987654321,
                StartedByName = "TestUser",
                StartedByIconUrl = "https://example.com/icon.png"
            };

            // act
            context.Quiz.Add(quiz);
            await context.SaveChangesAsync();

            // assert
            var saved = await context.Quiz.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal("Test Server", saved.ServerName);
            Assert.True(saved.IsActive);
        }

        [Fact]
        public async Task Quiz_CanBeUpdated()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var quiz = new Quiz
            {
                ServerId = 123,
                ServerName = "Original",
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 456,
                StartedByName = "User"
            };
            context.Quiz.Add(quiz);
            await context.SaveChangesAsync();

            // act
            quiz.IsActive = false;
            quiz.TimeEnded = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // assert
            var updated = await context.Quiz.FirstOrDefaultAsync();
            Assert.NotNull(updated);
            Assert.False(updated.IsActive);
            Assert.NotEqual(default, updated.TimeEnded);
        }

        [Fact]
        public async Task Quiz_CanBeDeleted()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var quiz = new Quiz
            {
                ServerId = 123,
                ServerName = "ToDelete",
                IsActive = false,
                TimeStarted = DateTime.UtcNow,
                StartedById = 456,
                StartedByName = "User"
            };
            context.Quiz.Add(quiz);
            await context.SaveChangesAsync();

            // act
            context.Quiz.Remove(quiz);
            await context.SaveChangesAsync();

            // assert
            var count = await context.Quiz.CountAsync();
            Assert.Equal(0, count);
        }

        #endregion

        #region UserAnswer Entity Tests

        [Fact]
        public async Task UserAnswer_CanBeCreatedWithQuizRelationship()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var quiz = new Quiz
            {
                ServerId = 123,
                ServerName = "Test",
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 456,
                StartedByName = "User"
            };
            context.Quiz.Add(quiz);
            await context.SaveChangesAsync();

            var userAnswer = new UserAnswer
            {
                Quiz = quiz,
                UserName = "TestUser",
                UserId = 789,
                AnswerText = "A",
                IsAnswer = true
            };

            // act
            context.UserAnswer.Add(userAnswer);
            await context.SaveChangesAsync();

            // assert
            var saved = await context.UserAnswer
                .Include(ua => ua.Quiz)
                .FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.NotNull(saved.Quiz);
            Assert.Equal(quiz.QuizId, saved.Quiz.QuizId);
        }

        [Fact]
        public async Task UserAnswer_MultipleAnswersPerQuiz()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var quiz = new Quiz
            {
                ServerId = 123,
                ServerName = "Test",
                IsActive = true,
                TimeStarted = DateTime.UtcNow,
                StartedById = 456,
                StartedByName = "User"
            };
            context.Quiz.Add(quiz);
            await context.SaveChangesAsync();

            // act
            for (int i = 0; i < 5; i++)
            {
                context.UserAnswer.Add(new UserAnswer
                {
                    Quiz = quiz,
                    UserName = $"User{i}",
                    UserId = 100 + i,
                    AnswerText = ((char)('A' + (i % 4))).ToString(),
                    IsAnswer = i % 2 == 0
                });
            }
            await context.SaveChangesAsync();

            // assert
            var answers = await context.UserAnswer
                .Where(ua => ua.Quiz.QuizId == quiz.QuizId)
                .ToListAsync();
            Assert.Equal(5, answers.Count);
        }

        #endregion

        #region HamTest Entity Tests

        [Fact]
        public async Task HamTest_CanBeCreated()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var hamTest = new HamTest
            {
                TestName = "Technician",
                TestDescription = "Technician Class License Exam",
                FromDate = new DateTime(2022, 7, 1),
                ToDate = new DateTime(2026, 6, 30)
            };

            // act
            context.HamTest.Add(hamTest);
            await context.SaveChangesAsync();

            // assert
            var saved = await context.HamTest.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal("Technician", saved.TestName);
            Assert.Equal("Technician Class License Exam", saved.TestDescription);
        }

        #endregion

        #region CallSignAssociation Entity Tests

        [Fact]
        public async Task CallSignAssociation_CanBeCreated()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var association = new CallSignAssociation
            {
                CallSign = "W1AW",
                DiscordUserId = 123456789,
                DiscordUserName = "TestUser"
            };

            // act
            context.CallSignAssociation.Add(association);
            await context.SaveChangesAsync();

            // assert
            var saved = await context.CallSignAssociation.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal("W1AW", saved.CallSign);
            Assert.Equal(123456789L, saved.DiscordUserId);
        }

        [Fact]
        public async Task CallSignAssociation_CanQueryByDiscordUserId()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var discordUserId = 123456789L;
            context.CallSignAssociation.Add(new CallSignAssociation
            {
                CallSign = "N0CALL",
                DiscordUserId = discordUserId,
                DiscordUserName = "TestUser"
            });
            await context.SaveChangesAsync();

            // act
            var result = await context.CallSignAssociation
                .FirstOrDefaultAsync(c => c.DiscordUserId == discordUserId);

            // assert
            Assert.NotNull(result);
            Assert.Equal("N0CALL", result.CallSign);
        }

        #endregion

        #region QuizSettings Entity Tests

        [Fact]
        public async Task QuizSettings_CanBeCreated()
        {
            // arrange
            using var context = new SevenThreeContext(_dbOptions);
            var settings = new QuizSettings
            {
                DiscordGuildId = 123456789,
                TechChannelId = 111111111,
                GeneralChannelId = 222222222,
                ExtraChannelId = 333333333,
                ClearAfterTaken = true
            };

            // act
            context.QuizSettings.Add(settings);
            await context.SaveChangesAsync();

            // assert
            var saved = await context.QuizSettings.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(123456789UL, saved.DiscordGuildId);
            Assert.Equal(111111111UL, saved.TechChannelId);
            Assert.True(saved.ClearAfterTaken);
        }

        #endregion
    }
}
