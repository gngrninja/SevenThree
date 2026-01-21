using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SevenThree.Constants;
using SevenThree.Database;
using SevenThree.Models;
using SevenThree.Modules;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class QuizButtonHandlerTests
    {
        private readonly HamTestService _hamTestService;
        private readonly Mock<ILogger<QuizButtonHandler>> _mockLogger;
        private readonly QuizButtonHandler _sut;

        public QuizButtonHandlerTests()
        {
            // Create mock factory and logger for HamTestService
            var mockFactory = new Mock<IDbContextFactory<SevenThreeContext>>();
            var mockHamTestLogger = new Mock<ILogger<HamTestService>>();
            _hamTestService = new HamTestService(mockFactory.Object, mockHamTestLogger.Object);

            _mockLogger = new Mock<ILogger<QuizButtonHandler>>();

            _sut = new QuizButtonHandler(_hamTestService, _mockLogger.Object);
        }

        #region Button ID Parsing Tests

        [Fact]
        public void QuizButtonId_ParsesParts_Correctly()
        {
            // arrange
            var sessionId = "123456789";
            var answer = "A";
            var customId = $"{QuizConstants.BUTTON_PREFIX}:{sessionId}:{answer}";

            // act
            var parts = customId.Split(':');

            // assert
            Assert.Equal(3, parts.Length);
            Assert.Equal(QuizConstants.BUTTON_PREFIX, parts[0]);
            Assert.Equal(sessionId, parts[1]);
            Assert.Equal(answer, parts[2]);
        }

        [Fact]
        public void StopButtonId_ParsesParts_Correctly()
        {
            // arrange
            var sessionId = "123456789";
            var customId = $"{QuizConstants.STOP_BUTTON_PREFIX}:{sessionId}";

            // act
            var parts = customId.Split(':');

            // assert
            Assert.Equal(2, parts.Length);
            Assert.Equal(QuizConstants.STOP_BUTTON_PREFIX, parts[0]);
            Assert.Equal(sessionId, parts[1]);
        }

        [Theory]
        [InlineData("quiz:123456789:A", true)]
        [InlineData("quiz:123456789:B", true)]
        [InlineData("quiz:123456789:C", true)]
        [InlineData("quiz:123456789:D", true)]
        [InlineData("quiz:invalid:A", false)]
        [InlineData("invalid", false)]
        public void QuizButtonId_SessionIdParsing(string customId, bool shouldParse)
        {
            // act
            var parts = customId.Split(':');
            var canParse = parts.Length >= 2 && ulong.TryParse(parts[1], out var sessionId);

            // assert
            Assert.Equal(shouldParse, canParse);
        }

        [Theory]
        [InlineData("quiz:123456789:A", true)]
        [InlineData("quiz:123456789", false)]
        public void QuizButtonId_HasAllParts(string customId, bool hasAllParts)
        {
            // Quiz button needs 3 parts: prefix, sessionId, answer
            var parts = customId.Split(':');
            var valid = parts.Length >= 3;

            Assert.Equal(hasAllParts, valid);
        }

        [Theory]
        [InlineData("quizstop:123456789", true)]
        [InlineData("quizstop:987654321", true)]
        [InlineData("quizstop:invalid", false)]
        [InlineData("quizstop", false)]
        public void StopButtonId_SessionIdParsing(string customId, bool shouldParse)
        {
            // act
            var parts = customId.Split(':');
            var canParse = parts.Length >= 2 && ulong.TryParse(parts[1], out var sessionId);

            // assert
            Assert.Equal(shouldParse, canParse);
        }

        #endregion

        #region Answer Validation Tests

        [Theory]
        [InlineData("A")]
        [InlineData("B")]
        [InlineData("C")]
        [InlineData("D")]
        public void ValidAnswers_AreRecognized(string answer)
        {
            // arrange
            var validAnswers = new[] { "A", "B", "C", "D" };

            // act
            var isValid = Array.Exists(validAnswers, a => a == answer);

            // assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("E")]
        [InlineData("1")]
        [InlineData("")]
        [InlineData("AB")]
        [InlineData("a")]
        public void InvalidAnswers_AreRejected(string answer)
        {
            // arrange
            var validAnswers = new[] { "A", "B", "C", "D" };

            // act
            var isValid = Array.Exists(validAnswers, a => a == answer);

            // assert
            Assert.False(isValid);
        }

        #endregion

        #region Session Lookup Tests

        [Fact]
        public void RunningTests_NonexistentSession_ReturnsNull()
        {
            // arrange
            var sessionId = 999999UL;

            // act
            var found = _hamTestService.RunningTests.TryGetValue(sessionId, out var quizUtil);

            // assert
            Assert.False(found);
            Assert.Null(quizUtil);
        }

        [Fact]
        public void RunningTests_ConcurrentDictionary_CanCheckContainsKey()
        {
            // arrange
            var sessionId = 123456789UL;

            // assert - dictionary starts empty
            Assert.False(_hamTestService.RunningTests.ContainsKey(sessionId));
        }

        #endregion

        #region Button Format Tests

        [Fact]
        public void CreateQuizButtonId_FormatsCorrectly()
        {
            // arrange
            var sessionId = 123456789UL;
            var answer = "B";

            // act
            var buttonId = $"{QuizConstants.BUTTON_PREFIX}:{sessionId}:{answer}";

            // assert
            Assert.Equal("quiz:123456789:B", buttonId);
        }

        [Fact]
        public void CreateStopButtonId_FormatsCorrectly()
        {
            // arrange
            var sessionId = 123456789UL;

            // act
            var buttonId = $"{QuizConstants.STOP_BUTTON_PREFIX}:{sessionId}";

            // assert
            Assert.Equal("quizstop:123456789", buttonId);
        }

        #endregion
    }
}
