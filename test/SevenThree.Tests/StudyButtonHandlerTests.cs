using System;
using System.Collections.Generic;
using Xunit;
using SevenThree.Constants;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class StudyButtonHandlerTests
    {
        #region Study Button ID Parsing Tests

        [Theory]
        [InlineData("study:show:abc123:0", "study", "show", "abc123", "0")]
        [InlineData("study:hide:abc123:0", "study", "hide", "abc123", "0")]
        [InlineData("study:next:abc123:1", "study", "next", "abc123", "1")]
        [InlineData("study:prev:abc123:2", "study", "prev", "abc123", "2")]
        [InlineData("study:done:abc123:3", "study", "done", "abc123", "3")]
        public void StudyButtonId_ParsesAllParts(string buttonId, string prefix, string action, string sessionId, string index)
        {
            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(4, parts.Length);
            Assert.Equal(prefix, parts[0]);
            Assert.Equal(action, parts[1]);
            Assert.Equal(sessionId, parts[2]);
            Assert.Equal(index, parts[3]);
        }

        [Theory]
        [InlineData("study:show:abc123:0", true)]
        [InlineData("study:next:abc123:1", true)]
        [InlineData("study", false)]
        [InlineData("study:show", false)]
        [InlineData("study:show:abc123", false)]
        public void StudyButtonId_ValidatesMinimumParts(string buttonId, bool isValid)
        {
            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(isValid, parts.Length >= 4);
        }

        [Theory]
        [InlineData("show")]
        [InlineData("hide")]
        [InlineData("next")]
        [InlineData("prev")]
        [InlineData("done")]
        public void StudyButtonAction_RecognizesValidActions(string action)
        {
            // arrange
            var validActions = new[] { "show", "hide", "next", "prev", "done" };

            // assert
            Assert.Contains(action, validActions);
        }

        #endregion

        #region Retry Button ID Parsing Tests

        [Theory]
        [InlineData("studyretry:abc123:A:101", "studyretry", "abc123", "A", "101")]
        [InlineData("studyretry:abc123:B:102", "studyretry", "abc123", "B", "102")]
        [InlineData("studyretry:abc123:skip:0", "studyretry", "abc123", "skip", "0")]
        [InlineData("studyretry:abc123:stop:0", "studyretry", "abc123", "stop", "0")]
        public void RetryButtonId_ParsesAllParts(string buttonId, string prefix, string sessionId, string answer, string answerId)
        {
            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(4, parts.Length);
            Assert.Equal(prefix, parts[0]);
            Assert.Equal(sessionId, parts[1]);
            Assert.Equal(answer, parts[2]);
            Assert.Equal(answerId, parts[3]);
        }

        [Theory]
        [InlineData("studyretry:abc123:A:101", true)]
        [InlineData("studyretry:abc123", false)]
        [InlineData("studyretry", false)]
        public void RetryButtonId_ValidatesMinimumParts(string buttonId, bool isValid)
        {
            // act
            var parts = buttonId.Split(':');

            // assert
            Assert.Equal(isValid, parts.Length >= 4);
        }

        [Theory]
        [InlineData("101", true)]
        [InlineData("0", true)]
        [InlineData("invalid", false)]
        [InlineData("", false)]
        public void RetryButtonId_AnswerIdParsesAsInt(string answerIdStr, bool shouldParse)
        {
            // act
            var canParse = int.TryParse(answerIdStr, out _);

            // assert
            Assert.Equal(shouldParse, canParse);
        }

        #endregion

        #region Button Routing Tests

        [Fact]
        public void StudyButton_MatchesStudyPrefix()
        {
            // arrange
            var buttonId = $"{StudyConstants.BUTTON_PREFIX}:show:abc123:0";

            // act - simulate InteractionHandler routing
            var matchesStudy = buttonId.StartsWith($"{StudyConstants.BUTTON_PREFIX}:");
            var matchesRetry = buttonId.StartsWith($"{StudyConstants.RETRY_BUTTON_PREFIX}:");
            var matchesQuiz = buttonId.StartsWith($"{QuizConstants.BUTTON_PREFIX}:");

            // assert
            Assert.True(matchesStudy);
            Assert.False(matchesRetry);
            Assert.False(matchesQuiz);
        }

        [Fact]
        public void RetryButton_MatchesRetryPrefix()
        {
            // arrange
            var buttonId = $"{StudyConstants.RETRY_BUTTON_PREFIX}:abc123:A:101";

            // act
            var matchesStudy = buttonId.StartsWith($"{StudyConstants.BUTTON_PREFIX}:");
            var matchesRetry = buttonId.StartsWith($"{StudyConstants.RETRY_BUTTON_PREFIX}:");
            var matchesQuiz = buttonId.StartsWith($"{QuizConstants.BUTTON_PREFIX}:");

            // assert
            Assert.False(matchesStudy);
            Assert.True(matchesRetry);
            Assert.False(matchesQuiz);
        }

        [Fact]
        public void QuizButton_DoesNotMatchStudyPrefixes()
        {
            // arrange
            var buttonId = $"{QuizConstants.BUTTON_PREFIX}:123456789:A";

            // act
            var matchesStudy = buttonId.StartsWith($"{StudyConstants.BUTTON_PREFIX}:");
            var matchesRetry = buttonId.StartsWith($"{StudyConstants.RETRY_BUTTON_PREFIX}:");

            // assert
            Assert.False(matchesStudy);
            Assert.False(matchesRetry);
        }

        #endregion

        #region Session Navigation Logic Tests

        [Theory]
        [InlineData(0, 5, true)]   // first question, has next
        [InlineData(2, 5, true)]   // middle question, has next
        [InlineData(4, 5, false)]  // last question, no next
        public void Navigation_HasNext_CalculatesCorrectly(int currentIndex, int totalQuestions, bool expectedHasNext)
        {
            // act - mirrors the logic in StudyButtonHandler
            var hasNext = currentIndex < totalQuestions - 1;

            // assert
            Assert.Equal(expectedHasNext, hasNext);
        }

        [Theory]
        [InlineData(0, false)]  // first question, no prev
        [InlineData(1, true)]   // second question, has prev
        [InlineData(4, true)]   // last question, has prev
        public void Navigation_HasPrev_CalculatesCorrectly(int currentIndex, bool expectedHasPrev)
        {
            // act - mirrors the logic in StudyButtonHandler
            var hasPrev = currentIndex > 0;

            // assert
            Assert.Equal(expectedHasPrev, hasPrev);
        }

        [Theory]
        [InlineData(0, "next", 1)]
        [InlineData(1, "next", 2)]
        [InlineData(2, "prev", 1)]
        [InlineData(1, "prev", 0)]
        public void Navigation_Action_UpdatesIndex(int startIndex, string action, int expectedIndex)
        {
            // arrange
            var totalQuestions = 5;
            var currentIndex = startIndex;

            // act - mirrors StudyButtonHandler switch logic
            switch (action)
            {
                case "next":
                    if (currentIndex < totalQuestions - 1) currentIndex++;
                    break;
                case "prev":
                    if (currentIndex > 0) currentIndex--;
                    break;
            }

            // assert
            Assert.Equal(expectedIndex, currentIndex);
        }

        [Fact]
        public void Navigation_Next_AtLastQuestion_DoesNotOverflow()
        {
            // arrange
            var totalQuestions = 3;
            var currentIndex = 2; // last index

            // act
            if (currentIndex < totalQuestions - 1) currentIndex++;

            // assert - stays at 2
            Assert.Equal(2, currentIndex);
        }

        [Fact]
        public void Navigation_Prev_AtFirstQuestion_DoesNotUnderflow()
        {
            // arrange
            var currentIndex = 0;

            // act
            if (currentIndex > 0) currentIndex--;

            // assert - stays at 0
            Assert.Equal(0, currentIndex);
        }

        #endregion

        #region Answer Correctness Logic Tests

        [Fact]
        public void RetryAnswer_CorrectAnswer_IsIdentified()
        {
            // arrange
            var answers = new List<AnswerOption>
            {
                new() { AnswerId = 101, AnswerText = "Correct", IsCorrect = true },
                new() { AnswerId = 102, AnswerText = "Wrong 1", IsCorrect = false },
                new() { AnswerId = 103, AnswerText = "Wrong 2", IsCorrect = false },
                new() { AnswerId = 104, AnswerText = "Wrong 3", IsCorrect = false }
            };
            var selectedId = 101;

            // act - mirrors StudyButtonHandler answer checking
            var selected = answers.Find(a => a.AnswerId == selectedId);
            var isCorrect = selected?.IsCorrect ?? false;

            // assert
            Assert.True(isCorrect);
        }

        [Fact]
        public void RetryAnswer_WrongAnswer_IsIdentified()
        {
            // arrange
            var answers = new List<AnswerOption>
            {
                new() { AnswerId = 101, AnswerText = "Correct", IsCorrect = true },
                new() { AnswerId = 102, AnswerText = "Wrong", IsCorrect = false }
            };
            var selectedId = 102;

            // act
            var selected = answers.Find(a => a.AnswerId == selectedId);
            var isCorrect = selected?.IsCorrect ?? false;

            // assert
            Assert.False(isCorrect);
        }

        [Fact]
        public void RetryAnswer_InvalidAnswerId_TreatedAsIncorrect()
        {
            // arrange
            var answers = new List<AnswerOption>
            {
                new() { AnswerId = 101, AnswerText = "Correct", IsCorrect = true }
            };
            var selectedId = 999; // not in list

            // act
            var selected = answers.Find(a => a.AnswerId == selectedId);
            var isCorrect = selected?.IsCorrect ?? false;

            // assert
            Assert.False(isCorrect);
        }

        #endregion
    }
}
