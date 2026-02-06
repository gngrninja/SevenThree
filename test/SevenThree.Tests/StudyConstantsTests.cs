using Xunit;
using SevenThree.Constants;

namespace SevenThree.Tests
{
    public class StudyConstantsTests
    {
        [Fact]
        public void BUTTON_PREFIX_HasExpectedValue()
        {
            Assert.Equal("study", StudyConstants.BUTTON_PREFIX);
        }

        [Fact]
        public void RETRY_BUTTON_PREFIX_HasExpectedValue()
        {
            Assert.Equal("studyretry", StudyConstants.RETRY_BUTTON_PREFIX);
        }

        [Fact]
        public void ButtonPrefixes_AreDifferent()
        {
            Assert.NotEqual(StudyConstants.BUTTON_PREFIX, StudyConstants.RETRY_BUTTON_PREFIX);
        }

        [Fact]
        public void StudyPrefixes_DoNotCollideWithQuizPrefixes()
        {
            // Critical: button routing uses StartsWith to dispatch, so prefixes must be unique
            Assert.False(StudyConstants.BUTTON_PREFIX.StartsWith(QuizConstants.BUTTON_PREFIX));
            Assert.False(QuizConstants.BUTTON_PREFIX.StartsWith(StudyConstants.BUTTON_PREFIX));

            Assert.False(StudyConstants.RETRY_BUTTON_PREFIX.StartsWith(QuizConstants.BUTTON_PREFIX));
            Assert.False(QuizConstants.BUTTON_PREFIX.StartsWith(StudyConstants.RETRY_BUTTON_PREFIX));

            Assert.False(StudyConstants.BUTTON_PREFIX.StartsWith(QuizConstants.STOP_BUTTON_PREFIX));
            Assert.False(QuizConstants.STOP_BUTTON_PREFIX.StartsWith(StudyConstants.BUTTON_PREFIX));
        }

        [Fact]
        public void StudyPrefix_DoesNotStartWithRetryPrefix()
        {
            // "study" must not be a prefix of "studyretry" button IDs
            // This is important: "study:..." must not match "studyretry:..." in StartsWith routing
            // Routing checks "study:" with the colon, so "studyretry:" won't match
            var studyRoute = $"{StudyConstants.BUTTON_PREFIX}:";
            var retryRoute = $"{StudyConstants.RETRY_BUTTON_PREFIX}:";

            Assert.False(retryRoute.StartsWith(studyRoute));
        }

        [Fact]
        public void SESSION_CACHE_MINUTES_IsPositive()
        {
            Assert.True(StudyConstants.SESSION_CACHE_MINUTES > 0);
        }

        [Fact]
        public void WEAK_THRESHOLD_IsAtLeast2()
        {
            Assert.True(StudyConstants.WEAK_THRESHOLD >= 2);
        }

        #region Button ID Format Tests

        [Theory]
        [InlineData("show", "abc123", "0")]
        [InlineData("hide", "abc123", "0")]
        [InlineData("next", "abc123", "1")]
        [InlineData("prev", "abc123", "1")]
        [InlineData("done", "abc123", "2")]
        public void StudyButtonId_Format_ParsesCorrectly(string action, string sessionId, string index)
        {
            // arrange
            var buttonId = $"{StudyConstants.BUTTON_PREFIX}:{action}:{sessionId}:{index}";

            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(4, parts.Length);
            Assert.Equal(StudyConstants.BUTTON_PREFIX, parts[0]);
            Assert.Equal(action, parts[1]);
            Assert.Equal(sessionId, parts[2]);
            Assert.Equal(index, parts[3]);
        }

        [Theory]
        [InlineData("abc123", "A", "101")]
        [InlineData("abc123", "skip", "0")]
        [InlineData("abc123", "stop", "0")]
        public void RetryButtonId_Format_ParsesCorrectly(string sessionId, string answer, string answerId)
        {
            // arrange
            var buttonId = $"{StudyConstants.RETRY_BUTTON_PREFIX}:{sessionId}:{answer}:{answerId}";

            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(4, parts.Length);
            Assert.Equal(StudyConstants.RETRY_BUTTON_PREFIX, parts[0]);
            Assert.Equal(sessionId, parts[1]);
            Assert.Equal(answer, parts[2]);
            Assert.Equal(answerId, parts[3]);
        }

        #endregion
    }
}
