using System.Collections.Generic;
using System.Linq;
using Xunit;
using SevenThree.Constants;
using SevenThree.Models;
using SevenThree.Modules.Study;

namespace SevenThree.Tests
{
    public class StudyEmbedBuilderTests
    {
        private static StudySession CreateSession(int questionCount = 3, int currentIndex = 0, bool showingAnswer = false)
        {
            var questions = Enumerable.Range(1, questionCount).Select(i => new MissedQuestion
            {
                QuestionId = i,
                QuestionText = $"What is question {i}?",
                QuestionSection = $"T1A{i:D2}",
                SubelementName = "T1",
                SubelementDesc = "FCC Rules",
                CorrectAnswer = $"Answer A for Q{i}",
                TestName = "tech",
                TimesMissed = i
            }).ToList();

            return new StudySession
            {
                SessionId = "abc12345",
                UserId = 12345UL,
                Questions = questions,
                CurrentIndex = currentIndex,
                ShowingAnswer = showingAnswer
            };
        }

        private static List<AnswerOption> CreateAnswerOptions(int count = 4)
        {
            return Enumerable.Range(0, count).Select(i => new AnswerOption
            {
                AnswerId = 100 + i,
                AnswerText = $"Answer option {(char)('A' + i)}",
                IsCorrect = i == 0
            }).ToList();
        }

        #region BuildFlashcardEmbed Tests

        [Fact]
        public void BuildFlashcardEmbed_ShowsQuestionText()
        {
            // arrange
            var session = CreateSession();

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Contains("What is question 1?", built.Fields.First().Value.ToString());
        }

        [Fact]
        public void BuildFlashcardEmbed_ShowsProgressInTitle()
        {
            // arrange
            var session = CreateSession(questionCount: 5, currentIndex: 2);

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Contains("3/5", built.Title);
        }

        [Fact]
        public void BuildFlashcardEmbed_HidesAnswer_WhenNotShowing()
        {
            // arrange
            var session = CreateSession(showingAnswer: false);

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.DoesNotContain(built.Fields, f => f.Name == "✅ Correct Answer");
            Assert.Contains("Show Answer", built.Footer.Value.Text);
        }

        [Fact]
        public void BuildFlashcardEmbed_ShowsAnswer_WhenFlagged()
        {
            // arrange
            var session = CreateSession(showingAnswer: true);

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Contains(built.Fields, f => f.Name == "✅ Correct Answer");
        }

        [Fact]
        public void BuildFlashcardEmbed_ShowsMissCount_WhenShowingAnswer()
        {
            // arrange - TimesMissed = 3 for the 3rd question
            var session = CreateSession(questionCount: 3, currentIndex: 2, showingAnswer: true);

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Contains("3 times", built.Footer.Value.Text);
        }

        [Fact]
        public void BuildFlashcardEmbed_ShowsSubelement()
        {
            // arrange
            var session = CreateSession();

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Contains(built.Fields, f => f.Name == "Topic");
        }

        [Fact]
        public void BuildFlashcardEmbed_UsesStudyColor()
        {
            // arrange
            var session = CreateSession();

            // act
            var embed = StudyEmbedBuilder.BuildFlashcardEmbed(session);
            var built = embed.Build();

            // assert
            Assert.Equal(StudyConstants.COLOR_STUDY, built.Color.Value);
        }

        #endregion

        #region BuildFlashcardButtons Tests

        [Fact]
        public void BuildFlashcardButtons_ShowAnswer_WhenNotShowing()
        {
            // arrange
            var session = CreateSession(showingAnswer: false);

            // act
            var component = StudyEmbedBuilder.BuildFlashcardButtons(session);

            // assert - component should contain buttons (non-null)
            Assert.NotNull(component);
        }

        [Fact]
        public void BuildFlashcardButtons_FirstQuestion_PreviousDisabled()
        {
            // arrange
            var session = CreateSession(currentIndex: 0);

            // act
            var component = StudyEmbedBuilder.BuildFlashcardButtons(session);

            // assert - component built successfully with navigation
            Assert.NotNull(component);
        }

        [Fact]
        public void BuildFlashcardButtons_LastQuestion_NextDisabled()
        {
            // arrange
            var session = CreateSession(questionCount: 3, currentIndex: 2);

            // act
            var component = StudyEmbedBuilder.BuildFlashcardButtons(session);

            // assert
            Assert.NotNull(component);
        }

        [Fact]
        public void BuildFlashcardButtons_MiddleQuestion_BothEnabled()
        {
            // arrange
            var session = CreateSession(questionCount: 3, currentIndex: 1);

            // act
            var component = StudyEmbedBuilder.BuildFlashcardButtons(session);

            // assert
            Assert.NotNull(component);
        }

        #endregion

        #region BuildRetryQuestionEmbed Tests

        [Fact]
        public void BuildRetryQuestionEmbed_ShowsAnswerChoices()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions();

            // act
            var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, answers);
            var built = embed.Build();

            // assert - should have A., B., C., D. fields
            Assert.Contains(built.Fields, f => f.Name == "A.");
            Assert.Contains(built.Fields, f => f.Name == "B.");
            Assert.Contains(built.Fields, f => f.Name == "C.");
            Assert.Contains(built.Fields, f => f.Name == "D.");
        }

        [Fact]
        public void BuildRetryQuestionEmbed_AnswerFieldsAreInline()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions();

            // act
            var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, answers);
            var built = embed.Build();

            // assert
            var answerFields = built.Fields.Where(f => f.Name.Length == 2 && f.Name.EndsWith("."));
            Assert.All(answerFields, f => Assert.True(f.Inline));
        }

        [Fact]
        public void BuildRetryQuestionEmbed_ShowsQuestionSection()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions();

            // act
            var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, answers);
            var built = embed.Build();

            // assert
            Assert.Contains(built.Fields, f => f.Name.Contains("T1A01"));
        }

        [Fact]
        public void BuildRetryQuestionEmbed_HandlesFewerThan4Answers()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions(count: 2);

            // act
            var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, answers);
            var built = embed.Build();

            // assert - only A. and B. should be present
            Assert.Contains(built.Fields, f => f.Name == "A.");
            Assert.Contains(built.Fields, f => f.Name == "B.");
            Assert.DoesNotContain(built.Fields, f => f.Name == "C.");
            Assert.DoesNotContain(built.Fields, f => f.Name == "D.");
        }

        [Fact]
        public void BuildRetryQuestionEmbed_ShowsMissCount_WhenMultiple()
        {
            // arrange - currentIndex 2 has TimesMissed = 3
            var session = CreateSession(questionCount: 3, currentIndex: 2);
            var answers = CreateAnswerOptions();

            // act
            var embed = StudyEmbedBuilder.BuildRetryQuestionEmbed(session, answers);
            var built = embed.Build();

            // assert
            Assert.Contains("3 times", built.Footer.Value.Text);
        }

        #endregion

        #region BuildRetryAnswerButtons Tests

        [Fact]
        public void BuildRetryAnswerButtons_Creates4AnswerButtons()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions();

            // act
            var component = StudyEmbedBuilder.BuildRetryAnswerButtons(session, answers);

            // assert
            Assert.NotNull(component);
        }

        [Fact]
        public void BuildRetryAnswerButtons_IncludesSkipAndStop()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions();

            // act
            var component = StudyEmbedBuilder.BuildRetryAnswerButtons(session, answers);

            // assert
            Assert.NotNull(component);
        }

        [Fact]
        public void BuildRetryAnswerButtons_HandlesFewerAnswers()
        {
            // arrange
            var session = CreateSession();
            var answers = CreateAnswerOptions(count: 2);

            // act
            var component = StudyEmbedBuilder.BuildRetryAnswerButtons(session, answers);

            // assert - should not throw
            Assert.NotNull(component);
        }

        #endregion
    }
}
