using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly Mock<ILogger<HamTestService>> _mockLogger;

        public HamTestServiceTests()
        {
            // Use InMemory database for testing
            _dbOptions = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _mockLogger = new Mock<ILogger<HamTestService>>();
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

        private HamTestService CreateService(IDbContextFactory<SevenThreeContext> factory = null)
        {
            return new HamTestService(factory ?? CreateMockFactory(), _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesRunningTestsDictionary()
        {
            // arrange & act
            var service = CreateService();

            // assert
            Assert.NotNull(service.RunningTests);
            Assert.Empty(service.RunningTests);
        }

        [Fact]
        public async Task InitializeAsync_CleansUpActiveQuizzes()
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
            var service = CreateService(factory);
            await service.InitializeAsync();

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
            var service = CreateService();

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
            var service = CreateService();

            // assert
            Assert.IsType<System.Collections.Concurrent.ConcurrentDictionary<ulong, QuizUtil>>(service.RunningTests);
        }

        [Fact]
        public void RunningTests_IsEmptyOnConstruction()
        {
            // arrange & act
            var service = CreateService();

            // assert
            Assert.Empty(service.RunningTests);
        }

        #endregion

        #region StopTests Tests

        [Fact]
        public async Task StopTests_EmptyDictionary_CompletesSuccessfully()
        {
            // arrange
            var service = CreateService();

            // act & assert (should not throw)
            await service.StopTests();
        }

        #endregion

        #region InitializeAsync Tests

        [Fact]
        public async Task InitializeAsync_CanBeCalledMultipleTimes()
        {
            // arrange
            var service = CreateService();

            // act & assert - should not throw on multiple calls
            await service.InitializeAsync();
            await service.InitializeAsync();
        }

        #endregion

        #region Pool Cache Tests

        private async Task SeedPoolsAsync()
        {
            using var context = new SevenThreeContext(_dbOptions);
            context.HamTest.AddRange(
                new HamTest
                {
                    TestId = 1,
                    TestName = "tech",
                    TestDescription = "Technician",
                    FromDate = new DateTime(2022, 7, 1),
                    ToDate = new DateTime(2026, 6, 30)
                },
                new HamTest
                {
                    TestId = 2,
                    TestName = "tech",
                    TestDescription = "Technician (next cycle)",
                    FromDate = new DateTime(2026, 7, 1),
                    ToDate = new DateTime(2030, 6, 30)
                },
                new HamTest
                {
                    TestId = 3,
                    TestName = "general",
                    TestDescription = "General",
                    FromDate = new DateTime(2023, 7, 1),
                    ToDate = new DateTime(2027, 6, 30)
                });
            await context.SaveChangesAsync();
        }

        [Fact]
        public void GetPools_BeforeRefresh_ReturnsEmpty()
        {
            // arrange
            var service = CreateService();

            // assert
            Assert.Empty(service.GetPools("tech"));
        }

        [Fact]
        public async Task RefreshPoolCacheAsync_LoadsPoolsFilteredByTestName()
        {
            // arrange
            await SeedPoolsAsync();
            var service = CreateService();

            // act
            await service.RefreshPoolCacheAsync();

            // assert
            var techPools = service.GetPools("tech");
            Assert.Equal(2, techPools.Count);
            Assert.All(techPools, p => Assert.Equal("tech", p.TestName));
            Assert.Single(service.GetPools("general"));
            Assert.Empty(service.GetPools("extra"));
        }

        [Fact]
        public async Task RefreshPoolCacheAsync_PicksUpNewlyImportedPools()
        {
            // arrange
            var service = CreateService();
            await service.RefreshPoolCacheAsync();
            Assert.Empty(service.GetPools("general"));

            // act - simulate /import writing a new pool, then the post-import refresh
            await SeedPoolsAsync();
            await service.RefreshPoolCacheAsync();

            // assert
            Assert.Single(service.GetPools("general"));
        }

        [Fact]
        public async Task InitializeAsync_PopulatesPoolCache()
        {
            // arrange
            await SeedPoolsAsync();
            var service = CreateService();

            // act
            await service.InitializeAsync();

            // assert
            Assert.Equal(2, service.GetPools("tech").Count);
        }

        #endregion
    }
}
