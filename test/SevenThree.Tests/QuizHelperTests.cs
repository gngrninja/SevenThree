using System;
using Xunit;
using SevenThree.Modules;

namespace SevenThree.Tests
{
    public class QuizHelperTests
    {
        private readonly QuizHelper _sut;

        public QuizHelperTests()
        {
            _sut = new QuizHelper();
        }

        #region GetNumberEmojiFromInt Tests

        [Theory]
        [InlineData(0, ":zero:")]
        [InlineData(1, ":one:")]
        [InlineData(2, ":two:")]
        [InlineData(3, ":three:")]
        [InlineData(4, ":four:")]
        [InlineData(5, ":five:")]
        [InlineData(6, ":six:")]
        [InlineData(7, ":seven:")]
        [InlineData(8, ":eight:")]
        [InlineData(9, ":nine:")]
        public void GetNumberEmojiFromInt_SingleDigit_ReturnsCorrectEmoji(int input, string expected)
        {
            // act
            var result = _sut.GetNumberEmojiFromInt(input);

            // assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(10, ":one::zero:")]
        [InlineData(11, ":one::one:")]
        [InlineData(12, ":one::two:")]
        [InlineData(13, ":one::three:")]
        [InlineData(14, ":one::four:")]
        public void GetNumberEmojiFromInt_DoubleDigit_ReturnsCorrectEmoji(int input, string expected)
        {
            // act
            var result = _sut.GetNumberEmojiFromInt(input);

            // assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(15)]
        [InlineData(100)]
        [InlineData(999)]
        public void GetNumberEmojiFromInt_OutOfRange_ReturnsZeroEmoji(int input)
        {
            // act
            var result = _sut.GetNumberEmojiFromInt(input);

            // assert
            Assert.Equal(":zero:", result);
        }

        #endregion

        #region GetPassFail Tests

        [Theory]
        [InlineData(74)]
        [InlineData(75)]
        [InlineData(100)]
        [InlineData(99.9)]
        public void GetPassFail_PassingScore_ReturnsCheckMark(decimal percentage)
        {
            // act
            var result = _sut.GetPassFail(percentage);

            // assert
            Assert.Equal(":white_check_mark:", result);
        }

        [Theory]
        [InlineData(73)]
        [InlineData(73.9)]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(1)]
        public void GetPassFail_FailingScore_ReturnsNoEntry(decimal percentage)
        {
            // act
            var result = _sut.GetPassFail(percentage);

            // assert
            Assert.Equal(":no_entry_sign:", result);
        }

        [Fact]
        public void GetPassFail_ExactlyAtThreshold_Passes()
        {
            // The FCC passing score is 74%
            // act
            var result = _sut.GetPassFail(74m);

            // assert
            Assert.Equal(":white_check_mark:", result);
        }

        [Fact]
        public void GetPassFail_JustBelowThreshold_Fails()
        {
            // act
            var result = _sut.GetPassFail(73.99m);

            // assert
            Assert.Equal(":no_entry_sign:", result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        public void GetPassFail_NegativeScore_Fails(decimal percentage)
        {
            // act
            var result = _sut.GetPassFail(percentage);

            // assert
            Assert.Equal(":no_entry_sign:", result);
        }

        #endregion
    }
}
