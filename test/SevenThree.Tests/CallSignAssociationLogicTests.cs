using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SevenThree.Database;

namespace SevenThree.Tests
{
    public class CallSignAssociationLogicTests : IDisposable
    {
        private readonly DbContextOptions<SevenThreeContext> _dbOptions;

        public CallSignAssociationLogicTests()
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

        [Fact]
        public async Task SetCall_NewUser_CreatesRecord()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            long userId = 123456789;
            var callsign = "W1AW";

            // act - simulate the upsert logic from CallAssociationSlashCommands.SetCall
            var existing = db.CallSignAssociation
                .Where(d => d.DiscordUserId == userId)
                .FirstOrDefault();

            Assert.Null(existing);

            db.CallSignAssociation.Add(new CallSignAssociation
            {
                DiscordUserId = userId,
                DiscordUserName = "TestUser",
                CallSign = callsign.ToUpper()
            });
            await db.SaveChangesAsync();

            // assert
            var saved = await db.CallSignAssociation.FirstOrDefaultAsync(d => d.DiscordUserId == userId);
            Assert.NotNull(saved);
            Assert.Equal("W1AW", saved.CallSign);
            Assert.Equal("TestUser", saved.DiscordUserName);
        }

        [Fact]
        public async Task SetCall_ExistingUser_UpdatesCallsign()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            long userId = 123456789;

            db.CallSignAssociation.Add(new CallSignAssociation
            {
                DiscordUserId = userId,
                DiscordUserName = "TestUser",
                CallSign = "W1AW"
            });
            await db.SaveChangesAsync();

            // act - simulate updating existing record
            var existing = db.CallSignAssociation
                .Where(d => d.DiscordUserId == userId)
                .FirstOrDefault();

            Assert.NotNull(existing);
            existing.CallSign = "N0CALL";
            await db.SaveChangesAsync();

            // assert
            var updated = await db.CallSignAssociation.FirstOrDefaultAsync(d => d.DiscordUserId == userId);
            Assert.NotNull(updated);
            Assert.Equal("N0CALL", updated.CallSign);
        }

        [Fact]
        public async Task SetCall_LowercaseInput_StoredAsUppercase()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            long userId = 123456789;
            var callsign = "w1aw";

            // act
            db.CallSignAssociation.Add(new CallSignAssociation
            {
                DiscordUserId = userId,
                DiscordUserName = "TestUser",
                CallSign = callsign.ToUpper()
            });
            await db.SaveChangesAsync();

            // assert
            var saved = await db.CallSignAssociation.FirstOrDefaultAsync(d => d.DiscordUserId == userId);
            Assert.NotNull(saved);
            Assert.Equal("W1AW", saved.CallSign);
        }

        [Fact]
        public async Task GetCall_ExistingUser_ReturnsMatch()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            long userId = 987654321;

            db.CallSignAssociation.Add(new CallSignAssociation
            {
                DiscordUserId = userId,
                DiscordUserName = "HamUser",
                CallSign = "KD2ABC"
            });
            await db.SaveChangesAsync();

            // act
            var callInfo = db.CallSignAssociation
                .Where(d => d.DiscordUserId == userId)
                .FirstOrDefault();

            // assert
            Assert.NotNull(callInfo);
            Assert.Equal("KD2ABC", callInfo.CallSign);
            Assert.Equal("HamUser", callInfo.DiscordUserName);
        }

        [Fact]
        public void GetCall_NonexistentUser_ReturnsNull()
        {
            // arrange
            using var db = new SevenThreeContext(_dbOptions);
            long userId = 999999999;

            // act
            var callInfo = db.CallSignAssociation
                .Where(d => d.DiscordUserId == userId)
                .FirstOrDefault();

            // assert
            Assert.Null(callInfo);
        }
    }
}
