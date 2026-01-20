using System;
using Xunit;
using SevenThree.Models;

namespace SevenThree.Tests
{
    public class QuizModeTests
    {
        [Fact]
        public void QuizMode_HasPrivateValue()
        {
            // assert
            Assert.True(Enum.IsDefined(typeof(QuizMode), QuizMode.Private));
        }

        [Fact]
        public void QuizMode_HasPublicValue()
        {
            // assert
            Assert.True(Enum.IsDefined(typeof(QuizMode), QuizMode.Public));
        }

        [Fact]
        public void QuizMode_Private_IsDefault()
        {
            // arrange
            QuizMode defaultMode = default;

            // assert
            Assert.Equal(QuizMode.Private, defaultMode);
        }

        [Fact]
        public void QuizMode_HasOnlyTwoValues()
        {
            // act
            var values = Enum.GetValues<QuizMode>();

            // assert
            Assert.Equal(2, values.Length);
        }

        [Theory]
        [InlineData(QuizMode.Private, "Private")]
        [InlineData(QuizMode.Public, "Public")]
        public void QuizMode_ToString_ReturnsExpectedValue(QuizMode mode, string expected)
        {
            // act
            var result = mode.ToString();

            // assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Private", true)]
        [InlineData("Public", true)]
        [InlineData("private", true)]
        [InlineData("public", true)]
        [InlineData("Invalid", false)]
        [InlineData("", false)]
        public void QuizMode_TryParse_HandlesInputCorrectly(string input, bool shouldParse)
        {
            // act
            var result = Enum.TryParse<QuizMode>(input, true, out var mode);

            // assert
            Assert.Equal(shouldParse, result);
        }
    }
}
