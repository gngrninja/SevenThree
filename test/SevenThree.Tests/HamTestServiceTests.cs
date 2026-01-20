using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using SevenThree.Database;
using SevenThree.Services;
using SevenThree.Modules;

namespace SevenThree.Tests
{
    public class HamTestServiceTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public HamTestServiceTests()
        {
            // Use InMemory database for testing
            _dbOptions = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        public void Dispose()
        {
            // Clean up InMemory database
            using var context = new SevenThreeContext(_dbOptions);
            context.Database.EnsureDeleted();
        }

        private IDbContextFactory<SevenThreeContext> CreateMockFactory()
        {
            var mockFactory = new Mock<IDbContextFactory<SevenThreeContext>>();
            mockFactory.Setup(f => f.CreateDbContext())
                .Returns(() => new SevenThreeContext(_dbOptions));
            return mockFactory.Object;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesRunningTestsDictionary()
        {
            // arrange
            var factory = CreateMockFactory();

            // act
            var service = new HamTestService(factory);

            // assert
            Assert.NotNull(service.RunningTests);
            Assert.Empty(service.RunningTests);
        }

        [Fact]
        public async Task Constructor_CleansUpActiveQuizzes()
        {
            // arrange
            var factory = CreateMockFactory();

            // Seed active quiz
            using (var context = new SevenThreeContext(_dbOptions))
            {
                context.Quiz.Add(new Quiz
                {
                    QuizId = 1,
                    ServerId = 123,
                    ServerName = "Test Server",
                    IsActive = true,
                    TimeStarted = DateTime.UtcNow,
                    StartedById = 456,
                    StartedByName = "TestUser"
                });
                await context.SaveChangesAsync();
            }

            // act
            var service = new HamTestService(factory);

            // Allow cleanup task to complete
            await Task.Delay(100);

            // assert
            using (var context = new SevenThreeContext(_dbOptions))
            {
                var quiz = await context.Quiz.FirstOrDefaultAsync(q => q.QuizId == 1);
                Assert.NotNull(quiz);
                Assert.False(quiz.IsActive);
            }
        }

        #endregion

        #region RunningTests Tests

        [Fact]
        public void RunningTests_GetValueForNonexistentKey_ReturnsFalse()
        {
            // arrange
            var factory = CreateMockFactory();
            var service = new HamTestService(factory);

            // act
            var found = service.RunningTests.TryGetValue(999999UL, out var result);

            // assert
            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void RunningTests_IsConcurrentDictionary()
        {
            // arrange
            var factory = CreateMockFactory();
            var service = new HamTestService(factory);

            // assert
            Assert.IsType<System.Collections.Concurrent.ConcurrentDictionary<ulong, QuizUtil>>(service.RunningTests);
        }

        [Fact]
        public void RunningTests_IsEmptyOnConstruction()
        {
            // arrange
            var factory = CreateMockFactory();

            // act
            var service = new HamTestService(factory);

            // assert
            Assert.Empty(service.RunningTests);
        }

        #endregion

        #region StopTests Tests

        [Fact]
        public async Task StopTests_EmptyDictionary_CompletesSuccessfully()
        {
            // arrange
            var factory = CreateMockFactory();
            var service = new HamTestService(factory);

            // act & assert (should not throw)
            await service.StopTests();
        }

        #endregion
    }
}
