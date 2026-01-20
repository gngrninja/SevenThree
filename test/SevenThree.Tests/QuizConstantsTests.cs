using Xunit;
using SevenThree.Constants;

namespace SevenThree.Tests
{
    public class QuizConstantsTests
    {
        [Fact]
        public void BUTTON_PREFIX_HasExpectedValue()
        {
            // assert
            Assert.Equal("quiz", QuizConstants.BUTTON_PREFIX);
        }

        [Fact]
        public void STOP_BUTTON_PREFIX_HasExpectedValue()
        {
            // assert
            Assert.Equal("quizstop", QuizConstants.STOP_BUTTON_PREFIX);
        }

        [Fact]
        public void ButtonPrefixes_AreDifferent()
        {
            // assert
            Assert.NotEqual(QuizConstants.BUTTON_PREFIX, QuizConstants.STOP_BUTTON_PREFIX);
        }

        [Theory]
        [InlineData("123456789", "A")]
        [InlineData("987654321", "B")]
        [InlineData("111222333", "C")]
        [InlineData("444555666", "D")]
        public void ButtonId_Format_MatchesExpectedPattern(string sessionId, string answer)
        {
            // arrange
            var expectedFormat = $"{QuizConstants.BUTTON_PREFIX}:{sessionId}:{answer}";

            // act
            var parts = expectedFormat.Split(':');

            // assert
            Assert.Equal(3, parts.Length);
            Assert.Equal(QuizConstants.BUTTON_PREFIX, parts[0]);
            Assert.Equal(sessionId, parts[1]);
            Assert.Equal(answer, parts[2]);
        }

        [Theory]
        [InlineData("123456789")]
        [InlineData("987654321")]
        public void StopButtonId_Format_MatchesExpectedPattern(string sessionId)
        {
            // arrange
            var expectedFormat = $"{QuizConstants.STOP_BUTTON_PREFIX}:{sessionId}";

            // act
            var parts = expectedFormat.Split(':');

            // assert
            Assert.Equal(2, parts.Length);
            Assert.Equal(QuizConstants.STOP_BUTTON_PREFIX, parts[0]);
            Assert.Equal(sessionId, parts[1]);
        }
    }
}
