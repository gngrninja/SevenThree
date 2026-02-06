using Xunit;
using SevenThree.Constants;
using SevenThree.Services;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests the button routing logic used by InteractionHandler.HandleButtonExecutedAsync.
    /// This ensures that all button prefixes are unique and route to the correct handler.
    /// </summary>
    public class InteractionRoutingTests
    {
        // All known button prefixes in the system
        private static readonly string[] AllPrefixes = new[]
        {
            QuizConstants.BUTTON_PREFIX,       // "quiz"
            QuizConstants.STOP_BUTTON_PREFIX,  // "quizstop"
            PskReporterService.BUTTON_PREFIX,  // "psk"
            StudyConstants.BUTTON_PREFIX,       // "study"
            StudyConstants.RETRY_BUTTON_PREFIX  // "studyretry"
        };

        #region Prefix Uniqueness

        [Fact]
        public void AllPrefixes_WithColon_AreUnique()
        {
            // InteractionHandler routes using StartsWith("{prefix}:")
            // No colon-suffixed prefix should be a prefix of another
            for (int i = 0; i < AllPrefixes.Length; i++)
            {
                for (int j = 0; j < AllPrefixes.Length; j++)
                {
                    if (i == j) continue;

                    var routeA = $"{AllPrefixes[i]}:";
                    var routeB = $"{AllPrefixes[j]}:";

                    Assert.False(routeB.StartsWith(routeA),
                        $"Route conflict: \"{routeB}\" starts with \"{routeA}\" - buttons would be misrouted");
                }
            }
        }

        #endregion

        #region Exact Routing Tests

        [Theory]
        [InlineData("quiz:123:A", "quiz")]
        [InlineData("quizstop:123", "quizstop")]
        [InlineData("psk:next:abc:0", "psk")]
        [InlineData("study:show:abc:0", "study")]
        [InlineData("studyretry:abc:A:101", "studyretry")]
        public void ButtonId_RoutesToExactlyOneHandler(string buttonId, string expectedPrefix)
        {
            int matchCount = 0;
            string matchedPrefix = null;

            foreach (var prefix in AllPrefixes)
            {
                if (buttonId.StartsWith($"{prefix}:"))
                {
                    matchCount++;
                    matchedPrefix = prefix;
                }
            }

            Assert.Equal(1, matchCount);
            Assert.Equal(expectedPrefix, matchedPrefix);
        }

        [Theory]
        [InlineData("unknown:something")]
        [InlineData("randombutton")]
        [InlineData("")]
        public void UnknownButtonId_MatchesNoHandler(string buttonId)
        {
            foreach (var prefix in AllPrefixes)
            {
                Assert.False(buttonId.StartsWith($"{prefix}:"),
                    $"Unknown button \"{buttonId}\" should not match prefix \"{prefix}\"");
            }
        }

        #endregion

        #region Handler Routing Order Tests

        [Fact]
        public void QuizButton_RoutedBeforeQuizStop()
        {
            // InteractionHandler checks quiz: before quizstop:
            // This verifies that "quiz:" doesn't accidentally match "quizstop:" buttons
            var quizStopButton = $"{QuizConstants.STOP_BUTTON_PREFIX}:123";

            Assert.False(quizStopButton.StartsWith($"{QuizConstants.BUTTON_PREFIX}:"),
                "quizstop button should NOT match quiz: prefix");
            Assert.StartsWith($"{QuizConstants.STOP_BUTTON_PREFIX}:", quizStopButton);
        }

        [Fact]
        public void StudyButton_RoutedBeforeStudyRetry()
        {
            // "study:" should not match "studyretry:" buttons
            var retryButton = $"{StudyConstants.RETRY_BUTTON_PREFIX}:abc:A:101";

            Assert.False(retryButton.StartsWith($"{StudyConstants.BUTTON_PREFIX}:"),
                "studyretry button should NOT match study: prefix");
            Assert.StartsWith($"{StudyConstants.RETRY_BUTTON_PREFIX}:", retryButton);
        }

        #endregion

        #region PSK Prefix Isolation

        [Fact]
        public void PskPrefix_DoesNotConflictWithAnyOther()
        {
            var pskRoute = $"{PskReporterService.BUTTON_PREFIX}:";

            foreach (var prefix in AllPrefixes)
            {
                if (prefix == PskReporterService.BUTTON_PREFIX) continue;

                var otherRoute = $"{prefix}:";
                Assert.False(pskRoute.StartsWith(otherRoute),
                    $"psk route starts with {otherRoute}");
                Assert.False(otherRoute.StartsWith(pskRoute),
                    $"{otherRoute} starts with psk route");
            }
        }

        #endregion
    }
}
